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
/// Per-block placement snapshot returned by <see cref="FindBlocksByLayoutTool"/>.
/// </summary>
internal sealed record LayoutBlockEntry(
    string Name,
    string Handle,
    Coordinate3 Position);

/// <summary>
/// Response snapshot for <see cref="FindBlocksByLayoutTool"/>.
/// </summary>
internal sealed record FindBlocksByLayoutResult(
    bool LayoutFound,
    IReadOnlyList<LayoutBlockEntry> Blocks);

/// <summary>
/// Finds block references placed in a named layout (read-only).
/// </summary>
/// <remarks>
/// Layout matching is case-insensitive against <see cref="Layout.LayoutName"/>.
/// If no layout matches, returns <c>layoutFound: false</c> and an empty block list.
///
/// For each <see cref="BlockReference"/> in the matched layout's
/// <see cref="Layout.BlockTableRecordId"/>, resolves the effective definition through
/// <see cref="BlockReference.DynamicBlockTableRecord"/> so dynamic-block insertions report
/// the canonical definition name rather than anonymous variants.
///
/// Results are sorted by handle (case-insensitive hex comparison).
/// All AutoCAD reads run on the application thread via
/// <see cref="HostDispatcher.InvokeOnApplicationThreadAsync{T}"/>.
/// </remarks>
[McpServerToolType]
public static class FindBlocksByLayoutTool
{
    [McpServerTool(Name = "chamber19_find_blocks_by_layout")]
    [Description("Finds all block references in the specified layout in the active AutoCAD drawing. layoutName matches Layout.LayoutName case-insensitively. If the layout does not exist, returns {layoutFound:false, blocks:[], ts}. If found, each block entry has {name, handle, position}, where name resolves via DynamicBlockTableRecord (effective block definition name). Results are sorted by handle using case-insensitive hex comparison. Read-only; opens a database transaction.")]
    public static async Task<string> FindBlocksByLayoutAsync(
        [Description("Layout tab name to inspect (case-insensitive).")]
        string layoutName)
    {
        var result = await HostDispatcher.InvokeOnApplicationThreadAsync(
            () => ReadBlocks(layoutName));
        return Serialize(result, DateTimeOffset.UtcNow);
    }

    private static FindBlocksByLayoutResult ReadBlocks(string layoutName)
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc is null)
        {
            return new FindBlocksByLayoutResult(LayoutFound: false, Blocks: Array.Empty<LayoutBlockEntry>());
        }

        var db = doc.Database;
        using var tx = db.TransactionManager.StartTransaction();

        var layoutDict = (DBDictionary)tx.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
        Layout? targetLayout = null;
        foreach (DBDictionaryEntry entry in layoutDict)
        {
            var layout = (Layout)tx.GetObject(entry.Value, OpenMode.ForRead);
            if (layout.LayoutName.Equals(layoutName, StringComparison.OrdinalIgnoreCase))
            {
                targetLayout = layout;
                break;
            }
        }

        if (targetLayout is null)
        {
            tx.Commit();
            return new FindBlocksByLayoutResult(LayoutFound: false, Blocks: Array.Empty<LayoutBlockEntry>());
        }

        var blocks = new List<LayoutBlockEntry>();
        var layoutBtr = (BlockTableRecord)tx.GetObject(targetLayout.BlockTableRecordId, OpenMode.ForRead);
        foreach (ObjectId entityId in layoutBtr)
        {
            var entity = tx.GetObject(entityId, OpenMode.ForRead);
            if (entity is not BlockReference blockReference)
            {
                continue;
            }

            var effective = (BlockTableRecord)tx.GetObject(blockReference.DynamicBlockTableRecord, OpenMode.ForRead);
            blocks.Add(new LayoutBlockEntry(
                Name: effective.Name,
                Handle: blockReference.Handle.ToString(),
                Position: new Coordinate3(
                    blockReference.Position.X,
                    blockReference.Position.Y,
                    blockReference.Position.Z)));
        }

        tx.Commit();

        return new FindBlocksByLayoutResult(
            LayoutFound: true,
            Blocks: blocks
                .OrderBy(block => block.Handle, StringComparer.OrdinalIgnoreCase)
                .ToList());
    }

    /// <summary>
    /// Pure JSON shaping logic for the tool response. Exposed for unit testing with mocked
    /// <see cref="FindBlocksByLayoutResult"/> values so tests don't need a real AutoCAD context.
    /// </summary>
    internal static string Serialize(FindBlocksByLayoutResult result, DateTimeOffset ts)
    {
        var payload = new
        {
            layoutFound = result.LayoutFound,
            blocks = result.Blocks.Select(block => new
            {
                name = block.Name,
                handle = block.Handle,
                position = new
                {
                    x = block.Position.X,
                    y = block.Position.Y,
                    z = block.Position.Z,
                },
            }).ToArray(),
            ts = ts.ToString("O"),
        };
        return JsonSerializer.Serialize(payload);
    }
}