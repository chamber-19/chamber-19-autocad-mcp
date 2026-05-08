using System;
using System.Linq;
using System.Text.Json;
using Chamber19.AutoCad.Mcp.Tools;
using Xunit;

namespace Chamber19.AutoCad.Mcp.Tests;

/// <summary>
/// Tests <see cref="GetBlockByHandleTool"/> pure helpers and serialization shape with mocked
/// results representing read-path outcomes.
/// </summary>
public sealed class GetBlockByHandleToolTests
{
    [Fact]
    public void Serialize_ValidButMissingHandle_ReturnsFoundFalse()
    {
        var ts = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);
        var json = GetBlockByHandleTool.Serialize(GetBlockByHandleTool.NotFound(), ts);

        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("found").GetBoolean());
        Assert.Equal(ts.ToString("O"), doc.RootElement.GetProperty("ts").GetString());
    }

    [Fact]
    public void Serialize_NonBlockHandle_ReturnsFoundFalse()
    {
        // Same serialization outcome as any non-resolving/non-block read-path branch.
        var json = GetBlockByHandleTool.Serialize(GetBlockByHandleTool.NotFound(), DateTimeOffset.UtcNow);

        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("found").GetBoolean());
    }

    [Fact]
    public void Serialize_DynamicBlockResult_MapsNamePositionAndAttributes()
    {
        var ts = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);
        var result = new BlockByHandleResult(
            Found: true,
            BlockName: "VALVE_DYNAMIC",
            Position: new Coordinate3(10.0, 20.0, 0.0),
            Attributes: new[]
            {
                new AttributeEntry("TAG", "V-101"),
                new AttributeEntry("SIZE", "2\""),
            });

        var json = GetBlockByHandleTool.Serialize(result, ts);

        using var doc = JsonDocument.Parse(json);
        var attrs = doc.RootElement.GetProperty("attributes").EnumerateArray().ToArray();

        Assert.True(doc.RootElement.GetProperty("found").GetBoolean());
        Assert.Equal("VALVE_DYNAMIC", doc.RootElement.GetProperty("blockName").GetString());
        Assert.Equal(20.0, doc.RootElement.GetProperty("position").GetProperty("y").GetDouble());
        Assert.Equal("TAG", attrs[0].GetProperty("tag").GetString());
        Assert.Equal("V-101", attrs[0].GetProperty("value").GetString());
        Assert.Equal(ts.ToString("O"), doc.RootElement.GetProperty("ts").GetString());
    }
}