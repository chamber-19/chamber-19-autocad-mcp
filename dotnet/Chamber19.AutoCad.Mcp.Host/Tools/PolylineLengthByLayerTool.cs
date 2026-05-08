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
/// Sums total length of all polylines on a specified layer in the active drawing (read-only).
/// </summary>
/// <remarks>
/// Filter pattern from <c>autocad-knowledge/polylines.md</c>: constructs a
/// <see cref="SelectionFilter"/> combining a <see cref="DxfCode.LayerName"/> condition with an
/// OR-group for <c>LWPOLYLINE</c> and <c>POLYLINE</c> entity types, then calls
/// <see cref="Editor.SelectAll(SelectionFilter)"/> to locate matching entities in the current
/// space. Each matched entity is opened ForRead inside a read-only transaction and its
/// <c>Length</c> property is summed. Unexpected types (defensive cast) are skipped.
///
/// <b>Polyline types included:</b>
/// <list type="bullet">
/// <item><c>LWPOLYLINE</c> (DXF) → <see cref="Polyline"/> in .NET — lightweight 2-D polyline</item>
/// <item><c>POLYLINE</c> (DXF) → <see cref="Polyline2d"/> or <see cref="Polyline3d"/> in .NET</item>
/// </list>
/// Both open and closed polylines are included; <c>Length</c> reports the full perimeter for
/// closed polylines.
///
/// <b>Current-space semantics:</b> <see cref="Editor.SelectAll"/> searches only the current
/// space — model space when <c>TILEMODE=1</c>, active paper space when <c>TILEMODE=0</c>.
/// Polylines in a different space are <b>not</b> included in the returned totals.
///
/// All AutoCAD interactions run on the application thread via
/// <see cref="HostDispatcher.InvokeOnApplicationThreadAsync{T}"/>.
/// </remarks>
[McpServerToolType]
public static class PolylineLengthByLayerTool
{
    [McpServerTool(Name = "chamber19_polyline_length_by_layer")]
    [Description("Sums total length of all polylines (LWPOLYLINE and POLYLINE) on the specified layer in the active AutoCAD drawing. Uses a TypedValue selection filter combining DxfCode.LayerName with an OR-group for LWPOLYLINE and POLYLINE entity types, applied via Editor.SelectAll — searches the current space (model space when TILEMODE=1, active paper space otherwise). Layer name matching is case-insensitive. Includes both open and closed polylines. Returns {totalLength, polylineCount, ts} where both are 0 when no drawing is open, the layer does not exist, or no polylines reside on it. totalLength is in drawing units. Read-only; opens a database transaction to read entity lengths.")]
    public static async Task<string> PolylineLengthByLayerAsync(
        [Description("Name of the layer to sum polyline lengths on (case-insensitive).")]
        string layerName)
    {
        var (totalLength, polylineCount) = await HostDispatcher.InvokeOnApplicationThreadAsync(
            () => MeasurePolylines(layerName));
        return Serialize(totalLength, polylineCount, DateTimeOffset.UtcNow);
    }

    private static (double totalLength, int polylineCount) MeasurePolylines(string layerName)
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc is null)
        {
            return (0.0, 0);
        }

        var filter = new SelectionFilter(new[]
        {
            new TypedValue((int)DxfCode.LayerName, layerName),
            new TypedValue((int)DxfCode.Operator, "<OR"),
            new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
            new TypedValue((int)DxfCode.Start, "POLYLINE"),
            new TypedValue((int)DxfCode.Operator, "OR>"),
        });

        var result = doc.Editor.SelectAll(filter);
        if (result.Status != PromptStatus.OK)
        {
            return (0.0, 0);
        }

        double totalLength = 0.0;
        int polylineCount = 0;

        using var tx = doc.Database.TransactionManager.StartTransaction();
        foreach (var objId in result.Value.GetObjectIds())
        {
            var entity = tx.GetObject(objId, OpenMode.ForRead);
            double? length = entity switch
            {
                Polyline pl => pl.Length,
                Polyline2d pl2 => pl2.Length,
                Polyline3d pl3 => pl3.Length,
                _ => null,
            };
            if (length.HasValue)
            {
                totalLength += length.Value;
                polylineCount++;
            }
        }
        tx.Commit();

        return (totalLength, polylineCount);
    }

    /// <summary>
    /// Pure JSON shaping logic for the tool response. Exposed for unit testing so tests
    /// don't need a real AutoCAD context to lock in the JSON shape clients depend on.
    /// </summary>
    internal static string Serialize(double totalLength, int polylineCount, DateTimeOffset ts)
    {
        var payload = new
        {
            totalLength,
            polylineCount,
            ts = ts.ToString("O"),
        };
        return JsonSerializer.Serialize(payload);
    }
}
