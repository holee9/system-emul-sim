using XrayDetector.Gui.Core;

namespace XrayDetector.Gui.ViewModels;

/// <summary>A single SPI register entry for display in the register map table.</summary>
public sealed class SpiRegisterEntry
{
    public string Name { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Value { get; set; } = "0x00";
}

/// <summary>
/// ViewModel for FPGA tab (SPEC-GUI-002).
/// Manages CSI-2 interface, gate timing, and FSM status.
/// </summary>
public sealed class FpgaEmulatorViewModel : ObservableObject
{
    private int _csi2Lanes = 4;
    private int _csi2DataRateMbps = 1500;
    private int _lineBufferDepth = 1024;
    private int _gateLines = 1024;
    private int _lineTimeUs = 100;
    private int _frameTimeMs = 100;
    private string _fsmState = "IDLE";
    private string _errorFlags = "None";

    /// <summary>CSI-2 lane count (1, 2, or 4).</summary>
    public int Csi2Lanes
    {
        get => _csi2Lanes;
        set => SetField(ref _csi2Lanes, value);
    }

    /// <summary>CSI-2 data rate in Mbps.</summary>
    public int Csi2DataRateMbps
    {
        get => _csi2DataRateMbps;
        set => SetField(ref _csi2DataRateMbps, value);
    }

    /// <summary>Line buffer depth in pixels.</summary>
    public int LineBufferDepth
    {
        get => _lineBufferDepth;
        set => SetField(ref _lineBufferDepth, value);
    }

    /// <summary>Gate lines (rows) count.</summary>
    public int GateLines
    {
        get => _gateLines;
        set => SetField(ref _gateLines, value);
    }

    /// <summary>Line readout time in microseconds.</summary>
    public int LineTimeUs
    {
        get => _lineTimeUs;
        set => SetField(ref _lineTimeUs, value);
    }

    /// <summary>Frame readout time in milliseconds.</summary>
    public int FrameTimeMs
    {
        get => _frameTimeMs;
        set => SetField(ref _frameTimeMs, value);
    }

    /// <summary>FPGA FSM state: IDLE / INTEGRATE / READOUT / LINE_DONE / FRAME_DONE / ERROR.</summary>
    public string FsmState
    {
        get => _fsmState;
        set
        {
            if (SetField(ref _fsmState, value))
                OnPropertyChanged(nameof(IsReady));
        }
    }

    /// <summary>Active error flags (CSI2_SYNC, FRAME_DROP, CRC_FAIL, TIMEOUT, OVERRUN).</summary>
    public string ErrorFlags
    {
        get => _errorFlags;
        set
        {
            if (SetField(ref _errorFlags, value))
                OnPropertyChanged(nameof(IsReady));
        }
    }

    /// <summary>Indicates whether this module is ready for integration run.</summary>
    public bool IsReady => FsmState == "IDLE" && ErrorFlags == "None";

    /// <summary>
    /// SPI register map (REQ-SIM-020): 14 key registers from fpga-design.md Section 6.3.
    /// Read-only display — values mirror current ViewModel state.
    /// </summary>
    public IReadOnlyList<SpiRegisterEntry> SpiRegisters { get; } =
    [
        new() { Name = "STATUS",           Address = "0x20", Description = "FSM state + error summary" },
        new() { Name = "CONTROL",          Address = "0x21", Description = "Acquisition enable / reset" },
        new() { Name = "FRAME_COUNT_HI",   Address = "0x30", Description = "Frame counter [15:8]" },
        new() { Name = "FRAME_COUNT_LO",   Address = "0x31", Description = "Frame counter [7:0]" },
        new() { Name = "TIMING_ROW_PERIOD",Address = "0x40", Description = "Row period (clocks)" },
        new() { Name = "TIMING_GATE_ON",   Address = "0x41", Description = "Gate ON duration (clocks)" },
        new() { Name = "TIMING_GATE_OFF",  Address = "0x42", Description = "Gate OFF duration (clocks)" },
        new() { Name = "ROIC_SETTLE_US",   Address = "0x43", Description = "ROIC settle time (us)" },
        new() { Name = "ADC_CONV_US",      Address = "0x44", Description = "ADC conversion time (us)" },
        new() { Name = "FRAME_BLANK_US",   Address = "0x45", Description = "Frame blanking (us)" },
        new() { Name = "PANEL_ROWS",       Address = "0x50", Description = "Detector row count" },
        new() { Name = "PANEL_COLS",       Address = "0x51", Description = "Detector column count" },
        new() { Name = "CSI2_LANE_COUNT",  Address = "0x60", Description = "Active CSI-2 lane count" },
        new() { Name = "ERROR_FLAGS",      Address = "0x80", Description = "Active error flags bitmask" },
    ];

    /// <summary>
    /// Updates FSM state to reflect pipeline acquisition state.
    /// Called by MainViewModel when IsAcquiring changes.
    /// </summary>
    public void UpdateFsmState(bool isAcquiring)
    {
        FsmState = isAcquiring ? "READOUT" : "IDLE";
    }

    /// <summary>
    /// Advances FSM to FRAME_DONE when a frame has been processed.
    /// Called by MainViewModel on each frame received.
    /// </summary>
    public void NotifyFrameProcessed()
    {
        if (FsmState == "READOUT")
            FsmState = "FRAME_DONE";
        else if (FsmState == "FRAME_DONE")
            FsmState = "READOUT";
    }
}
