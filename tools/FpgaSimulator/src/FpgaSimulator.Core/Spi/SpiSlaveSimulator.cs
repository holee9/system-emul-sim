namespace FpgaSimulator.Core.Spi;

using FpgaSimulator.Core.Fsm;

/// <summary>
/// Simulates the FPGA SPI Slave interface and register map.
/// Models SPI register access for SoC-FPGA communication.
/// Implements fpga-design.md Section 6 register map.
/// </summary>
public sealed class SpiSlaveSimulator
{
    private readonly object _lock = new();
    private readonly Dictionary<ushort, ushort> _registers;
    private readonly HashSet<ushort> _readOnlyRegisters;
    private uint _frameCounter;

    // ILA capture snapshot storage
    private ushort _ilaCapture0;
    private ushort _ilaCapture1;
    private ushort _ilaCapture2;
    private ushort _ilaCapture3;

    /// <summary>
    /// Initializes a new instance with default register values.
    /// </summary>
    public SpiSlaveSimulator()
    {
        _frameCounter = 0;

        // Initialize register map with default values
        _registers = new Dictionary<ushort, ushort>
        {
            // Identification
            { SpiRegisterAddresses.DEVICE_ID, 0xA735 }, // Artix-7 35T

            // Status and Control
            { SpiRegisterAddresses.STATUS, 0x0001 }, // Idle state
            { SpiRegisterAddresses.CONTROL, 0x0000 },

            // Frame Counter
            { SpiRegisterAddresses.FRAME_COUNT_HI, 0x0000 },
            { SpiRegisterAddresses.FRAME_COUNT_LO, 0x0000 },

            // Timing (defaults)
            { SpiRegisterAddresses.TIMING_ROW_PERIOD, 16 },
            { SpiRegisterAddresses.TIMING_GATE_ON, 1000 },
            { SpiRegisterAddresses.TIMING_GATE_OFF, 100 },
            { SpiRegisterAddresses.ROIC_SETTLE_US, 10 },
            { SpiRegisterAddresses.ADC_CONV_US, 5 },
            { SpiRegisterAddresses.FRAME_BLANK_US, 500 },

            // Panel Config
            { SpiRegisterAddresses.PANEL_ROWS, 1024 },
            { SpiRegisterAddresses.PANEL_COLS, 1024 },
            { SpiRegisterAddresses.BIT_DEPTH, 16 },
            { SpiRegisterAddresses.PIXEL_FORMAT, 0x2E }, // RAW16

            // CSI-2 Config
            { SpiRegisterAddresses.CSI2_LANE_COUNT, 0x04 }, // 4 lanes
            { SpiRegisterAddresses.CSI2_LANE_SPEED, 0x64 }, // 1.0 Gbps

            // Error Flags
            { SpiRegisterAddresses.ERROR_FLAGS, 0x0000 },

            // Data Interface Status
            { SpiRegisterAddresses.DATA_IF_STATUS, 0x0000 },
            { SpiRegisterAddresses.TX_FRAME_COUNT, 0x0000 },
            { SpiRegisterAddresses.TX_ERROR_COUNT, 0x0000 },

            // ILA Capture Registers
            { SpiRegisterAddresses.ILA_CAPTURE_0, 0x0000 },
            { SpiRegisterAddresses.ILA_CAPTURE_1, 0x0000 },
            { SpiRegisterAddresses.ILA_CAPTURE_2, 0x0000 },
            { SpiRegisterAddresses.ILA_CAPTURE_3, 0x0000 },

            // Version
            { SpiRegisterAddresses.VERSION, 0x0100 }, // v1.0.0
            { SpiRegisterAddresses.BUILD_DATE, 0x0217 }, // 2026-02-17
        };

        // Define read-only registers
        _readOnlyRegisters = new HashSet<ushort>
        {
            SpiRegisterAddresses.DEVICE_ID,
            SpiRegisterAddresses.ILA_CAPTURE_0,
            SpiRegisterAddresses.ILA_CAPTURE_1,
            SpiRegisterAddresses.ILA_CAPTURE_2,
            SpiRegisterAddresses.ILA_CAPTURE_3,
            SpiRegisterAddresses.STATUS,
            SpiRegisterAddresses.FRAME_COUNT_HI,
            SpiRegisterAddresses.FRAME_COUNT_LO,
            SpiRegisterAddresses.DATA_IF_STATUS,
            SpiRegisterAddresses.TX_FRAME_COUNT,
            SpiRegisterAddresses.TX_ERROR_COUNT,
            SpiRegisterAddresses.VERSION,
            SpiRegisterAddresses.BUILD_DATE
        };

        _ilaCapture0 = 0;
        _ilaCapture1 = 0;
        _ilaCapture2 = 0;
        _ilaCapture3 = 0;
    }

    /// <summary>Fixed device ID (0xA735 for Artix-7 35T)</summary>
    public ushort DeviceId => _registers[SpiRegisterAddresses.DEVICE_ID];

    /// <summary>
    /// Reads a 16-bit register value.
    /// </summary>
    /// <param name="address">Register address</param>
    /// <returns>Register value, or 0 if address not found</returns>
    public ushort GetRegisterValue(ushort address)
    {
        lock (_lock)
        {
            // ILA capture registers return latched snapshot values
            if (address == SpiRegisterAddresses.ILA_CAPTURE_0) return _ilaCapture0;
            if (address == SpiRegisterAddresses.ILA_CAPTURE_1) return _ilaCapture1;
            if (address == SpiRegisterAddresses.ILA_CAPTURE_2) return _ilaCapture2;
            if (address == SpiRegisterAddresses.ILA_CAPTURE_3) return _ilaCapture3;

            return _registers.GetValueOrDefault(address, (ushort)0);
        }
    }

    /// <summary>
    /// Writes a 16-bit value to a register.
    /// Read-only registers are not modified.
    /// </summary>
    /// <param name="address">Register address</param>
    /// <param name="value">Value to write</param>
    public void WriteRegister(ushort address, ushort value)
    {
        lock (_lock)
        {
            // Skip read-only registers
            if (_readOnlyRegisters.Contains(address))
                return;

            // Handle CONTROL register special bits
            if (address == SpiRegisterAddresses.CONTROL)
            {
                HandleControlRegisterWrite(value);
                return;
            }

            // Standard write
            if (_registers.ContainsKey(address))
            {
                _registers[address] = value;
            }
        }
    }

    /// <summary>
    /// Updates the STATUS register in real-time from FSM state and buffer bank.
    /// Called by the simulation controller to keep STATUS synchronized with FSM.
    /// </summary>
    /// <param name="status">Current FSM status snapshot</param>
    public void UpdateStatusFromFsm(FsmStatus status)
    {
        lock (_lock)
        {
            // STATUS register bit layout:
            // [0] idle, [1] busy, [2] error,
            // [7:4] fsm_state, [11] active_bank, [15:12] scan_mode
            ushort statusValue = 0;

            if (status.IsIdle) statusValue |= 0x0001;
            if (status.IsBusy) statusValue |= 0x0002;
            if (status.HasError) statusValue |= 0x0004;

            // Encode FSM state in bits [7:4]
            statusValue |= (ushort)(((int)status.State & 0x0F) << 4);

            // Active bank in bit [11]
            statusValue |= (ushort)((status.ActiveBank & 0x01) << 11);

            // Scan mode in bits [15:12]
            statusValue |= (ushort)(((int)status.ScanMode & 0x0F) << 12);

            _registers[SpiRegisterAddresses.STATUS] = statusValue;

            // Update frame counter registers
            _frameCounter = status.FrameCounter;
            UpdateFrameCounterRegisters();

            // Update error flags from FSM
            _registers[SpiRegisterAddresses.ERROR_FLAGS] = (ushort)status.ErrorFlags;
        }
    }

    /// <summary>
    /// Captures an ILA (Integrated Logic Analyzer) snapshot of internal state.
    /// Triggered on error conditions for post-mortem debugging.
    /// </summary>
    /// <param name="fsmState">Current FSM state encoding</param>
    /// <param name="lineCounter">Current line counter value</param>
    /// <param name="errorFlags">Current error flags</param>
    /// <param name="extraData">Additional diagnostic data</param>
    public void CaptureIlaSnapshot(ushort fsmState, ushort lineCounter, ushort errorFlags, ushort extraData)
    {
        lock (_lock)
        {
            _ilaCapture0 = fsmState;
            _ilaCapture1 = lineCounter;
            _ilaCapture2 = errorFlags;
            _ilaCapture3 = extraData;

            // Update the register map for GetAllRegisters consistency
            _registers[SpiRegisterAddresses.ILA_CAPTURE_0] = _ilaCapture0;
            _registers[SpiRegisterAddresses.ILA_CAPTURE_1] = _ilaCapture1;
            _registers[SpiRegisterAddresses.ILA_CAPTURE_2] = _ilaCapture2;
            _registers[SpiRegisterAddresses.ILA_CAPTURE_3] = _ilaCapture3;
        }
    }

    /// <summary>
    /// Increments the 32-bit frame counter.
    /// Updates both FRAME_COUNT_LO and FRAME_COUNT_HI registers.
    /// </summary>
    public void IncrementFrameCounter()
    {
        lock (_lock)
        {
            _frameCounter++;
            UpdateFrameCounterRegisters();
        }
    }

    /// <summary>
    /// Sets the 32-bit frame counter to a specific value.
    /// </summary>
    /// <param name="value">Frame counter value</param>
    public void SetFrameCounter(uint value)
    {
        lock (_lock)
        {
            _frameCounter = value;
            UpdateFrameCounterRegisters();
        }
    }

    /// <summary>
    /// Sets an error flag in the ERROR_FLAGS register.
    /// </summary>
    /// <param name="flag">Error flag bit(s) to set</param>
    public void SetErrorFlag(byte flag)
    {
        lock (_lock)
        {
            _registers[SpiRegisterAddresses.ERROR_FLAGS] |= flag;
            UpdateStatusRegister();
        }
    }

    /// <summary>
    /// Resets the SPI slave to initial state.
    /// Clears all writable registers and counters.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _frameCounter = 0;
            UpdateFrameCounterRegisters();

            // Clear writable registers
            _registers[SpiRegisterAddresses.CONTROL] = 0;
            _registers[SpiRegisterAddresses.ERROR_FLAGS] = 0;
            _registers[SpiRegisterAddresses.PANEL_ROWS] = 1024;
            _registers[SpiRegisterAddresses.PANEL_COLS] = 1024;
            _registers[SpiRegisterAddresses.BIT_DEPTH] = 16;

            // Clear ILA captures
            _ilaCapture0 = 0;
            _ilaCapture1 = 0;
            _ilaCapture2 = 0;
            _ilaCapture3 = 0;
            _registers[SpiRegisterAddresses.ILA_CAPTURE_0] = 0;
            _registers[SpiRegisterAddresses.ILA_CAPTURE_1] = 0;
            _registers[SpiRegisterAddresses.ILA_CAPTURE_2] = 0;
            _registers[SpiRegisterAddresses.ILA_CAPTURE_3] = 0;

            UpdateStatusRegister();
        }
    }

    /// <summary>
    /// Gets a copy of all register values.
    /// </summary>
    /// <returns>Dictionary mapping register addresses to values</returns>
    public Dictionary<ushort, ushort> GetAllRegisters()
    {
        lock (_lock)
        {
            return new Dictionary<ushort, ushort>(_registers);
        }
    }

    private void HandleControlRegisterWrite(ushort value)
    {
        // Extract individual bits
        bool startScan = (value & 0x01) != 0;
        bool stopScan = (value & 0x02) != 0;
        bool reset = (value & 0x04) != 0;
        bool errorClear = (value & 0x10) != 0;

        if (reset)
        {
            Reset();
            return;
        }

        if (errorClear)
        {
            _registers[SpiRegisterAddresses.ERROR_FLAGS] = 0;
        }

        // Update STATUS based on start/stop bits
        var status = _registers[SpiRegisterAddresses.STATUS];
        if (startScan)
        {
            status = (ushort)((status & ~0x0001) | 0x0002); // Clear idle, set busy
        }
        if (stopScan)
        {
            status = (ushort)((status & ~0x0002) | 0x0001); // Clear busy, set idle
        }
        _registers[SpiRegisterAddresses.STATUS] = status;

        // Store the complete CONTROL value (including mode bits)
        _registers[SpiRegisterAddresses.CONTROL] = value;
    }

    private void UpdateFrameCounterRegisters()
    {
        _registers[SpiRegisterAddresses.FRAME_COUNT_LO] = (ushort)(_frameCounter & 0xFFFF);
        _registers[SpiRegisterAddresses.FRAME_COUNT_HI] = (ushort)(_frameCounter >> 16);
    }

    private void UpdateStatusRegister()
    {
        var errorFlags = _registers[SpiRegisterAddresses.ERROR_FLAGS];
        var status = _registers[SpiRegisterAddresses.STATUS];

        if (errorFlags != 0)
        {
            // Set error state
            status = (ushort)((status & ~0x0003) | 0x0004); // Clear idle/busy, set error
        }
        else
        {
            // No error - ensure error bit is clear
            status = (ushort)(status & ~0x0004);
        }

        _registers[SpiRegisterAddresses.STATUS] = status;
    }
}
