<!-- markdownlint-disable MD013 -->
# Copilot Instructions — chamber-19-autocad-mcp

> **Repo:** `chamber-19/chamber-19-autocad-mcp`
> **Scope:** repo-specific guidance for AI agents and Copilot sessions in this repo.

This file overlays the org-wide baseline at `chamber-19/.github/.github/copilot-instructions.md`. Both apply; this file wins on conflict.

---

## What this repo is

A C# AutoCAD .NET plugin that loads via `NETLOAD` (or as a `PackageContents.xml` bundle) and acts as an **MCP server** for third-party clients (Claude Desktop, Claude Code, our own LLM clients, future Foundry agents). Clients connect to it over HTTP and drive AutoCAD through the documented .NET API and COM surfaces.

This is **not** a wrapper around Autodesk's internal `acmcp.dll`. We host our own MCP server inside acad.exe and expose our own tools.

## Locked design decisions

| Decision | Value |
|---|---|
| AutoCAD floor | 2025+ |
| Primary AutoCAD version | 2027 |
| Target framework | dual-target — `net8.0-windows` for AutoCAD 2025/2026, `net10.0-windows` for 2027+ (selected from `AutoCadVersion`) |
| Default `AutoCadVersion` | 2027 |
| Root namespace | `Chamber19.AutoCad.Mcp` |
| MCP transport | HTTP via `ModelContextProtocol` + `ModelContextProtocol.AspNetCore`. Custom-pipe / stdio shim was considered and rejected after a viability spike confirmed Kestrel hosts cleanly inside acad.exe 2027. |
| Auth | ON by default. Bearer token auto-generated at startup, written to the port file alongside the URL. |
| Distribution | `PackageContents.xml` bundle. `LoadOnAutoCADStartup="false"`, `LoadOnRequest="true"` so dev NETLOAD still works. |
| Port discovery | Per-PID file `%LOCALAPPDATA%\Chamber19\autocad-mcp\port.{pid}.txt`. Clients glob `port.*.txt` in the same directory to discover all running instances. |
| `net48` | Dropped. Not supported. |
| Activation gate | Out of scope. Activation is a `desktop-toolkit` concern, not a per-app concern. |

## Repo layout

```
Chamber19.AutoCad.Mcp.sln                          Solution at root
Directory.Build.props                              EnableWindowsTargeting for non-Windows restore
dotnet/
  Directory.Build.props                            Common langver/nullable/treat-warnings-as-errors
  Chamber19.AutoCad.Mcp/                           Main plugin project
bundle/
  Chamber19.AutoCad.Mcp.bundle/                    PackageContents.xml + staged Contents/Win64/
tools/
  install-bundle.ps1                               Copies bundle into %APPDATA%\Autodesk\ApplicationPlugins\
spikes/
  aspnetcore-viability/                            Historical: Kestrel-in-acad.exe viability probe (kept for reference)
```

## Build & run

```powershell
# Build, defaults to AutoCadVersion=2027 (net10.0-windows)
dotnet build .\Chamber19.AutoCad.Mcp.sln -c Release

# Build for AutoCAD 2026
dotnet build .\Chamber19.AutoCad.Mcp.sln -c Release /p:AutoCadVersion=2026

# Install the bundle into %APPDATA%\Autodesk\ApplicationPlugins\
.\tools\install-bundle.ps1

# For dev iteration: NETLOAD the build output directly inside AutoCAD
# .\dotnet\Chamber19.AutoCad.Mcp\bin\Release\net10.0-windows\Chamber19.AutoCad.Mcp.dll
```

In AutoCAD: run `MCPSTATUS` to print plugin/runtime info to the editor.

## Code change discipline (repo-specific overlay)

- **Do not** copy Suite's `dotnet/suite-cad-authoring/SuiteCadAuthoring.csproj` defaults verbatim. The version ladder, default `AutoCadVersion`, and TFM rule here differ from Suite's. Match the locked decisions above.
- **Do not** add an activation gate, PIN check, or Drive-bound license enforcement in this repo. That's `desktop-toolkit`'s job.
- **Do not** call into Autodesk's `acmcp.dll` or otherwise depend on Autodesk's internal MCP implementation. Autodesk does not document or commit to this surface. We host our own.
- **Do not** assume the plugin is the only MCP server on the host. Other AutoCAD plugins or external tools may also bind ports in the 5001–5050 range.

## Slice plan reminder

**Commit 1 (done):** empty NETLOAD shell — build, bundle, install script, IExtensionApplication shell, MCPSTATUS placeholder. No MCP code.

**Commit 2 (done):** MCP server skeleton — `ModelContextProtocol` + `ModelContextProtocol.AspNetCore` wired. Kestrel host binds first free port in 5001–5050, bearer auth (RFC 6750 401), 32-byte token, port file at `%LOCALAPPDATA%\Chamber19\autocad-mcp\port.<pid>.txt` with shape `{url, token, pid, started}`, single synthetic `chamber19_ping` tool returning `{ok, autocadVersion, plugin, ts}`. No AutoCAD interaction yet — `chamber19_ping` reads only cached snapshot data captured during `Initialize()`.

**Commit 3 (done):** AutoCAD-thread marshaling. `AutoCadThreadDispatcher` in `Threading/` (lifted from Suite's `SuiteCadPipeHost.InvokeOnApplicationThread` pattern) captures the application thread id during `Initialize`, attaches `Application.Idle` handler, queues callbacks from background threads (Kestrel) and runs them on the AutoCAD UI thread on the next idle tick. Async API: `Task` / `Task<T> InvokeOnApplicationThreadAsync(...)`. First database-reading tool: `chamber19_get_active_document` returning `{name, path, isModified, modelSpaceEntityCount, ts}`. Empirical finding: AutoCAD 2027 R26.0 was more thread-permissive than expected — the bypassed read path didn't fault, but the dispatcher is retained for forward compat (Suite parity, future write tools, AutoCAD's busy-state behavior). Lifecycle ordering: register idle handler AFTER host starts, deregister BEFORE host stops.

**Commit 4 (done):** test harness. `dotnet/Chamber19.AutoCad.Mcp.Tests/` (xUnit v3 + `xunit.runner.visualstudio` 3.x + `Microsoft.NET.Test.Sdk` 18.x + `Microsoft.AspNetCore.TestHost` 10.x). Targets `net10.0-windows`. Internal types accessed via the existing `[InternalsVisibleTo("Chamber19.AutoCad.Mcp.Tests")]`. Tests cover `BearerAuth` (4 cases: missing, invalid, valid, empty bearer — all assert RFC 6750 `WWW-Authenticate` and JSON body shape), `PortFile.Write/Delete` (atomic temp-then-move, JSON shape, parent-dir creation, overwrite, idempotent delete), and `PortAllocator.FindFreePort` (range membership + skip-bound-port behavior). Required a small refactor: added `internal` overloads of `PortFile.Write`/`PortFile.Delete` taking a target path so tests use a temp dir instead of `%LOCALAPPDATA%`. Run via `dotnet test`.

**Commit 5 (done):** `chamber19_list_layers` — first "must-have" read tool. `Tools/ListLayersTool.cs` iterates `LayerTable` inside a read-only `Transaction` (pattern from `autocad-knowledge/layers.md`), projects each `LayerTableRecord` to internal `LayerInfo` record (`Name, ColorIndex, IsFrozen, IsLocked, IsOff, IsPlottable`). All AutoCAD reads dispatch via `AutoCadThreadDispatcher.InvokeOnApplicationThreadAsync`. Pure `Serialize(IReadOnlyList<LayerInfo>, DateTimeOffset)` is exposed `internal` so tests target it with mocked LayerInfo arrays (the test pattern for tools whose AutoCAD-touching read can't be unit-tested without AutoCAD running). 4 new tests in `ListLayersToolTests`: empty list, field-order preservation, typical 5-layer mixed-flag mapping, Unicode-in-name JSON validity. 14 tests total now (10 from commit 4 + 4 new).

**Commit 6 (done):** `chamber19_list_blocks` — second "must-have" read tool. `Tools/ListBlocksTool.cs` does a two-pass walk inside a read-only `Transaction`: pass 1 collects user block definitions from the BlockTable (skips `IsAnonymous` `*U`/`*X`/`*D` and `IsLayout` BTRs; captures `IsDynamicBlock`); pass 2 walks every layout BTR (model space + paper spaces) and resolves each `BlockReference` via `DynamicBlockTableRecord` so dynamic-block customized variants count toward the original definition rather than their anonymous derivatives. Pattern from `autocad-knowledge/attributes.md` "Finding all instances of a named block". Returns `{blocks: [{name, referenceCount, isDynamic}], ts}`, sorted by name (case-insensitive). 4 new tests in `ListBlocksToolTests` following the same mocked-domain-record pattern. 18 tests total now.

**Commit 7 (done):** `chamber19_list_xrefs` — iterates `Database.XrefBlockTableRecordIds` inside a read-only Transaction, opens each `BlockTableRecord` ForRead, projects to `{name, path, isLoaded, isAttached}`. `isAttached` = `!btr.IsFromOverlayReference`. Sorted by name (case-insensitive). 4 new tests; 22 tests total.

**Commit 8 (done):** `chamber19_list_layouts` — iterates `Database.LayoutDictionaryId` inside a read-only Transaction, opens each `Layout` ForRead, projects to `{name, isCurrent, tabOrder}`. Sorted by `tabOrder`. 4 new tests; 26 tests total.

**Commit 9 (done):** `chamber19_get_block_attributes` — reads attribute values from the first placed instance of a named block. Walks every layout BTR inside a read-only Transaction, resolves via `DynamicBlockTableRecord`, iterates `AttributeCollection`. Returns `{attributes: [{tag, value}], ts}`. Pattern from `autocad-knowledge/attributes.md`. 4 new tests; 30 tests total.

**Commit 10 (done):** per-PID port discovery + harness expansion. `PortFile` now writes `port.{pid}.txt` instead of `port.txt`; `PortFile.GetPath(pid)` returns the canonical path. Added `PortFileTests.GetPath_*` (2), `AutoCadCompatibilityTests` (5), `AutoCadThreadDispatcherTests` (6), `BackpressureTests` (4); 47 tests total.

**Commit 11 (done):** `chamber19_list_dimstyles` — opens `Database.DimStyleTableId` as `DimStyleTable` ForRead, projects each `DimStyleTableRecord` to `{name, lineScale, textHeight}` (`lineScale` = `Dimscale`, `textHeight` = `Dimtxt`). 4 new tests; 51 tests total.

**Commit 12 (done):** `chamber19_count_entities_by_layer` — counts entities on a named layer via `Editor.SelectAll(new SelectionFilter([new TypedValue((int)DxfCode.LayerName, layerName)]))`. Returns `{count, ts}`. Pattern from `autocad-knowledge/selection_sets.md`. 4 new tests; 55 tests total.

**Slice 13+ (next):** more read-only tools — `polyline_length_by_layer`, `closed_polyline_area_by_layer`, `text_enumeration_by_layer`. `fixtures/` with sample DWGs for end-to-end eval tests. Open issues: ALC isolation (#3) before any external distribution, serverInfo polish (#4) when convenient.
