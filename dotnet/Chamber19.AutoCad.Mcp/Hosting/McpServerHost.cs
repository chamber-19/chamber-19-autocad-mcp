using System;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using Chamber19.AutoCad.Mcp.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Chamber19.AutoCad.Mcp.Hosting;

internal static class McpServerHost
{
    private static readonly object SyncRoot = new();
    private static WebApplication? _app;
    private static string? _boundUrl;
    private static string? _bearerToken;
    private static DateTimeOffset _startedAt;
    private static string? _bootError;

    public static void Start()
    {
        lock (SyncRoot)
        {
            if (_app is not null)
            {
                return;
            }
        }

        try
        {
            Log.Truncate();
            Log.Write("== McpServerHost.Start() ==");

            CapturePluginSnapshot();

            var port = PortAllocator.FindFreePort(IPAddress.Loopback);
            if (port is null)
            {
                _bootError = $"No free port available in range {PortAllocator.RangeStart}-{PortAllocator.RangeEnd}.";
                Log.Write(_bootError);
                return;
            }

            var token = GenerateToken();
            var url = $"http://127.0.0.1:{port}/";
            Log.Write($"Allocated port {port}, generated bearer token (length={token.Length}).");

            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.Logging.SetMinimumLevel(LogLevel.Warning);
            builder.WebHost.UseUrls($"http://127.0.0.1:{port}");

            // Tracked: https://github.com/chamber-19/chamber-19-autocad-mcp/issues/4
            // serverInfo is reported as the SDK's default ("ModelContextProtocol.Core") here.
            // Configuring it via AddMcpServer(options => options.ServerInfo = new Implementation { ... })
            // silently regressed request handling (200 with empty body on every MCP request).
            builder.Services
                .AddMcpServer()
                .WithHttpTransport(options => options.Stateless = true)
                .WithToolsFromAssembly();

            var app = builder.Build();
            app.UseBearerAuth(token);
            app.MapMcp();

            Log.Write("Calling app.StartAsync() ...");
            app.StartAsync().GetAwaiter().GetResult();
            Log.Write($"Kestrel bound at {url}");

            using var process = Process.GetCurrentProcess();
            var startedAt = DateTimeOffset.UtcNow;
            PortFile.Write(url, token, process.Id, startedAt);
            Log.Write($"Port file written to {PortFile.Path}");

            lock (SyncRoot)
            {
                _app = app;
                _boundUrl = url;
                _bearerToken = token;
                _startedAt = startedAt;
                _bootError = null;
            }

            Log.Write("== McpServerHost.Start() exiting cleanly ==");
        }
        catch (Exception ex)
        {
            _bootError = ex.ToString();
            Log.WriteException("McpServerHost.Start() failed.", ex);
        }
    }

    public static void Stop()
    {
        WebApplication? app;
        lock (SyncRoot)
        {
            app = _app;
            _app = null;
            _boundUrl = null;
            _bearerToken = null;
        }

        Log.Write("== McpServerHost.Stop() ==");

        // Delete the port file FIRST. AutoCAD's plugin-terminate window is short and may force-kill
        // the process before StopAsync completes; deleting the discovery file up front guarantees
        // external readers can't see stale entries pointing at a dead process even in that case.
        PortFile.Delete();
        Log.Write($"Port file at {PortFile.Path} deleted (if present).");

        if (app is not null)
        {
            try
            {
                using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                app.StopAsync(stopCts.Token).GetAwaiter().GetResult();
                ((IAsyncDisposable)app).DisposeAsync().AsTask().GetAwaiter().GetResult();
                Log.Write("Kestrel stopped cleanly.");
            }
            catch (Exception ex)
            {
                Log.WriteException("Kestrel shutdown failed.", ex);
            }
        }
        else
        {
            Log.Write("No running app to stop.");
        }
    }

    public static StatusSnapshot GetStatus()
    {
        lock (SyncRoot)
        {
            return new StatusSnapshot(
                Running: _app is not null,
                BoundUrl: _boundUrl,
                StartedAt: _app is not null ? _startedAt : null,
                TokenLength: _bearerToken?.Length,
                PortFilePath: PortFile.Path,
                LogPath: Log.Path,
                BootError: _bootError);
        }
    }

    private static void CapturePluginSnapshot()
    {
        var assembly = typeof(McpServerHost).Assembly;
        var assemblyName = assembly.GetName();
        var pluginVersion = assemblyName.Version?.ToString() ?? "0.0.0";
        var pluginLabel = $"{assemblyName.Name} v{pluginVersion}";

        var runtime = RuntimeInformation.FrameworkDescription;

        string autocadVersion;
        try
        {
            autocadVersion = Application.Version?.ToString() ?? "unknown";
        }
        catch (Exception ex)
        {
            autocadVersion = "unavailable";
            Log.WriteException("Could not read Application.Version.", ex);
        }

        PluginSnapshot.Set(new PluginSnapshot(autocadVersion, pluginLabel, runtime));
        Log.Write($"Plugin snapshot: autocad={autocadVersion}, plugin={pluginLabel}, runtime={runtime}");
    }

    private static string GenerateToken()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    public sealed record StatusSnapshot(
        bool Running,
        string? BoundUrl,
        DateTimeOffset? StartedAt,
        int? TokenLength,
        string PortFilePath,
        string LogPath,
        string? BootError);
}
