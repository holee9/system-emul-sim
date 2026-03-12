using FlaUI.Core.AutomationElements;

namespace XrayDetector.Gui.E2ETests.PageObjects;

/// <summary>
/// Page Object for HelpWindow. SPEC-HELP-001: REQ-HELP-052
/// </summary>
public sealed class HelpWindowPage(AutomationElement window)
{
    public string GetTitle() => window.Name;

    public void Close()
    {
        var closeBtn = window.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button));
        closeBtn?.AsButton().Invoke();
    }
}
