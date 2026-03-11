using System.Windows.Input;
using XrayDetector.Gui.Core;
using XrayDetector.Gui.Services;

namespace XrayDetector.Gui.ViewModels;

/// <summary>
/// ViewModel for scenario execution (REQ-UI-014).
/// Provides scenario selection, execution control, and result display.
/// </summary>
public sealed class ScenarioRunnerViewModel : ObservableObject
{
    private readonly ScenarioRunner _runner;
    private List<ScenarioDefinition> _scenarios;
    private ScenarioDefinition? _selectedScenario;
    private int _progress;
    private ScenarioResult? _result;
    private bool _isExecuting;

    public List<ScenarioDefinition> Scenarios
    {
        get => _scenarios;
        set => SetField(ref _scenarios, value);
    }

    public ScenarioDefinition? SelectedScenario
    {
        get => _selectedScenario;
        set => SetField(ref _selectedScenario, value);
    }

    public int Progress
    {
        get => _progress;
        set => SetField(ref _progress, value);
    }

    public ScenarioResult? Result
    {
        get => _result;
        set => SetField(ref _result, value);
    }

    public ICommand ExecuteCommand { get; private set; }

    public ScenarioRunnerViewModel()
    {
        _runner = new ScenarioRunner();
        _scenarios = ScenarioRunner.GetPredefinedScenarios();
        _selectedScenario = _scenarios.FirstOrDefault();

        ExecuteCommand = new RelayCommand(async () => await OnExecuteAsync(), () => !_isExecuting);
    }

    private async Task OnExecuteAsync()
    {
        if (_selectedScenario == null)
            return;

        _isExecuting = true;
        Progress = 0;
        Result = null;

        var progress = new Progress<int>(p => Progress = p);
        Result = await _runner.ExecuteScenarioAsync(_selectedScenario, progress, CancellationToken.None);

        _isExecuting = false;
    }

    private class RelayCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Func<Task> execute, Func<bool> canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute ?? (() => true);
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute();

        public async void Execute(object? parameter) => await _execute();
    }
}
