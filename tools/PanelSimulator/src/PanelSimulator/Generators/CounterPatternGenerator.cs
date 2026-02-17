namespace PanelSimulator.Generators;

/// <summary>
/// Generates a counter test pattern where pixel values increment sequentially.
/// REQ-SIM-013: Counter pattern mode for data integrity verification.
/// AC-SIM-001: pixel[r][c] == (r * cols + c) % 2^bit_depth
/// </summary>
public class CounterPatternGenerator : ITestPatternGenerator
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

        // AC-SIM-001: pixel[r][c] == (r * cols + c) % 2^bit_depth
        int maxValue = (1 << bitDepth) - 1;
        int mask = maxValue;

        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                int index = row * width + col;
                int value = (row * width + col) & mask;
                pixels[index] = (ushort)value;
            }
        }

        return pixels;
    }
}
