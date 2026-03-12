using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using XrayDetector.Common.Dto;
using XrayDetector.Gui.Core;
using XrayDetector.Gui.ViewModels;
using XrayDetector.Gui.Views;
using XrayDetector.Implementation;
using XrayDetector.Models;

namespace XrayDetector.Gui.ViewModels;

/// <summary>
/// Main ViewModel for unified WPF interface (REQ-TOOLS-040, REQ-TOOLS-043).
/// Integrates with IDetectorClient for connection management, frame acquisition, status monitoring.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IDetectorClient _detectorClient;
    private readonly DispatcherTimer _statusTimer;
    private bool _isConnected;
    private bool _isAcquiring;
    private string _hostAddress = "127.0.0.1";
    private int _port = 8000;
    private string _statusMessage = "Ready";
    private long _framesReceived;
    private long _droppedFrames;
    private double _windowCenter = 32768.0;
    private double _windowWidth = 65535.0;
    private Frame? _currentFrame;
    private long _throughputSnapshotFrames;
    private DateTime _throughputSnapshotTime = DateTime.UtcNow;
    private bool _isStatusBarVisible = true;
    private bool _isFullScreen;
    private int _selectedTabIndex;
    private bool _isShortcutOverlayVisible;

    /// <summary>
    /// Creates a new MainViewModel.
    /// </summary>
    /// <param name="detectorClient">Detector client for SDK integration (REQ-TOOLS-043).</param>
    public MainViewModel(IDetectorClient detectorClient)
    {
        _detectorClient = detectorClient ?? throw new ArgumentNullException(nameof(detectorClient));

        // Initialize child ViewModels
        StatusViewModel = new StatusViewModel();
        FramePreviewViewModel = new FramePreviewViewModel();
        SimulatorControlViewModel = new SimulatorControlViewModel();
        PipelineStatusViewModel = new PipelineStatusViewModel();
        ScenarioRunnerViewModel = new ScenarioRunnerViewModel();

        // Subscribe to SDK events (REQ-TOOLS-043)
        _detectorClient.ConnectionChanged += OnConnectionChanged;
        _detectorClient.FrameReceived += OnFrameReceived;
        _detectorClient.ErrorOccurred += OnErrorOccurred;

        // Setup status polling timer (REQ-TOOLS-045: 1 Hz minimum)
        _statusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500) // 2 Hz for better responsiveness
        };
        _statusTimer.Tick += OnStatusTimerTick;

        // Initialize commands
        InitializeCommands();
    }

    /// <summary>Status dashboard ViewModel (REQ-TOOLS-045).</summary>
    public StatusViewModel StatusViewModel { get; }

    /// <summary>Frame preview ViewModel (REQ-TOOLS-041, REQ-TOOLS-042).</summary>
    public FramePreviewViewModel FramePreviewViewModel { get; }

    /// <summary>Simulator control ViewModel (REQ-UI-012).</summary>
    public SimulatorControlViewModel SimulatorControlViewModel { get; }

    /// <summary>Pipeline status ViewModel (REQ-UI-013).</summary>
    public PipelineStatusViewModel PipelineStatusViewModel { get; }

    /// <summary>Scenario runner ViewModel (REQ-UI-014).</summary>
    public ScenarioRunnerViewModel ScenarioRunnerViewModel { get; }

    /// <summary>Current connection state.</summary>
    public bool IsConnected
    {
        get => _isConnected;
        private set => SetField(ref _isConnected, value);
    }

    /// <summary>Current acquisition state.</summary>
    public bool IsAcquiring
    {
        get => _isAcquiring;
        private set => SetField(ref _isAcquiring, value);
    }

    /// <summary>Host address for connection.</summary>
    public string HostAddress
    {
        get => _hostAddress;
        set => SetField(ref _hostAddress, value);
    }

    /// <summary>Port number for connection.</summary>
    public int Port
    {
        get => _port;
        set => SetField(ref _port, value);
    }

    /// <summary>Status message for display.</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    /// <summary>Total frames received (for display).</summary>
    public long FramesReceived
    {
        get => _framesReceived;
        private set => SetField(ref _framesReceived, value);
    }

    /// <summary>Total frames dropped (for display).</summary>
    public long DroppedFrames
    {
        get => _droppedFrames;
        private set => SetField(ref _droppedFrames, value);
    }

    /// <summary>Window center for frame preview (REQ-TOOLS-042).</summary>
    public double WindowCenter
    {
        get => _windowCenter;
        set
        {
            if (SetField(ref _windowCenter, value))
            {
                FramePreviewViewModel.UpdateWindowLevel(_windowCenter, _windowWidth);
            }
        }
    }

    /// <summary>Window width for frame preview (REQ-TOOLS-042).</summary>
    public double WindowWidth
    {
        get => _windowWidth;
        set
        {
            if (SetField(ref _windowWidth, value))
            {
                FramePreviewViewModel.UpdateWindowLevel(_windowCenter, _windowWidth);
            }
        }
    }

    /// <summary>Whether the status bar is visible (SPEC-HELP-001).</summary>
    public bool IsStatusBarVisible
    {
        get => _isStatusBarVisible;
        set => SetField(ref _isStatusBarVisible, value);
    }

    /// <summary>Whether the application is in full-screen mode (SPEC-HELP-001).</summary>
    public bool IsFullScreen
    {
        get => _isFullScreen;
        private set => SetField(ref _isFullScreen, value);
    }

    /// <summary>Selected tab index for tab switching (SPEC-HELP-001 Wave 2).</summary>
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetField(ref _selectedTabIndex, value);
    }

    /// <summary>Whether the keyboard shortcut overlay is visible (SPEC-HELP-001 Wave 2).</summary>
    public bool IsShortcutOverlayVisible
    {
        get => _isShortcutOverlayVisible;
        set => SetField(ref _isShortcutOverlayVisible, value);
    }

    /// <summary>Application version string from ApplicationInfo (SPEC-HELP-001).</summary>
    public string AppVersion { get; } = ApplicationInfo.Instance.Version;

    /// <summary>Raised when the full-screen state changes. Param = isFullScreen.</summary>
    public event Action<bool>? FullScreenRequested;

    // Commands (will be initialized by CommunityToolkit.Mvvm source generators)
    public ICommand ConnectCommand { get; private set; } = null!;
    public ICommand DisconnectCommand { get; private set; } = null!;
    public ICommand StartAcquisitionCommand { get; private set; } = null!;
    public ICommand StopAcquisitionCommand { get; private set; } = null!;
    public ICommand SaveFrameCommand { get; private set; } = null!;
    public ICommand AutoWindowLevelCommand { get; private set; } = null!;
    public ICommand OpenConfigCommand { get; private set; } = null!;

    // New commands for SPEC-HELP-001
    public ICommand ExitCommand { get; private set; } = null!;
    public ICommand ShowAboutCommand { get; private set; } = null!;
    public ICommand ToggleStatusBarCommand { get; private set; } = null!;
    public ICommand ToggleFullScreenCommand { get; private set; } = null!;

    // SPEC-HELP-001 Wave 2: Phase 4 commands
    public ICommand ShowHelpCommand { get; private set; } = null!;
    public ICommand SwitchTabCommand { get; private set; } = null!;
    public ICommand ShowShortcutOverlayCommand { get; private set; } = null!;

    private void InitializeCommands()
    {
        ConnectCommand = new RelayCommand(async () => await OnConnectAsync(), () => !IsConnected);
        DisconnectCommand = new RelayCommand(async () => await OnDisconnectAsync(), () => IsConnected);
        StartAcquisitionCommand = new RelayCommand(async () => await OnStartAcquisitionAsync(), () => IsConnected && !IsAcquiring);
        StopAcquisitionCommand = new RelayCommand(async () => await OnStopAcquisitionAsync(), () => IsAcquiring);
        SaveFrameCommand = new RelayCommand<string>(async path => await OnSaveFrameAsync(path), path => CanSaveFrame());
        AutoWindowLevelCommand = new RelayCommand(OnAutoWindowLevel, () => FramePreviewViewModel.CanSaveFrame());
        OpenConfigCommand = new RelayCommand(OnOpenConfig);

        // SPEC-HELP-001: New commands
        ExitCommand = new RelayCommand(() => Application.Current.Shutdown());
        ShowAboutCommand = new RelayCommand(OnShowAbout);
        ToggleStatusBarCommand = new RelayCommand(() => IsStatusBarVisible = !IsStatusBarVisible);
        ToggleFullScreenCommand = new RelayCommand(OnToggleFullScreen);

        // SPEC-HELP-001 Wave 2: Phase 4 commands
        ShowHelpCommand = new RelayCommand(OnShowHelp);
        SwitchTabCommand = new RelayCommand<string>(OnSwitchTab);
        ShowShortcutOverlayCommand = new RelayCommand(OnShowShortcutOverlay);
    }

    private async Task OnConnectAsync()
    {
        try
        {
            StatusMessage = $"Connecting to {HostAddress}:{Port}...";
            await _detectorClient.ConnectAsync(HostAddress, Port);
            _statusTimer.Start();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection failed: {ex.Message}";
        }
    }

    private async Task OnDisconnectAsync()
    {
        try
        {
            StatusMessage = "Disconnecting...";
            _statusTimer.Stop();
            await _detectorClient.DisconnectAsync();
            StatusViewModel.Reset();
            FramesReceived = 0;
            DroppedFrames = 0;
            StatusMessage = "Disconnected";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Disconnect failed: {ex.Message}";
        }
    }

    private async Task OnStartAcquisitionAsync()
    {
        try
        {
            StatusMessage = "Starting acquisition...";
            await _detectorClient.StartAcquisitionAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Start acquisition failed: {ex.Message}";
        }
    }

    private async Task OnStopAcquisitionAsync()
    {
        try
        {
            StatusMessage = "Stopping acquisition...";
            await _detectorClient.StopAcquisitionAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Stop acquisition failed: {ex.Message}";
        }
    }

    private async Task OnSaveFrameAsync(string? path)
    {
        if (_currentFrame == null || string.IsNullOrEmpty(path))
            return;

        try
        {
            StatusMessage = $"Saving frame to {path}...";
            await _detectorClient.SaveFrameAsync(_currentFrame, path, ImageFormat.Tiff);
            StatusMessage = $"Frame saved to {path}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
    }

    private bool CanSaveFrame() => _currentFrame != null;

    private void OnAutoWindowLevel()
    {
        FramePreviewViewModel.AutoWindowLevel();
        WindowCenter = FramePreviewViewModel.WindowCenter;
        WindowWidth = FramePreviewViewModel.WindowWidth;
    }

    private void OnShowAbout()
    {
        var dialog = new AboutWindow
        {
            DataContext = new AboutViewModel(new ClipboardService()),
            Owner = Application.Current.MainWindow
        };
        dialog.ShowDialog();
    }

    private void OnToggleFullScreen()
    {
        IsFullScreen = !IsFullScreen;
        FullScreenRequested?.Invoke(IsFullScreen);
    }

    private void OnShowHelp()
    {
        // Open HelpWindow singleton - actual window creation handled in code-behind
        HelpRequested?.Invoke();
    }

    private void OnSwitchTab(string? indexStr)
    {
        if (int.TryParse(indexStr, out int index))
            SelectedTabIndex = index;
    }

    private void OnShowShortcutOverlay()
    {
        IsShortcutOverlayVisible = !IsShortcutOverlayVisible;
    }

    /// <summary>Raised when help window should be shown.</summary>
    public event Action? HelpRequested;

    private void OnOpenConfig()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open Configuration File",
            Filter = "Configuration Files (*.json;*.yaml;*.yml)|*.json;*.yaml;*.yml|JSON Files (*.json)|*.json|YAML Files (*.yaml;*.yml)|*.yaml;*.yml|All Files (*.*)|*.*",
            DefaultExt = ".json",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            StatusMessage = $"Config loaded: {System.IO.Path.GetFileName(dialog.FileName)}";
            // TODO: parse and apply config from dialog.FileName
        }
    }

    private void OnConnectionChanged(object? sender, ConnectionStateChangedEventArgs e)
    {
        IsConnected = e.IsConnected;
        StatusMessage = e.IsConnected ? "Connected" : "Disconnected";

        // Refresh command states
        (ConnectCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (DisconnectCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (StartAcquisitionCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    private void OnFrameReceived(object? sender, FrameReceivedEventArgs e)
    {
        // Update frame count (REQ-TOOLS-041)
        FramesReceived++;

        // Update preview (REQ-TOOLS-041: up to 15 fps)
        _currentFrame = e.Frame;
        FramePreviewViewModel.SetFrame(e.Frame);

        // Update stats
        OnPropertyChanged(nameof(FramesReceived));
        (SaveFrameCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (AutoWindowLevelCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    private void OnErrorOccurred(object? sender, ErrorOccurredEventArgs e)
    {
        StatusMessage = $"Error: {e.Message}";
    }

    private async void OnStatusTimerTick(object? sender, EventArgs e)
    {
        // Update status dashboard at 2 Hz (REQ-TOOLS-045: 1 Hz minimum)
        try
        {
            var status = await _detectorClient.GetStatusAsync();
            StatusViewModel.Update(status, FramesReceived, DroppedFrames, CalculateThroughput());
        }
        catch
        {
            // Ignore status update errors
        }
    }

    private double CalculateThroughput()
    {
        // Calculate throughput in Gbps based on actual frame rate since last snapshot.
        // Assumes 16-bit (2 bytes) per pixel; uses _currentFrame dimensions when available.
        var now = DateTime.UtcNow;
        double elapsedSeconds = (now - _throughputSnapshotTime).TotalSeconds;

        if (elapsedSeconds < 0.5 || _currentFrame == null)
            return 0.0;

        long framesDelta = _framesReceived - _throughputSnapshotFrames;
        double fps = framesDelta / elapsedSeconds;
        double bytesPerFrame = (double)_currentFrame.Width * _currentFrame.Height * 2; // 16-bit
        double gbps = fps * bytesPerFrame * 8.0 / 1e9;

        // Update snapshot for next call
        _throughputSnapshotFrames = _framesReceived;
        _throughputSnapshotTime = now;

        return Math.Max(0.0, gbps);
    }

    // Test helpers (for TDD support)
    internal void SetIsConnectedForTesting(bool isConnected) => IsConnected = isConnected;
    internal void SetIsAcquiringForTesting(bool isAcquiring) => IsAcquiring = isAcquiring;
    internal void SetCurrentFrameForTesting(Frame? frame) => _currentFrame = frame;

    // Event handler exposure for testing
    internal void OnConnectionChangedHandler(ConnectionStateChangedEventArgs e) => OnConnectionChanged(this, e);
    internal void OnFrameReceivedHandler(FrameReceivedEventArgs e) => OnFrameReceived(this, e);
}

/// <summary>
/// Simple RelayCommand implementation for MVVM commands.
/// </summary>
internal class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();

    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// Generic RelayCommand for commands with parameters.
/// </summary>
internal class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;

    public void Execute(object? parameter) => _execute((T?)parameter);

    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
