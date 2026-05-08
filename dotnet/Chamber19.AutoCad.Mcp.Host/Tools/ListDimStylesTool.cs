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
/// Per-dimension-style snapshot returned by <see cref="ListDimStylesTool"/>.
/// </summary>
internal sealed record DimStyleInfo(
    string Name,
    double LineScale,
    double TextHeight);

/// <summary>
/// Lists all dimension styles in the active drawing (read-only).
/// </summary>
/// <remarks>
/// Opens <see cref="Database.DimStyleTableId"/> as a <see cref="DimStyleTable"/> ForRead,
/// iterates each <see cref="DimStyleTableRecord"/> ForRead, and projects to
/// <see cref="DimStyleInfo"/>. <see cref="DimStyleTableRecord.Dimscale"/> is the overall
/// dimension scale factor (exposed as <c>lineScale</c>) and
/// <see cref="DimStyleTableRecord.Dimtxt"/> is the primary-units text height.
///
/// Results are sorted by <c>name</c> (case-insensitive, ordinal). This ordering is stable
/// and guaranteed; clients may depend on it.
///
/// All AutoCAD reads run on the application thread via
/// <see cref="HostDispatcher.InvokeOnApplicationThreadAsync{T}"/>.
/// </remarks>
[McpServerToolType]
public static class ListDimStylesTool
{
    [McpServerTool(Name = "chamber19_list_dimstyles")]
    [Description("Lists all dimension styles defined in the active AutoCAD drawing. Each entry has name, lineScale (Dimscale — overall dimension scale factor), and textHeight (Dimtxt — primary-units text height). Results are sorted by name (case-insensitive). Returns an empty dimStyles array when no drawing is open. Read-only; opens a database transaction.")]
    public static async Task<string> ListDimStylesAsync()
    {
        var dimStyles = await HostDispatcher.InvokeOnApplicationThreadAsync(ReadDimStyles);
        return Serialize(dimStyles, DateTimeOffset.UtcNow);
    }

    private static IReadOnlyList<DimStyleInfo> ReadDimStyles()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc is null)
        {
            return Array.Empty<DimStyleInfo>();
        }

        var db = doc.Database;
        using var tx = db.TransactionManager.StartTransaction();
        var dimStyleTable = (DimStyleTable)tx.GetObject(db.DimStyleTableId, OpenMode.ForRead);

        var result = new List<DimStyleInfo>();
        foreach (ObjectId id in dimStyleTable)
        {
            var dstr = (DimStyleTableRecord)tx.GetObject(id, OpenMode.ForRead);
            result.Add(new DimStyleInfo(
                Name: dstr.Name,
                LineScale: dstr.Dimscale,
                TextHeight: dstr.Dimtxt));
        }
        tx.Commit();

        return result
            .OrderBy(ds => ds.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Pure JSON shaping logic for the tool response. Exposed for unit testing with mocked
    /// <see cref="DimStyleInfo"/> arrays so tests don't need a real AutoCAD context.
    /// </summary>
    internal static string Serialize(IReadOnlyList<DimStyleInfo> dimStyles, DateTimeOffset ts)
    {
        var payload = new
        {
            dimStyles = dimStyles.Select(ds => new
            {
                name = ds.Name,
                lineScale = ds.LineScale,
                textHeight = ds.TextHeight,
            }).ToArray(),
            ts = ts.ToString("O"),
        };
        return JsonSerializer.Serialize(payload);
    }
}
