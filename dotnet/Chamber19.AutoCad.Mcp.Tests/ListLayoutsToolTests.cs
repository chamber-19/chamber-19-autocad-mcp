using System;
using System.Linq;
using System.Text.Json;
using Chamber19.AutoCad.Mcp.Tools;
using Xunit;

namespace Chamber19.AutoCad.Mcp.Tests;

/// <summary>
/// Tests <see cref="ListLayoutsTool.Serialize"/> with mocked <see cref="LayoutInfo"/> arrays
/// representing what an AutoCAD LayoutDictionary iteration would have produced.
/// The actual AutoCAD-touching read path (<c>ReadLayouts</c>) is verified by the smoke test
/// against a running AutoCAD; here we lock in the JSON shape clients depend on.
/// </summary>
public sealed class ListLayoutsToolTests
{
    [Fact]
    public void Serialize_EmptyLayoutList_ReturnsEmptyArrayAndTimestamp()
    {
        var ts = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);

        var json = ListLayoutsTool.Serialize(Array.Empty<LayoutInfo>(), ts);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(0, doc.RootElement.GetProperty("layouts").GetArrayLength());
        Assert.Equal(ts.ToString("O"), doc.RootElement.GetProperty("ts").GetString());
    }

    [Fact]
    public void Serialize_PreservesFieldOrder_OnEachLayoutEntry()
    {
        var json = ListLayoutsTool.Serialize(
            new[] { new LayoutInfo("Model", IsCurrent: true, TabOrder: 0) },
            DateTimeOffset.UtcNow);

        using var doc = JsonDocument.Parse(json);
        var first = doc.RootElement.GetProperty("layouts")[0];
        var properties = first.EnumerateObject().Select(p => p.Name).ToArray();

        Assert.Equal(
            new[] { "name", "isCurrent", "tabOrder" },
            properties);
    }

    [Fact]
    public void Serialize_TypicalLayoutSet_MapsAllFieldsCorrectly()
    {
        var ts = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);
        var layouts = new[]
        {
            new LayoutInfo("Model",   IsCurrent: false, TabOrder: 0),
            new LayoutInfo("Sheet 1", IsCurrent: true,  TabOrder: 1),
            new LayoutInfo("Sheet 2", IsCurrent: false, TabOrder: 2),
        };

        var json = ListLayoutsTool.Serialize(layouts, ts);

        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement.GetProperty("layouts").EnumerateArray().ToArray();
        Assert.Equal(3, arr.Length);
        Assert.Equal(ts.ToString("O"), doc.RootElement.GetProperty("ts").GetString());

        // Model tab — not current, tabOrder 0.
        Assert.Equal("Model", arr[0].GetProperty("name").GetString());
        Assert.False(arr[0].GetProperty("isCurrent").GetBoolean(),
            "Model should not be the current tab");
        Assert.Equal(0, arr[0].GetProperty("tabOrder").GetInt32());

        // Sheet 1 — current, tabOrder 1.
        Assert.Equal("Sheet 1", arr[1].GetProperty("name").GetString());
        Assert.True(arr[1].GetProperty("isCurrent").GetBoolean(),
            "Sheet 1 should be the current tab");
        Assert.Equal(1, arr[1].GetProperty("tabOrder").GetInt32());

        // Sheet 2 — not current, tabOrder 2.
        Assert.Equal("Sheet 2", arr[2].GetProperty("name").GetString());
        Assert.False(arr[2].GetProperty("isCurrent").GetBoolean(),
            "Sheet 2 should not be the current tab");
        Assert.Equal(2, arr[2].GetProperty("tabOrder").GetInt32());
    }

    [Fact]
    public void Serialize_LayoutWithUnicodeName_ProducesValidJson()
    {
        var json = ListLayoutsTool.Serialize(
            new[] { new LayoutInfo("Feuille Détail – А1", IsCurrent: false, TabOrder: 1) },
            DateTimeOffset.UtcNow);

        using var doc = JsonDocument.Parse(json); // would throw on invalid JSON
        var entry = doc.RootElement.GetProperty("layouts")[0];
        Assert.Equal("Feuille Détail – А1", entry.GetProperty("name").GetString());
    }
}
