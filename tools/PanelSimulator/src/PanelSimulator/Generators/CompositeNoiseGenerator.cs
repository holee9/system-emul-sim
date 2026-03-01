using System;

namespace PanelSimulator.Generators;

/// <summary>
/// Configuration for the composite noise model.
/// </summary>
/// <param name="EnablePoissonNoise">Enable Poisson (photon shot) noise.</param>
/// <param name="EnableGaussianNoise">Enable Gaussian (electronic readout) noise.</param>
/// <param name="EnableDarkCurrent">Enable dark current noise contribution.</param>
/// <param name="EnableFlickerNoise">Enable 1/f (flicker) noise.</param>
/// <param name="ReadoutNoiseElectrons">Electronic readout noise in electrons RMS (typical: 2-10 e-).</param>
/// <param name="DarkCurrentElectrons">Dark current contribution in electrons per pixel per frame.</param>
/// <param name="FlickerNoiseAmplitude">1/f noise amplitude as fraction of signal (typical: 0.001-0.01).</param>
/// <param name="NoiseFloorDN">Minimum noise floor in digital numbers.</param>
/// <param name="FullWellCapacity">Full well capacity in electrons.</param>
/// <param name="AdcBits">ADC bit depth.</param>
public sealed record CompositeNoiseConfig(
    bool EnablePoissonNoise = true,
    bool EnableGaussianNoise = true,
    bool EnableDarkCurrent = true,
    bool EnableFlickerNoise = false,
    double ReadoutNoiseElectrons = 5.0,
    double DarkCurrentElectrons = 10.0,
    double FlickerNoiseAmplitude = 0.005,
    double NoiseFloorDN = 1.0,
    double FullWellCapacity = 1_000_000.0,
    int AdcBits = 16);

/// <summary>
/// Combines all noise sources for realistic X-ray detector simulation.
/// Noise model: Poisson(photon) + Gaussian(electronic) + Dark Current + 1/f Noise.
/// Uses seed-based random generators for reproducibility.
/// </summary>
public sealed class CompositeNoiseGenerator
{
    private readonly CompositeNoiseConfig _config;
    private readonly int _seed;
    private readonly double _electronsPerDN;

    /// <summary>
    /// Initializes a new instance of the CompositeNoiseGenerator.
    /// </summary>
    /// <param name="seed">Random seed for deterministic, reproducible output.</param>
    /// <param name="config">Noise configuration. Uses defaults if null.</param>
    public CompositeNoiseGenerator(int seed, CompositeNoiseConfig? config = null)
    {
        _config = config ?? new CompositeNoiseConfig();
        _seed = seed;

        if (_config.ReadoutNoiseElectrons < 0)
        {
            throw new ArgumentException("Readout noise must be non-negative.", nameof(config));
        }

        if (_config.DarkCurrentElectrons < 0)
        {
            throw new ArgumentException("Dark current must be non-negative.", nameof(config));
        }

        if (_config.FlickerNoiseAmplitude < 0)
        {
            throw new ArgumentException("Flicker noise amplitude must be non-negative.", nameof(config));
        }

        if (_config.NoiseFloorDN < 0)
        {
            throw new ArgumentException("Noise floor must be non-negative.", nameof(config));
        }

        double adcMax = (1 << _config.AdcBits) - 1;
        _electronsPerDN = _config.FullWellCapacity / adcMax;
    }

    /// <summary>
    /// Gets the current configuration.
    /// </summary>
    public CompositeNoiseConfig Config => _config;

    /// <summary>
    /// Applies all enabled noise sources to a 2D frame.
    /// Processing order: Dark current -> Poisson -> Gaussian -> 1/f -> Noise floor.
    /// </summary>
    /// <param name="frame">Input signal frame (rows x cols) in DN.</param>
    /// <param name="frameNumber">Frame sequence number (used for 1/f noise correlation).</param>
    /// <returns>New frame with composite noise applied.</returns>
    public ushort[,] ApplyNoise(ushort[,] frame, int frameNumber = 0)
    {
        if (frame == null)
        {
            throw new ArgumentNullException(nameof(frame));
        }

        int rows = frame.GetLength(0);
        int cols = frame.GetLength(1);

        // Work in double precision for intermediate calculations
        double[,] working = ConvertToDouble(frame);

        // 1. Add dark current (uniform offset)
        if (_config.EnableDarkCurrent && _config.DarkCurrentElectrons > 0)
        {
            double darkDN = _config.DarkCurrentElectrons / _electronsPerDN;
            AddConstant(working, darkDN);
        }

        // 2. Poisson noise (signal-dependent, applied to total signal including dark current)
        if (_config.EnablePoissonNoise)
        {
            var poissonGen = new PoissonNoiseGenerator(_seed + frameNumber * 3);
            ApplyPoissonInPlace(working, poissonGen);
        }

        // 3. Gaussian readout noise (signal-independent)
        if (_config.EnableGaussianNoise && _config.ReadoutNoiseElectrons > 0)
        {
            double readoutNoiseDN = _config.ReadoutNoiseElectrons / _electronsPerDN;
            var gaussianRng = new Random(_seed + frameNumber * 3 + 1);
            ApplyGaussianInPlace(working, readoutNoiseDN, gaussianRng);
        }

        // 4. 1/f (flicker) noise - spatially correlated, low frequency
        if (_config.EnableFlickerNoise && _config.FlickerNoiseAmplitude > 0)
        {
            var flickerRng = new Random(_seed + frameNumber * 3 + 2);
            ApplyFlickerNoiseInPlace(working, flickerRng, rows, cols);
        }

        // 5. Apply noise floor
        if (_config.NoiseFloorDN > 0)
        {
            ApplyNoiseFloor(working, _config.NoiseFloorDN);
        }

        return ConvertToUShort(working);
    }

    /// <summary>
    /// Converts a ushort frame to double for intermediate calculations.
    /// </summary>
    private static double[,] ConvertToDouble(ushort[,] frame)
    {
        int rows = frame.GetLength(0);
        int cols = frame.GetLength(1);
        var result = new double[rows, cols];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                result[r, c] = frame[r, c];
            }
        }
        return result;
    }

    /// <summary>
    /// Converts a double frame back to ushort with clamping.
    /// </summary>
    private static ushort[,] ConvertToUShort(double[,] frame)
    {
        int rows = frame.GetLength(0);
        int cols = frame.GetLength(1);
        var result = new ushort[rows, cols];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                result[r, c] = ClampToUShort(frame[r, c]);
            }
        }
        return result;
    }

    /// <summary>
    /// Adds a constant value to all pixels.
    /// </summary>
    private static void AddConstant(double[,] frame, double value)
    {
        int rows = frame.GetLength(0);
        int cols = frame.GetLength(1);
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                frame[r, c] += value;
            }
        }
    }

    /// <summary>
    /// Applies Poisson noise in-place on the working double frame.
    /// Each pixel value is replaced by a Poisson sample with lambda = pixel value.
    /// </summary>
    private static void ApplyPoissonInPlace(double[,] frame, PoissonNoiseGenerator poissonGen)
    {
        int rows = frame.GetLength(0);
        int cols = frame.GetLength(1);

        // Convert to ushort frame for Poisson generator, then back
        var tempFrame = new ushort[rows, cols];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                tempFrame[r, c] = ClampToUShort(frame[r, c]);
            }
        }

        var noisyFrame = poissonGen.ApplyNoise(tempFrame);
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                frame[r, c] = noisyFrame[r, c];
            }
        }
    }

    /// <summary>
    /// Applies Gaussian readout noise in-place.
    /// </summary>
    private static void ApplyGaussianInPlace(double[,] frame, double stdDev, Random rng)
    {
        int rows = frame.GetLength(0);
        int cols = frame.GetLength(1);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                double noise = NextGaussian(rng) * stdDev;
                frame[r, c] += noise;
            }
        }
    }

    /// <summary>
    /// Applies 1/f (flicker) noise in-place.
    /// Models low-frequency spatial noise by adding row-correlated and column-correlated components.
    /// </summary>
    private static void ApplyFlickerNoiseInPlace(
        double[,] frame,
        Random rng,
        int rows,
        int cols)
    {
        // Generate row-correlated noise (same noise for entire row)
        double[] rowNoise = new double[rows];
        for (int r = 0; r < rows; r++)
        {
            rowNoise[r] = NextGaussian(rng);
        }

        // Generate column-correlated noise (same noise for entire column)
        double[] colNoise = new double[cols];
        for (int c = 0; c < cols; c++)
        {
            colNoise[c] = NextGaussian(rng);
        }

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                // 1/f noise combines row and column correlations
                double flickerNoise = (rowNoise[r] + colNoise[c]) * 0.5;
                // Scale by signal level (multiplicative noise component)
                double signalLevel = Math.Max(frame[r, c], 1.0);
                frame[r, c] += flickerNoise * signalLevel * 0.005; // amplitude factor
            }
        }
    }

    /// <summary>
    /// Ensures minimum noise floor is present in the frame.
    /// </summary>
    private static void ApplyNoiseFloor(double[,] frame, double noiseFloor)
    {
        int rows = frame.GetLength(0);
        int cols = frame.GetLength(1);
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (frame[r, c] > 0 && frame[r, c] < noiseFloor)
                {
                    frame[r, c] = noiseFloor;
                }
            }
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
