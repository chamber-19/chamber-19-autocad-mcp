using System;
using System.Linq;
using System.Text.Json;
using Chamber19.AutoCad.Mcp.Tools;
using Xunit;

namespace Chamber19.AutoCad.Mcp.Tests;

/// <summary>
/// Tests <see cref="ListLayersTool.Serialize"/> with mocked <see cref="LayerInfo"/> arrays
/// representing what an AutoCAD layer-table iteration would have produced.
/// The actual AutoCAD-touching read path (<c>ReadLayers</c>) requires integration testing
/// against a running AutoCAD instance; here we lock in the JSON shape clients depend on.
/// </summary>
public sealed class ListLayersToolTests
{
    [Fact]
    public void Serialize_EmptyLayerList_ReturnsEmptyArrayAndTimestamp()
    {
        var ts = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);

        var json = ListLayersTool.Serialize(Array.Empty<LayerInfo>(), ts);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(0, doc.RootElement.GetProperty("layers").GetArrayLength());
        Assert.Equal(ts.ToString("O"), doc.RootElement.GetProperty("ts").GetString());
    }

    [Fact]
    public void Serialize_PreservesFieldOrder_OnEachLayerEntry()
    {
        var json = ListLayersTool.Serialize(
            new[] { new LayerInfo("0", 7, false, false, false, true) },
            DateTimeOffset.UtcNow);

        using var doc = JsonDocument.Parse(json);
        var first = doc.RootElement.GetProperty("layers")[0];
        var properties = first.EnumerateObject().Select(p => p.Name).ToArray();

        // Order matters: clients expecting a fixed shape will index by position too.
        Assert.Equal(
            new[] { "name", "colorIndex", "isFrozen", "isLocked", "isOff", "isPlottable" },
            properties);
    }

    [Fact]
    public void Serialize_TypicalLayerSet_MapsAllFieldsCorrectly()
    {
        var ts = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);
        var layers = new[]
        {
            new LayerInfo("0", 7, IsFrozen: false, IsLocked: false, IsOff: false, IsPlottable: true),
            new LayerInfo("WIRE", 1, IsFrozen: false, IsLocked: false, IsOff: false, IsPlottable: true),
            new LayerInfo("HIDDEN", 2, IsFrozen: true, IsLocked: false, IsOff: false, IsPlottable: true),
            new LayerInfo("LOCKED-NOTES", 4, IsFrozen: false, IsLocked: true, IsOff: false, IsPlottable: true),
            new LayerInfo("OFF-CONSTRUCTION", 8, IsFrozen: false, IsLocked: false, IsOff: true, IsPlottable: false),
        };

        var json = ListLayersTool.Serialize(layers, ts);

        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement.GetProperty("layers").EnumerateArray().ToArray();
        Assert.Equal(5, arr.Length);

        Assert.Equal("0", arr[0].GetProperty("name").GetString());
        Assert.Equal(7, arr[0].GetProperty("colorIndex").GetInt32());
        Assert.False(arr[0].GetProperty("isFrozen").GetBoolean());
        Assert.False(arr[0].GetProperty("isLocked").GetBoolean());
        Assert.False(arr[0].GetProperty("isOff").GetBoolean());
        Assert.True(arr[0].GetProperty("isPlottable").GetBoolean());

        Assert.True(arr[2].GetProperty("isFrozen").GetBoolean(),
            "HIDDEN should round-trip IsFrozen=true");
        Assert.True(arr[3].GetProperty("isLocked").GetBoolean(),
            "LOCKED-NOTES should round-trip IsLocked=true");
        Assert.True(arr[4].GetProperty("isOff").GetBoolean(),
            "OFF-CONSTRUCTION should round-trip IsOff=true");
        Assert.False(arr[4].GetProperty("isPlottable").GetBoolean(),
            "OFF-CONSTRUCTION should round-trip IsPlottable=false");
    }

    [Fact]
    public void Serialize_LayerWithNullCharactersInName_ProducesValidJson()
    {
        // AutoCAD layer names can include unicode but not control chars; this guards against
        // accidental string mishandling in our own serializer rather than testing AutoCAD's rules.
        var json = ListLayersTool.Serialize(
            new[] { new LayerInfo("ø-special", 7, false, false, false, true) },
            DateTimeOffset.UtcNow);

        using var doc = JsonDocument.Parse(json); // would throw on invalid JSON
        Assert.Equal("ø-special", doc.RootElement.GetProperty("layers")[0].GetProperty("name").GetString());
    }
}
