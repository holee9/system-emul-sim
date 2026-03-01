using System;

namespace PanelSimulator.Models.Temporal;

/// <summary>
/// Drift pattern type for temperature-dependent offset changes.
/// </summary>
public enum DriftPattern
{
    /// <summary>Linear drift: offset increases linearly with time.</summary>
    Linear,

    /// <summary>Sinusoidal drift: offset oscillates with time (models thermal cycling).</summary>
    Sinusoidal
}

/// <summary>
/// Configuration for the temperature drift model.
/// </summary>
/// <param name="DriftRateDNPerHour">Offset drift rate in DN per hour (typical: 0.5-5.0 DN/hr).</param>
/// <param name="Pattern">Drift pattern (linear or sinusoidal).</param>
/// <param name="SinusoidalPeriodHours">Period of sinusoidal drift in hours (if Pattern is Sinusoidal).</param>
/// <param name="FrameIntervalMs">Time interval between frames in milliseconds.</param>
public sealed record DriftConfig(
    double DriftRateDNPerHour = 2.0,
    DriftPattern Pattern = DriftPattern.Linear,
    double SinusoidalPeriodHours = 1.0,
    double FrameIntervalMs = 100.0);

/// <summary>
/// Simulates temperature-dependent offset drift over time.
/// Models the slow offset shift in detector electronics caused by temperature changes.
/// Supports linear and sinusoidal drift patterns.
/// </summary>
public sealed class DriftModel
{
    private readonly DriftConfig _config;
    private long _frameCount;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the DriftModel.
    /// </summary>
    /// <param name="config">Drift configuration. Uses defaults if null.</param>
    public DriftModel(DriftConfig? config = null)
    {
        _config = config ?? new DriftConfig();

        if (_config.DriftRateDNPerHour < 0)
        {
            throw new ArgumentException(
                "Drift rate must be non-negative.", nameof(config));
        }

        if (_config.SinusoidalPeriodHours <= 0)
        {
            throw new ArgumentException(
                "Sinusoidal period must be positive.", nameof(config));
        }

        if (_config.FrameIntervalMs <= 0)
        {
            throw new ArgumentException(
                "Frame interval must be positive.", nameof(config));
        }

        _frameCount = 0;
    }

    /// <summary>
    /// Gets the current configuration.
    /// </summary>
    public DriftConfig Config => _config;

    /// <summary>
    /// Gets the current frame count.
    /// </summary>
    public long FrameCount
    {
        get
        {
            lock (_lock)
            {
                return _frameCount;
            }
        }
    }

    /// <summary>
    /// Gets the elapsed time in hours since the last reset.
    /// </summary>
    public double ElapsedHours
    {
        get
        {
            lock (_lock)
            {
                return _frameCount * _config.FrameIntervalMs / 3_600_000.0;
            }
        }
    }

    /// <summary>
    /// Calculates the current drift offset in DN based on elapsed time and pattern.
    /// </summary>
    /// <returns>Current drift offset in DN (can be positive or negative for sinusoidal).</returns>
    public double CalculateCurrentDriftDN()
    {
        lock (_lock)
        {
            return CalculateDriftAtTime(ElapsedHours);
        }
    }

    /// <summary>
    /// Calculates drift offset at a specific elapsed time.
    /// </summary>
    /// <param name="elapsedHours">Elapsed time in hours.</param>
    /// <returns>Drift offset in DN.</returns>
    public double CalculateDriftAtTime(double elapsedHours)
    {
        if (_config.DriftRateDNPerHour <= 0)
        {
            return 0.0;
        }

        return _config.Pattern switch
        {
            DriftPattern.Linear => _config.DriftRateDNPerHour * elapsedHours,
            DriftPattern.Sinusoidal => CalculateSinusoidalDrift(elapsedHours),
            _ => 0.0
        };
    }

    /// <summary>
    /// Applies drift offset to a frame and advances the internal frame counter.
    /// </summary>
    /// <param name="frame">Input frame.</param>
    /// <returns>New frame with drift offset applied.</returns>
    public ushort[,] ApplyDrift(ushort[,] frame)
    {
        if (frame == null)
        {
            throw new ArgumentNullException(nameof(frame));
        }

        int rows = frame.GetLength(0);
        int cols = frame.GetLength(1);

        double driftDN;
        lock (_lock)
        {
            driftDN = CalculateDriftAtTime(ElapsedHours);
            _frameCount++;
        }

        if (Math.Abs(driftDN) < 0.001)
        {
            // No significant drift, return a copy
            var copy = new ushort[rows, cols];
            Array.Copy(frame, copy, frame.Length);
            return copy;
        }

        var result = new ushort[rows, cols];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                double value = frame[r, c] + driftDN;
                result[r, c] = ClampToUShort(value);
            }
        }

        return result;
    }

    /// <summary>
    /// Resets the drift model, clearing elapsed time.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _frameCount = 0;
        }
    }

    /// <summary>
    /// Calculates sinusoidal drift: amplitude * sin(2 * pi * t / period).
    /// The amplitude equals the drift rate (peak-to-peak = 2 * driftRate).
    /// </summary>
    private double CalculateSinusoidalDrift(double elapsedHours)
    {
        double angularFrequency = 2.0 * Math.PI / _config.SinusoidalPeriodHours;
        return _config.DriftRateDNPerHour * Math.Sin(angularFrequency * elapsedHours);
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
