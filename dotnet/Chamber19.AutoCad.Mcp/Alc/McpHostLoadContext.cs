using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace Chamber19.AutoCad.Mcp;

/// <summary>
/// Custom <see cref="AssemblyLoadContext"/> that isolates the MCP host assembly and its
/// private dependencies from the default ALC.
/// <para>
/// This prevents version conflicts with Autodesk's <c>acmcp.dll</c>, which loads the same
/// <c>ModelContextProtocol</c> SDK at a potentially different version in the default ALC.
/// </para>
/// </summary>
/// <remarks>
/// <b>Fallback policy:</b>
/// <list type="bullet">
///   <item><description><c>System.*</c>, <c>Microsoft.*</c>, <c>netstandard</c>, <c>mscorlib</c>
///     — shared runtime assemblies, fall back to default ALC.</description></item>
///   <item><description><c>accoremgd</c>, <c>acdbmgd</c>, <c>acmgd</c> — AutoCAD managed DLLs
///     already loaded by acad.exe, fall back to default ALC.</description></item>
///   <item><description><c>Chamber19.AutoCad.Mcp</c> (the shell assembly) — must be the same
///     instance as in the default ALC so cross-ALC object references work. Falls back.</description></item>
///   <item><description>Everything else — resolved from the <c>private/</c> sub-directory
///     adjacent to the host DLL via <see cref="AssemblyDependencyResolver"/>.</description></item>
/// </list>
/// </remarks>
internal sealed class McpHostLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    internal McpHostLoadContext(string hostDllPath)
        : base(name: "chamber19-mcp-host", isCollectible: false)
    {
        _resolver = new AssemblyDependencyResolver(hostDllPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var name = assemblyName.Name ?? string.Empty;

        // Shared runtime + AutoCAD DLLs + shell assembly: fall back to default ALC.
        if (name.StartsWith("System.", StringComparison.Ordinal) ||
            name.StartsWith("Microsoft.", StringComparison.Ordinal) ||
            name.Equals("netstandard", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("mscorlib", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("accoremgd", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("acdbmgd", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("acmgd", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Chamber19.AutoCad.Mcp", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Resolve from the private/ directory where the host DLL and its deps live.
        var resolvedPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (resolvedPath is not null)
        {
            return LoadFromAssemblyPath(resolvedPath);
        }

        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var resolvedPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (resolvedPath is not null)
        {
            return LoadUnmanagedDllFromPath(resolvedPath);
        }
        return IntPtr.Zero;
    }
}
