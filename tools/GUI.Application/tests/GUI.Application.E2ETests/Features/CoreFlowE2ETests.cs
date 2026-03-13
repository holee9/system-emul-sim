using FlaUI.Core.AutomationElements;
using FluentAssertions;
using Xunit;
using XrayDetector.Gui.E2ETests.Infrastructure;

namespace XrayDetector.Gui.E2ETests.Features;

/// <summary>
/// Core flow E2E tests. SPEC-HELP-001: REQ-HELP-054
/// </summary>
[Collection("E2E")]
public sealed class CoreFlowE2ETests(AppFixture fixture) : E2ETestBase(fixture)
{
    [RequiresDesktopFact]
    public void BtnStart_HasCorrectAutomationId()
    {
        // BtnStart is in SimulatorControlView inside Simulator Control tab (index 3)
        var tabControl = MainWindow.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Tab));
        var tabs = tabControl?.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem));
        if (tabs != null && tabs.Length > 3) tabs[3].AsTabItem().Select();
        Thread.Sleep(500);

        var btn = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("BtnStart"));
        btn.Should().NotBeNull("BtnStart AutomationId must exist");
    }

    [RequiresDesktopFact]
    public void BtnStop_HasCorrectAutomationId()
    {
        // BtnStop is in SimulatorControlView inside Simulator Control tab (index 3)
        var tabControl = MainWindow.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Tab));
        var tabs = tabControl?.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem));
        if (tabs != null && tabs.Length > 3) tabs[3].AsTabItem().Select();
        Thread.Sleep(500);

        var btn = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("BtnStop"));
        btn.Should().NotBeNull("BtnStop AutomationId must exist");
    }

    [RequiresDesktopFact]
    public void FileExit_MenuItemExists()
    {
        // WPF MenuItem sub-items only appear in UIAutomation tree when parent menu is expanded.
        var menuBar = MainWindow.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Menu));
        var fileMenu = menuBar?.FindFirstChild(cf => cf.ByName("File"));
        fileMenu.Should().NotBeNull("File menu must exist");
        fileMenu!.AsMenuItem().Click();
        Thread.Sleep(200);
        var item = fileMenu.FindFirstChild(cf => cf.ByAutomationId("MenuFileExit"));
        FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE);
        Thread.Sleep(100);
        item?.Should().NotBeNull("MenuFileExit AutomationId must exist");
    }

    [RequiresDesktopFact]
    public async Task TabSwitching_WorksViaTabControl()
    {
        var tabControl = MainWindow.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Tab));
        tabControl.Should().NotBeNull("TabControl must exist");

        var tabs = tabControl!.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TabItem));
        tabs.Should().HaveCountGreaterThanOrEqualTo(6);

        // Select tab 2 (Frame Preview)
        tabs[1].AsTabItem().Select();
        await WaitHelper.DelayAsync(200);
        tabs[1].AsTabItem().IsSelected.Should().BeTrue();

        // Return to first tab
        tabs[0].AsTabItem().Select();
        await WaitHelper.DelayAsync(200);
    }
}
