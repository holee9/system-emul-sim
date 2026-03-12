using FlaUI.Core.AutomationElements;

namespace XrayDetector.Gui.E2ETests.PageObjects;

/// <summary>
/// Page Object for SimulatorControlView. SPEC-HELP-001: REQ-HELP-052
/// </summary>
public sealed class SimulatorControlPage(AutomationElement window)
{
    public void SetKvp(string value)
    {
        var input = window.FindFirstDescendant(cf => cf.ByAutomationId("InputKvp"));
        if (input != null)
        {
            var tb = input.AsTextBox();
            tb.Text = value;
        }
    }

    public string GetToolTipText(string automationId)
    {
        // ToolTip text retrieval via UIA is complex - return empty string as placeholder
        return string.Empty;
    }

    public void StartSimulation()
    {
        var btn = window.FindFirstDescendant(cf => cf.ByAutomationId("BtnStart"));
        btn?.AsButton().Invoke();
    }

    public void StopSimulation()
    {
        var btn = window.FindFirstDescendant(cf => cf.ByAutomationId("BtnStop"));
        btn?.AsButton().Invoke();
    }
}
