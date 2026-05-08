using System;
using System.Linq;
using System.Text.Json;
using Chamber19.AutoCad.Mcp.Tools;
using Xunit;

namespace Chamber19.AutoCad.Mcp.Tests;

/// <summary>
/// Tests <see cref="FindBlocksByLayoutTool.Serialize"/> with mocked layout query results.
/// </summary>
public sealed class FindBlocksByLayoutToolTests
{
    [Fact]
    public void Serialize_LayoutNotFound_ReturnsFalseAndEmptyBlocks()
    {
        var ts = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);
        var result = new FindBlocksByLayoutResult(false, Array.Empty<LayoutBlockEntry>());

        var json = FindBlocksByLayoutTool.Serialize(result, ts);

        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("layoutFound").GetBoolean());
        Assert.Equal(0, doc.RootElement.GetProperty("blocks").GetArrayLength());
        Assert.Equal(ts.ToString("O"), doc.RootElement.GetProperty("ts").GetString());
    }

    [Fact]
    public void Serialize_EmptyLayout_ReturnsTrueAndEmptyBlocks()
    {
        var result = new FindBlocksByLayoutResult(true, Array.Empty<LayoutBlockEntry>());

        var json = FindBlocksByLayoutTool.Serialize(result, DateTimeOffset.UtcNow);

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("layoutFound").GetBoolean());
        Assert.Equal(0, doc.RootElement.GetProperty("blocks").GetArrayLength());
    }

    [Fact]
    public void Serialize_MultipleBlocks_MapsFieldsCorrectly()
    {
        var ts = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);
        var result = new FindBlocksByLayoutResult(
            true,
            new[]
            {
                new LayoutBlockEntry("TITLEBLOCK", "0100", new Coordinate3(0.0, 0.0, 0.0)),
                new LayoutBlockEntry("VALVE_DYNAMIC", "0101", new Coordinate3(25.5, 11.2, 0.0)),
            });

        var json = FindBlocksByLayoutTool.Serialize(result, ts);

        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement.GetProperty("blocks").EnumerateArray().ToArray();

        Assert.True(doc.RootElement.GetProperty("layoutFound").GetBoolean());
        Assert.Equal(2, arr.Length);
        Assert.Equal("TITLEBLOCK", arr[0].GetProperty("name").GetString());
        Assert.Equal("0101", arr[1].GetProperty("handle").GetString());
        Assert.Equal(25.5, arr[1].GetProperty("position").GetProperty("x").GetDouble());
        Assert.Equal(ts.ToString("O"), doc.RootElement.GetProperty("ts").GetString());
    }

    [Fact]
    public void Serialize_PreservesFieldOrder_OnBlockEntries()
    {
        var json = FindBlocksByLayoutTool.Serialize(
            new FindBlocksByLayoutResult(
                true,
                new[]
                {
                    new LayoutBlockEntry("DYN_BLOCK", "00AA", new Coordinate3(1.0, 2.0, 3.0)),
                }),
            DateTimeOffset.UtcNow);

        using var doc = JsonDocument.Parse(json);
        var properties = doc.RootElement.GetProperty("blocks")[0]
            .EnumerateObject().Select(p => p.Name).ToArray();

        Assert.Equal(new[] { "name", "handle", "position" }, properties);
    }
}