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
}
