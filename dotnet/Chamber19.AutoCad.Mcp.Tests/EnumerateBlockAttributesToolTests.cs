using System;
using System.Linq;
using System.Text.Json;
using Chamber19.AutoCad.Mcp.Tools;
using Xunit;

namespace Chamber19.AutoCad.Mcp.Tests;

/// <summary>
/// Tests <see cref="EnumerateBlockAttributesTool.Serialize"/> with mocked
/// <see cref="BlockInstanceEntry"/> arrays representing what an AutoCAD attribute walk
/// over all matching BlockReferences would have produced.
/// The actual AutoCAD-touching read path (<c>ReadAllInstances</c>) requires integration
/// testing against a running AutoCAD instance; here we lock in the JSON shape clients
/// depend on.
/// </summary>
public sealed class EnumerateBlockAttributesToolTests
{
    [Fact]
    public void Serialize_EmptyInstanceList_ReturnsEmptyArrayAndTimestamp()
    {
        var ts = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);

        var json = EnumerateBlockAttributesTool.Serialize(Array.Empty<BlockInstanceEntry>(), ts);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(0, doc.RootElement.GetProperty("instances").GetArrayLength());
        Assert.Equal(ts.ToString("O"), doc.RootElement.GetProperty("ts").GetString());
    }

    [Fact]
    public void Serialize_PreservesFieldOrder_OnInstanceAndAttributeEntries()
    {
        var json = EnumerateBlockAttributesTool.Serialize(
            new[]
            {
                new BlockInstanceEntry("1A2", new[] { new AttributeEntry("PART_NO", "100-ABC") }),
            },
            DateTimeOffset.UtcNow);

        using var doc = JsonDocument.Parse(json);
        var instance = doc.RootElement.GetProperty("instances")[0];

        // Instance-level field order: handle, attributes
        var instanceProperties = instance.EnumerateObject().Select(p => p.Name).ToArray();
        Assert.Equal(new[] { "handle", "attributes" }, instanceProperties);

        // Attribute-level field order: tag, value
        var attrProperties = instance.GetProperty("attributes")[0]
            .EnumerateObject().Select(p => p.Name).ToArray();
        Assert.Equal(new[] { "tag", "value" }, attrProperties);
    }

    [Fact]
    public void Serialize_MultipleInstances_MapsAllFieldsCorrectly()
    {
        var ts = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);
        var instances = new[]
        {
            new BlockInstanceEntry("1A2", new[]
            {
                new AttributeEntry("PART_NO", "100-ABC"),
                new AttributeEntry("RATING",  "100A"),
            }),
            new BlockInstanceEntry("1B3", new[]
            {
                new AttributeEntry("PART_NO", "200-DEF"),
                new AttributeEntry("RATING",  "200A"),
            }),
        };

        var json = EnumerateBlockAttributesTool.Serialize(instances, ts);

        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement.GetProperty("instances").EnumerateArray().ToArray();
        Assert.Equal(2, arr.Length);
        Assert.Equal(ts.ToString("O"), doc.RootElement.GetProperty("ts").GetString());

        Assert.Equal("1A2", arr[0].GetProperty("handle").GetString());
        var attrs0 = arr[0].GetProperty("attributes").EnumerateArray().ToArray();
        Assert.Equal(2, attrs0.Length);
        Assert.Equal("PART_NO", attrs0[0].GetProperty("tag").GetString());
        Assert.Equal("100-ABC", attrs0[0].GetProperty("value").GetString());

        Assert.Equal("1B3", arr[1].GetProperty("handle").GetString());
        var attrs1 = arr[1].GetProperty("attributes").EnumerateArray().ToArray();
        Assert.Equal("PART_NO", attrs1[0].GetProperty("tag").GetString());
        Assert.Equal("200-DEF", attrs1[0].GetProperty("value").GetString());
        Assert.Equal("RATING", attrs1[1].GetProperty("tag").GetString());
        Assert.Equal("200A", attrs1[1].GetProperty("value").GetString());
    }

    [Fact]
    public void Serialize_AttributesWithSpecialCharacters_ProducesValidJson()
    {
        // Block attribute values in electrical drawings frequently contain slashes, ampersands,
        // and Unicode characters (e.g., degree symbol for ratings).
        var json = EnumerateBlockAttributesTool.Serialize(
            new[]
            {
                new BlockInstanceEntry("FF1", new[]
                {
                    new AttributeEntry("RATING", "120/240V"),
                    new AttributeEntry("DESC",   "Feed & Panel"),
                    new AttributeEntry("TEMP",   "75°C"),
                }),
            },
            DateTimeOffset.UtcNow);

        using var doc = JsonDocument.Parse(json); // throws on invalid JSON
        var attrs = doc.RootElement
            .GetProperty("instances")[0]
            .GetProperty("attributes")
            .EnumerateArray().ToArray();
        Assert.Equal("120/240V",    attrs[0].GetProperty("value").GetString());
        Assert.Equal("Feed & Panel", attrs[1].GetProperty("value").GetString());
        Assert.Equal("75°C",        attrs[2].GetProperty("value").GetString());
    }
}
