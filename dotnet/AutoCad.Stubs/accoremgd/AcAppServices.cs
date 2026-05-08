// Stub for Autodesk.AutoCAD.ApplicationServices (accoremgd) — CI builds only.
#pragma warning disable CS8618
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace Autodesk.AutoCAD.ApplicationServices;

public class Application
{
    public static DocumentCollection DocumentManager { get; } = new DocumentCollection();
    public static Version Version { get; } = new Version(26, 0, 0, 0);
    public static event EventHandler? Idle;
    public static object GetSystemVariable(string name) => throw new NotImplementedException();
}

public class DocumentCollection
{
    public Document? MdiActiveDocument { get; }
}

public class Document
{
    public Database Database { get; } = null!;
    public Editor Editor { get; } = null!;
    public string Name { get; } = string.Empty;
}
