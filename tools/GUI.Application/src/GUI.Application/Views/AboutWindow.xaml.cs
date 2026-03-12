using System.Windows;

namespace XrayDetector.Gui.Views;

/// <summary>
/// Code-behind for About dialog (SPEC-HELP-001).
/// </summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
