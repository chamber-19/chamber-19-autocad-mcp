// Stub implementation of Autodesk.AutoCAD.EditorInput for CI builds.
// All members throw NotImplementedException at runtime; only compilation is guaranteed.
#pragma warning disable CS8618

using Autodesk.AutoCAD.DatabaseServices;

namespace Autodesk.AutoCAD.EditorInput;

public class Editor
{
    public PromptSelectionResult SelectAll(SelectionFilter filter) => throw new NotImplementedException();
    public void WriteMessage(string message) { }
    public void WriteMessage(string message, params object[] parameter) { }
}

public class PromptSelectionResult
{
    public PromptStatus Status { get; }
    public SelectionSet Value { get; } = null!;
}

public enum PromptStatus
{
    OK = 5100,
    Error = -5001,
    Cancel = -5002,
    Keyword = -5005,
    Modeless = 5027,
    None = 5000,
}

public class SelectionSet : IDisposable
{
    public int Count { get; }
    public void Dispose() { }
}
