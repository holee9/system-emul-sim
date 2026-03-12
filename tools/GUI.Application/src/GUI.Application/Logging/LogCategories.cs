namespace XrayDetector.Gui.Logging;

/// <summary>
/// Log category constants for structured logging (SPEC-HELP-001).
/// Use these constants with Serilog's SourceContext or ForContext calls.
/// </summary>
public static class LogCategories
{
    /// <summary>Category for pipeline-related log events.</summary>
    public const string Pipeline = "XrayDetector.Gui.Pipeline";

    /// <summary>Category for UI interaction log events.</summary>
    public const string UI = "XrayDetector.Gui.UI";

    /// <summary>Category for performance measurement log events.</summary>
    public const string Performance = "XrayDetector.Gui.Performance";

    /// <summary>Category for user action log events.</summary>
    public const string UserAction = "XrayDetector.Gui.UserAction";

    /// <summary>Category for help system log events.</summary>
    public const string Help = "XrayDetector.Gui.Help";

    /// <summary>Category for application lifecycle log events.</summary>
    public const string App = "XrayDetector.Gui.App";
}
