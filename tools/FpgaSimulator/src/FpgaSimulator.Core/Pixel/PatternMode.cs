namespace FpgaSimulator.Core.Pixel;

/// <summary>
/// Test pattern generation modes for pixel data.
/// Used for testing and validation of the data pipeline.
/// </summary>
public enum PatternMode
{
    /// <summary>Sequential pixel values: pixel[r,c] = (r * cols + c) % 2^bit_depth</summary>
    Counter,

    /// <summary>All pixels have the same constant value</summary>
    Constant,

    /// <summary>Pseudo-random values with configurable seed</summary>
    Random,

    /// <summary>Alternating max/zero values (checkerboard pattern)</summary>
    Checkerboard
}
