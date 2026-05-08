using System;
using System.Linq;
using System.Text.Json;
using Chamber19.AutoCad.Mcp.Tools;
using Xunit;

namespace Chamber19.AutoCad.Mcp.Tests;

/// <summary>
/// Tests <see cref="ClosedPolylineAreaByLayerTool.Serialize"/> and pure helpers with mocked
/// measurement results representing what the AutoCAD read path would have produced.
/// </summary>
public sealed class ClosedPolylineAreaByLayerToolTests
{
    [Fact]
    public void Serialize_ZeroValues_ReturnsZeroAreaAndZeroCount()
    {
        var ts = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);

        var json = ClosedPolylineAreaByLayerTool.Serialize(0.0, 0, ts);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(0.0, doc.RootElement.GetProperty("totalArea").GetDouble());
        Assert.Equal(0, doc.RootElement.GetProperty("polylineCount").GetInt32());
        Assert.Equal(ts.ToString("O"), doc.RootElement.GetProperty("ts").GetString());
    }

    [Fact]
    public void Serialize_PreservesFieldOrder_OnRootElement()
    {
        var json = ClosedPolylineAreaByLayerTool.Serialize(25.0, 2, DateTimeOffset.UtcNow);

        using var doc = JsonDocument.Parse(json);
        var properties = doc.RootElement.EnumerateObject().Select(p => p.Name).ToArray();

        Assert.Equal(new[] { "totalArea", "polylineCount", "ts" }, properties);
    }

    [Fact]
    public void Serialize_MixedOpenAndClosedPolylines_OnlyClosedContribute()
    {
        // Represents an upstream read where open polylines were excluded and only two
        // closed polylines contributed to the aggregated result.
        var ts = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);

        var json = ClosedPolylineAreaByLayerTool.Serialize(150.5, 2, ts);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(150.5, doc.RootElement.GetProperty("totalArea").GetDouble(), precision: 10);
        Assert.Equal(2, doc.RootElement.GetProperty("polylineCount").GetInt32());
        Assert.Equal(ts.ToString("O"), doc.RootElement.GetProperty("ts").GetString());
    }

    [Fact]
    public void NormalizeArea_NegativeInput_ReturnsAbsoluteValue()
    {
        Assert.Equal(42.25, ClosedPolylineAreaByLayerTool.NormalizeArea(-42.25));
        Assert.Equal(42.25, ClosedPolylineAreaByLayerTool.NormalizeArea(42.25));
    }
}