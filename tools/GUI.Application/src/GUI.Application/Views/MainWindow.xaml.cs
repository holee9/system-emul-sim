using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using XrayDetector.Gui.ViewModels;

namespace XrayDetector.Gui.Views;

/// <summary>
/// Main window for X-ray Detector Panel System GUI.
/// Unified WPF interface per REQ-TOOLS-040.
/// Wires FramePreviewViewModel.DisplayPixels to the WPF Image control via WriteableBitmap.
/// </summary>
public partial class MainWindow : Window
{
    private WriteableBitmap? _bitmap;
    private FramePreviewViewModel? _frameVm;
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
            oldVm.FramePreviewViewModel.PropertyChanged -= OnFramePreviewPropertyChanged;
            oldVm.FullScreenRequested -= OnFullScreenRequested;
            oldVm.HelpRequested -= OnHelpRequested;
        }

        if (e.NewValue is MainViewModel newVm)
        {
            _frameVm = newVm.FramePreviewViewModel;
            _frameVm.PropertyChanged += OnFramePreviewPropertyChanged;
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

    private void OnFramePreviewPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FramePreviewViewModel.DisplayPixels))
            return;

        if (_frameVm?.DisplayPixels is not { Length: > 0 } pixels)
            return;

        var frame = _frameVm.CurrentFrame;
        if (frame == null)
            return;

        int width = frame.Width;
        int height = frame.Height;

        // Must run on the UI thread
        if (Dispatcher.CheckAccess())
            RenderPixels(pixels, width, height);
        else
            Dispatcher.BeginInvoke(() => RenderPixels(pixels, width, height));
    }

    private void RenderPixels(byte[] pixels, int width, int height)
    {
        if (_bitmap == null || _bitmap.PixelWidth != width || _bitmap.PixelHeight != height)
        {
            _bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Gray8, null);
            FrameImage.Source = _bitmap;
        }

        _bitmap.Lock();
        try
        {
            _bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width, 0);
            _bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
        }
        finally
        {
            _bitmap.Unlock();
        }
    }
}
