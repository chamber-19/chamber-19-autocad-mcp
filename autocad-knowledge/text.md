# Text Entities — DBText vs MText

Reference: AutoCAD .NET Developer's Guide — text entities and MText formatting

## Namespace imports

```csharp
using Autodesk.AutoCAD.DatabaseServices; // DBText, MText
using Autodesk.AutoCAD.EditorInput;      // SelectionFilter, PromptStatus
```

## Entity differences

| DXF entity type | .NET class | Raw value property | Plain display property | Position property |
| --- | --- | --- | --- | --- |
| `TEXT` | `DBText` | `TextString` | `TextString` | `Position` |
| `MTEXT` | `MText` | `Contents` | `Text` | `Location` |

For `MText`, `Contents` may include formatting codes (`\\P`, `\\A1;`, etc.).
`Text` is the plain display value with formatting interpreted.

## Selection filter for TEXT/MTEXT on a layer

```csharp
var filter = new SelectionFilter(new[]
{
    new TypedValue((int)DxfCode.LayerName, layerName),
    new TypedValue((int)DxfCode.Operator, "<OR"),
    new TypedValue((int)DxfCode.Start, "TEXT"),
    new TypedValue((int)DxfCode.Start, "MTEXT"),
    new TypedValue((int)DxfCode.Operator, "OR>"),
});
```

Layer name matching is case-insensitive in AutoCAD's selection engine.
Like other `Editor.SelectAll` calls, this searches only the current space.

## Projection pattern

```csharp
switch (entity)
{
    case DBText dbText:
        kind = "DBText";
        value = dbText.TextString;
        plainValue = dbText.TextString;
        position = dbText.Position;
        break;

    case MText mText:
        kind = "MText";
        value = mText.Contents; // formatting preserved
        plainValue = mText.Text; // plain display text
        position = mText.Location;
        break;
}
```

Sort results by handle using case-insensitive hex-string comparison for stable output.

## Threading note

All `Editor` and `Database` calls must run on the AutoCAD application thread.
Dispatch through `AutoCadThreadDispatcher.InvokeOnApplicationThreadAsync(...)`.
