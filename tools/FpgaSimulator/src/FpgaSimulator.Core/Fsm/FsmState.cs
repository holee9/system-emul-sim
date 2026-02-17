namespace FpgaSimulator.Core.Fsm;

/// <summary>
/// Represents the state of the Panel Scan FSM.
/// Matches the RTL FSM encoding in fpga-design.md Section 3.2.
/// </summary>
public enum FsmState
{
    /// <summary>Waiting for start command (encoding: 3'b000)</summary>
    Idle = 0,

    /// <summary>Exposure in progress (encoding: 3'b001)</summary>
    Integrate = 1,

    /// <summary>Line readout active (encoding: 3'b010)</summary>
    Readout = 2,

    /// <summary>Line buffered, preparing next (encoding: 3'b011)</summary>
    LineDone = 3,

    /// <summary>Frame complete, updating counters (encoding: 3'b100)</summary>
    FrameDone = 4,

    /// <summary>Error detected, safe state (encoding: 3'b101)</summary>
    Error = 5
}
