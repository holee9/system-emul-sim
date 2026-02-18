namespace XrayDetector.Core.Processing;

/// <summary>
/// Frame statistics with lazy computation.
/// Provides min, max, and mean values for 16-bit grayscale image data.
/// </summary>
public sealed class FrameStatistics
{
    private readonly ushort[] _pixelData;
    private ushort? _min;
    private ushort? _max;
    private double? _mean;

    /// <summary>
    /// Creates a new frame statistics instance.
    /// Computation is lazy - values are calculated on first access.
    /// </summary>
    /// <param name="pixelData">16-bit grayscale pixel data.</param>
    public FrameStatistics(ushort[] pixelData)
    {
        _pixelData = pixelData ?? throw new ArgumentNullException(nameof(pixelData));
        PixelCount = pixelData.Length;
    }

    /// <summary>Number of pixels in the frame.</summary>
    public int PixelCount { get; }

    /// <summary>
    /// Gets the minimum pixel value.
    /// Computed on first access and cached.
    /// </summary>
    public ushort Min
    {
        get
        {
            if (!_min.HasValue)
            {
                ComputeAll();
            }
            return _min.GetValueOrDefault();
        }
    }

    /// <summary>
    /// Gets the maximum pixel value.
    /// Computed on first access and cached.
    /// </summary>
    public ushort Max
    {
        get
        {
            if (!_max.HasValue)
            {
                ComputeAll();
            }
            return _max.GetValueOrDefault();
        }
    }

    /// <summary>
    /// Gets the mean (average) pixel value.
    /// Computed on first access and cached.
    /// </summary>
    public double Mean
    {
        get
        {
            if (!_mean.HasValue)
            {
                ComputeAll();
            }
            return _mean.GetValueOrDefault();
        }
    }

    /// <summary>
    /// Computes all statistics in a single pass.
    /// More efficient than accessing properties individually.
    /// </summary>
    private void ComputeAll()
    {
        if (_pixelData.Length == 0)
        {
            _min = 0;
            _max = 0;
            _mean = 0.0;
            return;
        }

        uint min = uint.MaxValue;
        uint max = 0;
        ulong sum = 0;

        foreach (ushort pixel in _pixelData)
        {
            if (pixel < min) min = pixel;
            if (pixel > max) max = pixel;
            sum += pixel;
        }

        _min = (ushort)min;
        _max = (ushort)max;
        _mean = _pixelData.Length > 0 ? (double)sum / _pixelData.Length : 0.0;
    }

    /// <summary>
    /// Gets a summary of the frame statistics.
    /// </summary>
    public override string ToString()
    {
        return $"FrameStatistics: Min={Min}, Max={Max}, Mean={Mean:F2}, Pixels={PixelCount}";
    }
}
