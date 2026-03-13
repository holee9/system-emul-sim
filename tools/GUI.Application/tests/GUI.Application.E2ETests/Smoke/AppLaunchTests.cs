using FlaUI.Core.AutomationElements;
using FluentAssertions;
using Xunit;
using XrayDetector.Gui.E2ETests.Infrastructure;
using XrayDetector.Gui.E2ETests.PageObjects;

namespace XrayDetector.Gui.E2ETests.Smoke;

/// <summary>
/// Smoke tests: app launch and basic UI. SPEC-HELP-001: REQ-HELP-054
/// </summary>
[Collection("E2E")]
public sealed class AppLaunchTests(AppFixture fixture) : E2ETestBase(fixture)
{
    [RequiresDesktopFact]
    public void App_Launches_AndMainWindowIsVisible()
    {
        MainWindow.Should().NotBeNull();
        MainWindow.Name.Should().Contain("X-ray");
    }

    [RequiresDesktopFact]
    public void StatusBar_DisplaysDynamicVersion_NotHardcoded()
    {
        var page = new MainWindowPage(MainWindow, Fixture);
        var version = page.GetStatusBarVersion();
        version.Should().NotBeEmpty();
        version.Should().NotBe("v1.0.0", "version should be dynamic from assembly");
    }

    [RequiresDesktopFact]
    public async Task AllSixTabs_AreAccessible()
    {
        await WaitHelper.DelayAsync(500);
        var tabControl = MainWindow.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Tab));
        tabControl.Should().NotBeNull();
        var tabs = tabControl!.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem));
        tabs.Length.Should().BeGreaterThanOrEqualTo(6, "all 6 tabs should be accessible");
    }

    [RequiresDesktopFact]
    public void FileExit_ClosesApplication()
    {
        // WPF MenuItem sub-items only appear in UIAutomation tree when the parent menu is expanded.
        var menuBar = MainWindow.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Menu));
        menuBar.Should().NotBeNull("MenuBar must exist");

        var fileMenu = menuBar!.FindFirstChild(cf => cf.ByName("File"));
        fileMenu.Should().NotBeNull("File top-level menu must exist");

        fileMenu!.AsMenuItem().Click(); // expand

        // Retry: WPF lazily registers MenuItem sub-items with UIAutomation via Dispatcher.
        // On first expansion, peer registration can take up to 40s on some machines; allow 200 attempts.
        AutomationElement? menuFileExit = null;
        for (int attempt = 0; attempt < 200; attempt++)
        {
            Thread.Sleep(200);
            menuFileExit = fileMenu.FindFirstChild(cf => cf.ByAutomationId("MenuFileExit"));
            if (menuFileExit != null) break;
        }

        // Collapse without executing
        FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE);
        Thread.Sleep(100);

        menuFileExit.Should().NotBeNull("File > Exit menu item should exist with AutomationId");
    }
}
