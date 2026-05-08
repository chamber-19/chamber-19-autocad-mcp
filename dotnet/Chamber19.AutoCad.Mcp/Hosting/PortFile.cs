using System;
using System.IO;
using System.Text.Json;

namespace Chamber19.AutoCad.Mcp.Hosting;

/// <summary>
/// Writes/deletes the per-process MCP discovery file at
/// <c>%LOCALAPPDATA%\Chamber19\autocad-mcp\port.&lt;pid&gt;.txt</c>. The pid suffix is what
/// makes the layout multi-instance safe — two AutoCAD sessions never race on the same file.
/// Clients glob <c>port.*.txt</c> in <see cref="DirectoryPath"/> to discover all running
/// MCP servers on the host.
/// </summary>
internal static class PortFile
{
    public static string DirectoryPath { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Chamber19", "autocad-mcp");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
    };

    /// <summary>Returns the canonical port-file path for a given AutoCAD process id.</summary>
    public static string GetPath(int pid) =>
        System.IO.Path.Combine(DirectoryPath, $"port.{pid}.txt");

    public static void Write(string url, string token, int pid, DateTimeOffset started) =>
        Write(GetPath(pid), url, token, pid, started);

    public static void Delete(int pid) => Delete(GetPath(pid));

    // Path-parameterized overloads exist so tests can target a temp directory instead of the
    // canonical %LOCALAPPDATA% path. Public Write/Delete delegate here.
    internal static void Write(string targetPath, string url, string token, int pid, DateTimeOffset started)
    {
        var directory = System.IO.Path.GetDirectoryName(targetPath)!;
        Directory.CreateDirectory(directory);

        var payload = new
        {
            url,
            token,
            pid,
            started = started.ToString("O"),
        };

        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        var temp = targetPath + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, targetPath, overwrite: true);
    }

    internal static void Delete(string targetPath)
    {
        try
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }
        catch
        {
            // Best-effort cleanup; a stale file won't break the next launch because Write overwrites.
        }
    }
}
