using System.Buffers;
using XrayDetector.Core.Processing;
using XrayDetector.Gui.Core;
using XrayDetector.Models;

namespace XrayDetector.Gui.ViewModels;

/// <summary>
/// ViewModel for frame preview display (REQ-TOOLS-041, REQ-TOOLS-042).
/// Handles 16-bit to 8-bit conversion, Window/Level mapping.
/// </summary>
public sealed class FramePreviewViewModel : ObservableObject
{
    private readonly WindowLevelMapper _mapper;
    private Frame? _currentFrame;
    private double _windowCenter = 32768.0;
    private double _windowWidth = 65535.0;
    private double _zoomLevel = 1.0;
    private string _frameInfo = "No frame";
    private byte[] _displayPixels = Array.Empty<byte>();

    /// <summary>
    /// Creates a new FramePreviewViewModel.
    /// </summary>
    public FramePreviewViewModel()
    {
        _mapper = new WindowLevelMapper();
    }

    /// <summary>Current frame being displayed.</summary>
    public Frame? CurrentFrame
    {
        get => _currentFrame;
        private set => SetField(ref _currentFrame, value);
    }

    /// <summary>Window center (level) for display mapping (REQ-TOOLS-042).</summary>
    public double WindowCenter
    {
        get => _windowCenter;
        private set => SetField(ref _windowCenter, value);
    }

    /// <summary>Window width for display mapping (REQ-TOOLS-042).</summary>
    public double WindowWidth
    {
        get => _windowWidth;
        private set => SetField(ref _windowWidth, value);
    }

    /// <summary>Current zoom level for display.</summary>
    public double ZoomLevel
    {
        get => _zoomLevel;
        private set => SetField(ref _zoomLevel, value);
    }

    /// <summary>Frame information display string.</summary>
    public string FrameInfo
    {
        get => _frameInfo;
        private set => SetField(ref _frameInfo, value);
    }

    /// <summary>8-bit grayscale pixel data for display (REQ-TOOLS-041).</summary>
    public byte[] DisplayPixels
    {
        get => _displayPixels;
        private set => SetField(ref _displayPixels, value);
    }

    /// <summary>Minimum pixel value in current frame (for info display).</summary>
    public ushort MinValue => CurrentFrame?.MinValue ?? 0;

    /// <summary>Maximum pixel value in current frame (for info display).</summary>
    public ushort MaxValue => CurrentFrame?.MaxValue ?? 0;

    /// <summary>Mean pixel value in current frame (for info display).</summary>
    public double MeanValue => CurrentFrame?.MeanValue ?? 0.0;

    /// <summary>
    /// Sets the current frame and updates display pixels.
    /// </summary>
    public void SetFrame(Frame? frame)
    {
        CurrentFrame = frame;

        if (frame == null)
        {
            DisplayPixels = Array.Empty<byte>();
            FrameInfo = "No frame";
            return;
        }

        // Update frame info (REQ-TOOLS-041)
        FrameInfo = $"Frame {frame.FrameNumber}: {frame.Width}x{frame.Height}, {frame.BitDepth}-bit @ {frame.Timestamp:HH:mm:ss.fff}";

        // Apply window/level mapping (REQ-TOOLS-042)
        UpdateDisplayPixels();

        // Notify stats properties
        OnPropertyChanged(nameof(MinValue));
        OnPropertyChanged(nameof(MaxValue));
        OnPropertyChanged(nameof(MeanValue));
    }

    /// <summary>
    /// Updates window and level values.
    /// Must complete within 100ms per REQ-TOOLS-042.
    /// </summary>
    public void UpdateWindowLevel(double center, double width)
    {
        if (width <= 0)
            throw new ArgumentException("Width must be positive", nameof(width));

        WindowCenter = center;
        WindowWidth = width;

        _mapper.UpdateWindowLevel(center, width);

        // Update display pixels immediately
        if (CurrentFrame != null)
        {
            UpdateDisplayPixels();
        }
    }

    /// <summary>
    /// Auto-calculates optimal window/level from current frame data.
    /// </summary>
    public void AutoWindowLevel()
    {
        if (CurrentFrame == null)
            return;

        _mapper.AutoWindowLevel(CurrentFrame.PixelData, 0.95);

        WindowCenter = _mapper.Level;
        WindowWidth = _mapper.Window;

        UpdateDisplayPixels();
    }

    /// <summary>
    /// Sets the zoom level for display.
    /// </summary>
    public void SetZoomLevel(double zoom)
    {
        ZoomLevel = Math.Max(0.1, Math.Min(10.0, zoom));
    }

    /// <summary>
    /// Returns true if a frame can be saved (REQ-TOOLS-044).
    /// </summary>
    public bool CanSaveFrame() => CurrentFrame != null;

    private void UpdateDisplayPixels()
    {
        if (CurrentFrame == null)
            return;

        // Apply window/level mapping (16-bit to 8-bit)
        DisplayPixels = _mapper.Map(CurrentFrame.PixelData);
    }
}
