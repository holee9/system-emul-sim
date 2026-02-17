using System;

namespace PanelSimulator.Generators;

/// <summary>
/// Generates Gaussian (normal) distribution noise for pixel values.
/// REQ-SIM-011: Noise model with configurable standard deviation.
/// AC-SIM-002: Noise model validation.
/// </summary>
public class GaussianNoiseGenerator
{
    private readonly double _standardDeviation;
    private readonly Random _random;
    private bool _hasSpareValue;
    private double _spareValue;

    /// <summary>
    /// Initializes a new instance of the GaussianNoiseGenerator.
    /// </summary>
    /// <param name="standardDeviation">Standard deviation of the noise.</param>
    /// <param name="seed">Random seed for deterministic output.</param>
    public GaussianNoiseGenerator(double standardDeviation, int seed)
    {
        if (standardDeviation < 0)
        {
            throw new ArgumentException("Standard deviation must be non-negative.", nameof(standardDeviation));
        }

        _standardDeviation = standardDeviation;
        _random = new Random(seed);
        _hasSpareValue = false;
        _spareValue = 0;
    }

    /// <summary>
    /// Applies Gaussian noise to the input pixel array.
    /// </summary>
    /// <param name="pixels">Input pixel array.</param>
    /// <returns>Noisy pixel array.</returns>
    public ushort[] ApplyNoise(ushort[] pixels)
    {
        if (pixels == null)
        {
            throw new ArgumentNullException(nameof(pixels));
        }

        ushort[] noisyPixels = new ushort[pixels.Length];

        for (int i = 0; i < pixels.Length; i++)
        {
            double noise = GetNextGaussian() * _standardDeviation;
            double noisyValue = pixels[i] + noise;

            // Clamp to valid range
            noisyPixels[i] = ClampToUShort(noisyValue);
        }

        return noisyPixels;
    }

    /// <summary>
    /// Gets the next value from a standard Gaussian (normal) distribution.
    /// Uses the Box-Muller transform to generate normally distributed random numbers.
    /// </summary>
    private double GetNextGaussian()
    {
        if (_hasSpareValue)
        {
            _hasSpareValue = false;
            return _spareValue;
        }

        // Box-Muller transform
        double u1 = _random.NextDouble();
        double u2 = _random.NextDouble();

        // Avoid log(0)
        u1 = Math.Max(u1, double.Epsilon);

        double radius = Math.Sqrt(-2.0 * Math.Log(u1));
        double theta = 2.0 * Math.PI * u2;

        double standardNormal1 = radius * Math.Cos(theta);
        double standardNormal2 = radius * Math.Sin(theta);

        _spareValue = standardNormal2;
        _hasSpareValue = true;

        return standardNormal1;
    }

    /// <summary>
    /// Clamps a double value to the ushort range [0, 65535].
    /// </summary>
    private static ushort ClampToUShort(double value)
    {
        if (value < 0)
        {
            return 0;
        }

        if (value > 65535)
        {
            return 65535;
        }

        return (ushort)Math.Round(value);
    }
}
