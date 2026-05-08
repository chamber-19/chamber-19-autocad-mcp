# Layouts — AutoCAD .NET API

## What is a layout?

A layout is a named paper-space tab (or the special Model tab) that holds a viewport arrangement for plotting. Every DWG always has at least one layout: **Model** (the model-space tab). Paper-space layouts are added by the user and each corresponds to a `BlockTableRecord` plus a `Layout` object stored in the Layout Dictionary.

| Tab | Block name | Notes |
|---|---|---|
| **Model** | `*Model_Space` | Always present. `Layout.TabOrder == 0`. |
| **Paper space 1** | `*Paper_Space` | The first paper-space tab. `TabOrder == 1`. |
| **Paper space N** | `*Paper_Space0`, `*Paper_Space1`, … | Additional paper-space tabs. Tab order increments. |

## Getting all layouts in a drawing

```csharp
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

var doc = Application.DocumentManager.MdiActiveDocument;
var db  = doc.Database;

// Name of the currently active layout tab.
string currentLayoutName = LayoutManager.Current.CurrentLayout;

using var tx = db.TransactionManager.StartTransaction();

// LayoutDictionaryId points to a DBDictionary whose values are Layout objects.
var layoutDict = (DBDictionary)tx.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);

var results = new List<(string Name, bool IsCurrent, int TabOrder)>();
foreach (DBDictionaryEntry entry in layoutDict)
{
    var layout = (Layout)tx.GetObject(entry.Value, OpenMode.ForRead);

    string name      = layout.LayoutName;
    bool   isCurrent = string.Equals(name, currentLayoutName, StringComparison.OrdinalIgnoreCase);
    int    tabOrder  = layout.TabOrder;

    results.Add((name, isCurrent, tabOrder));
}

tx.Commit();

// Sort by tab order so the list reflects the left-to-right tab strip.
results.Sort((a, b) => a.TabOrder.CompareTo(b.TabOrder));
```

### Key properties

| Property | Type | Notes |
|---|---|---|
| `Database.LayoutDictionaryId` | `ObjectId` | Points to the `DBDictionary` that maps layout name → `Layout` ObjectId. |
| `Layout.LayoutName` | `string` | The human-visible tab name (e.g., `"Model"`, `"Sheet 1"`). |
| `Layout.TabOrder` | `int` | Left-to-right tab position. Model is always `0`; paper-space tabs start at `1`. |
| `LayoutManager.Current.CurrentLayout` | `string` | Name of the currently active tab. `LayoutManager` is a singleton accessed via the static `Current` property. |

## Notes

- `DBDictionary` iteration yields `DBDictionaryEntry` structs with `Key` (the layout name string) and `Value` (the `ObjectId` of the `Layout` record). You can use either the key or `Layout.LayoutName`; they are the same string.
- Sorting by `TabOrder` is recommended before returning results so that consumers receive tabs in the visual left-to-right order rather than dictionary insertion order.
- Opening `Layout` objects `OpenMode.ForWrite` (e.g., to rename a tab) must happen on the AutoCAD application thread. Use `AutoCadThreadDispatcher.InvokeOnApplicationThreadAsync` for all layout reads and writes dispatched from Kestrel.
- `LayoutManager.Current` is safe to call from any thread for reads; the `CurrentLayout` property returns the name of the currently active layout.
