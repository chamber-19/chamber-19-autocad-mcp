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
/// Per-xref snapshot returned by <see cref="ListXrefsTool"/>.
/// </summary>
internal sealed record XrefInfo(
    string Name,
    string Path,
    bool IsLoaded,
    bool IsAttached);

/// <summary>
/// Lists external references (xrefs) in the active drawing (read-only).
/// </summary>
/// <remarks>
/// API pattern from <c>autocad-knowledge/xrefs.md</c>: iterate
/// <see cref="Database.XrefBlockTableRecordIds"/> inside a read-only transaction,
/// open each <see cref="BlockTableRecord"/> ForRead, and inspect
/// <see cref="BlockTableRecord.IsLoaded"/>, <see cref="BlockTableRecord.PathName"/>,
/// and <see cref="BlockTableRecord.IsFromOverlayReference"/> (used to derive
/// <c>isAttached</c>: <c>true</c> = Attach mode, <c>false</c> = Overlay mode).
///
/// All AutoCAD reads run on the application thread via
/// <see cref="HostDispatcher.InvokeOnApplicationThreadAsync{T}"/>.
/// </remarks>
[McpServerToolType]
public static class ListXrefsTool
{
    [McpServerTool(Name = "chamber19_list_xrefs")]
    [Description("Lists external references (xrefs) in the active AutoCAD drawing. Each entry has name, path (as stored in the DWG), isLoaded (file resolved and loaded), and isAttached (true = Attach mode, false = Overlay mode). Returns an empty xrefs array when no drawing is open. Read-only; opens a database transaction.")]
    public static async Task<string> ListXrefsAsync()
    {
        var xrefs = await HostDispatcher.InvokeOnApplicationThreadAsync(ReadXrefs);
        return Serialize(xrefs, DateTimeOffset.UtcNow);
    }

    private static IReadOnlyList<XrefInfo> ReadXrefs()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc is null)
        {
            return Array.Empty<XrefInfo>();
        }

        var db = doc.Database;
        using var tx = db.TransactionManager.StartTransaction();

        var result = new List<XrefInfo>();
        foreach (ObjectId id in db.XrefBlockTableRecordIds)
        {
            var btr = (BlockTableRecord)tx.GetObject(id, OpenMode.ForRead);
            result.Add(new XrefInfo(
                Name: btr.Name,
                Path: btr.PathName,
                IsLoaded: btr.IsLoaded,
                IsAttached: !btr.IsFromOverlayReference));
        }

        tx.Commit();

        return result
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Pure JSON shaping logic for the tool response. Exposed for unit testing with mocked
    /// <see cref="XrefInfo"/> arrays so tests don't need a real AutoCAD context.
    /// </summary>
    internal static string Serialize(IReadOnlyList<XrefInfo> xrefs, DateTimeOffset ts)
    {
        var payload = new
        {
            xrefs = xrefs.Select(xref => new
            {
                name = xref.Name,
                path = xref.Path,
                isLoaded = xref.IsLoaded,
                isAttached = xref.IsAttached,
            }).ToArray(),
            ts = ts.ToString("O"),
        };
        return JsonSerializer.Serialize(payload);
    }
}
