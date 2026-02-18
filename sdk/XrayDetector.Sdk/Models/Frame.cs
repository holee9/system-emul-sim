using System.Buffers;
using XrayDetector.Common.Dto;
using XrayDetector.Core.Processing;

namespace XrayDetector.Models;

/// <summary>
/// Represents a single X-ray detector frame with 16-bit grayscale pixel data.
/// Implements IDisposable for efficient memory management using ArrayPool.
/// </summary>
public sealed class Frame : IDisposable
{
    private readonly ArrayPool<ushort> _pool;
    private readonly bool _ownsArray;
    private bool _disposed;
    private FrameStatistics? _statistics;

    /// <summary>
    /// Creates a new frame from pixel data and metadata.
    /// Uses shared array pool for memory management.
    /// </summary>
    /// <param name="pixelData">16-bit grayscale pixel data.</param>
    /// <param name="metadata">Frame metadata.</param>
    public Frame(ushort[] pixelData, FrameMetadata metadata)
        : this(pixelData, metadata, ArrayPool<ushort>.Shared)
    {
        _ownsArray = false; // Caller owns the array
    }

    /// <summary>
    /// Creates a new frame with explicit pool ownership.
    /// </summary>
    /// <param name="pixelData">16-bit grayscale pixel data.</param>
    /// <param name="metadata">Frame metadata.</param>
    /// <param name="pool">Array pool for memory management (null = shared).</param>
    public Frame(ushort[] pixelData, FrameMetadata metadata, ArrayPool<ushort>? pool)
    {
        PixelData = pixelData ?? throw new ArgumentNullException(nameof(pixelData));
        Width = metadata.Width;
        Height = metadata.Height;
        BitDepth = metadata.BitDepth;
        Timestamp = metadata.Timestamp;
        FrameNumber = metadata.FrameNumber;
        _pool = pool ?? ArrayPool<ushort>.Shared;
        _ownsArray = true;
        _disposed = false;
    }

    /// <summary>Frame width in pixels.</summary>
    public int Width { get; }

    /// <summary>Frame height in pixels.</summary>
    public int Height { get; }

    /// <summary>Bit depth (typically 16).</summary>
    public int BitDepth { get; }

    /// <summary>Frame timestamp.</summary>
    public DateTime Timestamp { get; }

    /// <summary>Frame sequence number.</summary>
    public uint FrameNumber { get; }

    /// <summary>16-bit grayscale pixel data.</summary>
    public ushort[] PixelData { get; }

    /// <summary>Total number of pixels.</summary>
    public int PixelCount => PixelData.Length;

    /// <summary>
    /// Gets frame statistics (lazy computation).
    /// Cached after first computation.
    /// </summary>
    public FrameStatistics Statistics
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _statistics ??= new FrameStatistics(PixelData);
        }
    }

    /// <summary>
    /// Releases resources and returns array to pool.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        if (_ownsArray && PixelData != null)
        {
            // Only return to pool if it's not a shared pool array from caller
            // Shared pool arrays should not be returned if they weren't rented from our pool instance
            try
            {
                _pool.Return(PixelData, clearArray: false);
            }
            catch (ArgumentException)
            {
                // Array was not from this pool, ignore
            }
        }

        _disposed = true;
    }

    /// <summary>
    /// Creates a deep copy of this frame.
    /// </summary>
    public Frame Clone()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ushort[] clonedData = new ushort[PixelData.Length];
        Array.Copy(PixelData, clonedData, PixelData.Length);

        var metadata = new FrameMetadata(
            (int)Width,
            (int)Height,
            (int)BitDepth,
            Timestamp,
            FrameNumber
        );

        return new Frame(clonedData, metadata, _pool);
    }

    /// <summary>
    /// Extracts a region of interest (ROI) from this frame.
    /// </summary>
    /// <param name="x">X coordinate of top-left corner.</param>
    /// <param name="y">Y coordinate of top-left corner.</param>
    /// <param name="width">ROI width.</param>
    /// <param name="height">ROI height.</param>
    /// <returns>New frame containing the ROI.</returns>
    public Frame ExtractRoi(int x, int y, int width, int height)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (x < 0 || y < 0 || width < 1 || height < 1)
        {
            throw new ArgumentException("Invalid ROI parameters");
        }
        if (x + width > Width || y + height > Height)
        {
            throw new ArgumentException("ROI extends beyond frame boundaries");
        }

        ushort[] roiData = new ushort[width * height];

        for (int row = 0; row < height; row++)
        {
            int srcOffset = (y + row) * Width + x;
            int dstOffset = row * width;
            Array.Copy(PixelData, srcOffset, roiData, dstOffset, width);
        }

        var metadata = new FrameMetadata(
            width,
            height,
            BitDepth,
            Timestamp,
            FrameNumber
        );

        return new Frame(roiData, metadata, _pool);
    }

    public override string ToString()
    {
        return $"Frame {FrameNumber}: {Width}x{Height}, {BitDepth}-bit, {PixelCount} pixels";
    }
}
