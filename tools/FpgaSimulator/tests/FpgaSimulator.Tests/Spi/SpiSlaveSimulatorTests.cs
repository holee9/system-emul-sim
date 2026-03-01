namespace FpgaSimulator.Tests.Spi;

using FluentAssertions;
using FpgaSimulator.Core.Fsm;
using FpgaSimulator.Core.Spi;
using Xunit;

public class SpiSlaveSimulatorTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var spi = new SpiSlaveSimulator();

        // Assert
        spi.DeviceId.Should().Be(0xA735);
        spi.GetRegisterValue(0x00).Should().Be(0xA735);
    }

    [Fact]
    public void WriteRegister_WhenValidAddress_ShouldUpdateValue()
    {
        // Arrange
        var spi = new SpiSlaveSimulator();

        // Act - Use PANEL_ROWS instead of CONTROL (CONTROL has special bit handling)
        spi.WriteRegister(0x50, 2048);

        // Assert
        spi.GetRegisterValue(0x50).Should().Be(2048);
    }

    [Fact]
    public void WriteRegister_ReadOnlyRegister_ShouldNotUpdate()
    {
        // Arrange
        var spi = new SpiSlaveSimulator();
        var originalValue = spi.GetRegisterValue(0x00); // DEVICE_ID is read-only

        // Act
        spi.WriteRegister(0x00, 0x1234);

        // Assert
        spi.GetRegisterValue(0x00).Should().Be(originalValue);
    }

    [Fact]
    public void ReadRegister_StatusRegister_ShouldReturnCorrectFlags()
    {
        // Arrange
        var spi = new SpiSlaveSimulator();

        // Act
        var status = spi.GetRegisterValue(0x20); // STATUS register

        // Assert
        status.Should().Be(0x01); // Idle flag should be set
    }

    [Fact]
    public void WriteControl_StartScan_ShouldSetBusyFlag()
    {
        // Arrange
        var spi = new SpiSlaveSimulator();

        // Act
        spi.WriteRegister(0x21, 0x01); // Set start_scan bit

        // Assert
        var status = spi.GetRegisterValue(0x20);
        (status & 0x02).Should().Be(0x02); // Busy flag should be set
    }

    [Fact]
    public void WriteControl_StopScan_ShouldSetIdleFlag()
    {
        // Arrange
        var spi = new SpiSlaveSimulator();
        spi.WriteRegister(0x21, 0x01); // Start scan

        // Act
        spi.WriteRegister(0x21, 0x02); // Stop scan

        // Assert
        var status = spi.GetRegisterValue(0x20);
        (status & 0x01).Should().Be(0x01); // Idle flag should be set
    }

    [Fact]
    public void FrameCounter_ShouldBeInitiallyZero()
    {
        // Arrange
        var spi = new SpiSlaveSimulator();

        // Act
        var frameCountLo = spi.GetRegisterValue(0x31);
        var frameCountHi = spi.GetRegisterValue(0x30);

        // Assert
        frameCountLo.Should().Be(0);
        frameCountHi.Should().Be(0);
    }

    [Fact]
    public void IncrementFrameCounter_ShouldUpdateRegister()
    {
        // Arrange
        var spi = new SpiSlaveSimulator();

        // Act
        spi.IncrementFrameCounter();
        spi.IncrementFrameCounter();
        spi.IncrementFrameCounter();

        // Assert
        var frameCountLo = spi.GetRegisterValue(0x31);
        frameCountLo.Should().Be(3);
    }

    [Fact]
    public void FrameCounter_Overflow_ShouldWrapCorrectly()
    {
        // Arrange
        var spi = new SpiSlaveSimulator();

        // Act - Set to near max, then increment
        spi.SetFrameCounter(0xFFFFFFFF);
        spi.IncrementFrameCounter();

        // Assert - 32-bit counter wraps to 0
        var frameCountLo = spi.GetRegisterValue(0x31);
        var frameCountHi = spi.GetRegisterValue(0x30);
        frameCountLo.Should().Be(0);
        frameCountHi.Should().Be(0); // Counter wrapped
    }

    [Fact]
    public void ErrorFlags_ShouldBeInitiallyZero()
    {
        // Arrange
        var spi = new SpiSlaveSimulator();

        // Act
        var errorFlags = spi.GetRegisterValue(0x80); // ERROR_FLAGS

        // Assert
        errorFlags.Should().Be(0);
    }

    [Fact]
    public void SetErrorFlag_ShouldUpdateErrorRegister()
    {
        // Arrange
        var spi = new SpiSlaveSimulator();

        // Act
        spi.SetErrorFlag(0x01); // Timeout error

        // Assert
        var errorFlags = spi.GetRegisterValue(0x80);
        errorFlags.Should().Be(0x01);
    }

    [Fact]
    public void ClearErrorFlagViaControl_ShouldClearErrorRegister()
    {
        // Arrange
        var spi = new SpiSlaveSimulator();
        spi.SetErrorFlag(0x01);

        // Act
        spi.WriteRegister(0x21, 0x10); // error_clear bit

        // Assert
        var errorFlags = spi.GetRegisterValue(0x80);
        errorFlags.Should().Be(0);
    }

    [Fact]
    public void TimingRegisters_ShouldStoreConfiguredValues()
    {
        // Arrange
        var spi = new SpiSlaveSimulator();

        // Act
        spi.WriteRegister(0x40, 1000); // TIMING_ROW_PERIOD
        spi.WriteRegister(0x41, 500);  // TIMING_GATE_ON

        // Assert
        spi.GetRegisterValue(0x40).Should().Be(1000);
        spi.GetRegisterValue(0x41).Should().Be(500);
    }

    [Fact]
    public void PanelConfigRegisters_ShouldStoreDimensions()
    {
        // Arrange
        var spi = new SpiSlaveSimulator();

        // Act
        spi.WriteRegister(0x50, 2048); // PANEL_ROWS
        spi.WriteRegister(0x51, 3072); // PANEL_COLS

        // Assert
        spi.GetRegisterValue(0x50).Should().Be(2048);
        spi.GetRegisterValue(0x51).Should().Be(3072);
    }

    [Fact]
    public void Csi2ConfigRegisters_ShouldStoreSettings()
    {
        // Arrange
        var spi = new SpiSlaveSimulator();

        // Act
        spi.WriteRegister(0x60, 0x05); // 4 lanes + tx_enable

        // Assert
        spi.GetRegisterValue(0x60).Should().Be(0x05);
    }

    [Fact]
    public void Reset_ShouldRestoreInitialValues()
    {
        // Arrange
        var spi = new SpiSlaveSimulator();
        spi.WriteRegister(0x21, 0xFF);
        spi.WriteRegister(0x50, 2048);
        spi.SetErrorFlag(0x01);

        // Act
        spi.Reset();

        // Assert
        spi.GetRegisterValue(0x21).Should().Be(0); // CONTROL cleared
        spi.GetRegisterValue(0x50).Should().Be(1024); // PANEL_ROWS reset to default
        spi.GetRegisterValue(0x80).Should().Be(0); // ERROR_FLAGS cleared
    }

    [Fact]
    public void DeviceId_IsAlwaysReadOnly()
    {
        // Arrange
        var spi = new SpiSlaveSimulator();

        // Act
        spi.WriteRegister(0x00, 0xFFFF);

        // Assert
        spi.GetRegisterValue(0x00).Should().Be(0xA735);
    }

    [Fact]
    public void InvalidAddress_ShouldReturnZero()
    {
        // Arrange
        var spi = new SpiSlaveSimulator();

        // Act
        var value = spi.GetRegisterValue(0xFFFE); // Invalid address

        // Assert
        value.Should().Be(0);
    }

    [Fact]
    public void GetAllRegisters_ShouldReturnCompleteMap()
    {
        // Arrange
        var spi = new SpiSlaveSimulator();

        // Act
        var registers = spi.GetAllRegisters();

        // Assert
        registers.Should().ContainKey(0x00); // DEVICE_ID
        registers.Should().ContainKey(0x20); // STATUS
        registers.Should().ContainKey(0x21); // CONTROL
        registers.Count.Should().BeGreaterThan(10);
    }

    // ---------------------------------------------------------------
    // Phase 2: UpdateStatusFromFsm tests
    // ---------------------------------------------------------------

    [Fact]
    public void UpdateStatusFromFsm_IdleState_ShouldSetIdleBit()
    {
        // Arrange
        var spi = new SpiSlaveSimulator();
        var status = new FsmStatus
        {
            State = FsmState.Idle,
            FrameCounter = 0,
            LineCounter = 0,
            ErrorFlags = ErrorFlags.None,
            ScanMode = ScanMode.Single,
            ActiveBank = 0
        };

        // Act
        spi.UpdateStatusFromFsm(status);

        // Assert
        var reg = spi.GetRegisterValue(SpiRegisterAddresses.STATUS);
        (reg & 0x0001).Should().Be(0x0001, "idle bit [0] should be set");
        (reg & 0x0002).Should().Be(0x0000, "busy bit [1] should be clear");
        (reg & 0x0004).Should().Be(0x0000, "error bit [2] should be clear");
    }

    [Fact]
    public void UpdateStatusFromFsm_IntegrateState_ShouldSetBusyBit()
    {
        // Arrange
        var spi = new SpiSlaveSimulator();
        var status = new FsmStatus
        {
            State = FsmState.Integrate,
            FrameCounter = 5,
            LineCounter = 0,
            ErrorFlags = ErrorFlags.None,
            ScanMode = ScanMode.Continuous,
            ActiveBank = 0
        };

        // Act
        spi.UpdateStatusFromFsm(status);

        // Assert
        var reg = spi.GetRegisterValue(SpiRegisterAddresses.STATUS);
        (reg & 0x0002).Should().Be(0x0002, "busy bit [1] should be set for Integrate");
        (reg & 0x0001).Should().Be(0x0000, "idle bit [0] should be clear");
    }

    [Fact]
    public void UpdateStatusFromFsm_ErrorState_ShouldSetErrorBit()
    {
        // Arrange
        var spi = new SpiSlaveSimulator();
        var status = new FsmStatus
        {
            State = FsmState.Error,
            FrameCounter = 10,
            LineCounter = 5,
            ErrorFlags = ErrorFlags.Timeout,
            ScanMode = ScanMode.Single,
            ActiveBank = 1
        };

        // Act
        spi.UpdateStatusFromFsm(status);

        // Assert
        var reg = spi.GetRegisterValue(SpiRegisterAddresses.STATUS);
        (reg & 0x0004).Should().Be(0x0004, "error bit [2] should be set");
    }

    [Fact]
    public void UpdateStatusFromFsm_ShouldEncodeFsmStateInBits7to4()
    {
        // Arrange
        var spi = new SpiSlaveSimulator();
        var status = new FsmStatus
        {
            State = FsmState.Readout, // value = 2
            FrameCounter = 0,
            LineCounter = 0,
            ErrorFlags = ErrorFlags.None,
            ScanMode = ScanMode.Single,
            ActiveBank = 0
        };

        // Act
        spi.UpdateStatusFromFsm(status);

        // Assert
        var reg = spi.GetRegisterValue(SpiRegisterAddresses.STATUS);
        int fsmBits = (reg >> 4) & 0x0F;
        fsmBits.Should().Be((int)FsmState.Readout);
    }

    [Fact]
    public void UpdateStatusFromFsm_ShouldEncodeActiveBankInBit11()
    {
        // Arrange
        var spi = new SpiSlaveSimulator();
        var status = new FsmStatus
        {
            State = FsmState.Readout,
            FrameCounter = 0,
            LineCounter = 0,
            ErrorFlags = ErrorFlags.None,
            ScanMode = ScanMode.Single,
            ActiveBank = 1
        };

        // Act
        spi.UpdateStatusFromFsm(status);

        // Assert
        var reg = spi.GetRegisterValue(SpiRegisterAddresses.STATUS);
        int bankBit = (reg >> 11) & 0x01;
        bankBit.Should().Be(1, "active bank bit [11] should be 1");
    }

    [Fact]
    public void UpdateStatusFromFsm_ShouldEncodeScanModeInBits15to12()
    {
        // Arrange
        var spi = new SpiSlaveSimulator();
        var status = new FsmStatus
        {
            State = FsmState.Integrate,
            FrameCounter = 0,
            LineCounter = 0,
            ErrorFlags = ErrorFlags.None,
            ScanMode = ScanMode.Continuous, // value = 1
            ActiveBank = 0
        };

        // Act
        spi.UpdateStatusFromFsm(status);

        // Assert
        var reg = spi.GetRegisterValue(SpiRegisterAddresses.STATUS);
        int scanModeBits = (reg >> 12) & 0x0F;
        scanModeBits.Should().Be((int)ScanMode.Continuous);
    }

    [Fact]
    public void UpdateStatusFromFsm_ShouldUpdateFrameCounterRegisters()
    {
        // Arrange
        var spi = new SpiSlaveSimulator();
        var status = new FsmStatus
        {
            State = FsmState.Idle,
            FrameCounter = 0x0001_ABCD,
            LineCounter = 0,
            ErrorFlags = ErrorFlags.None,
            ScanMode = ScanMode.Single,
            ActiveBank = 0
        };

        // Act
        spi.UpdateStatusFromFsm(status);

        // Assert
        var lo = spi.GetRegisterValue(SpiRegisterAddresses.FRAME_COUNT_LO);
        var hi = spi.GetRegisterValue(SpiRegisterAddresses.FRAME_COUNT_HI);
        lo.Should().Be(0xABCD);
        hi.Should().Be(0x0001);
    }

    [Fact]
    public void UpdateStatusFromFsm_ShouldUpdateErrorFlagsRegister()
    {
        // Arrange
        var spi = new SpiSlaveSimulator();
        var status = new FsmStatus
        {
            State = FsmState.Error,
            FrameCounter = 0,
            LineCounter = 0,
            ErrorFlags = ErrorFlags.Timeout | ErrorFlags.Overflow,
            ScanMode = ScanMode.Single,
            ActiveBank = 0
        };

        // Act
        spi.UpdateStatusFromFsm(status);

        // Assert
        var errReg = spi.GetRegisterValue(SpiRegisterAddresses.ERROR_FLAGS);
        ((ErrorFlags)errReg).Should().HaveFlag(ErrorFlags.Timeout);
        ((ErrorFlags)errReg).Should().HaveFlag(ErrorFlags.Overflow);
    }

    // ---------------------------------------------------------------
    // Phase 2: CaptureIlaSnapshot tests
    // ---------------------------------------------------------------

    [Fact]
    public void CaptureIlaSnapshot_ShouldStoreValues()
    {
        // Arrange
        var spi = new SpiSlaveSimulator();

        // Act
        spi.CaptureIlaSnapshot(
            fsmState: 0x0005,
            lineCounter: 0x0100,
            errorFlags: 0x00FF,
            extraData: 0xCAFE);

        // Assert
        spi.GetRegisterValue(SpiRegisterAddresses.ILA_CAPTURE_0).Should().Be(0x0005);
        spi.GetRegisterValue(SpiRegisterAddresses.ILA_CAPTURE_1).Should().Be(0x0100);
        spi.GetRegisterValue(SpiRegisterAddresses.ILA_CAPTURE_2).Should().Be(0x00FF);
        spi.GetRegisterValue(SpiRegisterAddresses.ILA_CAPTURE_3).Should().Be(0xCAFE);
    }

    [Fact]
    public void CaptureIlaSnapshot_ShouldBeReadOnly()
    {
        // Arrange
        var spi = new SpiSlaveSimulator();
        spi.CaptureIlaSnapshot(fsmState: 0x1234, lineCounter: 0, errorFlags: 0, extraData: 0);

        // Act - attempt to write to ILA capture registers
        spi.WriteRegister(SpiRegisterAddresses.ILA_CAPTURE_0, 0xFFFF);

        // Assert - value should be unchanged
        spi.GetRegisterValue(SpiRegisterAddresses.ILA_CAPTURE_0).Should().Be(0x1234);
    }

    [Fact]
    public void CaptureIlaSnapshot_ShouldOverwritePreviousCapture()
    {
        // Arrange
        var spi = new SpiSlaveSimulator();
        spi.CaptureIlaSnapshot(fsmState: 0x0001, lineCounter: 0x0001, errorFlags: 0x0001, extraData: 0x0001);

        // Act - capture again with different values
        spi.CaptureIlaSnapshot(fsmState: 0x0002, lineCounter: 0x0002, errorFlags: 0x0002, extraData: 0x0002);

        // Assert - should reflect latest capture
        spi.GetRegisterValue(SpiRegisterAddresses.ILA_CAPTURE_0).Should().Be(0x0002);
        spi.GetRegisterValue(SpiRegisterAddresses.ILA_CAPTURE_1).Should().Be(0x0002);
        spi.GetRegisterValue(SpiRegisterAddresses.ILA_CAPTURE_2).Should().Be(0x0002);
        spi.GetRegisterValue(SpiRegisterAddresses.ILA_CAPTURE_3).Should().Be(0x0002);
    }

    [Fact]
    public void CaptureIlaSnapshot_ShouldAlsoUpdateGetAllRegisters()
    {
        // Arrange
        var spi = new SpiSlaveSimulator();

        // Act
        spi.CaptureIlaSnapshot(fsmState: 0xAAAA, lineCounter: 0xBBBB, errorFlags: 0xCCCC, extraData: 0xDDDD);
        var allRegs = spi.GetAllRegisters();

        // Assert
        allRegs[SpiRegisterAddresses.ILA_CAPTURE_0].Should().Be(0xAAAA);
        allRegs[SpiRegisterAddresses.ILA_CAPTURE_1].Should().Be(0xBBBB);
        allRegs[SpiRegisterAddresses.ILA_CAPTURE_2].Should().Be(0xCCCC);
        allRegs[SpiRegisterAddresses.ILA_CAPTURE_3].Should().Be(0xDDDD);
    }

    [Fact]
    public void Reset_ShouldClearIlaCaptures()
    {
        // Arrange
        var spi = new SpiSlaveSimulator();
        spi.CaptureIlaSnapshot(fsmState: 0x1234, lineCounter: 0x5678, errorFlags: 0x9ABC, extraData: 0xDEF0);

        // Act
        spi.Reset();

        // Assert
        spi.GetRegisterValue(SpiRegisterAddresses.ILA_CAPTURE_0).Should().Be(0);
        spi.GetRegisterValue(SpiRegisterAddresses.ILA_CAPTURE_1).Should().Be(0);
        spi.GetRegisterValue(SpiRegisterAddresses.ILA_CAPTURE_2).Should().Be(0);
        spi.GetRegisterValue(SpiRegisterAddresses.ILA_CAPTURE_3).Should().Be(0);
    }
}
