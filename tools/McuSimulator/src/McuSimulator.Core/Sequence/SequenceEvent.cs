namespace McuSimulator.Core.Sequence;

/// <summary>
/// Sequence Engine events (1:1 mapping from seq_event_t in fw/include/sequence_engine.h).
/// </summary>
public enum SequenceEvent
{
    StartScan = 0,
    ConfigDone = 1,
    ArmDone = 2,
    FrameReady = 3,
    StopScan = 4,
    Error = 5,
    ErrorCleared = 6,
    Complete = 7
}
