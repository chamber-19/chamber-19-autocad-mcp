using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
/// Per-text snapshot returned by <see cref="TextEnumerationByLayerTool"/>.
/// </summary>
internal sealed record TextEntry(
    string Handle,
    string Kind,
    string Value,
    string PlainValue,
    Coordinate3 Position);

/// <summary>
/// Enumerates text entities on a specified layer in the active drawing (read-only).
/// </summary>
/// <remarks>
/// Uses <see cref="SelectionFilter"/> with a layer condition and OR-group for <c>TEXT</c> and
/// <c>MTEXT</c>, then projects each matched entity to a normalized payload shape.
///
/// For <see cref="DBText"/>:
/// <list type="bullet">
/// <item><c>kind</c> = <c>DBText</c></item>
/// <item><c>value</c> = <see cref="DBText.TextString"/></item>
/// <item><c>plainValue</c> = <see cref="DBText.TextString"/></item>
/// <item><c>position</c> = <see cref="DBText.Position"/></item>
/// </list>
///
/// For <see cref="MText"/>:
/// <list type="bullet">
/// <item><c>kind</c> = <c>MText</c></item>
/// <item><c>value</c> = <see cref="MText.Contents"/> (formatting codes preserved)</item>
/// <item><c>plainValue</c> = <see cref="MText.Text"/> (display text)</item>
/// <item><c>position</c> = <see cref="MText.Location"/></item>
/// </list>
///
/// Results are sorted by handle using case-insensitive hex-string comparison.
/// All AutoCAD interactions run on the application thread via
/// <see cref="HostDispatcher.InvokeOnApplicationThreadAsync{T}"/>.
/// </remarks>
[McpServerToolType]
public static class TextEnumerationByLayerTool
{
    [McpServerTool(Name = "chamber19_text_enumeration_by_layer")]
    [Description("Enumerates all text objects on the specified layer in the active AutoCAD drawing. Includes both DBText and MText entities from the current space. Layer name matching is case-insensitive. Each entry has {handle, kind, value, plainValue, position}. For DBText, value/plainValue both use TextString; for MText, value uses Contents (formatting codes) and plainValue uses Text (display text). Results are sorted by handle using case-insensitive hex comparison. Returns {texts, ts}; texts is empty when no drawing is open or no matching text exists. Read-only; opens a database transaction.")]
    public static async Task<string> TextEnumerationByLayerAsync(
        [Description("Name of the layer to enumerate DBText and MText entities on (case-insensitive).")]
        string layerName)
    {
        var texts = await HostDispatcher.InvokeOnApplicationThreadAsync(
            () => ReadTexts(layerName));
        return Serialize(texts, DateTimeOffset.UtcNow);
    }

    private static IReadOnlyList<TextEntry> ReadTexts(string layerName)
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc is null)
        {
            return Array.Empty<TextEntry>();
        }

        var filter = new SelectionFilter(new[]
        {
            new TypedValue((int)DxfCode.LayerName, layerName),
            new TypedValue((int)DxfCode.Operator, "<OR"),
            new TypedValue((int)DxfCode.Start, "TEXT"),
            new TypedValue((int)DxfCode.Start, "MTEXT"),
            new TypedValue((int)DxfCode.Operator, "OR>"),
        });

        var result = doc.Editor.SelectAll(filter);
        if (result.Status != PromptStatus.OK)
        {
            return Array.Empty<TextEntry>();
        }

        var texts = new List<TextEntry>();

        using var tx = doc.Database.TransactionManager.StartTransaction();
        foreach (var objId in result.Value.GetObjectIds())
        {
            var entity = tx.GetObject(objId, OpenMode.ForRead);
            switch (entity)
            {
                case DBText dbText:
                    texts.Add(new TextEntry(
                        Handle: dbText.Handle.ToString(),
                        Kind: "DBText",
                        Value: dbText.TextString,
                        PlainValue: dbText.TextString,
                        Position: new Coordinate3(dbText.Position.X, dbText.Position.Y, dbText.Position.Z)));
                    break;

                case MText mText:
                    texts.Add(new TextEntry(
                        Handle: mText.Handle.ToString(),
                        Kind: "MText",
                        Value: mText.Contents,
                        PlainValue: mText.Text,
                        Position: new Coordinate3(mText.Location.X, mText.Location.Y, mText.Location.Z)));
                    break;
            }
        }
        tx.Commit();

        return texts
            .OrderBy(t => t.Handle, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Pure JSON shaping logic for the tool response. Exposed for unit testing with mocked
    /// <see cref="TextEntry"/> arrays so tests don't need a real AutoCAD context.
    /// </summary>
    internal static string Serialize(IReadOnlyList<TextEntry> texts, DateTimeOffset ts)
    {
        var payload = new
        {
            texts = texts.Select(text => new
            {
                handle = text.Handle,
                kind = text.Kind,
                value = text.Value,
                plainValue = text.PlainValue,
                position = new
                {
                    x = text.Position.X,
                    y = text.Position.Y,
                    z = text.Position.Z,
                },
            }).ToArray(),
            ts = ts.ToString("O"),
        };
        return JsonSerializer.Serialize(payload);
    }
}