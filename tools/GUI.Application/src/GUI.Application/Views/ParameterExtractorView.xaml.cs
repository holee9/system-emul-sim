using XrayDetector.Gui.ViewModels;

namespace XrayDetector.Gui.Views;

/// <summary>
/// View for PDF datasheet parameter extraction (REQ-UI-013).
/// </summary>
public sealed partial class ParameterExtractorView
{
    public ParameterExtractorView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Gets the ViewModel for this view.
    /// </summary>
    public ParameterExtractorViewModel? ViewModel => DataContext as ParameterExtractorViewModel;
}
