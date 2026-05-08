using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Chamber19.AutoCad.Mcp.Diagnostics;
using Chamber19.AutoCad.Mcp.Threading;

namespace Chamber19.AutoCad.Mcp.Hosting;

/// <summary>
/// Shell-side bootstrap that loads the host assembly into <see cref="McpHostLoadContext"/>
/// and calls into it reflectively. Owns port allocation, token generation, port file
/// lifecycle, and all state needed by <c>MCPSTATUS</c>.
/// </summary>
internal static class McpHostBootstrap
{
    private static readonly object SyncRoot = new();
    private static string? _boundUrl;
    private static string? _bearerToken;
    private static DateTimeOffset _startedAt;
    private static string? _bootError;
    private static int _processId;
    private static bool _started;

    public static void Start()
    {
        lock (SyncRoot)
        {
            if (_started)
            {
                return;
            }
        }

        try
        {
            Log.Truncate();
            Log.Write("== McpHostBootstrap.Start() ==");

            Version? acadVersion = null;
            try
            {
                acadVersion = Autodesk.AutoCAD.ApplicationServices.Application.Version;
            }
            catch (Exception ex)
            {
                Log.WriteException("Could not read Application.Version for compatibility check.", ex);
            }
            if (!AutoCadCompatibility.IsSupported(acadVersion))
            {
                var desc = AutoCadCompatibility.Describe(acadVersion);
                lock (SyncRoot) { _bootError = desc; }
                Log.Write($"Refusing to start MCP host: {desc}");
                return;
            }
            Log.Write($"Compat check OK: {AutoCadCompatibility.Describe(acadVersion)}");

            var port = PortAllocator.FindFreePort(IPAddress.Loopback);
            if (port is null)
            {
                var err = $"No free port available in range {PortAllocator.RangeStart}-{PortAllocator.RangeEnd}.";
                lock (SyncRoot) { _bootError = err; }
                Log.Write(err);
                return;
            }

            var token = GenerateToken();
            var url = $"http://127.0.0.1:{port}/";
            Log.Write($"Allocated port {port}, generated bearer token (length={token.Length}).");

            var hostDllPath = ResolveHostDllPath();
            if (hostDllPath is null)
            {
                var err = "Could not locate Chamber19.AutoCad.Mcp.Host.dll.";
                lock (SyncRoot) { _bootError = err; }
                Log.Write(err);
                return;
            }
            Log.Write($"Loading host from: {hostDllPath}");

            var alc = new McpHostLoadContext(hostDllPath);
            var hostAssembly = alc.LoadFromAssemblyPath(hostDllPath);
            var entryType = hostAssembly.GetType("Chamber19.AutoCad.Mcp.Host.McpHostEntry", throwOnError: true)!;

            var startMethod = entryType.GetMethod("StartHost",
                BindingFlags.Public | BindingFlags.Static,
                new[]
                {
                    typeof(string),                           // url
                    typeof(string),                           // token
                    typeof(string),                           // acadVersionStr
                    typeof(string),                           // logPath
                    typeof(Func<Func<object?>, Task<object?>>), // dispatcher
                    typeof(Func<int>),                        // getQueueDepth
                    typeof(Func<int>),                        // getQueueCapacity
                    typeof(Action<string>),                   // statusCallback
                })!;

            Func<Func<object?>, Task<object?>> dispatcherBridge =
                func => AutoCadThreadDispatcher.InvokeOnApplicationThreadAsync<object?>(() => func());

            var startedAt = DateTimeOffset.UtcNow;
            var acadVersionStr = acadVersion?.ToString() ?? "unknown";
            var ok = (bool)startMethod.Invoke(null, new object?[]
            {
                url,
                token,
                acadVersionStr,
                Log.Path,
                dispatcherBridge,
                (Func<int>)(() => AutoCadThreadDispatcher.QueueDepth),
                (Func<int>)(() => AutoCadThreadDispatcher.QueueCapacity),
                (Action<string>)(msg => Log.Write($"[host-bootstrap] {msg}")),
            })!;

            if (!ok)
            {
                var err = "McpHostEntry.StartHost() returned false (see log).";
                lock (SyncRoot) { _bootError = err; }
                return;
            }

            using var process = Process.GetCurrentProcess();
            var pid = process.Id;
            PortFile.Write(url, token, pid, startedAt);
            Log.Write($"Port file written to {PortFile.GetPath(pid)}");

            lock (SyncRoot)
            {
                _started = true;
                _boundUrl = url;
                _bearerToken = token;
                _startedAt = startedAt;
                _processId = pid;
                _bootError = null;
            }

            Log.Write("== McpHostBootstrap.Start() exiting cleanly ==");
        }
        catch (Exception ex)
        {
            var err = ex.ToString();
            lock (SyncRoot) { _bootError = err; }
            Log.WriteException("McpHostBootstrap.Start() failed.", ex);
        }
    }

    public static void Stop()
    {
        int pid;
        lock (SyncRoot)
        {
            pid = _processId;
            _started = false;
            _boundUrl = null;
            _bearerToken = null;
            _processId = 0;
        }

        Log.Write("== McpHostBootstrap.Stop() ==");

        if (pid > 0)
        {
            PortFile.Delete(pid);
            Log.Write($"Port file at {PortFile.GetPath(pid)} deleted (if present).");
        }

        // Reflectively call StopHost on the already-loaded host assembly via the named ALC.
        try
        {
            foreach (var alc in System.Runtime.Loader.AssemblyLoadContext.All)
            {
                if (alc.Name == "chamber19-mcp-host")
                {
                    foreach (var asm in alc.Assemblies)
                    {
                        if (asm.GetName().Name == "Chamber19.AutoCad.Mcp.Host")
                        {
                            var entryType = asm.GetType("Chamber19.AutoCad.Mcp.Host.McpHostEntry");
                            var stopMethod = entryType?.GetMethod("StopHost",
                                BindingFlags.Public | BindingFlags.Static);
                            stopMethod?.Invoke(null, null);
                            return;
                        }
                    }
                }
            }
            Log.Write("McpHostBootstrap.Stop(): host ALC not found (host may not have started).");
        }
        catch (Exception ex)
        {
            Log.WriteException("McpHostBootstrap.Stop() reflective call failed.", ex);
        }
    }

    public static StatusSnapshot GetStatus()
    {
        lock (SyncRoot)
        {
            var portFilePath = _processId > 0
                ? PortFile.GetPath(_processId)
                : PortFile.DirectoryPath;
            return new StatusSnapshot(
                Running: _started,
                BoundUrl: _boundUrl,
                StartedAt: _started ? _startedAt : null,
                TokenLength: _bearerToken?.Length,
                PortFilePath: portFilePath,
                LogPath: Log.Path,
                BootError: _bootError);
        }
    }

    public sealed record StatusSnapshot(
        bool Running,
        string? BoundUrl,
        DateTimeOffset? StartedAt,
        int? TokenLength,
        string PortFilePath,
        string LogPath,
        string? BootError);

    private static string? ResolveHostDllPath()
    {
        // The host DLL is staged in private/ next to the shell DLL (bundle layout).
        var shellPath = typeof(McpHostBootstrap).Assembly.Location;
        var shellDir = Path.GetDirectoryName(shellPath);
        if (shellDir is not null)
        {
            var candidate = Path.Combine(shellDir, "private", "Chamber19.AutoCad.Mcp.Host.dll");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            // During dev (NETLOAD from bin/), the host is in the same output directory.
            candidate = Path.Combine(shellDir, "Chamber19.AutoCad.Mcp.Host.dll");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
        return null;
    }

    private static string GenerateToken()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}
