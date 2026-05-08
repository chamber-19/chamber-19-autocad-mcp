// Stub for Autodesk.AutoCAD.Runtime (accoremgd) — CI builds only.
#pragma warning disable CS8618
namespace Autodesk.AutoCAD.Runtime;

public interface IExtensionApplication
{
    void Initialize();
    void Terminate();
}

[AttributeUsage(AttributeTargets.Assembly)]
public class ExtensionApplicationAttribute : Attribute
{
    public ExtensionApplicationAttribute(Type type) { }
}

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public class CommandClassAttribute : Attribute
{
    public CommandClassAttribute(Type type) { }
}

[AttributeUsage(AttributeTargets.Method)]
public class CommandMethodAttribute : Attribute
{
    public CommandMethodAttribute(string globalName) { }
    public CommandMethodAttribute(string globalName, CommandFlags flags) { }
}

[Flags]
public enum CommandFlags
{
    None = 0,
    Modal = 1,
    Transparent = 2,
    UsePickSet = 4,
    Redraw = 8,
    NoPerspective = 16,
    NoMultiple = 32,
    NoTileMode = 64,
    NoPaperSpace = 128,
    Plot = 256,
    Session = 512,
    Interruptible = 1024,
    NoInternalLock = 2048,
    DocReadLock = 8192,
    DocExclusiveLock = 16384,
    NoUndo = 65536,
    NoBlockEditor = 131072,
    NoActionRecording = 262144,
    ActionMacro = 524288,
    Defun = 8388608,
    TempShowDynInput = 33554432,
    NoDynInput = 67108864,
}
