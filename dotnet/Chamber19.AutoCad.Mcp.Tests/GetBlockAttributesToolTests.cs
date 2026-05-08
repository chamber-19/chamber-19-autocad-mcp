using System;
using System.Linq;
using System.Text.Json;
using Chamber19.AutoCad.Mcp.Tools;
using Xunit;

namespace Chamber19.AutoCad.Mcp.Tests;

/// <summary>
/// Tests <see cref="GetBlockAttributesTool.Serialize"/> with mocked <see cref="AttributeEntry"/>
/// arrays representing what an AutoCAD BlockReference attribute walk would have produced.
/// The actual AutoCAD-touching read path (<c>ReadAttributes</c>) is verified by the smoke test
/// against a running AutoCAD; here we lock in the JSON shape clients depend on.
/// </summary>
public sealed class GetBlockAttributesToolTests
{
    [Fact]
    public void Serialize_EmptyAttributeList_ReturnsEmptyArrayAndTimestamp()
    {
        var ts = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);

        var json = GetBlockAttributesTool.Serialize(Array.Empty<AttributeEntry>(), ts);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(0, doc.RootElement.GetProperty("attributes").GetArrayLength());
        Assert.Equal(ts.ToString("O"), doc.RootElement.GetProperty("ts").GetString());
    }

    [Fact]
    public void Serialize_PreservesFieldOrder_OnEachAttributeEntry()
    {
        var json = GetBlockAttributesTool.Serialize(
            new[] { new AttributeEntry("PART_NO", "100-ABC") },
            DateTimeOffset.UtcNow);

        using var doc = JsonDocument.Parse(json);
        var first = doc.RootElement.GetProperty("attributes")[0];
        var properties = first.EnumerateObject().Select(p => p.Name).ToArray();

        Assert.Equal(new[] { "tag", "value" }, properties);
    }

    [Fact]
    public void Serialize_TypicalAttributeSet_MapsAllFieldsCorrectly()
    {
        var ts = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);
        var attributes = new[]
        {
            new AttributeEntry("PART_NO",   "100-ABC"),
            new AttributeEntry("RATING",    "100A"),
            new AttributeEntry("VOLTAGE",   "480V"),
            new AttributeEntry("MFR",       "ACME"),
            new AttributeEntry("DESC",      "Main Breaker"),
        };

        var json = GetBlockAttributesTool.Serialize(attributes, ts);

        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement.GetProperty("attributes").EnumerateArray().ToArray();
        Assert.Equal(5, arr.Length);
        Assert.Equal(ts.ToString("O"), doc.RootElement.GetProperty("ts").GetString());

        Assert.Equal("PART_NO", arr[0].GetProperty("tag").GetString());
        Assert.Equal("100-ABC", arr[0].GetProperty("value").GetString());

        Assert.Equal("RATING", arr[1].GetProperty("tag").GetString());
        Assert.Equal("100A", arr[1].GetProperty("value").GetString());

        Assert.Equal("DESC", arr[4].GetProperty("tag").GetString());
        Assert.Equal("Main Breaker", arr[4].GetProperty("value").GetString());
    }

    [Fact]
    public void Serialize_AttributesWithSpecialCharacters_ProducesValidJson()
    {
        // Block attribute values in electrical drawings frequently contain slashes, ampersands,
        // and Unicode characters (e.g., degree symbol for ratings).
        var json = GetBlockAttributesTool.Serialize(
            new[]
            {
                new AttributeEntry("RATING", "120/240V"),
                new AttributeEntry("DESC", "Feed & Panel"),
                new AttributeEntry("TEMP", "75°C"),
            },
            DateTimeOffset.UtcNow);

        using var doc = JsonDocument.Parse(json); // throws on invalid JSON
        var arr = doc.RootElement.GetProperty("attributes").EnumerateArray().ToArray();
        Assert.Equal("120/240V", arr[0].GetProperty("value").GetString());
        Assert.Equal("Feed & Panel", arr[1].GetProperty("value").GetString());
        Assert.Equal("75°C", arr[2].GetProperty("value").GetString());
    }
}
