namespace XrayDetector.Gui.Help;

/// <summary>
/// Represents a single help topic in the help system (SPEC-HELP-001 Wave 2).
/// </summary>
public record HelpTopic(string Id, string Title, string? ParentId = null)
{
    /// <summary>Child topics under this topic.</summary>
    public List<HelpTopic> Children { get; init; } = new();
}
