using System;
using Chamber19.AutoCad.Mcp.Hosting;
using Xunit;

namespace Chamber19.AutoCad.Mcp.Tests;

/// <summary>
/// Pure version-predicate tests for <see cref="AutoCadCompatibility"/>. The runtime
/// integration (calling <c>Application.Version</c> and refusing to start when unsupported)
/// requires integration testing against a running AutoCAD instance; here we lock in the rule.
/// Floor: AutoCAD 2025 = R24.0.
/// </summary>
public sealed class AutoCadCompatibilityTests
{
    [Theory]
    [InlineData(24, true)]   // AutoCAD 2025 — at the floor
    [InlineData(25, true)]   // AutoCAD 2026
    [InlineData(26, true)]   // AutoCAD 2027 — primary
    [InlineData(27, true)]   // hypothetical future AutoCAD 2028
    [InlineData(23, false)]  // AutoCAD 2024 — below the floor
    [InlineData(22, false)]  // AutoCAD 2023
    [InlineData(0, false)]
    public void IsSupported_RespectsMajorFloor(int major, bool expected)
    {
        var version = new Version(major, 0, 0, 0);
        Assert.Equal(expected, AutoCadCompatibility.IsSupported(version));
    }

    [Fact]
    public void IsSupported_NullVersion_False()
    {
        Assert.False(AutoCadCompatibility.IsSupported(null));
    }

    [Fact]
    public void Describe_NullVersion_MentionsUnknownAndFloor()
    {
        var msg = AutoCadCompatibility.Describe(null);
        Assert.Contains("unknown", msg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"R{AutoCadCompatibility.MinimumSupportedMajor}.0", msg);
    }

    [Fact]
    public void Describe_SupportedVersion_MentionsVersionAndFloor()
    {
        var msg = AutoCadCompatibility.Describe(new Version(26, 0, 0, 0));
        Assert.Contains("R26.0.0.0", msg);
        Assert.Contains("supported", msg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"R{AutoCadCompatibility.MinimumSupportedMajor}.0", msg);
    }

    [Fact]
    public void Describe_BelowFloor_FlagsAsUnsupported()
    {
        var msg = AutoCadCompatibility.Describe(new Version(23, 0, 0, 0));
        Assert.Contains("R23.0.0.0", msg);
        Assert.Contains("below", msg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"R{AutoCadCompatibility.MinimumSupportedMajor}.0+", msg);
    }
}
