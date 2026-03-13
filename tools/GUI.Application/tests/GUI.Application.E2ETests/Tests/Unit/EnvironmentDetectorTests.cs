using FluentAssertions;
using Xunit;
using XrayDetector.Gui.E2ETests.Infrastructure;

namespace XrayDetector.Gui.E2ETests.Tests.Unit;

/// <summary>
/// Unit tests for EnvironmentDetector.
/// TAG-001: Tests must pass without desktop/GUI.
/// </summary>
public sealed class EnvironmentDetectorTests
{
    // ── IsCI ────────────────────────────────────────────────────────────────

    [Fact]
    public void IsCI_WhenCI_True_ReturnsTrue()
    {
        var result = EnvironmentDetector.IsInteractiveDesktop(
            ci: "true", githubActions: null, force: null,
            sessionName: "Console", userInteractive: true,
            wtSession: null, msystem: null);

        result.Should().BeFalse("CI=true overrides everything");
    }

    [Fact]
    public void IsCI_WhenGitHubActions_ReturnsTrue()
    {
        var result = EnvironmentDetector.IsInteractiveDesktop(
            ci: null, githubActions: "true", force: null,
            sessionName: "Console", userInteractive: true,
            wtSession: null, msystem: null);

        result.Should().BeFalse("GITHUB_ACTIONS set means CI environment");
    }

    [Fact]
    public void IsInteractiveDesktop_WhenCI_ReturnsFalse()
    {
        var result = EnvironmentDetector.IsInteractiveDesktop(
            ci: "true", githubActions: null, force: null,
            sessionName: null, userInteractive: false,
            wtSession: null, msystem: null);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsInteractiveDesktop_WhenForced_ReturnsTrue()
    {
        // XRAY_E2E_FORCE=1 overrides CI detection
        var result = EnvironmentDetector.IsInteractiveDesktop(
            ci: null, githubActions: null, force: "1",
            sessionName: null, userInteractive: false,
            wtSession: null, msystem: null);

        result.Should().BeTrue("XRAY_E2E_FORCE=1 forces interactive mode");
    }

    [Fact]
    public void IsInteractiveDesktop_WhenSessionNameConsole_ReturnsTrue()
    {
        var result = EnvironmentDetector.IsInteractiveDesktop(
            ci: null, githubActions: null, force: null,
            sessionName: "Console", userInteractive: false,
            wtSession: null, msystem: null);

        result.Should().BeTrue("SESSIONNAME=Console is a real desktop session");
    }

    [Fact]
    public void IsInteractiveDesktop_WhenSessionNameRDP_ReturnsTrue()
    {
        var result = EnvironmentDetector.IsInteractiveDesktop(
            ci: null, githubActions: null, force: null,
            sessionName: "RDP-Tcp#0", userInteractive: false,
            wtSession: null, msystem: null);

        result.Should().BeTrue("SESSIONNAME=RDP-Tcp#0 is a remote desktop session");
    }

    [Fact]
    public void IsInteractiveDesktop_WhenNoSessionName_WithGitBashEnv_ReturnsFalse()
    {
        // MSYSTEM set = Git Bash (MSYS2) - not a real desktop
        var result = EnvironmentDetector.IsInteractiveDesktop(
            ci: null, githubActions: null, force: null,
            sessionName: null, userInteractive: true,
            wtSession: null, msystem: "MINGW64");

        result.Should().BeFalse("Git Bash (MSYSTEM set) is not an interactive desktop for UIAutomation");
    }

    [Fact]
    public void IsInteractiveDesktop_WhenUserInteractive_WithNoOverrides_ReturnsTrue()
    {
        var result = EnvironmentDetector.IsInteractiveDesktop(
            ci: null, githubActions: null, force: null,
            sessionName: null, userInteractive: true,
            wtSession: null, msystem: null);

        result.Should().BeTrue("UserInteractive fallback when no other indicators present");
    }

    [Fact]
    public void GetEnvironmentSummary_ReturnsNonEmptyString()
    {
        // Verify GetEnvironmentSummary() doesn't throw and returns a useful string
        var summary = EnvironmentDetector.GetEnvironmentSummary();
        summary.Should().NotBeNullOrEmpty();
        summary.Should().Contain("CI=");
        summary.Should().Contain("SESSIONNAME=");
        summary.Should().Contain("UserInteractive=");
    }
}
