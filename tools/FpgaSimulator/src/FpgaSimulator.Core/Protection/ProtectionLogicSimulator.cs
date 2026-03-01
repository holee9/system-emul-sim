namespace FpgaSimulator.Core.Protection;

using FpgaSimulator.Core.Fsm;

/// <summary>
/// Simulates the FPGA protection logic subsystem.
/// Models watchdog timer, readout timeout, safety shutdown, and error flag latching.
/// Implements fpga-design.md Section 7 protection features.
/// </summary>
public sealed class ProtectionLogicSimulator
{
    private readonly object _lock = new();
    private readonly ProtectionConfig _config;

    // Watchdog timer state
    private long _watchdogCounter;
    private long _watchdogLimit;

    // Readout timeout state
    private long _readoutCounter;
    private long _readoutLimit;
    private bool _readoutActive;

    // Safety shutdown state
    private int _shutdownCounter;
    private bool _shutdownInProgress;

    // Error flags (latched until explicit clear)
    private ProtectionError _errorFlags;

    // Output signals
    private bool _gateSafe;
    private bool _csi2Disable;
    private bool _bufferDisable;

    /// <summary>
    /// Initializes a new instance with the specified configuration.
    /// </summary>
    /// <param name="config">Protection configuration parameters</param>
    public ProtectionLogicSimulator(ProtectionConfig? config = null)
    {
        _config = config ?? new ProtectionConfig();

        // Convert timeouts to tick counts (assuming 100 MHz system clock)
        _watchdogLimit = (long)(_config.WatchdogTimeoutMs * 100_000); // ms to 100MHz ticks
        _readoutLimit = (long)(_config.ReadoutTimeoutUs * 100); // us to 100MHz ticks

        _watchdogCounter = 0;
        _readoutCounter = 0;
        _readoutActive = false;
        _shutdownCounter = 0;
        _shutdownInProgress = false;
        _errorFlags = ProtectionError.None;
        _gateSafe = false;
        _csi2Disable = false;
        _bufferDisable = false;
    }

    /// <summary>Current latched error flags</summary>
    public ProtectionError ErrorFlags
    {
        get { lock (_lock) { return _errorFlags; } }
    }

    /// <summary>True when gate output is forced to safe state</summary>
    public bool GateSafe
    {
        get { lock (_lock) { return _gateSafe; } }
    }

    /// <summary>True when CSI-2 TX is disabled by protection logic</summary>
    public bool Csi2Disable
    {
        get { lock (_lock) { return _csi2Disable; } }
    }

    /// <summary>True when line buffer is disabled by protection logic</summary>
    public bool BufferDisable
    {
        get { lock (_lock) { return _bufferDisable; } }
    }

    /// <summary>True when safety shutdown sequence is in progress</summary>
    public bool IsShutdownInProgress
    {
        get { lock (_lock) { return _shutdownInProgress; } }
    }

    /// <summary>Current watchdog counter value (for diagnostics)</summary>
    public long WatchdogCounter
    {
        get { lock (_lock) { return _watchdogCounter; } }
    }

    /// <summary>
    /// Resets the watchdog timer. Must be called periodically via SPI heartbeat
    /// to prevent watchdog timeout.
    /// </summary>
    public void ResetWatchdog()
    {
        lock (_lock)
        {
            _watchdogCounter = 0;
        }
    }

    /// <summary>
    /// Signals the start of a readout operation.
    /// Starts the readout timeout counter.
    /// </summary>
    public void BeginReadout()
    {
        lock (_lock)
        {
            _readoutActive = true;
            _readoutCounter = 0;
        }
    }

    /// <summary>
    /// Signals the end of a readout operation.
    /// Stops the readout timeout counter.
    /// </summary>
    public void EndReadout()
    {
        lock (_lock)
        {
            _readoutActive = false;
            _readoutCounter = 0;
        }
    }

    /// <summary>
    /// Clears all latched error flags and deasserts safety outputs.
    /// Corresponds to writing error_clear bit to CONTROL register.
    /// </summary>
    public void ClearErrors()
    {
        lock (_lock)
        {
            _errorFlags = ProtectionError.None;
            _shutdownInProgress = false;
            _shutdownCounter = 0;
            _gateSafe = false;
            _csi2Disable = false;
            _bufferDisable = false;
        }
    }

    /// <summary>
    /// Processes one system clock tick, advancing all protection timers.
    /// </summary>
    public void ProcessTick()
    {
        lock (_lock)
        {
            // Process safety shutdown sequence
            if (_shutdownInProgress)
            {
                ProcessShutdown();
                return;
            }

            // Watchdog timer
            if (_config.WatchdogEnabled)
            {
                _watchdogCounter++;
                if (_watchdogCounter >= _watchdogLimit)
                {
                    LatchError(ProtectionError.WatchdogTimeout, isFatal: true);
                    return;
                }
            }

            // Readout timeout
            if (_config.ReadoutTimeoutEnabled && _readoutActive)
            {
                _readoutCounter++;
                if (_readoutCounter >= _readoutLimit)
                {
                    LatchError(ProtectionError.ReadoutTimeout, isFatal: false);
                    _readoutActive = false;
                }
            }
        }
    }

    /// <summary>
    /// Injects an external error condition (e.g., from FSM or CSI-2 subsystem).
    /// </summary>
    /// <param name="error">Error flag to latch</param>
    /// <param name="isFatal">True if the error requires immediate safety shutdown</param>
    public void ReportError(ProtectionError error, bool isFatal)
    {
        lock (_lock)
        {
            LatchError(error, isFatal);
        }
    }

    /// <summary>
    /// Converts the current protection error flags to FSM ErrorFlags for compatibility.
    /// </summary>
    /// <returns>Equivalent ErrorFlags value</returns>
    public ErrorFlags ToFsmErrorFlags()
    {
        lock (_lock)
        {
            var result = Fsm.ErrorFlags.None;

            if (_errorFlags.HasFlag(ProtectionError.WatchdogTimeout))
                result |= Fsm.ErrorFlags.Watchdog;
            if (_errorFlags.HasFlag(ProtectionError.ReadoutTimeout))
                result |= Fsm.ErrorFlags.Timeout;
            if (_errorFlags.HasFlag(ProtectionError.BufferOverflow))
                result |= Fsm.ErrorFlags.Overflow;
            if (_errorFlags.HasFlag(ProtectionError.Csi2Error))
                result |= Fsm.ErrorFlags.CrcError;

            return result;
        }
    }

    /// <summary>
    /// Resets the protection logic to initial state.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _watchdogCounter = 0;
            _readoutCounter = 0;
            _readoutActive = false;
            _shutdownCounter = 0;
            _shutdownInProgress = false;
            _errorFlags = ProtectionError.None;
            _gateSafe = false;
            _csi2Disable = false;
            _bufferDisable = false;
        }
    }

    private void LatchError(ProtectionError error, bool isFatal)
    {
        // Latch error flag (maintained until explicit clear)
        _errorFlags |= error;

        if (isFatal && !_shutdownInProgress)
        {
            // Initiate safety shutdown sequence
            _shutdownInProgress = true;
            _shutdownCounter = 0;
        }
    }

    private void ProcessShutdown()
    {
        _shutdownCounter++;

        // Assert safety outputs progressively within shutdown window
        // All outputs must be asserted within ShutdownResponseClocks
        if (_shutdownCounter >= 1)
            _gateSafe = true;

        if (_shutdownCounter >= 2)
            _csi2Disable = true;

        if (_shutdownCounter >= 3)
            _bufferDisable = true;

        // Shutdown complete when all outputs asserted
        if (_shutdownCounter >= _config.ShutdownResponseClocks)
        {
            _shutdownInProgress = false;
        }
    }
}

/// <summary>
/// Protection error flags for the protection logic subsystem.
/// These are latched until explicitly cleared via error_clear.
/// </summary>
[Flags]
public enum ProtectionError : byte
{
    /// <summary>No error condition</summary>
    None = 0,

    /// <summary>Watchdog timer expired - fatal (bit [0])</summary>
    WatchdogTimeout = 1 << 0,

    /// <summary>Readout timeout - non-fatal (bit [1])</summary>
    ReadoutTimeout = 1 << 1,

    /// <summary>Buffer overflow detected - non-fatal (bit [2])</summary>
    BufferOverflow = 1 << 2,

    /// <summary>CSI-2 TX error - non-fatal (bit [3])</summary>
    Csi2Error = 1 << 3,

    /// <summary>ROIC interface fault - fatal (bit [4])</summary>
    RoicFault = 1 << 4,

    /// <summary>Configuration error - fatal (bit [5])</summary>
    ConfigError = 1 << 5
}
