using System.Text.Json.Serialization;

namespace Common.Dto.Dtos;

/// <summary>
/// Represents a complete frame of pixel data from the X-ray detector panel.
/// REQ-SIM-051: Common.Dto shall define data transfer objects including FrameData.
/// </summary>
public record FrameData
{
    /// <summary>
    /// Sequential frame identifier.
    /// </summary>
    [JsonPropertyName("frameNumber")]
    public int FrameNumber { get; init; }

    /// <summary>
    /// Frame width in pixels.
    /// </summary>
    [JsonPropertyName("width")]
    public int Width { get; init; }

    /// <summary>
    /// Frame height in pixels.
    /// </summary>
    [JsonPropertyName("height")]
    public int Height { get; init; }

    /// <summary>
    /// Pixel data array (16-bit values).
    /// </summary>
    [JsonPropertyName("pixels")]
    public ushort[] Pixels { get; init; }

    /// <summary>
    /// Creates a new FrameData instance with validation.
    /// </summary>
    public FrameData(int frameNumber, int width, int height, ushort[] pixels)
    {
        if (width <= 0)
        {
            throw new ArgumentException("Width must be positive.", nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentException("Height must be positive.", nameof(height));
        }

        if (pixels == null)
        {
            throw new ArgumentNullException(nameof(pixels));
        }

        int expectedSize = width * height;
        if (pixels.Length != expectedSize)
        {
            throw new ArgumentException(
                $"Pixels array size ({pixels.Length}) must match width * height ({expectedSize}).",
                nameof(pixels));
        }

        FrameNumber = frameNumber;
        Width = width;
        Height = height;
        Pixels = pixels;
    }

    /// <summary>
    /// Returns a string representation of the frame data for debugging.
    /// </summary>
    public override string ToString()
    {
        return $"FrameData {{ FrameNumber = {FrameNumber}, Width = {Width}, Height = {Height}, PixelsLength = {Pixels.Length} }}";
    }
}
