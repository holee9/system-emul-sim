using Xunit;
using FluentAssertions;
using IntegrationTests.Helpers;
using FpgaSimulator.Core.Spi;
using McuSimulator.Core.Spi;
using Common.Dto.Dtos;

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
        // Arrange
        byte registerAddress = 0x10; // EXPOSURE_TIME register
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
        // Arrange - Define 10 different register values (per AC-INTEG-003)
        var testRegisters = new (byte Address, ushort Value)[]
        {
            (0x10, 100),  // EXPOSURE_TIME: 100us
            (0x11, 50),   // GAIN: 50
            (0x12, 100),  // OFFSET: 100
            (0x13, 1024), // ROWS: 1024
            (0x14, 1024), // COLS: 1024
            (0x15, 14),   // BIT_DEPTH: 14
            (0x16, 30),   // FRAME_RATE: 30 fps
            (0x17, 1),    // SCAN_MODE: Continuous
            (0x18, 0),    // NOISE_ENABLE: Off
            (0x19, 0)     // DEFECT_RATE: 0%
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
        // Arrange & Act - Perform multiple SPI operations
        for (int i = 0; i < 10; i++)
        {
            byte address = (byte)(0x20 + i);
            ushort value = (ushort)(i * 100);
            _spiMaster.WriteRegister(address, value);
            _ = _spiMaster.ReadRegister(address);
        }

        // Assert - SPI error count should be zero
        var status = _fpgaSlave.GetStatus();
        status.ErrorCount.Should().Be(0, "SPI operations should complete without errors");
    }

    [Fact]
    public void SpiRegisterRead_ShallReturnDefaultValue_UnwrittenRegister()
    {
        // Arrange - Use a register that hasn't been written
        byte unwrittenAddress = 0xFF;

        // Act
        ushort value = _spiMaster.ReadRegister(unwrittenAddress);

        // Assert - Should return default value (0 for FPGA registers)
        value.Should().Be(0, "Unwritten registers should return default value");
    }

    [Fact]
    public void SpiRegisterWrite_ShallOverwritePreviousValue_LastWriteWins()
    {
        // Arrange
        byte address = 0x10;
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
        // Cleanup
        _fpgaSlave?.Dispose();
    }
}
