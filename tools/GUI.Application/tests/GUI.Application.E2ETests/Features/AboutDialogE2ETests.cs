using System.Runtime.InteropServices;
using FlaUI.Core.AutomationElements;
using FluentAssertions;
using Xunit;
using XrayDetector.Gui.E2ETests.Infrastructure;
using XrayDetector.Gui.E2ETests.PageObjects;

namespace XrayDetector.Gui.E2ETests.Features;

/// <summary>
/// E2E tests for About dialog. SPEC-HELP-001: REQ-HELP-054
/// </summary>
[Collection("E2E")]
public sealed class AboutDialogE2ETests(AppFixture fixture) : E2ETestBase(fixture)
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    [Fact]
    public async Task HelpAbout_OpensModalDialog()
    {
        // WPF MenuItem sub-items only appear in UIAutomation tree when the parent menu is expanded.
        var menuBar = MainWindow.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Menu));
        menuBar.Should().NotBeNull("MenuBar must exist");
        var helpMenu = menuBar!.FindFirstChild(cf => cf.ByName("Help"));
        helpMenu.Should().NotBeNull("Help top-level menu must exist");

        // Ensure any open menu is closed first (previous test may have left it open)
        FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE);
        Thread.Sleep(100);

        helpMenu!.AsMenuItem().Click(); // expand

        // Retry: WPF lazily registers MenuItem sub-items with UIAutomation via Dispatcher.
        // On first expansion, peer registration can take up to 40s; allow 200 attempts.
        AutomationElement? aboutMenuItem = null;
        for (int attempt = 0; attempt < 200; attempt++)
        {
            Thread.Sleep(200);
            aboutMenuItem = helpMenu.FindFirstChild(cf => cf.ByAutomationId("MenuHelpAbout"));
            if (aboutMenuItem != null) break;
        }
        aboutMenuItem.Should().NotBeNull("MenuHelpAbout AutomationId should exist");

        // Re-open Help menu and invoke About using access key 'A' (_About in XAML).
        // Keyboard access keys bypass DPI-scaling coordinate issues that affect mouse clicks.
        // WPF menu popup processes the 'A' keypress and fires the bound ShowAboutCommand.
        FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE);
        Thread.Sleep(200);
        helpMenu!.AsMenuItem().Click(); // re-expand Help popup
        Thread.Sleep(500); // allow popup to fully render before key input
        FlaUI.Core.Input.Keyboard.Type('a'); // access key 'A' → invokes _About menu item

        // Wait for About dialog using Win32 FindWindow (bypasses UIAutomation tree hierarchy).
        // WPF ShowInTaskbar=False + Owner set can hide the dialog from UIAutomation desktop children;
        // Win32 FindWindow searches all top-level windows by title regardless of ownership chain.
        await WaitHelper.WaitUntilAsync(() =>
        {
            var hwnd = FindWindow(null, "About X-ray Detector GUI");
            return hwnd != IntPtr.Zero;
        }, 12000);

        var aboutHwnd = FindWindow(null, "About X-ray Detector GUI");
        aboutHwnd.Should().NotBe(IntPtr.Zero, "About dialog should open");

        // Wrap Win32 HWND as AutomationElement for page-object interaction.
        var aboutDialog = Fixture.Automation.FromHandle(aboutHwnd);

        // Close it
        var page = new AboutDialogPage(aboutDialog);
        page.Close();

        await WaitHelper.DelayAsync(300);
    }

    [Fact]
    public void AboutDialog_HasVersionInfo()
    {
        // WPF MenuItem sub-items only appear in UIAutomation tree when parent menu is expanded.
        var menuBar = MainWindow.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Menu));
        var helpMenu = menuBar?.FindFirstChild(cf => cf.ByName("Help"));
        helpMenu.Should().NotBeNull("Help menu must exist");
        FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE); // close any open menu
        Thread.Sleep(100);
        helpMenu!.AsMenuItem().Click();
        AutomationElement? menuHelpAbout = null;
        for (int attempt = 0; attempt < 200; attempt++) { Thread.Sleep(200); menuHelpAbout = helpMenu.FindFirstChild(cf => cf.ByAutomationId("MenuHelpAbout")); if (menuHelpAbout != null) break; }
        FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE);
        Thread.Sleep(100);
        menuHelpAbout.Should().NotBeNull();
    }

    [Fact]
    public void MenuHelpAbout_HasCorrectAutomationId()
    {
        // WPF MenuItem sub-items only appear in UIAutomation tree when parent menu is expanded.
        var menuBar = MainWindow.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Menu));
        var helpMenu = menuBar?.FindFirstChild(cf => cf.ByName("Help"));
        helpMenu.Should().NotBeNull("Help menu must exist");
        FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE); // close any open menu
        Thread.Sleep(100);
        helpMenu!.AsMenuItem().Click();
        AutomationElement? menuItem = null;
        for (int attempt = 0; attempt < 200; attempt++) { Thread.Sleep(200); menuItem = helpMenu.FindFirstChild(cf => cf.ByAutomationId("MenuHelpAbout")); if (menuItem != null) break; }
        FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE);
        Thread.Sleep(100);
        menuItem.Should().NotBeNull("MenuHelpAbout AutomationId must be set");
    }
}
