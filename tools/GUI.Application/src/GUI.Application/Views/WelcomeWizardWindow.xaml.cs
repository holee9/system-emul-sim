using System.Windows;
using XrayDetector.Gui.Core;
using XrayDetector.Gui.ViewModels;

namespace XrayDetector.Gui.Views;

/// <summary>
/// Code-behind for WelcomeWizardWindow (SPEC-HELP-001 Wave 2).
/// Shows a 3-step welcome wizard on first application launch.
/// </summary>
public partial class WelcomeWizardWindow : Window
{
    private readonly WelcomeWizardViewModel _viewModel;
    private readonly FirstRunManager _firstRunManager;

    public WelcomeWizardWindow() : this(new FirstRunManager())
    {
    }

    public WelcomeWizardWindow(FirstRunManager firstRunManager)
    {
        InitializeComponent();
        _firstRunManager = firstRunManager;
        _viewModel = new WelcomeWizardViewModel
        {
            FinishAction = OnFinish
        };
        DataContext = _viewModel;
    }

    private void OnFinish()
    {
        if (_viewModel.DontShowAgain)
            _firstRunManager.MarkFirstRunComplete();

        Close();
    }
}
