using FlaUI.Core.AutomationElements;
using Xunit;

namespace XrayDetector.Gui.E2ETests.Infrastructure;

/// <summary>
/// Base class for all E2E tests. Provides access to AppFixture.
/// SPEC-HELP-001: REQ-HELP-051
/// SPEC-E2E-002: REQ-E2E2-002 (RunWithScreenshot)
/// </summary>
[Collection("E2E")]
public abstract class E2ETestBase
{
    protected readonly AppFixture Fixture;
    protected AutomationElement MainWindow =>
        Fixture.IsDesktopAvailable
            ? Fixture.MainWindow ?? throw new InvalidOperationException("Main window not available")
            : throw new InvalidOperationException(
                "E2E tests require an interactive desktop session. " +
                "Run from PowerShell terminal or Visual Studio (not CI/bash). " +
                "Use [RequiresDesktopFact] to auto-skip in non-interactive environments.");

    protected E2ETestBase(AppFixture fixture)
    {
        Fixture = fixture;
    }

    /// <summary>
    /// Runs the test action and captures a screenshot on failure.
    /// SPEC-E2E-002: REQ-E2E2-002
    /// </summary>
    protected void RunWithScreenshot(string testName, Action test)
    {
        try
        {
            test();
        }
        catch (Exception)
        {
            ScreenshotHelper.CaptureOnFailure(
                testName,
                Fixture.IsDesktopAvailable ? Fixture.MainWindow : null);
            throw;
        }
    }

    /// <summary>
    /// Runs the async test and captures a screenshot on failure.
    /// SPEC-E2E-002: REQ-E2E2-002
    /// </summary>
    protected async Task RunWithScreenshotAsync(string testName, Func<Task> test)
    {
        try
        {
            await test();
        }
        catch (Exception)
        {
            ScreenshotHelper.CaptureOnFailure(
                testName,
                Fixture.IsDesktopAvailable ? Fixture.MainWindow : null);
            throw;
        }
    }
}

/// <summary>
/// xUnit collection definition - E2E tests share one AppFixture instance.
/// </summary>
[CollectionDefinition("E2E")]
public sealed class E2ECollection : ICollectionFixture<AppFixture> { }
