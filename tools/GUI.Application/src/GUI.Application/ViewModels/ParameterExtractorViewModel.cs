// @MX:NOTE: PDF 데이터시트 파라미터 추출용 ViewModel (REQ-UI-013)
// ParameterExtractor.Core를 통합 GUI에 통합합니다
// @MX:ANCHOR: MVVM 패턴으로 파라미터 추출 UI 상태 관리
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using IntegrationRunner.Core.Models;
using Microsoft.Win32;
using ParamExtractorModels = ParameterExtractor.Core.Models;
using XrayDetector.Gui.Core;
using XrayDetector.Gui.Services;

namespace XrayDetector.Gui.ViewModels;

/// <summary>
/// ViewModel for PDF datasheet parameter extraction (REQ-UI-013).
/// Integrates ParameterExtractor.Core into the unified GUI.
/// </summary>
public sealed class ParameterExtractorViewModel : ObservableObject
{
    private readonly ParameterExtractorService _service;
    private string _sourceFilePath = string.Empty;
    private string _statusMessage = "Load a PDF datasheet to extract parameters.";
    private bool _isBusy;
    private ParamExtractorModels.ParameterInfo? _selectedParameter;

    /// <summary>
    /// Creates a new ParameterExtractorViewModel.
    /// </summary>
    public ParameterExtractorViewModel()
    {
        _service = new ParameterExtractorService();
        Parameters = new ObservableCollection<ParamExtractorModels.ParameterInfo>();
        ValidationSummary = new ObservableCollection<ValidationSummaryItem>();

        LoadPdfCommand = new RelayCommand(async _ => await OnLoadPdfAsync(), _ => !IsBusy);
        ApplyToSimulatorCommand = new RelayCommand(_ => OnApplyToSimulator(), _ => Parameters.Any(p => p.ValidationStatus == ParamExtractorModels.ValidationStatus.Valid || p.ValidationStatus == ParamExtractorModels.ValidationStatus.Warning));
        ClearCommand = new RelayCommand(_ => OnClear(), _ => Parameters.Any());
    }

    /// <summary>
    /// Extracted parameters from the PDF.
    /// </summary>
    public ObservableCollection<ParamExtractorModels.ParameterInfo> Parameters { get; }

    /// <summary>
    /// Validation summary statistics.
    /// </summary>
    public ObservableCollection<ValidationSummaryItem> ValidationSummary { get; }

    /// <summary>
    /// Source PDF file path.
    /// </summary>
    public string SourceFilePath
    {
        get => _sourceFilePath;
        set => SetField(ref _sourceFilePath, value);
    }

    /// <summary>
    /// Current status message for user feedback.
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    /// <summary>
    /// Indicates if an operation is in progress.
    /// </summary>
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetField(ref _isBusy, value))
            {
                // Refresh command states
                (LoadPdfCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (ApplyToSimulatorCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Currently selected parameter in the grid.
    /// </summary>
    public ParamExtractorModels.ParameterInfo? SelectedParameter
    {
        get => _selectedParameter;
        set => SetField(ref _selectedParameter, value);
    }

    /// <summary>
    /// Command to load and parse a PDF datasheet.
    /// </summary>
    // @MX:ANCHOR: PDF 로드 명령 - ParameterExtractorView.xaml에서 바인딩
    public ICommand LoadPdfCommand { get; }

    /// <summary>
    /// Command to apply extracted parameters to simulator control.
    /// </summary>
    // @MX:ANCHOR: 시뮬레이터 적용 명령 - ParameterExtractorView.xaml에서 바인딩
    public ICommand ApplyToSimulatorCommand { get; }

    /// <summary>
    /// Command to clear all extracted parameters.
    /// </summary>
    // @MX:ANCHOR: 초기화 명령 - ParameterExtractorView.xaml에서 바인딩
    public ICommand ClearCommand { get; }

    /// <summary>
    /// Event raised when parameters should be applied to the simulator.
    /// </summary>
    public event EventHandler<IntegrationRunner.Core.Models.DetectorConfig>? ParametersApplied;

    /// <summary>
    /// Gets validation counts for UI display.
    /// </summary>
    public (int Valid, int Warning, int Error, int Pending) ValidationCounts
    {
        get
        {
            var grouped = Parameters.GroupBy(p => p.ValidationStatus)
                .ToDictionary(g => g.Key, g => g.Count());

            return (
                grouped.GetValueOrDefault(ParamExtractorModels.ValidationStatus.Valid, 0),
                grouped.GetValueOrDefault(ParamExtractorModels.ValidationStatus.Warning, 0),
                grouped.GetValueOrDefault(ParamExtractorModels.ValidationStatus.Error, 0),
                grouped.GetValueOrDefault(ParamExtractorModels.ValidationStatus.Pending, 0)
            );
        }
    }

    private async Task OnLoadPdfAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Panel Datasheet PDF",
            Filter = "PDF Files (*.pdf)|*.pdf|All Files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
            return;

        IsBusy = true;
        StatusMessage = $"Parsing {Path.GetFileName(dialog.FileName)}...";

        try
        {
            var result = await _service.ParsePdfAsync(dialog.FileName);

            if (result.IsSuccessful)
            {
                Parameters.Clear();

                // Sort by category for better UX
                var sortedParams = result.Parameters
                    .OrderBy(p => p.Category)
                    .ThenBy(p => p.Name);

                foreach (var param in sortedParams)
                {
                    Parameters.Add(param);
                }

                SourceFilePath = dialog.FileName;
                UpdateValidationSummary();

                var counts = ValidationCounts;
                StatusMessage = $"Extracted {result.ExtractedCount} parameters: {counts.Valid} valid, {counts.Warning} warnings, {counts.Error} errors.";
            }
            else
            {
                StatusMessage = $"Parse failed: {string.Join(", ", result.Messages)}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnApplyToSimulator()
    {
        try
        {
            var config = _service.ToDetectorConfig(Parameters);

            // Raise event for MainViewModel to handle
            ParametersApplied?.Invoke(this, config);

            var counts = ValidationCounts;
            StatusMessage = $"Applied {counts.Valid + counts.Warning} parameters to simulator.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Apply failed: {ex.Message}";
        }
    }

    private void OnClear()
    {
        Parameters.Clear();
        ValidationSummary.Clear();
        SourceFilePath = string.Empty;
        StatusMessage = "Cleared. Load a PDF datasheet to extract parameters.";
    }

    private void UpdateValidationSummary()
    {
        ValidationSummary.Clear();

        var counts = ValidationCounts;
        if (counts.Valid > 0)
            ValidationSummary.Add(new ValidationSummaryItem("Valid", counts.Valid, ParamExtractorModels.ValidationStatus.Valid));
        if (counts.Warning > 0)
            ValidationSummary.Add(new ValidationSummaryItem("Warning", counts.Warning, ParamExtractorModels.ValidationStatus.Warning));
        if (counts.Error > 0)
            ValidationSummary.Add(new ValidationSummaryItem("Error", counts.Error, ParamExtractorModels.ValidationStatus.Error));
        if (counts.Pending > 0)
            ValidationSummary.Add(new ValidationSummaryItem("Pending", counts.Pending, ParamExtractorModels.ValidationStatus.Pending));

        OnPropertyChanged(nameof(ValidationCounts));
    }

    /// <summary>
    /// Internal RelayCommand with CanExecuteChanged support.
    /// </summary>
    private sealed class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool> _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute ?? (_ => true);
        }

        public bool CanExecute(object? parameter) => _canExecute(parameter);

        public void Execute(object? parameter) => _execute(parameter);

        public event EventHandler? CanExecuteChanged;

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// Summary item for validation statistics display.
/// </summary>
public sealed class ValidationSummaryItem
{
    public string Label { get; }
    public int Count { get; }
    public ParamExtractorModels.ValidationStatus Status { get; }

    public ValidationSummaryItem(string label, int count, ParamExtractorModels.ValidationStatus status)
    {
        Label = label;
        Count = count;
        Status = status;
    }
}
