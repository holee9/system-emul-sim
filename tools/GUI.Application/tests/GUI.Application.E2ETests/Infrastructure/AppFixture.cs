using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using System.Diagnostics;
using Xunit;

namespace XrayDetector.Gui.E2ETests.Infrastructure;

/// <summary>
/// Manages GUI.Application process lifecycle for E2E tests.
/// SPEC-HELP-001: REQ-HELP-051
/// SPEC-E2E-002: REQ-E2E2-001 (E2ELogger), REQ-E2E2-003 (timing instrumentation)
/// SPEC-E2E-004: TAG-001 (attach mode), TAG-005 (no-kill on dispose), TAG-006 (fast PID validation)
/// </summary>
public sealed class AppFixture : IAsyncLifetime, IDisposable
{
    private Process? _appProcess;
    private UIA3Automation? _automation;
    private Application? _flaUiApp;
    private bool _isAttachMode;
    private bool _skipWarmup;

    public AutomationElement? MainWindow { get; private set; }
    public bool IsDesktopAvailable { get; private set; } = true;
    public UIA3Automation Automation => _automation ?? throw new InvalidOperationException("Automation not initialized");

    /// <summary>Structured logger for this E2E session. SPEC-E2E-002: REQ-E2E2-001</summary>
    public E2ELogger Logger { get; } = new E2ELogger();

    /// <summary>
    /// Set to true BEFORE calling InitializeAsync() to skip WPF MenuItem warmup.
    /// Use for lifecycle-only tests that do not need menu accessibility, to prevent
    /// parallel warmup interference with the main [Collection("E2E")] fixture.
    /// xUnit collection fixtures require a parameterless constructor, so this is
    /// exposed as a settable property rather than a constructor parameter.
    /// </summary>
    public bool SkipWarmup { set => _skipWarmup = value; }

    // Path to the built executable
    private static string GetAppExePath()
    {
        var projectDir = FindProjectRoot();
        // Try Release first, then Debug
        var releasePath = Path.Combine(projectDir, "tools", "GUI.Application", "src", "GUI.Application",
            "bin", "Release", "net8.0-windows", "GUI.Application.exe");
        var debugPath = Path.Combine(projectDir, "tools", "GUI.Application", "src", "GUI.Application",
            "bin", "Debug", "net8.0-windows", "GUI.Application.exe");

        if (File.Exists(releasePath)) return releasePath;
        if (File.Exists(debugPath)) return debugPath;
        throw new FileNotFoundException($"GUI.Application.exe not found. Build the project first.\nChecked:\n  {releasePath}\n  {debugPath}");
    }

    private static string FindProjectRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "tools", "GUI.Application")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not find project root");
    }

    public async Task InitializeAsync()
    {
        // SPEC-E2E-004: TAG-001 — Attach mode takes priority; skips desktop check and process launch.
        if (EnvironmentDetector.IsAttachMode())
        {
            await InitializeAttachModeAsync();
            return;
        }

        IsDesktopAvailable = EnvironmentDetector.IsInteractiveDesktop();
        if (!IsDesktopAvailable)
        {
            Trace.WriteLine(
                "[AppFixture] Non-interactive session detected. Skipping WPF process launch. " +
                "Run E2E tests from an interactive desktop session (PowerShell terminal or Visual Studio).");
            Logger.Warn("Non-interactive session. WPF process launch skipped.");
            return;
        }

        var totalSw = Stopwatch.StartNew();
        var exePath = GetAppExePath();
        Logger.Step($"Starting GUI.Application: {exePath}");

        var startInfo = new ProcessStartInfo(exePath)
        {
            UseShellExecute = false,
        };
        // .NET 8 uses Environment dictionary, not EnvironmentVariables
        startInfo.Environment["XRAY_E2E_MODE"] = "true";

        _appProcess = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start GUI.Application");
        Logger.Step($"Process started. PID={_appProcess.Id}");

        _automation = new UIA3Automation();
        _flaUiApp = FlaUI.Core.Application.Attach(_appProcess);

        // Wait for main window (timeout 30 seconds)
        var timeout = TimeSpan.FromSeconds(30);
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            try
            {
                MainWindow = _flaUiApp.GetMainWindow(_automation);
                if (MainWindow != null)
                {
                    Logger.Step($"MainWindow found after {sw.Elapsed.TotalSeconds:F1}s");

                    // Initial settle: allow WPF Dispatcher to process startup events.
                    await Task.Delay(2000);

                    // Deep warmup: expand each menu TWICE to pre-register WPF AutomationPeers.
                    // WPF MenuItem peers register lazily at Background Dispatcher priority (~26s on
                    // this machine on first expansion). After the popup closes, peers are destroyed.
                    // Running two passes here ensures the 3rd expansion (tests) finds items instantly.
                    // Skipped when _skipWarmup=true (lifecycle-only tests must pass skipWarmup=true
                    // to avoid parallel warmup interference with the main E2E fixture).
                    if (!_skipWarmup)
                    {
                        await WarmupSingleMenuAsync("File", "MenuFileExit");
                        await WarmupSingleMenuAsync("Help", "MenuHelpAbout");
                    }

                    Logger.Step($"Menu warmup complete. Total init: {totalSw.Elapsed.TotalSeconds:F1}s");
                    await Task.Delay(500);
                    break;
                }
            }
            catch { }
            await Task.Delay(500);
        }

        if (MainWindow == null)
        {
            Logger.Fail("MainWindow did not appear within 30 seconds.");
            throw new TimeoutException("GUI.Application main window did not appear within 30 seconds");
        }
    }

    /// <summary>
    /// Attaches FlaUI to an existing process specified by XRAY_E2E_ATTACH_PID.
    /// SPEC-E2E-004: TAG-001, TAG-006
    /// REQ-2: Attach instead of launch.
    /// REQ-3: Skip IsInteractiveDesktop() check (app is already visibly running).
    /// REQ-6: Fail fast on invalid PID (no 30-second hang).
    /// </summary>
    private async Task InitializeAttachModeAsync()
    {
        _isAttachMode = true;
        IsDesktopAvailable = true;

        var pidEnv = Environment.GetEnvironmentVariable("XRAY_E2E_ATTACH_PID")!;
        if (!int.TryParse(pidEnv, out var pid))
            throw new InvalidOperationException(
                $"XRAY_E2E_ATTACH_PID='{pidEnv}' is not a valid integer. " +
                "Set it to the PID of a running GUI.Application process.");

        Process process;
        try
        {
            process = Process.GetProcessById(pid);
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException(
                $"XRAY_E2E_ATTACH_PID={pid}: No running process found with that PID. " +
                "Start GUI.Application.exe first, then re-run the tests.");
        }

        Logger.Step($"Attach mode: connecting to PID={pid} ({process.ProcessName})");

        _automation = new UIA3Automation();
        _flaUiApp = FlaUI.Core.Application.Attach(process);

        // Wait for main window (timeout 30 seconds)
        var timeout = TimeSpan.FromSeconds(30);
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            try
            {
                MainWindow = _flaUiApp.GetMainWindow(_automation);
                if (MainWindow != null)
                {
                    Logger.Step($"MainWindow found after {sw.Elapsed.TotalSeconds:F1}s");
                    await Task.Delay(2000);
                    if (!_skipWarmup)
                    {
                        await WarmupSingleMenuAsync("File", "MenuFileExit");
                        await WarmupSingleMenuAsync("Help", "MenuHelpAbout");
                    }
                    Logger.Step("Attach mode init complete.");
                    await Task.Delay(500);
                    break;
                }
            }
            catch { }
            await Task.Delay(500);
        }

        if (MainWindow == null)
        {
            Logger.Fail($"MainWindow not found for PID={pid} within 30s.");
            throw new TimeoutException(
                $"GUI.Application (PID={pid}) main window not found within 30 seconds. " +
                "Ensure the application is fully started and its main window is visible.");
        }
    }

    /// <summary>
    /// Expands the named top-level menu (up to 3 passes) until the specified
    /// AutomationId sub-item appears in the UIAutomation tree.
    ///
    /// WHY THREE PASSES:
    /// WPF MenuItem AutomationPeers register via Dispatcher.BeginInvoke(Background=4).
    /// On warm machines (app previously run): Background runs in &lt;1s — pass 1 succeeds.
    /// On cold start: .NET JIT + WPF DataBind(8)/Render(7)/Loaded(6) work starves
    /// Background(4) for 350-550s from app launch. Passes 1+2 each time out at 90s.
    /// Pass 3 adds a 30s rest to allow Dispatcher to drain, then polls for 120s.
    /// For the second menu (Help), this warmup window spans t=332-662s from app start,
    /// which overlaps the typical cold-start peer registration window (~350-550s).
    ///
    /// Best-effort: exceptions are swallowed; tests fall back to their own retry logic.
    /// </summary>
    private async Task WarmupSingleMenuAsync(string menuName, string targetAutomationId)
    {
        var warmupSw = Stopwatch.StartNew();
        Logger.Step($"Warmup start: {menuName} (target={targetAutomationId})");
        try
        {
            var menu = MainWindow?.FindFirstDescendant(
                cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Menu));
            if (menu == null) return;

            var menuItem = menu.FindFirstChild(cf => cf.ByName(menuName));
            if (menuItem == null) return;

            // Helper: open menu, poll for up to timeoutSec, return true if found.
            // Always collapses the menu before returning.
            async Task<bool> TryPassAsync(int passNumber, double timeoutSec)
            {
                FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE);
                await Task.Delay(200);
                menuItem.AsMenuItem().Click();

                var passSw = Stopwatch.StartNew();
                while (passSw.Elapsed < TimeSpan.FromSeconds(timeoutSec))
                {
                    await Task.Delay(500);
                    var t = menuItem.FindFirstChild(cf => cf.ByAutomationId(targetAutomationId));
                    if (t != null)
                    {
                        _ = t.AutomationId;
                        _ = t.Name;
                        Logger.Step($"Warmup pass {passNumber} done: {menuName} ({warmupSw.Elapsed.TotalSeconds:F1}s)");
                        FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE);
                        await Task.Delay(300);
                        return true;
                    }
                }

                Logger.Warn($"Warmup pass {passNumber} timeout: {menuName} ({targetAutomationId} not found in {timeoutSec}s)");
                FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE);
                await Task.Delay(300);
                return false;
            }

            // ── Pass 1 (90s): warm machines succeed here in < 1s ─────────────────────
            if (await TryPassAsync(1, 90)) return;

            // ── Pass 2 (90s): re-register after first collapse ────────────────────────
            await Task.Delay(500);
            if (await TryPassAsync(2, 90)) return;

            // ── Pass 3 (120s): cold-start extended retry ──────────────────────────────
            // On cold start, WPF Background(4) is starved by JIT + DataBind(8)/Render(7)
            // for 350-550s from app launch. Passes 1+2 cover t=2-182s from app start.
            // A 30s rest here lets the Background queue partially drain, then pass 3
            // polls for 120s (covering t=212-332s for File, t=332-662s for Help).
            // The Help-menu window (t=332-662s) overlaps the typical registration time,
            // so Help warmup succeeds on cold start before the test suite begins.
            Logger.Step($"Warmup pass 3 start: {menuName} — resting 30s to allow Background Dispatcher drain");
            await Task.Delay(30_000);
            await TryPassAsync(3, 120);

            Logger.Step($"Warmup complete: {menuName} total={warmupSw.Elapsed.TotalSeconds:F1}s");
        }
        catch { /* best-effort warmup */ }
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _flaUiApp?.Dispose();
        _automation?.Dispose();

        // SPEC-E2E-004: TAG-005 — In attach mode, the process is externally owned.
        // Do NOT kill or dispose it; only release automation resources.
        if (!_isAttachMode)
        {
            try
            {
                if (_appProcess != null && !_appProcess.HasExited)
                {
                    _appProcess.Kill(entireProcessTree: true);
                    _appProcess.WaitForExit(5000);
                }
            }
            catch (InvalidOperationException) { /* process already exited */ }
            finally
            {
                _appProcess?.Dispose();
            }
        }

        Logger.Dispose();
    }
}
