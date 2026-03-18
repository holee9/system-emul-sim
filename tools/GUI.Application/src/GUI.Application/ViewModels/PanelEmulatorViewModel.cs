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

    /// <summary>Indicates whether this module is ready for integration run.</summary>
    public bool IsReady => true;
}
