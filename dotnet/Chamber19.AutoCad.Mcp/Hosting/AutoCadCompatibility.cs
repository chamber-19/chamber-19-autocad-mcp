using System;

namespace Chamber19.AutoCad.Mcp.Hosting;

/// <summary>
/// Pure runtime check for whether the host AutoCAD version is supported.
/// </summary>
/// <remarks>
/// Floor is AutoCAD 2025 (R24.0). The mapping is:
///   AutoCAD 2024 = R23.0  (unsupported)
///   AutoCAD 2025 = R24.0  (supported)
///   AutoCAD 2026 = R25.0  (supported)
///   AutoCAD 2027 = R26.0  (supported, primary)
/// Compile-time targeting is enforced by the csproj's TFM rule; this runtime check
/// guards the case where someone copies a build to a wrong AutoCAD or a future
/// AutoCAD changes things in a way that breaks the assumption.
/// </remarks>
internal static class AutoCadCompatibility
{
    /// <summary>R24.0 = AutoCAD 2025. Below this we refuse to start the MCP host.</summary>
    public const int MinimumSupportedMajor = 24;

    public static bool IsSupported(Version? version) =>
        version is not null && version.Major >= MinimumSupportedMajor;

    public static string Describe(Version? version)
    {
        if (version is null)
        {
            return $"AutoCAD version: unknown (floor R{MinimumSupportedMajor}.0).";
        }
        if (version.Major >= MinimumSupportedMajor)
        {
            return $"AutoCAD R{version} (supported; floor R{MinimumSupportedMajor}.0).";
        }
        return $"AutoCAD R{version} is below the supported floor (R{MinimumSupportedMajor}.0+).";
    }
}
