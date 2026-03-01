using Xunit;
using FluentAssertions;
using FpgaSimulator.Core.Protection;

namespace IntegrationTests.Integration;

/// <summary>
/// IT-16: FPGA ProtectionLogicSimulator Safety Shutdown Tests.
/// Validates watchdog timeout, readout timeout, safety shutdown sequence,
/// error flag latching, and heartbeat reset behavior.
/// Reference: SPEC-EMUL-001, fpga-design.md Section 7
/// </summary>
public class IT16_ProtectionLogicShutdownTests
{
    [Fact]
    public void WatchdogTimeout_TriggersShutdown()
    {
        // Arrange - Create protection logic with very short watchdog timeout
        // WatchdogTimeoutMs = 0.001 ms -> 0.001 * 100_000 = 100 ticks
        var config = new ProtectionConfig(
            WatchdogTimeoutMs: 0.001,
            WatchdogEnabled: true,
            ReadoutTimeoutEnabled: false,
            ShutdownResponseClocks: 10);

        var protection = new ProtectionLogicSimulator(config);

        // Act - Tick past the watchdog timeout without resetting
        long watchdogLimit = (long)(0.001 * 100_000); // 100 ticks
        for (int i = 0; i < watchdogLimit + 5; i++)
        {
            protection.ProcessTick();
        }

        // Assert - WatchdogTimeout error should be latched
        protection.ErrorFlags.Should().HaveFlag(ProtectionError.WatchdogTimeout,
            "watchdog timeout error should be latched after timeout");

        // Tick through the shutdown sequence (ShutdownResponseClocks = 10)
        for (int i = 0; i < 10; i++)
        {
            protection.ProcessTick();
        }

        // Verify safety outputs are asserted after shutdown
        protection.GateSafe.Should().BeTrue("gate should be forced to safe state");
        protection.Csi2Disable.Should().BeTrue("CSI-2 TX should be disabled");
        protection.BufferDisable.Should().BeTrue("line buffer should be disabled");
    }

    [Fact]
    public void ReadoutTimeout_NonFatal_NoShutdown()
    {
        // Arrange - Create protection logic with short readout timeout
        // ReadoutTimeoutUs = 0.01 us -> 0.01 * 100 = 1 tick
        var config = new ProtectionConfig(
            WatchdogEnabled: false,
            ReadoutTimeoutEnabled: true,
            ReadoutTimeoutUs: 0.01,
            ShutdownResponseClocks: 10);

        var protection = new ProtectionLogicSimulator(config);

        // Act - Begin readout and tick past timeout
        protection.BeginReadout();
        long readoutLimit = (long)(0.01 * 100); // 1 tick
        for (int i = 0; i < readoutLimit + 5; i++)
        {
            protection.ProcessTick();
        }

        // Assert - ReadoutTimeout should be latched but no shutdown
        protection.ErrorFlags.Should().HaveFlag(ProtectionError.ReadoutTimeout,
            "readout timeout error should be latched");
        protection.IsShutdownInProgress.Should().BeFalse(
            "readout timeout is non-fatal and should not trigger shutdown");
        protection.GateSafe.Should().BeFalse(
            "gate should NOT be forced safe for non-fatal error");
        protection.Csi2Disable.Should().BeFalse(
            "CSI-2 should NOT be disabled for non-fatal error");
        protection.BufferDisable.Should().BeFalse(
            "buffer should NOT be disabled for non-fatal error");
    }

    [Fact]
    public void SafeShutdown_CompletesWithinClockLimit()
    {
        // Arrange - Create protection logic with specific shutdown clock count
        var config = new ProtectionConfig(
            WatchdogEnabled: false,
            ReadoutTimeoutEnabled: false,
            ShutdownResponseClocks: 10);

        var protection = new ProtectionLogicSimulator(config);

        // Act - Report a fatal error to trigger shutdown
        protection.ReportError(ProtectionError.RoicFault, isFatal: true);
        protection.IsShutdownInProgress.Should().BeTrue(
            "fatal error should initiate shutdown sequence");

        // Tick through the shutdown sequence
        for (int tick = 1; tick <= 10; tick++)
        {
            protection.ProcessTick();
        }

        // Assert - All safety outputs should be asserted within 10 clocks
        protection.GateSafe.Should().BeTrue(
            "gate should be safe after shutdown sequence completes");
        protection.Csi2Disable.Should().BeTrue(
            "CSI-2 should be disabled after shutdown sequence completes");
        protection.BufferDisable.Should().BeTrue(
            "buffer should be disabled after shutdown sequence completes");

        // Shutdown should be complete (no longer in progress)
        protection.IsShutdownInProgress.Should().BeFalse(
            "shutdown should be complete after ShutdownResponseClocks ticks");
    }

    [Fact]
    public void ErrorClear_ResetsAllFlags()
    {
        // Arrange - Trigger multiple errors
        var config = new ProtectionConfig(
            WatchdogEnabled: false,
            ReadoutTimeoutEnabled: false,
            ShutdownResponseClocks: 5);

        var protection = new ProtectionLogicSimulator(config);

        // Report fatal error to trigger shutdown
        protection.ReportError(ProtectionError.WatchdogTimeout, isFatal: true);

        // Tick through shutdown to assert all outputs
        for (int i = 0; i < 5; i++)
        {
            protection.ProcessTick();
        }

        // Also latch a non-fatal error
        protection.ReportError(ProtectionError.ReadoutTimeout, isFatal: false);

        // Verify errors and outputs are asserted
        protection.ErrorFlags.Should().HaveFlag(ProtectionError.WatchdogTimeout);
        protection.ErrorFlags.Should().HaveFlag(ProtectionError.ReadoutTimeout);
        protection.GateSafe.Should().BeTrue();

        // Act - Clear all errors
        protection.ClearErrors();

        // Assert - All flags and outputs should be reset
        protection.ErrorFlags.Should().Be(ProtectionError.None,
            "all error flags should be cleared");
        protection.GateSafe.Should().BeFalse(
            "gate safe should be deasserted after clear");
        protection.Csi2Disable.Should().BeFalse(
            "CSI-2 disable should be deasserted after clear");
        protection.BufferDisable.Should().BeFalse(
            "buffer disable should be deasserted after clear");
        protection.IsShutdownInProgress.Should().BeFalse(
            "shutdown should no longer be in progress after clear");
    }

    [Fact]
    public void HeartbeatReset_PreventsWatchdogTimeout()
    {
        // Arrange - Create protection logic with watchdog timeout
        // WatchdogTimeoutMs = 0.001 ms -> 100 ticks
        var config = new ProtectionConfig(
            WatchdogTimeoutMs: 0.001,
            WatchdogEnabled: true,
            ReadoutTimeoutEnabled: false,
            ShutdownResponseClocks: 10);

        var protection = new ProtectionLogicSimulator(config);
        long watchdogLimit = (long)(0.001 * 100_000); // 100 ticks

        // Act - Tick partway (half of timeout), then reset watchdog
        for (int i = 0; i < watchdogLimit / 2; i++)
        {
            protection.ProcessTick();
        }

        // Reset watchdog (heartbeat)
        protection.ResetWatchdog();
        protection.WatchdogCounter.Should().Be(0,
            "watchdog counter should be reset to 0");

        // Continue ticking for another half period
        for (int i = 0; i < watchdogLimit / 2; i++)
        {
            protection.ProcessTick();
        }

        // Assert - No timeout should have occurred
        protection.ErrorFlags.Should().NotHaveFlag(ProtectionError.WatchdogTimeout,
            "watchdog should not timeout when heartbeat resets the counter");
        protection.GateSafe.Should().BeFalse(
            "no shutdown should have been triggered");
    }
}
