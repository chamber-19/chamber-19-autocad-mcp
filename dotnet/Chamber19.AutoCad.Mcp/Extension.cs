using Autodesk.AutoCAD.Runtime;
using Chamber19.AutoCad.Mcp.Diagnostics;
using Chamber19.AutoCad.Mcp.Hosting;
using Chamber19.AutoCad.Mcp.Threading;

[assembly: ExtensionApplication(typeof(Chamber19.AutoCad.Mcp.Extension))]

namespace Chamber19.AutoCad.Mcp;

public sealed class Extension : IExtensionApplication
{
    public void Initialize()
    {
        Log.Write("Extension.Initialize() entered.");
        // Spec ordering: start the MCP host first, then attach the dispatcher's idle handler.
        // The brief race between host-listening and dispatcher-ready is accepted; in practice
        // no client races us in the few microseconds it takes.
        McpServerHost.Start();
        AutoCadThreadDispatcher.Initialize();
    }

    public void Terminate()
    {
        Log.Write("Extension.Terminate() entered.");
        // Spec ordering: detach the dispatcher's idle handler and cancel pending callbacks
        // FIRST so any in-flight tool requests fault fast, then stop the host so Kestrel
        // drains its request pipeline.
        AutoCadThreadDispatcher.Shutdown();
        McpServerHost.Stop();
    }
}
