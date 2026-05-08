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

    public static void Write(string url, string token, int pid, DateTimeOffset started)
    {
        var directory = System.IO.Path.GetDirectoryName(Path)!;
        Directory.CreateDirectory(directory);

        var payload = new
        {
            url,
            token,
            pid,
            started = started.ToString("O"),
        };

        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        var temp = Path + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, Path, overwrite: true);
    }

    public static void Delete()
    {
        try
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
        catch
        {
            // Best-effort cleanup; a stale file won't break the next launch because Write overwrites.
        }
    }
}
