using System.Windows;
using XrayDetector.Gui.Help;
using XrayDetector.Gui.ViewModels;

namespace XrayDetector.Gui.Views;

/// <summary>
/// Code-behind for HelpWindow (SPEC-HELP-001 Wave 2).
/// Provides two-panel help navigation with topic tree and FlowDocument viewer.
/// </summary>
public partial class HelpWindow : Window
{
    private readonly HelpViewModel _viewModel;

    public HelpWindow()
    {
        InitializeComponent();
        _viewModel = new HelpViewModel(new EmbeddedHelpContentService());
        DataContext = _viewModel;

        // Auto-select first topic
        var topics = _viewModel.Topics;
        if (topics.Count > 0)
            _viewModel.SelectTopicCommand.Execute(topics[0].Id);
    }

    /// <summary>
    /// Opens the help window and navigates to the specified topic.
    /// </summary>
    public void NavigateToTopic(string? topicId)
    {
        if (!string.IsNullOrWhiteSpace(topicId))
            _viewModel.SelectTopicCommand.Execute(topicId);
    }
}
