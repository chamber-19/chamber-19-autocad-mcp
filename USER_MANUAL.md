# Chamber19 AutoCAD MCP User Manual

This guide explains how to build, install, run, and call the Chamber19 AutoCAD MCP plugin from an MCP client or PowerShell.

## What this plugin does

The plugin runs inside AutoCAD and exposes drawing-inspection tools over HTTP MCP.

- Transport: HTTP endpoint at /mcp
- Auth: bearer token required
- Discovery: per-process port file at %LOCALAPPDATA%\Chamber19\autocad-mcp\port.{pid}.txt

## Prerequisites

- Windows x64
- AutoCAD 2025, 2026, or 2027
- .NET SDK:
  - AutoCAD 2027 builds against net10.0-windows
  - AutoCAD 2025 and 2026 builds against net8.0-windows

## Build

From repo root:

```powershell
dotnet build .\Chamber19.AutoCad.Mcp.sln -c Release
```

Optional explicit AutoCAD version:

```powershell
# AutoCAD 2026 target
dotnet build .\Chamber19.AutoCad.Mcp.sln -c Release /p:AutoCadVersion=2026
```

## Install bundle

```powershell
.\tools\install-bundle.ps1
```

This stages the bundle to:

- %APPDATA%\Autodesk\ApplicationPlugins\Chamber19.AutoCad.Mcp.bundle

## Load and verify in AutoCAD

1. Start AutoCAD.
2. Load the plugin by either:
   - Running MCPSTATUS (bundle LoadOnRequest), or
   - NETLOAD the shell DLL from the build output.
3. Run MCPSTATUS.

Expected MCPSTATUS output includes:

- running state
- bound URL
- port-file path
- log path

## Discover running MCP servers

Use the included discovery script:

```powershell
.\tools\find-mcp.ps1
```

Common options:

```powershell
# only live AutoCAD processes
.\tools\find-mcp.ps1 -AliveOnly

# show full bearer token
.\tools\find-mcp.ps1 -AliveOnly -ShowToken
```

## Run smoke test

Use the included end-to-end smoke test:

```powershell
.\tools\smoke-test.ps1
```

Target a specific AutoCAD process:

```powershell
.\tools\smoke-test.ps1 -PidFilter 12345
```

## Calling MCP manually (PowerShell)

### 1. Read URL and token from port file

```powershell
$inst = .\tools\find-mcp.ps1 -AliveOnly -ShowToken | Select-Object -First 1
$uri = $inst.Url.TrimEnd('/') + '/mcp'
$token = $inst.Token
```

### 2. Initialize MCP session

```powershell
$initBody = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"ps-client","version":"1.0"}}}'

Invoke-RestMethod -Uri $uri -Method POST -Headers @{
  Authorization = "Bearer $token"
  Accept = "application/json, text/event-stream"
} -ContentType 'application/json' -Body $initBody
```

### 3. List tools

```powershell
$listBody = '{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}'

Invoke-RestMethod -Uri $uri -Method POST -Headers @{
  Authorization = "Bearer $token"
  Accept = "application/json, text/event-stream"
} -ContentType 'application/json' -Body $listBody
```

### 4. Call a tool example

```powershell
$callBody = '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"chamber19_list_layers","arguments":{}}}'

Invoke-RestMethod -Uri $uri -Method POST -Headers @{
  Authorization = "Bearer $token"
  Accept = "application/json, text/event-stream"
} -ContentType 'application/json' -Body $callBody
```

## Available MCP tools

| Tool name | Purpose |
| --- | --- |
| chamber19_ping | Health and runtime snapshot |
| chamber19_get_active_document | Active drawing metadata and model-space entity count |
| chamber19_list_layers | List drawing layers and flags |
| chamber19_list_blocks | List user block definitions and reference counts |
| chamber19_list_xrefs | List xrefs with load and attach/overlay state |
| chamber19_list_layouts | List layout tabs |
| chamber19_list_dimstyles | List dimension styles |
| chamber19_count_entities_by_layer | Count entities on a layer in current space |
| chamber19_polyline_length_by_layer | Sum polyline lengths on a layer in current space |
| chamber19_closed_polyline_area_by_layer | Sum closed-polyline area with Math.Abs(area) |
| chamber19_get_block_attributes | Attributes from first matching block instance |
| chamber19_enumerate_block_attributes | Attributes from all matching block instances |
| chamber19_enumerate_attribute_values_by_tag | All attribute values by tag across drawing |
| chamber19_find_blocks_by_layout | All block references in a specific layout |
| chamber19_get_block_by_handle | Resolve handle to block, position, attributes |
| chamber19_text_enumeration_by_layer | Enumerate DBText and MText on layer |

## Semantics and behavior notes

- Most selection-based tools are current-space only.
- Layer and tag matching are case-insensitive.
- chamber19_closed_polyline_area_by_layer uses Math.Abs on each polyline area.
- chamber19_get_block_by_handle returns found:false for malformed, missing, or non-block handles.

## Troubleshooting

### No port file appears

- Confirm plugin loaded in AutoCAD with MCPSTATUS.
- Check log path shown by MCPSTATUS.
- Verify bundle install path under %APPDATA%\Autodesk\ApplicationPlugins.

### 401 unauthorized from /mcp

- Use bearer token from the selected port.{pid}.txt file.
- Ensure Authorization header uses Bearer TOKEN.

### Build cannot find AutoCAD assemblies

Set environment variable to your AutoCAD install directory:

```powershell
$env:AUTOCAD_INSTALL_DIR = 'C:\Program Files\Autodesk\AutoCAD 2027'
```

Then rebuild.
