using System;
using System.Diagnostics;
using System.IO;

namespace Chamber19.AutoCad.Mcp.Diagnostics;

/// <summary>
/// Simple file logger for the host assembly (loaded in the custom ALC).
/// Writes to the same log path as the shell's <c>Log</c> class; both append to the same file.
/// Initialized by <see cref="Chamber19.AutoCad.Mcp.Host.McpHostEntry.StartHost"/> with the
/// path provided by the shell.
/// </summary>
internal static class HostLog
{
    private static string _path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Chamber19", "autocad-mcp", "plugin.log");

    private static readonly object SyncRoot = new();

    internal static void Initialize(string logPath)
    {
        lock (SyncRoot)
        {
            _path = logPath;
        }
    }

    public static void Write(string line)
    {
        try
        {
            string path;
            lock (SyncRoot)
            {
                path = _path;
            }
            var formatted = $"[{DateTimeOffset.UtcNow:O}] [tid={Environment.CurrentManagedThreadId,3}] [host] {line}{Environment.NewLine}";
            lock (SyncRoot)
            {
                File.AppendAllText(path, formatted);
            }
            Trace.WriteLine($"[Chamber19.AutoCad.Mcp.Host] {line}");
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
