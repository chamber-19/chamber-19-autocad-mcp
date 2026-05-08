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
/// Per-block snapshot returned by <see cref="ListBlocksTool"/>.
/// </summary>
internal sealed record BlockInfo(
    string Name,
    int ReferenceCount,
    bool IsDynamic);

/// <summary>
/// Lists user-defined block definitions in the active drawing along with the count of
/// placed references and whether the definition is dynamic.
/// </summary>
/// <remarks>
/// Iteration pattern lifted from <c>autocad-knowledge/attributes.md</c> ("Finding all
/// instances of a named block"): walk the BlockTable, identify candidate definitions
/// (skip anonymous <c>*U</c>/<c>*X</c>/<c>*D</c> blocks and layout BTRs), then walk every
/// layout BTR (model space + paper spaces) and resolve each <see cref="BlockReference"/>
/// via <see cref="BlockReference.DynamicBlockTableRecord"/> so dynamic-block references
/// count against the original definition rather than its anonymous customized variants.
///
/// All AutoCAD reads run on the application thread via
/// <see cref="HostDispatcher.InvokeOnApplicationThreadAsync{T}"/>.
/// </remarks>
[McpServerToolType]
public static class ListBlocksTool
{
    [McpServerTool(Name = "chamber19_list_blocks")]
    [Description("Lists user-defined block definitions in the active AutoCAD drawing. Each entry has name, reference count (how many BlockReferences in model space + paper spaces resolve to this definition), and isDynamic flag. Anonymous (*U/*X/*D) and layout BTRs are excluded. Returns an empty blocks array when no drawing is open. Read-only; opens a database transaction.")]
    public static async Task<string> ListBlocksAsync()
    {
        var blocks = await HostDispatcher.InvokeOnApplicationThreadAsync(ReadBlocks);
        return Serialize(blocks, DateTimeOffset.UtcNow);
    }

    private static IReadOnlyList<BlockInfo> ReadBlocks()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc is null)
        {
            return Array.Empty<BlockInfo>();
        }

        var db = doc.Database;
        using var tx = db.TransactionManager.StartTransaction();
        var blockTable = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);

        // Pass 1: collect non-anonymous, non-layout BlockTableRecords as candidate definitions.
        var drafts = new Dictionary<string, BlockDraft>(StringComparer.OrdinalIgnoreCase);
        foreach (ObjectId btrId in blockTable)
        {
            var btr = (BlockTableRecord)tx.GetObject(btrId, OpenMode.ForRead);
            if (btr.IsAnonymous || btr.IsLayout)
            {
                continue;
            }
            drafts[btr.Name] = new BlockDraft
            {
                Name = btr.Name,
                IsDynamic = btr.IsDynamicBlock,
            };
        }

        // Pass 2: walk every layout BTR (model space + paper spaces). For each BlockReference,
        // resolve to its effective definition via DynamicBlockTableRecord and increment the count.
        foreach (ObjectId btrId in blockTable)
        {
            var btr = (BlockTableRecord)tx.GetObject(btrId, OpenMode.ForRead);
            if (!btr.IsLayout)
            {
                continue;
            }

            foreach (ObjectId entityId in btr)
            {
                var entity = tx.GetObject(entityId, OpenMode.ForRead);
                if (entity is not BlockReference bref)
                {
                    continue;
                }

                var effective = (BlockTableRecord)tx.GetObject(bref.DynamicBlockTableRecord, OpenMode.ForRead);
                if (drafts.TryGetValue(effective.Name, out var draft))
                {
                    draft.ReferenceCount++;
                }
            }
        }

        tx.Commit();

        return drafts.Values
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .Select(d => new BlockInfo(d.Name, d.ReferenceCount, d.IsDynamic))
            .ToList();
    }

    /// <summary>
    /// Pure JSON shaping logic for the tool response. Exposed for unit testing with mocked
    /// <see cref="BlockInfo"/> arrays so tests don't need a real AutoCAD context.
    /// </summary>
    internal static string Serialize(IReadOnlyList<BlockInfo> blocks, DateTimeOffset ts)
    {
        var payload = new
        {
            blocks = blocks.Select(block => new
            {
                name = block.Name,
                referenceCount = block.ReferenceCount,
                isDynamic = block.IsDynamic,
            }).ToArray(),
            ts = ts.ToString("O"),
        };
        return JsonSerializer.Serialize(payload);
    }

    private sealed class BlockDraft
    {
        public required string Name { get; init; }
        public required bool IsDynamic { get; init; }
        public int ReferenceCount { get; set; }
    }
}
