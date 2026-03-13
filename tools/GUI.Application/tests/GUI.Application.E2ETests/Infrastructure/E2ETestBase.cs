using FlaUI.Core.AutomationElements;
using Xunit;
using Xunit.Abstractions;

namespace XrayDetector.Gui.E2ETests.Infrastructure;

/// <summary>
/// Base class for all E2E tests. Provides access to AppFixture, structured logging,
/// automatic log flush to xUnit output, and screenshot-on-failure detection.
///
/// Usage:
///   - Constructor must accept ITestOutputHelper (xUnit injects it per-test).
///   - Call RecordTestPassed() as the last line of each passing test method.
///   - Use RunWithScreenshot() for explicit failure screenshot wrapping.
///
/// SPEC-HELP-001: REQ-HELP-051
/// SPEC-E2E-002: REQ-E2E2-001 (logging), REQ-E2E2-002 (screenshot-on-failure)
/// </summary>
[Collection("E2E")]
public abstract class E2ETestBase : IAsyncLifetime
{
    protected readonly AppFixture Fixture;
    protected readonly ITestOutputHelper OutputHelper;

    private bool _testPassed;

    protected AutomationElement MainWindow =>
        Fixture.IsDesktopAvailable
            ? Fixture.MainWindow ?? throw new InvalidOperationException("Main window not available")
            : throw new InvalidOperationException(
                "E2E tests require an interactive desktop session. " +
                "Run from PowerShell terminal or Visual Studio (not CI/bash). " +
                "Use [RequiresDesktopFact] to auto-skip in non-interactive environments.");

    /// <summary>Shortcut to the shared E2E session logger.</summary>
    protected E2ELogger Logger => Fixture.Logger;

    protected E2ETestBase(AppFixture fixture, ITestOutputHelper output)
    {
        Fixture = fixture;
        OutputHelper = output;
    }

    /// <summary>
    /// Called by xUnit before each test. Begins test-scoped log context.
    /// </summary>
    public Task InitializeAsync()
    {
        _testPassed = false;
        Logger.BeginTest(GetType().Name);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called by xUnit after each test. Flushes per-test log buffer to xUnit output.
    /// </summary>
    public Task DisposeAsync()
    {
        Logger.EndTest(OutputHelper, _testPassed);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Mark the current test as passed (used for auto-screenshot detection).
    /// When called, DisposeAsync skips screenshot. When not called, screenshots
    /// are only captured via RunWithScreenshot().
    /// </summary>
    protected void RecordTestPassed() => _testPassed = true;

    /// <summary>
    /// Runs the test action and captures a screenshot on exception (explicit alternative to RecordTestPassed).
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
    /// Runs the async test and captures a screenshot on exception.
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
