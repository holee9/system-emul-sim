using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using System.Diagnostics;
using Xunit;

namespace XrayDetector.Gui.E2ETests.Infrastructure;

/// <summary>
/// Manages GUI.Application process lifecycle for E2E tests.
/// SPEC-HELP-001: REQ-HELP-051
/// </summary>
public sealed class AppFixture : IAsyncLifetime, IDisposable
{
    private Process? _appProcess;
    private UIA3Automation? _automation;
    private Application? _flaUiApp;

    public AutomationElement? MainWindow { get; private set; }
    public UIA3Automation Automation => _automation ?? throw new InvalidOperationException("Automation not initialized");

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
        var exePath = GetAppExePath();

        var startInfo = new ProcessStartInfo(exePath)
        {
            UseShellExecute = false,
        };
        // .NET 8 uses Environment dictionary, not EnvironmentVariables
        startInfo.Environment["XRAY_E2E_MODE"] = "true";

        _appProcess = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start GUI.Application");

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

                    await Task.Delay(500);
                    break;
                }
            }
            catch { }
            await Task.Delay(500);
        }

        if (MainWindow == null)
            throw new TimeoutException("GUI.Application main window did not appear within 30 seconds");
    }

    /// <summary>
    /// Expands the named top-level menu and keeps it open until the specified
    /// AutomationId sub-item appears in the UIAutomation tree, then collapses.
    /// This ensures the peer is registered before any test needs it.
    /// Best-effort: exceptions are swallowed; tests fall back to their own retry logic.
    /// </summary>
    private async Task WarmupSingleMenuAsync(string menuName, string targetAutomationId)
    {
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
                    break;
                }
            }

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
}
