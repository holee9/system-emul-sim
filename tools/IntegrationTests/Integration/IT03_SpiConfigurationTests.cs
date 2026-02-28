using Xunit;
using FluentAssertions;
using IntegrationTests.Helpers;
using FpgaSimulator.Core.Spi;
using McuSimulator.Core.Spi;
using Common.Dto.Dtos;

// Alias for register addresses
using SpiRegisterAddresses = FpgaSimulator.Core.Spi.SpiRegisterAddresses;

namespace IntegrationTests.Integration;

/// <summary>
/// IT-03: SPI Configuration Update test.
/// Validates FPGA register write/read via SPI interface.
/// Reference: SPEC-INTEG-001 AC-INTEG-003
/// </summary>
public class IT03_SpiConfigurationTests : IDisposable
{
    private readonly SpiSlaveSimulator _fpgaSlave;
    private readonly SpiMasterSimulator _spiMaster;

    public IT03_SpiConfigurationTests()
    {
        _fpgaSlave = new SpiSlaveSimulator();
        _spiMaster = new SpiMasterSimulator(_fpgaSlave);
    }

    [Fact]
    public void ConfigureAsync_ShallWriteAndReadRegister_CorrectValue()
    {
        // Arrange - Use TIMING_GATE_ON register (0x41) which is writable
        byte registerAddress = SpiRegisterAddresses.TIMING_GATE_ON;
        ushort expectedValue = 100; // 100 microseconds

        // Act - Write register via SPI
        _spiMaster.WriteRegister(registerAddress, expectedValue);

        // Assert - Read back and verify
        ushort actualValue = _spiMaster.ReadRegister(registerAddress);
        actualValue.Should().Be(expectedValue, "SPI register read should return written value");
    }

    [Fact]
    public void ConfigureAsync_ShallWriteMultipleRegisters_AllValuesPersisted()
    {
        // Arrange - Define 10 different register values using valid writable addresses (per AC-INTEG-003)
        var testRegisters = new (byte Address, ushort Value)[]
        {
            (SpiRegisterAddresses.TIMING_GATE_ON, 100),      // Gate ON timing: 100us
            (SpiRegisterAddresses.TIMING_GATE_OFF, 50),      // Gate OFF timing: 50us
            (SpiRegisterAddresses.TIMING_ROW_PERIOD, 16),    // Row period
            (SpiRegisterAddresses.ROIC_SETTLE_US, 10),       // ROIC settle time
            (SpiRegisterAddresses.ADC_CONV_US, 5),           // ADC conversion time
            (SpiRegisterAddresses.PANEL_ROWS, 1024),         // Rows: 1024
            (SpiRegisterAddresses.PANEL_COLS, 1024),         // Cols: 1024
            (SpiRegisterAddresses.BIT_DEPTH, 14),            // Bit depth: 14
            (SpiRegisterAddresses.CSI2_LANE_COUNT, 4),       // Lane count: 4
            (SpiRegisterAddresses.CSI2_LANE_SPEED, 100)      // Lane speed: 1.0 Gbps
        };

        // Act - Write all registers
        foreach (var (address, value) in testRegisters)
        {
            _spiMaster.WriteRegister(address, value);
        }

        // Assert - Verify all values persisted
        foreach (var (address, expectedValue) in testRegisters)
        {
            ushort actualValue = _spiMaster.ReadRegister(address);
            actualValue.Should().Be(expectedValue,
                $"Register 0x{address:X2} should contain written value");
        }
    }

    [Fact]
    public void SpiOperations_ShallMaintainZeroErrorCount_NoErrors()
    {
        // Arrange & Act - Perform multiple SPI operations using writable registers
        for (int i = 0; i < 10; i++)
        {
            // Use timing registers (0x40-0x49) which are writable
            byte address = (byte)(SpiRegisterAddresses.TIMING_ROW_PERIOD + i);
            ushort value = (ushort)(i * 100 + 10);
            _spiMaster.WriteRegister(address, value);
            _ = _spiMaster.ReadRegister(address);
        }

        // Assert - SPI error count should be zero (check ERROR_FLAGS register)
        var errorFlags = _fpgaSlave.GetRegisterValue(SpiRegisterAddresses.ERROR_FLAGS);
        errorFlags.Should().Be(0, "SPI operations should complete without errors");
    }

    [Fact]
    public void SpiRegisterRead_ShallReturnDefaultValue_UnwrittenRegister()
    {
        // Arrange - Use a register that hasn't been written (within valid range but not initialized)
        byte unwrittenAddress = 0x46; // Unused timing register slot

        // Act
        ushort value = _spiMaster.ReadRegister(unwrittenAddress);

        // Assert - Should return default value (0 for FPGA registers)
        value.Should().Be(0, "Unwritten registers should return default value");
    }

    [Fact]
    public void SpiRegisterWrite_ShallOverwritePreviousValue_LastWriteWins()
    {
        // Arrange - Use writable timing register
        byte address = SpiRegisterAddresses.TIMING_GATE_ON;
        ushort firstValue = 100;
        ushort secondValue = 200;

        // Act - Write twice
        _spiMaster.WriteRegister(address, firstValue);
        _spiMaster.WriteRegister(address, secondValue);

        // Assert - Second write should win
        ushort actualValue = _spiMaster.ReadRegister(address);
        actualValue.Should().Be(secondValue, "Second write should overwrite first value");
    }

    public void Dispose()
    {
        // Cleanup - SpiSlaveSimulator does not implement IDisposable
        // No explicit cleanup needed
    }
}
