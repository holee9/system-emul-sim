using System;

namespace PanelSimulator.Models.Physics;

/// <summary>
/// Configuration for the exposure model.
/// </summary>
/// <param name="ExposureTimeMs">Exposure (integration) time in milliseconds.</param>
/// <param name="GatePulseCount">Number of gate_on pulses per exposure.</param>
/// <param name="GatePulseWidthUs">Width of each gate_on pulse in microseconds.</param>
/// <param name="DarkCurrentRatePerSec">Dark current generation rate in electrons/pixel/second.</param>
public sealed record ExposureConfig(
    double ExposureTimeMs = 100.0,
    int GatePulseCount = 1,
    double GatePulseWidthUs = 100_000.0,
    double DarkCurrentRatePerSec = 50.0);

/// <summary>
/// Models gate_on pulse-based exposure timing and signal integration.
/// Calculates exposure-proportional signals and tracks integration time.
/// </summary>
public sealed class ExposureModel
{
    private readonly ExposureConfig _config;
    private double _accumulatedExposureMs;
    private int _accumulatedPulses;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the ExposureModel.
    /// </summary>
    /// <param name="config">Exposure configuration. Uses defaults if null.</param>
    public ExposureModel(ExposureConfig? config = null)
    {
        _config = config ?? new ExposureConfig();

        if (_config.ExposureTimeMs <= 0)
        {
            throw new ArgumentException("Exposure time must be positive.", nameof(config));
        }

        if (_config.GatePulseCount < 1)
        {
            throw new ArgumentException("Gate pulse count must be at least 1.", nameof(config));
        }

        if (_config.GatePulseWidthUs <= 0)
        {
            throw new ArgumentException("Gate pulse width must be positive.", nameof(config));
        }

        if (_config.DarkCurrentRatePerSec < 0)
        {
            throw new ArgumentException("Dark current rate must be non-negative.", nameof(config));
        }

        _accumulatedExposureMs = 0;
        _accumulatedPulses = 0;
    }

    /// <summary>
    /// Gets the current configuration.
    /// </summary>
    public ExposureConfig Config => _config;

    /// <summary>
    /// Gets the total accumulated exposure time in milliseconds.
    /// </summary>
    public double AccumulatedExposureMs
    {
        get
        {
            lock (_lock)
            {
                return _accumulatedExposureMs;
            }
        }
    }

    /// <summary>
    /// Gets the total accumulated pulse count.
    /// </summary>
    public int AccumulatedPulses
    {
        get
        {
            lock (_lock)
            {
                return _accumulatedPulses;
            }
        }
    }

    /// <summary>
    /// Calculates the effective integration time from gate_on pulses.
    /// Total integration = gatePulseCount * gatePulseWidthUs / 1000 (ms).
    /// </summary>
    /// <returns>Effective integration time in milliseconds.</returns>
    public double CalculateEffectiveIntegrationTimeMs()
    {
        return _config.GatePulseCount * _config.GatePulseWidthUs / 1000.0;
    }

    /// <summary>
    /// Calculates the exposure scaling factor relative to the nominal exposure time.
    /// </summary>
    /// <returns>Scaling factor where 1.0 represents nominal exposure.</returns>
    public double CalculateExposureScalingFactor()
    {
        double effectiveMs = CalculateEffectiveIntegrationTimeMs();
        return effectiveMs / _config.ExposureTimeMs;
    }

    /// <summary>
    /// Applies exposure-proportional scaling to a signal frame.
    /// Scales pixel values based on the effective integration time relative to nominal.
    /// </summary>
    /// <param name="frame">Input signal frame (rows x cols).</param>
    /// <returns>New frame with exposure-scaled signal values.</returns>
    public ushort[,] ApplyExposureScaling(ushort[,] frame)
    {
        if (frame == null)
        {
            throw new ArgumentNullException(nameof(frame));
        }

        int rows = frame.GetLength(0);
        int cols = frame.GetLength(1);
        double scalingFactor = CalculateExposureScalingFactor();

        var result = new ushort[rows, cols];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                double scaled = frame[r, c] * scalingFactor;
                result[r, c] = ClampToUShort(scaled);
            }
        }

        lock (_lock)
        {
            _accumulatedExposureMs += CalculateEffectiveIntegrationTimeMs();
            _accumulatedPulses += _config.GatePulseCount;
        }

        return result;
    }

    /// <summary>
    /// Calculates dark current contribution for the current exposure time.
    /// </summary>
    /// <returns>Dark current signal in electrons per pixel.</returns>
    public double CalculateDarkCurrentElectrons()
    {
        double effectiveSec = CalculateEffectiveIntegrationTimeMs() / 1000.0;
        return _config.DarkCurrentRatePerSec * effectiveSec;
    }

    /// <summary>
    /// Generates a dark current frame based on exposure time.
    /// </summary>
    /// <param name="rows">Number of rows.</param>
    /// <param name="cols">Number of columns.</param>
    /// <param name="fullWellCapacity">Full well capacity in electrons.</param>
    /// <param name="adcBits">ADC bit depth.</param>
    /// <returns>2D frame with uniform dark current signal.</returns>
    public ushort[,] GenerateDarkCurrentFrame(
        int rows,
        int cols,
        double fullWellCapacity = 1_000_000.0,
        int adcBits = 16)
    {
        if (rows <= 0)
        {
            throw new ArgumentException("Rows must be positive.", nameof(rows));
        }

        if (cols <= 0)
        {
            throw new ArgumentException("Cols must be positive.", nameof(cols));
        }

        double darkElectrons = CalculateDarkCurrentElectrons();
        double adcMax = (1 << adcBits) - 1;
        double electronsPerDN = fullWellCapacity / adcMax;
        ushort darkDN = ClampToUShort(darkElectrons / electronsPerDN);

        var frame = new ushort[rows, cols];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                frame[r, c] = darkDN;
            }
        }

        return frame;
    }

    /// <summary>
    /// Resets the accumulated exposure tracking.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _accumulatedExposureMs = 0;
            _accumulatedPulses = 0;
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
