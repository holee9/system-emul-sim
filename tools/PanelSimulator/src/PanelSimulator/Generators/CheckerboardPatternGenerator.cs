namespace PanelSimulator.Generators;

/// <summary>
/// Generates a checkerboard test pattern with alternating max/zero values.
/// REQ-SIM-014: Checkerboard pattern for electrical stress testing validation.
/// </summary>
public class CheckerboardPatternGenerator : ITestPatternGenerator
{
    /// <inheritdoc />
    public ushort[] Generate(int width, int height, int bitDepth, int frameNumber)
    {
        if (width <= 0)
        {
            throw new ArgumentException("Width must be positive.", nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentException("Height must be positive.", nameof(height));
        }

        if (bitDepth is < 1 or > 16)
        {
            throw new ArgumentException("Bit depth must be between 1 and 16.", nameof(bitDepth));
        }

        int pixelCount = width * height;
        ushort[] pixels = new ushort[pixelCount];

        // REQ-SIM-014: Even pixels = 0, odd pixels = max value. Pattern inverts every other row.
        ushort maxValue = (ushort)((1 << bitDepth) - 1);

        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                int index = row * width + col;
                // Invert pattern every other row
                bool isEvenPixel = (row % 2 == 0) ? (col % 2 == 0) : (col % 2 == 1);
                pixels[index] = isEvenPixel ? (ushort)0 : maxValue;
            }
        }

        return pixels;
    }
}
