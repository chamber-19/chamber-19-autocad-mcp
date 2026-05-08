using System;
using System.ComponentModel;
using System.Text.Json;
using Chamber19.AutoCad.Mcp.Hosting;
using ModelContextProtocol.Server;

namespace Chamber19.AutoCad.Mcp.Tools;

[McpServerToolType]
public static class PingTool
{
    [McpServerTool(Name = "chamber19_ping")]
    [Description("Synthetic ping that returns plugin and runtime info without touching the drawing.")]
    public static string Ping()
    {
        var snapshot = PluginSnapshot.Current;
        var payload = new
        {
            ok = true,
            autocadVersion = snapshot.AutoCadVersion,
            plugin = snapshot.Plugin,
            ts = DateTimeOffset.UtcNow.ToString("O"),
        };
        return JsonSerializer.Serialize(payload);
    }
}
