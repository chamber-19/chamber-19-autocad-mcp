using Autodesk.AutoCAD.Runtime;
using Chamber19.AutoCad.Mcp.Diagnostics;
using Chamber19.AutoCad.Mcp.Hosting;

[assembly: ExtensionApplication(typeof(Chamber19.AutoCad.Mcp.Extension))]

namespace Chamber19.AutoCad.Mcp;

public sealed class Extension : IExtensionApplication
{
    public void Initialize()
    {
        Log.Write("Extension.Initialize() entered.");
        McpServerHost.Start();
    }

    public void Terminate()
    {
        Log.Write("Extension.Terminate() entered.");
        McpServerHost.Stop();
    }
}
