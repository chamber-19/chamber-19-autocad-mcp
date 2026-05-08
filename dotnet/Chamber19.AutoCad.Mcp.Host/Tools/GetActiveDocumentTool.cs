using System;
using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Chamber19.AutoCad.Mcp.Threading;
using ModelContextProtocol.Server;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Chamber19.AutoCad.Mcp.Tools;

[McpServerToolType]
public static class GetActiveDocumentTool
{
    [McpServerTool(Name = "chamber19_get_active_document")]
    [Description("Returns metadata about the active AutoCAD drawing: document name, full file path, modified state, modelspace entity count, and current UTC timestamp. All AutoCAD reads dispatch onto the application thread via AutoCadThreadDispatcher.")]
    public static async Task<string> GetActiveDocumentAsync()
    {
        var json = await HostDispatcher.InvokeOnApplicationThreadAsync(() =>
        {
            var ts = DateTimeOffset.UtcNow.ToString("O");
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc is null)
            {
                return JsonSerializer.Serialize(new
                {
                    name = (string?)null,
                    path = (string?)null,
                    isModified = (bool?)null,
                    modelSpaceEntityCount = (int?)null,
                    ts,
                });
            }

            var db = doc.Database;
            var path = string.IsNullOrEmpty(db.Filename) ? null : db.Filename;
            var dbmod = Convert.ToInt32(Application.GetSystemVariable("DBMOD"));

            using var tx = db.TransactionManager.StartTransaction();
            var blockTable = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
            var modelSpace = (BlockTableRecord)tx.GetObject(
                blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            var modelSpaceEntityCount = 0;
            foreach (var _ in modelSpace)
            {
                modelSpaceEntityCount++;
            }
            tx.Commit();

            return JsonSerializer.Serialize(new
            {
                name = doc.Name,
                path,
                isModified = dbmod != 0,
                modelSpaceEntityCount,
                ts,
            });
        });

        return json;
    }
}
