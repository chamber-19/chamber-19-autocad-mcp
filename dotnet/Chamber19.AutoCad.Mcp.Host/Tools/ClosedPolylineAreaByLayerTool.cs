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
/// Sums total enclosed area of closed lightweight polylines on a specified layer in the active
/// drawing (read-only).
/// </summary>
/// <remarks>
/// Search pattern mirrors <see cref="PolylineLengthByLayerTool"/>: a <see cref="SelectionFilter"/>
/// combines <see cref="DxfCode.LayerName"/> with an OR-group for <c>LWPOLYLINE</c> and
/// <c>POLYLINE</c>, then <see cref="Editor.SelectAll(SelectionFilter)"/> searches the current
/// space only. Matched entities are opened ForRead inside a read-only transaction.
///
/// Only <see cref="Polyline"/> entities with <see cref="Polyline.Closed"/> == <c>true</c> are
/// counted. Each contributing polyline uses <c>Math.Abs(Polyline.Area)</c>, so clockwise and
/// counter-clockwise winding produce identical positive area totals.
///
/// All AutoCAD interactions run on the application thread via
/// <see cref="HostDispatcher.InvokeOnApplicationThreadAsync{T}"/>.
/// </remarks>
[McpServerToolType]
public static class ClosedPolylineAreaByLayerTool
{
    [McpServerTool(Name = "chamber19_closed_polyline_area_by_layer")]
    [Description("Sums enclosed area of closed polylines on the specified layer in the active AutoCAD drawing. Uses a TypedValue selection filter combining DxfCode.LayerName with an OR-group for LWPOLYLINE and POLYLINE entity types, applied via Editor.SelectAll in the current space. Layer name matching is case-insensitive. Counts only Polyline entities where Closed == true, and each contribution is Math.Abs(Polyline.Area). Returns {totalArea, polylineCount, ts}; both values are 0 when no drawing is open or no qualifying closed polylines are found. Read-only; opens a database transaction.")]
    public static async Task<string> ClosedPolylineAreaByLayerAsync(
        [Description("Name of the layer to sum closed-polyline area on (case-insensitive).")]
        string layerName)
    {
        var (totalArea, polylineCount) = await HostDispatcher.InvokeOnApplicationThreadAsync(
            () => MeasureClosedPolylines(layerName));
        return Serialize(totalArea, polylineCount, DateTimeOffset.UtcNow);
    }

    private static (double totalArea, int polylineCount) MeasureClosedPolylines(string layerName)
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

        double totalArea = 0.0;
        int polylineCount = 0;

        using var tx = doc.Database.TransactionManager.StartTransaction();
        foreach (var objId in result.Value.GetObjectIds())
        {
            var entity = tx.GetObject(objId, OpenMode.ForRead);
            if (entity is Polyline polyline && polyline.Closed)
            {
                totalArea += NormalizeArea(polyline.Area);
                polylineCount++;
            }
        }
        tx.Commit();

        return (totalArea, polylineCount);
    }

    internal static double NormalizeArea(double rawArea) => Math.Abs(rawArea);

    /// <summary>
    /// Pure JSON shaping logic for the tool response. Exposed for unit testing so tests
    /// don't need a real AutoCAD context to lock in the JSON shape clients depend on.
    /// </summary>
    internal static string Serialize(double totalArea, int polylineCount, DateTimeOffset ts)
    {
        var payload = new
        {
            totalArea,
            polylineCount,
            ts = ts.ToString("O"),
        };
        return JsonSerializer.Serialize(payload);
    }
}