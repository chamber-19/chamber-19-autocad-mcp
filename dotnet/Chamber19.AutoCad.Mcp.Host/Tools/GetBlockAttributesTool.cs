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
/// Per-attribute snapshot used by <see cref="GetBlockAttributesTool"/> and
/// <see cref="EnumerateBlockAttributesTool"/>.
/// </summary>
internal sealed record AttributeEntry(string Tag, string Value);

/// <summary>
/// Returns the attribute values for the first placed instance of a named block in the active
/// drawing (read-only).
/// </summary>
/// <remarks>
/// Pattern from <c>autocad-knowledge/attributes.md</c>: walk every layout BTR (model space +
/// paper spaces), find the first <see cref="BlockReference"/> whose effective definition name
/// (resolved via <see cref="BlockReference.DynamicBlockTableRecord"/>) matches the requested
/// block name (case-insensitive), then iterate its
/// <see cref="BlockReference.AttributeCollection"/> and open each
/// <see cref="AttributeReference"/> ForRead to capture <see cref="AttributeReference.Tag"/>
/// and <see cref="AttributeReference.TextString"/>.
///
/// <b>First-instance semantics:</b> "First" means the first <see cref="BlockReference"/>
/// encountered while iterating layout BTRs in block-table iteration order (model space BTR is
/// visited first, followed by paper-space BTRs in layout-tab order). If the same block is
/// placed multiple times, only the first encountered instance's attributes are returned; all
/// other instances are ignored.
///
/// All AutoCAD reads run on the application thread via
/// <see cref="HostDispatcher.InvokeOnApplicationThreadAsync{T}"/>.
/// </remarks>
[McpServerToolType]
public static class GetBlockAttributesTool
{
    [McpServerTool(Name = "chamber19_get_block_attributes")]
    [Description("Returns attributes for the first placed instance of the named block in the active AutoCAD drawing. \"First\" means the first BlockReference encountered while walking layout BTRs in block-table iteration order (model space is visited first, then paper-space tabs); if the block appears multiple times only the first instance's attributes are returned. Attributes are returned as [{tag, value}] in AttributeCollection order. Returns an empty attributes array when no drawing is open, the block is not found, or the first instance has no attributes. blockName is case-insensitive. Read-only; opens a database transaction.")]
    public static async Task<string> GetBlockAttributesAsync(
        [Description("Name of the block definition to query (case-insensitive).")]
        string blockName)
    {
        var attributes = await HostDispatcher.InvokeOnApplicationThreadAsync(
            () => ReadAttributes(blockName));
        return Serialize(attributes, DateTimeOffset.UtcNow);
    }

    private static IReadOnlyList<AttributeEntry> ReadAttributes(string blockName)
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc is null)
        {
            return Array.Empty<AttributeEntry>();
        }

        var db = doc.Database;
        using var tx = db.TransactionManager.StartTransaction();
        var blockTable = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);

        // Walk every layout BTR (model space + paper spaces) looking for the first
        // BlockReference whose effective definition matches blockName.
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
                if (entity is not BlockReference bref)
                {
                    continue;
                }

                var effective = (BlockTableRecord)tx.GetObject(bref.DynamicBlockTableRecord, OpenMode.ForRead);
                if (!effective.Name.Equals(blockName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var result = new List<AttributeEntry>();
                foreach (ObjectId attId in bref.AttributeCollection)
                {
                    var att = (AttributeReference)tx.GetObject(attId, OpenMode.ForRead);
                    result.Add(new AttributeEntry(Tag: att.Tag, Value: att.TextString));
                }

                tx.Commit();
                return result;
            }
        }

        tx.Commit();
        return Array.Empty<AttributeEntry>();
    }

    /// <summary>
    /// Pure JSON shaping logic for the tool response. Exposed for unit testing with mocked
    /// <see cref="AttributeEntry"/> arrays so tests don't need a real AutoCAD context.
    /// </summary>
    internal static string Serialize(IReadOnlyList<AttributeEntry> attributes, DateTimeOffset ts)
    {
        var payload = new
        {
            attributes = attributes.Select(a => new
            {
                tag = a.Tag,
                value = a.Value,
            }).ToArray(),
            ts = ts.ToString("O"),
        };
        return JsonSerializer.Serialize(payload);
    }
}
