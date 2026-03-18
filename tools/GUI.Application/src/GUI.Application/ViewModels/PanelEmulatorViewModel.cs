using XrayDetector.Gui.Core;

namespace XrayDetector.Gui.ViewModels;

/// <summary>
/// ViewModel for Panel tab (SPEC-GUI-002).
/// Combines sensor spec parameters, X-ray source settings, and PDF extraction.
/// </summary>
public sealed class PanelEmulatorViewModel : ObservableObject
{
    /// <summary>Creates a new PanelEmulatorViewModel.</summary>
    public PanelEmulatorViewModel(
        SimulatorControlViewModel simulatorControl,
        ParameterExtractorViewModel parameterExtractor)
    {
        SimulatorControl = simulatorControl ?? throw new ArgumentNullException(nameof(simulatorControl));
        ParameterExtractor = parameterExtractor ?? throw new ArgumentNullException(nameof(parameterExtractor));

        // Re-emit IsReady when panel parameters change
        SimulatorControl.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SimulatorControlViewModel.PanelRows)
                or nameof(SimulatorControlViewModel.PanelCols)
                or nameof(SimulatorControlViewModel.PanelBitDepth))
                OnPropertyChanged(nameof(IsReady));
        };
    }

    /// <summary>
    /// SimulatorControlViewModel providing panel and source parameters.
    /// Exposes: PanelRows, PanelCols, PanelBitDepth, PanelKvp, PanelMas, PanelPixelPitchUm.
    /// </summary>
    public SimulatorControlViewModel SimulatorControl { get; }

    /// <summary>
    /// ParameterExtractorViewModel for PDF datasheet parameter extraction.
    /// </summary>
    public ParameterExtractorViewModel ParameterExtractor { get; }

    /// <summary>
    /// Indicates whether this module is ready for integration run.
    /// Requires valid Rows/Cols (> 0) and BitDepth in [8, 16].
    /// </summary>
    public bool IsReady =>
        SimulatorControl.PanelRows > 0 &&
        SimulatorControl.PanelCols > 0 &&
        SimulatorControl.PanelBitDepth is >= 8 and <= 16;
}
