namespace XrayDetector.Gui.Core;

/// <summary>
/// Abstraction over system clipboard for testability (DEC-004).
/// </summary>
public interface IClipboardService
{
    /// <summary>Sets the clipboard text content.</summary>
    /// <param name="text">Text to copy to clipboard.</param>
    void SetText(string text);
}
