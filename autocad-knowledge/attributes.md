# Block Attributes — AutoCAD .NET API

Reference: AutoCAD .NET Developer's Guide — "Working with Block Attributes"

## What are block attributes?

Attributes are tagged, editable text fields attached to a block insertion. Each `BlockReference` placed from a definition with attributes carries its own `AttributeReference` collection. Attributes are commonly used for part numbers, ratings, voltages, and other per-instance metadata.

## Key types

| Type | Assembly | Role |
|---|---|---|
| `BlockTable` | `acdbmgd.dll` | Stores all block definitions in the drawing. |
| `BlockTableRecord` (BTR) | `acdbmgd.dll` | A single block definition or layout. `HasAttributeDefinitions` is `true` when the definition contains attribute definitions. |
| `BlockReference` | `acdbmgd.dll` | A placed instance of a block definition. |
| `AttributeCollection` | `acdbmgd.dll` | Ordered collection of `ObjectId`s for `AttributeReference` objects on a single `BlockReference`. |
| `AttributeReference` | `acdbmgd.dll` | A placed instance of a single attribute on a `BlockReference`. Carries the tag and the current value. |

## Namespace imports

```csharp
using Autodesk.AutoCAD.ApplicationServices; // Application, Document
using Autodesk.AutoCAD.DatabaseServices;    // BlockTable, BlockTableRecord, BlockReference, AttributeReference, ...
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
```

## Reading attributes from the first instance of a named block

> **First-instance limitation:** this pattern returns attributes from the **first**
> `BlockReference` encountered while iterating layout BTRs in block-table iteration order.
> The model-space BTR is visited first, followed by paper-space BTRs. If a block is placed
> multiple times, only the first encountered instance's attributes are captured; all other
> instances are silently ignored. To read attributes from *all* instances, see
> [Finding all instances of a named block](#finding-all-instances-of-a-named-block) below.

```csharp
var doc = Application.DocumentManager.MdiActiveDocument;
var db = doc.Database;
using var tx = db.TransactionManager.StartTransaction();

var blockTable = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);

foreach (ObjectId btrId in blockTable)
{
    var layoutBtr = (BlockTableRecord)tx.GetObject(btrId, OpenMode.ForRead);
    if (!layoutBtr.IsLayout)
    {
        continue; // only walk layout BTRs (model space + paper spaces)
    }

    foreach (ObjectId entityId in layoutBtr)
    {
        var entity = tx.GetObject(entityId, OpenMode.ForRead);
        if (entity is not BlockReference bref)
        {
            continue;
        }

        // Resolve to the canonical definition for dynamic blocks.
        // A dynamic block's customized insertion is an anonymous *U... BTR.
        // DynamicBlockTableRecord always resolves to the original named BTR.
        var effective = (BlockTableRecord)tx.GetObject(bref.DynamicBlockTableRecord, OpenMode.ForRead);
        if (!effective.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        // Found a matching instance — read its attributes.
        foreach (ObjectId attId in bref.AttributeCollection)
        {
            var att = (AttributeReference)tx.GetObject(attId, OpenMode.ForRead);
            string tag   = att.Tag;        // e.g. "PART_NO"
            string value = att.TextString; // e.g. "100-ABC"
        }

        break; // stop at the first matching instance
    }
}

tx.Commit();
```

## Finding all instances of a named block

To count or aggregate across all instances, remove the `break` and accumulate results. Use `DynamicBlockTableRecord` to normalize dynamic-block references so anonymous `*U` variants map back to the named definition:

```csharp
foreach (ObjectId entityId in layoutBtr)
{
    if (tx.GetObject(entityId, OpenMode.ForRead) is not BlockReference bref)
        continue;

    var effective = (BlockTableRecord)tx.GetObject(bref.DynamicBlockTableRecord, OpenMode.ForRead);
    if (effective.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
    {
        // process bref.AttributeCollection ...
    }
}
```

## Key properties on `AttributeReference`

| Property | Type | Notes |
|---|---|---|
| `Tag` | `string` | The attribute tag name, as defined in the block definition. Always uppercase in standard AutoCAD drawings. |
| `TextString` | `string` | The current value of the attribute for this insertion. Empty string if never filled in. |
| `IsConstant` | `bool` | Constant attributes cannot be edited per-instance. Their `TextString` is fixed by the definition. |
| `IsInvisible` | `bool` | Invisible attributes are not displayed on-screen but are readable here. |
| `IsMTextAttribute` | `bool` | True when the attribute is stored as an MText object (rare). Read `MTextAttribute.Contents` instead of `TextString` for multi-line values. |

## Threading note

All `BlockTable` and `BlockReference` reads must run on the AutoCAD application thread. Dispatch via
`AutoCadThreadDispatcher.InvokeOnApplicationThreadAsync(...)`.

## Notes

- `AttributeCollection` is ordered; the order matches the order attributes were defined in the block editor.
- `BlockTableRecord.HasAttributeDefinitions` can be tested before opening references to skip blocks with no attributes — an optimisation for large drawings. The tool does not need this guard because `AttributeCollection` will simply be empty for blocks without attributes.
- For multi-line attribute values (`IsMTextAttribute == true`), access `att.MTextAttribute.Contents` instead of `att.TextString`.
