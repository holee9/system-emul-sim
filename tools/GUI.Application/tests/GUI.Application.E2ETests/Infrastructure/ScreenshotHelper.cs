using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;

namespace XrayDetector.Gui.E2ETests.Infrastructure;

/// <summary>
/// Captures screenshots on test failure.
/// SPEC-HELP-001: REQ-HELP-055
/// TAG-004: Added overload accepting explicit directory and filename for auto-failure hook.
/// </summary>
public static class ScreenshotHelper
{
    /// <summary>
    /// Captures a screenshot using the default TestResults/Screenshots directory.
    /// Filename is derived from <paramref name="testName"/> + timestamp.
    /// </summary>
    public static void CaptureOnFailure(string testName, AutomationElement? window = null)
    {
        CaptureOnFailure(testName, window, directory: Path.Combine("TestResults", "Screenshots"));
    }

    /// <summary>
    /// Captures a screenshot to <paramref name="directory"/> with <paramref name="fileNameWithoutExtension"/>.
    /// TAG-004: Used by E2ETestBase auto-failure hook (XRAY_E2E_SCREENSHOT_DIR).
    /// </summary>
    public static void CaptureOnFailure(string fileNameWithoutExtension, AutomationElement? window, string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"{SanitizeName(fileNameWithoutExtension)}.png");

            var capture = window != null
                ? Capture.Element(window)
                : Capture.Screen();
            capture.ToFile(path);
        }
        catch
        {
            // Screenshot capture failures should not fail tests
        }
    }

    private static string SanitizeName(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
