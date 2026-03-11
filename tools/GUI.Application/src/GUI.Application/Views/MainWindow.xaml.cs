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

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainViewModel oldVm)
            oldVm.FramePreviewViewModel.PropertyChanged -= OnFramePreviewPropertyChanged;

        if (e.NewValue is MainViewModel newVm)
        {
            _frameVm = newVm.FramePreviewViewModel;
            _frameVm.PropertyChanged += OnFramePreviewPropertyChanged;
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
