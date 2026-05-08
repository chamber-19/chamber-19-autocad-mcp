using System;
using System.Linq;
using System.Text.Json;
using Chamber19.AutoCad.Mcp.Tools;
using Xunit;

namespace Chamber19.AutoCad.Mcp.Tests;

/// <summary>
/// Tests <see cref="ListDimStylesTool.Serialize"/> with mocked <see cref="DimStyleInfo"/> arrays
/// representing what an AutoCAD DimStyleTable iteration would have produced.
/// The actual AutoCAD-touching read path (<c>ReadDimStyles</c>) is verified by the smoke test
/// against a running AutoCAD; here we lock in the JSON shape clients depend on.
/// </summary>
public sealed class ListDimStylesToolTests
{
    [Fact]
    public void Serialize_EmptyDimStyleList_ReturnsEmptyArrayAndTimestamp()
    {
        var ts = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);

        var json = ListDimStylesTool.Serialize(Array.Empty<DimStyleInfo>(), ts);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(0, doc.RootElement.GetProperty("dimStyles").GetArrayLength());
        Assert.Equal(ts.ToString("O"), doc.RootElement.GetProperty("ts").GetString());
    }

    [Fact]
    public void Serialize_PreservesFieldOrder_OnEachDimStyleEntry()
    {
        var json = ListDimStylesTool.Serialize(
            new[] { new DimStyleInfo("Standard", LineScale: 1.0, TextHeight: 2.5) },
            DateTimeOffset.UtcNow);

        using var doc = JsonDocument.Parse(json);
        var first = doc.RootElement.GetProperty("dimStyles")[0];
        var properties = first.EnumerateObject().Select(p => p.Name).ToArray();

        // Order matters: clients expecting a fixed shape will index by position too.
        Assert.Equal(
            new[] { "name", "lineScale", "textHeight" },
            properties);
    }

    [Fact]
    public void Serialize_TypicalDimStyleSet_MapsAllFieldsCorrectly()
    {
        // Input already in the order ReadDimStyles would return: sorted by name, case-insensitive.
        var ts = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);
        var dimStyles = new[]
        {
            new DimStyleInfo("ARCH", LineScale: 48.0, TextHeight: 3.0),
            new DimStyleInfo("METRIC-25", LineScale: 25.0, TextHeight: 2.0),
            new DimStyleInfo("Standard", LineScale: 1.0, TextHeight: 2.5),
        };

        var json = ListDimStylesTool.Serialize(dimStyles, ts);

        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement.GetProperty("dimStyles").EnumerateArray().ToArray();
        Assert.Equal(3, arr.Length);
        Assert.Equal(ts.ToString("O"), doc.RootElement.GetProperty("ts").GetString());

        // ARCH comes first (case-insensitive sort: A < M < S).
        Assert.Equal("ARCH", arr[0].GetProperty("name").GetString());
        Assert.Equal(48.0, arr[0].GetProperty("lineScale").GetDouble());
        Assert.Equal(3.0, arr[0].GetProperty("textHeight").GetDouble());

        Assert.Equal("METRIC-25", arr[1].GetProperty("name").GetString());
        Assert.Equal(25.0, arr[1].GetProperty("lineScale").GetDouble());
        Assert.Equal(2.0, arr[1].GetProperty("textHeight").GetDouble());

        Assert.Equal("Standard", arr[2].GetProperty("name").GetString());
        Assert.Equal(1.0, arr[2].GetProperty("lineScale").GetDouble());
        Assert.Equal(2.5, arr[2].GetProperty("textHeight").GetDouble());
    }

    [Fact]
    public void Serialize_DimStyleWithUnicodeInName_ProducesValidJson()
    {
        // AutoCAD dim style names can include unicode; this guards against accidental
        // string mishandling in our own serializer rather than testing AutoCAD's rules.
        var json = ListDimStylesTool.Serialize(
            new[] { new DimStyleInfo("Ø-Toleranz", LineScale: 1.0, TextHeight: 2.5) },
            DateTimeOffset.UtcNow);

        using var doc = JsonDocument.Parse(json); // would throw on invalid JSON
        Assert.Equal("Ø-Toleranz", doc.RootElement.GetProperty("dimStyles")[0].GetProperty("name").GetString());
    }
}
