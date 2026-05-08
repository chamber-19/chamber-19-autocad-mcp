using System;
using System.IO;
using System.Text.Json;

namespace Chamber19.AutoCad.Mcp.Hosting;

internal static class PortFile
{
    public static string Path { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Chamber19", "autocad-mcp", "port.txt");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
    };

    public static void Write(string url, string token, int pid, DateTimeOffset started) =>
        Write(Path, url, token, pid, started);

    public static void Delete() => Delete(Path);

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
