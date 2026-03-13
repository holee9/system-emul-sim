using System;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FluentAssertions;
using Xunit;
using XrayDetector.Gui.E2ETests.Infrastructure;

namespace XrayDetector.Gui.E2ETests.Features;

/// <summary>
/// E2E tests for Parameter Extraction feature (PDF datasheet parsing).
/// Verifies integration of ParameterExtractor.Core into GUI.Application.
/// </summary>
[Collection("E2E")]
public sealed class ParameterExtractionE2ETests : E2ETestBase
{
    public ParameterExtractionE2ETests(AppFixture fixture) : base(fixture)
    {
    }

    [RequiresDesktopFact]
    public async Task Tab3_ParameterExtraction_Should_Exist_And_Be_Clickable()
    {
        // Find the TabControl
        var tabControl = MainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tab))
            ?? throw new Exception("TabControl not found");

        // Get all tab items
        var tabItems = tabControl.FindAllChildren(cf => cf.ByControlType(ControlType.TabItem));
        Assert.True(tabItems.Length >= 3, $"Expected at least 3 tabs, found {tabItems.Length}");

        // Tab 3 should be "Parameter Extraction"
        var paramExtractTab = tabItems[2];
        var tabName = paramExtractTab.Name;
        Assert.Equal("Parameter Extraction", tabName);

        // Click the tab to switch to it
        paramExtractTab.Click();
        await Task.Delay(500);

        // Verify the Load PDF button exists in Parameter Extraction view
        var loadPdfButton = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("BtnLoadPdf"))
            ?? throw new Exception("Load PDF button (BtnLoadPdf) not found");

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
    public async Task Tab4_SimulatorControl_Should_Have_Config_Buttons()
    {
        // Navigate to Tab 4 (Simulator Control)
        var tabControl = MainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tab))
            ?? throw new Exception("TabControl not found");

        var tabItems = tabControl.FindAllChildren(cf => cf.ByControlType(ControlType.TabItem));
        Assert.True(tabItems.Length >= 4, $"Expected at least 4 tabs, found {tabItems.Length}");

        // Tab 4 should be "Simulator Control"
        var simulatorTab = tabItems[3];
        Assert.Equal("Simulator Control", simulatorTab.Name);

        // Click the tab
        simulatorTab.Click();
        await Task.Delay(500);

        // Verify Load Config button exists
        var loadConfigButton = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("BtnLoadConfig"))
            ?? throw new Exception("Load Config button (BtnLoadConfig) not found");

        Assert.Equal("Load Config", loadConfigButton.Name);

        // Verify Save Config button exists
        var saveConfigButton = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("BtnSaveConfig"))
            ?? throw new Exception("Save Config button (BtnSaveConfig) not found");

        Assert.Equal("Save Config", saveConfigButton.Name);
    }
}
