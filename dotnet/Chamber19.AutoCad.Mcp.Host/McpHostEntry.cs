using System;
using System.Threading.Tasks;
using Chamber19.AutoCad.Mcp.Diagnostics;
using Chamber19.AutoCad.Mcp.Hosting;
using Chamber19.AutoCad.Mcp.Threading;

namespace Chamber19.AutoCad.Mcp.Host;

/// <summary>
/// Public entry point for the host assembly. Called reflectively by the shell's
/// <c>McpHostBootstrap</c> across the AssemblyLoadContext boundary.
/// All parameters and return values are primitive / shared-runtime types only.
/// </summary>
public static class McpHostEntry
{
    /// <summary>
    /// Starts the MCP HTTP server.
    /// </summary>
    /// <param name="url">The URL to bind Kestrel to (e.g. "http://127.0.0.1:5001/").</param>
    /// <param name="token">Bearer token to use for authentication.</param>
    /// <param name="acadVersionStr">AutoCAD version string for display/logging.</param>
    /// <param name="logPath">Log file path (same file as the shell's Log).</param>
    /// <param name="dispatcher">
    ///   Delegate that marshals a <c>Func&lt;object?&gt;</c> onto the AutoCAD application
    ///   thread and returns a <c>Task&lt;object?&gt;</c>. Provided by the shell.
    /// </param>
    /// <param name="getQueueDepth">Returns the current dispatcher queue depth.</param>
    /// <param name="getQueueCapacity">Returns the dispatcher queue capacity.</param>
    /// <param name="statusCallback">Receives human-readable progress/error messages.</param>
    /// <returns><c>true</c> on success; <c>false</c> on failure.</returns>
    public static bool StartHost(
        string url,
        string token,
        string acadVersionStr,
        string logPath,
        Func<Func<object?>, Task<object?>> dispatcher,
        Func<int> getQueueDepth,
        Func<int> getQueueCapacity,
        Action<string> statusCallback)
    {
        try
        {
            HostLog.Initialize(logPath);
            HostLog.Write("== McpHostEntry.StartHost() ==");

            statusCallback("Initializing dispatcher...");
            HostDispatcher.Initialize(dispatcher);

            statusCallback("Starting Kestrel...");
            var ok = McpServerHost.Start(url, token, acadVersionStr, getQueueDepth, getQueueCapacity);
            if (ok)
            {
                statusCallback($"Host started at {url}");
                HostLog.Write("== McpHostEntry.StartHost() exiting cleanly ==");
            }
            else
            {
                statusCallback("Host failed to start (see log).");
            }
            return ok;
        }
        catch (Exception ex)
        {
            var msg = $"McpHostEntry.StartHost() threw: {ex.Message}";
            statusCallback(msg);
            try { HostLog.WriteException("McpHostEntry.StartHost() threw.", ex); } catch { }
            return false;
        }
    }

    /// <summary>Stops the MCP HTTP server.</summary>
    public static void StopHost()
    {
        try
        {
            HostLog.Write("== McpHostEntry.StopHost() ==");
            McpServerHost.Stop();
        }
        catch (Exception ex)
        {
            try { HostLog.WriteException("McpHostEntry.StopHost() threw.", ex); } catch { }
        }
    }
}
