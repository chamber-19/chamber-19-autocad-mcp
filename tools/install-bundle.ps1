[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")][string]$Configuration = "Release",
    [string]$AutoCadVersion,
    [string]$AutoCadInstallDir,
    [switch]$SkipBuild,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repoRoot "dotnet\Chamber19.AutoCad.Mcp\Chamber19.AutoCad.Mcp.csproj"
$bundleSource = Join-Path $repoRoot "bundle\Chamber19.AutoCad.Mcp.bundle"
$bundleDestRoot = Join-Path $env:APPDATA "Autodesk\ApplicationPlugins"
$bundleDest = Join-Path $bundleDestRoot "Chamber19.AutoCad.Mcp.bundle"

if (-not (Test-Path $projectPath)) {
    throw "Project not found at $projectPath."
}

if (-not $SkipBuild) {
    $buildArgs = @("build", $projectPath, "-c", $Configuration, "-nologo")
    if ($AutoCadVersion)    { $buildArgs += "/p:AutoCadVersion=$AutoCadVersion" }
    if ($AutoCadInstallDir) { $buildArgs += "/p:AutoCadInstallDir=$AutoCadInstallDir" }

    Write-Host "Building Chamber19.AutoCad.Mcp ($Configuration)..." -ForegroundColor Cyan
    & dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE."
    }
}

$stagedDll = Join-Path $bundleSource "Contents\Win64\Chamber19.AutoCad.Mcp.dll"
if (-not (Test-Path $stagedDll)) {
    throw "Bundle is not staged. Expected $stagedDll. Run a build first or omit -SkipBuild."
}

if (Test-Path $bundleDest) {
    if (-not $Force) {
        $autocadRunning = Get-Process -Name "acad" -ErrorAction SilentlyContinue
        if ($autocadRunning) {
            throw "AutoCAD is currently running. Close it before installing, or pass -Force to overwrite anyway (DLLs may be locked)."
        }
    }
    Write-Host "Removing existing bundle at $bundleDest..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $bundleDest
}

if (-not (Test-Path $bundleDestRoot)) {
    New-Item -ItemType Directory -Path $bundleDestRoot -Force | Out-Null
}

Write-Host "Copying $bundleSource → $bundleDest..." -ForegroundColor Cyan
Copy-Item -Recurse -Path $bundleSource -Destination $bundleDestRoot -Force

Write-Host ""
Write-Host "Installed Chamber19.AutoCad.Mcp.bundle to:" -ForegroundColor Green
Write-Host "  $bundleDest"
Write-Host ""
Write-Host "Launch AutoCAD; the bundle is set to LoadOnRequest only, so the plugin loads when an MCP* command is invoked, not at startup." -ForegroundColor Gray
Write-Host "For dev iteration, NETLOAD against the build output directly: dotnet\Chamber19.AutoCad.Mcp\bin\$Configuration\<tfm>\Chamber19.AutoCad.Mcp.dll" -ForegroundColor Gray
