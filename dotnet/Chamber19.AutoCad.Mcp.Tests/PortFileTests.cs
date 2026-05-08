using System;
using System.IO;
using System.Text.Json;
using Chamber19.AutoCad.Mcp.Hosting;
using Xunit;

namespace Chamber19.AutoCad.Mcp.Tests;

public sealed class PortFileTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _targetPath;

    public PortFileTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"chamber19-portfile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _targetPath = Path.Combine(_tempDir, "port.txt");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Best-effort test cleanup.
        }
    }

    [Fact]
    public void Write_CreatesFile_WithExpectedJsonShape()
    {
        var started = new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);

        PortFile.Write(_targetPath, "http://127.0.0.1:5001/", "token-abc", 1234, started);

        Assert.True(File.Exists(_targetPath));
        Assert.False(File.Exists(_targetPath + ".tmp"), "temp file should be removed after atomic move");

        using var doc = JsonDocument.Parse(File.ReadAllText(_targetPath));
        var root = doc.RootElement;

        // Field order matches the public spec the user dictated for port.txt.
        var properties = root.EnumerateObject().Select(p => p.Name).ToArray();
        Assert.Equal(new[] { "url", "token", "pid", "started" }, properties);

        Assert.Equal("http://127.0.0.1:5001/", root.GetProperty("url").GetString());
        Assert.Equal("token-abc", root.GetProperty("token").GetString());
        Assert.Equal(1234, root.GetProperty("pid").GetInt32());
        Assert.Equal(started.ToString("O"), root.GetProperty("started").GetString());
    }

    [Fact]
    public void Write_OverwritesExistingFile_AtomicMove()
    {
        File.WriteAllText(_targetPath, "stale content from prior crashed session");

        PortFile.Write(_targetPath, "http://127.0.0.1:5002/", "new-token", 5678, DateTimeOffset.UtcNow);

        var content = File.ReadAllText(_targetPath);
        Assert.DoesNotContain("stale content", content);
        using var doc = JsonDocument.Parse(content);
        Assert.Equal(5678, doc.RootElement.GetProperty("pid").GetInt32());
    }

    [Fact]
    public void Write_CreatesParentDirectory_IfMissing()
    {
        var nestedPath = Path.Combine(_tempDir, "nested", "deeper", "port.txt");
        Assert.False(File.Exists(Path.GetDirectoryName(nestedPath)!));

        PortFile.Write(nestedPath, "http://127.0.0.1:5003/", "tok", 9, DateTimeOffset.UtcNow);

        Assert.True(File.Exists(nestedPath));
    }

    [Fact]
    public void Delete_RemovesFile_AndIsIdempotent()
    {
        File.WriteAllText(_targetPath, "{}");

        PortFile.Delete(_targetPath);
        Assert.False(File.Exists(_targetPath));

        // Second call on a missing file must not throw.
        PortFile.Delete(_targetPath);
    }
}
