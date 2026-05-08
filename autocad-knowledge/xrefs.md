# External References (Xrefs) — AutoCAD .NET API

## What is an xref?

An external reference (xref) is a drawing file attached to a host drawing. The host drawing stores a `BlockTableRecord` (BTR) that holds the xref definition; `BlockReference` entities in layout BTRs (model space or paper spaces) place the xref on screen. Xrefs come in two modes:

| Mode | Description |
|---|---|
| **Attach** | Nested xrefs are inherited — if drawing A attaches B which attaches C, a host drawing that attaches A also sees C. |
| **Overlay** | Nested xrefs are suppressed — only the immediate file is visible. Preferred when circular references are possible. |

## Getting all xrefs in a drawing

```csharp
var db = doc.Database;
using var tx = db.TransactionManager.StartTransaction();

foreach (ObjectId id in db.XrefBlockTableRecordIds)
{
    var btr = (BlockTableRecord)tx.GetObject(id, OpenMode.ForRead);

    string name     = btr.Name;                    // Xref block name in the host drawing
    string path     = btr.PathName;                // As-stored path (may be relative or UNC)
    bool isLoaded   = btr.IsLoaded;                // True when the xref file was found and loaded
    bool isAttached = !btr.IsFromOverlayReference; // True = Attach mode; false = Overlay mode
}

tx.Commit();
```

### Key properties

| Property | Type | Notes |
|---|---|---|
| `Database.XrefBlockTableRecordIds` | `ObjectIdCollection` | All xref BTR ids — both attached and overlay, loaded or unresolved. Iterate with a `foreach`. |
| `BlockTableRecord.Name` | `string` | The xref alias used inside the host drawing (not the file stem). |
| `BlockTableRecord.PathName` | `string` | Stored path. Can be relative, absolute, or a UNC path. Resolve against the host DWG location to get the full path when needed. |
| `BlockTableRecord.IsLoaded` | `bool` | `true` when AutoCAD has successfully resolved and loaded the external file. `false` for unresolved, not-found, or manually unloaded xrefs. |
| `BlockTableRecord.IsFromExternalReference` | `bool` | `true` for any block originating from an xref (both attach and overlay modes). Use this as a guard when iterating the full BlockTable rather than `XrefBlockTableRecordIds`. |
| `BlockTableRecord.IsFromOverlayReference` | `bool` | `true` when the xref was inserted in **Overlay** mode. Use `!IsFromOverlayReference` to derive an `isAttached` flag. |

## Resolving a relative path

`PathName` is stored as the user typed it (often relative). To resolve:

```csharp
string hostDir = System.IO.Path.GetDirectoryName(db.Filename) ?? string.Empty;
string absolute = System.IO.Path.GetFullPath(System.IO.Path.Combine(hostDir, btr.PathName));
```

Do not mutate `PathName` on the BTR — it is stored in the DWG and a round-trip change would mark the drawing modified.

## Notes

- `XrefBlockTableRecordIds` returns ids for **all** xref BTRs regardless of load state. There is no need to fall back to iterating the full BlockTable and filtering on `IsFromExternalReference`.
- Nested xrefs (xrefs within xrefs) appear as additional BTRs with their own `XrefBlockTableRecordIds` entries if they have been loaded at least once. An overlay xref suppresses its own nested xrefs from appearing.
- Opening a BTR `OpenMode.ForWrite` to bind or detach an xref requires the calling code to run on the AutoCAD application thread. Use `AutoCadThreadDispatcher.InvokeOnApplicationThreadAsync` for all xref reads and writes dispatched from Kestrel.
