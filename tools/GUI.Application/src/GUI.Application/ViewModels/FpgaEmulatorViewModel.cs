using XrayDetector.Gui.Core;

namespace XrayDetector.Gui.ViewModels;

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
        set => SetField(ref _fsmState, value);
    }

    /// <summary>Active error flags (CSI2_SYNC, FRAME_DROP, CRC_FAIL, TIMEOUT, OVERRUN).</summary>
    public string ErrorFlags
    {
        get => _errorFlags;
        set => SetField(ref _errorFlags, value);
    }

    /// <summary>Indicates whether this module is ready for integration run.</summary>
    public bool IsReady => FsmState == "IDLE" && ErrorFlags == "None";
}
