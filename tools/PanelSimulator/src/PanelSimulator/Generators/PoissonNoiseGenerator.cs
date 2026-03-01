using System;

namespace PanelSimulator.Generators;

/// <summary>
/// Generates Poisson-distributed photon statistics noise.
/// Models the inherent quantum noise in X-ray detection where variance equals the mean signal.
/// </summary>
public sealed class PoissonNoiseGenerator
{
    private readonly Random _random;

    /// <summary>
    /// Initializes a new instance of the PoissonNoiseGenerator.
    /// </summary>
    /// <param name="seed">Random seed for deterministic output.</param>
    public PoissonNoiseGenerator(int seed)
    {
        _random = new Random(seed);
    }

    /// <summary>
    /// Applies Poisson noise to a 2D frame.
    /// Each pixel value is treated as the mean (lambda) of a Poisson distribution.
    /// The output pixel is a random sample from Poisson(lambda = input pixel value).
    /// </summary>
    /// <param name="frame">Input signal frame (rows x cols).</param>
    /// <returns>New frame with Poisson noise applied.</returns>
    public ushort[,] ApplyNoise(ushort[,] frame)
    {
        if (frame == null)
        {
            throw new ArgumentNullException(nameof(frame));
        }

        int rows = frame.GetLength(0);
        int cols = frame.GetLength(1);
        var result = new ushort[rows, cols];

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                double lambda = frame[r, c];
                double sample = SamplePoisson(lambda);
                result[r, c] = ClampToUShort(sample);
            }
        }

        return result;
    }

    /// <summary>
    /// Applies Poisson noise to a 1D pixel array.
    /// </summary>
    /// <param name="pixels">Input pixel array.</param>
    /// <returns>New array with Poisson noise applied.</returns>
    public ushort[] ApplyNoise(ushort[] pixels)
    {
        if (pixels == null)
        {
            throw new ArgumentNullException(nameof(pixels));
        }

        var result = new ushort[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            double lambda = pixels[i];
            double sample = SamplePoisson(lambda);
            result[i] = ClampToUShort(sample);
        }

        return result;
    }

    /// <summary>
    /// Samples from a Poisson distribution with the given mean (lambda).
    /// Uses Knuth's algorithm for small lambda (&lt;= 30) and
    /// Gaussian approximation for large lambda (> 30).
    /// </summary>
    /// <param name="lambda">Mean of the Poisson distribution.</param>
    /// <returns>A Poisson-distributed random sample.</returns>
    internal double SamplePoisson(double lambda)
    {
        if (lambda <= 0)
        {
            return 0;
        }

        if (lambda <= 30.0)
        {
            return SamplePoissonKnuth(lambda);
        }

        // For large lambda, use Gaussian approximation:
        // Poisson(lambda) ~ N(lambda, sqrt(lambda))
        return SamplePoissonGaussian(lambda);
    }

    /// <summary>
    /// Knuth's algorithm for Poisson sampling (accurate for small lambda).
    /// </summary>
    private double SamplePoissonKnuth(double lambda)
    {
        double l = Math.Exp(-lambda);
        int k = 0;
        double p = 1.0;

        do
        {
            k++;
            p *= _random.NextDouble();
        } while (p > l);

        return k - 1;
    }

    /// <summary>
    /// Gaussian approximation for Poisson sampling (efficient for large lambda).
    /// Poisson(lambda) ~ Normal(lambda, sqrt(lambda)) for lambda >> 1.
    /// </summary>
    private double SamplePoissonGaussian(double lambda)
    {
        // Box-Muller transform for Gaussian sample
        double u1 = Math.Max(_random.NextDouble(), double.Epsilon);
        double u2 = _random.NextDouble();
        double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);

        // Scale to Poisson approximation
        double sample = lambda + Math.Sqrt(lambda) * z;
        return Math.Max(0, Math.Round(sample));
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
