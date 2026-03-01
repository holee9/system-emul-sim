namespace PanelSimulator.Generators;

/// <summary>
/// Generates a flat field (uniform value) test pattern.
/// All pixels have the same value (typically mid-range).
/// REQ-SIM-011: Flat field pattern for uniform exposure testing.
/// </summary>
public sealed class FlatFieldPatternGenerator : ITestPatternGenerator
{
    /// <summary>
    /// Generates a flat field pattern where all pixels have the same value.
    /// </summary>
    /// <param name="width">Width of the frame in pixels.</param>
    /// <param name="height">Height of the frame in pixels.</param>
    /// <param name="bitDepth">Bit depth per pixel.</param>
    /// <param name="frameNumber">Frame sequence number (unused for flat field).</param>
    /// <returns>Array of uniform pixel values.</returns>
    public ushort[] Generate(int width, int height, int bitDepth, int frameNumber)
    {
        int pixelCount = width * height;
        var pixels = new ushort[pixelCount];

        // Calculate mid-range value for the bit depth
        // For 16-bit: 32768, for 14-bit: 8192
        ushort uniformValue = (ushort)((1 << (bitDepth - 1)) - 1);

        // Fill all pixels with uniform value
        Array.Fill(pixels, uniformValue);

        return pixels;
    }
}
