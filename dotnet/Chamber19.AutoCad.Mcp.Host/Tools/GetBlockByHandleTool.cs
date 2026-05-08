using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
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
/// Result snapshot returned by <see cref="GetBlockByHandleTool"/>.
/// </summary>
internal sealed record BlockByHandleResult(
    bool Found,
    string? BlockName,
    Coordinate3? Position,
    IReadOnlyList<AttributeEntry> Attributes);

/// <summary>
/// Resolves a handle to a block reference and returns its effective name, position, and
/// attributes (read-only).
/// </summary>
/// <remarks>
/// The input handle is parsed as hexadecimal (DXF group code 5 semantics), then resolved via
/// <see cref="Database.GetObjectId(bool, Handle, int)"/>. Invalid or non-resolving handles,
/// or handles that resolve to non-<see cref="BlockReference"/> objects, return
/// <c>found: false</c> without throwing.
///
/// For dynamic blocks, <c>blockName</c> resolves via
/// <see cref="BlockReference.DynamicBlockTableRecord"/>.
///
/// All AutoCAD reads run on the application thread via
/// <see cref="HostDispatcher.InvokeOnApplicationThreadAsync{T}"/>.
/// </remarks>
[McpServerToolType]
public static class GetBlockByHandleTool
{
    [McpServerTool(Name = "chamber19_get_block_by_handle")]
    [Description("Resolves a hex handle (DXF group code 5 format) to a BlockReference in the active AutoCAD drawing. Returns {found:false, ts} when the handle is malformed, missing, or resolves to a non-block object. When found, returns {found:true, blockName, position, attributes, ts} where blockName resolves via DynamicBlockTableRecord (effective definition name), position is insertion point, and attributes are [{tag, value}] from the block's AttributeCollection. Read-only; opens a database transaction and does not throw for malformed/non-block handles.")]
    public static async Task<string> GetBlockByHandleAsync(
        [Description("Hex handle string (DXF group code 5 format), e.g. '1A2B'.")]
        string handle)
    {
        var result = await HostDispatcher.InvokeOnApplicationThreadAsync(
            () => ReadBlock(handle));
        return Serialize(result, DateTimeOffset.UtcNow);
    }

    private static BlockByHandleResult ReadBlock(string handleText)
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc is null)
        {
            return NotFound();
        }

        if (!TryParseHandle(handleText, out var handle))
        {
            return NotFound();
        }

        try
        {
            var db = doc.Database;
            var objectId = db.GetObjectId(createIfNotFound: false, handle, 0);

            using var tx = db.TransactionManager.StartTransaction();
            var obj = tx.GetObject(objectId, OpenMode.ForRead);
            if (obj is not BlockReference blockReference)
            {
                tx.Commit();
                return NotFound();
            }

            var effective = (BlockTableRecord)tx.GetObject(blockReference.DynamicBlockTableRecord, OpenMode.ForRead);
            var attributes = new List<AttributeEntry>();
            foreach (ObjectId attId in blockReference.AttributeCollection)
            {
                var att = (AttributeReference)tx.GetObject(attId, OpenMode.ForRead);
                attributes.Add(new AttributeEntry(Tag: att.Tag, Value: att.TextString));
            }

            tx.Commit();

            return new BlockByHandleResult(
                Found: true,
                BlockName: effective.Name,
                Position: new Coordinate3(
                    blockReference.Position.X,
                    blockReference.Position.Y,
                    blockReference.Position.Z),
                Attributes: attributes);
        }
        catch
        {
            return NotFound();
        }
    }

    internal static bool TryParseHandle(string handleText, out Handle handle)
    {
        handle = default;

        if (string.IsNullOrWhiteSpace(handleText))
        {
            return false;
        }

        var normalized = handleText.Trim();
        if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[2..];
        }

        if (!ulong.TryParse(normalized, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        handle = new Handle(unchecked((long)parsed));
        return true;
    }

    internal static BlockByHandleResult NotFound() =>
        new(
            Found: false,
            BlockName: null,
            Position: null,
            Attributes: Array.Empty<AttributeEntry>());

    /// <summary>
    /// Pure JSON shaping logic for the tool response. Exposed for unit testing with mocked
    /// <see cref="BlockByHandleResult"/> values so tests don't need a real AutoCAD context.
    /// </summary>
    internal static string Serialize(BlockByHandleResult result, DateTimeOffset ts)
    {
        if (!result.Found)
        {
            var notFoundPayload = new
            {
                found = false,
                ts = ts.ToString("O"),
            };
            return JsonSerializer.Serialize(notFoundPayload);
        }

        var foundPayload = new
        {
            found = true,
            blockName = result.BlockName,
            position = new
            {
                x = result.Position!.X,
                y = result.Position.Y,
                z = result.Position.Z,
            },
            attributes = result.Attributes.Select(a => new
            {
                tag = a.Tag,
                value = a.Value,
            }).ToArray(),
            ts = ts.ToString("O"),
        };
        return JsonSerializer.Serialize(foundPayload);
    }
}