using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;

namespace XrayDetector.Gui.E2ETests.Infrastructure;

/// <summary>
/// Captures screenshots on test failure.
/// SPEC-HELP-001: REQ-HELP-055
/// </summary>
public static class ScreenshotHelper
{
    public static void CaptureOnFailure(string testName, AutomationElement? window = null)
    {
        try
        {
            var dir = Path.Combine("TestResults", "Screenshots");
            Directory.CreateDirectory(dir);
            var fileName = Path.Combine(dir, $"{SanitizeName(testName)}_{DateTime.Now:yyyyMMdd_HHmmss}.png");

            var capture = window != null
                ? Capture.Element(window)
                : Capture.Screen();
            capture.ToFile(fileName);
        }
        catch
        {
            // Screenshot capture failures should not fail tests
        }
    }

    private static string SanitizeName(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
