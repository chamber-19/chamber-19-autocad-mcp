using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using Chamber19.AutoCad.Mcp.Hosting;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(Chamber19.AutoCad.Mcp.Commands))]

namespace Chamber19.AutoCad.Mcp;

public sealed class Commands
{
    [CommandMethod("MCPSTATUS", CommandFlags.Session)]
    public void Status()
    {
        var editor = Application.DocumentManager.MdiActiveDocument?.Editor;
        if (editor is null)
        {
            return;
        }

        var status = McpServerHost.GetStatus();

        if (status.Running)
        {
            editor.WriteMessage(
                $"\n[Chamber19] MCP server RUNNING.\n" +
                $"  URL:        {status.BoundUrl}\n" +
                $"  Started:    {status.StartedAt:O}\n" +
                $"  Auth:       Bearer (token length {status.TokenLength}, see port file)\n" +
                $"  Port file:  {status.PortFilePath}\n" +
                $"  Log file:   {status.LogPath}\n");
        }
        else if (status.BootError is not null)
        {
            var firstLine = status.BootError.Split('\n', 2)[0].Trim();
            editor.WriteMessage(
                $"\n[Chamber19] MCP server FAILED to start.\n" +
                $"  Error:    {firstLine}\n" +
                $"  Log file: {status.LogPath}\n");
        }
        else
        {
            editor.WriteMessage(
                $"\n[Chamber19] MCP server NOT RUNNING. Log file: {status.LogPath}\n");
        }
    }
}
