using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using XrayDetector.Gui.ViewModels;

namespace XrayDetector.Gui.Views;

/// <summary>
/// Code-behind for ConsoleView.
/// Wires FramePreviewViewModel.DisplayPixels to the WPF Image control via WriteableBitmap (SPEC-GUI-002).
/// </summary>
public partial class ConsoleView : UserControl
{
    private WriteableBitmap? _bitmap;
    private FramePreviewViewModel? _frameVm;

    public ConsoleView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ConsoleViewModel oldVm)
        {
            oldVm.FramePreview.PropertyChanged -= OnFramePreviewPropertyChanged;
            _frameVm = null;
        }

        if (e.NewValue is ConsoleViewModel newVm)
        {
            _frameVm = newVm.FramePreview;
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
            _bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), pixels, width, 0);
            _bitmap.AddDirtyRect(new System.Windows.Int32Rect(0, 0, width, height));
        }
        finally
        {
            _bitmap.Unlock();
        }
    }
}
