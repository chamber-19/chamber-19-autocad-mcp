# chamber-19-autocad-mcp

A C# AutoCAD .NET plugin that loads inside `acad.exe` and acts as an MCP server. Third-party MCP clients (Claude Desktop, Claude Code, custom LLM clients, Foundry agents) connect to it over HTTP and drive AutoCAD through the documented .NET API and COM surfaces.

This is **not** a wrapper around Autodesk's internal `acmcp.dll`. We host our own MCP server inside acad.exe and expose our own tools.

## Coexistence with Autodesk's MCP bundle

This plugin loads the `ModelContextProtocol` SDK in its own private `AssemblyLoadContext` (`chamber19-mcp-host`), isolating it from Autodesk's `AutoCAD-MCP-Server-2027.bundle` which pins a different version of the same SDK in the default ALC. Both plugins can be installed simultaneously without version conflicts.

`Chamber19.AutoCad.Mcp.Host.dll` and all MCP SDK dependencies live under `Contents/Win64/private/` inside the bundle. The shell (`Chamber19.AutoCad.Mcp.dll`) has zero `ModelContextProtocol.*` references and loads cleanly into the default ALC.

## Tool semantics

Important behavioral contracts that clients must understand:

### `chamber19_polyline_length_by_layer` — current-space only

`Editor.SelectAll` operates on the **current space** only.

| TILEMODE value | "Current space" |
|---|---|
| `1` (default, tiled viewports) | Model space |
| `0` (paper space active) | Active paper space tab |

Polylines on the queried layer that live in a *different* space are **not** included in the
returned totals. To measure polylines across all spaces, activate each space in turn and call
the tool again.

Both open and closed polylines are included. `totalLength` reports the full perimeter for closed
polylines (the closing segment is part of `Length`). Includes both `LWPOLYLINE` and `POLYLINE`
entity types (the latter covers legacy 2-D and 3-D polylines). `totalLength` is in drawing units.

### `chamber19_count_entities_by_layer` — current-space only

`Editor.SelectAll` operates on the **current space** only.

| TILEMODE value | "Current space" |
|---|---|
| `1` (default, tiled viewports) | Model space |
| `0` (paper space active) | Active paper space tab |

Entities on the queried layer that live in a *different* space (e.g. model-space entities when
a paper-space tab is active, or vice versa) are **not** counted. `count=0` can therefore mean
either "no entities on that layer in this drawing" or "no entities in the current space on that
layer." To count across all spaces, activate each space in turn and call the tool again.

### `chamber19_get_block_attributes` — first matching instance only

The tool walks layout BTRs in block-table iteration order (model space is visited first, then
paper-space BTRs) and returns the attributes of the **first** `BlockReference` whose effective
definition name matches `blockName`. All subsequent instances of the same block are ignored.

"First" is therefore determined by block-table walk order, **not** by insertion order,
creation date, or any other user-visible property. If attribute values differ between
instances, clients have no way to request a specific instance through this tool.

### `chamber19_enumerate_block_attributes` — all matching instances

Complements `chamber19_get_block_attributes` by returning attributes for **every** placed
instance of the named block. Each instance is represented as `{handle, attributes}` where
`handle` is the AutoCAD entity handle (hex string, matching DXF group code 5 and the
AutoLISP `(handent)` function). Instances are returned in block-table walk order (model
space BTRs first, then paper-space BTRs). Returns an empty `instances` array when no
drawing is open, the block is not found, or no instances exist.

## Status

**Commit 15 — `chamber19_polyline_length_by_layer`.** Sums total polyline length for a named layer in the active drawing. Uses a `SelectionFilter` combining `DxfCode.LayerName` with an OR-group for `LWPOLYLINE` and `POLYLINE` entity types applied via `Editor.SelectAll`. Matched entities are opened ForRead inside a read-only transaction; the `.Length` property is summed via defensive switch-cast over `Polyline`, `Polyline2d`, and `Polyline3d` (unexpected types skipped). Searches the current space (model space when `TILEMODE=1`, active paper space otherwise); layer name matching is case-insensitive. Both open and closed polylines are included; `totalLength` is the full perimeter for closed polylines. Returns `{totalLength, polylineCount, ts}` where both are 0 when no drawing is open, the layer does not exist, or no polylines reside on it. Pattern documented in the new `autocad-knowledge/polylines.md`. 4 new tests in `PolylineLengthByLayerToolTests`; 63 tests total.

**Commit 14 — ALC isolation.** Splits `Chamber19.AutoCad.Mcp.dll` (shell) from a new `Chamber19.AutoCad.Mcp.Host.dll` (host), loaded in a custom `AssemblyLoadContext` named `chamber19-mcp-host`. The shell has zero `ModelContextProtocol.*` references and stays in the default ALC. The host carries all MCP SDK code, Kestrel bootstrap, bearer auth, backpressure middleware, and all tools; its private deps live in `Contents/Win64/private/`. The shell's `McpHostBootstrap` owns port allocation, token generation, and the port file lifecycle; it calls the host via a reflection-only contract (`McpHostEntry.StartHost`/`StopHost`) whose parameters are all primitive/shared-runtime types. A `Func<Func<object?>, Task<object?>>` lambda bridges `AutoCadThreadDispatcher` across the ALC boundary without sharing any SDK types. 59 tests continue to pass.

**Commit 13 — `chamber19_enumerate_block_attributes`.** Complements the existing `chamber19_get_block_attributes` by returning attributes for **every** placed instance of a named block. Walks all layout BTRs (model space + paper spaces) inside a read-only Transaction, resolves each `BlockReference` via `DynamicBlockTableRecord` (case-insensitive), and collects the `AttributeCollection` from every matching instance. Each instance is identified by its AutoCAD entity handle (hex string). Returns `{instances: [{handle, attributes: [{tag, value}]}], ts}`. Returns an empty `instances` array when no drawing is open, the block is not found, or no instances have attributes. Pattern from `autocad-knowledge/attributes.md` "Finding all instances of a named block". 4 new tests in `EnumerateBlockAttributesToolTests`; 59 tests total.

**Commit 12 — `chamber19_count_entities_by_layer`.** Counts all entities on a named layer in the active drawing using `Editor.SelectAll(SelectionFilter)` with a single `DxfCode.LayerName` TypedValue. Searches the current space (model space when `TILEMODE=1`, active paper space otherwise); layer name matching is case-insensitive in AutoCAD's selection engine. Returns `{count, ts}` where `count=0` when no drawing is open, the layer does not exist, or no entities reside on it. Pattern from `autocad-knowledge/selection_sets.md`. 4 new tests in `CountEntitiesByLayerToolTests`; 55 tests total.

**Commit 11 — `chamber19_list_dimstyles`.** Lists all dimension styles in the active drawing. Opens `Database.DimStyleTableId` as a `DimStyleTable` ForRead inside a read-only Transaction, projects each `DimStyleTableRecord` to `{name, lineScale, textHeight}` where `lineScale` is `Dimscale` (overall scale factor) and `textHeight` is `Dimtxt` (primary-units text height). Results are sorted by name (case-insensitive). Returns `{dimStyles: [{name, lineScale, textHeight}], ts}`. Pattern documented in `autocad-knowledge/dimstyles.md`. 4 new tests in `ListDimStylesToolTests`; 51 tests total.

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
  Chamber19.AutoCad.Mcp/               Shell plugin (default ALC, zero MCP SDK refs)
    Chamber19.AutoCad.Mcp.csproj
    Extension.cs                       IExtensionApplication shell
    Commands.cs                        MCPSTATUS command
    Alc/McpHostLoadContext.cs          Custom AssemblyLoadContext for host isolation
    Hosting/McpHostBootstrap.cs        Port allocation, token, port file, host launch
  Chamber19.AutoCad.Mcp.Host/          Host (private ALC, owns all MCP SDK code)
    Chamber19.AutoCad.Mcp.Host.csproj
    McpHostEntry.cs                    Public reflection entry point (StartHost/StopHost)
    Hosting/McpServerHost.cs           Kestrel bootstrap, BearerAuth, Backpressure
    Threading/HostDispatcher.cs        Cross-ALC dispatcher bridge
    Tools/                             All MCP tool implementations
  Chamber19.AutoCad.Mcp.Tests/         xUnit v3 test harness (63 tests)
bundle/
  Chamber19.AutoCad.Mcp.bundle/        PackageContents.xml + staged DLLs
    Contents/Win64/                    Shell DLL staged here
    Contents/Win64/private/            Host DLL + MCP SDK deps staged here
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
