namespace FpgaSimulator.Core.Fsm;

/// <summary>
/// Simulates the Panel Scan FSM behavior from the FPGA RTL.
/// Models state transitions, timing, and frame/line counters.
/// Implements the state machine from fpga-design.md Section 3.
/// </summary>
public sealed class PanelScanFsmSimulator
{
    private readonly object _lock = new();
    private int _gateOnTicks;
    private int _gateOffTicks;
    private int _currentTick;

    /// <summary>
    /// Initializes a new instance with default timing parameters.
    /// </summary>
    public PanelScanFsmSimulator()
    {
        CurrentState = FsmState.Idle;
        ScanMode = ScanMode.Single;
        PanelRows = 1024;
        PanelCols = 1024;
        FrameCounter = 0;
        LineCounter = 0;
        ErrorFlagsValue = ErrorFlags.None;
        ActiveBank = 0;

        // Default timing: 1000us gate on, 100us gate off
        _gateOnTicks = 1000;
        _gateOffTicks = 100;
        _currentTick = 0;
    }

    /// <summary>Current FSM state</summary>
    public FsmState CurrentState { get; private set; }

    /// <summary>Current scan operating mode</summary>
    public ScanMode ScanMode { get; private set; }

    /// <summary>Panel dimension: number of rows (max 3072)</summary>
    public int PanelRows { get; private set; }

    /// <summary>Panel dimension: number of columns (max 3072)</summary>
    public int PanelCols { get; private set; }

    /// <summary>32-bit frame sequence counter</summary>
    public uint FrameCounter { get; private set; }

    /// <summary>Current line counter within frame (0 to PanelRows-1)</summary>
    public uint LineCounter { get; private set; }

    /// <summary>Active error flags</summary>
    public ErrorFlags ErrorFlagsValue { get; private set; }

    /// <summary>Current active write bank (0=A, 1=B)</summary>
    public int ActiveBank { get; private set; }

    /// <summary>
    /// Starts a new scan sequence by transitioning to INTEGRATE state.
    /// Corresponds to writing start_scan bit to CONTROL register.
    /// </summary>
    public void StartScan()
    {
        lock (_lock)
        {
            if (CurrentState == FsmState.Error)
                return;

            CurrentState = FsmState.Integrate;
            _currentTick = 0;
            LineCounter = 0;
        }
    }

    /// <summary>
    /// Stops the current scan and returns to IDLE state.
    /// Corresponds to writing stop_scan bit to CONTROL register.
    /// </summary>
    public void StopScan()
    {
        lock (_lock)
        {
            if (CurrentState == FsmState.Error)
                return;

            CurrentState = FsmState.Idle;
            LineCounter = 0;
        }
    }

    /// <summary>
    /// Sets the scan operating mode.
    /// </summary>
    /// <param name="mode">The scan mode to use</param>
    public void SetScanMode(ScanMode mode)
    {
        lock (_lock)
        {
            ScanMode = mode;
        }
    }

    /// <summary>
    /// Configures panel dimensions.
    /// </summary>
    /// <param name="rows">Number of rows (max 3072)</param>
    /// <param name="cols">Number of columns (max 3072)</param>
    public void SetPanelDimensions(int rows, int cols)
    {
        lock (_lock)
        {
            PanelRows = Math.Min(rows, 3072);
            PanelCols = Math.Min(cols, 3072);
        }
    }

    /// <summary>
    /// Configures gate timing in microseconds.
    /// </summary>
    /// <param name="gateOnUs">Gate ON duration (exposure time)</param>
    /// <param name="gateOffUs">Gate OFF duration (readout time)</param>
    public void SetGateTiming(int gateOnUs, int gateOffUs = 100)
    {
        lock (_lock)
        {
            _gateOnTicks = Math.Max(gateOnUs, 1);
            _gateOffTicks = Math.Max(gateOffUs, 1);
        }
    }

    /// <summary>
    /// Triggers an error condition, transitioning FSM to ERROR state.
    /// Used for error injection testing.
    /// </summary>
    /// <param name="error">The error flag to set</param>
    public void TriggerError(ErrorFlags error)
    {
        lock (_lock)
        {
            ErrorFlagsValue |= error;
            CurrentState = FsmState.Error;
        }
    }

    /// <summary>
    /// Clears error flags and returns to IDLE state.
    /// Corresponds to writing error_clear bit to CONTROL register.
    /// </summary>
    public void ClearError()
    {
        lock (_lock)
        {
            ErrorFlagsValue = ErrorFlags.None;
            CurrentState = FsmState.Idle;
            _currentTick = 0;
            LineCounter = 0;
        }
    }

    /// <summary>
    /// Resets the FSM to initial state, clearing all counters.
    /// Corresponds to writing reset bit to CONTROL register.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            CurrentState = FsmState.Idle;
            FrameCounter = 0;
            LineCounter = 0;
            ErrorFlagsValue = ErrorFlags.None;
            _currentTick = 0;
            ActiveBank = 0;
        }
    }

    /// <summary>
    /// Processes one clock tick, advancing the FSM state machine.
    /// Call this method repeatedly to simulate time progression.
    /// </summary>
    public void ProcessTick()
    {
        lock (_lock)
        {
            if (CurrentState == FsmState.Idle || CurrentState == FsmState.Error)
                return;

            _currentTick++;

            switch (CurrentState)
            {
                case FsmState.Integrate:
                    ProcessIntegrateState();
                    break;

                case FsmState.Readout:
                    ProcessReadoutState();
                    break;

                case FsmState.LineDone:
                    ProcessLineDoneState();
                    break;

                case FsmState.FrameDone:
                    ProcessFrameDoneState();
                    break;
            }
        }
    }

    /// <summary>
    /// Gets current status snapshot for STATUS register read.
    /// </summary>
    public FsmStatus GetStatus()
    {
        lock (_lock)
        {
            return new FsmStatus
            {
                State = CurrentState,
                FrameCounter = FrameCounter,
                LineCounter = LineCounter,
                ErrorFlags = ErrorFlagsValue,
                ScanMode = ScanMode,
                ActiveBank = ActiveBank
            };
        }
    }

    private void ProcessIntegrateState()
    {
        if (_currentTick >= _gateOnTicks)
        {
            CurrentState = FsmState.Readout;
            _currentTick = 0;
        }
    }

    private void ProcessReadoutState()
    {
        if (_currentTick >= _gateOffTicks)
        {
            CurrentState = FsmState.LineDone;
            _currentTick = 0;
        }
    }

    private void ProcessLineDoneState()
    {
        LineCounter++;
        ActiveBank = 1 - ActiveBank; // Toggle bank

        if (LineCounter >= PanelRows)
        {
            CurrentState = FsmState.FrameDone;
        }
        else
        {
            // Continue to next line
            CurrentState = FsmState.Readout;
            _currentTick = 0;
        }
    }

    private void ProcessFrameDoneState()
    {
        FrameCounter++;

        if (ScanMode == ScanMode.Continuous)
        {
            // Start next frame
            CurrentState = FsmState.Integrate;
            LineCounter = 0;
        }
        else
        {
            // Return to idle (Single or Calibration mode)
            CurrentState = FsmState.Idle;
            LineCounter = 0;
        }

        _currentTick = 0;
    }
}
