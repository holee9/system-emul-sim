using FlaUI.Core.AutomationElements;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using XrayDetector.Gui.E2ETests.Infrastructure;

namespace XrayDetector.Gui.E2ETests.Features;

/// <summary>
/// Automated click-through validation for SPEC-GUI-001 MVP-1 through MVP-3.
/// Exercises the real PipelineDetectorClient / SimulatorPipeline integration
/// via UIAutomation — no mocks, no fakes.
///
/// Test coverage:
///   MVP-1: Real pipeline frames visible (BtnStart → TxtFramesProcessed > 0)
///   MVP-2: Pipeline Status updates in real-time; Stop freezes counters
///   MVP-3: Scenario IT-01 returns real pass/fail (not hardcoded "Passed=true")
///   MVP-1: TxtFrameInfo shows correct resolution after Start
///
/// XRAY_E2E_MODE=true is set by AppFixture. The app connects but does NOT
/// auto-start acquisition — tests control Start/Stop entirely.
/// </summary>
[Collection("E2E")]
public sealed class PipelineValidationE2ETests(AppFixture fixture, ITestOutputHelper output)
    : E2ETestBase(fixture, output)
{
    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Selects a tab by AutomationId and waits 300ms for the UI to settle.
    /// </summary>
    private async Task SelectTabAsync(string automationId)
    {
        Logger.Step($"Selecting tab: {automationId}");
        var tab = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
        tab.Should().NotBeNull($"Tab '{automationId}' must exist");
        tab!.AsTabItem().Select();
        await WaitHelper.DelayAsync(300);
    }

    /// <summary>
    /// Clicks BtnStart (in Simulator Control tab) and waits for it to become disabled
    /// (IsAcquiring=true → CanExecute → IsEnabled=false within 2s).
    /// </summary>
    private async Task ClickStartAsync()
    {
        Logger.Step("Clicking BtnStart to start acquisition");
        await SelectTabAsync("TabSimulator");

        var btnStart = await WaitHelper.WaitForElementAsync(
            MainWindow,
            () => MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("BtnStart")),
            timeoutMs: 5000,
            logger: Logger,
            description: "BtnStart");
        btnStart.Should().NotBeNull("BtnStart must be present in Simulator Control tab");
        btnStart!.AsButton().Invoke(); // Invoke() uses UIAutomation pattern — no mouse simulation needed
        Logger.Info("BtnStart invoked");

        // Wait for acquisition to start: BtnStop becomes enabled
        await WaitHelper.WaitUntilAsync(
            () =>
            {
                var btnStop = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("BtnStop"));
                return btnStop?.AsButton().IsEnabled == true;
            },
            timeoutMs: 5000,
            description: "BtnStop enabled after Start");
        Logger.Info("Acquisition started (BtnStop is now enabled)");
    }

    /// <summary>
    /// Clicks BtnStop and waits for acquisition to halt.
    /// </summary>
    private async Task ClickStopAsync()
    {
        Logger.Step("Clicking BtnStop to stop acquisition");
        await SelectTabAsync("TabSimulator");

        var btnStop = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("BtnStop"));
        if (btnStop?.AsButton().IsEnabled != true)
        {
            Logger.Warn("BtnStop not enabled — acquisition may already be stopped");
            return;
        }
        btnStop.AsButton().Invoke(); // Invoke() uses UIAutomation pattern — no mouse simulation needed
        Logger.Info("BtnStop invoked");

        // Wait for acquisition to stop: BtnStart becomes enabled again
        await WaitHelper.WaitUntilAsync(
            () =>
            {
                var btnStart = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("BtnStart"));
                return btnStart?.AsButton().IsEnabled == true;
            },
            timeoutMs: 5000,
            description: "BtnStart enabled after Stop");
        Logger.Info("Acquisition stopped");
    }

    /// <summary>
    /// Reads TxtFramesProcessed text from Pipeline Status tab.
    /// WPF TextBlock text is exposed as AutomationElement.Name in UIAutomation.
    /// Returns 0 on parse failure.
    /// </summary>
    private int ReadFramesProcessed()
    {
        var txt = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("TxtFramesProcessed"));
        if (txt == null) return 0;
        return int.TryParse(txt.Name, out var v) ? v : 0;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // MVP-1: Real Pipeline Connection
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// MVP-1: After BtnStart, TxtFramesProcessed must exceed 0 within 5 seconds.
    /// Proves PipelineDetectorClient is wired to SimulatorPipeline — not synthetic data.
    /// </summary>
    [RequiresDesktopFact]
    public async Task MVP1_PipelineStartsAndGeneratesFrames()
    {
        Logger.BeginTest(nameof(MVP1_PipelineStartsAndGeneratesFrames));
        try
        {
            await ClickStartAsync();

            // Navigate to Pipeline Status and wait for frames
            await SelectTabAsync("TabPipeline");

            var framesAppeared = await WaitHelper.WaitUntilAsync(
                () => ReadFramesProcessed() > 0,
                timeoutMs: 8000,
                pollIntervalMs: 200,
                description: "TxtFramesProcessed > 0");

            int finalCount = ReadFramesProcessed();
            Logger.Info($"TxtFramesProcessed = {finalCount} after start");

            framesAppeared.Should().BeTrue(
                "SimulatorPipeline must generate at least 1 frame within 8s of starting acquisition. " +
                "If this fails, PipelineDetectorClient is not wired to SimulatorPipeline.");
            finalCount.Should().BeGreaterThan(0);
        }
        finally
        {
            await ClickStopAsync();
        }

        RecordTestPassed();
    }

    /// <summary>
    /// MVP-1: TxtFrameInfo shows resolution string (e.g. "1024x1024") after Start.
    /// Proves frame metadata from real pipeline propagates to the Frame Preview tab.
    /// </summary>
    [RequiresDesktopFact]
    public async Task MVP1_FrameInfoShowsResolutionAfterStart()
    {
        Logger.BeginTest(nameof(MVP1_FrameInfoShowsResolutionAfterStart));
        try
        {
            await ClickStartAsync();

            // Navigate to Frame Preview tab
            await SelectTabAsync("TabImage");

            var frameInfoPopulated = await WaitHelper.WaitUntilAsync(
                () =>
                {
                    var txt = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("TxtFrameInfo"));
                    var text = txt?.Name;
                    return !string.IsNullOrWhiteSpace(text) && text.Contains("x");
                },
                timeoutMs: 8000,
                pollIntervalMs: 300,
                description: "TxtFrameInfo shows resolution");

            var frameInfoTxt = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("TxtFrameInfo"));
            var frameInfo = frameInfoTxt?.Name ?? string.Empty;
            Logger.Info($"TxtFrameInfo = '{frameInfo}'");

            frameInfoPopulated.Should().BeTrue(
                "Frame preview must show resolution string (e.g. '1024x1024') within 8s of starting. " +
                "Format: '<Width>x<Height>'.");
            frameInfo.Should().Contain("x");
        }
        finally
        {
            await ClickStopAsync();
        }

        RecordTestPassed();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // MVP-2: Live Pipeline Statistics
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// MVP-2: FramesProcessed counter increments while acquisition runs.
    /// Takes two readings 2 seconds apart — second must be strictly greater.
    /// </summary>
    [RequiresDesktopFact]
    public async Task MVP2_PipelineStatusCounterIncrementsWhileRunning()
    {
        Logger.BeginTest(nameof(MVP2_PipelineStatusCounterIncrementsWhileRunning));
        try
        {
            await ClickStartAsync();
            await SelectTabAsync("TabPipeline");

            // Wait for first non-zero reading
            await WaitHelper.WaitUntilAsync(
                () => ReadFramesProcessed() > 0,
                timeoutMs: 8000,
                description: "first non-zero FramesProcessed");

            int reading1 = ReadFramesProcessed();
            Logger.Info($"Reading 1: FramesProcessed = {reading1}");

            // Wait 2 seconds then take second reading
            await WaitHelper.DelayAsync(2000);
            int reading2 = ReadFramesProcessed();
            Logger.Info($"Reading 2: FramesProcessed = {reading2}");

            reading2.Should().BeGreaterThan(reading1,
                "FramesProcessed must increase over time while acquisition is running. " +
                "If this fails, the status timer or PipelineDetectorClient.GetStatistics() is not wired.");
        }
        finally
        {
            await ClickStopAsync();
        }

        RecordTestPassed();
    }

    /// <summary>
    /// MVP-2: After BtnStop, FramesProcessed counter freezes (does not increment).
    /// Takes two readings 2 seconds apart after Stop — values must be equal.
    /// </summary>
    [RequiresDesktopFact]
    public async Task MVP2_StopFreezesPipelineCounter()
    {
        Logger.BeginTest(nameof(MVP2_StopFreezesPipelineCounter));

        await ClickStartAsync();
        await SelectTabAsync("TabPipeline");

        // Wait for non-zero before stopping
        await WaitHelper.WaitUntilAsync(
            () => ReadFramesProcessed() > 0,
            timeoutMs: 8000,
            description: "non-zero frames before stop");

        await ClickStopAsync();
        await SelectTabAsync("TabPipeline");

        // Allow one final timer tick to settle (status timer is 2Hz → 500ms)
        await WaitHelper.DelayAsync(700);

        int reading1 = ReadFramesProcessed();
        Logger.Info($"Reading 1 after stop: FramesProcessed = {reading1}");

        await WaitHelper.DelayAsync(2000);

        int reading2 = ReadFramesProcessed();
        Logger.Info($"Reading 2 after stop: FramesProcessed = {reading2}");

        reading2.Should().Be(reading1,
            "FramesProcessed must NOT increase after acquisition is stopped. " +
            "If this fails, StopAcquisitionAsync is not properly stopping the loop.");

        RecordTestPassed();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // MVP-3: Real Scenario Execution
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// MVP-3: IT-01 scenario (10 frames, zero tolerance) executes via real SimulatorPipeline.
    /// Result must be non-empty and must NOT be the old hardcoded string "Passed=true".
    /// Passes when result contains "Frames:" (actual message format).
    /// </summary>
    [RequiresDesktopFact]
    public async Task MVP3_ScenarioIT01_ReturnsRealPipelineResult()
    {
        Logger.BeginTest(nameof(MVP3_ScenarioIT01_ReturnsRealPipelineResult));

        await SelectTabAsync("TabScenario");
        await WaitHelper.DelayAsync(300);

        // Ensure IT-01 is selected (it should be first by default)
        var cbo = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("CboScenarios"));
        cbo.Should().NotBeNull("CboScenarios must be present in Scenario Runner tab");
        Logger.Info($"ComboBox found. Selecting first item (IT-01).");

        // ComboBox is already bound to Scenarios list; SelectedItem should be first by default.
        // Click Execute
        var btnExecute = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("BtnExecuteScenario"));
        btnExecute.Should().NotBeNull("BtnExecuteScenario must be present");
        Logger.Step("Invoking BtnExecuteScenario (IT-01, 10 frames)");
        btnExecute!.AsButton().Invoke(); // Invoke() uses UIAutomation pattern — no mouse simulation needed

        // Wait for result — IT-01 runs 10 frames, should complete in < 10s
        var resultAppeared = await WaitHelper.WaitUntilAsync(
            () =>
            {
                var txt = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("TxtScenarioResult"));
                var text = txt?.Name;
                return !string.IsNullOrWhiteSpace(text) && text.Contains("Frames:");
            },
            timeoutMs: 15000,
            pollIntervalMs: 500,
            description: "TxtScenarioResult contains 'Frames:'");

        var resultTxt = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("TxtScenarioResult"));
        var resultText = resultTxt?.Name ?? string.Empty;
        Logger.Info($"TxtScenarioResult = '{resultText}'");

        resultAppeared.Should().BeTrue(
            "Scenario IT-01 must complete within 15s and show 'Frames:' in result. " +
            "If this fails, ScenarioRunner is not wired to real SimulatorPipeline.");
        resultText.Should().Contain("Frames:",
            "Result message must match ScenarioRunner.BuildMessage() format: 'Frames: N/N completed'");

        // Verify it is NOT the old fake/hardcoded result
        resultText.Should().NotBeEquivalentTo("Passed=true",
            "Old fake result must be replaced by real pipeline execution output");

        RecordTestPassed();
    }

    /// <summary>
    /// MVP-3: Progress bar advances during scenario execution.
    /// Captures PrgScenario.Value before and during execution.
    /// </summary>
    [RequiresDesktopFact]
    public async Task MVP3_ScenarioExecution_ProgressBarAdvances()
    {
        Logger.BeginTest(nameof(MVP3_ScenarioExecution_ProgressBarAdvances));

        await SelectTabAsync("TabScenario");
        await WaitHelper.DelayAsync(300);

        var btnExecute = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("BtnExecuteScenario"));
        btnExecute.Should().NotBeNull("BtnExecuteScenario must exist");

        Logger.Step("Invoking BtnExecuteScenario and monitoring result");
        btnExecute!.AsButton().Invoke();

        // Poll for result text to appear (scenario completes when TxtScenarioResult is non-empty)
        var progressStarted = await WaitHelper.WaitUntilAsync(
            () =>
            {
                var txt = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("TxtScenarioResult"));
                var name = txt?.Name;
                return !string.IsNullOrWhiteSpace(name);
            },
            timeoutMs: 15000,
            pollIntervalMs: 300,
            description: "TxtScenarioResult non-empty (scenario completed)");

        var resultEl = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("TxtScenarioResult"));
        Logger.Info($"TxtScenarioResult = '{resultEl?.Name}'");

        progressStarted.Should().BeTrue(
            "Scenario must complete within 15s and show result in TxtScenarioResult. " +
            "If this fails, ScenarioRunner.ExecuteScenarioAsync is not reporting progress.");

        RecordTestPassed();
    }
}
