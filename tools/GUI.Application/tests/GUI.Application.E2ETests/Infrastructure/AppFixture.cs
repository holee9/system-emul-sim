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

    public AutomationElement? MainWindow { get; private set; }
    public bool IsDesktopAvailable { get; private set; } = true;
    public UIA3Automation Automation => _automation ?? throw new InvalidOperationException("Automation not initialized");

    /// <summary>Structured logger for this E2E session. SPEC-E2E-002: REQ-E2E2-001</summary>
    public E2ELogger Logger { get; } = new E2ELogger();

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

                    // Deep warmup: expand each menu and HOLD IT OPEN until its key sub-items
                    // appear in the UIAutomation tree (up to 90s per menu).
                    // WPF registers MenuItem AutomationPeers lazily at Background Dispatcher priority.
                    // On this machine, registration takes ~26s from first expansion.
                    // Critically, the menu MUST remain expanded during the entire wait period;
                    // collapsing and re-expanding resets the registration timer.
                    await WarmupSingleMenuAsync("File", "MenuFileExit");
                    await WarmupSingleMenuAsync("Help", "MenuHelpTopics");

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
                    await WarmupSingleMenuAsync("File", "MenuFileExit");
                    await WarmupSingleMenuAsync("Help", "MenuHelpTopics");
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
    /// Expands the named top-level menu and keeps it open until the specified
    /// AutomationId sub-item appears in the UIAutomation tree, then collapses.
    /// This ensures the peer is registered before any test needs it.
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

            // Ensure any open menu is closed first.
            FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE);
            await Task.Delay(200);

            menuItem.AsMenuItem().Click(); // expand and HOLD open

            // Poll until target sub-item appears. Menu stays expanded the whole time.
            // WPF's Background-priority Dispatcher work creates AutomationPeers while menu is open.
            var warmupTimeout = TimeSpan.FromSeconds(90);
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < warmupTimeout)
            {
                await Task.Delay(500);
                var target = menuItem.FindFirstChild(cf => cf.ByAutomationId(targetAutomationId));
                if (target != null)
                {
                    // Trigger full peer initialization by accessing properties.
                    _ = target.AutomationId;
                    _ = target.Name;
                    Logger.Step($"Warmup done: {menuName} ({warmupSw.Elapsed.TotalSeconds:F1}s)");
                    break;
                }
            }

            if (warmupSw.Elapsed.TotalSeconds >= 90)
                Logger.Warn($"Warmup timeout: {menuName} ({targetAutomationId} not found in 90s)");

            // Collapse menu.
            FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE);
            await Task.Delay(300);
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
