using Xunit;

namespace XrayDetector.Gui.E2ETests.Infrastructure;

/// <summary>
/// xUnit Fact that auto-skips in non-interactive (CI) environments.
/// Requires a real desktop session for FlaUI/UIAutomation to work.
/// SPEC-HELP-001: E2E tests require interactive Windows session.
/// TAG-001: Refactored to use EnvironmentDetector for centralized detection.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequiresDesktopFactAttribute : FactAttribute
{
    public RequiresDesktopFactAttribute()
    {
        if (!EnvironmentDetector.IsInteractiveDesktop())
            Skip = $"Requires interactive desktop session (FlaUI UIAutomation unavailable in CI). " +
                   $"Environment: {EnvironmentDetector.GetEnvironmentSummary()}";
    }

    /// <summary>
    /// Backward-compatible delegate to EnvironmentDetector.IsInteractiveDesktop().
    /// </summary>
    internal static bool IsInteractiveDesktop() => EnvironmentDetector.IsInteractiveDesktop();
}
