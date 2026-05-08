namespace Chamber19.AutoCad.Mcp.Hosting;

internal sealed record PluginSnapshot(string AutoCadVersion, string Plugin, string Runtime)
{
    private static PluginSnapshot _current = new("unknown", "Chamber19.AutoCad.Mcp", "unknown");

    public static PluginSnapshot Current => _current;

    public static void Set(PluginSnapshot snapshot) => _current = snapshot;
}
