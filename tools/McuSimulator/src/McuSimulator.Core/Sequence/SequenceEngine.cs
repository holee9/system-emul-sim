using FpgaSimulator.Core.Fsm;

namespace McuSimulator.Core.Sequence;

/// <summary>
/// Core FSM class for the MCU Sequence Engine.
/// 1:1 port from fw/src/sequence_engine.c.
/// <para>
/// 7 states, 8 events, MAX_RETRY_COUNT = 3.
/// </para>
/// </summary>
public class SequenceEngine
{
    /// <summary>Maximum retries before the engine stays in ERROR permanently.</summary>
    public const uint MaxRetryCount = 3;

    private readonly ISequenceCallback? _callback;

    /// <summary>Current FSM state.</summary>
    public SequenceState State { get; private set; } = SequenceState.Idle;

    /// <summary>Active scan mode (set on StartScan).</summary>
    public ScanMode Mode { get; private set; } = ScanMode.Single;

    /// <summary>Runtime statistics.</summary>
    public SequenceStatistics Statistics { get; } = new();

    /// <summary>Current retry count (incremented on ErrorCleared, capped at MaxRetryCount).</summary>
    public uint RetryCount { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SequenceEngine"/> class.
    /// </summary>
    /// <param name="callback">Optional callback for external integration (SPI to FPGA).</param>
    public SequenceEngine(ISequenceCallback? callback = null)
    {
        _callback = callback;
    }

    /// <summary>
    /// Initiate a scan sequence. Only valid from IDLE or COMPLETE states.
    /// Transitions to CONFIGURE and invokes <see cref="ISequenceCallback.OnConfigure"/>.
    /// </summary>
    /// <param name="mode">The scan mode to use.</param>
    public void StartScan(ScanMode mode)
    {
        if (State != SequenceState.Idle && State != SequenceState.Complete)
            return;

        Mode = mode;
        State = SequenceState.Configure;
        _callback?.OnConfigure(mode);
    }

    /// <summary>
    /// Stop the current scan from any state. Transitions to IDLE and invokes
    /// <see cref="ISequenceCallback.OnStop"/>.
    /// </summary>
    public void StopScan()
    {
        State = SequenceState.Idle;
        _callback?.OnStop();
    }

    /// <summary>
    /// Core FSM event dispatch (1:1 from seq_engine_handle_event in fw/src/sequence_engine.c).
    /// Invalid transitions are silently ignored.
    /// </summary>
    /// <param name="evt">The event to handle.</param>
    public void HandleEvent(SequenceEvent evt)
    {
        switch (evt)
        {
            case SequenceEvent.StartScan:
                // StartScan is handled via the dedicated StartScan(mode) method.
                // Receiving it as a raw event is a no-op (mode is unknown).
                break;

            case SequenceEvent.ConfigDone:
                if (State == SequenceState.Configure)
                {
                    State = SequenceState.Arm;
                    _callback?.OnArm();
                }
                break;

            case SequenceEvent.ArmDone:
                if (State == SequenceState.Arm)
                {
                    State = SequenceState.Scanning;
                }
                break;

            case SequenceEvent.FrameReady:
                if (State == SequenceState.Scanning)
                {
                    State = SequenceState.Streaming;
                    Statistics.FramesReceived++;
                }
                break;

            case SequenceEvent.Complete:
                if (State == SequenceState.Streaming)
                {
                    HandleStreamingComplete();
                }
                break;

            case SequenceEvent.StopScan:
                StopScan();
                break;

            case SequenceEvent.Error:
                HandleError();
                break;

            case SequenceEvent.ErrorCleared:
                HandleErrorCleared();
                break;
        }
    }

    /// <summary>
    /// Reset the engine to its initial state, clearing all statistics and retry count.
    /// </summary>
    public void Reset()
    {
        State = SequenceState.Idle;
        Mode = ScanMode.Single;
        RetryCount = 0;
        Statistics.Reset();
    }

    /// <summary>
    /// Handle completion of the STREAMING state based on the active scan mode.
    /// </summary>
    private void HandleStreamingComplete()
    {
        Statistics.FramesSent++;

        switch (Mode)
        {
            case ScanMode.Single:
                // Single mode: complete, then auto-return to IDLE.
                State = SequenceState.Complete;
                State = SequenceState.Idle;
                break;

            case ScanMode.Continuous:
            case ScanMode.Calibration:
                // Continuous / Calibration: loop back to SCANNING for next frame.
                State = SequenceState.Scanning;
                break;
        }
    }

    /// <summary>
    /// Transition to ERROR from any state.
    /// </summary>
    private void HandleError()
    {
        var previousState = State;
        State = SequenceState.Error;
        Statistics.Errors++;
        _callback?.OnError(previousState, $"Error in state {previousState}");
    }

    /// <summary>
    /// Handle ErrorCleared: if retries remain, return to IDLE; otherwise stay in ERROR.
    /// </summary>
    private void HandleErrorCleared()
    {
        if (State != SequenceState.Error)
            return;

        if (RetryCount < MaxRetryCount)
        {
            RetryCount++;
            Statistics.Retries++;
            State = SequenceState.Idle;
        }
        // else: stay in ERROR -- retries exhausted.
    }
}
