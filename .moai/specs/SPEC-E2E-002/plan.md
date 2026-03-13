# SPEC-E2E-002 Implementation Plan

## Overview

4개의 새 파일 + 3개 기존 파일 수정. 빌드 경고 없이 구현.

## Phase 1: E2ELogger + TreeDumper (신규 파일)

### 1-A. E2ELogger.cs

**파일**: `tools/GUI.Application/tests/GUI.Application.E2ETests/Infrastructure/E2ELogger.cs`

```csharp
using System.Diagnostics;
using Xunit.Abstractions;

namespace XrayDetector.Gui.E2ETests.Infrastructure;

/// <summary>
/// Structured logger for E2E test sessions.
/// Writes timestamped entries to file and supports flush to xUnit ITestOutputHelper.
/// SPEC-E2E-002: REQ-E2E2-001
/// </summary>
public sealed class E2ELogger : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly StringBuilder _buffer = new();
    private readonly Stopwatch _sessionTimer = Stopwatch.StartNew();
    private bool _disposed;

    public E2ELogger()
    {
        var dir = Path.Combine("TestResults", "Logs");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"e2e_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        _writer = new StreamWriter(path, append: false) { AutoFlush = true };
        Info($"=== E2E Session Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
    }

    public void Info(string message) => Write("INFO", message);
    public void Step(string message) => Write("STEP", message);
    public void Warn(string message) => Write("WARN", message);
    public void Fail(string message) => Write("FAIL", message);

    private void Write(string level, string message)
    {
        var entry = $"[{DateTime.Now:HH:mm:ss.fff}] [{level,-4}] {message}";
        _buffer.AppendLine(entry);
        _writer.WriteLine(entry);
        Trace.WriteLine(entry);
    }

    /// <summary>Flushes accumulated log to xUnit test output.</summary>
    public void FlushTo(ITestOutputHelper output)
    {
        output.WriteLine(_buffer.ToString());
        _buffer.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Info($"=== E2E Session Ended. Total: {_sessionTimer.Elapsed.TotalSeconds:F1}s ===");
        _writer.Dispose();
    }
}
```

### 1-B. TreeDumper.cs

**파일**: `tools/GUI.Application/tests/GUI.Application.E2ETests/Infrastructure/TreeDumper.cs`

```csharp
using FlaUI.Core.AutomationElements;
using System.Text;

namespace XrayDetector.Gui.E2ETests.Infrastructure;

/// <summary>
/// Dumps UIAutomation element tree for debugging.
/// SPEC-E2E-002: REQ-E2E2-004
/// </summary>
public static class TreeDumper
{
    public static string Dump(AutomationElement? root, int maxDepth = 4)
    {
        if (root == null) return "(null root element)";
        var sb = new StringBuilder();
        sb.AppendLine("=== UIAutomation Tree Dump ===");
        DumpElement(root, sb, 0, maxDepth);
        return sb.ToString();
    }

    private static void DumpElement(AutomationElement el, StringBuilder sb, int depth, int maxDepth)
    {
        if (depth > maxDepth) return;
        var indent = new string(' ', depth * 2);
        try
        {
            var id = el.AutomationId ?? "";
            var name = el.Name ?? "";
            var type = el.ControlType.ToString();
            sb.AppendLine($"{indent}[{type}] id='{id}' name='{name}'");

            var children = el.FindAllChildren();
            foreach (var child in children)
                DumpElement(child, sb, depth + 1, maxDepth);
        }
        catch
        {
            sb.AppendLine($"{indent}(error reading element)");
        }
    }
}
```

## Phase 2: AppFixture 타이밍 계측

**파일**: `tools/GUI.Application/tests/GUI.Application.E2ETests/Infrastructure/AppFixture.cs`

변경점:
1. `E2ELogger Logger { get; private set; }` 프로퍼티 추가
2. `InitializeAsync()`: E2ELogger 생성, 단계별 타이밍 로깅
3. `WarmupSingleMenuAsync()`: 시작/완료 시간 로깅
4. `Dispose()`: logger.Dispose()

주요 추가 코드:
```csharp
public E2ELogger Logger { get; } = new E2ELogger();

// InitializeAsync() 내:
Logger.Step($"Process started PID={_appProcess.Id}");
Logger.Step($"MainWindow found after {sw.Elapsed.TotalSeconds:F1}s");
Logger.Step($"Starting warmup: {menuName}");
Logger.Step($"Warmup done: {menuName} ({sw.Elapsed.TotalSeconds:F1}s)");
Logger.Info($"Total init: {totalSw.Elapsed.TotalSeconds:F1}s");
```

## Phase 3: WaitHelper WaitForElementAsync 추가

**파일**: `tools/GUI.Application/tests/GUI.Application.E2ETests/Infrastructure/WaitHelper.cs`

새 메서드 추가 (기존 WaitUntilAsync 유지):
```csharp
public static async Task<AutomationElement?> WaitForElementAsync(
    AutomationElement root,
    Func<AutomationElement?> finder,
    int timeoutMs = 5000,
    int pollIntervalMs = 100,
    E2ELogger? logger = null)
{
    var sw = Stopwatch.StartNew();
    while (sw.ElapsedMilliseconds < timeoutMs)
    {
        var el = finder();
        if (el != null) return el;
        await Task.Delay(pollIntervalMs);
    }
    // Timeout: dump tree
    logger?.Fail($"WaitForElement timed out after {timeoutMs}ms. Tree dump:\n{TreeDumper.Dump(root)}");
    return null;
}
```

## Phase 4: E2ETestBase RunWithScreenshot

**파일**: `tools/GUI.Application/tests/GUI.Application.E2ETests/Infrastructure/E2ETestBase.cs`

추가:
```csharp
using Xunit.Abstractions;

// E2ETestBase에 추가:
protected void RunWithScreenshot(string testName, Action test)
{
    try { test(); }
    catch (Exception)
    {
        ScreenshotHelper.CaptureOnFailure(
            testName,
            Fixture.IsDesktopAvailable ? Fixture.MainWindow : null);
        throw;
    }
}

protected async Task RunWithScreenshotAsync(string testName, Func<Task> test)
{
    try { await test(); }
    catch (Exception)
    {
        ScreenshotHelper.CaptureOnFailure(
            testName,
            Fixture.IsDesktopAvailable ? Fixture.MainWindow : null);
        throw;
    }
}
```

## Phase 5: PowerShell Run Script

**파일**: `tools/GUI.Application/tests/GUI.Application.E2ETests/Run-E2ETests.ps1`

```powershell
#Requires -Version 5.1
<#
.SYNOPSIS
    Run E2E tests in interactive desktop session with log capture.
    SPEC-E2E-002: REQ-E2E2-005
.DESCRIPTION
    1. Ensures CI env var is unset (interactive mode)
    2. Builds GUI.Application (Debug)
    3. Runs dotnet test with detailed logging
    4. Reports results and log location
#>

[CmdletBinding()]
param(
    [string]$Filter = "",        # --filter expression
    [switch]$NoBuild             # Skip build step
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path "$PSScriptRoot\..\..\..\..\.."
$testProject = "$PSScriptRoot\GUI.Application.E2ETests.csproj"
$appProject = "$repoRoot\tools\GUI.Application\src\GUI.Application\GUI.Application.csproj"

Write-Host "=== E2E Test Runner ===" -ForegroundColor Cyan
Write-Host "Repo: $repoRoot"

# 1. Remove CI env vars to enable interactive mode
Remove-Item Env:CI -ErrorAction SilentlyContinue
Remove-Item Env:GITHUB_ACTIONS -ErrorAction SilentlyContinue
Write-Host "[OK] CI env vars cleared" -ForegroundColor Green

# 2. Build GUI.Application
if (-not $NoBuild) {
    Write-Host "`nBuilding GUI.Application..." -ForegroundColor Yellow
    & dotnet build $appProject -c Debug --no-restore 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }
    Write-Host "[OK] Build succeeded" -ForegroundColor Green
}

# 3. Run E2E tests
$logDir = "$PSScriptRoot\TestResults\Logs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

$filterArg = if ($Filter) { "--filter `"$Filter`"" } else { "" }
$logFile = "$logDir\run_{0}.log" -f (Get-Date -Format "yyyyMMdd_HHmmss")

Write-Host "`nRunning E2E tests..." -ForegroundColor Yellow
Write-Host "Log: $logFile`n"

$cmd = "dotnet test `"$testProject`" --logger `"console;verbosity=detailed`" $filterArg"
Write-Host $cmd -ForegroundColor DarkGray
Invoke-Expression $cmd 2>&1 | Tee-Object -FilePath $logFile

# 4. Report
Write-Host "`n=== Results ===" -ForegroundColor Cyan
Write-Host "Logs:        $logDir"
Write-Host "Screenshots: $PSScriptRoot\TestResults\Screenshots"
```

## Verification Steps

```powershell
# 1. 빌드 경고 확인
dotnet build tools/GUI.Application/tests/GUI.Application.E2ETests/ 2>&1 | Select-String -Pattern "warning"

# 2. CI 모드 skip 확인 (E2E-001 회귀 방지)
$env:CI="true"
dotnet test tools/GUI.Application/tests/GUI.Application.E2ETests/ --verbosity normal
# 기대: 21 tests Skipped, 0 Failed

# 3. 대화형 모드 - E2ELogger 파일 생성 확인
Remove-Item Env:CI -ErrorAction SilentlyContinue
dotnet test tools/GUI.Application/tests/GUI.Application.E2ETests/ --verbosity normal
# 기대: TestResults/Logs/e2e_*.log 파일 생성

# 4. Run script 동작 확인
pwsh tools/GUI.Application/tests/GUI.Application.E2ETests/Run-E2ETests.ps1 -NoBuild
```

## File Change Summary

| 파일 | 변경 유형 | 영향 |
|------|-----------|------|
| Infrastructure/E2ELogger.cs | NEW | 구조화 로거 |
| Infrastructure/TreeDumper.cs | NEW | UIAutomation 트리 덤프 |
| Run-E2ETests.ps1 | NEW | 대화형 실행 스크립트 |
| Infrastructure/AppFixture.cs | MODIFY | Logger 통합, 타이밍 계측 |
| Infrastructure/WaitHelper.cs | MODIFY | WaitForElementAsync 추가 |
| Infrastructure/E2ETestBase.cs | MODIFY | RunWithScreenshot 추가 |
