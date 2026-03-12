using System.IO;
using System.Reflection;

namespace XrayDetector.Gui.Help;

/// <summary>
/// Loads help content from embedded Markdown resources (SPEC-HELP-001 Wave 2).
/// Resource naming: GUI.Application.Help.Topics.{filename}.md
/// </summary>
public class EmbeddedHelpContentService : IHelpContentService
{
    private static readonly IReadOnlyList<HelpTopic> _topics = BuildTopicList();
    private static readonly Dictionary<string, HelpTopic> _topicIndex = _topics.ToDictionary(t => t.Id);

    /// <inheritdoc/>
    public IReadOnlyList<HelpTopic> GetTopics() => _topics;

    /// <inheritdoc/>
    public HelpTopic? GetTopic(string topicId)
    {
        _topicIndex.TryGetValue(topicId, out var topic);
        return topic;
    }

    /// <inheritdoc/>
    public string GetContent(string topicId)
    {
        var assembly = Assembly.GetExecutingAssembly();
        // Resource name follows RootNamespace convention: XrayDetector.Gui.Help.Topics.{id}.md
        var resourceName = $"XrayDetector.Gui.Help.Topics.{topicId}.md";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            return string.Empty;

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static IReadOnlyList<HelpTopic> BuildTopicList()
    {
        return new List<HelpTopic>
        {
            new HelpTopic("overview", "시스템 개요"),
            new HelpTopic("getting-started", "빠른 시작 가이드"),
            new HelpTopic("panel-simulation", "Panel 시뮬레이션"),
            new HelpTopic("fpga-csi2", "FPGA/CSI-2 처리"),
            new HelpTopic("mcu-udp", "MCU/UDP 통신"),
            new HelpTopic("host-pipeline", "Host 파이프라인"),
            new HelpTopic("parameters-ref", "파라미터 레퍼런스"),
            new HelpTopic("keyboard-shortcuts", "키보드 단축키"),
            new HelpTopic("troubleshooting", "문제 해결"),
        };
    }
}
