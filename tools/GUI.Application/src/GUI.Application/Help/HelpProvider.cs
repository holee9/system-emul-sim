using System.Windows;

namespace XrayDetector.Gui.Help;

/// <summary>
/// Attached property for context-sensitive help (F1) support (SPEC-HELP-001 Wave 2).
/// Set HelpTopicId on any DependencyObject to associate it with a help topic.
/// </summary>
public static class HelpProvider
{
    /// <summary>Attached property to associate a help topic ID with a UI element.</summary>
    public static readonly DependencyProperty HelpTopicIdProperty =
        DependencyProperty.RegisterAttached(
            "HelpTopicId",
            typeof(string),
            typeof(HelpProvider),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

    /// <summary>Gets the help topic ID from the given object.</summary>
    public static string? GetHelpTopicId(DependencyObject obj)
        => (string?)obj.GetValue(HelpTopicIdProperty);

    /// <summary>Sets the help topic ID on the given object.</summary>
    public static void SetHelpTopicId(DependencyObject obj, string? value)
        => obj.SetValue(HelpTopicIdProperty, value);
}
