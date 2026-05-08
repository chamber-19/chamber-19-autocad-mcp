<#
.SYNOPSIS
    Lists all Chamber19 AutoCAD MCP server instances running on this host.

.DESCRIPTION
    Globs %LOCALAPPDATA%\Chamber19\autocad-mcp\port.*.txt and reads each port file to
    enumerate MCP server instances. For each instance it reports:

      Pid     : AutoCAD process ID
      Url     : HTTP base URL (e.g. http://127.0.0.1:5001/)
      Token   : Bearer token — masked by default; pass -ShowToken to reveal the full value
      Started : ISO-8601 timestamp recorded when the server started
      IsAlive : Whether the AutoCAD process is still running ($null when -SkipAliveCheck)
      File    : Full path to the port file

    The script emits one [PSCustomObject] per discovered instance so results can be piped
    to Where-Object, Select-Object, ForEach-Object, and so on.

    Stale port files (process no longer running) are included unless -AliveOnly is set.
    A stale file will not prevent the next AutoCAD launch from binding a fresh port; Write
    overwrites atomically with a temp-then-move.

.PARAMETER AliveOnly
    Suppress instances whose AutoCAD process is no longer running.

.PARAMETER ShowToken
    Emit the full bearer token instead of a masked placeholder.
    The token grants full access to the MCP server — handle it accordingly.

.PARAMETER SkipAliveCheck
    Skip the Get-Process liveness lookup. IsAlive will be $null for all results.
    Useful in restricted environments where process enumeration is not permitted.

.EXAMPLE
    # List all instances (alive and stale)
    .\find-mcp.ps1

.EXAMPLE
    # Only live instances, pipe to first
    .\find-mcp.ps1 -AliveOnly | Select-Object -First 1

.EXAMPLE
    # Reveal tokens for scripted use
    $inst = .\find-mcp.ps1 -AliveOnly -ShowToken | Select-Object -First 1
    Invoke-RestMethod -Uri ($inst.Url.TrimEnd('/') + '/mcp') `
        -Method POST `
        -Headers @{ Authorization = "Bearer $($inst.Token)" } `
        -ContentType 'application/json' `
        -Body '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"ps-client","version":"1.0"}}}'

.NOTES
    Port files: %LOCALAPPDATA%\Chamber19\autocad-mcp\port.<pid>.txt
    File shape: { url, token, pid, started }

    Run MCPSTATUS inside AutoCAD to confirm the plugin is loaded and the port file is written.
#>

[CmdletBinding()]
param(
    [switch]$AliveOnly,
    [switch]$ShowToken,
    [switch]$SkipAliveCheck
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ─── Discovery directory ──────────────────────────────────────────────────────

$portDir     = Join-Path $env:LOCALAPPDATA "Chamber19\autocad-mcp"
$portPattern = Join-Path $portDir "port.*.txt"

Write-Verbose "Globbing: $portPattern"
$portFiles = @(Get-Item $portPattern -ErrorAction SilentlyContinue)

if ($portFiles.Count -eq 0) {
    Write-Host "No Chamber19 MCP port files found at:" -ForegroundColor Yellow
    Write-Host "  $portDir" -ForegroundColor Gray
    Write-Host "Is AutoCAD running with the MCP plugin loaded? Run MCPSTATUS inside AutoCAD to confirm." -ForegroundColor Gray
    return
}

Write-Verbose "Found $($portFiles.Count) port file(s)"

# ─── Parse and emit ───────────────────────────────────────────────────────────

foreach ($f in $portFiles) {
    Write-Verbose "Reading $($f.FullName)"

    $data = Get-Content $f.FullName -Raw | ConvertFrom-Json

    $isAlive = $null
    if (-not $SkipAliveCheck) {
        $isAlive = $null -ne (Get-Process -Id $data.pid -ErrorAction SilentlyContinue)
    }

    if ($AliveOnly -and ($isAlive -eq $false)) {
        Write-Verbose "Skipping stale port file (PID $($data.pid) not running): $($f.Name)"
        continue
    }

    $tokenDisplay = if ($ShowToken) {
        $data.token
    } else {
        $data.token.Substring(0, [Math]::Min(8, $data.token.Length)) + '...'
    }

    [PSCustomObject]@{
        Pid     = [int]$data.pid
        Url     = $data.url
        Token   = $tokenDisplay
        Started = $data.started
        IsAlive = $isAlive
        File    = $f.FullName
    }
}
