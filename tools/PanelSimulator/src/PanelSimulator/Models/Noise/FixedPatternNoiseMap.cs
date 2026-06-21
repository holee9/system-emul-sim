using System;

namespace PanelSimulator.Models.Noise;

/// <summary>
/// Fixed Pattern Noise (FPN) map for realistic X-ray detector simulation.
/// REQ-PHY-003: Generates a per-session, seed-based, frame-invariant noise map
/// composed of three layers: column FPN, row FPN, and per-pixel FPN.
///
/// Physical origin:
///   - Column FPN: ROIC ADC channel-to-channel offset differences
///   - Row FPN:    Gate line RC delay causing row-to-row gain variation
///   - Pixel FPN:  Photodiode sensitivity variation (PRNU)
///
/// The map is computed once at construction and reused across all frames (frame-invariant).
/// </summary>
public sealed class FixedPatternNoiseMap
{
    private readonly double[,] _map;

    /// <summary>
    /// Initializes the FPN map. Computed once and cached for all subsequent Apply() calls.
    /// </summary>
    /// <param name="rows">Frame height in pixels.</param>
    /// <param name="cols">Frame width in pixels.</param>
    /// <param name="amplitudeFraction">FPN amplitude as a fraction of signal (e.g. 0.015 = 1.5%).</param>
    /// <param name="seed">Random seed for reproducible output.</param>
    public FixedPatternNoiseMap(int rows, int cols, double amplitudeFraction, int seed)
    {
        if (rows <= 0) throw new ArgumentException("Rows must be positive.", nameof(rows));
        if (cols <= 0) throw new ArgumentException("Cols must be positive.", nameof(cols));
        if (amplitudeFraction < 0) throw new ArgumentException("Amplitude must be non-negative.", nameof(amplitudeFraction));

        _map = ComputeMap(rows, cols, amplitudeFraction, seed);
    }

    /// <summary>
    /// Applies the FPN map to a frame. Each pixel is multiplied by (1 + map[r,c]).
    /// The map is frame-invariant — same spatial pattern on every call.
    /// </summary>
    /// <param name="frame">Input frame (rows × cols).</param>
    /// <returns>New frame with FPN applied.</returns>
    public ushort[,] Apply(ushort[,] frame)
    {
        if (frame == null) throw new ArgumentNullException(nameof(frame));

        int rows = frame.GetLength(0);
        int cols = frame.GetLength(1);
        var result = new ushort[rows, cols];

        int mapRows = _map.GetLength(0);
        int mapCols = _map.GetLength(1);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                double mapValue = (r < mapRows && c < mapCols) ? _map[r, c] : 0.0;
                double fpnPixel = frame[r, c] * (1.0 + mapValue);
                result[r, c] = ClampToUShort(fpnPixel);
            }
        }

        return result;
    }

    /// <summary>
    /// Exposes the raw map for testing purposes (read-only access via indexed copy).
    /// </summary>
    /// <returns>Copy of the FPN map as a 2D array.</returns>
    public double[,] GetMapCopy()
    {
        int rows = _map.GetLength(0);
        int cols = _map.GetLength(1);
        var copy = new double[rows, cols];
        Array.Copy(_map, copy, _map.Length);
        return copy;
    }

    /// <summary>
    /// Computes the FPN map from three additive layers:
    ///   1. Column FPN: low-frequency sinusoid + small random per-column offset
    ///   2. Row FPN:    low-frequency sinusoid + small random per-row offset (half amplitude)
    ///   3. Pixel FPN:  Gaussian per-pixel variation (quarter amplitude)
    /// Total amplitude ≈ amplitudeFraction at each pixel.
    /// </summary>
    private static double[,] ComputeMap(int rows, int cols, double amplitudeFraction, int seed)
    {
        var rng = new Random(seed);
        var map = new double[rows, cols];

        // Layer 1 — Column FPN (dominant, 60% of total amplitude)
        double colAmplitude = amplitudeFraction * 0.60;
        double[] colFpn = new double[cols];
        for (int c = 0; c < cols; c++)
        {
            double sinusoidal = Math.Sin(2.0 * Math.PI * c / (cols / 3.0 + 1.0));
            double random = NextGaussian(rng);
            colFpn[c] = colAmplitude * (0.7 * sinusoidal + 0.3 * random);
        }

        // Layer 2 — Row FPN (half amplitude, 25% of total)
        double rowAmplitude = amplitudeFraction * 0.25;
        double[] rowFpn = new double[rows];
        for (int r = 0; r < rows; r++)
        {
            double sinusoidal = Math.Sin(2.0 * Math.PI * r / (rows / 5.0 + 1.0));
            double random = NextGaussian(rng);
            rowFpn[r] = rowAmplitude * (0.6 * sinusoidal + 0.4 * random);
        }

        // Layer 3 — Pixel FPN (Gaussian per-pixel, 15% of total)
        double pixelAmplitude = amplitudeFraction * 0.15;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                double pixelFpn = pixelAmplitude * NextGaussian(rng);
                map[r, c] = colFpn[c] + rowFpn[r] + pixelFpn;
            }
        }

        return map;
    }

    private static double NextGaussian(Random rng)
    {
        double u1 = Math.Max(rng.NextDouble(), double.Epsilon);
        double u2 = rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    private static ushort ClampToUShort(double value)
    {
        if (value < 0) return 0;
        if (value > 65535) return 65535;
        return (ushort)Math.Round(value);
    }
}
