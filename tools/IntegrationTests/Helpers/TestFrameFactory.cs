using Common.Dto.Dtos;

namespace IntegrationTests.Helpers;

/// <summary>
/// Factory for creating test frames with predictable pixel patterns.
/// Supports 16-bit pixel depth and standard panel resolutions.
/// Uses FrameData DTO for simulator integration tests.
/// </summary>
public static class TestFrameFactory
{
    /// <summary>
    /// Pattern types for test frame generation.
    /// </summary>
    public enum PatternType
    {
        /// <summary>Solid color (all pixels same value).</summary>
        Solid,

        /// <summary>Horizontal gradient (left to right).</summary>
        Gradient,

        /// <summary>Checkerboard pattern (alternating pixels).</summary>
        Checkerboard
    }

    /// <summary>
    /// Creates a test frame with the specified pattern.
    /// </summary>
    /// <param name="width">Frame width in pixels.</param>
    /// <param name="height">Frame height in pixels.</param>
    /// <param name="patternType">Pattern type to generate.</param>
    /// <param name="frameNumber">Frame sequence number.</param>
    /// <returns>Test frame data with generated pixel data.</returns>
    public static FrameData CreateTestFrame(int width, int height, PatternType patternType, int frameNumber = 0)
    {
        if (width < 1 || height < 1)
            throw new ArgumentException("Width and height must be at least 1.");

        ushort[] pixelData = new ushort[width * height];

        switch (patternType)
        {
            case PatternType.Solid:
                CreateSolidPattern(pixelData, width, height);
                break;
            case PatternType.Gradient:
                CreateGradientPattern(pixelData, width, height);
                break;
            case PatternType.Checkerboard:
                CreateCheckerboardPattern(pixelData, width, height);
                break;
            default:
                throw new ArgumentException($"Unknown pattern type: {patternType}");
        }

        return new FrameData(frameNumber, width, height, pixelData);
    }

    /// <summary>
    /// Creates a frame with horizontal gradient pattern (0 to 65535 left to right).
    /// </summary>
    /// <param name="width">Frame width in pixels.</param>
    /// <param name="height">Frame height in pixels.</param>
    /// <returns>Test frame data with gradient pattern.</returns>
    public static FrameData CreateGradientFrame(int width, int height, int frameNumber = 0)
    {
        return CreateTestFrame(width, height, PatternType.Gradient, frameNumber);
    }

    /// <summary>
    /// Creates a frame with solid color pattern (all pixels at 32768, mid-gray).
    /// </summary>
    /// <param name="width">Frame width in pixels.</param>
    /// <param name="height">Frame height in pixels.</param>
    /// <param name="value">Solid pixel value (default: 32768, mid-gray).</param>
    /// <returns>Test frame data with solid pattern.</returns>
    public static FrameData CreateSolidFrame(int width, int height, ushort value = 32768, int frameNumber = 0)
    {
        if (width < 1 || height < 1)
            throw new ArgumentException("Width and height must be at least 1.");

        ushort[] pixelData = new ushort[width * height];
        Array.Fill(pixelData, value);

        return new FrameData(frameNumber, width, height, pixelData);
    }

    /// <summary>
    /// Creates a standard 1024x1024 test frame with gradient pattern.
    /// </summary>
    public static FrameData Create1024Gradient(int frameNumber = 0) =>
        CreateGradientFrame(1024, 1024, frameNumber);

    /// <summary>
    /// Creates a standard 2048x2048 test frame with gradient pattern.
    /// </summary>
    public static FrameData Create2048Gradient(int frameNumber = 0) =>
        CreateGradientFrame(2048, 2048, frameNumber);

    /// <summary>
    /// Creates a standard 3072x3072 test frame with gradient pattern.
    /// </summary>
    public static FrameData Create3072Gradient(int frameNumber = 0) =>
        CreateGradientFrame(3072, 3072, frameNumber);

    private static void CreateSolidPattern(ushort[] pixelData, int width, int height)
    {
        const ushort midGray = 32768;
        Array.Fill(pixelData, midGray);
    }

    private static void CreateGradientPattern(ushort[] pixelData, int width, int height)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // Horizontal gradient: 0 at left, 65535 at right
                ushort value = (ushort)((x * 65535) / (width - 1));
                pixelData[y * width + x] = value;
            }
        }
    }

    private static void CreateCheckerboardPattern(ushort[] pixelData, int width, int height)
    {
        const ushort black = 0;
        const ushort white = 65535;
        const int checkerSize = 8; // 8x8 pixel checkers

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool isWhite = ((x / checkerSize) + (y / checkerSize)) % 2 == 0;
                pixelData[y * width + x] = isWhite ? white : black;
            }
        }
    }
}
