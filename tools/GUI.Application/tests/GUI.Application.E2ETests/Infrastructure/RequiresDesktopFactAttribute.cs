using Xunit;

namespace XrayDetector.Gui.E2ETests.Infrastructure;

/// <summary>
/// xUnit Fact that auto-skips in non-interactive (CI) environments.
/// Requires a real desktop session for FlaUI/UIAutomation to work.
/// SPEC-HELP-001: E2E tests require interactive Windows session.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequiresDesktopFactAttribute : FactAttribute
{
    public RequiresDesktopFactAttribute()
    {
        if (!IsInteractiveDesktop())
            Skip = "Requires interactive desktop session (FlaUI UIAutomation unavailable in CI)";
    }

    internal static bool IsInteractiveDesktop()
    {
        if (Environment.GetEnvironmentVariable("CI") == "true") return false;
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true") return false;
        return Environment.UserInteractive;
    }
}
