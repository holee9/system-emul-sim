using XrayDetector.Gui.Core;

namespace XrayDetector.Gui.ViewModels;

/// <summary>
/// ViewModel for Host PC (SDK) tab (SPEC-GUI-002).
/// Manages DetectorClient connection, frame storage, and SDK configuration.
/// </summary>
public sealed class SdkHostViewModel : ObservableObject
{
    private string _outputDirectory = string.Empty;
    private string _outputFormat = "TIFF";
    private int _maxFrames = 100;
    private bool _autoReconnect = true;
    private int _maxRetries = 3;
    private string _clientMode = "SimulatedDetectorClient";

    /// <summary>Creates a new SdkHostViewModel.</summary>
    public SdkHostViewModel(StatusViewModel status)
    {
        Status = status ?? throw new ArgumentNullException(nameof(status));
    }

    /// <summary>
    /// StatusViewModel providing SDK connection state and frame statistics.
    /// Exposes: ConnectionState, AcquisitionState, FramesReceived, DroppedFrames,
    ///          ThroughputGbps, DropRatePercentage, IsConnected, IsAcquiring.
    /// </summary>
    public StatusViewModel Status { get; }

    /// <summary>Output directory for saved frames.</summary>
    public string OutputDirectory
    {
        get => _outputDirectory;
        set => SetField(ref _outputDirectory, value);
    }

    /// <summary>Output format for saved frames (TIFF or RAW).</summary>
    public string OutputFormat
    {
        get => _outputFormat;
        set => SetField(ref _outputFormat, value);
    }

    /// <summary>Maximum frames to capture per acquisition run.</summary>
    public int MaxFrames
    {
        get => _maxFrames;
        set => SetField(ref _maxFrames, Math.Max(1, value));
    }

    /// <summary>Whether to automatically reconnect on connection loss.</summary>
    public bool AutoReconnect
    {
        get => _autoReconnect;
        set => SetField(ref _autoReconnect, value);
    }

    /// <summary>Maximum reconnection retry count.</summary>
    public int MaxRetries
    {
        get => _maxRetries;
        set => SetField(ref _maxRetries, Math.Max(0, value));
    }

    /// <summary>
    /// Active client mode: SimulatedDetectorClient or PipelineDetectorClient.
    /// </summary>
    public string ClientMode
    {
        get => _clientMode;
        set => SetField(ref _clientMode, value);
    }

    /// <summary>Available client modes for ComboBox binding.</summary>
    public string[] ClientModes { get; } = ["SimulatedDetectorClient", "PipelineDetectorClient"];

    /// <summary>Indicates whether this module is ready for integration run.</summary>
    public bool IsReady => Status.IsConnected;
}
