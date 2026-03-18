using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using XrayDetector.Gui.ViewModels;

namespace XrayDetector.Gui.Views;

/// <summary>
/// Main window for X-ray Detector Panel System GUI (SPEC-GUI-002).
/// Handles full-screen toggle and help window lifecycle.
/// Frame rendering is delegated to ConsoleView.xaml.cs.
/// </summary>
public partial class MainWindow : Window
{
    private WindowState _previousWindowState = WindowState.Normal;
    private WindowStyle _previousWindowStyle = WindowStyle.SingleBorderWindow;
    private ResizeMode _previousResizeMode = ResizeMode.CanResize;
    private HelpWindow? _helpWindow;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainViewModel oldVm)
        {
            oldVm.FullScreenRequested -= OnFullScreenRequested;
            oldVm.HelpRequested -= OnHelpRequested;
        }

        if (e.NewValue is MainViewModel newVm)
        {
            newVm.FullScreenRequested += OnFullScreenRequested;
            newVm.HelpRequested += OnHelpRequested;
        }
    }

    private void OnFullScreenRequested(bool isFullScreen)
    {
        if (isFullScreen)
        {
            _previousWindowState = WindowState;
            _previousWindowStyle = WindowStyle;
            _previousResizeMode = ResizeMode;

            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            ResizeMode = ResizeMode.NoResize;
        }
        else
        {
            WindowStyle = _previousWindowStyle;
            ResizeMode = _previousResizeMode;
            WindowState = _previousWindowState;
        }
    }

    private void OnHelpRequested()
    {
        if (_helpWindow == null || !_helpWindow.IsVisible)
        {
            _helpWindow = new HelpWindow { Owner = this };
            _helpWindow.Show();
        }
        else
        {
            _helpWindow.Activate();
        }
    }
}
