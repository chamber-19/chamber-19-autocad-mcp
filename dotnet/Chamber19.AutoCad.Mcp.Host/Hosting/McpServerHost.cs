using System;
using System.Runtime.InteropServices;
using System.Threading;
using Chamber19.AutoCad.Mcp.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Chamber19.AutoCad.Mcp.Hosting;

internal static class McpServerHost
{
    private static readonly object SyncRoot = new();
    private static WebApplication? _app;

    public static bool Start(
        string url,
        string token,
        string acadVersionStr,
        Func<int> getQueueDepth,
        Func<int> getQueueCapacity)
    {
        lock (SyncRoot)
        {
            if (_app is not null)
            {
                return true;
            }
        }

        try
        {
            HostLog.Write($"McpServerHost.Start() url={url} token_len={token.Length}");

            CapturePluginSnapshot(acadVersionStr);

            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.Logging.SetMinimumLevel(LogLevel.Warning);
            builder.WebHost.UseUrls(url);

            // Tracked: https://github.com/chamber-19/chamber-19-autocad-mcp/issues/4
            builder.Services
                .AddMcpServer()
                .WithHttpTransport(options => options.Stateless = true)
                .WithToolsFromAssembly(typeof(McpServerHost).Assembly);

            var app = builder.Build();
            app.UseBearerAuth(token);
            app.UseBackpressure(getQueueDepth, getQueueCapacity);
            app.MapMcp("/mcp");

            HostLog.Write("Calling app.StartAsync() ...");
            app.StartAsync().GetAwaiter().GetResult();
            HostLog.Write($"Kestrel started at {url}");

            lock (SyncRoot)
            {
                _app = app;
            }

            return true;
        }
        catch (Exception ex)
        {
            HostLog.WriteException("McpServerHost.Start() failed.", ex);
            return false;
        }
    }

    public static void Stop()
    {
        WebApplication? app;
        lock (SyncRoot)
        {
            app = _app;
            _app = null;
        }

        HostLog.Write("McpServerHost.Stop()");

        if (app is not null)
        {
            try
            {
                using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                app.StopAsync(stopCts.Token).GetAwaiter().GetResult();
                ((IAsyncDisposable)app).DisposeAsync().AsTask().GetAwaiter().GetResult();
                HostLog.Write("Kestrel stopped cleanly.");
            }
            catch (Exception ex)
            {
                HostLog.WriteException("Kestrel shutdown failed.", ex);
            }
        }
        else
        {
            HostLog.Write("No running app to stop.");
        }
    }

    private static void CapturePluginSnapshot(string acadVersionStr)
    {
        var assembly = typeof(McpServerHost).Assembly;
        var assemblyName = assembly.GetName();
        var pluginVersion = assemblyName.Version?.ToString() ?? "0.0.0";
        var pluginLabel = $"{assemblyName.Name} v{pluginVersion}";
        var runtime = RuntimeInformation.FrameworkDescription;
        PluginSnapshot.Set(new PluginSnapshot(acadVersionStr, pluginLabel, runtime));
        HostLog.Write($"Plugin snapshot: autocad={acadVersionStr}, plugin={pluginLabel}, runtime={runtime}");
    }
}
