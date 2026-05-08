using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Chamber19.AutoCad.Mcp.Threading;
using ModelContextProtocol.Server;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Chamber19.AutoCad.Mcp.Tools;

/// <summary>
/// Per-match snapshot returned by <see cref="EnumerateAttributeValuesByTagTool"/>.
/// </summary>
internal sealed record AttributeValueMatch(
    string BlockName,
    string Handle,
    string Value);

/// <summary>
/// Enumerates all attribute values across the drawing for a given attribute tag (read-only).
/// </summary>
/// <remarks>
/// Walk pattern mirrors <see cref="EnumerateBlockAttributesTool"/>: iterate all layout BTRs
/// (model space + paper spaces), enumerate each <see cref="BlockReference"/>'s
/// <see cref="BlockReference.AttributeCollection"/>, and collect matching
/// <see cref="AttributeReference"/> records.
///
/// Tag matching is case-insensitive. <c>blockName</c> resolves via
/// <see cref="BlockReference.DynamicBlockTableRecord"/> so dynamic block instances report the
/// canonical definition name. <c>handle</c> is the attribute reference handle (not the parent
/// block handle). Results are sorted by handle.
///
/// All AutoCAD reads run on the application thread via
/// <see cref="HostDispatcher.InvokeOnApplicationThreadAsync{T}"/>.
/// </remarks>
[McpServerToolType]
public static class EnumerateAttributeValuesByTagTool
{
    [McpServerTool(Name = "chamber19_enumerate_attribute_values_by_tag")]
    [Description("Enumerates all attribute values across the active AutoCAD drawing for a given attribute tag. Walks all layout BTRs (model space + paper spaces), every BlockReference, and each AttributeCollection. Tag matching is case-insensitive. Returns matches as {blockName, handle, value}, where blockName resolves via DynamicBlockTableRecord (effective definition name) and handle is the AttributeReference handle (not the parent block handle). Results are sorted by handle. Returns {matches:[], ts} when no drawing is open or no matching tags exist. Read-only; opens a database transaction.")]
    public static async Task<string> EnumerateAttributeValuesByTagAsync(
        [Description("Attribute tag to match (case-insensitive).")]
        string tag)
    {
        var matches = await HostDispatcher.InvokeOnApplicationThreadAsync(
            () => ReadMatches(tag));
        return Serialize(matches, DateTimeOffset.UtcNow);
    }

    private static IReadOnlyList<AttributeValueMatch> ReadMatches(string tag)
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc is null)
        {
            return Array.Empty<AttributeValueMatch>();
        }

        var db = doc.Database;
        using var tx = db.TransactionManager.StartTransaction();
        var blockTable = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);

        var matches = new List<AttributeValueMatch>();
        foreach (ObjectId btrId in blockTable)
        {
            var layoutBtr = (BlockTableRecord)tx.GetObject(btrId, OpenMode.ForRead);
            if (!layoutBtr.IsLayout)
            {
                continue;
            }

            foreach (ObjectId entityId in layoutBtr)
            {
                var entity = tx.GetObject(entityId, OpenMode.ForRead);
                if (entity is not BlockReference blockReference)
                {
                    continue;
                }

                var effective = (BlockTableRecord)tx.GetObject(blockReference.DynamicBlockTableRecord, OpenMode.ForRead);

                foreach (ObjectId attId in blockReference.AttributeCollection)
                {
                    var attribute = (AttributeReference)tx.GetObject(attId, OpenMode.ForRead);
                    if (!TagMatches(attribute.Tag, tag))
                    {
                        continue;
                    }

                    matches.Add(new AttributeValueMatch(
                        BlockName: effective.Name,
                        Handle: attribute.Handle.ToString(),
                        Value: attribute.TextString));
                }
            }
        }

        tx.Commit();

        return matches
            .OrderBy(match => match.Handle, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static bool TagMatches(string attributeTag, string targetTag) =>
        attributeTag.Equals(targetTag, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Pure JSON shaping logic for the tool response. Exposed for unit testing with mocked
    /// <see cref="AttributeValueMatch"/> arrays so tests don't need a real AutoCAD context.
    /// </summary>
    internal static string Serialize(IReadOnlyList<AttributeValueMatch> matches, DateTimeOffset ts)
    {
        var payload = new
        {
            matches = matches.Select(match => new
            {
                blockName = match.BlockName,
                handle = match.Handle,
                value = match.Value,
            }).ToArray(),
            ts = ts.ToString("O"),
        };
        return JsonSerializer.Serialize(payload);
    }
}