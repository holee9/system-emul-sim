using FlaUI.Core.AutomationElements;

namespace XrayDetector.Gui.E2ETests.PageObjects;

/// <summary>
/// Page Object for AboutWindow dialog. SPEC-HELP-001: REQ-HELP-052
/// </summary>
public sealed class AboutDialogPage(AutomationElement dialog)
{
    public string GetVersion()
    {
        var el = dialog.FindFirstDescendant(cf => cf.ByAutomationId("AboutVersionText"));
        return el?.Name ?? string.Empty;
    }

    public void ClickCopyToClipboard()
    {
        var btn = dialog.FindFirstDescendant(cf => cf.ByAutomationId("BtnCopyClipboard"));
        btn?.AsButton().Invoke();
    }

    public void Close()
    {
        var closeBtn = dialog.FindFirstDescendant(cf => cf.ByName("닫기"));
        closeBtn?.AsButton().Invoke();
    }
}
