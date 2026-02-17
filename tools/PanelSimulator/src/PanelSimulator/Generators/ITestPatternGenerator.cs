namespace PanelSimulator.Generators;

/// <summary>
/// Interface for test pattern generators.
/// </summary>
public interface ITestPatternGenerator
{
    /// <summary>
    /// Generates a test pattern pixel array.
    /// </summary>
    /// <param name="width">Width of the frame in pixels.</param>
    /// <param name="height">Height of the frame in pixels.</param>
    /// <param name="bitDepth">Bit depth per pixel.</param>
    /// <param name="frameNumber">Frame sequence number.</param>
    /// <returns>Array of pixel values.</returns>
    ushort[] Generate(int width, int height, int bitDepth, int frameNumber);
}
