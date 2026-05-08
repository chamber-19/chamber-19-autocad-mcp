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
/// Per-instance snapshot returned by <see cref="EnumerateBlockAttributesTool"/>.
/// </summary>
internal sealed record BlockInstanceEntry(string Handle, IReadOnlyList<AttributeEntry> Attributes);

/// <summary>
/// Returns the attribute values for every placed instance of a named block in the active
/// drawing (read-only).
/// </summary>
/// <remarks>
/// Pattern from <c>autocad-knowledge/attributes.md</c> "Finding all instances of a named block":
/// walk every layout BTR (model space + paper spaces), collect each
/// <see cref="BlockReference"/> whose effective definition name
/// (resolved via <see cref="BlockReference.DynamicBlockTableRecord"/>) matches the requested
/// block name (case-insensitive), then iterate each instance's
/// <see cref="BlockReference.AttributeCollection"/> and open each
/// <see cref="AttributeReference"/> ForRead to capture <see cref="AttributeReference.Tag"/>
/// and <see cref="AttributeReference.TextString"/>.
///
/// All AutoCAD reads run on the application thread via
/// <see cref="HostDispatcher.InvokeOnApplicationThreadAsync{T}"/>.
/// </remarks>
[McpServerToolType]
public static class EnumerateBlockAttributesTool
{
    [McpServerTool(Name = "chamber19_enumerate_block_attributes")]
    [Description("Returns attributes for every placed instance of the named block in the active AutoCAD drawing. Walks all layout BTRs (model space first, then paper-space tabs) and collects attribute values from each matching BlockReference. Each instance is identified by its AutoCAD entity handle (hex string). Attributes for each instance are returned as [{tag, value}] in AttributeCollection order. Returns an empty instances array when no drawing is open, the block is not found, or no instances have attributes. blockName is case-insensitive. Read-only; opens a database transaction.")]
    public static async Task<string> EnumerateBlockAttributesAsync(
        [Description("Name of the block definition to query (case-insensitive).")]
        string blockName)
    {
        var instances = await HostDispatcher.InvokeOnApplicationThreadAsync(
            () => ReadAllInstances(blockName));
        return Serialize(instances, DateTimeOffset.UtcNow);
    }

    private static IReadOnlyList<BlockInstanceEntry> ReadAllInstances(string blockName)
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc is null)
        {
            return Array.Empty<BlockInstanceEntry>();
        }

        var db = doc.Database;
        using var tx = db.TransactionManager.StartTransaction();
        var blockTable = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);

        var result = new List<BlockInstanceEntry>();

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

                var attributes = new List<AttributeEntry>();
                foreach (ObjectId attId in bref.AttributeCollection)
                {
                    var att = (AttributeReference)tx.GetObject(attId, OpenMode.ForRead);
                    attributes.Add(new AttributeEntry(Tag: att.Tag, Value: att.TextString));
                }

                result.Add(new BlockInstanceEntry(
                    Handle: bref.Handle.ToString(),
                    Attributes: attributes));
            }
        }

        tx.Commit();
        return result;
    }

    /// <summary>
    /// Pure JSON shaping logic for the tool response. Exposed for unit testing with mocked
    /// <see cref="BlockInstanceEntry"/> arrays so tests don't need a real AutoCAD context.
    /// </summary>
    internal static string Serialize(IReadOnlyList<BlockInstanceEntry> instances, DateTimeOffset ts)
    {
        var payload = new
        {
            instances = instances.Select(inst => new
            {
                handle = inst.Handle,
                attributes = inst.Attributes.Select(a => new
                {
                    tag = a.Tag,
                    value = a.Value,
                }).ToArray(),
            }).ToArray(),
            ts = ts.ToString("O"),
        };
        return JsonSerializer.Serialize(payload);
    }
}
