namespace FpgaSimulator.Core.Fsm;

/// <summary>
/// Operating modes for the Panel Scan FSM.
/// Matches CONTROL register bits [3:2] in fpga-design.md Section 6.3.
/// </summary>
public enum ScanMode
{
    /// <summary>One frame capture, return to IDLE (bits [3:2] = 2'b00)</summary>
    Single = 0,

    /// <summary>Repeat frames until stop_scan (bits [3:2] = 2'b01)</summary>
    Continuous = 1,

    /// <summary>Dark frame (gate OFF during INTEGRATE) (bits [3:2] = 2'b10)</summary>
    Calibration = 2
}
