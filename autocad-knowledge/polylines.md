# Polylines — Length and Selection

Reference: AutoCAD .NET Developer's Guide — "Working with Polylines", "Filtering Selection Sets"

## Namespace imports

```csharp
using Autodesk.AutoCAD.DatabaseServices; // Polyline, Polyline2d, Polyline3d, TypedValue, SelectionFilter
using Autodesk.AutoCAD.EditorInput;      // Editor, SelectionResult, PromptStatus
```

## Polyline types

AutoCAD has three polyline classes in the .NET API:

| DXF entity type | .NET class | Description |
|---|---|---|
| `LWPOLYLINE` | `Autodesk.AutoCAD.DatabaseServices.Polyline` | Lightweight 2-D polyline (most common in modern drawings) |
| `POLYLINE` | `Autodesk.AutoCAD.DatabaseServices.Polyline2d` | Legacy 2-D polyline (older DWG format) |
| `POLYLINE` | `Autodesk.AutoCAD.DatabaseServices.Polyline3d` | 3-D polyline |

All three expose a `Length` property that returns the total arc length of the entity in drawing
units. For closed polylines, `Length` is the full perimeter (the closing segment is included).

## Selection filter for polylines on a layer

To select all `LWPOLYLINE` and `POLYLINE` entities on a specific layer, combine a
`DxfCode.LayerName` condition with an OR-group for the two entity types:

```csharp
var filter = new SelectionFilter(new[]
{
    new TypedValue((int)DxfCode.LayerName, layerName),
    new TypedValue((int)DxfCode.Operator, "<OR"),
    new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
    new TypedValue((int)DxfCode.Start, "POLYLINE"),
    new TypedValue((int)DxfCode.Operator, "OR>"),
});
```

The implicit AND semantics mean "layer matches AND (type is LWPOLYLINE OR type is POLYLINE)".
Layer name matching is **case-insensitive** in AutoCAD's selection engine.

## Summing polyline lengths

After selecting with `Editor.SelectAll`, open each entity inside a read-only transaction and
cast defensively:

```csharp
double totalLength = 0.0;
int polylineCount = 0;

using var tx = doc.Database.TransactionManager.StartTransaction();
foreach (var objId in result.Value.GetObjectIds())
{
    var entity = tx.GetObject(objId, OpenMode.ForRead);
    double? length = entity switch
    {
        Polyline   pl  => pl.Length,
        Polyline2d pl2 => pl2.Length,
        Polyline3d pl3 => pl3.Length,
        _              => null,          // defensive: skip unexpected types
    };
    if (length.HasValue)
    {
        totalLength += length.Value;
        polylineCount++;
    }
}
tx.Commit();
```

The defensive `_ => null` skip is intentional: `SelectAll` is driven by DXF entity type strings,
but the .NET runtime type is the authoritative check. Skipping non-polyline types prevents a
`ClassCastException` from unexpected or corrupted entities.

## Open vs. closed polylines

Both open and closed polylines are included when iterating `SelectAll` results. The `Length`
property behaves identically for both:

- **Open polyline:** sum of segment lengths from the first vertex to the last.
- **Closed polyline:** sum of all segment lengths including the closing segment back to the
  first vertex (i.e., the full perimeter).

There is no flag to exclude one or the other via the selection filter; if you need to separate
them, inspect `Polyline.Closed` / `Polyline2d.Closed` after opening the entity.

## Current-space semantics

`Editor.SelectAll` operates on the **current space** only:

| TILEMODE value | "Current space" |
|---|---|
| `1` (default, tiled viewports) | Model space |
| `0` (paper space active) | Active paper space tab |

Polylines in a different space are **not** included. To measure polylines across all spaces,
switch to each space in turn and call the tool again.

## Threading note

All `Editor` calls must run on the AutoCAD application thread. In the Host assembly, dispatch
via `HostDispatcher.InvokeOnApplicationThreadAsync(...)`.
