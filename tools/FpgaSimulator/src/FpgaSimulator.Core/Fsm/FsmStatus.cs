namespace FpgaSimulator.Core.Fsm;

/// <summary>
/// Status snapshot of the Panel Scan FSM.
/// Provides current state information for the STATUS register.
/// </summary>
public record FsmStatus
{
    /// <summary>Current FSM state</summary>
    public required FsmState State { get; init; }

    /// <summary>True when FSM is in IDLE state (STATUS bit [0])</summary>
    public bool IsIdle => State == FsmState.Idle;

    /// <summary>True when scan is in progress (STATUS bit [1])</summary>
    public bool IsBusy => State is FsmState.Integrate or FsmState.Readout or
                         FsmState.LineDone or FsmState.FrameDone;

    /// <summary>True when error condition is active (STATUS bit [2])</summary>
    public bool HasError => State == FsmState.Error;

    /// <summary>Current frame count value</summary>
    public required uint FrameCounter { get; init; }

    /// <summary>Current line count within frame</summary>
    public required uint LineCounter { get; init; }

    /// <summary>Active error flags</summary>
    public required ErrorFlags ErrorFlags { get; init; }

    /// <summary>Current scan mode</summary>
    public required ScanMode ScanMode { get; init; }

    /// <summary>Current active write bank (0=A, 1=B) for STATUS bit [11]</summary>
    public required int ActiveBank { get; init; }
}
