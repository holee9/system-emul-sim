using System.Text.Json.Serialization;

namespace Common.Dto.Dtos;

/// <summary>
/// Represents a single line of pixel data from a frame.
/// REQ-SIM-051: Common.Dto shall define data transfer objects including LineData.
/// </summary>
public record LineData
{
    /// <summary>
    /// Frame identifier this line belongs to.
    /// </summary>
    [JsonPropertyName("frameNumber")]
    public int FrameNumber { get; init; }

    /// <summary>
    /// Line number within the frame (0-based).
    /// </summary>
    [JsonPropertyName("lineNumber")]
    public int LineNumber { get; init; }

    /// <summary>
    /// Pixel data for this line.
    /// </summary>
    [JsonPropertyName("pixels")]
    public ushort[] Pixels { get; init; }

    /// <summary>
    /// Creates a new LineData instance with validation.
    /// </summary>
    public LineData(int frameNumber, int lineNumber, ushort[] pixels)
    {
        if (lineNumber < 0)
        {
            throw new ArgumentException("LineNumber must be non-negative.", nameof(lineNumber));
        }

        if (pixels == null)
        {
            throw new ArgumentNullException(nameof(pixels));
        }

        if (pixels.Length == 0)
        {
            throw new ArgumentException("Pixels array must not be empty.", nameof(pixels));
        }

        FrameNumber = frameNumber;
        LineNumber = lineNumber;
        Pixels = pixels;
    }

    /// <summary>
    /// Returns a string representation of the line data for debugging.
    /// </summary>
    public override string ToString()
    {
        return $"LineData {{ FrameNumber = {FrameNumber}, LineNumber = {LineNumber}, PixelsLength = {Pixels.Length} }}";
    }
}
