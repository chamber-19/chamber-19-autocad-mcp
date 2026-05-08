using System;
using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Chamber19.AutoCad.Mcp.Threading;
using ModelContextProtocol.Server;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Chamber19.AutoCad.Mcp.Tools;

/// <summary>
/// Counts entities on a specified layer in the active drawing (read-only).
/// </summary>
/// <remarks>
/// Filter pattern from <c>autocad-knowledge/selection_sets.md</c>: construct a
/// <see cref="SelectionFilter"/> with a single <see cref="DxfCode.LayerName"/> TypedValue and
/// call <see cref="Editor.SelectAll(SelectionFilter)"/> to count entities in the current space.
/// Layer name matching is case-insensitive in AutoCAD's selection engine.
///
/// <b>Current-space semantics:</b> <see cref="Editor.SelectAll"/> searches only the current
/// space — model space when <c>TILEMODE=1</c>, active paper space when <c>TILEMODE=0</c>.
/// Entities on the same layer that exist in a different space are <b>not</b> included in the
/// returned count. To count entities in all spaces, switch to each space and call the tool again.
///
/// All AutoCAD interactions run on the application thread via
/// <see cref="HostDispatcher.InvokeOnApplicationThreadAsync{T}"/>.
/// </remarks>
[McpServerToolType]
public static class CountEntitiesByLayerTool
{
    [McpServerTool(Name = "chamber19_count_entities_by_layer")]
    [Description("Counts all entities on the specified layer in the active AutoCAD drawing. Uses a TypedValue/DxfCode.LayerName selection filter applied via Editor.SelectAll — searches the current space (model space when TILEMODE=1, active paper space otherwise). Layer name matching is case-insensitive. Returns {count, ts} where count=0 when no drawing is open, the layer does not exist, or no entities reside on it. Read-only; does not open a database transaction.")]
    public static async Task<string> CountEntitiesByLayerAsync(
        [Description("Name of the layer to count entities on (case-insensitive).")]
        string layerName)
    {
        var count = await HostDispatcher.InvokeOnApplicationThreadAsync(
            () => CountEntities(layerName));
        return Serialize(count, DateTimeOffset.UtcNow);
    }

    private static int CountEntities(string layerName)
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc is null)
        {
            return 0;
        }

        var filter = new SelectionFilter(new[]
        {
            new TypedValue((int)DxfCode.LayerName, layerName),
        });

        var result = doc.Editor.SelectAll(filter);
        if (result.Status != PromptStatus.OK)
        {
            return 0;
        }

        return result.Value.Count;
    }

    /// <summary>
    /// Pure JSON shaping logic for the tool response. Exposed for unit testing so tests
    /// don't need a real AutoCAD context to lock in the JSON shape clients depend on.
    /// </summary>
    internal static string Serialize(int count, DateTimeOffset ts)
    {
        var payload = new
        {
            count,
            ts = ts.ToString("O"),
        };
        return JsonSerializer.Serialize(payload);
    }
}
