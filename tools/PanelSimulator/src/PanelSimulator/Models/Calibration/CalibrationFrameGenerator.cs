using System;
using PanelSimulator.Generators;

namespace PanelSimulator.Models.Calibration;

/// <summary>
/// Configuration for calibration frame generation.
/// </summary>
/// <param name="Rows">Number of rows.</param>
/// <param name="Cols">Number of columns.</param>
/// <param name="AdcBits">ADC bit depth (14 or 16).</param>
/// <param name="ReadoutNoiseElectrons">Electronic readout noise in electrons RMS.</param>
/// <param name="DarkCurrentElectrons">Dark current in electrons per pixel per frame.</param>
/// <param name="FullWellCapacity">Full well capacity in electrons.</param>
/// <param name="FlatFieldSignalDN">Signal level for flat field frames in DN.</param>
public sealed record CalibrationConfig(
    int Rows = 256,
    int Cols = 256,
    int AdcBits = 16,
    double ReadoutNoiseElectrons = 5.0,
    double DarkCurrentElectrons = 10.0,
    double FullWellCapacity = 1_000_000.0,
    ushort FlatFieldSignalDN = 32768);

/// <summary>
/// Generates calibration reference frames for detector characterization.
/// Produces dark frames, flat field frames, and bias frames with realistic noise.
/// </summary>
public sealed class CalibrationFrameGenerator
{
    private readonly CalibrationConfig _config;
    private readonly double _electronsPerDN;

    /// <summary>
    /// Initializes a new instance of the CalibrationFrameGenerator.
    /// </summary>
    /// <param name="config">Calibration configuration. Uses defaults if null.</param>
    public CalibrationFrameGenerator(CalibrationConfig? config = null)
    {
        _config = config ?? new CalibrationConfig();

        if (_config.Rows <= 0)
        {
            throw new ArgumentException("Rows must be positive.", nameof(config));
        }

        if (_config.Cols <= 0)
        {
            throw new ArgumentException("Cols must be positive.", nameof(config));
        }

        if (_config.AdcBits < 1 || _config.AdcBits > 16)
        {
            throw new ArgumentException("ADC bits must be between 1 and 16.", nameof(config));
        }

        double adcMax = (1 << _config.AdcBits) - 1;
        _electronsPerDN = _config.FullWellCapacity / adcMax;
    }

    /// <summary>
    /// Gets the current configuration.
    /// </summary>
    public CalibrationConfig Config => _config;

    /// <summary>
    /// Generates a dark frame (no X-ray exposure).
    /// Contains offset + dark current + readout noise.
    /// </summary>
    /// <param name="gainOffsetMap">Optional gain/offset map for non-uniformity. Uses flat map if null.</param>
    /// <param name="seed">Random seed for reproducibility.</param>
    /// <returns>Dark frame with realistic noise characteristics.</returns>
    public ushort[,] GenerateDarkFrame(GainOffsetMap? gainOffsetMap = null, int seed = 42)
    {
        var map = gainOffsetMap ?? GainOffsetMap.CreateFlat(_config.Rows, _config.Cols);
        ValidateMapDimensions(map);

        var rng = new Random(seed);
        var frame = new ushort[_config.Rows, _config.Cols];

        // Dark frame = offset + dark current + readout noise
        double darkDN = _config.DarkCurrentElectrons / _electronsPerDN;
        double readoutNoiseDN = _config.ReadoutNoiseElectrons / _electronsPerDN;

        for (int r = 0; r < _config.Rows; r++)
        {
            for (int c = 0; c < _config.Cols; c++)
            {
                // Base: offset from gain/offset map
                double value = map.GetOffset(r, c);

                // Add dark current (with Poisson statistics)
                if (darkDN > 0)
                {
                    value += darkDN + NextGaussian(rng) * Math.Sqrt(darkDN);
                }

                // Add readout noise (Gaussian)
                value += NextGaussian(rng) * readoutNoiseDN;

                frame[r, c] = ClampToUShort(value);
            }
        }

        return frame;
    }

    /// <summary>
    /// Generates a flat field frame (uniform X-ray exposure).
    /// Contains signal + gain non-uniformity + offset + all noise sources.
    /// </summary>
    /// <param name="gainOffsetMap">Optional gain/offset map for non-uniformity. Uses flat map if null.</param>
    /// <param name="seed">Random seed for reproducibility.</param>
    /// <returns>Flat field frame with realistic non-uniformity and noise.</returns>
    public ushort[,] GenerateFlatFieldFrame(GainOffsetMap? gainOffsetMap = null, int seed = 42)
    {
        var map = gainOffsetMap ?? GainOffsetMap.CreateFlat(_config.Rows, _config.Cols);
        ValidateMapDimensions(map);

        var rng = new Random(seed);
        var frame = new ushort[_config.Rows, _config.Cols];

        double readoutNoiseDN = _config.ReadoutNoiseElectrons / _electronsPerDN;
        double darkDN = _config.DarkCurrentElectrons / _electronsPerDN;

        for (int r = 0; r < _config.Rows; r++)
        {
            for (int c = 0; c < _config.Cols; c++)
            {
                // Start with uniform X-ray signal
                double signal = _config.FlatFieldSignalDN;

                // Apply per-pixel gain variation
                signal *= map.GetGain(r, c);

                // Add per-pixel offset
                signal += map.GetOffset(r, c);

                // Add dark current
                signal += darkDN;

                // Add Poisson noise (signal-dependent)
                if (signal > 0)
                {
                    signal += NextGaussian(rng) * Math.Sqrt(signal);
                }

                // Add readout noise (signal-independent)
                signal += NextGaussian(rng) * readoutNoiseDN;

                frame[r, c] = ClampToUShort(signal);
            }
        }

        return frame;
    }

    /// <summary>
    /// Generates a bias frame (zero exposure time, electronic offset only).
    /// Contains only the electronic offset and readout noise, no dark current or X-ray signal.
    /// </summary>
    /// <param name="gainOffsetMap">Optional gain/offset map for non-uniformity. Uses flat map if null.</param>
    /// <param name="seed">Random seed for reproducibility.</param>
    /// <returns>Bias frame with electronic offset and readout noise only.</returns>
    public ushort[,] GenerateBiasFrame(GainOffsetMap? gainOffsetMap = null, int seed = 42)
    {
        var map = gainOffsetMap ?? GainOffsetMap.CreateFlat(_config.Rows, _config.Cols);
        ValidateMapDimensions(map);

        var rng = new Random(seed);
        var frame = new ushort[_config.Rows, _config.Cols];

        double readoutNoiseDN = _config.ReadoutNoiseElectrons / _electronsPerDN;

        for (int r = 0; r < _config.Rows; r++)
        {
            for (int c = 0; c < _config.Cols; c++)
            {
                // Bias frame = offset + readout noise only (no signal, no dark current)
                double value = map.GetOffset(r, c);

                // Add readout noise (Gaussian)
                value += NextGaussian(rng) * readoutNoiseDN;

                frame[r, c] = ClampToUShort(value);
            }
        }

        return frame;
    }

    /// <summary>
    /// Generates multiple averaged dark frames for improved noise estimation.
    /// Averaging N frames reduces noise by sqrt(N).
    /// </summary>
    /// <param name="count">Number of frames to average.</param>
    /// <param name="gainOffsetMap">Optional gain/offset map.</param>
    /// <param name="baseSeed">Base random seed.</param>
    /// <returns>Averaged dark frame.</returns>
    public ushort[,] GenerateAveragedDarkFrame(
        int count,
        GainOffsetMap? gainOffsetMap = null,
        int baseSeed = 42)
    {
        if (count < 1)
        {
            throw new ArgumentException("Count must be at least 1.", nameof(count));
        }

        var accumulator = new double[_config.Rows, _config.Cols];

        for (int i = 0; i < count; i++)
        {
            var darkFrame = GenerateDarkFrame(gainOffsetMap, baseSeed + i);
            for (int r = 0; r < _config.Rows; r++)
            {
                for (int c = 0; c < _config.Cols; c++)
                {
                    accumulator[r, c] += darkFrame[r, c];
                }
            }
        }

        var result = new ushort[_config.Rows, _config.Cols];
        for (int r = 0; r < _config.Rows; r++)
        {
            for (int c = 0; c < _config.Cols; c++)
            {
                result[r, c] = ClampToUShort(accumulator[r, c] / count);
            }
        }

        return result;
    }

    private void ValidateMapDimensions(GainOffsetMap map)
    {
        if (map.Rows != _config.Rows || map.Cols != _config.Cols)
        {
            throw new ArgumentException(
                $"GainOffsetMap dimensions ({map.Rows}x{map.Cols}) " +
                $"must match config dimensions ({_config.Rows}x{_config.Cols}).");
        }
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
