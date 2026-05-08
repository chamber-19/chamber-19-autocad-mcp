using System;
using System.Diagnostics;
using System.IO;

namespace Chamber19.AutoCad.Mcp.Diagnostics;

internal static class Log
{
    public static string Path { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Chamber19", "autocad-mcp", "plugin.log");

    private static readonly object SyncRoot = new();

    public static void Truncate()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllText(Path, string.Empty);
        }
        catch
        {
            // Logging is best-effort.
        }
    }

    public static void Write(string line)
    {
        try
        {
            var formatted = $"[{DateTimeOffset.UtcNow:O}] [tid={Environment.CurrentManagedThreadId,3}] {line}{Environment.NewLine}";
            lock (SyncRoot)
            {
                File.AppendAllText(Path, formatted);
            }
            Trace.WriteLine($"[Chamber19.AutoCad.Mcp] {line}");
        }
        catch
        {
            // Logging is best-effort.
        }
    }

    public static void WriteException(string message, Exception ex)
    {
        Write(message);
        var current = ex;
        var depth = 0;
        while (current is not null)
        {
            Write($"  Exception[{depth}] {current.GetType().FullName}: {current.Message}");
            if (!string.IsNullOrEmpty(current.StackTrace))
            {
                Write(current.StackTrace);
            }
            current = current.InnerException;
            depth++;
        }
    }
}
