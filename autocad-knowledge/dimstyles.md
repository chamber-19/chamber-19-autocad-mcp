# AutoCAD Dimension Styles — API notes

## Reading dimension styles

Open `Database.DimStyleTableId` as a `DimStyleTable` ForRead inside a read-only transaction, then iterate each `ObjectId` entry and open each `DimStyleTableRecord` ForRead.

```csharp
using var tx = db.TransactionManager.StartTransaction();
var dimStyleTable = (DimStyleTable)tx.GetObject(db.DimStyleTableId, OpenMode.ForRead);

foreach (ObjectId id in dimStyleTable)
{
    var dstr = (DimStyleTableRecord)tx.GetObject(id, OpenMode.ForRead);
    // dstr.Name        — style name (e.g. "Standard", "ARCH")
    // dstr.Dimscale    — overall dimension scale factor (lineScale)
    // dstr.Dimtxt      — primary-units text height (textHeight)
}
tx.Commit();
```

Every drawing includes at least the built-in `Standard` style.

## Key properties

| Property | Type | Notes |
|---|---|---|
| `DimStyleTableRecord.Name` | `string` | Style name as shown in the Dimension Style Manager |
| `DimStyleTableRecord.Dimscale` | `double` | Overall scale factor applied to all dimension geometry. `1.0` = no scale. |
| `DimStyleTableRecord.Dimtxt` | `double` | Text height for primary dimension units in drawing units. |

## Ordering contract

`chamber19_list_dimstyles` returns styles sorted by **name, case-insensitive (ordinal)**. This ordering is stable and guaranteed; clients may depend on it. The sort is applied after the table walk, before serialization.
