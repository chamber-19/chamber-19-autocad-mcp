using System.Diagnostics;
using Autodesk.AutoCAD.Runtime;

[assembly: ExtensionApplication(typeof(Chamber19.AutoCad.Mcp.Extension))]

namespace Chamber19.AutoCad.Mcp;

public sealed class Extension : IExtensionApplication
{
    public void Initialize()
    {
        Trace.WriteLine("[Chamber19.AutoCad.Mcp] Initialize() — commit 1 shell, MCP server not yet wired.");
    }

    public void Terminate()
    {
        Trace.WriteLine("[Chamber19.AutoCad.Mcp] Terminate().");
    }
}
