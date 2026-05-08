using System;
using System.Linq;
using System.Text.Json;
using Chamber19.AutoCad.Mcp.Tools;
using Xunit;

namespace Chamber19.AutoCad.Mcp.Tests;

/// <summary>
/// Tests <see cref="PolylineLengthByLayerTool.Serialize"/> with mocked measurement results
/// representing what the AutoCAD selection-set + transaction read path would have produced.
/// The actual AutoCAD-touching read path (<c>MeasurePolylines</c>) requires integration testing
/// against a running AutoCAD instance; here we lock in the JSON shape clients depend on.
/// </summary>
public sealed class PolylineLengthByLayerToolTests
{
    [Fact]
    public void Serialize_ZeroValues_ReturnsZeroLengthAndZeroCount()
    {
        // Represents the no-drawing-open / layer-not-found / no-polylines-on-layer case.
        var ts = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);

        var json = PolylineLengthByLayerTool.Serialize(0.0, 0, ts);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(0.0, doc.RootElement.GetProperty("totalLength").GetDouble());
        Assert.Equal(0, doc.RootElement.GetProperty("polylineCount").GetInt32());
        Assert.Equal(ts.ToString("O"), doc.RootElement.GetProperty("ts").GetString());
    }

    [Fact]
    public void Serialize_PreservesFieldOrder_OnRootElement()
    {
        var json = PolylineLengthByLayerTool.Serialize(10.5, 2, DateTimeOffset.UtcNow);

        using var doc = JsonDocument.Parse(json);
        var properties = doc.RootElement.EnumerateObject().Select(p => p.Name).ToArray();

        // Order matters: clients expecting a fixed shape will index by position too.
        Assert.Equal(new[] { "totalLength", "polylineCount", "ts" }, properties);
    }

    [Fact]
    public void Serialize_TypicalMixedLengths_SumsPrecisely()
    {
        // Simulates three polylines (lengths 12.5, 7.25, 100.0) summed upstream by MeasurePolylines.
        var ts = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);

        var json = PolylineLengthByLayerTool.Serialize(119.75, 3, ts);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(119.75, doc.RootElement.GetProperty("totalLength").GetDouble(), precision: 10);
        Assert.Equal(3, doc.RootElement.GetProperty("polylineCount").GetInt32());
        Assert.Equal(ts.ToString("O"), doc.RootElement.GetProperty("ts").GetString());
    }

    [Fact]
    public void Serialize_MixedOpenAndClosedPolylines_IncludesBothInSum()
    {
        // Both open and closed polylines contribute to the totals; this test exercises the
        // serialization of a result that would only arise when both types were counted.
        // A count of 5 with the given totalLength implies at least one of each was measured.
        var ts = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);

        var json = PolylineLengthByLayerTool.Serialize(250.0, 5, ts);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(250.0, doc.RootElement.GetProperty("totalLength").GetDouble());
        Assert.Equal(5, doc.RootElement.GetProperty("polylineCount").GetInt32());
        Assert.Equal(ts.ToString("O"), doc.RootElement.GetProperty("ts").GetString());
    }
}
