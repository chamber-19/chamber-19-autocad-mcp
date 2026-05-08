# Dimension Styles — AutoCAD .NET API

Reference: AutoCAD .NET Developer's Guide — "Working with Dimension Styles"

## What is a dimension style?

A dimension style (dimstyle) is a named collection of settings that controls how AutoCAD draws and annotates dimensions — arrowhead size, text height, unit format, tolerances, colors, etc. Every DWG always contains at least one built-in style: **Standard**. All dimension objects (`Dimension` and its subclasses) reference a style by its `DimStyleTableRecord` ObjectId.

Styles are stored in the `DimStyleTable`, accessed via `Database.DimStyleTableId`.

## Key types

| Type | Assembly | Role |
|---|---|---|
| `DimStyleTable` | `acdbmgd.dll` | Symbol table that holds all dimension style definitions in the drawing. |
| `DimStyleTableRecord` | `acdbmgd.dll` | A single dimension style definition. Exposes all `Dim*` variables as typed properties. |

## Namespace imports

```csharp
using Autodesk.AutoCAD.ApplicationServices; // Application, Document
using Autodesk.AutoCAD.DatabaseServices;    // DimStyleTable, DimStyleTableRecord, ...
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
```

## Reading all dimension styles

Open `Database.DimStyleTableId` as a `DimStyleTable` ForRead inside a read-only transaction, then iterate each `ObjectId` entry and open each `DimStyleTableRecord` ForRead.

```csharp
var doc = Application.DocumentManager.MdiActiveDocument;
var db  = doc.Database;

using var tx = db.TransactionManager.StartTransaction();
var dimStyleTable = (DimStyleTable)tx.GetObject(db.DimStyleTableId, OpenMode.ForRead);

foreach (ObjectId id in dimStyleTable)
{
    var dstr = (DimStyleTableRecord)tx.GetObject(id, OpenMode.ForRead);

    string name      = dstr.Name;      // e.g. "Standard", "ARCH", "ISO-25"
    double lineScale = dstr.Dimscale;  // overall scale factor (1.0 = no scale)
    double textHeight = dstr.Dimtxt;   // primary-units text height in drawing units
}

tx.Commit();
```

Every drawing includes at least the built-in `Standard` style. `DimStyleTable` iteration order is not guaranteed; sort results explicitly before returning them to clients.

## Key properties

### Geometry

| Property | Type | Notes |
|---|---|---|
| `DimStyleTableRecord.Dimscale` | `double` | Overall scale factor applied to all dimension geometry. `1.0` = no scale. Multiply all other size variables by this to get the effective drawn size. |
| `DimStyleTableRecord.Dimasz` | `double` | Arrowhead size in drawing units (before `Dimscale`). |
| `DimStyleTableRecord.Dimexo` | `double` | Extension line offset from the dimensioned point. |
| `DimStyleTableRecord.Dimdli` | `double` | Baseline spacing — distance between successive baseline dimension lines. |
| `DimStyleTableRecord.Dimgap` | `double` | Gap between the dimension text and the dimension line. |

### Text

| Property | Type | Notes |
|---|---|---|
| `DimStyleTableRecord.Dimtxt` | `double` | Text height for primary dimension units in drawing units (before `Dimscale`). |
| `DimStyleTableRecord.Dimtxsty` | `ObjectId` | ObjectId of the `TextStyleTableRecord` used for dimension text. Open ForRead to get the text style name. |
| `DimStyleTableRecord.Dimtad` | `int` | Text placement relative to dimension line: `0` = centered, `1` = above, `4` = below. |

### Units

| Property | Type | Notes |
|---|---|---|
| `DimStyleTableRecord.Dimlunit` | `int` | Linear units format: `1` = Scientific, `2` = Decimal, `3` = Engineering, `4` = Architectural, `5` = Fractional, `6` = Desktop. |
| `DimStyleTableRecord.Dimdec` | `int` | Number of decimal places for primary units. |
| `DimStyleTableRecord.Dimrnd` | `double` | Rounding value for primary units distances. `0.0` = no rounding. |
| `DimStyleTableRecord.Dimpost` | `string` | Suffix appended to primary unit measurements (e.g., `" mm"`). Prefix uses `<>` placeholder: `"R<>"` prepends "R". |

### Colors

| Property | Type | Notes |
|---|---|---|
| `DimStyleTableRecord.Dimclrd` | `Color` | Color of dimension lines and arrowheads. |
| `DimStyleTableRecord.Dimclre` | `Color` | Color of extension lines. |
| `DimStyleTableRecord.Dimclrt` | `Color` | Color of dimension text. |

### Arrowheads

| Property | Type | Notes |
|---|---|---|
| `DimStyleTableRecord.Dimblk` | `ObjectId` | Block used for both arrowheads when `Dimsah` is `false`. `ObjectId.Null` = built-in filled arrowhead. |
| `DimStyleTableRecord.Dimblk1` | `ObjectId` | First (start) arrowhead block when `Dimsah` is `true`. |
| `DimStyleTableRecord.Dimblk2` | `ObjectId` | Second (end) arrowhead block when `Dimsah` is `true`. |
| `DimStyleTableRecord.Dimsah` | `bool` | When `true`, `Dimblk1` and `Dimblk2` specify separate arrowheads for each end. |

## Tool contract — `chamber19_list_dimstyles`

### JSON response shape

```json
{
  "dimStyles": [
    {
      "name": "ARCH",
      "lineScale": 48.0,
      "textHeight": 0.09375
    },
    {
      "name": "Standard",
      "lineScale": 1.0,
      "textHeight": 0.18
    }
  ],
  "ts": "2026-05-01T12:00:00.0000000+00:00"
}
```

| Field | Source | Notes |
|---|---|---|
| `name` | `DimStyleTableRecord.Name` | Style name as shown in the Dimension Style Manager. |
| `lineScale` | `DimStyleTableRecord.Dimscale` | Overall dimension scale factor. `1.0` = no scaling. |
| `textHeight` | `DimStyleTableRecord.Dimtxt` | Primary-units text height in drawing units. |
| `ts` | `DateTimeOffset.UtcNow` | ISO 8601 timestamp of when the snapshot was taken. |

### Ordering contract

Results are sorted by **name, case-insensitive ordinal**. This ordering is stable and guaranteed; clients may depend on it. The sort is applied after the table walk, before serialization.

### Empty-document behavior

When no drawing is open (`MdiActiveDocument` is `null`), the tool returns `{"dimStyles": [], "ts": "..."}` — an empty array with a valid timestamp. This is not an error condition.

## Threading note

All `DimStyleTable` reads must run on the AutoCAD application thread. Dispatch via
`AutoCadThreadDispatcher.InvokeOnApplicationThreadAsync(...)`.

## Notes

- `DimStyleTable` iteration order is unspecified and should not be relied upon; always sort results explicitly.
- The `Standard` style is always present and cannot be deleted. It is included in the `chamber19_list_dimstyles` result like any other style.
- Dimension objects that have been overridden at the object level may not reflect the style's stored `Dim*` values. Object-level overrides are stored on the `Dimension` entity itself, not on the `DimStyleTableRecord`.
- To resolve the text style name from `Dimtxsty`, open the returned `ObjectId` as a `TextStyleTableRecord` ForRead and read its `Name` property.
- Modifying `DimStyleTableRecord` properties (e.g., renaming a style) requires `OpenMode.ForWrite` and must run on the AutoCAD application thread. Always commit the transaction after writes.
