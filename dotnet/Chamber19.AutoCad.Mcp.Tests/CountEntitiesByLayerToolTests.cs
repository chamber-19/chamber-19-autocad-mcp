using System;
using System.Linq;
using System.Text.Json;
using Chamber19.AutoCad.Mcp.Tools;
using Xunit;

namespace Chamber19.AutoCad.Mcp.Tests;

/// <summary>
/// Tests <see cref="CountEntitiesByLayerTool.Serialize"/> with mocked counts representing what
/// the AutoCAD selection-set read path would have produced.
/// The actual AutoCAD-touching read path (<c>CountEntities</c>) requires integration testing
/// against a running AutoCAD instance; here we lock in the JSON shape clients depend on.
/// </summary>
public sealed class CountEntitiesByLayerToolTests
{
    [Fact]
    public void Serialize_ZeroCount_ReturnsZeroAndTimestamp()
    {
        var ts = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);

        var json = CountEntitiesByLayerTool.Serialize(0, ts);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(0, doc.RootElement.GetProperty("count").GetInt32());
        Assert.Equal(ts.ToString("O"), doc.RootElement.GetProperty("ts").GetString());
    }

    [Fact]
    public void Serialize_PreservesFieldOrder_OnRootElement()
    {
        var json = CountEntitiesByLayerTool.Serialize(42, DateTimeOffset.UtcNow);

        using var doc = JsonDocument.Parse(json);
        var properties = doc.RootElement.EnumerateObject().Select(p => p.Name).ToArray();

        // Order matters: clients expecting a fixed shape will index by position too.
        Assert.Equal(new[] { "count", "ts" }, properties);
    }

    [Fact]
    public void Serialize_NonZeroCount_RoundTripsCorrectly()
    {
        var ts = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);

        var json = CountEntitiesByLayerTool.Serialize(137, ts);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(137, doc.RootElement.GetProperty("count").GetInt32());
        Assert.Equal(ts.ToString("O"), doc.RootElement.GetProperty("ts").GetString());
    }

    [Fact]
    public void Serialize_LargeCount_RoundTripsWithoutOverflow()
    {
        // Large drawings can have tens of thousands of entities on a single layer.
        var ts = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);

        var json = CountEntitiesByLayerTool.Serialize(98_765, ts);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(98_765, doc.RootElement.GetProperty("count").GetInt32());
        Assert.Equal(ts.ToString("O"), doc.RootElement.GetProperty("ts").GetString());
    }
}
