using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Exception = System.Exception;

[assembly: CommandClass(typeof(Chamber19.AutoCad.Mcp.Spike.SpikeCommands))]
[assembly: ExtensionApplication(typeof(Chamber19.AutoCad.Mcp.Spike.SpikeExtension))]

namespace Chamber19.AutoCad.Mcp.Spike;

public sealed class SpikeExtension : IExtensionApplication
{
    internal static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Chamber19", "autocad-mcp-spike", "spike.log"
    );

    private static readonly object SyncRoot = new();
    private static WebApplication? _app;
    private static string? _boundUrl;
    private static string? _bootError;

    public void Initialize()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            // Truncate prior log for a clean read.
            File.WriteAllText(LogPath, string.Empty);

            Log("== Initialize() entered ==");
            using (var proc = Process.GetCurrentProcess())
            {
                Log($"Process: {proc.ProcessName} pid={proc.Id}");
            }
            Log($"Runtime: {RuntimeInformation.FrameworkDescription}");
            Log($"OS: {RuntimeInformation.OSDescription}");
            Log($"BaseDir: {AppContext.BaseDirectory}");
            Log($"PluginDir: {Path.GetDirectoryName(typeof(SpikeExtension).Assembly.Location)}");

            Log("Calling WebApplication.CreateBuilder() ...");
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.Logging.SetMinimumLevel(LogLevel.Warning);
            // Bind any free port on loopback only.
            builder.WebHost.UseUrls("http://127.0.0.1:0");

            Log("Calling builder.Build() ...");
            var app = builder.Build();

            app.MapGet("/health", () => Results.Json(new
            {
                ok = true,
                source = "Chamber19.AutoCad.Mcp.Spike",
                ts = DateTimeOffset.UtcNow,
            }));

            Log("Calling app.StartAsync() ...");
            app.StartAsync().GetAwaiter().GetResult();

            var addresses = app.Services
                .GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>();
            var bound = addresses?.Addresses.FirstOrDefault() ?? "<unknown>";

            lock (SyncRoot)
            {
                _app = app;
                _boundUrl = bound;
                _bootError = null;
            }

            Log($"Kestrel STARTED, bound to: {bound}");
            Log("== Initialize() exiting cleanly ==");
        }
        catch (Exception ex)
        {
            _bootError = ex.ToString();
            Log("Kestrel FAILED to start.");
            LogException(ex);
        }
    }

    public void Terminate()
    {
        try
        {
            Log("== Terminate() entered ==");
            WebApplication? app;
            lock (SyncRoot)
            {
                app = _app;
                _app = null;
                _boundUrl = null;
            }

            if (app is null)
            {
                Log("No running app to stop.");
                return;
            }

            using var stopCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3));
            app.StopAsync(stopCts.Token).GetAwaiter().GetResult();
            ((IAsyncDisposable)app).DisposeAsync().AsTask().GetAwaiter().GetResult();
            Log("Kestrel stopped cleanly.");
        }
        catch (Exception ex)
        {
            Log("Terminate failed.");
            LogException(ex);
        }
    }

    internal static (bool running, string? boundUrl, string? error) Status()
    {
        lock (SyncRoot)
        {
            return (_app is not null, _boundUrl, _bootError);
        }
    }

    internal static void Log(string line)
    {
        try
        {
            var formatted = $"[{DateTimeOffset.UtcNow:O}] [tid={Environment.CurrentManagedThreadId,3}] {line}{Environment.NewLine}";
            File.AppendAllText(LogPath, formatted);
            Trace.WriteLine($"[Chamber19.Spike] {line}");
        }
        catch
        {
            // Logging is best-effort.
        }
    }

    private static void LogException(Exception ex)
    {
        var current = ex;
        var depth = 0;
        while (current is not null)
        {
            Log($"Exception[{depth}] {current.GetType().FullName}: {current.Message}");
            if (!string.IsNullOrEmpty(current.StackTrace))
            {
                Log(current.StackTrace);
            }
            current = current.InnerException;
            depth++;
        }
    }
}

public sealed class SpikeCommands
{
    [CommandMethod("MCPSPIKESTATUS", CommandFlags.Session)]
    public void Status()
    {
        var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
        var (running, boundUrl, err) = SpikeExtension.Status();

        string msg;
        if (running)
        {
            msg = $"[Spike] Kestrel running at {boundUrl ?? "<unknown>"}. Log: {SpikeExtension.LogPath}";
        }
        else if (err is not null)
        {
            var firstLine = err.Split('\n', 2)[0].Trim();
            msg = $"[Spike] Kestrel FAILED: {firstLine}. Full log: {SpikeExtension.LogPath}";
        }
        else
        {
            msg = $"[Spike] Plugin loaded but Kestrel state unknown. Log: {SpikeExtension.LogPath}";
        }

        ed?.WriteMessage("\n" + msg + "\n");
    }
}
