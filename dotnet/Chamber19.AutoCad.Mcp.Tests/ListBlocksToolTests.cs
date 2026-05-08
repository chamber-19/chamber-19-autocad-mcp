using System;
using System.Linq;
using System.Text.Json;
using Chamber19.AutoCad.Mcp.Tools;
using Xunit;

namespace Chamber19.AutoCad.Mcp.Tests;

/// <summary>
/// Tests <see cref="ListBlocksTool.Serialize"/> with mocked <see cref="BlockInfo"/> arrays
/// representing what an AutoCAD BlockTable walk + reference-count pass would have produced.
/// The actual AutoCAD-touching read path (<c>ReadBlocks</c>) is verified by the smoke test
/// against a running AutoCAD; here we lock in the JSON shape clients depend on.
/// </summary>
public sealed class ListBlocksToolTests
{
    [Fact]
    public void Serialize_EmptyBlockList_ReturnsEmptyArrayAndTimestamp()
    {
        var ts = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);

        var json = ListBlocksTool.Serialize(Array.Empty<BlockInfo>(), ts);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(0, doc.RootElement.GetProperty("blocks").GetArrayLength());
        Assert.Equal(ts.ToString("O"), doc.RootElement.GetProperty("ts").GetString());
    }

    [Fact]
    public void Serialize_PreservesFieldOrder_OnEachBlockEntry()
    {
        var json = ListBlocksTool.Serialize(
            new[] { new BlockInfo("TITLEBLOCK", 1, IsDynamic: false) },
            DateTimeOffset.UtcNow);

        using var doc = JsonDocument.Parse(json);
        var first = doc.RootElement.GetProperty("blocks")[0];
        var properties = first.EnumerateObject().Select(p => p.Name).ToArray();

        Assert.Equal(
            new[] { "name", "referenceCount", "isDynamic" },
            properties);
    }

    [Fact]
    public void Serialize_TypicalBlockSet_MapsAllFieldsCorrectly()
    {
        var ts = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);
        var blocks = new[]
        {
            new BlockInfo("CATL TOP", ReferenceCount: 54, IsDynamic: false),
            new BlockInfo("DYNAMIC-DOOR", ReferenceCount: 12, IsDynamic: true),
            new BlockInfo("PANEL-100A", ReferenceCount: 3, IsDynamic: false),
            new BlockInfo("UNUSED-BLOCK", ReferenceCount: 0, IsDynamic: false),
            new BlockInfo("DYNAMIC-WINDOW-WITH-NO-INSERTS", ReferenceCount: 0, IsDynamic: true),
        };

        var json = ListBlocksTool.Serialize(blocks, ts);

        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement.GetProperty("blocks").EnumerateArray().ToArray();
        Assert.Equal(5, arr.Length);
        Assert.Equal(ts.ToString("O"), doc.RootElement.GetProperty("ts").GetString());

        // High-count regular block.
        Assert.Equal("CATL TOP", arr[0].GetProperty("name").GetString());
        Assert.Equal(54, arr[0].GetProperty("referenceCount").GetInt32());
        Assert.False(arr[0].GetProperty("isDynamic").GetBoolean());

        // Dynamic block with refs.
        Assert.Equal("DYNAMIC-DOOR", arr[1].GetProperty("name").GetString());
        Assert.Equal(12, arr[1].GetProperty("referenceCount").GetInt32());
        Assert.True(arr[1].GetProperty("isDynamic").GetBoolean());

        // Defined-but-unused block — count 0 must round-trip.
        Assert.Equal("UNUSED-BLOCK", arr[3].GetProperty("name").GetString());
        Assert.Equal(0, arr[3].GetProperty("referenceCount").GetInt32());
        Assert.False(arr[3].GetProperty("isDynamic").GetBoolean());

        // Dynamic-and-unused — both flags meaningful.
        Assert.Equal(0, arr[4].GetProperty("referenceCount").GetInt32());
        Assert.True(arr[4].GetProperty("isDynamic").GetBoolean());
    }

    [Fact]
    public void Serialize_BlockNameWithSpacesAndPunctuation_ProducesValidJson()
    {
        // R3P drawings frequently use block names like "CATL TOP" (space) or "PANEL-100A" (hyphen).
        var json = ListBlocksTool.Serialize(
            new[]
            {
                new BlockInfo("CATL TOP", 54, false),
                new BlockInfo("PANEL-100A", 3, false),
            },
            DateTimeOffset.UtcNow);

        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement.GetProperty("blocks").EnumerateArray().ToArray();
        Assert.Equal("CATL TOP", arr[0].GetProperty("name").GetString());
        Assert.Equal("PANEL-100A", arr[1].GetProperty("name").GetString());
    }
}
