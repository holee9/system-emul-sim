namespace FpgaSimulator.Core.Fsm;

/// <summary>
/// Error flags representing error conditions in the FPGA.
/// Matches ERROR_FLAGS register bits in fpga-design.md Section 6.3.
/// </summary>
[Flags]
public enum ErrorFlags : byte
{
    /// <summary>No error condition</summary>
    None = 0,

    /// <summary>Readout timeout exceeded (bit [0])</summary>
    Timeout = 1 << 0,

    /// <summary>Line buffer overflow (bit [1])</summary>
    Overflow = 1 << 1,

    /// <summary>CSI-2 CRC mismatch (bit [2])</summary>
    CrcError = 1 << 2,

    /// <summary>Pixel saturation detected (bit [3])</summary>
    Overexposure = 1 << 3,

    /// <summary>ROIC interface error (bit [4])</summary>
    RoicFault = 1 << 4,

    /// <summary>D-PHY initialization failure (bit [5])</summary>
    DphyError = 1 << 5,

    /// <summary>Invalid configuration detected (bit [6])</summary>
    ConfigError = 1 << 6,

    /// <summary>System watchdog timeout (bit [7])</summary>
    Watchdog = 1 << 7
}
