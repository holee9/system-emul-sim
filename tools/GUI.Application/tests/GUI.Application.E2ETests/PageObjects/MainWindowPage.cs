using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using XrayDetector.Gui.E2ETests.Infrastructure;

namespace XrayDetector.Gui.E2ETests.PageObjects;

/// <summary>
/// Page Object for MainWindow. SPEC-HELP-001: REQ-HELP-052
/// </summary>
public sealed class MainWindowPage
{
    private readonly AutomationElement _window;
    // fixture retained for screenshot and automation access
    private readonly AppFixture _fixture;

    public MainWindowPage(AutomationElement window, AppFixture fixture)
    {
        _window = window;
        _fixture = fixture;
    }

    // Menu navigation
    public void ClickMenu(string topMenu, string subMenu)
    {
        var menu = _window.FindFirstDescendant(cf => cf.ByControlType(ControlType.MenuBar));
        var topItem = menu.FindFirstChild(cf => cf.ByName(topMenu));
        topItem.AsMenuItem().Click();
        var subItem = topItem.AsMenuItem().Items.First(x => x.Name == subMenu);
        subItem.Click();
    }

    public string GetStatusBarText()
    {
        var statusBar = _window.FindFirstDescendant(cf => cf.ByAutomationId("StatusBarMain"));
        return statusBar?.FindFirstChild()?.Name ?? string.Empty;
    }

    public string GetStatusBarVersion()
    {
        var versionEl = _window.FindFirstDescendant(cf => cf.ByAutomationId("StatusBarVersion"));
        return versionEl?.Name ?? string.Empty;
    }

    public int GetActiveTabIndex()
    {
        var tabControl = _window.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tab));
        var tabs = tabControl?.FindAllChildren(cf => cf.ByControlType(ControlType.TabItem)) ?? Array.Empty<AutomationElement>();
        for (int i = 0; i < tabs.Length; i++)
        {
            if (tabs[i].AsTabItem().IsSelected) return i;
        }
        return -1;
    }

    public async Task WaitForWindowAsync(int timeoutMs = 5000)
        => await WaitHelper.WaitUntilAsync(() => _window != null, timeoutMs);
}
