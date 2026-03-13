using FluentAssertions;
using System.Diagnostics;
using Xunit;
using XrayDetector.Gui.E2ETests.Infrastructure;

namespace XrayDetector.Gui.E2ETests.Tests.Unit;

/// <summary>
/// Unit and integration tests for AppFixture Attach mode.
/// SPEC-E2E-004: TAG-004
///
/// Unit tests (no desktop required):
///   - AttachMode_InvalidPid_ThrowsMeaningfulError
/// Desktop + running app tests (skip when GUI.Application not running):
///   - AttachMode_ValidPid_AttachesWithoutLaunching
///   - AttachMode_DoesNotKillProcessOnDispose
///
/// Collection("UnitTests"): prevents parallel execution with E2ELoggerBridgeTests
/// to avoid E2ELogger file-lock conflicts (both create AppFixture/E2ELogger in same second).
/// </summary>
[Collection("UnitTests")]
public sealed class AppFixtureAttachTests
{
    /// <summary>
    /// TAG-006: Invalid PID must throw immediately (no 30-second hang).
    /// REQ-6: Meaningful error, not TimeoutException from window wait.
    /// </summary>
    [Fact]
    public async Task AttachMode_InvalidPid_ThrowsMeaningfulError()
    {
        const string fakePid = "999999999"; // extremely unlikely to exist
        var original = Environment.GetEnvironmentVariable("XRAY_E2E_ATTACH_PID");
        try
        {
            Environment.SetEnvironmentVariable("XRAY_E2E_ATTACH_PID", fakePid);

            await using var fixture = new AppFixture();
            // Must throw before the 30-second window wait
            var act = () => fixture.InitializeAsync();
            await act.Should().ThrowAsync<InvalidOperationException>(
                "an invalid PID must produce a meaningful error immediately");
        }
        finally
        {
            Environment.SetEnvironmentVariable("XRAY_E2E_ATTACH_PID", original);
        }
    }

    /// <summary>
    /// TAG-001: Attach mode must not launch a new process.
    /// REQ-2: FlaUI attaches to existing process; no Process.Start called.
    /// Requires interactive desktop AND GUI.Application already running.
    /// </summary>
    [RequiresRunningGuiAppFact]
    public async Task AttachMode_ValidPid_AttachesWithoutLaunching()
    {
        var guiProcesses = Process.GetProcessesByName("GUI.Application");
        var targetPid = guiProcesses[0].Id;
        var processCountBefore = guiProcesses.Length;
        var original = Environment.GetEnvironmentVariable("XRAY_E2E_ATTACH_PID");
        try
        {
            Environment.SetEnvironmentVariable("XRAY_E2E_ATTACH_PID", targetPid.ToString());

            await using var fixture = new AppFixture();
            await fixture.InitializeAsync();

            fixture.IsDesktopAvailable.Should().BeTrue();
            fixture.MainWindow.Should().NotBeNull(
                "attaching to a running GUI.Application must find its main window");

            // Verify no new process was spawned
            var processCountAfter = Process.GetProcessesByName("GUI.Application").Length;
            processCountAfter.Should().Be(processCountBefore,
                "attach mode must not launch a new GUI.Application instance");
        }
        finally
        {
            Environment.SetEnvironmentVariable("XRAY_E2E_ATTACH_PID", original);
        }
    }

    /// <summary>
    /// TAG-005: DisposeAsync must NOT kill the attached process.
    /// REQ-5: Process lifecycle is owned by the caller, not AppFixture.
    /// Requires interactive desktop AND GUI.Application already running.
    /// </summary>
    [RequiresRunningGuiAppFact]
    public async Task AttachMode_DoesNotKillProcessOnDispose()
    {
        var guiProcesses = Process.GetProcessesByName("GUI.Application");
        var targetPid = guiProcesses[0].Id;
        var original = Environment.GetEnvironmentVariable("XRAY_E2E_ATTACH_PID");
        try
        {
            Environment.SetEnvironmentVariable("XRAY_E2E_ATTACH_PID", targetPid.ToString());

            await using (var fixture = new AppFixture())
            {
                await fixture.InitializeAsync();
                // fixture disposed here — DisposeAsync must not kill the target process
            }

            // Process must still be alive after fixture disposal
            var processAfter = Process.GetProcessById(targetPid);
            processAfter.HasExited.Should().BeFalse(
                "AppFixture in attach mode must not kill the process it did not own");
        }
        finally
        {
            Environment.SetEnvironmentVariable("XRAY_E2E_ATTACH_PID", original);
        }
    }
}

/// <summary>
/// xUnit Fact that skips unless both conditions are met:
/// 1. Interactive desktop session (EnvironmentDetector.IsInteractiveDesktop())
/// 2. GUI.Application.exe is currently running
/// Used for AppFixture Attach mode tests. SPEC-E2E-004: TAG-004
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class RequiresRunningGuiAppFactAttribute : FactAttribute
{
    public RequiresRunningGuiAppFactAttribute()
    {
        if (!EnvironmentDetector.IsInteractiveDesktop())
        {
            Skip = $"Requires interactive desktop session. " +
                   $"Environment: {EnvironmentDetector.GetEnvironmentSummary()}";
            return;
        }

        if (Process.GetProcessesByName("GUI.Application").Length == 0)
        {
            Skip = "Requires GUI.Application.exe to be running. " +
                   "Start the app manually before running this test.";
        }
    }
}
