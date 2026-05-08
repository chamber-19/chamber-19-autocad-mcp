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
/// Per-layer snapshot returned by <see cref="ListLayersTool"/>.
/// </summary>
internal sealed record LayerInfo(
    string Name,
    int ColorIndex,
    bool IsFrozen,
    bool IsLocked,
    bool IsOff,
    bool IsPlottable);

/// <summary>
/// Lists all layers in the active drawing (read-only).
/// </summary>
/// <remarks>
/// Iteration pattern lifted from <c>autocad-knowledge/layers.md</c>: open the LayerTable ForRead,
/// iterate ObjectIds, open each LayerTableRecord ForRead, project to <see cref="LayerInfo"/>.
/// All AutoCAD reads run on the application thread via
/// <see cref="AutoCadThreadDispatcher.InvokeOnApplicationThreadAsync{T}"/>.
/// </remarks>
[McpServerToolType]
public static class ListLayersTool
{
    [McpServerTool(Name = "chamber19_list_layers")]
    [Description("Lists all layers in the active AutoCAD drawing. Each entry has name, AutoCAD Color Index (ACI), and the frozen/locked/off/plottable flags. Returns an empty layers array when no drawing is open. Read-only; opens a database transaction.")]
    public static async Task<string> ListLayersAsync()
    {
        var layers = await AutoCadThreadDispatcher.InvokeOnApplicationThreadAsync(ReadLayers);
        return Serialize(layers, DateTimeOffset.UtcNow);
    }

    private static IReadOnlyList<LayerInfo> ReadLayers()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc is null)
        {
            return Array.Empty<LayerInfo>();
        }

        var db = doc.Database;
        using var tx = db.TransactionManager.StartTransaction();
        var layerTable = (LayerTable)tx.GetObject(db.LayerTableId, OpenMode.ForRead);

        var result = new List<LayerInfo>();
        foreach (ObjectId id in layerTable)
        {
            var ltr = (LayerTableRecord)tx.GetObject(id, OpenMode.ForRead);
            result.Add(new LayerInfo(
                Name: ltr.Name,
                ColorIndex: ltr.Color?.ColorIndex ?? 0,
                IsFrozen: ltr.IsFrozen,
                IsLocked: ltr.IsLocked,
                IsOff: ltr.IsOff,
                IsPlottable: ltr.IsPlottable));
        }
        tx.Commit();

        return result;
    }

    /// <summary>
    /// Pure JSON shaping logic for the tool response. Exposed for unit testing with mocked
    /// <see cref="LayerInfo"/> arrays so tests don't need a real AutoCAD context.
    /// </summary>
    internal static string Serialize(IReadOnlyList<LayerInfo> layers, DateTimeOffset ts)
    {
        var payload = new
        {
            layers = layers.Select(layer => new
            {
                name = layer.Name,
                colorIndex = layer.ColorIndex,
                isFrozen = layer.IsFrozen,
                isLocked = layer.IsLocked,
                isOff = layer.IsOff,
                isPlottable = layer.IsPlottable,
            }).ToArray(),
            ts = ts.ToString("O"),
        };
        return JsonSerializer.Serialize(payload);
    }
}
