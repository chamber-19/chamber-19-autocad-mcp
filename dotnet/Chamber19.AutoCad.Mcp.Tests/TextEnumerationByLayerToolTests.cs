using System;
using System.Linq;
using System.Text.Json;
using Chamber19.AutoCad.Mcp.Tools;
using Xunit;

namespace Chamber19.AutoCad.Mcp.Tests;

/// <summary>
/// Tests <see cref="TextEnumerationByLayerTool.Serialize"/> with mocked text snapshots.
/// </summary>
public sealed class TextEnumerationByLayerToolTests
{
    [Fact]
    public void Serialize_EmptyTextList_ReturnsEmptyArrayAndTimestamp()
    {
        var ts = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);

        var json = TextEnumerationByLayerTool.Serialize(Array.Empty<TextEntry>(), ts);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(0, doc.RootElement.GetProperty("texts").GetArrayLength());
        Assert.Equal(ts.ToString("O"), doc.RootElement.GetProperty("ts").GetString());
    }

    [Fact]
    public void Serialize_MixedDbTextAndMText_MapsKindsAndPositions()
    {
        var ts = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);
        var texts = new[]
        {
            new TextEntry("00A1", "DBText", "PANEL-A", "PANEL-A", new Coordinate3(1.0, 2.0, 0.0)),
            new TextEntry("00A2", "MText", "\\A1;LINE1\\PLINE2", "LINE1\nLINE2", new Coordinate3(5.5, 9.0, 0.0)),
        };

        var json = TextEnumerationByLayerTool.Serialize(texts, ts);

        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement.GetProperty("texts").EnumerateArray().ToArray();

        Assert.Equal(2, arr.Length);
        Assert.Equal("DBText", arr[0].GetProperty("kind").GetString());
        Assert.Equal("PANEL-A", arr[0].GetProperty("value").GetString());
        Assert.Equal("PANEL-A", arr[0].GetProperty("plainValue").GetString());
        Assert.Equal(1.0, arr[0].GetProperty("position").GetProperty("x").GetDouble());

        Assert.Equal("MText", arr[1].GetProperty("kind").GetString());
        Assert.Equal("\\A1;LINE1\\PLINE2", arr[1].GetProperty("value").GetString());
        Assert.Equal("LINE1\nLINE2", arr[1].GetProperty("plainValue").GetString());
        Assert.Equal(9.0, arr[1].GetProperty("position").GetProperty("y").GetDouble());
        Assert.Equal(ts.ToString("O"), doc.RootElement.GetProperty("ts").GetString());
    }

    [Fact]
    public void Serialize_PreservesFieldOrder_OnTextEntry()
    {
        var json = TextEnumerationByLayerTool.Serialize(
            new[]
            {
                new TextEntry("00A1", "DBText", "X", "X", new Coordinate3(0.0, 0.0, 0.0)),
            },
            DateTimeOffset.UtcNow);

        using var doc = JsonDocument.Parse(json);
        var properties = doc.RootElement.GetProperty("texts")[0]
            .EnumerateObject().Select(p => p.Name).ToArray();

        Assert.Equal(new[] { "handle", "kind", "value", "plainValue", "position" }, properties);
    }

    [Fact]
    public void Serialize_UnicodeAndSpecialCharacters_ProducesValidJson()
    {
        var json = TextEnumerationByLayerTool.Serialize(
            new[]
            {
                new TextEntry("00AF", "DBText", "Load μ=0.85", "Load μ=0.85", new Coordinate3(0.0, 0.0, 0.0)),
                new TextEntry("00B0", "MText", "Flow & Temp", "Flow & Temp", new Coordinate3(1.0, 1.0, 0.0)),
            },
            DateTimeOffset.UtcNow);

        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement.GetProperty("texts").EnumerateArray().ToArray();
        Assert.Equal("Load μ=0.85", arr[0].GetProperty("value").GetString());
        Assert.Equal("Flow & Temp", arr[1].GetProperty("plainValue").GetString());
    }
}