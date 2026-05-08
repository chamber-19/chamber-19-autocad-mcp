# chamber-19-autocad-mcp

A C# AutoCAD .NET plugin that loads inside `acad.exe` and acts as an MCP server. Third-party MCP clients (Claude Desktop, Claude Code, custom LLM clients, Foundry agents) connect to it over HTTP and drive AutoCAD through the documented .NET API and COM surfaces.

This is **not** a wrapper around Autodesk's internal `acmcp.dll`. We host our own MCP server inside acad.exe and expose our own tools.

## Known limitations — read before installing

> **Autodesk's `AutoCAD-MCP-Server-2027.bundle` must be disabled before installing this plugin.** Both plugins ship the `ModelContextProtocol` C# SDK, both load into AutoCAD's default `AssemblyLoadContext`, and the version mismatch (Autodesk pins `0.4.0`, we use a current release) causes our plugin to fail at startup with a `TypeLoadException`. Until [issue #3 — ALC isolation for MCP SDK to coexist with Autodesk's acmcp.dll bundle](https://github.com/chamber-19/chamber-19-autocad-mcp/issues/3) is resolved, you must rename Autodesk's bundle folder before installing this one:
>
> ```powershell
> # Run from an elevated PowerShell. Reverse with the opposite rename.
> Rename-Item "C:\Program Files\Autodesk\ApplicationPlugins\AutoCAD-MCP-Server-2027.bundle" `
>             "AutoCAD-MCP-Server-2027.bundle-disabled"
> ```
>
> Restart AutoCAD after renaming. Without this step, our plugin's `MCPSTATUS` will show `MCP server FAILED to start` and the HTTP server will not bind.

## Status

**Commit 12 — `chamber19_count_entities_by_layer`.** Counts all entities on a named layer in the active drawing using `Editor.SelectAll(SelectionFilter)` with a single `DxfCode.LayerName` TypedValue. Searches the current space (model space when `TILEMODE=1`, active paper space otherwise); layer name matching is case-insensitive in AutoCAD's selection engine. Returns `{count, ts}` where `count=0` when no drawing is open, the layer does not exist, or no entities reside on it. Pattern from `autocad-knowledge/selection_sets.md`. 4 new tests in `CountEntitiesByLayerToolTests`; 55 tests total.

**Commit 11 — `chamber19_list_dimstyles`.** Lists all dimension styles in the active drawing. Opens `Database.DimStyleTableId` as a `DimStyleTable` ForRead inside a read-only Transaction, projects each `DimStyleTableRecord` to `{name, lineScale, textHeight}` where `lineScale` is `Dimscale` (overall scale factor) and `textHeight` is `Dimtxt` (primary-units text height). Returns `{dimStyles: [{name, lineScale, textHeight}], ts}`. 4 new tests in `ListDimStylesToolTests`; 51 tests total.

**Commit 10 — per-PID port discovery + harness expansion.** Refactored `PortFile` from a single `port.txt` to per-process `port.{pid}.txt` at `%LOCALAPPDATA%\Chamber19\autocad-mcp\`. Multiple AutoCAD instances no longer race on a shared file; clients glob `port.*.txt` in the directory to discover all running MCP servers on the host. Added `PortFile.GetPath(pid)` and expanded the test harness with `PortFileTests.GetPath_*` (2 new tests), `AutoCadCompatibilityTests` (5 tests, including 1 theory with 7 cases), `AutoCadThreadDispatcherTests` (6 tests covering queue-cap and initialization guards), and `BackpressureTests` (4 tests covering the 429 middleware via ASP.NET TestHost); 47 tests total after this commit.

**Commit 9 — `chamber19_get_block_attributes`.** Reads attribute values from the first placed instance of a named block. Walks every layout BTR (model space + paper spaces) inside a read-only Transaction, finds the first `BlockReference` whose effective definition (resolved via `DynamicBlockTableRecord`) matches the requested `blockName` (case-insensitive), then iterates its `AttributeCollection` and opens each `AttributeReference` ForRead to capture `Tag` and `TextString`. Returns `{attributes: [{tag, value}], ts}`. Returns an empty `attributes` array when no drawing is open, the block is not found, or the first instance has no attributes. Pattern from `autocad-knowledge/attributes.md`. 4 new tests in `GetBlockAttributesToolTests`; 30 tests total.

**Commit 8 — `chamber19_list_layouts`.** Lists all layout tabs in the active drawing. Iterates `Database.LayoutDictionaryId` (a `DBDictionary`) inside a read-only Transaction, opens each `Layout` ForRead, and projects to `{name, isCurrent, tabOrder}`. `isCurrent` is derived from `LayoutManager.Current.CurrentLayout`. Results are sorted by `tabOrder` (0 = Model, 1+ = paper-space tabs). Returns `{layouts: [{name, isCurrent, tabOrder}], ts}`. Pattern documented in the new `autocad-knowledge/layouts.md`. 4 new tests in `ListLayoutsToolTests`; 26 tests total.

**Commit 7 — `chamber19_list_xrefs`.** Third "must-have" read tool. Iterates `Database.XrefBlockTableRecordIds` inside a read-only Transaction, opens each `BlockTableRecord` ForRead, and projects to `{name, path, isLoaded, isAttached}`. `isAttached` is `!btr.IsFromOverlayReference` — `true` = Attach mode, `false` = Overlay mode. Sorted by name (case-insensitive). Returns `{xrefs: [{name, path, isLoaded, isAttached}], ts}`. Pattern documented in the new `autocad-knowledge/xrefs.md`. 4 new tests in `ListXrefsToolTests`; 22 tests total.

**Commit 6 — `chamber19_list_blocks`.** Second "must-have" read tool. Two-pass walk inside a read-only Transaction: pass 1 collects user block definitions from the BlockTable (skips anonymous `*U`/`*X`/`*D` and layout BTRs, captures `IsDynamicBlock`); pass 2 walks layout BTRs (model space + paper spaces) and resolves every `BlockReference` via `DynamicBlockTableRecord` so dynamic-block customized variants count toward the original definition. Returns `{blocks: [{name, referenceCount, isDynamic}], ts}`. Pattern from `autocad-knowledge/attributes.md` "Finding all instances of a named block". 4 tests in the harness pattern; 18 tests total now.

**Commit 5 — `chamber19_list_layers`.** First "must-have" read tool. Iterates the LayerTable inside a read-only Transaction (pattern from `autocad-knowledge/layers.md`), projects each LayerTableRecord to `{name, colorIndex, isFrozen, isLocked, isOff, isPlottable}`, returns `{layers, ts}`. Dispatched via `AutoCadThreadDispatcher.InvokeOnApplicationThreadAsync`. Pure `Serialize` method exposed for unit testing with mocked LayerInfo arrays — 4 tests cover empty, field-order, typical 5-layer set with mixed flags, and Unicode names.

**Commit 4 — test harness.** Adds `dotnet/Chamber19.AutoCad.Mcp.Tests/` (xUnit v3, `net10.0-windows`) with tests for the existing low-level pieces: `BearerAuth` middleware (missing token / invalid token / valid token / empty bearer all assert correct status + RFC 6750 `WWW-Authenticate` header + JSON body), `PortFile.Write` (atomic temp-then-move, JSON shape, parent-dir creation, overwrite, idempotent delete), and `PortAllocator.FindFreePort` (returns a port in 5001–5050; skips an already-bound port). Establishes the harness; future tools land their tests here. Run via `dotnet test`.

**Commit 3 — AutoCAD-thread dispatcher and first database-reading tool.** Adds `AutoCadThreadDispatcher` in `Threading/`, lifted from Suite's `SuiteCadPipeHost.InvokeOnApplicationThread` pattern: captures the AutoCAD application thread id during `Initialize`, attaches `Application.Idle` handler, queues callbacks from background threads (Kestrel) and runs them on the UI thread on the next idle tick. Async API: `Task InvokeOnApplicationThreadAsync(Action)` and `Task<T> InvokeOnApplicationThreadAsync<T>(Func<T>)`. The first tool that exercises the dispatcher is `chamber19_get_active_document`, returning `{name, path, isModified, modelSpaceEntityCount, ts}`. Builds on commit 2's MCP host (`chamber19_ping`, port file, bearer auth, RFC 6750 401).

## Requirements

- AutoCAD 2025, 2026, or 2027 (Windows x64). 2027 is the primary target.
- .NET 10 SDK (also builds against .NET 8 for AutoCAD 2025/2026 target).

## Quick start

```powershell
# Build for AutoCAD 2027 (default — net10.0-windows)
dotnet build .\Chamber19.AutoCad.Mcp.sln -c Release

# Build for AutoCAD 2025 or 2026 (net8.0-windows)
dotnet build .\Chamber19.AutoCad.Mcp.sln -c Release /p:AutoCadVersion=2026

# Install bundle into %APPDATA%\Autodesk\ApplicationPlugins\ for production-style loading
.\tools\install-bundle.ps1

# Run the test suite (xUnit v3, no AutoCAD required)
dotnet test .\Chamber19.AutoCad.Mcp.sln -c Release

# Dev iteration: launch AutoCAD, then NETLOAD this DLL directly:
#   .\dotnet\Chamber19.AutoCad.Mcp\bin\Release\net10.0-windows\Chamber19.AutoCad.Mcp.dll
```

In AutoCAD, run `MCPSTATUS` to confirm the plugin is loaded.

## How it loads

The bundle ships with `LoadOnAutoCADStartup="false"` and `LoadOnRequest="true"` in `PackageContents.xml`. The plugin loads when:
- An `MCP*` command is invoked, or
- An engineer manually `NETLOAD`s the DLL (dev workflow).

## Repo layout

```
Chamber19.AutoCad.Mcp.sln              Solution
Directory.Build.props                  Cross-platform restore guard
dotnet/
  Directory.Build.props                Shared C# settings
  Chamber19.AutoCad.Mcp/               Main plugin
    Chamber19.AutoCad.Mcp.csproj
    Extension.cs                       IExtensionApplication shell
    Commands.cs                        MCPSTATUS command
bundle/
  Chamber19.AutoCad.Mcp.bundle/        PackageContents.xml + staged DLLs (Contents/Win64/, regenerated by build)
tools/
  install-bundle.ps1                   Stages bundle into %APPDATA%\Autodesk\ApplicationPlugins\
spikes/
  aspnetcore-viability/                Historical Kestrel-in-acad.exe viability probe
```

## Locked design decisions

See `.github/copilot-instructions.md` for the full table. Summary:

- AutoCAD 2025+, 2027 primary
- Dual-target `net8.0-windows;net10.0-windows`, selected by `AutoCadVersion`
- HTTP MCP transport (Kestrel + `ModelContextProtocol.AspNetCore`)
- Auth ON by default, bearer token written to port file
- Per-PID port file at `%LOCALAPPDATA%\Chamber19\autocad-mcp\port.{pid}.txt`; clients glob `port.*.txt` to discover all running instances
- No activation gate (toolkit concern, not per-app)

## License

TBD.
