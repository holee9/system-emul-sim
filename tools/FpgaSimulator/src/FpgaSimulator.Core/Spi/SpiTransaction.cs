namespace FpgaSimulator.Core.Spi;

/// <summary>
/// Represents a SPI transaction (address + data).
/// Models the 32-bit SPI protocol from fpga-design.md Section 6.2.
/// </summary>
public readonly record struct SpiTransaction
{
    /// <summary>Register address (8 bits)</summary>
    public required byte Address { get; init; }

    /// <summary>Write flag (0=read, 1=write)</summary>
    public required bool IsWrite { get; init; }

    /// <summary>Data payload (16 bits)</summary>
    public required ushort Data { get; init; }
}
