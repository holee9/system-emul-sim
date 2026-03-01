using FpgaSimulator.Core.Fsm;

namespace McuSimulator.Core.Sequence;

/// <summary>
/// Callback interface for SequenceEngine external integration.
/// Abstracts SPI communication with FPGA during scan lifecycle.
/// </summary>
public interface ISequenceCallback
{
    /// <summary>Called when the engine transitions to CONFIGURE state.</summary>
    /// <param name="mode">The scan mode requested by the host.</param>
    void OnConfigure(ScanMode mode);

    /// <summary>Called when the engine transitions to ARM state.</summary>
    void OnArm();

    /// <summary>Called when scanning is stopped (any state to IDLE).</summary>
    void OnStop();

    /// <summary>Called when the engine enters ERROR state.</summary>
    /// <param name="state">The state the engine was in when the error occurred.</param>
    /// <param name="reason">Human-readable error description.</param>
    void OnError(SequenceState state, string reason);
}
