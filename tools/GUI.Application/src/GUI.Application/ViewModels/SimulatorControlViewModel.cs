using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using IntegrationRunner.Core.Models;
using IntegrationRunner.Core.Network;
using Microsoft.Win32;
using XrayDetector.Gui.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace XrayDetector.Gui.ViewModels;

/// <summary>
/// ViewModel for simulator parameter control (REQ-UI-012).
/// Provides parameter binding for Panel, FPGA, MCU, and Network settings.
/// </summary>
public sealed class SimulatorControlViewModel : ObservableObject
{
    // Default values from SPEC
    private const int DefaultRows = 1024;
    private const int DefaultCols = 1024;
    private const int DefaultBitDepth = 14;
    private const double DefaultKvp = 80.0;
    private const double DefaultMas = 1.0;
    private const int DefaultFrameBufferCount = 4;
    private const double DefaultPacketLossRate = 0.0;
    private const double DefaultReorderRate = 0.0;
    private const double DefaultCorruptionRate = 0.0;
    private const int DefaultMinDelayMs = 0;
    private const int DefaultMaxDelayMs = 0;

    // Panel parameters
    private int _panelRows = DefaultRows;
    private int _panelCols = DefaultCols;
    private int _panelBitDepth = DefaultBitDepth;
    private double _panelKvp = DefaultKvp;
    private double _panelMas = DefaultMas;

    // MCU parameters
    private int _frameBufferCount = DefaultFrameBufferCount;
    private string _mcuBufferState = "Free";

    // Network parameters
    private double _packetLossRate = DefaultPacketLossRate;
    private double _reorderRate = DefaultReorderRate;
    private double _corruptionRate = DefaultCorruptionRate;
    private int _minDelayMs = DefaultMinDelayMs;
    private int _maxDelayMs = DefaultMaxDelayMs;

    /// <summary>
    /// Panel rows (detector height in pixels).
    /// </summary>
    public int PanelRows
    {
        get => _panelRows;
        set => SetField(ref _panelRows, value);
    }

    /// <summary>
    /// Panel columns (detector width in pixels).
    /// </summary>
    public int PanelCols
    {
        get => _panelCols;
        set => SetField(ref _panelCols, value);
    }

    /// <summary>
    /// Panel bit depth (bits per pixel).
    /// </summary>
    public int PanelBitDepth
    {
        get => _panelBitDepth;
        set => SetField(ref _panelBitDepth, value);
    }

    /// <summary>
    /// X-ray tube peak voltage (kVp). Valid range: 40-150.
    /// </summary>
    public double PanelKvp
    {
        get => _panelKvp;
        set => SetField(ref _panelKvp, Math.Clamp(value, 40, 150));
    }

    /// <summary>
    /// X-ray tube current (mAs).
    /// </summary>
    public double PanelMas
    {
        get => _panelMas;
        set => SetField(ref _panelMas, value);
    }

    /// <summary>
    /// MCU frame buffer count. Valid range: 1-8.
    /// </summary>
    public int FrameBufferCount
    {
        get => _frameBufferCount;
        set => SetField(ref _frameBufferCount, Math.Clamp(value, 1, 8));
    }

    /// <summary>
    /// MCU buffer state (Free/Filling/Ready/Sending).
    /// </summary>
    public string McuBufferState
    {
        get => _mcuBufferState;
        set => SetField(ref _mcuBufferState, value);
    }

    /// <summary>
    /// Network packet loss rate. Valid range: 0.0-1.0.
    /// </summary>
    public double PacketLossRate
    {
        get => _packetLossRate;
        set => SetField(ref _packetLossRate, Math.Clamp(value, 0.0, 1.0));
    }

    /// <summary>
    /// Network packet reorder rate. Valid range: 0.0-1.0.
    /// </summary>
    public double ReorderRate
    {
        get => _reorderRate;
        set => SetField(ref _reorderRate, Math.Clamp(value, 0.0, 1.0));
    }

    /// <summary>
    /// Network packet corruption rate. Valid range: 0.0-1.0.
    /// </summary>
    public double CorruptionRate
    {
        get => _corruptionRate;
        set => SetField(ref _corruptionRate, Math.Clamp(value, 0.0, 1.0));
    }

    /// <summary>
    /// Minimum network delay in milliseconds.
    /// </summary>
    public int MinDelayMs
    {
        get => _minDelayMs;
        set => SetField(ref _minDelayMs, value);
    }

    /// <summary>
    /// Maximum network delay in milliseconds.
    /// </summary>
    public int MaxDelayMs
    {
        get => _maxDelayMs;
        set => SetField(ref _maxDelayMs, value);
    }

    /// <summary>
    /// Start simulator command.
    /// </summary>
    public ICommand StartCommand { get; private set; }

    /// <summary>
    /// Stop simulator command.
    /// </summary>
    public ICommand StopCommand { get; private set; }

    /// <summary>
    /// Reset simulator command.
    /// </summary>
    public ICommand ResetCommand { get; private set; }

    /// <summary>
    /// Load configuration from YAML file command.
    /// </summary>
    public ICommand LoadConfigCommand { get; private set; }

    /// <summary>
    /// Save configuration to YAML file command.
    /// </summary>
    public ICommand SaveConfigCommand { get; private set; }

    /// <summary>
    /// Creates a new SimulatorControlViewModel.
    /// </summary>
    public SimulatorControlViewModel()
    {
        // Placeholder commands replaced by MainViewModel.WireCommands() (SPEC-GUI-001 MVP-1)
        StartCommand = new RelayCommand(() => { }, () => true);
        StopCommand = new RelayCommand(() => { }, () => false);
        ResetCommand = new RelayCommand(() => { }, () => true);
        LoadConfigCommand = new RelayCommand(async () => await OnLoadConfigAsync(), () => true);
        SaveConfigCommand = new RelayCommand(async () => await OnSaveConfigAsync(), () => true);
    }

    /// <summary>
    /// Wires Start/Stop/Reset commands to actual pipeline control.
    /// Called by MainViewModel after initialization (SPEC-GUI-001 MVP-1).
    /// </summary>
    public void SetCommands(ICommand start, ICommand stop, ICommand reset)
    {
        StartCommand = start;
        StopCommand = stop;
        ResetCommand = reset;
    }

    /// <summary>
    /// Converts current parameters to DetectorConfig.
    /// </summary>
    public DetectorConfig ToDetectorConfig()
    {
        return new DetectorConfig
        {
            Panel = new PanelConfig
            {
                Rows = _panelRows,
                Cols = _panelCols,
                BitDepth = _panelBitDepth
            },
            Source = new SourceConfig
            {
                KVp = _panelKvp,
                MAs = _panelMas
            },
            Fpga = new FpgaConfig
            {
                Csi2Lanes = 4,
                Csi2DataRateMbps = 1500,
                LineBufferDepth = 1024
            },
            Soc = new SocConfig
            {
                FrameBufferCount = _frameBufferCount,
                UdpPort = 8001,
                EthernetPort = 9001
            },
            Simulation = new SimulationConfig
            {
                MaxFrames = 100 // Default
            }
        };
    }

    /// <summary>
    /// Updates properties from an existing DetectorConfig.
    /// </summary>
    public void UpdateFromConfig(DetectorConfig config)
    {
        if (config?.Panel != null)
        {
            PanelRows = config.Panel.Rows;
            PanelCols = config.Panel.Cols;
            PanelBitDepth = config.Panel.BitDepth;
        }

        if (config?.Source != null)
        {
            if (config.Source.KVp > 0) PanelKvp = config.Source.KVp;
            if (config.Source.MAs > 0) PanelMas = config.Source.MAs;
        }

        if (config?.Soc != null)
        {
            FrameBufferCount = config.Soc.FrameBufferCount;
        }

        if (config?.Simulation != null)
        {
            // Update other simulation parameters if needed
        }
    }

    /// <summary>
    /// Loads configuration from YAML file.
    /// </summary>
    private async Task OnLoadConfigAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Load Detector Configuration",
            Filter = "YAML Files (*.yaml;*.yml)|*.yaml;*.yml|All Files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var yaml = await File.ReadAllTextAsync(dialog.FileName);
            var config = deserializer.Deserialize<DetectorConfig>(yaml);

            if (config != null)
            {
                UpdateFromConfig(config);
                Debug.WriteLine($"Loaded configuration from {dialog.FileName}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Load config error: {ex.Message}");
            // TODO: Show error dialog to user
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Saves configuration to YAML file.
    /// </summary>
    private async Task OnSaveConfigAsync()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save Detector Configuration",
            Filter = "YAML Files (*.yaml)|*.yaml|All Files (*.*)|*.*",
            FileName = "detector_config.yaml",
            DefaultExt = "yaml"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var config = ToDetectorConfig();
            var yaml = serializer.Serialize(config);

            await File.WriteAllTextAsync(dialog.FileName, yaml);
            Debug.WriteLine($"Saved configuration to {dialog.FileName}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Save config error: {ex.Message}");
            // TODO: Show error dialog to user
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Internal RelayCommand implementation.
    /// </summary>
    private class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute ?? (() => true);
        }

#pragma warning disable CS0067 // CanExecuteChanged is required by ICommand interface
        public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067

        public bool CanExecute(object? parameter) => _canExecute();

        public void Execute(object? parameter) => _execute();
    }
}
