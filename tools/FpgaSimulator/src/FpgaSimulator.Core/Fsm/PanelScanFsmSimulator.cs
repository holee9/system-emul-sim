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

    // Separate timers for ROIC settle and ADC conversion (8-bit each)
    private byte _settleTimer;
    private byte _adcTimer;
    private byte _settleTimeoutTicks;
    private byte _adcTimeoutTicks;

    // Track previous state for edge detection
    private FsmState _previousState;

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

        // Default separate timers: settle 10 ticks, ADC 5 ticks
        _settleTimeoutTicks = 10;
        _adcTimeoutTicks = 5;
        _settleTimer = 0;
        _adcTimer = 0;

        _previousState = FsmState.Idle;

        // Output signals default to inactive
        GateOn = false;
        RoicSync = false;
        LineValid = false;
        FrameValid = false;
        LineWriteAddress = 0;
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

    // --- Control signal outputs ---

    /// <summary>Active during INTEGRATE state. Exposure control pulse to Panel.</summary>
    public bool GateOn { get; private set; }

    /// <summary>Triggered on IDLE to INTEGRATE transition. ROIC read trigger pulse.</summary>
    public bool RoicSync { get; private set; }

    /// <summary>Active when READOUT completes for current line. Line data valid signal.</summary>
    public bool LineValid { get; private set; }

    /// <summary>Active during FRAME_DONE state. Frame complete signal.</summary>
    public bool FrameValid { get; private set; }

    /// <summary>Current write address during READOUT (increments per pixel clock).</summary>
    public ushort LineWriteAddress { get; private set; }

    // --- Separate timer access (read-only) ---

    /// <summary>Current ROIC settle countdown timer value (8-bit).</summary>
    public byte SettleTimer
    {
        get { lock (_lock) { return _settleTimer; } }
    }

    /// <summary>Current ADC conversion countdown timer value (8-bit).</summary>
    public byte AdcTimer
    {
        get { lock (_lock) { return _adcTimer; } }
    }

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

            _previousState = CurrentState;
            CurrentState = FsmState.Integrate;
            _currentTick = 0;
            LineCounter = 0;
            LineWriteAddress = 0;
            _settleTimer = 0;
            _adcTimer = 0;

            UpdateOutputSignals();
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

            _previousState = CurrentState;
            CurrentState = FsmState.Idle;
            LineCounter = 0;
            LineWriteAddress = 0;

            UpdateOutputSignals();
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
    /// Configures the separate ROIC settle and ADC conversion timers.
    /// </summary>
    /// <param name="settleTicks">ROIC settle countdown ticks (8-bit, max 255)</param>
    /// <param name="adcTicks">ADC conversion countdown ticks (8-bit, max 255)</param>
    public void SetTimerParameters(byte settleTicks, byte adcTicks)
    {
        lock (_lock)
        {
            _settleTimeoutTicks = Math.Max(settleTicks, (byte)1);
            _adcTimeoutTicks = Math.Max(adcTicks, (byte)1);
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
            _previousState = CurrentState;
            CurrentState = FsmState.Error;
            UpdateOutputSignals();
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
            _previousState = CurrentState;
            CurrentState = FsmState.Idle;
            _currentTick = 0;
            LineCounter = 0;
            LineWriteAddress = 0;
            UpdateOutputSignals();
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
            _previousState = CurrentState;
            CurrentState = FsmState.Idle;
            FrameCounter = 0;
            LineCounter = 0;
            ErrorFlagsValue = ErrorFlags.None;
            _currentTick = 0;
            ActiveBank = 0;
            LineWriteAddress = 0;
            _settleTimer = 0;
            _adcTimer = 0;
            UpdateOutputSignals();
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

            _previousState = CurrentState;
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

            UpdateOutputSignals();
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
            _settleTimer = _settleTimeoutTicks;
            LineWriteAddress = 0;
        }
    }

    private void ProcessReadoutState()
    {
        // Decrement settle timer first
        if (_settleTimer > 0)
        {
            _settleTimer--;
            return;
        }

        // Then decrement ADC timer
        if (_adcTimer > 0)
        {
            _adcTimer--;
            return;
        }

        // Increment write address during readout
        if (LineWriteAddress < PanelCols)
        {
            LineWriteAddress++;
        }

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
            _settleTimer = _settleTimeoutTicks;
            LineWriteAddress = 0;
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
            LineWriteAddress = 0;
        }
        else
        {
            // Return to idle (Single or Calibration mode)
            CurrentState = FsmState.Idle;
            LineCounter = 0;
            LineWriteAddress = 0;
        }

        _currentTick = 0;
    }

    private void UpdateOutputSignals()
    {
        // GateOn: active during INTEGRATE state
        GateOn = CurrentState == FsmState.Integrate;

        // RoicSync: one-shot pulse on IDLE -> INTEGRATE transition
        RoicSync = _previousState == FsmState.Idle && CurrentState == FsmState.Integrate;

        // LineValid: active when READOUT transitions to LineDone (line data ready)
        LineValid = CurrentState == FsmState.LineDone;

        // FrameValid: active during FRAME_DONE state
        FrameValid = CurrentState == FsmState.FrameDone;
    }
}
