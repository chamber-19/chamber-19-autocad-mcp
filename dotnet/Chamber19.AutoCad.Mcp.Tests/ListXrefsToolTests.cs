using System;
using System.Linq;
using System.Text.Json;
using Chamber19.AutoCad.Mcp.Tools;
using Xunit;

namespace Chamber19.AutoCad.Mcp.Tests;

/// <summary>
/// Tests <see cref="ListXrefsTool.Serialize"/> with mocked <see cref="XrefInfo"/> arrays
/// representing what an AutoCAD XrefBlockTableRecordIds walk would have produced.
/// The actual AutoCAD-touching read path (<c>ReadXrefs</c>) is verified by the smoke test
/// against a running AutoCAD; here we lock in the JSON shape clients depend on.
/// </summary>
public sealed class ListXrefsToolTests
{
    [Fact]
    public void Serialize_EmptyXrefList_ReturnsEmptyArrayAndTimestamp()
    {
        var ts = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);

        var json = ListXrefsTool.Serialize(Array.Empty<XrefInfo>(), ts);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(0, doc.RootElement.GetProperty("xrefs").GetArrayLength());
        Assert.Equal(ts.ToString("O"), doc.RootElement.GetProperty("ts").GetString());
    }

    [Fact]
    public void Serialize_PreservesFieldOrder_OnEachXrefEntry()
    {
        var json = ListXrefsTool.Serialize(
            new[] { new XrefInfo("SITE", @"C:\drawings\site.dwg", IsLoaded: true, IsAttached: true) },
            DateTimeOffset.UtcNow);

        using var doc = JsonDocument.Parse(json);
        var first = doc.RootElement.GetProperty("xrefs")[0];
        var properties = first.EnumerateObject().Select(p => p.Name).ToArray();

        Assert.Equal(
            new[] { "name", "path", "isLoaded", "isAttached" },
            properties);
    }

    [Fact]
    public void Serialize_TypicalXrefSet_MapsAllFieldsCorrectly()
    {
        var ts = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);
        var xrefs = new[]
        {
            new XrefInfo("SITE", @"C:\drawings\site.dwg", IsLoaded: true, IsAttached: true),
            new XrefInfo("OVERLAY-SURVEY", @"\\server\share\survey.dwg", IsLoaded: true, IsAttached: false),
            new XrefInfo("UNRESOLVED-ARCH", @"..\arch\floor.dwg", IsLoaded: false, IsAttached: true),
        };

        var json = ListXrefsTool.Serialize(xrefs, ts);

        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement.GetProperty("xrefs").EnumerateArray().ToArray();
        Assert.Equal(3, arr.Length);
        Assert.Equal(ts.ToString("O"), doc.RootElement.GetProperty("ts").GetString());

        // Loaded attached xref.
        Assert.Equal("SITE", arr[0].GetProperty("name").GetString());
        Assert.Equal(@"C:\drawings\site.dwg", arr[0].GetProperty("path").GetString());
        Assert.True(arr[0].GetProperty("isLoaded").GetBoolean());
        Assert.True(arr[0].GetProperty("isAttached").GetBoolean());

        // Loaded overlay xref — isAttached must be false.
        Assert.Equal("OVERLAY-SURVEY", arr[1].GetProperty("name").GetString());
        Assert.Equal(@"\\server\share\survey.dwg", arr[1].GetProperty("path").GetString());
        Assert.True(arr[1].GetProperty("isLoaded").GetBoolean());
        Assert.False(arr[1].GetProperty("isAttached").GetBoolean());

        // Unresolved attached xref — isLoaded must be false.
        Assert.Equal("UNRESOLVED-ARCH", arr[2].GetProperty("name").GetString());
        Assert.Equal(@"..\arch\floor.dwg", arr[2].GetProperty("path").GetString());
        Assert.False(arr[2].GetProperty("isLoaded").GetBoolean());
        Assert.True(arr[2].GetProperty("isAttached").GetBoolean());
    }

    [Fact]
    public void Serialize_XrefWithUnicodePath_ProducesValidJson()
    {
        var json = ListXrefsTool.Serialize(
            new[] { new XrefInfo("RÉFÉRENCE", @"C:\données\référence.dwg", IsLoaded: false, IsAttached: true) },
            DateTimeOffset.UtcNow);

        using var doc = JsonDocument.Parse(json); // would throw on invalid JSON
        var entry = doc.RootElement.GetProperty("xrefs")[0];
        Assert.Equal("RÉFÉRENCE", entry.GetProperty("name").GetString());
        Assert.Equal(@"C:\données\référence.dwg", entry.GetProperty("path").GetString());
    }
}
