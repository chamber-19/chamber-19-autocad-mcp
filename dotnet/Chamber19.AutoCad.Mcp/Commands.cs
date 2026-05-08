using System.Diagnostics;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
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

        using var process = Process.GetCurrentProcess();
        var assembly = typeof(Commands).Assembly;
        var assemblyName = assembly.GetName();
        var version = assemblyName.Version?.ToString() ?? "0.0.0";
        var runtime = RuntimeInformation.FrameworkDescription;

        editor.WriteMessage(
            $"\n[Chamber19] MCP server not yet wired (commit 1 shell). " +
            $"Plugin {assemblyName.Name} v{version} loaded into {process.ProcessName} (pid={process.Id}) on {runtime}.\n"
        );
    }
}
