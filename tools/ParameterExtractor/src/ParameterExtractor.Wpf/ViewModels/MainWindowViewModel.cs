using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using ParameterExtractor.Core.Models;
using ParameterExtractor.Core.Services;

namespace ParameterExtractor.Wpf.ViewModels;

/// <summary>
/// Main window ViewModel for ParameterExtractor WPF application.
/// Implements MVVM pattern with INotifyPropertyChanged.
/// </summary>
public class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IPdfParser _pdfParser;
    private readonly IRuleValidator _ruleValidator;
    private readonly IConfigExporter _configExporter;

    private string _sourceFilePath = string.Empty;
    private string _statusMessage = "Ready";
    private bool _isBusy;
    private ParameterInfo? _selectedParameter;

    public MainWindowViewModel()
    {
        _pdfParser = new PdfParser();
        _ruleValidator = new RuleValidator();
        _configExporter = new ConfigExporter();

        Parameters = new ObservableCollection<ParameterInfo>();
        ValidationSummary = new ObservableCollection<ValidationSummaryItem>();

        // Initialize commands
        LoadPdfCommand = new RelayCommand(async _ => await LoadPdfAsync(), _ => !IsBusy);
        ValidateAllCommand = new RelayCommand(_ => ValidateAll(), _ => Parameters.Any());
        ExportCommand = new RelayCommand(async _ => await ExportAsync(), _ => Parameters.Any() && !HasErrors());
        AddParameterCommand = new RelayCommand(_ => AddParameter());
        RemoveParameterCommand = new RelayCommand(_ => RemoveParameter(), _ => SelectedParameter != null);
        EditParameterCommand = new RelayCommand(_ => EditParameter(), _ => SelectedParameter != null);
    }

    public ObservableCollection<ParameterInfo> Parameters { get; }

    public ObservableCollection<ValidationSummaryItem> ValidationSummary { get; }

    public string SourceFilePath
    {
        get => _sourceFilePath;
        set
        {
            _sourceFilePath = value;
            OnPropertyChanged(nameof(SourceFilePath));
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged(nameof(StatusMessage));
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
            OnPropertyChanged(nameof(IsBusy));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public ParameterInfo? SelectedParameter
    {
        get => _selectedParameter;
        set
        {
            _selectedParameter = value;
            OnPropertyChanged(nameof(SelectedParameter));
        }
    }

    public ICommand LoadPdfCommand { get; }
    public ICommand ValidateAllCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand AddParameterCommand { get; }
    public ICommand RemoveParameterCommand { get; }
    public ICommand EditParameterCommand { get; }

    /// <summary>
    /// Gets the count of parameters by validation status.
    /// </summary>
    public (int Valid, int Warning, int Error, int Pending) ValidationCounts
    {
        get
        {
            var grouped = Parameters.GroupBy(p => p.ValidationStatus)
                .ToDictionary(g => g.Key, g => g.Count());

            return (
                grouped.GetValueOrDefault(ValidationStatus.Valid, 0),
                grouped.GetValueOrDefault(ValidationStatus.Warning, 0),
                grouped.GetValueOrDefault(ValidationStatus.Error, 0),
                grouped.GetValueOrDefault(ValidationStatus.Pending, 0)
            );
        }
    }

    private async Task LoadPdfAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select PDF Datasheet",
            Filter = "PDF Files (*.pdf)|*.pdf|All Files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
            return;

        IsBusy = true;
        StatusMessage = $"Loading {Path.GetFileName(dialog.FileName)}...";

        try
        {
            var result = await _pdfParser.ParseAsync(dialog.FileName);

            if (result.IsSuccessful)
            {
                Parameters.Clear();
                foreach (var param in result.Parameters)
                {
                    Parameters.Add(param);
                }

                SourceFilePath = dialog.FileName;
                StatusMessage = $"Extracted {result.ExtractedCount} parameters from PDF.";

                // Auto-validate on load
                ValidateAll();
            }
            else
            {
                StatusMessage = $"Error: {string.Join(", ", result.Messages)}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            Debug.WriteLine($"LoadPdf error: {ex}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ValidateAll()
    {
        ValidationSummary.Clear();

        foreach (var param in Parameters)
        {
            _ruleValidator.Validate(param);
        }

        // Update validation summary
        var counts = ValidationCounts;
        ValidationSummary.Add(new ValidationSummaryItem("Valid", counts.Valid, ValidationStatus.Valid));
        ValidationSummary.Add(new ValidationSummaryItem("Warning", counts.Warning, ValidationStatus.Warning));
        ValidationSummary.Add(new ValidationSummaryItem("Error", counts.Error, ValidationStatus.Error));
        ValidationSummary.Add(new ValidationSummaryItem("Pending", counts.Pending, ValidationStatus.Pending));

        StatusMessage = $"Validation complete: {counts.Valid} valid, {counts.Warning} warnings, {counts.Error} errors.";
        OnPropertyChanged(nameof(ValidationCounts));
    }

    private async Task ExportAsync()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export detector_config.yaml",
            Filter = "YAML Files (*.yaml)|*.yaml|All Files (*.*)|*.*",
            FileName = "detector_config.yaml",
            DefaultExt = "yaml"
        };

        if (dialog.ShowDialog() != true)
            return;

        // Determine schema path
        var schemaPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", "..", "config", "schema", "detector-config-schema.json");

        if (!File.Exists(schemaPath))
        {
            // Try relative to solution
            schemaPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "..", "..", "config", "schema", "detector-config-schema.json");
        }

        IsBusy = true;
        StatusMessage = "Exporting configuration...";

        try
        {
            var result = await _configExporter.ExportAsync(
                Parameters,
                dialog.FileName,
                File.Exists(schemaPath) ? schemaPath : null);

            if (result.IsSuccessful)
            {
                StatusMessage = $"Successfully exported to {dialog.FileName}";
            }
            else
            {
                var errorMsg = string.Join("\n", result.ValidationErrors.Select(e => $"{e.Path}: {e.Message}"));
                StatusMessage = $"Export failed:\n{errorMsg}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export error: {ex.Message}";
            Debug.WriteLine($"Export error: {ex}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void AddParameter()
    {
        var param = new ParameterInfo
        {
            Name = "New Parameter",
            Value = "0",
            Unit = string.Empty,
            Category = "unknown"
        };

        Parameters.Add(param);
        SelectedParameter = param;
    }

    private void RemoveParameter()
    {
        if (SelectedParameter != null)
        {
            Parameters.Remove(SelectedParameter);
            SelectedParameter = null;
        }
    }

    private void EditParameter()
    {
        // For a full implementation, this would open a dialog
        // For now, inline editing in DataGrid is sufficient
        StatusMessage = "Edit parameters directly in the grid.";
    }

    private bool HasErrors()
    {
        return Parameters.Any(p => p.ValidationStatus == ValidationStatus.Error);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Summary item for validation statistics.
/// </summary>
public class ValidationSummaryItem
{
    public string Label { get; }
    public int Count { get; }
    public ValidationStatus Status { get; }

    public ValidationSummaryItem(string label, int count, ValidationStatus status)
    {
        Label = label;
        Count = count;
        Status = status;
    }
}

/// <summary>
/// Simple ICommand implementation for MVVM commands.
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
