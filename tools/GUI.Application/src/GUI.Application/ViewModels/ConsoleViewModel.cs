using System.Windows.Input;
using XrayDetector.Gui.Core;

namespace XrayDetector.Gui.ViewModels;

/// <summary>
/// ViewModel for Console tab (SPEC-GUI-002).
/// Aggregates frame viewer, acquisition control, and scenario runner.
/// </summary>
public sealed class ConsoleViewModel : ObservableObject
{
    private double _windowCenter = 32768.0;
    private double _windowWidth = 65535.0;

    /// <summary>Creates a new ConsoleViewModel.</summary>
    public ConsoleViewModel(
        FramePreviewViewModel framePreview,
        ScenarioRunnerViewModel scenarioRunner)
    {
        FramePreview = framePreview ?? throw new ArgumentNullException(nameof(framePreview));
        ScenarioRunner = scenarioRunner ?? throw new ArgumentNullException(nameof(scenarioRunner));

        StartCommand = new NoOpCommand();
        StopCommand = new NoOpCommand();
        ResetCommand = new NoOpCommand();
        SaveFrameCommand = new NoOpCommand();
        AutoWindowLevelCommand = new RelayCommand(OnAutoWindowLevel);
    }

    /// <summary>Frame preview sub-ViewModel.</summary>
    public FramePreviewViewModel FramePreview { get; }

    /// <summary>Scenario runner sub-ViewModel.</summary>
    public ScenarioRunnerViewModel ScenarioRunner { get; }

    /// <summary>Window center for frame display mapping.</summary>
    public double WindowCenter
    {
        get => _windowCenter;
        set
        {
            if (SetField(ref _windowCenter, value))
                FramePreview.UpdateWindowLevel(_windowCenter, _windowWidth > 0 ? _windowWidth : 1);
        }
    }

    /// <summary>Window width for frame display mapping.</summary>
    public double WindowWidth
    {
        get => _windowWidth;
        set
        {
            if (SetField(ref _windowWidth, value) && _windowWidth > 0)
                FramePreview.UpdateWindowLevel(_windowCenter, _windowWidth);
        }
    }

    /// <summary>Start acquisition command.</summary>
    public ICommand StartCommand { get; set; }

    /// <summary>Stop acquisition command.</summary>
    public ICommand StopCommand { get; set; }

    /// <summary>Reset pipeline command.</summary>
    public ICommand ResetCommand { get; set; }

    /// <summary>Save frame command.</summary>
    public ICommand SaveFrameCommand { get; set; }

    /// <summary>Auto window/level command.</summary>
    public ICommand AutoWindowLevelCommand { get; private set; }

    /// <summary>
    /// Wires acquisition commands from MainViewModel after initialization.
    /// </summary>
    public void SetCommands(ICommand start, ICommand stop, ICommand reset, ICommand saveFrame)
    {
        StartCommand = start;
        StopCommand = stop;
        ResetCommand = reset;
        SaveFrameCommand = saveFrame;
        OnPropertyChanged(nameof(StartCommand));
        OnPropertyChanged(nameof(StopCommand));
        OnPropertyChanged(nameof(ResetCommand));
        OnPropertyChanged(nameof(SaveFrameCommand));
    }

    /// <summary>Refreshes the AutoWindowLevel command enabled state.</summary>
    public void NotifyAutoWindowLevelCanExecuteChanged()
    {
        (AutoWindowLevelCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    private void OnAutoWindowLevel()
    {
        if (!FramePreview.CanSaveFrame()) return;
        FramePreview.AutoWindowLevel();
        _windowCenter = FramePreview.WindowCenter;
        _windowWidth = FramePreview.WindowWidth;
        OnPropertyChanged(nameof(WindowCenter));
        OnPropertyChanged(nameof(WindowWidth));
    }

    /// <summary>Placeholder command that does nothing and is always disabled.</summary>
    private sealed class NoOpCommand : ICommand
    {
#pragma warning disable CS0067 // Required by ICommand interface
        public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067
        public bool CanExecute(object? parameter) => false;
        public void Execute(object? parameter) { }
    }
}
