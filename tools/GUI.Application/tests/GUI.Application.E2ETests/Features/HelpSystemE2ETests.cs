using System.Runtime.InteropServices;
using FlaUI.Core.AutomationElements;
using FluentAssertions;
using Xunit;
using XrayDetector.Gui.E2ETests.Infrastructure;
using XrayDetector.Gui.E2ETests.PageObjects;

namespace XrayDetector.Gui.E2ETests.Features;

// ROOT CAUSE ANALYSIS - Why existing tests did NOT catch the Help wiring bug:
//
// 1. No existing test ever clicked "Help → Topics" or pressed F1.
//    All original HelpSystemE2ETests only verified AutomationIds for View menu items,
//    the StatusBar, and an InputKvp field — none touched HelpWindow at all.
//
// 2. AboutDialogE2ETests verifies MenuHelpAbout (About dialog), but not MenuHelpTopics.
//
// 3. AppFixture.WarmupSingleMenuAsync only waits for "MenuHelpAbout" to register.
//    If MenuHelpTopics was absent from the XAML, warmup would still succeed because
//    it never looks for MenuHelpTopics.
//
// 4. HelpWindowPage only had GetTitle()/Close() — no method to verify the window
//    actually opened or that its topic tree was visible.
//
// CONCLUSION: Zero tests exercised the click-to-open-HelpWindow path. The bug
// (missing MenuItem wiring) was completely invisible to the E2E suite.
//
// FIX: Tests below (HelpTopics_*, F1_*) actually open HelpWindow and verify it.

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

    // -------------------------------------------------------------------------
    // Behavioral tests: these actually OPEN HelpWindow and verify behavior.
    // Without these, missing menu wiring is invisible to the E2E suite.
    // -------------------------------------------------------------------------

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    [Fact]
    public async Task HelpTopicsMenuItem_OpensHelpWindow()
    {
        // Expand Help menu and find MenuHelpTopics (added by coder teammate).
        var menuBar = MainWindow.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Menu));
        menuBar.Should().NotBeNull("MenuBar must exist");
        var helpMenu = menuBar!.FindFirstChild(cf => cf.ByName("Help"));
        helpMenu.Should().NotBeNull("Help top-level menu must exist");

        FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE);
        Thread.Sleep(100);

        helpMenu!.AsMenuItem().Click();

        // WPF registers MenuItem AutomationPeers lazily — retry until visible.
        AutomationElement? topicsItem = null;
        for (int attempt = 0; attempt < 200; attempt++)
        {
            Thread.Sleep(200);
            topicsItem = helpMenu.FindFirstChild(cf => cf.ByAutomationId("MenuHelpTopics"));
            if (topicsItem != null) break;
        }
        topicsItem.Should().NotBeNull("MenuHelpTopics AutomationId must exist (coder wires this)");

        // Click the menu item directly — access key would be '도' (Korean), not ASCII.
        topicsItem!.AsMenuItem().Click();

        // HelpWindow title is "도움말" (set in HelpWindow.xaml Title property).
        await WaitHelper.WaitUntilAsync(() => FindWindow(null, "도움말") != IntPtr.Zero, 12000);

        var hwnd = FindWindow(null, "도움말");
        hwnd.Should().NotBe(IntPtr.Zero, "HelpWindow should open when Help Topics menu item is clicked");

        // Close the window to restore state for subsequent tests.
        var helpWindowElement = Fixture.Automation.FromHandle(hwnd);
        var page = new HelpWindowPage(helpWindowElement);
        page.Close();
        await WaitHelper.DelayAsync(300);
    }

    [Fact]
    public async Task HelpTopicsMenuItem_HasCorrectAutomationId()
    {
        // Structural check: MenuHelpTopics AutomationId must exist in expanded Help menu.
        // This is the minimum that would have caught the missing wiring bug.
        var menuBar = MainWindow.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Menu));
        var helpMenu = menuBar?.FindFirstChild(cf => cf.ByName("Help"));
        helpMenu.Should().NotBeNull("Help menu must exist");

        FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE);
        Thread.Sleep(100);
        helpMenu!.AsMenuItem().Click();

        AutomationElement? topicsItem = null;
        for (int attempt = 0; attempt < 200; attempt++)
        {
            Thread.Sleep(200);
            topicsItem = helpMenu.FindFirstChild(cf => cf.ByAutomationId("MenuHelpTopics"));
            if (topicsItem != null) break;
        }

        FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE);
        Thread.Sleep(100);

        topicsItem.Should().NotBeNull("MenuHelpTopics AutomationId must be set in MainWindow.xaml");
    }

    [Fact]
    public async Task F1Key_OpensHelpWindow()
    {
        // Ensure no menu is open that might consume F1.
        FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE);
        Thread.Sleep(200);

        // Click main window to ensure it has focus before pressing F1.
        MainWindow.SetForeground();
        Thread.Sleep(300);

        FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.F1);

        await WaitHelper.WaitUntilAsync(() => FindWindow(null, "도움말") != IntPtr.Zero, 12000);

        var hwnd = FindWindow(null, "도움말");
        hwnd.Should().NotBe(IntPtr.Zero, "HelpWindow should open when F1 is pressed");

        // Close to restore state.
        var helpWindowElement = Fixture.Automation.FromHandle(hwnd);
        var page = new HelpWindowPage(helpWindowElement);
        page.Close();
        await WaitHelper.DelayAsync(300);
    }

    [Fact]
    public async Task HelpWindow_HasTopicTreeVisible()
    {
        // Open HelpWindow via F1, then verify the topic TreeView is rendered.
        FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE);
        Thread.Sleep(200);
        MainWindow.SetForeground();
        Thread.Sleep(300);

        FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.F1);

        await WaitHelper.WaitUntilAsync(() => FindWindow(null, "도움말") != IntPtr.Zero, 12000);
        var hwnd = FindWindow(null, "도움말");
        hwnd.Should().NotBe(IntPtr.Zero, "HelpWindow must open");

        var helpWindowElement = Fixture.Automation.FromHandle(hwnd);
        var page = new HelpWindowPage(helpWindowElement);

        // Give WPF time to render TreeView items bound to FilteredTopics.
        await WaitHelper.DelayAsync(500);

        page.IsTopicTreeVisible().Should().BeTrue("HelpWindow must show a topic TreeView (bound to FilteredTopics)");

        page.Close();
        await WaitHelper.DelayAsync(300);
    }
}
