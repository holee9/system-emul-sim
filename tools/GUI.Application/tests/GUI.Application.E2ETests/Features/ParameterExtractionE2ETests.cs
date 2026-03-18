using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using XrayDetector.Gui.E2ETests.Infrastructure;

namespace XrayDetector.Gui.E2ETests.Features;

/// <summary>
/// E2E tests for Parameter Extraction feature (PDF datasheet parsing).
/// Updated for SPEC-GUI-002: parameter extraction is now in Panel tab (index 0).
/// </summary>
[Collection("E2E")]
public sealed class ParameterExtractionE2ETests : E2ETestBase
{
    public ParameterExtractionE2ETests(AppFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }

    [RequiresDesktopFact]
    public async Task Tab1_Panel_Should_Exist_And_Have_PdfButtons()
    {
        // Find the TabControl
        var tabControl = MainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tab))
            ?? throw new Exception("TabControl not found");

        // Get all tab items
        var tabItems = tabControl.FindAllChildren(cf => cf.ByControlType(ControlType.TabItem));
        Assert.True(tabItems.Length >= 1, $"Expected at least 1 tab, found {tabItems.Length}");

        // Tab 1 (index 0) should be "Panel" (SPEC-GUI-002 module-oriented layout)
        var panelTab = tabItems[0];
        var tabName = panelTab.Name;
        Assert.Equal("Panel", tabName);

        // Click the tab to switch to it
        panelTab.Click();
        await Task.Delay(500);

        // Verify the Load PDF button exists in Panel view (PDF extraction is embedded in Panel tab)
        var loadPdfButton = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("BtnLoadPdf"))
            ?? throw new Exception("Load PDF button (BtnLoadPdf) not found in Panel tab");

        Assert.Equal("Load PDF Datasheet", loadPdfButton.Name);

        // Verify the Apply to Simulator button exists
        var applyButton = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("BtnApplyToSimulator"))
            ?? throw new Exception("Apply button (BtnApplyToSimulator) not found");

        Assert.Equal("Apply to Simulator", applyButton.Name);

        // Verify the Clear button exists
        var clearButton = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("BtnClearParams"))
            ?? throw new Exception("Clear button (BtnClearParams) not found");

        Assert.Equal("Clear", clearButton.Name);
    }

    [RequiresDesktopFact]
    public async Task Tab1_Panel_Should_Have_Config_Buttons()
    {
        // Navigate to Tab 1 (Panel) — SPEC-GUI-002: config buttons moved to Panel tab
        var tabControl = MainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tab))
            ?? throw new Exception("TabControl not found");

        var tabItems = tabControl.FindAllChildren(cf => cf.ByControlType(ControlType.TabItem));
        Assert.True(tabItems.Length >= 1, $"Expected at least 1 tab, found {tabItems.Length}");

        // Tab 1 (index 0) should be "Panel"
        var panelTab = tabItems[0];
        Assert.Equal("Panel", panelTab.Name);

        // Click the tab
        panelTab.Click();
        await Task.Delay(500);

        // Verify Load Config button exists in Panel tab
        var loadConfigButton = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("BtnLoadConfig"))
            ?? throw new Exception("Load Config button (BtnLoadConfig) not found in Panel tab");

        Assert.Equal("Load Config", loadConfigButton.Name);

        // Verify Save Config button exists in Panel tab
        var saveConfigButton = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("BtnSaveConfig"))
            ?? throw new Exception("Save Config button (BtnSaveConfig) not found in Panel tab");

        Assert.Equal("Save Config", saveConfigButton.Name);
    }
}
