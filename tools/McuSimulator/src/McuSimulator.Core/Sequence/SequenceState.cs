namespace McuSimulator.Core.Sequence;

/// <summary>
/// Sequence Engine states (1:1 mapping from seq_state_t in fw/include/sequence_engine.h).
/// </summary>
public enum SequenceState
{
    Idle = 0,
    Configure = 1,
    Arm = 2,
    Scanning = 3,
    Streaming = 4,
    Complete = 5,
    Error = 6
}
