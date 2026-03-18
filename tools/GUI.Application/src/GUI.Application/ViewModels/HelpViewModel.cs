using System.Windows.Documents;
using System.Windows.Input;
using XrayDetector.Gui.Core;
using XrayDetector.Gui.Help;

namespace XrayDetector.Gui.ViewModels;

/// <summary>
/// ViewModel for the HelpWindow (SPEC-HELP-001 Wave 2).
/// Manages topic navigation, search/filtering, and Markdown content rendering.
/// </summary>
public class HelpViewModel : ObservableObject
{
    private readonly IHelpContentService _contentService;
    private readonly MarkdownFlowDocumentConverter _converter = new();

    private HelpTopic? _selectedTopic;
    private FlowDocument? _currentContent;
    private string _searchText = string.Empty;

    /// <summary>
    /// Creates a new HelpViewModel with the given content service.
    /// </summary>
    public HelpViewModel(IHelpContentService contentService)
    {
        _contentService = contentService ?? throw new ArgumentNullException(nameof(contentService));
        Topics = _contentService.GetTopics();
        SelectTopicCommand = new RelayCommand<string>(OnSelectTopic);
    }

    /// <summary>All available topics from the content service.</summary>
    public IReadOnlyList<HelpTopic> Topics { get; }

    /// <summary>Currently selected topic.</summary>
    public HelpTopic? SelectedTopic
    {
        get => _selectedTopic;
        private set => SetField(ref _selectedTopic, value);
    }

    /// <summary>FlowDocument for the currently selected topic's content.</summary>
    public FlowDocument? CurrentContent
    {
        get => _currentContent;
        private set
        {
            if (SetField(ref _currentContent, value))
                OnPropertyChanged(nameof(CurrentContent));
        }
    }

    /// <summary>Search text used to filter the topic list.</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
                OnPropertyChanged(nameof(FilteredTopics));
        }
    }

    /// <summary>Topics filtered by SearchText (case-insensitive, matches Id or Title).</summary>
    public IEnumerable<HelpTopic> FilteredTopics
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_searchText))
                return Topics;

            return Topics.Where(t =>
                t.Id.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                t.Title.Contains(_searchText, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>Command to select a topic by its ID string.</summary>
    public ICommand SelectTopicCommand { get; }

    private void OnSelectTopic(string? topicId)
    {
        if (string.IsNullOrWhiteSpace(topicId))
            return;

        var topic = _contentService.GetTopic(topicId);
        if (topic == null)
            return;

        SelectedTopic = topic;

        var markdown = _contentService.GetContent(topicId);
        CurrentContent = _converter.Convert(markdown);
    }
}
