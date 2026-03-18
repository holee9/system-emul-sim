using FlaUI.Core.AutomationElements;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using XrayDetector.Gui.E2ETests.Infrastructure;

namespace XrayDetector.Gui.E2ETests.Features;

/// <summary>
/// Tab-by-tab render crash validation tests.
/// Adopted from hnvue-console's systematic per-view crash detection pattern.
///
/// Purpose: Navigate to each tab and verify the WPF app renders without XAML exceptions.
/// XAML binding errors (TwoWay on read-only, missing Path in DataTemplate, etc.) are
/// only detectable at runtime -- compiler and unit tests cannot catch them.
///
/// Each test:
///   1. Navigates to the target tab by AutomationId
///   2. Verifies the tab activated without crash (window still alive + key element found)
///   3. Records success to suppress auto-screenshot
///
/// Derived from hnvue-console lessons:
///   - Tab header must be searched as ControlType.TabItem (not ControlType.Text)
///   - Mock services (XRAY_E2E_MODE=true) required to avoid gRPC connection timeouts
///   - 1s settle time after tab switch allows async ViewModel initialization
/// </summary>
[Collection("E2E")]
public sealed class TabRenderE2ETests(AppFixture fixture, ITestOutputHelper output) : E2ETestBase(fixture, output)
{
    private const int TabSwitchDelayMs = 1000;

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "TabRender")]
    public async Task Tab_Panel_RendersWithoutCrash()
    {
        var tab = await WaitHelper.WaitForElementAsync(
            MainWindow,
            () => MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("TabPanel")),
            timeoutMs: 5000,
            description: "TabPanel");
        tab.Should().NotBeNull("TabPanel must exist in UIAutomation tree");

        tab!.AsTabItem().Select();
        await WaitHelper.DelayAsync(TabSwitchDelayMs);

        var inputRows = await WaitHelper.WaitForElementAsync(
            MainWindow,
            () => MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("InputRows")),
            timeoutMs: 5000,
            logger: Logger,
            description: "InputRows (Panel tab key element)");
        inputRows.Should().NotBeNull("Panel tab must render PanelEmulatorView without XAML crash");

        RecordTestPassed();
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "TabRender")]
    public async Task Tab_Fpga_RendersWithoutCrash()
    {
        var tab = await WaitHelper.WaitForElementAsync(
            MainWindow,
            () => MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("TabFpga")),
            timeoutMs: 5000,
            description: "TabFpga");
        tab.Should().NotBeNull("TabFpga must exist in UIAutomation tree");

        tab!.AsTabItem().Select();
        await WaitHelper.DelayAsync(TabSwitchDelayMs);

        var inputCsi2DataRate = await WaitHelper.WaitForElementAsync(
            MainWindow,
            () => MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("InputCsi2DataRate")),
            timeoutMs: 5000,
            logger: Logger,
            description: "InputCsi2DataRate (FPGA tab key element)");
        inputCsi2DataRate.Should().NotBeNull("FPGA tab must render FpgaEmulatorView without XAML crash");

        RecordTestPassed();
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "TabRender")]
    public async Task Tab_Soc_RendersWithoutCrash()
    {
        var tab = await WaitHelper.WaitForElementAsync(
            MainWindow,
            () => MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("TabSoc")),
            timeoutMs: 5000,
            description: "TabSoc");
        tab.Should().NotBeNull("TabSoc must exist in UIAutomation tree");

        tab!.AsTabItem().Select();
        await WaitHelper.DelayAsync(TabSwitchDelayMs);

        var inputFrameBufferCount = await WaitHelper.WaitForElementAsync(
            MainWindow,
            () => MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("InputFrameBufferCount")),
            timeoutMs: 5000,
            logger: Logger,
            description: "InputFrameBufferCount (SoC tab key element)");
        inputFrameBufferCount.Should().NotBeNull("SoC tab must render SocEmulatorView without XAML crash");

        RecordTestPassed();
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "TabRender")]
    public async Task Tab_Ethernet_RendersWithoutCrash()
    {
        var tab = await WaitHelper.WaitForElementAsync(
            MainWindow,
            () => MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("TabEthernet")),
            timeoutMs: 5000,
            description: "TabEthernet");
        tab.Should().NotBeNull("TabEthernet must exist in UIAutomation tree");

        tab!.AsTabItem().Select();
        await WaitHelper.DelayAsync(TabSwitchDelayMs);

        var txtEthConnectionState = await WaitHelper.WaitForElementAsync(
            MainWindow,
            () => MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("TxtEthConnectionState")),
            timeoutMs: 5000,
            logger: Logger,
            description: "TxtEthConnectionState (Ethernet tab key element)");
        txtEthConnectionState.Should().NotBeNull("Ethernet tab must render EthernetView without XAML crash");

        RecordTestPassed();
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "TabRender")]
    public async Task Tab_HostPc_RendersWithoutCrash()
    {
        var tab = await WaitHelper.WaitForElementAsync(
            MainWindow,
            () => MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("TabSdkHost")),
            timeoutMs: 5000,
            description: "TabSdkHost");
        tab.Should().NotBeNull("TabSdkHost must exist in UIAutomation tree");

        tab!.AsTabItem().Select();
        await WaitHelper.DelayAsync(TabSwitchDelayMs);

        var txtSdkConnectionState = await WaitHelper.WaitForElementAsync(
            MainWindow,
            () => MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("TxtSdkConnectionState")),
            timeoutMs: 5000,
            logger: Logger,
            description: "TxtSdkConnectionState (Host PC tab key element)");
        txtSdkConnectionState.Should().NotBeNull("Host PC tab must render SdkHostView without XAML crash");

        RecordTestPassed();
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "TabRender")]
    public async Task Tab_Console_RendersWithoutCrash()
    {
        var tab = await WaitHelper.WaitForElementAsync(
            MainWindow,
            () => MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("TabConsole")),
            timeoutMs: 5000,
            description: "TabConsole");
        tab.Should().NotBeNull("TabConsole must exist in UIAutomation tree");

        tab!.AsTabItem().Select();
        await WaitHelper.DelayAsync(TabSwitchDelayMs);

        var btnStart = await WaitHelper.WaitForElementAsync(
            MainWindow,
            () => MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("BtnStart")),
            timeoutMs: 5000,
            logger: Logger,
            description: "BtnStart (Console tab key element)");
        btnStart.Should().NotBeNull("Console tab must render ConsoleView without XAML crash");

        RecordTestPassed();
    }

    [RequiresDesktopFact]
    [Trait("Category", "E2E")]
    [Trait("UserJourney", "TabRender")]
    public async Task AllTabs_CycleWithoutCrash()
    {
        // Full cycle: navigate through all 6 tabs in sequence.
        // Validates that tab state is independent and no accumulated XAML errors occur.
        var tabIds = new[]
        {
            ("TabPanel", "InputRows"),
            ("TabFpga", "InputCsi2DataRate"),
            ("TabSoc", "InputFrameBufferCount"),
            ("TabEthernet", "TxtEthConnectionState"),
            ("TabSdkHost", "TxtSdkConnectionState"),
            ("TabConsole", "BtnStart"),
        };

        foreach (var (tabId, keyElementId) in tabIds)
        {
            var tab = await WaitHelper.WaitForElementAsync(
                MainWindow,
                () => MainWindow.FindFirstDescendant(cf => cf.ByAutomationId(tabId)),
                timeoutMs: 5000,
                description: tabId);
            tab.Should().NotBeNull($"{tabId} must be findable");

            tab!.AsTabItem().Select();
            await WaitHelper.DelayAsync(800);

            var keyElement = await WaitHelper.WaitForElementAsync(
                MainWindow,
                () => MainWindow.FindFirstDescendant(cf => cf.ByAutomationId(keyElementId)),
                timeoutMs: 5000,
                logger: Logger,
                description: $"{keyElementId} in {tabId}");
            keyElement.Should().NotBeNull($"{tabId} must render {keyElementId} without XAML crash");
        }

        RecordTestPassed();
    }
}
