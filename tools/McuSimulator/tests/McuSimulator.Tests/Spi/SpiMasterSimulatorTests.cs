using McuSimulator.Core.Spi;
using Xunit;

namespace McuSimulator.Tests.Spi;

/// <summary>
/// Tests for SpiMasterSimulator.
/// Follows TDD: RED-GREEN-REFACTOR cycle.
/// REQ-SIM-003: McuSimulator shall implement SPI master for FPGA communication.
/// </summary>
public sealed class SpiMasterSimulatorTests
{
    [Fact]
    public void ReadRegister_ShouldReturnCorrectValue_FromFpgaSlave()
    {
        // Arrange
        var fpgaSlave = new FpgaSimulator.Core.Spi.SpiSlaveSimulator();
        var spiMaster = new SpiMasterSimulator(fpgaSlave);
        byte address = 0x20; // STATUS register

        // Act
        ushort value = spiMaster.ReadRegister(address);

        // Assert
        Assert.Equal(0x0001, value); // Default STATUS = 0x0001 (idle)
    }

    [Fact]
    public void WriteRegister_ShouldUpdateWritableRegister_InFpgaSlave()
    {
        // Arrange
        var fpgaSlave = new FpgaSimulator.Core.Spi.SpiSlaveSimulator();
        var spiMaster = new SpiMasterSimulator(fpgaSlave);
        byte address = 0x21; // CONTROL register (writable)
        ushort value = 0x0009; // scan_enable=1, continuous mode

        // Act
        spiMaster.WriteRegister(address, value);

        // Assert
        Assert.Equal(value, fpgaSlave.GetRegisterValue(address));
    }

    [Fact]
    public void WriteRegister_ShouldNotUpdateReadOnlyRegister_InFpgaSlave()
    {
        // Arrange
        var fpgaSlave = new FpgaSimulator.Core.Spi.SpiSlaveSimulator();
        var spiMaster = new SpiMasterSimulator(fpgaSlave);
        byte address = 0x00; // DEVICE_ID (read-only)
        ushort originalValue = fpgaSlave.GetRegisterValue(address);
        ushort newValue = 0xFFFF;

        // Act
        spiMaster.WriteRegister(address, newValue);

        // Assert
        Assert.Equal(originalValue, fpgaSlave.GetRegisterValue(address));
    }

    [Fact]
    public void FormatTransaction_Read_ShouldProduceCorrect32BitFormat()
    {
        // Arrange
        var fpgaSlave = new FpgaSimulator.Core.Spi.SpiSlaveSimulator();
        var spiMaster = new SpiMasterSimulator(fpgaSlave);
        byte address = 0x20;

        // Act
        var transaction = spiMaster.FormatReadTransaction(address);

        // Assert: Word0 = [addr << 8 | 0x00], Word1 = 0x0000
        Assert.Equal((ushort)((address << 8) | 0x00), transaction.Word0);
        Assert.Equal((ushort)0x0000, transaction.Word1);
    }

    [Fact]
    public void FormatTransaction_Write_ShouldProduceCorrect32BitFormat()
    {
        // Arrange
        var fpgaSlave = new FpgaSimulator.Core.Spi.SpiSlaveSimulator();
        var spiMaster = new SpiMasterSimulator(fpgaSlave);
        byte address = 0x21;
        ushort data = 0x1234;

        // Act
        var transaction = spiMaster.FormatWriteTransaction(address, data);

        // Assert: Word0 = [addr << 8 | 0x01], Word1 = data
        Assert.Equal((ushort)((address << 8) | 0x01), transaction.Word0);
        Assert.Equal(data, transaction.Word1);
    }

    [Fact]
    public void ExecuteTransaction_ShouldReturnCorrectValue_ForRead()
    {
        // Arrange
        var fpgaSlave = new FpgaSimulator.Core.Spi.SpiSlaveSimulator();
        var spiMaster = new SpiMasterSimulator(fpgaSlave);
        var transaction = spiMaster.FormatReadTransaction(0x20);

        // Act
        ushort result = spiMaster.ExecuteTransaction(transaction);

        // Assert
        Assert.Equal(0x0001, result); // Default STATUS value
    }
}
