#Requires -Version 5.1
<#
.SYNOPSIS
    Run E2E tests in interactive desktop session with log capture.
    SPEC-E2E-002: REQ-E2E2-005
    SPEC-E2E-004: TAG-003 (-AttachPid parameter for AI-driven E2E loop)

.DESCRIPTION
    1. Ensures CI env vars are unset (interactive mode)
    2. Builds GUI.Application (Debug) — skipped in attach mode
    3. Runs dotnet test with detailed logging
    4. Reports results and output locations

.PARAMETER Filter
    xUnit --filter expression (e.g. "FullyQualifiedName~AppLaunchTests")

.PARAMETER NoBuild
    Skip the build step (use when already built)

.PARAMETER Force
    Force interactive mode via XRAY_E2E_FORCE=1

.PARAMETER AttachPid
    PID of an already-running GUI.Application process to attach to.
    When set, skips the build step and sets XRAY_E2E_ATTACH_PID environment variable.
    Use this for AI-driven E2E test loops where GUI.Application is started manually.

.EXAMPLE
    # Run all E2E tests (launch mode)
    .\Run-E2ETests.ps1

    # Run only AppLaunchTests, skip build
    .\Run-E2ETests.ps1 -Filter "FullyQualifiedName~AppLaunchTests" -NoBuild

    # Attach to existing GUI.Application (AI-driven loop, SPEC-E2E-004)
    .\Run-E2ETests.ps1 -AttachPid 12345
#>

[CmdletBinding()]
param(
    [string]$Filter = "",
    [switch]$NoBuild,
    [switch]$Force,
    [int]$AttachPid = 0
)

$ErrorActionPreference = "Stop"

$scriptDir   = $PSScriptRoot
$repoRoot    = Resolve-Path (Join-Path $scriptDir "..\..\..\..\..")
$testProject = Join-Path $scriptDir "GUI.Application.E2ETests.csproj"
$appProject  = Join-Path $repoRoot "tools\GUI.Application\src\GUI.Application\GUI.Application.csproj"

Write-Host ""
Write-Host "=== E2E Test Runner ===" -ForegroundColor Cyan
Write-Host "Repo:    $repoRoot"
Write-Host "Project: $testProject"
Write-Host ""

# SPEC-E2E-004: TAG-003 — Attach mode: validate PID, set env var, skip build
if ($AttachPid -ne 0) {
    Write-Host "[Attach Mode] Validating PID=$AttachPid ..." -ForegroundColor Cyan
    $attachProcess = Get-Process -Id $AttachPid -ErrorAction SilentlyContinue
    if (-not $attachProcess) {
        Write-Host "[FAIL] No process found with PID=$AttachPid. Start GUI.Application.exe first." -ForegroundColor Red
        exit 1
    }
    $env:XRAY_E2E_ATTACH_PID = $AttachPid.ToString()
    Write-Host "[OK] Attaching to existing GUI.Application (PID=$AttachPid, Name=$($attachProcess.ProcessName))" -ForegroundColor Green
    $NoBuild = $true  # Skip build — app is already running
    Write-Host "[OK] Build step skipped (attach mode)" -ForegroundColor Green
    Write-Host ""
}

# 0. TAG-006: Detect non-interactive environment early and warn
$sessionName = $Env:SESSIONNAME
$msystem     = $Env:MSYSTEM
$isNonInteractive = $false

if ($msystem) {
    $isNonInteractive = $true
    Write-Warning "Non-interactive environment detected: MSYSTEM=$msystem (Git Bash / MSYS2)."
    Write-Warning "FlaUI UIAutomation requires a real Windows desktop session."
} elseif (-not $sessionName) {
    if (-not [Environment]::UserInteractive) {
        $isNonInteractive = $true
        Write-Warning "Non-interactive environment detected: SESSIONNAME not set, UserInteractive=False."
    }
} elseif ($sessionName -notmatch '^(Console|RDP-Tcp)') {
    $isNonInteractive = $true
    Write-Warning "Potentially non-interactive environment: SESSIONNAME=$sessionName."
}

if ($isNonInteractive -and -not $Force) {
    Write-Warning "Use -Force to run E2E tests anyway (tests will likely skip via [RequiresDesktopFact])."
    Write-Host ""
}

if ($Force) {
    $Env:XRAY_E2E_FORCE = "1"
    Write-Host "[OK] -Force flag set: XRAY_E2E_FORCE=1" -ForegroundColor Yellow
}

# 1. Remove CI env vars to enable interactive mode
$removed = @()
foreach ($var in @("CI", "GITHUB_ACTIONS", "TF_BUILD")) {
    if (Test-Path "Env:$var") {
        Remove-Item "Env:$var" -ErrorAction SilentlyContinue
        $removed += $var
    }
}
if ($removed.Count -gt 0) {
    Write-Host "[OK] Removed CI env vars: $($removed -join ', ')" -ForegroundColor Green
} else {
    Write-Host "[OK] No CI env vars to remove" -ForegroundColor Green
}

# Verify interactive mode
if (-not [Environment]::UserInteractive) {
    Write-Warning "Session is NOT interactive. FlaUI UIAutomation may not work."
    Write-Warning "Run this script from PowerShell ISE, Windows Terminal, or VS Code terminal."
}

# 2. Build GUI.Application
if (-not $NoBuild) {
    Write-Host ""
    Write-Host "Building GUI.Application (Debug)..." -ForegroundColor Yellow
    & dotnet build "$appProject" -c Debug --no-restore
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[FAIL] Build failed." -ForegroundColor Red
        exit 1
    }
    Write-Host "[OK] Build succeeded." -ForegroundColor Green
}

# 3. Prepare log directory
$logDir = Join-Path $scriptDir "TestResults\Logs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$logFile = Join-Path $logDir "run_$timestamp.log"

# 4. Run E2E tests
Write-Host ""
Write-Host "Running E2E tests..." -ForegroundColor Yellow

$testArgs = @(
    "test", "`"$testProject`"",
    "--logger", "console;verbosity=detailed",
    "--no-restore"
)
if ($Filter) {
    $testArgs += "--filter"
    $testArgs += $Filter
}

$cmd = "dotnet " + ($testArgs -join " ")
Write-Host $cmd -ForegroundColor DarkGray
Write-Host ""

& dotnet @testArgs 2>&1 | Tee-Object -FilePath $logFile
$exitCode = $LASTEXITCODE

# 5. Report
Write-Host ""
Write-Host "=== Run Complete ===" -ForegroundColor Cyan
Write-Host "Exit Code:   $exitCode"
Write-Host "Log:         $logFile"
Write-Host "Screenshots: $(Join-Path $scriptDir 'TestResults\Screenshots')"
Write-Host ""

if ($exitCode -eq 0) {
    Write-Host "[PASS] All tests passed." -ForegroundColor Green
} else {
    Write-Host "[FAIL] Some tests failed. Check log above." -ForegroundColor Red
}

exit $exitCode
