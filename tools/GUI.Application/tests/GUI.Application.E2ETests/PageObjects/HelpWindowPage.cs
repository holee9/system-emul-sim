using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace XrayDetector.Gui.E2ETests.PageObjects;

/// <summary>
/// Page Object for HelpWindow. SPEC-HELP-001: REQ-HELP-052
/// </summary>
public sealed class HelpWindowPage(AutomationElement window)
{
    public string GetTitle() => window.Name;

    /// <summary>
    /// Returns true if the topic TreeView is present in the HelpWindow.
    /// HelpWindow.xaml contains a TreeView bound to FilteredTopics in the left panel.
    /// </summary>
    public bool IsTopicTreeVisible()
    {
        var tree = window.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tree));
        return tree != null;
    }

    public void Close()
    {
        // Try title bar close button first (standard WPF window chrome)
        var closeBtn = window.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button));
        closeBtn?.AsButton().Invoke();
    }
}
