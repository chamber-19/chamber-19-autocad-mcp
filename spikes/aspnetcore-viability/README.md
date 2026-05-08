# AspNetCore-in-AutoCAD viability spike

**Status: COMPLETE — Kestrel boots cleanly inside acad.exe 2027.** Test on 2026-05-07 confirmed `WebApplication.CreateBuilder()` → `Build()` → `StartAsync()` succeeds in ~53ms on the AutoCAD application thread, ASP.NET Core shared framework resolves via the plugin's deps.json/runtimeconfig.json, and the `/health` endpoint responds correctly to external `curl`. Commit 1 of the production plugin proceeds with HTTP transport via `ModelContextProtocol.AspNetCore`.

This folder is kept as historical reference. Do not modify or extend it — open new probes under `spikes/<new-name>/` if needed.

---

**Original goal:** prove (or disprove) that Kestrel can host an HTTP server inside `acad.exe` 2027's CLR when loaded as a NETLOAD plugin.

This is a throwaway probe before commit 1 of the real plugin. No MCP, no tools, no domain logic. Just: `IExtensionApplication.Initialize()` → start Kestrel on `127.0.0.1:0` with a single `/health` endpoint → log everything to a file → `Terminate()` → stop cleanly.

## Build

```powershell
dotnet build .\Chamber19.AutoCad.Mcp.Spike.csproj -c Release
```

Output: `bin\Release\net8.0-windows\Chamber19.AutoCad.Mcp.Spike.dll`

## Run

1. Launch AutoCAD 2027.
2. At the command line: `NETLOAD` → pick the spike DLL above.
3. Initialize() runs immediately on load. A log file appears at:
   ```
   %LOCALAPPDATA%\Chamber19\autocad-mcp-spike\spike.log
   ```
4. At the AutoCAD command line: `MCPSPIKESTATUS` → prints whether Kestrel is running, the bound URL, or a one-line error.
5. From outside AutoCAD: `curl http://127.0.0.1:<port>/health` should return `{"ok":true,...}` if Kestrel is up.
6. Close AutoCAD to trigger `Terminate()`. Re-open the log to confirm clean shutdown.

## What we want to learn

- Does `WebApplication.CreateBuilder()` succeed inside acad.exe's CLR?
- Does `app.StartAsync()` bind a socket?
- Does the ASP.NET Core shared framework resolve correctly when the plugin is dynamically loaded? (FrameworkReference + EnableDynamicLoading + GenerateRuntimeConfigurationFiles)
- Does `Terminate()` shut Kestrel down without hanging AutoCAD's exit?

## Possible failure modes (what to look for in the log)

- `FileNotFoundException` for `Microsoft.AspNetCore.*` — shared framework not resolved by the dynamic loader; would push us to standalone Kestrel packages or copying assemblies locally.
- `SocketException` on bind — port binding rejected (firewall, AppContainer, etc.).
- Long hang at `app.StartAsync()` — host build deadlock; would push us off `WebApplication` toward bare `IHostBuilder`.
- AutoCAD hangs on close — `Terminate()` not draining cleanly; would need background-thread shutdown.
