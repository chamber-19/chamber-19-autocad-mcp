# External References (Xrefs) — AutoCAD .NET API

## What is an xref?

An external reference (xref) is a drawing file attached to a host drawing. The host drawing stores a `BlockTableRecord` (BTR) that holds the xref definition; `BlockReference` entities in layout BTRs (model space or paper spaces) place the xref on screen. Xrefs come in two modes:

| Mode | Description |
| --- | --- |
| **Attach** | Nested xrefs are inherited — if drawing A attaches B which attaches C, a host drawing that attaches A also sees C. |
| **Overlay** | Nested xrefs are suppressed — only the immediate file is visible. Preferred when circular references are possible. |

## Getting all xrefs in a drawing

```csharp
var db = doc.Database;
using var tx = db.TransactionManager.StartTransaction();

using var xrefGraph = db.GetHostDwgXrefGraph(includeGhosts: true);
for (int i = 0; i < xrefGraph.NumNodes; i++)
{
    var node = xrefGraph.GetXrefNode(i);
    ObjectId id = node.BlockTableRecordId;
    if (id == ObjectId.Null)
        continue;

    var btr = (BlockTableRecord)tx.GetObject(id, OpenMode.ForRead);
    if (!btr.IsFromExternalReference)
        continue; // skips the host/root node

    string name     = btr.Name;                    // Xref block name in the host drawing
    string path     = btr.PathName;                // As-stored path (may be relative or UNC)
    bool isLoaded   = btr.XrefStatus == XrefStatus.Resolved;
    bool isAttached = !btr.IsFromOverlayReference; // True = Attach mode; false = Overlay mode
}

tx.Commit();
```

### Key properties

| Property | Type | Notes |
| --- | --- | --- |
| `Database.GetHostDwgXrefGraph(true)` | `XrefGraph` | Returns the host drawing xref graph. Iterate nodes and skip root/non-xref nodes by checking `IsFromExternalReference`. |
| `BlockTableRecord.Name` | `string` | The xref alias used inside the host drawing (not the file stem). |
| `BlockTableRecord.PathName` | `string` | Stored path. Can be relative, absolute, or a UNC path. Resolve against the host DWG location to get the full path when needed. |
| `BlockTableRecord.XrefStatus` | `XrefStatus` | Use `XrefStatus.Resolved` as the "loaded" state. Other values include `Unloaded`, `FileNotFound`, and `Unresolved`. |
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

- `GetHostDwgXrefGraph(true)` includes the host/root node. Guard with `IsFromExternalReference` to keep only true xref BTRs.
- Nested xrefs (xrefs within xrefs) appear as additional graph nodes when resolved. Overlay behavior still controls propagation.
- Opening a BTR `OpenMode.ForWrite` to bind or detach an xref requires the calling code to run on the AutoCAD application thread. Use `AutoCadThreadDispatcher.InvokeOnApplicationThreadAsync` for all xref reads and writes dispatched from Kestrel.
