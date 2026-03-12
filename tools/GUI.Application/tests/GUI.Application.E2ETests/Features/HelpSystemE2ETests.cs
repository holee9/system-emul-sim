using FlaUI.Core.AutomationElements;
using FluentAssertions;
using Xunit;
using XrayDetector.Gui.E2ETests.Infrastructure;

namespace XrayDetector.Gui.E2ETests.Features;

/// <summary>
/// E2E tests for Help system. SPEC-HELP-001: REQ-HELP-054
/// </summary>
[Collection("E2E")]
public sealed class HelpSystemE2ETests(AppFixture fixture) : E2ETestBase(fixture)
{
    [Fact]
    public void ViewStatusBar_HasCorrectAutomationId()
    {
        // WPF MenuItem sub-items only appear in UIAutomation tree when parent menu is expanded.
        var menuBar = MainWindow.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Menu));
        var viewMenu = menuBar?.FindFirstChild(cf => cf.ByName("View"));
        viewMenu.Should().NotBeNull("View menu must exist");
        viewMenu!.AsMenuItem().Click();
        Thread.Sleep(200);
        var item = viewMenu.FindFirstChild(cf => cf.ByAutomationId("MenuViewStatusBar"));
        FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE);
        Thread.Sleep(100);
        item.Should().NotBeNull("MenuViewStatusBar AutomationId must exist");
    }

    [Fact]
    public void ViewFullScreen_HasCorrectAutomationId()
    {
        // WPF MenuItem sub-items only appear in UIAutomation tree when parent menu is expanded.
        var menuBar = MainWindow.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Menu));
        var viewMenu = menuBar?.FindFirstChild(cf => cf.ByName("View"));
        viewMenu.Should().NotBeNull("View menu must exist");
        viewMenu!.AsMenuItem().Click();
        Thread.Sleep(200);
        var item = viewMenu.FindFirstChild(cf => cf.ByAutomationId("MenuViewFullScreen"));
        FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE);
        Thread.Sleep(100);
        item.Should().NotBeNull("MenuViewFullScreen AutomationId must exist");
    }

    [Fact]
    public void StatusBarVersion_HasCorrectAutomationId()
    {
        var item = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("StatusBarVersion"));
        item.Should().NotBeNull("StatusBarVersion AutomationId must exist");
    }

    [Fact]
    public void InputKvp_HasCorrectAutomationId()
    {
        // Switch to Simulator Control tab first (Tab index 3)
        var tabControl = MainWindow.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Tab));
        tabControl.Should().NotBeNull("TabControl must exist");

        var tabs = tabControl!.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem));
        tabs.Should().HaveCountGreaterThan(3, "Need at least 4 tabs to reach Simulator Control");
        tabs[3].AsTabItem().Select();
        Thread.Sleep(500); // Wait for tab content to render

        // Retry up to 5 times in case automation tree needs time to update
        FlaUI.Core.AutomationElements.AutomationElement? input = null;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            input = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("InputKvp"));
            if (input != null) break;
            Thread.Sleep(200);
        }
        input.Should().NotBeNull("InputKvp AutomationId must exist on SimulatorControlView");
    }
}
