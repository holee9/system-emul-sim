using XrayDetector.Gui.Core;

namespace XrayDetector.Gui.ViewModels;

/// <summary>
/// ViewModel for Ethernet tab (SPEC-GUI-002).
/// Manages network impairment parameters and real-time network statistics.
/// </summary>
public sealed class EthernetViewModel : ObservableObject
{
    /// <summary>Creates a new EthernetViewModel.</summary>
    public EthernetViewModel(
        SimulatorControlViewModel simulatorControl,
        StatusViewModel status,
        PipelineStatusViewModel pipelineStatus)
    {
        SimulatorControl = simulatorControl ?? throw new ArgumentNullException(nameof(simulatorControl));
        Status = status ?? throw new ArgumentNullException(nameof(status));
        PipelineStatus = pipelineStatus ?? throw new ArgumentNullException(nameof(pipelineStatus));
    }

    /// <summary>
    /// SimulatorControlViewModel providing network impairment parameters.
    /// Exposes: PacketLossRate, ReorderRate, CorruptionRate, MinDelayMs, MaxDelayMs.
    /// </summary>
    public SimulatorControlViewModel SimulatorControl { get; }

    /// <summary>
    /// StatusViewModel providing connection state and throughput.
    /// Exposes: ConnectionState, ThroughputGbps, IsConnected.
    /// </summary>
    public StatusViewModel Status { get; }

    /// <summary>
    /// PipelineStatusViewModel providing packet statistics.
    /// Exposes: PacketsSent, PacketsLost, PacketsReordered, PacketsCorrupted.
    /// </summary>
    public PipelineStatusViewModel PipelineStatus { get; }

    /// <summary>Indicates whether this module is ready for integration run.</summary>
    public bool IsReady => true;
}
