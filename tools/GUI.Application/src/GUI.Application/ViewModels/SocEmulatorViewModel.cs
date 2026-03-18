using XrayDetector.Gui.Core;

namespace XrayDetector.Gui.ViewModels;

/// <summary>
/// ViewModel for SoC tab (SPEC-GUI-002).
/// Manages CSI-2 RX, frame buffer, UDP TX, and sequence engine state.
/// </summary>
public sealed class SocEmulatorViewModel : ObservableObject
{
    private string _udpTargetIp = "127.0.0.1";
    private int _udpTargetPort = 8001;
    private string _sequenceEngineState = "Idle";

    /// <summary>Creates a new SocEmulatorViewModel.</summary>
    public SocEmulatorViewModel(
        SimulatorControlViewModel simulatorControl,
        PipelineStatusViewModel pipelineStatus)
    {
        SimulatorControl = simulatorControl ?? throw new ArgumentNullException(nameof(simulatorControl));
        PipelineStatus = pipelineStatus ?? throw new ArgumentNullException(nameof(pipelineStatus));
    }

    /// <summary>
    /// SimulatorControlViewModel providing MCU parameters.
    /// Exposes: FrameBufferCount, McuBufferState.
    /// </summary>
    public SimulatorControlViewModel SimulatorControl { get; }

    /// <summary>
    /// PipelineStatusViewModel providing frame processing and network statistics.
    /// Exposes: FramesProcessed, FramesFailed, PacketsSent, PacketsLost, StatusIndicator.
    /// </summary>
    public PipelineStatusViewModel PipelineStatus { get; }

    /// <summary>UDP TX target IP address.</summary>
    public string UdpTargetIp
    {
        get => _udpTargetIp;
        set => SetField(ref _udpTargetIp, value);
    }

    /// <summary>UDP TX target port (1024-65535).</summary>
    public int UdpTargetPort
    {
        get => _udpTargetPort;
        set => SetField(ref _udpTargetPort, Math.Clamp(value, 1024, 65535));
    }

    /// <summary>
    /// Sequence Engine command state: Idle / Arming / Acquiring / Draining / Error.
    /// Updated by MainViewModel when acquisition state changes.
    /// </summary>
    public string SequenceEngineState
    {
        get => _sequenceEngineState;
        set => SetField(ref _sequenceEngineState, value);
    }

    /// <summary>
    /// Updates the Sequence Engine state based on acquisition lifecycle.
    /// Called by MainViewModel when IsAcquiring or IsConnected changes.
    /// </summary>
    public void UpdateSequenceState(bool isConnected, bool isAcquiring)
    {
        SequenceEngineState = (isConnected, isAcquiring) switch
        {
            (false, _) => "Idle",
            (true, false) => "Arming",
            (true, true) => "Acquiring",
        };
    }

    /// <summary>Indicates whether this module is ready for integration run.</summary>
    public bool IsReady => true;
}
