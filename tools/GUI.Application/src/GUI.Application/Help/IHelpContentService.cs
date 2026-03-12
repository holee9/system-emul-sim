namespace XrayDetector.Gui.Help;

/// <summary>
/// Service interface for loading help content (SPEC-HELP-001 Wave 2).
/// </summary>
public interface IHelpContentService
{
    /// <summary>Returns all available help topics.</summary>
    IReadOnlyList<HelpTopic> GetTopics();

    /// <summary>Returns the topic with the given ID, or null if not found.</summary>
    HelpTopic? GetTopic(string topicId);

    /// <summary>
    /// Returns the Markdown content for the given topic ID.
    /// Returns empty string if the resource is not found.
    /// </summary>
    string GetContent(string topicId);
}
