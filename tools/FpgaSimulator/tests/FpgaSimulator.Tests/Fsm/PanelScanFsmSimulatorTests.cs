namespace FpgaSimulator.Tests.Fsm;

using FluentAssertions;
using FpgaSimulator.Core.Fsm;
using Xunit;

public class PanelScanFsmSimulatorTests
{
    [Fact]
    public void Constructor_ShouldInitializeToIdleState()
    {
        // Arrange & Act
        var fsm = new PanelScanFsmSimulator();

        // Assert
        fsm.CurrentState.Should().Be(FsmState.Idle);
        fsm.FrameCounter.Should().Be(0);
        fsm.LineCounter.Should().Be(0);
    }

    [Fact]
    public void StartScan_ShouldTransitionToIntegrateState()
    {
        // Arrange
        var fsm = new PanelScanFsmSimulator();

        // Act
        fsm.StartScan();

        // Assert
        fsm.CurrentState.Should().Be(FsmState.Integrate);
    }

    [Fact]
    public void StopScan_ShouldReturnToIdleState()
    {
        // Arrange
        var fsm = new PanelScanFsmSimulator();
        fsm.StartScan();

        // Act
        fsm.StopScan();

        // Assert
        fsm.CurrentState.Should().Be(FsmState.Idle);
    }

    [Theory]
    [InlineData(ScanMode.Single)]
    [InlineData(ScanMode.Continuous)]
    [InlineData(ScanMode.Calibration)]
    public void SetScanMode_ShouldUpdateMode(ScanMode mode)
    {
        // Arrange
        var fsm = new PanelScanFsmSimulator();

        // Act
        fsm.SetScanMode(mode);

        // Assert
        fsm.ScanMode.Should().Be(mode);
    }

    [Fact]
    public void ProcessTick_WhenIdle_ShouldRemainIdle()
    {
        // Arrange
        var fsm = new PanelScanFsmSimulator();

        // Act
        fsm.ProcessTick();

        // Assert
        fsm.CurrentState.Should().Be(FsmState.Idle);
    }

    [Fact]
    public void ProcessTick_WhenIntegrateAndTimerComplete_ShouldTransitionToReadout()
    {
        // Arrange
        var fsm = new PanelScanFsmSimulator();
        fsm.SetGateTiming(gateOnUs: 1); // Very short timing for test
        fsm.StartScan();

        // Act - Process enough ticks to complete integration
        for (int i = 0; i < 2; i++)
        {
            fsm.ProcessTick();
        }

        // Assert
        fsm.CurrentState.Should().Be(FsmState.Readout);
    }

    [Fact]
    public void Reset_ShouldReturnToIdleStateAndClearCounters()
    {
        // Arrange
        var fsm = new PanelScanFsmSimulator();
        fsm.StartScan();
        fsm.SetGateTiming(gateOnUs: 1);
        for (int i = 0; i < 5; i++)
        {
            fsm.ProcessTick();
        }

        // Act
        fsm.Reset();

        // Assert
        fsm.CurrentState.Should().Be(FsmState.Idle);
        fsm.FrameCounter.Should().Be(0);
        fsm.LineCounter.Should().Be(0);
    }

    [Fact]
    public void ProcessTick_WhenFrameComplete_ShouldIncrementFrameCounter()
    {
        // Arrange
        var fsm = new PanelScanFsmSimulator();
        fsm.SetPanelDimensions(rows: 2, cols: 2);
        fsm.SetGateTiming(gateOnUs: 1, gateOffUs: 1);
        fsm.SetScanMode(ScanMode.Single);
        fsm.StartScan();

        // Act - Process entire frame
        for (int i = 0; i < 100; i++)
        {
            fsm.ProcessTick();
            if (fsm.CurrentState == FsmState.Idle)
                break;
        }

        // Assert
        fsm.FrameCounter.Should().Be(1);
    }

    [Fact]
    public void SetPanelDimensions_ShouldUpdateDimensions()
    {
        // Arrange
        var fsm = new PanelScanFsmSimulator();

        // Act
        fsm.SetPanelDimensions(rows: 2048, cols: 3072);

        // Assert
        fsm.PanelRows.Should().Be(2048);
        fsm.PanelCols.Should().Be(3072);
    }

    [Fact]
    public void TriggerError_ShouldTransitionToErrorState()
    {
        // Arrange
        var fsm = new PanelScanFsmSimulator();
        fsm.StartScan();

        // Act
        fsm.TriggerError(ErrorFlags.Timeout);

        // Assert
        fsm.CurrentState.Should().Be(FsmState.Error);
        fsm.ErrorFlagsValue.Should().Be(ErrorFlags.Timeout);
    }

    [Fact]
    public void ClearError_ShouldReturnToIdleState()
    {
        // Arrange
        var fsm = new PanelScanFsmSimulator();
        fsm.StartScan();
        fsm.TriggerError(ErrorFlags.Timeout);

        // Act
        fsm.ClearError();

        // Assert
        fsm.CurrentState.Should().Be(FsmState.Idle);
        fsm.ErrorFlagsValue.Should().Be(ErrorFlags.None);
    }

    [Fact]
    public void Status_ShouldReflectCurrentState()
    {
        // Arrange
        var fsm = new PanelScanFsmSimulator();

        // Act
        var status = fsm.GetStatus();

        // Assert
        status.IsIdle.Should().BeTrue();
        status.IsBusy.Should().BeFalse();
        status.HasError.Should().BeFalse();
    }

    [Fact]
    public void ContinuousMode_ShouldNotReturnToIdleAfterFrameComplete()
    {
        // Arrange
        var fsm = new PanelScanFsmSimulator();
        fsm.SetPanelDimensions(rows: 2, cols: 2);
        fsm.SetGateTiming(gateOnUs: 1, gateOffUs: 1);
        fsm.SetScanMode(ScanMode.Continuous);
        fsm.StartScan();

        // Act - Process first complete frame
        for (int i = 0; i < 100; i++)
        {
            fsm.ProcessTick();
            if (fsm.FrameCounter == 1)
                break;
        }

        // Assert - Should continue to next frame (Integrate state), not Idle
        fsm.CurrentState.Should().Be(FsmState.Integrate);
        fsm.FrameCounter.Should().Be(1);
    }

    // ---------------------------------------------------------------
    // Phase 2: Control signal output tests
    // ---------------------------------------------------------------

    [Fact]
    public void GateOn_ShouldBeActiveDuringIntegrateState()
    {
        // Arrange
        var fsm = new PanelScanFsmSimulator();

        // Act
        fsm.StartScan();

        // Assert
        fsm.GateOn.Should().BeTrue("GateOn must be active during INTEGRATE");
    }

    [Fact]
    public void GateOn_ShouldBeInactiveDuringReadoutState()
    {
        // Arrange
        var fsm = new PanelScanFsmSimulator();
        fsm.SetGateTiming(gateOnUs: 1);
        fsm.StartScan();

        // Act - transition to Readout
        for (int i = 0; i < 2; i++)
            fsm.ProcessTick();

        // Assert
        fsm.CurrentState.Should().Be(FsmState.Readout);
        fsm.GateOn.Should().BeFalse("GateOn must be inactive outside INTEGRATE");
    }

    [Fact]
    public void GateOn_ShouldBeInactiveInIdleState()
    {
        var fsm = new PanelScanFsmSimulator();
        fsm.GateOn.Should().BeFalse("GateOn must be inactive in IDLE");
    }

    [Fact]
    public void RoicSync_ShouldPulseOnIdleToIntegrateTransition()
    {
        // Arrange
        var fsm = new PanelScanFsmSimulator();

        // Act
        fsm.StartScan();

        // Assert - RoicSync is one-shot pulse on IDLE -> INTEGRATE
        fsm.RoicSync.Should().BeTrue("RoicSync should pulse on IDLE -> INTEGRATE transition");
    }

    [Fact]
    public void RoicSync_ShouldDeassertAfterFirstProcessTick()
    {
        // Arrange
        var fsm = new PanelScanFsmSimulator();
        fsm.SetGateTiming(gateOnUs: 10); // Long enough to stay in INTEGRATE
        fsm.StartScan();

        // RoicSync is true right after StartScan
        fsm.RoicSync.Should().BeTrue();

        // Act - first ProcessTick: previous state becomes INTEGRATE, current stays INTEGRATE
        fsm.ProcessTick();

        // Assert - no longer IDLE -> INTEGRATE transition
        fsm.RoicSync.Should().BeFalse("RoicSync is one-shot, should deassert after transition");
    }

    [Fact]
    public void LineValid_ShouldBeActiveDuringLineDoneState()
    {
        // Arrange
        var fsm = new PanelScanFsmSimulator();
        fsm.SetPanelDimensions(rows: 2, cols: 2);
        fsm.SetGateTiming(gateOnUs: 1, gateOffUs: 1);
        fsm.SetTimerParameters(settleTicks: 1, adcTicks: 1);
        fsm.StartScan();

        // Act - advance to LineDone state
        bool reachedLineDone = false;
        for (int i = 0; i < 50; i++)
        {
            fsm.ProcessTick();
            if (fsm.CurrentState == FsmState.LineDone)
            {
                reachedLineDone = true;
                break;
            }
        }

        // Assert
        reachedLineDone.Should().BeTrue("FSM should reach LineDone state");
        fsm.LineValid.Should().BeTrue("LineValid should be active during LineDone");
    }

    [Fact]
    public void FrameValid_ShouldBeActiveDuringFrameDoneState()
    {
        // Arrange
        var fsm = new PanelScanFsmSimulator();
        fsm.SetPanelDimensions(rows: 1, cols: 2);
        fsm.SetGateTiming(gateOnUs: 1, gateOffUs: 1);
        fsm.SetTimerParameters(settleTicks: 1, adcTicks: 1);
        fsm.SetScanMode(ScanMode.Single);
        fsm.StartScan();

        // Act - advance to FrameDone state
        bool reachedFrameDone = false;
        for (int i = 0; i < 100; i++)
        {
            fsm.ProcessTick();
            if (fsm.CurrentState == FsmState.FrameDone)
            {
                reachedFrameDone = true;
                break;
            }
        }

        // Assert
        reachedFrameDone.Should().BeTrue("FSM should reach FrameDone state");
        fsm.FrameValid.Should().BeTrue("FrameValid should be active during FrameDone");
    }

    [Fact]
    public void OutputSignals_InIdleState_ShouldAllBeInactive()
    {
        // Arrange
        var fsm = new PanelScanFsmSimulator();

        // Assert
        fsm.GateOn.Should().BeFalse();
        fsm.RoicSync.Should().BeFalse();
        fsm.LineValid.Should().BeFalse();
        fsm.FrameValid.Should().BeFalse();
    }

    [Fact]
    public void OutputSignals_InErrorState_ShouldAllBeInactive()
    {
        // Arrange
        var fsm = new PanelScanFsmSimulator();
        fsm.StartScan();

        // Act
        fsm.TriggerError(ErrorFlags.Timeout);

        // Assert
        fsm.GateOn.Should().BeFalse();
        fsm.LineValid.Should().BeFalse();
        fsm.FrameValid.Should().BeFalse();
    }

    // ---------------------------------------------------------------
    // Phase 2: Separate timer tests
    // ---------------------------------------------------------------

    [Fact]
    public void SetTimerParameters_ShouldConfigureTimers()
    {
        // Arrange
        var fsm = new PanelScanFsmSimulator();

        // Act
        fsm.SetTimerParameters(settleTicks: 20, adcTicks: 15);

        // Assert - timers are set (indirect verification through state machine behavior)
        // The settle timer should load on entering READOUT state
        fsm.SetGateTiming(gateOnUs: 1);
        fsm.StartScan();

        // ProcessTick 1: tick increments to 1, 1 >= gateOnTicks(1), transition to READOUT
        //   settleTimer loaded to 20
        fsm.ProcessTick();
        fsm.CurrentState.Should().Be(FsmState.Readout);
        fsm.SettleTimer.Should().Be(20, "settle timer should be loaded on entering READOUT");
    }

    [Fact]
    public void LineWriteAddress_ShouldIncrementDuringReadout()
    {
        // Arrange
        var fsm = new PanelScanFsmSimulator();
        fsm.SetPanelDimensions(rows: 2, cols: 10);
        fsm.SetGateTiming(gateOnUs: 1, gateOffUs: 50);
        fsm.SetTimerParameters(settleTicks: 1, adcTicks: 1);
        fsm.StartScan();

        // Advance to Readout
        for (int i = 0; i < 2; i++)
            fsm.ProcessTick();

        fsm.CurrentState.Should().Be(FsmState.Readout);

        // Act - process ticks during readout (after settle + adc timers expire)
        // settle: 1 tick, adc: 0 ticks initially
        for (int i = 0; i < 5; i++)
            fsm.ProcessTick();

        // Assert - LineWriteAddress should have incremented
        fsm.LineWriteAddress.Should().BeGreaterThan(0,
            "LineWriteAddress should increment during READOUT after timers expire");
    }

    [Fact]
    public void ActiveBank_ShouldToggleOnLineDone()
    {
        // Arrange
        var fsm = new PanelScanFsmSimulator();
        fsm.SetPanelDimensions(rows: 2, cols: 2);
        fsm.SetGateTiming(gateOnUs: 1, gateOffUs: 1);
        fsm.SetTimerParameters(settleTicks: 1, adcTicks: 1);
        fsm.StartScan();

        int initialBank = fsm.ActiveBank;

        // Act - advance until bank changes
        bool bankChanged = false;
        for (int i = 0; i < 50; i++)
        {
            fsm.ProcessTick();
            if (fsm.ActiveBank != initialBank)
            {
                bankChanged = true;
                break;
            }
        }

        // Assert
        bankChanged.Should().BeTrue("ActiveBank should toggle on LineDone");
        fsm.ActiveBank.Should().Be(1 - initialBank);
    }
}
