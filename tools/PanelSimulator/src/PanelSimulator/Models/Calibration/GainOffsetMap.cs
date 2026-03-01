using System;

namespace PanelSimulator.Models.Calibration;

/// <summary>
/// Per-pixel gain and offset correction map for flat-field calibration.
/// Gain models pixel sensitivity variation (typically +/-5% around 1.0).
/// Offset models pixel-level electronic offset (dark level variation).
/// </summary>
public sealed class GainOffsetMap
{
    private readonly double[,] _gainMap;
    private readonly double[,] _offsetMap;

    /// <summary>
    /// Gets the number of rows.
    /// </summary>
    public int Rows { get; }

    /// <summary>
    /// Gets the number of columns.
    /// </summary>
    public int Cols { get; }

    /// <summary>
    /// Initializes a new instance of GainOffsetMap with pre-computed maps.
    /// </summary>
    /// <param name="gainMap">Per-pixel gain map (multiplicative, centered around 1.0).</param>
    /// <param name="offsetMap">Per-pixel offset map (additive, in DN).</param>
    public GainOffsetMap(double[,] gainMap, double[,] offsetMap)
    {
        if (gainMap == null)
        {
            throw new ArgumentNullException(nameof(gainMap));
        }

        if (offsetMap == null)
        {
            throw new ArgumentNullException(nameof(offsetMap));
        }

        if (gainMap.GetLength(0) != offsetMap.GetLength(0) ||
            gainMap.GetLength(1) != offsetMap.GetLength(1))
        {
            throw new ArgumentException("Gain and offset maps must have the same dimensions.");
        }

        Rows = gainMap.GetLength(0);
        Cols = gainMap.GetLength(1);
        _gainMap = gainMap;
        _offsetMap = offsetMap;
    }

    /// <summary>
    /// Gets the gain value at the specified pixel location.
    /// </summary>
    public double GetGain(int row, int col)
    {
        ValidateCoordinates(row, col);
        return _gainMap[row, col];
    }

    /// <summary>
    /// Gets the offset value at the specified pixel location.
    /// </summary>
    public double GetOffset(int row, int col)
    {
        ValidateCoordinates(row, col);
        return _offsetMap[row, col];
    }

    /// <summary>
    /// Applies gain and offset to a frame: output[r,c] = input[r,c] * gain[r,c] + offset[r,c].
    /// This simulates the detector non-uniformity (applying the map forward).
    /// </summary>
    /// <param name="frame">Input signal frame.</param>
    /// <returns>New frame with gain/offset non-uniformity applied.</returns>
    public ushort[,] ApplyForward(ushort[,] frame)
    {
        if (frame == null)
        {
            throw new ArgumentNullException(nameof(frame));
        }

        ValidateFrameDimensions(frame);

        var result = new ushort[Rows, Cols];
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Cols; c++)
            {
                double corrected = frame[r, c] * _gainMap[r, c] + _offsetMap[r, c];
                result[r, c] = ClampToUShort(corrected);
            }
        }

        return result;
    }

    /// <summary>
    /// Applies gain/offset correction (inverse) to a frame:
    /// output[r,c] = (input[r,c] - offset[r,c]) / gain[r,c].
    /// This removes the detector non-uniformity (calibration correction).
    /// </summary>
    /// <param name="frame">Input raw frame with non-uniformity.</param>
    /// <returns>New frame with gain/offset correction applied.</returns>
    public ushort[,] ApplyCorrection(ushort[,] frame)
    {
        if (frame == null)
        {
            throw new ArgumentNullException(nameof(frame));
        }

        ValidateFrameDimensions(frame);

        var result = new ushort[Rows, Cols];
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Cols; c++)
            {
                double gain = _gainMap[r, c];
                if (Math.Abs(gain) < double.Epsilon)
                {
                    // Avoid division by zero - dead pixel
                    result[r, c] = 0;
                    continue;
                }

                double corrected = (frame[r, c] - _offsetMap[r, c]) / gain;
                result[r, c] = ClampToUShort(corrected);
            }
        }

        return result;
    }

    /// <summary>
    /// Generates a flat (uniform) gain/offset map.
    /// All gains are 1.0, all offsets are 0.0 (ideal detector).
    /// </summary>
    /// <param name="rows">Number of rows.</param>
    /// <param name="cols">Number of columns.</param>
    /// <returns>A flat GainOffsetMap.</returns>
    public static GainOffsetMap CreateFlat(int rows, int cols)
    {
        if (rows <= 0)
        {
            throw new ArgumentException("Rows must be positive.", nameof(rows));
        }

        if (cols <= 0)
        {
            throw new ArgumentException("Cols must be positive.", nameof(cols));
        }

        var gainMap = new double[rows, cols];
        var offsetMap = new double[rows, cols];

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                gainMap[r, c] = 1.0;
                offsetMap[r, c] = 0.0;
            }
        }

        return new GainOffsetMap(gainMap, offsetMap);
    }

    /// <summary>
    /// Generates a random gain/offset map simulating detector non-uniformity.
    /// Gain values are normally distributed around 1.0 with the specified standard deviation.
    /// Offset values are normally distributed around 0.0 with the specified standard deviation.
    /// </summary>
    /// <param name="rows">Number of rows.</param>
    /// <param name="cols">Number of columns.</param>
    /// <param name="gainStdDev">Standard deviation of gain variation (typical: 0.03-0.05 for +/-5%).</param>
    /// <param name="offsetStdDev">Standard deviation of offset variation in DN (typical: 5-20).</param>
    /// <param name="seed">Random seed for reproducibility.</param>
    /// <returns>A random GainOffsetMap.</returns>
    public static GainOffsetMap CreateRandom(
        int rows,
        int cols,
        double gainStdDev = 0.05,
        double offsetStdDev = 10.0,
        int seed = 42)
    {
        if (rows <= 0)
        {
            throw new ArgumentException("Rows must be positive.", nameof(rows));
        }

        if (cols <= 0)
        {
            throw new ArgumentException("Cols must be positive.", nameof(cols));
        }

        if (gainStdDev < 0)
        {
            throw new ArgumentException("Gain standard deviation must be non-negative.", nameof(gainStdDev));
        }

        if (offsetStdDev < 0)
        {
            throw new ArgumentException("Offset standard deviation must be non-negative.", nameof(offsetStdDev));
        }

        var rng = new Random(seed);
        var gainMap = new double[rows, cols];
        var offsetMap = new double[rows, cols];

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                // Gain centered around 1.0 with Gaussian variation
                double gainNoise = NextGaussian(rng) * gainStdDev;
                gainMap[r, c] = Math.Max(0.01, 1.0 + gainNoise); // Ensure positive gain

                // Offset centered around 0.0 with Gaussian variation
                offsetMap[r, c] = NextGaussian(rng) * offsetStdDev;
            }
        }

        return new GainOffsetMap(gainMap, offsetMap);
    }

    /// <summary>
    /// Generates a standard normal random number using Box-Muller transform.
    /// </summary>
    private static double NextGaussian(Random rng)
    {
        double u1 = Math.Max(rng.NextDouble(), double.Epsilon);
        double u2 = rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    private void ValidateCoordinates(int row, int col)
    {
        if (row < 0 || row >= Rows)
        {
            throw new ArgumentOutOfRangeException(nameof(row), $"Row must be in [0, {Rows - 1}].");
        }

        if (col < 0 || col >= Cols)
        {
            throw new ArgumentOutOfRangeException(nameof(col), $"Col must be in [0, {Cols - 1}].");
        }
    }

    private void ValidateFrameDimensions(ushort[,] frame)
    {
        if (frame.GetLength(0) != Rows || frame.GetLength(1) != Cols)
        {
            throw new ArgumentException(
                $"Frame dimensions ({frame.GetLength(0)}x{frame.GetLength(1)}) " +
                $"must match map dimensions ({Rows}x{Cols}).");
        }
    }

    /// <summary>
    /// Clamps a double value to the ushort range [0, 65535].
    /// </summary>
    private static ushort ClampToUShort(double value)
    {
        if (value < 0) return 0;
        if (value > 65535) return 65535;
        return (ushort)Math.Round(value);
    }
}
