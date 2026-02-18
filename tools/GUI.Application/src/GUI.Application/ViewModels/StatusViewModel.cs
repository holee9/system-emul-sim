using System.ComponentModel;
using XrayDetector.Common.Dto;
using XrayDetector.Gui.Core;

namespace XrayDetector.Gui.ViewModels;

/// <summary>
/// ViewModel for status dashboard (REQ-TOOLS-045).
/// Displays real-time detector status: connection, acquisition, throughput.
/// </summary>
public sealed class StatusViewModel : ObservableObject
{
    private string _connectionState = "Disconnected";
    private string _acquisitionState = "Idle";
    private long _framesReceived;
    private long _droppedFrames;
    private double _throughputGbps;
    private double _temperature;
    private bool _isConnected;
    private bool _isAcquiring;

    /// <summary>Current connection state display string.</summary>
    public string ConnectionState
    {
        get => _connectionState;
        private set => SetField(ref _connectionState, value);
    }

    /// <summary>Current acquisition state display string.</summary>
    public string AcquisitionState
    {
        get => _acquisitionState;
        private set => SetField(ref _acquisitionState, value);
    }

    /// <summary>Total frames received since connection.</summary>
    public long FramesReceived
    {
        get => _framesReceived;
        private set => SetField(ref _framesReceived, value);
    }

    /// <summary>Total frames dropped since connection.</summary>
    public long DroppedFrames
    {
        get => _droppedFrames;
        private set => SetField(ref _droppedFrames, value);
    }

    /// <summary>Current throughput in Gbps (REQ-TOOLS-045).</summary>
    public double ThroughputGbps
    {
        get => _throughputGbps;
        private set => SetField(ref _throughputGbps, value);
    }

    /// <summary>Detector temperature in Celsius (optional).</summary>
    public double Temperature
    {
        get => _temperature;
        private set => SetField(ref _temperature, value);
    }

    /// <summary>True if connected to detector.</summary>
    public bool IsConnected
    {
        get => _isConnected;
        private set => SetField(ref _isConnected, value);
    }

    /// <summary>True if currently acquiring frames.</summary>
    public bool IsAcquiring
    {
        get => _isAcquiring;
        private set => SetField(ref _isAcquiring, value);
    }

    /// <summary>Drop rate percentage (0-100).</summary>
    public double DropRatePercentage
    {
        get
        {
            long total = FramesReceived + DroppedFrames;
            return total > 0 ? (double)DroppedFrames / total * 100.0 : 0.0;
        }
    }

    /// <summary>
    /// Updates the view model with new detector status.
    /// Called at 1 Hz minimum per REQ-TOOLS-045.
    /// </summary>
    public void Update(DetectorStatus status, long framesReceived, long droppedFrames, double throughputGbps)
    {
        ConnectionState = MapConnectionState(status.ConnectionState);
        AcquisitionState = MapAcquisitionState(status.AcquisitionState);
        FramesReceived = framesReceived;
        DroppedFrames = droppedFrames;
        ThroughputGbps = throughputGbps;
        Temperature = (double)status.Temperature;
        IsConnected = status.IsConnected();
        IsAcquiring = status.IsAcquiring();

        // Notify DropRatePercentage change
        OnPropertyChanged(nameof(DropRatePercentage));
    }

    /// <summary>
    /// Resets all status values to default.
    /// Called on disconnect.
    /// </summary>
    public void Reset()
    {
        ConnectionState = "Disconnected";
        AcquisitionState = "Idle";
        FramesReceived = 0;
        DroppedFrames = 0;
        ThroughputGbps = 0.0;
        Temperature = 0.0;
        IsConnected = false;
        IsAcquiring = false;
        OnPropertyChanged(nameof(DropRatePercentage));
    }

    private static string MapConnectionState(Common.Dto.ConnectionState state) => state switch
    {
        Common.Dto.ConnectionState.Disconnected => "Disconnected",
        Common.Dto.ConnectionState.Connecting => "Connecting...",
        Common.Dto.ConnectionState.Connected => "Connected",
        Common.Dto.ConnectionState.Reconnecting => "Reconnecting...",
        Common.Dto.ConnectionState.Error => "Connection Error",
        _ => "Unknown"
    };

    private static string MapAcquisitionState(Common.Dto.AcquisitionState state) => state switch
    {
        Common.Dto.AcquisitionState.Idle => "Idle",
        Common.Dto.AcquisitionState.Arming => "Arming...",
        Common.Dto.AcquisitionState.Acquiring => "Acquiring",
        Common.Dto.AcquisitionState.Draining => "Draining...",
        Common.Dto.AcquisitionState.Error => "Acquisition Error",
        _ => "Unknown"
    };
}
