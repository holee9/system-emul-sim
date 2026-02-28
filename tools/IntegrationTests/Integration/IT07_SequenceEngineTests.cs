using Xunit;
using FluentAssertions;
using IntegrationTests.Helpers;
using FpgaSimulator.Core.Fsm;

namespace IntegrationTests.Integration;

/// <summary>
/// IT-07: Sequence Engine State Machine test.
/// Validates 6-state FSM transitions (IDLE → INIT → READY → CAPTURE → TRANSFER → ERROR).
/// Reference: SPEC-INTEG-001 AC-INTEG-007
/// </summary>
public class IT07_SequenceEngineTests
{
    [Fact]
    public void StateMachine_ShallTransition_IdleToIntegrateToReadout()
    {
        // Arrange - Start in IDLE state
        var fsm = new PanelScanFsmSimulator();
        fsm.CurrentState.Should().Be(FsmState.Idle, "Initial state should be IDLE");

        // Act - Start scan (IDLE → INTEGRATE)
        fsm.StartScan();

        // Assert
        fsm.CurrentState.Should().Be(FsmState.Integrate, "Should transition to INTEGRATE");

        // Process ticks to transition through READOUT
        for (int i = 0; i < 1100; i++) // 1000 ticks for gate_on + 100 for gate_off
        {
            fsm.ProcessTick();
        }

        fsm.CurrentState.Should().Be(FsmState.Readout, "Should transition to READOUT");
    }

    [Fact]
    public void StateMachine_ShallCompleteFullCycle_NoInvalidTransitions()
    {
        // Arrange - Single scan mode
        var fsm = new PanelScanFsmSimulator();
        fsm.SetScanMode(ScanMode.Single);

        // Act - Run full cycle: IDLE → INTEGRATE → READOUT → ... → FRAME_DONE → IDLE
        fsm.StartScan(); // IDLE → INTEGRATE

        var states = new List<FsmState> { fsm.CurrentState };

        // Process until back to IDLE (max 2000 ticks)
        for (int i = 0; i < 2000 && fsm.CurrentState != FsmState.Idle; i++)
        {
            fsm.ProcessTick();
            if (fsm.CurrentState != states[^1])
            {
                states.Add(fsm.CurrentState);
            }
        }

        // Assert - Valid state sequence
        fsm.CurrentState.Should().Be(FsmState.Idle, "Should return to IDLE after single scan");

        // Verify no invalid states (only valid FSM states encountered)
        var validStates = new[] { FsmState.Idle, FsmState.Integrate, FsmState.Readout, FsmState.LineDone, FsmState.FrameDone };
        foreach (var state in states)
        {
            validStates.Should().Contain(state, $"State {state} should be a valid FSM state");
        }

        // Frame counter should increment
        fsm.FrameCounter.Should().Be(1, "Frame counter should increment after complete scan");
    }

    [Fact]
    public void StateMachine_ShallHandleContinuousMode_MultipleFrames()
    {
        // Arrange - Continuous scan mode
        var fsm = new PanelScanFsmSimulator();
        fsm.SetScanMode(ScanMode.Continuous);
        fsm.SetPanelDimensions(256, 256); // Smaller for faster testing

        // Act - Start continuous scan
        fsm.StartScan();

        // Process multiple frames
        for (int i = 0; i < 2500; i++) // Enough for 2 frames
        {
            fsm.ProcessTick();
        }

        // Assert - Multiple frames completed
        fsm.FrameCounter.Should().BeGreaterOrEqualTo(2, "Continuous mode should produce multiple frames");
        fsm.CurrentState.Should().NotBe(FsmState.Error, "Should not enter ERROR state");
    }

    [Fact]
    public void StateMachine_ShallEnterErrorState_OnErrorInjection()
    {
        // Arrange
        var fsm = new PanelScanFsmSimulator();
        fsm.StartScan();

        // Act - Inject error
        fsm.TriggerError(ErrorFlags.CrcError);

        // Assert - Should be in ERROR state
        fsm.CurrentState.Should().Be(FsmState.Error, "Should transition to ERROR on error injection");
    }

    [Fact]
    public void StateMachine_ShallRecoverFromError_ErrorToIdle()
    {
        // Arrange - Enter ERROR state
        var fsm = new PanelScanFsmSimulator();
        fsm.StartScan();
        fsm.TriggerError(ErrorFlags.Timeout);
        fsm.CurrentState.Should().Be(FsmState.Error);

        // Act - Clear error
        fsm.ClearError();

        // Assert - Should return to IDLE
        fsm.CurrentState.Should().Be(FsmState.Idle, "Should return to IDLE after error clear");

        // Error flags should be cleared
        var status = fsm.GetStatus();
        status.ErrorFlags.Should().Be(ErrorFlags.None, "Error flags should be cleared");
    }

    [Fact]
    public void StateMachine_ShallPreventInvalidTransitions_NoInvalidStates()
    {
        // Arrange
        var fsm = new PanelScanFsmSimulator();

        // Act & Assert - Test various invalid operation sequences
        // Test 1: Stop while not started (should stay IDLE)
        fsm.StopScan();
        fsm.CurrentState.Should().Be(FsmState.Idle);

        // Test 2: Reset from ERROR should work
        fsm.TriggerError(ErrorFlags.CrcError);
        fsm.Reset();
        fsm.CurrentState.Should().Be(FsmState.Idle);
        fsm.FrameCounter.Should().Be(0, "Frame counter should reset");

        // Test 3: Starting from ERROR should not work (need ClearError first)
        fsm.TriggerError(ErrorFlags.Overflow);
        fsm.StartScan();
        fsm.CurrentState.Should().Be(FsmState.Error, "Start from ERROR should not work");
    }

    [Fact]
    public void StateMachine_ShallBeDeterministic_MultipleCycles()
    {
        // Arrange
        const int cycleCount = 5;
        var frameCounts = new List<uint>();

        // Act - Run multiple scan cycles
        for (int cycle = 0; cycle < cycleCount; cycle++)
        {
            var fsm = new PanelScanFsmSimulator();
            fsm.SetScanMode(ScanMode.Single);
            fsm.SetPanelDimensions(256, 256);

            fsm.StartScan();

            // Process until IDLE
            for (int i = 0; i < 2000 && fsm.CurrentState != FsmState.Idle; i++)
            {
                fsm.ProcessTick();
            }

            frameCounts.Add(fsm.FrameCounter);
        }

        // Assert - Each cycle should produce exactly 1 frame
        frameCounts.Should().AllSatisfy(count => count.Should().Be(1),
            "Each single scan cycle should produce exactly 1 frame");
    }

    [Fact]
    public void StateMachine_StatusSnapshot_ShallReflectCurrentState()
    {
        // Arrange
        var fsm = new PanelScanFsmSimulator();
        fsm.SetPanelDimensions(512, 512);

        // Act - Get status in IDLE
        var idleStatus = fsm.GetStatus();
        fsm.StartScan();

        var runningStatus = fsm.GetStatus();

        // Assert - Status should reflect state
        idleStatus.State.Should().Be(FsmState.Idle);
        runningStatus.State.Should().Be(FsmState.Integrate);
        runningStatus.ScanMode.Should().Be(ScanMode.Single);
    }

    [Fact]
    public void StateMachine_ErrorState_ShouldLogAllErrors()
    {
        // Arrange
        var fsm = new PanelScanFsmSimulator();

        // Act - Trigger multiple errors
        fsm.TriggerError(ErrorFlags.CrcError);
        var status1 = fsm.GetStatus();

        fsm.TriggerError(ErrorFlags.Timeout);
        var status2 = fsm.GetStatus();

        // Assert - Error flags should accumulate
        status1.ErrorFlags.Should().Be(ErrorFlags.CrcError);
        status2.ErrorFlags.Should().Be(ErrorFlags.CrcError | ErrorFlags.Timeout,
            "Error flags should accumulate");
    }

    [Fact]
    public void StateMachine_LineCounter_ShallIncrementCorrectly()
    {
        // Arrange
        var fsm = new PanelScanFsmSimulator();
        fsm.SetPanelDimensions(256, 256);
        fsm.SetGateTiming(100, 10); // Fast timing for testing

        // Act - Start scan and process
        fsm.StartScan();

        var lineCounts = new List<uint>();
        for (int i = 0; i < 3000; i++)
        {
            fsm.ProcessTick();
            if (fsm.CurrentState == FsmState.LineDone || fsm.CurrentState == FsmState.FrameDone)
            {
                lineCounts.Add(fsm.LineCounter);
            }
            if (fsm.CurrentState == FsmState.Idle)
                break;
        }

        // Assert - Line counter should reach panel rows (256)
        lineCounts.Should().Contain(256, "Line counter should reach panel row count");
    }
}
