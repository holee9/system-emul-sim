#!/usr/bin/env pwsh
<#
.SYNOPSIS
    E2E Proof-of-Operation Verification - WPF app auto-click feature validation

.DESCRIPTION
    Launches the GUI.Application and verifies all 6 tabs render without XAML crashes
    using FlaUI UIA3 automation. No real hardware required (XRAY_E2E_MODE=true uses mock services).

.PARAMETER Build
    Build the app before running E2E tests

.PARAMETER Config
    Build configuration (Debug / Release). Default: Debug

.PARAMETER Filter
    Test filter (run specific test class only)
    Example: -Filter "TabRender" or -Filter "CoreFlow"

.PARAMETER ShowVerbose
    Show detailed test output

.EXAMPLE
    # One-step: build + full E2E verification
    .\tools\GUI.Application\scripts\e2e-verify.ps1 -Build

.EXAMPLE
    # Quick run (already built)
    .\tools\GUI.Application\scripts\e2e-verify.ps1

.EXAMPLE
    # Verify only tab rendering (crash detection)
    .\tools\GUI.Application\scripts\e2e-verify.ps1 -Filter "TabRender"
#>

[CmdletBinding()]
param(
    [switch]$Build,

    [ValidateSet("Debug", "Release")]
    [string]$Config = "Debug",

    [string]$Filter = "",

    [switch]$ShowVerbose
)

$ErrorActionPreference = "Stop"
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $ScriptRoot))
$E2EProject = Join-Path $ProjectRoot "tools\GUI.Application\tests\GUI.Application.E2ETests\GUI.Application.E2ETests.csproj"
$AppProject = Join-Path $ProjectRoot "tools\GUI.Application\src\GUI.Application\GUI.Application.csproj"

function Write-Banner {
    Write-Host ""
    Write-Host "╔══════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║    GUI.Application - E2E Proof-of-Operation Verify      ║" -ForegroundColor Cyan
    Write-Host "║    WPF UI auto-click to verify feature implementation   ║" -ForegroundColor Cyan
    Write-Host "╚══════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host ""
}

function Write-Step {
    param([string]$Message)
    Write-Host "  ▶ $Message" -ForegroundColor Yellow
}

function Write-Success {
    param([string]$Message)
    Write-Host "  ✅ $Message" -ForegroundColor Green
}

function Write-Fail {
    param([string]$Message)
    Write-Host "  ❌ $Message" -ForegroundColor Red
}

# Check desktop environment
$isCI = $env:CI -eq "true" -or -not [string]::IsNullOrEmpty($env:GITHUB_ACTIONS)
if ($isCI) {
    Write-Host ""
    Write-Host "  ⚠️  CI environment detected. E2E tests require an interactive desktop." -ForegroundColor Yellow
    Write-Host "     Run from a local dev machine or set XRAY_E2E_FORCE=1 to override." -ForegroundColor Yellow
    Write-Host ""
}

Write-Banner

# Step 1: Build
if ($Build) {
    Write-Step "Building app... ($Config)"
    $buildResult = dotnet build $AppProject -c $Config --nologo -v quiet 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Fail "Build failed"
        Write-Host $buildResult -ForegroundColor Red
        exit 1
    }
    Write-Success "Build complete"
}

# Step 2: Run E2E tests
Write-Step "Starting E2E auto-click verification..."
Write-Host "     - No hardware required (XRAY_E2E_MODE=true uses mock services)" -ForegroundColor DarkGray
Write-Host "     - Screenshots on failure: TestResults/Screenshots/" -ForegroundColor DarkGray
Write-Host ""

$testArgs = @(
    "test", $E2EProject,
    "-c", $Config,
    "--no-build",
    "--logger", "console;verbosity=$(if ($ShowVerbose) { 'detailed' } else { 'minimal' })"
)

if ($Filter -ne "") {
    $testArgs += "--filter"
    $testArgs += "FullyQualifiedName~$Filter"
    Write-Step "Filter: $Filter"
}

$sw = [System.Diagnostics.Stopwatch]::StartNew()
dotnet @testArgs
$exitCode = $LASTEXITCODE
$sw.Stop()

Write-Host ""
Write-Host "══════════════════════════════════════════════════════════════" -ForegroundColor Cyan

if ($exitCode -eq 0) {
    Write-Success "E2E Proof-of-Operation PASSED! ($([math]::Round($sw.Elapsed.TotalSeconds, 1))s)"
    Write-Host ""
    Write-Host "  All tabs render without XAML crash, key UI elements are functional." -ForegroundColor Green
} else {
    Write-Fail "E2E verification FAILED ($([math]::Round($sw.Elapsed.TotalSeconds, 1))s)"
    Write-Host ""
    Write-Host "  Diagnosis:" -ForegroundColor Yellow
    Write-Host "    1. Check failure screenshots: TestResults/Screenshots/" -ForegroundColor Yellow
    Write-Host "    2. Review UIA tree dump in test output" -ForegroundColor Yellow
    Write-Host "    3. Windows Event Log: Get-EventLog -LogName Application -Source '.NET Runtime' -Newest 5" -ForegroundColor Yellow
    Write-Host "    4. Re-run with -ShowVerbose for detailed output" -ForegroundColor Yellow
}

Write-Host ""
exit $exitCode
