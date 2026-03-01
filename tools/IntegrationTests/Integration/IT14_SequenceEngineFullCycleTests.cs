using Xunit;
using FluentAssertions;
using McuSimulator.Core.Sequence;
using FpgaSimulator.Core.Fsm;

namespace IntegrationTests.Integration;

/// <summary>
/// IT-14: MCU SequenceEngine Full Cycle Tests.
/// Validates the state machine transitions through complete scan cycles,
/// error recovery, and statistics tracking.
/// Reference: SPEC-EMUL-001
/// </summary>
public class IT14_SequenceEngineFullCycleTests
{
    private readonly SequenceEngine _engine;

    public IT14_SequenceEngineFullCycleTests()
    {
        _engine = new SequenceEngine();
    }

    [Fact]
    public void SingleScanCycle_IdleToComplete_ReturnsToIdle()
    {
        // Arrange - Engine starts in Idle
        _engine.State.Should().Be(SequenceState.Idle);

        // Act - Walk through a full single scan cycle
        _engine.StartScan(ScanMode.Single);
        _engine.State.Should().Be(SequenceState.Configure);

        _engine.HandleEvent(SequenceEvent.ConfigDone);
        _engine.State.Should().Be(SequenceState.Arm);

        _engine.HandleEvent(SequenceEvent.ArmDone);
        _engine.State.Should().Be(SequenceState.Scanning);

        _engine.HandleEvent(SequenceEvent.FrameReady);
        _engine.State.Should().Be(SequenceState.Streaming);

        _engine.HandleEvent(SequenceEvent.Complete);

        // Assert - Single mode should return to Idle after Complete
        _engine.State.Should().Be(SequenceState.Idle,
            "single scan should auto-return to Idle after completion");

        // Verify statistics
        _engine.Statistics.FramesReceived.Should().Be(1);
        _engine.Statistics.FramesSent.Should().Be(1);
        _engine.Statistics.Errors.Should().Be(0);
    }

    [Fact]
    public void ContinuousScanCycle_LoopsToScanning()
    {
        // Arrange - Start continuous scan
        _engine.StartScan(ScanMode.Continuous);
        _engine.HandleEvent(SequenceEvent.ConfigDone);
        _engine.HandleEvent(SequenceEvent.ArmDone);
        _engine.State.Should().Be(SequenceState.Scanning);

        // Act - Complete first frame
        _engine.HandleEvent(SequenceEvent.FrameReady);
        _engine.State.Should().Be(SequenceState.Streaming);

        _engine.HandleEvent(SequenceEvent.Complete);

        // Assert - Continuous mode should loop back to Scanning, not Idle
        _engine.State.Should().Be(SequenceState.Scanning,
            "continuous scan should loop back to Scanning after Complete");

        // Complete second frame to verify loop continues
        _engine.HandleEvent(SequenceEvent.FrameReady);
        _engine.HandleEvent(SequenceEvent.Complete);
        _engine.State.Should().Be(SequenceState.Scanning,
            "continuous scan should keep looping");

        // Verify statistics track both frames
        _engine.Statistics.FramesReceived.Should().Be(2);
        _engine.Statistics.FramesSent.Should().Be(2);
    }

    [Fact]
    public void ErrorRecovery_RetryUnderMaxCount_ReturnsToIdle()
    {
        // Arrange - Start a scan and trigger error
        _engine.StartScan(ScanMode.Single);
        _engine.HandleEvent(SequenceEvent.ConfigDone);
        _engine.HandleEvent(SequenceEvent.ArmDone);
        _engine.State.Should().Be(SequenceState.Scanning);

        // Act - Trigger error
        _engine.HandleEvent(SequenceEvent.Error);
        _engine.State.Should().Be(SequenceState.Error);

        // Clear error (retry count 0 -> 1, under MaxRetryCount=3)
        _engine.HandleEvent(SequenceEvent.ErrorCleared);

        // Assert - Should return to Idle since retries remain
        _engine.State.Should().Be(SequenceState.Idle,
            "error cleared with retries remaining should return to Idle");
        _engine.RetryCount.Should().Be(1);
        _engine.Statistics.Errors.Should().Be(1);
        _engine.Statistics.Retries.Should().Be(1);
    }

    [Fact]
    public void ErrorRecovery_ExceedMaxRetry_StaysInError()
    {
        // Arrange - Exhaust all retries (MaxRetryCount = 3)
        for (uint i = 0; i < SequenceEngine.MaxRetryCount; i++)
        {
            _engine.StartScan(ScanMode.Single);
            _engine.HandleEvent(SequenceEvent.ConfigDone);
            _engine.HandleEvent(SequenceEvent.ArmDone);

            // Trigger error and clear
            _engine.HandleEvent(SequenceEvent.Error);
            _engine.HandleEvent(SequenceEvent.ErrorCleared);
            _engine.State.Should().Be(SequenceState.Idle,
                $"retry {i + 1} should return to Idle");
        }

        _engine.RetryCount.Should().Be(SequenceEngine.MaxRetryCount);

        // Act - Trigger one more error after retries exhausted
        _engine.StartScan(ScanMode.Single);
        _engine.HandleEvent(SequenceEvent.ConfigDone);
        _engine.HandleEvent(SequenceEvent.ArmDone);
        _engine.HandleEvent(SequenceEvent.Error);
        _engine.HandleEvent(SequenceEvent.ErrorCleared);

        // Assert - Should stay in Error since MaxRetryCount is exhausted
        _engine.State.Should().Be(SequenceState.Error,
            "error cleared with retries exhausted should stay in Error");
    }

    [Fact]
    public void StopScan_FromAnyState_ReturnsToIdle()
    {
        // Arrange - Start scan and advance to Scanning state
        _engine.StartScan(ScanMode.Continuous);
        _engine.HandleEvent(SequenceEvent.ConfigDone);
        _engine.HandleEvent(SequenceEvent.ArmDone);
        _engine.State.Should().Be(SequenceState.Scanning);

        // Act - Stop scan
        _engine.HandleEvent(SequenceEvent.StopScan);

        // Assert - Should return to Idle regardless of current state
        _engine.State.Should().Be(SequenceState.Idle,
            "StopScan should return to Idle from any state");
    }

    [Fact]
    public void Statistics_TracksFramesAndErrors()
    {
        // Arrange & Act - Run multiple cycles with mixed results
        // Cycle 1: Successful single scan
        _engine.StartScan(ScanMode.Single);
        _engine.HandleEvent(SequenceEvent.ConfigDone);
        _engine.HandleEvent(SequenceEvent.ArmDone);
        _engine.HandleEvent(SequenceEvent.FrameReady);
        _engine.HandleEvent(SequenceEvent.Complete);

        // Cycle 2: Continuous with 2 frames then stop
        _engine.StartScan(ScanMode.Continuous);
        _engine.HandleEvent(SequenceEvent.ConfigDone);
        _engine.HandleEvent(SequenceEvent.ArmDone);
        _engine.HandleEvent(SequenceEvent.FrameReady);
        _engine.HandleEvent(SequenceEvent.Complete);
        _engine.HandleEvent(SequenceEvent.FrameReady);
        _engine.HandleEvent(SequenceEvent.Complete);
        _engine.StopScan();

        // Cycle 3: Error during scan
        _engine.StartScan(ScanMode.Single);
        _engine.HandleEvent(SequenceEvent.ConfigDone);
        _engine.HandleEvent(SequenceEvent.ArmDone);
        _engine.HandleEvent(SequenceEvent.Error);
        _engine.HandleEvent(SequenceEvent.ErrorCleared);

        // Assert - Verify cumulative statistics
        _engine.Statistics.FramesReceived.Should().Be(3,
            "3 FrameReady events were processed");
        _engine.Statistics.FramesSent.Should().Be(3,
            "3 Complete events were processed from Streaming");
        _engine.Statistics.Errors.Should().Be(1,
            "1 Error event was triggered");
        _engine.Statistics.Retries.Should().Be(1,
            "1 ErrorCleared was processed successfully");
    }
}
