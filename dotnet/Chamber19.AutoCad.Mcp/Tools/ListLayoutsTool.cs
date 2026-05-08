using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Chamber19.AutoCad.Mcp.Threading;
using ModelContextProtocol.Server;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Chamber19.AutoCad.Mcp.Tools;

/// <summary>
/// Per-layout snapshot returned by <see cref="ListLayoutsTool"/>.
/// </summary>
internal sealed record LayoutInfo(
    string Name,
    bool IsCurrent,
    int TabOrder);

/// <summary>
/// Lists all layouts (tabs) in the active drawing (read-only).
/// </summary>
/// <remarks>
/// API pattern from <c>autocad-knowledge/layouts.md</c>: open
/// <see cref="Database.LayoutDictionaryId"/> as a <see cref="DBDictionary"/> ForRead,
/// iterate its entries, open each <see cref="Layout"/> ForRead, and project to
/// <see cref="LayoutInfo"/>. The current tab name is resolved via
/// <see cref="LayoutManager.Current"/>.
///
/// All AutoCAD reads run on the application thread via
/// <see cref="AutoCadThreadDispatcher.InvokeOnApplicationThreadAsync{T}"/>.
/// </remarks>
[McpServerToolType]
public static class ListLayoutsTool
{
    [McpServerTool(Name = "chamber19_list_layouts")]
    [Description("Lists all layout tabs in the active AutoCAD drawing. Each entry has name, isCurrent (true for the active tab), and tabOrder (0 = Model, 1+ = paper-space tabs in left-to-right order). Returns an empty layouts array when no drawing is open. Read-only; opens a database transaction.")]
    public static async Task<string> ListLayoutsAsync()
    {
        var layouts = await AutoCadThreadDispatcher.InvokeOnApplicationThreadAsync(ReadLayouts);
        return Serialize(layouts, DateTimeOffset.UtcNow);
    }

    private static IReadOnlyList<LayoutInfo> ReadLayouts()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc is null)
        {
            return Array.Empty<LayoutInfo>();
        }

        var db = doc.Database;
        string currentLayoutName = LayoutManager.Current.CurrentLayout;

        using var tx = db.TransactionManager.StartTransaction();
        var layoutDict = (DBDictionary)tx.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);

        var result = new List<LayoutInfo>();
        foreach (DBDictionaryEntry entry in layoutDict)
        {
            var layout = (Layout)tx.GetObject(entry.Value, OpenMode.ForRead);
            result.Add(new LayoutInfo(
                Name: layout.LayoutName,
                IsCurrent: string.Equals(layout.LayoutName, currentLayoutName, StringComparison.OrdinalIgnoreCase),
                TabOrder: layout.TabOrder));
        }

        tx.Commit();

        return result
            .OrderBy(l => l.TabOrder)
            .ToList();
    }

    /// <summary>
    /// Pure JSON shaping logic for the tool response. Exposed for unit testing with mocked
    /// <see cref="LayoutInfo"/> arrays so tests don't need a real AutoCAD context.
    /// </summary>
    internal static string Serialize(IReadOnlyList<LayoutInfo> layouts, DateTimeOffset ts)
    {
        var payload = new
        {
            layouts = layouts.Select(l => new
            {
                name = l.Name,
                isCurrent = l.IsCurrent,
                tabOrder = l.TabOrder,
            }).ToArray(),
            ts = ts.ToString("O"),
        };
        return JsonSerializer.Serialize(payload);
    }
}
