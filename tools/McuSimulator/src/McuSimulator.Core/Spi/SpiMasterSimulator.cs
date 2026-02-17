using FpgaSimulator.Core.Spi;

namespace McuSimulator.Core.Spi;

/// <summary>
/// Represents a 32-bit SPI transaction in wire format.
/// Format: Word0=[addr << 8 | rw_flag], Word1=[data]
/// </summary>
public readonly record struct SpiTransaction32
{
    /// <summary>Word 0: [addr << 8 | rw_flag]</summary>
    public required ushort Word0 { get; init; }

    /// <summary>Word 1: data (16 bits)</summary>
    public required ushort Word1 { get; init; }
}

/// <summary>
/// Simulates the SoC SPI Master interface.
/// Models SPI transactions from SoC to FPGA.
/// Implements spi-register-map.md Section 2 transaction format.
/// </summary>
public sealed class SpiMasterSimulator
{
    private readonly SpiSlaveSimulator _fpgaSlave;

    /// <summary>
    /// Initializes a new instance connected to an FPGA SPI slave.
    /// </summary>
    /// <param name="fpgaSlave">FPGA SPI slave simulator</param>
    public SpiMasterSimulator(SpiSlaveSimulator fpgaSlave)
    {
        _fpgaSlave = fpgaSlave ?? throw new ArgumentNullException(nameof(fpgaSlave));
    }

    /// <summary>
    /// Reads a 16-bit register value from the FPGA.
    /// Transaction format: Word0=[addr << 8 | 0x00], Word1=[0x0000]
    /// </summary>
    /// <param name="address">Register address (0x00-0xFF)</param>
    /// <returns>Register value from FPGA</returns>
    public ushort ReadRegister(byte address)
    {
        var transaction = FormatReadTransaction(address);
        return ExecuteTransaction(transaction);
    }

    /// <summary>
    /// Writes a 16-bit value to an FPGA register.
    /// Transaction format: Word0=[addr << 8 | 0x01], Word1=[data]
    /// </summary>
    /// <param name="address">Register address (0x00-0xFF)</param>
    /// <param name="value">Value to write</param>
    public void WriteRegister(byte address, ushort value)
    {
        var transaction = FormatWriteTransaction(address, value);
        ExecuteWriteTransaction(transaction);
    }

    /// <summary>
    /// Formats a read transaction per spi-register-map.md Section 2.3.
    /// </summary>
    /// <param name="address">Register address</param>
    /// <returns>Formatted transaction</returns>
    public SpiTransaction32 FormatReadTransaction(byte address)
    {
        return new SpiTransaction32
        {
            Word0 = (ushort)((address << 8) | 0x00), // Address + Read flag
            Word1 = 0x0000  // Dummy data for read
        };
    }

    /// <summary>
    /// Formats a write transaction per spi-register-map.md Section 2.2.
    /// </summary>
    /// <param name="address">Register address</param>
    /// <param name="data">Data to write</param>
    /// <returns>Formatted transaction</returns>
    public SpiTransaction32 FormatWriteTransaction(byte address, ushort data)
    {
        return new SpiTransaction32
        {
            Word0 = (ushort)((address << 8) | 0x01), // Address + Write flag
            Word1 = data
        };
    }

    /// <summary>
    /// Executes a read transaction on the FPGA SPI slave.
    /// </summary>
    /// <param name="transaction">Transaction to execute</param>
    /// <returns>Register value read from FPGA</returns>
    public ushort ExecuteTransaction(SpiTransaction32 transaction)
    {
        // Extract address from Word0: [addr << 8 | rw_flag]
        byte address = (byte)(transaction.Word0 >> 8);
        byte rwFlag = (byte)(transaction.Word0 & 0xFF);

        if (rwFlag == 0x00)
        {
            // Read transaction
            return _fpgaSlave.GetRegisterValue(address);
        }
        else
        {
            // Write transaction - return the written value for confirmation
            _fpgaSlave.WriteRegister(address, transaction.Word1);
            return transaction.Word1;
        }
    }

    /// <summary>
    /// Executes a write transaction on the FPGA SPI slave.
    /// </summary>
    /// <param name="transaction">Transaction to execute</param>
    private void ExecuteWriteTransaction(SpiTransaction32 transaction)
    {
        // Extract address from Word0: [addr << 8 | rw_flag]
        byte address = (byte)(transaction.Word0 >> 8);
        _fpgaSlave.WriteRegister(address, transaction.Word1);
    }
}
