using System.Windows;

namespace XrayDetector.Gui.Core;

/// <summary>
/// WPF clipboard implementation (DEC-004).
/// Must be called from the UI thread.
/// </summary>
public sealed class ClipboardService : IClipboardService
{
    /// <inheritdoc />
    public void SetText(string text)
    {
        Clipboard.SetText(text);
    }
}
