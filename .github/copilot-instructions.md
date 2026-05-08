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
| Port discovery | Single-instance via `%LOCALAPPDATA%\Chamber19\autocad-mcp\port.txt`. Multi-instance punted. |
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

Commit 1 (this commit): empty NETLOAD shell that proves the build and bundle work. **No MCP code yet.**

Commit 2: wire `ModelContextProtocol` + `ModelContextProtocol.AspNetCore`. Start an HTTP MCP server inside `Initialize()` with one synthetic tool (`chamber19_ping`). Publish port + bearer token to `port.txt`.

Commit 3: port the AutoCAD-thread marshaling pattern from Suite's `SuiteCadPipeHost` into an `AutoCadThreadDispatcher`. Expose one read-only AutoCAD-touching tool (e.g., `getActiveDocument`).

Slice 4+: introduce `fixtures/` with sample DWGs and `expected.json` per fixture as the eval discipline for read-only tools.
