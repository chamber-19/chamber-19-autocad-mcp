# Selection Sets and Filters

Reference: AutoCAD .NET Developer's Guide — "Filtering Selection Sets"

## Namespace imports

```csharp
using Autodesk.AutoCAD.DatabaseServices; // DxfCode, TypedValue, SelectionFilter
using Autodesk.AutoCAD.EditorInput;      // Editor, SelectionResult, PromptStatus
```

`Editor` is obtained from `Document.Editor`; both are in `accoremgd.dll`.

## Building a selection filter

Filters are arrays of `TypedValue` pairs. The first argument is the DXF group code (cast from
`DxfCode`), and the second is the value to match.

### Filter by layer name

```csharp
var filter = new SelectionFilter(new[]
{
    new TypedValue((int)DxfCode.LayerName, "WIR"),
});
```

Layer name matching is **case-insensitive** in AutoCAD's selection engine.

### Filter by entity type

```csharp
var filter = new SelectionFilter(new[]
{
    new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
});
```

### Combining filters (AND semantics — all conditions must match)

```csharp
var filter = new SelectionFilter(new[]
{
    new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
    new TypedValue((int)DxfCode.LayerName, "WIR"),
});
```

### Logical OR via grouping operators

Use `DxfCode.Operator` with `"<OR"` / `"OR>"` to combine conditions with OR semantics:

```csharp
var filter = new SelectionFilter(new[]
{
    new TypedValue((int)DxfCode.Operator, "<OR"),
    new TypedValue((int)DxfCode.Start, "LINE"),
    new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
    new TypedValue((int)DxfCode.Operator, "OR>"),
});
```

## Selecting all entities (no user interaction)

`Editor.SelectAll(SelectionFilter)` selects all entities in the **current space** (model space
when `TILEMODE=1`; active paper space when `TILEMODE=0`) that match the filter.

```csharp
var result = editor.SelectAll(filter);
if (result.Status != PromptStatus.OK)
{
    // No entities matched — count is zero; this is not an error.
    return 0;
}

int count = result.Value.Count;
```

`PromptStatus.OK` means at least one entity matched.  
`PromptStatus.Error` (the common non-OK value here) means no entities matched — treat as 0.

## Threading note

All `Editor` calls must run on the AutoCAD application thread. Dispatch via
`AutoCadThreadDispatcher.InvokeOnApplicationThreadAsync(...)`.

## Performance note

On very large drawings (100k+ entities on a single layer) `SelectAll` can take several hundred
milliseconds. The HTTP backpressure middleware (`UseBackpressure`) limits concurrent requests and
returns `429 Too Many Requests` when the queue is full, preventing runaway accumulation of
slow selection-set calls.
