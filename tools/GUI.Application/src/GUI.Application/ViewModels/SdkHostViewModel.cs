using System.Collections.ObjectModel;
using System.Windows.Input;
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

        // Subscribe to StatusViewModel changes to re-emit IsReady
        Status.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(StatusViewModel.IsConnected))
                OnPropertyChanged(nameof(IsReady));
        };
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

    /// <summary>Connect command — wired from MainViewModel.</summary>
    public ICommand ConnectCommand { get; private set; } = new NoOpCommand();

    /// <summary>Disconnect command — wired from MainViewModel.</summary>
    public ICommand DisconnectCommand { get; private set; } = new NoOpCommand();

    /// <summary>Wires SDK connect/disconnect commands from MainViewModel.</summary>
    public void SetCommands(ICommand connect, ICommand disconnect)
    {
        ConnectCommand = connect;
        DisconnectCommand = disconnect;
        OnPropertyChanged(nameof(ConnectCommand));
        OnPropertyChanged(nameof(DisconnectCommand));
    }

    /// <summary>Recently saved file paths (most recent first, max 10 entries).</summary>
    public ObservableCollection<string> RecentSaves { get; } = [];

    /// <summary>
    /// Records a saved file path into the recent saves list.
    /// Keeps at most 10 entries; duplicates are moved to the front.
    /// </summary>
    public void AddRecentSave(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        RecentSaves.Remove(path);
        RecentSaves.Insert(0, path);
        while (RecentSaves.Count > 10)
            RecentSaves.RemoveAt(RecentSaves.Count - 1);
    }

    /// <summary>Indicates whether this module is ready for integration run.</summary>
    public bool IsReady => Status.IsConnected;

    private sealed class NoOpCommand : ICommand
    {
#pragma warning disable CS0067
        public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067
        public bool CanExecute(object? parameter) => false;
        public void Execute(object? parameter) { }
    }
}
