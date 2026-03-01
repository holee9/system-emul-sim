namespace FpgaSimulator.Tests.Protection;

using FluentAssertions;
using FpgaSimulator.Core.Fsm;
using FpgaSimulator.Core.Protection;
using Xunit;

public class ProtectionLogicTests
{
    // ---------------------------------------------------------------
    // Constructor and initial state tests
    // ---------------------------------------------------------------

    [Fact]
    public void Constructor_Default_ShouldHaveNoErrors()
    {
        // Arrange & Act
        var protection = new ProtectionLogicSimulator();

        // Assert
        protection.ErrorFlags.Should().Be(ProtectionError.None);
        protection.GateSafe.Should().BeFalse();
        protection.Csi2Disable.Should().BeFalse();
        protection.BufferDisable.Should().BeFalse();
        protection.IsShutdownInProgress.Should().BeFalse();
        protection.WatchdogCounter.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithCustomConfig_ShouldApplyConfig()
    {
        // Arrange & Act
        var config = new ProtectionConfig(
            WatchdogTimeoutMs: 50.0,
            ReadoutTimeoutUs: 200.0,
            ShutdownResponseClocks: 5);
        var protection = new ProtectionLogicSimulator(config);

        // Assert - should initialize without error
        protection.ErrorFlags.Should().Be(ProtectionError.None);
    }

    // ---------------------------------------------------------------
    // Watchdog timeout tests
    // ---------------------------------------------------------------

    [Fact]
    public void WatchdogTimeout_ShouldTriggerFatalError()
    {
        // Arrange - very short watchdog for test: 0.001 ms = 100 ticks at 100 MHz
        var config = new ProtectionConfig(WatchdogTimeoutMs: 0.001);
        var protection = new ProtectionLogicSimulator(config);

        // Act - process enough ticks to exceed 100 tick limit
        for (int i = 0; i < 101; i++)
        {
            protection.ProcessTick();
        }

        // Assert
        protection.ErrorFlags.Should().HaveFlag(ProtectionError.WatchdogTimeout);
    }

    [Fact]
    public void WatchdogTimeout_ShouldInitiateSafetyShutdown()
    {
        // Arrange
        var config = new ProtectionConfig(WatchdogTimeoutMs: 0.001);
        var protection = new ProtectionLogicSimulator(config);

        // Act - trigger watchdog
        for (int i = 0; i < 101; i++)
        {
            protection.ProcessTick();
        }

        // Continue processing to complete shutdown sequence
        for (int i = 0; i < 10; i++)
        {
            protection.ProcessTick();
        }

        // Assert - all safety outputs should be asserted
        protection.GateSafe.Should().BeTrue("watchdog is fatal, gate must be safe");
        protection.Csi2Disable.Should().BeTrue("CSI-2 must be disabled on fatal error");
        protection.BufferDisable.Should().BeTrue("buffer must be disabled on fatal error");
    }

    [Fact]
    public void WatchdogReset_ShouldPreventTimeout()
    {
        // Arrange
        var config = new ProtectionConfig(WatchdogTimeoutMs: 0.001); // 100 ticks
        var protection = new ProtectionLogicSimulator(config);

        // Act - process 50 ticks, reset, process 50 more ticks
        for (int i = 0; i < 50; i++)
        {
            protection.ProcessTick();
        }
        protection.ResetWatchdog();
        for (int i = 0; i < 50; i++)
        {
            protection.ProcessTick();
        }

        // Assert - should not have triggered because we reset midway
        protection.ErrorFlags.Should().Be(ProtectionError.None);
    }

    [Fact]
    public void WatchdogDisabled_ShouldNeverTimeout()
    {
        // Arrange
        var config = new ProtectionConfig(WatchdogTimeoutMs: 0.001, WatchdogEnabled: false);
        var protection = new ProtectionLogicSimulator(config);

        // Act - process many ticks
        for (int i = 0; i < 200; i++)
        {
            protection.ProcessTick();
        }

        // Assert
        protection.ErrorFlags.Should().Be(ProtectionError.None);
    }

    // ---------------------------------------------------------------
    // Readout timeout tests
    // ---------------------------------------------------------------

    [Fact]
    public void ReadoutTimeout_WhenActive_ShouldTriggerNonFatalError()
    {
        // Arrange - 1.0 us readout timeout = 100 ticks at 100 MHz
        var config = new ProtectionConfig(ReadoutTimeoutUs: 1.0, WatchdogEnabled: false);
        var protection = new ProtectionLogicSimulator(config);

        // Act
        protection.BeginReadout();
        for (int i = 0; i < 101; i++)
        {
            protection.ProcessTick();
        }

        // Assert - readout timeout is non-fatal
        protection.ErrorFlags.Should().HaveFlag(ProtectionError.ReadoutTimeout);
        // Non-fatal should NOT trigger shutdown
        protection.IsShutdownInProgress.Should().BeFalse();
        protection.GateSafe.Should().BeFalse();
    }

    [Fact]
    public void ReadoutTimeout_WhenNotActive_ShouldNotTrigger()
    {
        // Arrange
        var config = new ProtectionConfig(ReadoutTimeoutUs: 1.0, WatchdogEnabled: false);
        var protection = new ProtectionLogicSimulator(config);

        // Act - no BeginReadout called
        for (int i = 0; i < 200; i++)
        {
            protection.ProcessTick();
        }

        // Assert
        protection.ErrorFlags.Should().Be(ProtectionError.None);
    }

    [Fact]
    public void EndReadout_ShouldStopTimeoutCounter()
    {
        // Arrange
        var config = new ProtectionConfig(ReadoutTimeoutUs: 1.0, WatchdogEnabled: false);
        var protection = new ProtectionLogicSimulator(config);

        // Act - begin readout, process some ticks, end readout
        protection.BeginReadout();
        for (int i = 0; i < 50; i++)
        {
            protection.ProcessTick();
        }
        protection.EndReadout();

        // Process many more ticks
        for (int i = 0; i < 200; i++)
        {
            protection.ProcessTick();
        }

        // Assert - should not have timed out because readout was ended
        protection.ErrorFlags.Should().Be(ProtectionError.None);
    }

    [Fact]
    public void ReadoutTimeoutDisabled_ShouldNeverTimeout()
    {
        // Arrange
        var config = new ProtectionConfig(
            ReadoutTimeoutUs: 1.0,
            ReadoutTimeoutEnabled: false,
            WatchdogEnabled: false);
        var protection = new ProtectionLogicSimulator(config);

        // Act
        protection.BeginReadout();
        for (int i = 0; i < 200; i++)
        {
            protection.ProcessTick();
        }

        // Assert
        protection.ErrorFlags.Should().Be(ProtectionError.None);
    }

    // ---------------------------------------------------------------
    // Safety shutdown sequence tests
    // ---------------------------------------------------------------

    [Fact]
    public void SafetyShutdown_ShouldAssertOutputsProgressively()
    {
        // Arrange
        var config = new ProtectionConfig(ShutdownResponseClocks: 10, WatchdogEnabled: false);
        var protection = new ProtectionLogicSimulator(config);

        // Act - trigger fatal error
        protection.ReportError(ProtectionError.RoicFault, isFatal: true);

        // After 1 tick: gate_safe asserted
        protection.ProcessTick();
        protection.GateSafe.Should().BeTrue("gate_safe should be first in shutdown sequence");
        protection.Csi2Disable.Should().BeFalse("csi2_disable not yet");

        // After 2 ticks: csi2_disable asserted
        protection.ProcessTick();
        protection.Csi2Disable.Should().BeTrue("csi2_disable should be second");
        protection.BufferDisable.Should().BeFalse("buffer_disable not yet");

        // After 3 ticks: buffer_disable asserted
        protection.ProcessTick();
        protection.BufferDisable.Should().BeTrue("buffer_disable should be third");
    }

    [Fact]
    public void SafetyShutdown_ShouldCompleteWithinShutdownResponseClocks()
    {
        // Arrange
        var config = new ProtectionConfig(ShutdownResponseClocks: 5, WatchdogEnabled: false);
        var protection = new ProtectionLogicSimulator(config);

        // Act - trigger fatal error and process all shutdown clocks
        protection.ReportError(ProtectionError.ConfigError, isFatal: true);
        for (int i = 0; i < 5; i++)
        {
            protection.ProcessTick();
        }

        // Assert
        protection.IsShutdownInProgress.Should().BeFalse("shutdown should be complete");
        protection.GateSafe.Should().BeTrue();
        protection.Csi2Disable.Should().BeTrue();
        protection.BufferDisable.Should().BeTrue();
    }

    // ---------------------------------------------------------------
    // Error flag latching tests
    // ---------------------------------------------------------------

    [Fact]
    public void ErrorFlags_ShouldLatchUntilExplicitClear()
    {
        // Arrange
        var config = new ProtectionConfig(WatchdogEnabled: false);
        var protection = new ProtectionLogicSimulator(config);

        // Act - inject error
        protection.ReportError(ProtectionError.BufferOverflow, isFatal: false);

        // Process ticks - error should remain latched
        for (int i = 0; i < 100; i++)
        {
            protection.ProcessTick();
        }

        // Assert
        protection.ErrorFlags.Should().HaveFlag(ProtectionError.BufferOverflow);
    }

    [Fact]
    public void ClearErrors_ShouldResetAllFlagsAndOutputs()
    {
        // Arrange
        var config = new ProtectionConfig(WatchdogEnabled: false);
        var protection = new ProtectionLogicSimulator(config);

        // Inject fatal error and run shutdown
        protection.ReportError(ProtectionError.RoicFault, isFatal: true);
        for (int i = 0; i < 10; i++)
        {
            protection.ProcessTick();
        }

        // Act
        protection.ClearErrors();

        // Assert
        protection.ErrorFlags.Should().Be(ProtectionError.None);
        protection.GateSafe.Should().BeFalse();
        protection.Csi2Disable.Should().BeFalse();
        protection.BufferDisable.Should().BeFalse();
        protection.IsShutdownInProgress.Should().BeFalse();
    }

    [Fact]
    public void MultipleErrors_ShouldAccumulateFlags()
    {
        // Arrange
        var config = new ProtectionConfig(WatchdogEnabled: false);
        var protection = new ProtectionLogicSimulator(config);

        // Act - inject multiple non-fatal errors
        protection.ReportError(ProtectionError.BufferOverflow, isFatal: false);
        protection.ReportError(ProtectionError.Csi2Error, isFatal: false);

        // Assert
        protection.ErrorFlags.Should().HaveFlag(ProtectionError.BufferOverflow);
        protection.ErrorFlags.Should().HaveFlag(ProtectionError.Csi2Error);
    }

    // ---------------------------------------------------------------
    // Fatal vs non-fatal classification tests
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(ProtectionError.WatchdogTimeout)]
    [InlineData(ProtectionError.RoicFault)]
    [InlineData(ProtectionError.ConfigError)]
    public void FatalError_ShouldTriggerShutdown(ProtectionError error)
    {
        // Arrange
        var config = new ProtectionConfig(WatchdogEnabled: false);
        var protection = new ProtectionLogicSimulator(config);

        // Act
        protection.ReportError(error, isFatal: true);

        // Assert
        protection.IsShutdownInProgress.Should().BeTrue();
        protection.ErrorFlags.Should().HaveFlag(error);
    }

    [Theory]
    [InlineData(ProtectionError.ReadoutTimeout)]
    [InlineData(ProtectionError.BufferOverflow)]
    [InlineData(ProtectionError.Csi2Error)]
    public void NonFatalError_ShouldNotTriggerShutdown(ProtectionError error)
    {
        // Arrange
        var config = new ProtectionConfig(WatchdogEnabled: false);
        var protection = new ProtectionLogicSimulator(config);

        // Act
        protection.ReportError(error, isFatal: false);

        // Assert
        protection.IsShutdownInProgress.Should().BeFalse();
        protection.ErrorFlags.Should().HaveFlag(error);
    }

    // ---------------------------------------------------------------
    // ReportError external injection tests
    // ---------------------------------------------------------------

    [Fact]
    public void ReportError_ExternalInjection_ShouldLatchFlag()
    {
        // Arrange
        var protection = new ProtectionLogicSimulator(
            new ProtectionConfig(WatchdogEnabled: false));

        // Act
        protection.ReportError(ProtectionError.Csi2Error, isFatal: false);

        // Assert
        protection.ErrorFlags.Should().HaveFlag(ProtectionError.Csi2Error);
    }

    // ---------------------------------------------------------------
    // ToFsmErrorFlags conversion tests
    // ---------------------------------------------------------------

    [Fact]
    public void ToFsmErrorFlags_ShouldConvertCorrectly()
    {
        // Arrange
        var protection = new ProtectionLogicSimulator(
            new ProtectionConfig(WatchdogEnabled: false));

        protection.ReportError(ProtectionError.WatchdogTimeout, isFatal: true);
        protection.ReportError(ProtectionError.ReadoutTimeout, isFatal: false);

        // Act
        var fsmFlags = protection.ToFsmErrorFlags();

        // Assert
        fsmFlags.Should().HaveFlag(ErrorFlags.Watchdog);
        fsmFlags.Should().HaveFlag(ErrorFlags.Timeout);
    }

    [Fact]
    public void ToFsmErrorFlags_NoErrors_ShouldReturnNone()
    {
        // Arrange
        var protection = new ProtectionLogicSimulator();

        // Act
        var fsmFlags = protection.ToFsmErrorFlags();

        // Assert
        fsmFlags.Should().Be(ErrorFlags.None);
    }

    [Fact]
    public void ToFsmErrorFlags_BufferOverflow_ShouldMapToOverflow()
    {
        // Arrange
        var protection = new ProtectionLogicSimulator(
            new ProtectionConfig(WatchdogEnabled: false));
        protection.ReportError(ProtectionError.BufferOverflow, isFatal: false);

        // Act
        var fsmFlags = protection.ToFsmErrorFlags();

        // Assert
        fsmFlags.Should().HaveFlag(ErrorFlags.Overflow);
    }

    [Fact]
    public void ToFsmErrorFlags_Csi2Error_ShouldMapToCrcError()
    {
        // Arrange
        var protection = new ProtectionLogicSimulator(
            new ProtectionConfig(WatchdogEnabled: false));
        protection.ReportError(ProtectionError.Csi2Error, isFatal: false);

        // Act
        var fsmFlags = protection.ToFsmErrorFlags();

        // Assert
        fsmFlags.Should().HaveFlag(ErrorFlags.CrcError);
    }

    // ---------------------------------------------------------------
    // Reset tests
    // ---------------------------------------------------------------

    [Fact]
    public void Reset_ShouldClearAllState()
    {
        // Arrange
        var config = new ProtectionConfig(WatchdogEnabled: false);
        var protection = new ProtectionLogicSimulator(config);

        // Create error state
        protection.ReportError(ProtectionError.RoicFault, isFatal: true);
        for (int i = 0; i < 10; i++)
        {
            protection.ProcessTick();
        }

        // Act
        protection.Reset();

        // Assert
        protection.ErrorFlags.Should().Be(ProtectionError.None);
        protection.GateSafe.Should().BeFalse();
        protection.Csi2Disable.Should().BeFalse();
        protection.BufferDisable.Should().BeFalse();
        protection.IsShutdownInProgress.Should().BeFalse();
        protection.WatchdogCounter.Should().Be(0);
    }

    // ---------------------------------------------------------------
    // ProtectionConfig record tests
    // ---------------------------------------------------------------

    [Fact]
    public void ProtectionConfig_DefaultValues_ShouldMatchFpgaDefaults()
    {
        // Arrange & Act
        var config = new ProtectionConfig();

        // Assert
        config.WatchdogTimeoutMs.Should().Be(100.0);
        config.ReadoutTimeoutUs.Should().Be(100.0);
        config.ShutdownResponseClocks.Should().Be(10);
        config.WatchdogEnabled.Should().BeTrue();
        config.ReadoutTimeoutEnabled.Should().BeTrue();
    }

    [Fact]
    public void ProtectionConfig_Equality_ShouldWorkAsRecord()
    {
        // Arrange
        var config1 = new ProtectionConfig(WatchdogTimeoutMs: 50.0);
        var config2 = new ProtectionConfig(WatchdogTimeoutMs: 50.0);

        // Assert
        config1.Should().Be(config2);
    }
}
