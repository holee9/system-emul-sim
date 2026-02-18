namespace XrayDetector.Common.Dto;

/// <summary>
/// Value object representing metadata for a single detector frame.
/// Immutable - use WithNextFrameNumber() to create updated instances.
/// </summary>
public sealed class FrameMetadata : IEquatable<FrameMetadata>
{
    /// <summary>Frame width in pixels.</summary>
    public int Width { get; }

    /// <summary>Frame height in pixels.</summary>
    public int Height { get; }

    /// <summary>Bit depth per pixel (8, 12, 14, or 16).</summary>
    public int BitDepth { get; }

    /// <summary>Timestamp when frame was captured.</summary>
    public DateTime Timestamp { get; }

    /// <summary>Frame sequence number (wraps at UInt32.MaxValue).</summary>
    public uint FrameNumber { get; }

    /// <summary>
    /// Creates a new FrameMetadata instance.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when width or height is less than 1, or bitDepth is not 8, 12, 14, or 16.
    /// </exception>
    public FrameMetadata(int width, int height, int bitDepth, DateTime timestamp, uint frameNumber)
    {
        if (width < 1)
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be at least 1.");
        if (height < 1)
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be at least 1.");
        if (bitDepth is not 8 and not 12 and not 14 and not 16)
            throw new ArgumentOutOfRangeException(nameof(bitDepth), "Bit depth must be 8, 12, 14, or 16.");

        Width = width;
        Height = height;
        BitDepth = bitDepth;
        Timestamp = timestamp;
        FrameNumber = frameNumber;
    }

    /// <summary>Creates new instance with incremented frame number (wraps at UInt32.MaxValue).</summary>
    public FrameMetadata WithNextFrameNumber() =>
        new(Width, Height, BitDepth, Timestamp, FrameNumber == uint.MaxValue ? 0 : FrameNumber + 1);

    /// <summary>Total number of pixels in the frame.</summary>
    public int PixelCount => Width * Height;

    /// <summary>Number of bytes required to store the frame data.</summary>
    public int BytesPerFrame => PixelCount * ((BitDepth + 7) / 8);

    /// <inheritdoc />
    public bool Equals(FrameMetadata? other) =>
        other != null &&
        Width == other.Width &&
        Height == other.Height &&
        BitDepth == other.BitDepth &&
        Timestamp == other.Timestamp &&
        FrameNumber == other.FrameNumber;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as FrameMetadata);

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(Width, Height, BitDepth, Timestamp, FrameNumber);

    /// <inheritdoc />
    public override string ToString() =>
        $"{Width}x{Height}, {BitDepth}-bit, Frame #{FrameNumber}";
}
