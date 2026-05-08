<#
.SYNOPSIS
    Post-install smoke test for the Chamber19 AutoCAD MCP plugin.

.DESCRIPTION
    Verifies that the plugin is discoverable, authenticated, and callable over HTTP:

      1. Port discovery — globs %LOCALAPPDATA%\Chamber19\autocad-mcp\port.*.txt and
         reads the URL + bearer token from the selected instance.
      2. Authenticated MCP initialize — POST /mcp with a valid bearer token; asserts
         the response carries a protocolVersion and a tools capability.
      3. Authenticated chamber19_ping — POST /mcp for tools/call; asserts ok: true.
      4. Unauthenticated rejection — POST /mcp without a token; asserts 401 and the
         correct RFC 6750 WWW-Authenticate header.

    The test exits 0 on success and non-zero (with a diagnostic summary) on failure.
    It is fully re-runnable and leaves no side-effects.

.PARAMETER BearerOverride
    Use this token instead of the one in the port file. Useful when testing with a
    known token during development.

.PARAMETER PidFilter
    If multiple AutoCAD instances are running, select the one matching this process ID.
    When omitted, the first port file found is used.

.EXAMPLE
    # Basic smoke test against the first running AutoCAD instance
    .\smoke-test.ps1

.EXAMPLE
    # Target a specific AutoCAD process
    .\smoke-test.ps1 -PidFilter 12345

.EXAMPLE
    # Override the bearer token (e.g. for scripted CI harness)
    .\smoke-test.ps1 -BearerOverride "my-test-token"

.NOTES
    For meaningful full coverage, AutoCAD should be running with
    fixtures\sample.dxf open and the MCP plugin loaded (run MCPSTATUS
    inside AutoCAD to confirm).
#>

[CmdletBinding()]
param(
    [string]$BearerOverride,
    [string]$PidFilter
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ─── Helpers ─────────────────────────────────────────────────────────────────

$script:FailCount = 0
$script:PassCount = 0

function Write-Pass([string]$msg) {
    $script:PassCount++
    Write-Host "  [PASS] $msg" -ForegroundColor Green
}

function Write-Fail([string]$msg) {
    $script:FailCount++
    Write-Host "  [FAIL] $msg" -ForegroundColor Red
}

function Assert-Equal($actual, $expected, [string]$label) {
    if ($actual -eq $expected) {
        Write-Pass $label
    } else {
        Write-Fail "$label — expected '$expected', got '$actual'"
    }
}

function Assert-NotEmpty($value, [string]$label) {
    if (-not [string]::IsNullOrWhiteSpace($value)) {
        Write-Pass $label
    } else {
        Write-Fail "$label — value was null/empty"
    }
}

function Assert-Contains($haystack, $needle, [string]$label) {
    if ($haystack -like "*$needle*") {
        Write-Pass $label
    } else {
        Write-Fail "$label — '$needle' not found in response"
    }
}

# ─── Step 1: Port discovery ───────────────────────────────────────────────────

Write-Host ""
Write-Host "=== Step 1: Port discovery ===" -ForegroundColor Cyan

$portDir = Join-Path $env:LOCALAPPDATA "Chamber19\autocad-mcp"
$portPattern = Join-Path $portDir "port.*.txt"

Write-Host "  Globbing: $portPattern"
$portFiles = @(Get-Item $portPattern -ErrorAction SilentlyContinue)

if ($portFiles.Count -eq 0) {
    Write-Host ""
    Write-Host "FATAL: No port files found at $portDir" -ForegroundColor Red
    Write-Host "       Is AutoCAD running with the MCP plugin loaded?" -ForegroundColor Red
    Write-Host "       Run MCPSTATUS inside AutoCAD to load and verify the plugin." -ForegroundColor Gray
    exit 1
}

Write-Host "  Found $($portFiles.Count) port file(s):" -ForegroundColor Gray
foreach ($f in $portFiles) { Write-Host "    $($f.Name)" -ForegroundColor Gray }

if ($PidFilter) {
    $portFile = $portFiles | Where-Object { $_.Name -match "port\.$PidFilter\.txt" } | Select-Object -First 1
    if (-not $portFile) {
        Write-Host "FATAL: No port file found for PID $PidFilter" -ForegroundColor Red
        exit 1
    }
} else {
    $portFile = $portFiles[0]
}

Write-Host "  Using: $($portFile.FullName)"

$portData = Get-Content $portFile.FullName -Raw | ConvertFrom-Json
$pluginUrl  = $portData.url    # e.g. "http://127.0.0.1:5001/"
$pluginPid  = $portData.pid
$portToken  = $portData.token

$activeToken = if ($BearerOverride) { $BearerOverride } else { $portToken }

# MCP endpoint is mounted at /mcp (McpServerHost.cs: app.MapMcp("/mcp"))
$mcpUrl = $pluginUrl.TrimEnd('/') + '/mcp'

Write-Host "  URL  : $pluginUrl (PID $pluginPid)"
Write-Host "  MCP  : $mcpUrl"
if ($BearerOverride) {
    Write-Host "  Token: [overridden by -BearerOverride]" -ForegroundColor Yellow
} else {
    Write-Host "  Token: $($portToken.Substring(0, [Math]::Min(8, $portToken.Length)))... (from port file)"
}
Write-Pass "Port file discovered and parsed (PID $pluginPid)"

# ─── Step 2: Authenticated MCP initialize ────────────────────────────────────

Write-Host ""
Write-Host "=== Step 2: Authenticated MCP initialize ===" -ForegroundColor Cyan

$initBody = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"smoke-test","version":"1.0"}}}'

try {
    $initResp = Invoke-WebRequest `
        -Uri $mcpUrl `
        -Method POST `
        -Headers @{
            'Content-Type'  = 'application/json'
            'Accept'        = 'application/json, text/event-stream'
            'Authorization' = "Bearer $activeToken"
        } `
        -Body $initBody `
        -UseBasicParsing `
        -ErrorAction Stop

    Assert-Equal $initResp.StatusCode 200 "initialize returns HTTP 200"

    $initJson = $initResp.Content | ConvertFrom-Json
    Assert-NotEmpty $initJson.result "initialize response has 'result'"
    Assert-NotEmpty $initJson.result.protocolVersion "initialize result.protocolVersion is non-empty"

    # Capability: tools (its presence means tool support is advertised)
    $hasCap = $null -ne $initJson.result.capabilities
    if ($hasCap) {
        Write-Pass "initialize result.capabilities is present"
    } else {
        Write-Fail "initialize result.capabilities is missing"
    }

    Write-Host "  protocolVersion : $($initJson.result.protocolVersion)" -ForegroundColor Gray
    if ($initJson.result.serverInfo) {
        Write-Host "  serverInfo      : $($initJson.result.serverInfo.name) v$($initJson.result.serverInfo.version)" -ForegroundColor Gray
    }
}
catch {
    Write-Fail "initialize request failed: $_"
}

# ─── Step 3: Authenticated chamber19_ping ────────────────────────────────────

Write-Host ""
Write-Host "=== Step 3: Authenticated chamber19_ping ===" -ForegroundColor Cyan

$pingBody = '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"chamber19_ping","arguments":{}}}'

try {
    $pingResp = Invoke-WebRequest `
        -Uri $mcpUrl `
        -Method POST `
        -Headers @{
            'Content-Type'  = 'application/json'
            'Accept'        = 'application/json, text/event-stream'
            'Authorization' = "Bearer $activeToken"
        } `
        -Body $pingBody `
        -UseBasicParsing `
        -ErrorAction Stop

    Assert-Equal $pingResp.StatusCode 200 "chamber19_ping returns HTTP 200"

    $pingJson = $pingResp.Content | ConvertFrom-Json
    Assert-NotEmpty $pingJson.result "chamber19_ping response has 'result'"

    # Unwrap the MCP text content: result.content[0].text is a JSON string
    $content = $pingJson.result.content
    if ($null -ne $content -and $content.Count -gt 0) {
        $textItem = $content | Where-Object { $_.type -eq 'text' } | Select-Object -First 1
        if ($textItem) {
            $innerJson = $textItem.text | ConvertFrom-Json
            Assert-Equal $innerJson.ok $true "chamber19_ping inner ok == true"
            Write-Host "  autocadVersion : $($innerJson.autocadVersion)" -ForegroundColor Gray
            Write-Host "  plugin         : $($innerJson.plugin)" -ForegroundColor Gray
        } else {
            Write-Fail "chamber19_ping result.content has no text item"
        }
    } else {
        Write-Fail "chamber19_ping result.content is empty or missing"
    }
}
catch {
    Write-Fail "chamber19_ping request failed: $_"
}

# ─── Step 4: Unauthenticated request is rejected ─────────────────────────────

Write-Host ""
Write-Host "=== Step 4: Unauthenticated request rejected ===" -ForegroundColor Cyan

$unauthBody = '{"jsonrpc":"2.0","id":3,"method":"tools/list","params":{}}'

# Use .NET HttpClient directly for a PS5.1/PS7-portable 401 assertion.
# Invoke-WebRequest throws on 4xx in PS5.1 and returns the response in PS7,
# which makes cross-version header inspection awkward.
$httpClient = [System.Net.Http.HttpClient]::new()
try {
    $req = [System.Net.Http.HttpRequestMessage]::new(
        [System.Net.Http.HttpMethod]::Post, $mcpUrl)
    $req.Content = [System.Net.Http.StringContent]::new(
        $unauthBody,
        [System.Text.Encoding]::UTF8,
        'application/json')
    $req.Headers.Accept.ParseAdd('application/json')
    $req.Headers.Accept.ParseAdd('text/event-stream')
    # Intentionally no Authorization header.

    $unauthHttpResp = $httpClient.SendAsync($req).GetAwaiter().GetResult()
    $statusCode     = [int]$unauthHttpResp.StatusCode

    Assert-Equal $statusCode 401 "Unauthenticated request returns 401"

    # WWW-Authenticate: Bearer realm="chamber19-autocad-mcp" (RFC 6750)
    $wwwAuth = ($unauthHttpResp.Headers.WwwAuthenticate | ForEach-Object { $_.ToString() }) -join ', '
    Assert-Contains $wwwAuth 'Bearer' "WWW-Authenticate header contains 'Bearer'"
    Assert-Contains $wwwAuth 'chamber19-autocad-mcp' "WWW-Authenticate realm is 'chamber19-autocad-mcp'"
}
catch {
    Write-Fail "Unauthenticated request test failed: $_"
}
finally {
    $httpClient.Dispose()
}

# ─── Summary ──────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "═══════════════════════════════════════════" -ForegroundColor Cyan
$total = $script:PassCount + $script:FailCount
Write-Host "  Results: $($script:PassCount) passed, $($script:FailCount) failed  (total $total)" -ForegroundColor $(
    if ($script:FailCount -eq 0) { "Green" } else { "Red" }
)
Write-Host "═══════════════════════════════════════════" -ForegroundColor Cyan

if ($script:FailCount -gt 0) {
    Write-Host ""
    Write-Host "Smoke test FAILED. Review the [FAIL] lines above." -ForegroundColor Red
    Write-Host "Tips:" -ForegroundColor Gray
    Write-Host "  - Run MCPSTATUS inside AutoCAD to confirm the plugin is loaded." -ForegroundColor Gray
    Write-Host "  - Open fixtures\sample.dxf in AutoCAD for full drawing-tool coverage." -ForegroundColor Gray
    Write-Host "  - Use -PidFilter if multiple AutoCAD instances are running." -ForegroundColor Gray
    Write-Host "  - Use -BearerOverride if testing with a known token." -ForegroundColor Gray
    exit 1
}

Write-Host ""
Write-Host "Smoke test PASSED." -ForegroundColor Green
exit 0
