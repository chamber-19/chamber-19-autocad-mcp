using System;
using System.Linq;
using System.Text.Json;
using Chamber19.AutoCad.Mcp.Tools;
using Xunit;

namespace Chamber19.AutoCad.Mcp.Tests;

/// <summary>
/// Tests <see cref="EnumerateAttributeValuesByTagTool.Serialize"/> and pure matching helpers.
/// </summary>
public sealed class EnumerateAttributeValuesByTagToolTests
{
    [Fact]
    public void Serialize_TagNotFound_ReturnsEmptyMatches()
    {
        var ts = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);

        var json = EnumerateAttributeValuesByTagTool.Serialize(Array.Empty<AttributeValueMatch>(), ts);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(0, doc.RootElement.GetProperty("matches").GetArrayLength());
        Assert.Equal(ts.ToString("O"), doc.RootElement.GetProperty("ts").GetString());
    }

    [Fact]
    public void Serialize_SingleMatch_MapsAllFields()
    {
        var json = EnumerateAttributeValuesByTagTool.Serialize(
            new[]
            {
                new AttributeValueMatch("PANEL_TAG", "00AF", "P-101"),
            },
            DateTimeOffset.UtcNow);

        using var doc = JsonDocument.Parse(json);
        var first = doc.RootElement.GetProperty("matches")[0];
        Assert.Equal("PANEL_TAG", first.GetProperty("blockName").GetString());
        Assert.Equal("00AF", first.GetProperty("handle").GetString());
        Assert.Equal("P-101", first.GetProperty("value").GetString());
    }

    [Fact]
    public void Serialize_MultiMatchAcrossBlocks_PreservesOrderAndValues()
    {
        var ts = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);
        var json = EnumerateAttributeValuesByTagTool.Serialize(
            new[]
            {
                new AttributeValueMatch("TITLEBLOCK", "0010", "A1"),
                new AttributeValueMatch("EQUIPMENT", "0011", "A2"),
                new AttributeValueMatch("EQUIPMENT", "0012", "A3"),
            },
            ts);

        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement.GetProperty("matches").EnumerateArray().ToArray();
        Assert.Equal(3, arr.Length);
        Assert.Equal("TITLEBLOCK", arr[0].GetProperty("blockName").GetString());
        Assert.Equal("A2", arr[1].GetProperty("value").GetString());
        Assert.Equal("0012", arr[2].GetProperty("handle").GetString());
        Assert.Equal(ts.ToString("O"), doc.RootElement.GetProperty("ts").GetString());
    }

    [Fact]
    public void TagMatches_IsCaseInsensitive()
    {
        Assert.True(EnumerateAttributeValuesByTagTool.TagMatches("PART_NO", "part_no"));
        Assert.True(EnumerateAttributeValuesByTagTool.TagMatches("Part_No", "PART_NO"));
        Assert.False(EnumerateAttributeValuesByTagTool.TagMatches("RATING", "VOLTAGE"));
    }
}