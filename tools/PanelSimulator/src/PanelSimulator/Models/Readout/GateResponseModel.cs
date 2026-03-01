using System;

namespace PanelSimulator.Models.Readout;

/// <summary>
/// Configuration for the gate response model.
/// </summary>
/// <param name="MaxExposureTimeMs">Maximum allowed exposure time in milliseconds.</param>
/// <param name="DarkCurrentPerMs">Dark current contribution in DN per millisecond of exposure.</param>
/// <param name="SaturationLevel">ADC saturation level (max DN value).</param>
public sealed record GateResponseConfig(
    double MaxExposureTimeMs = 200.0,
    double DarkCurrentPerMs = 0.5,
    ushort SaturationLevel = 65535);

/// <summary>
/// Models the gate (TFT switch) response of a flat-panel detector.
/// Controls signal accumulation based on gate on/off state and exposure parameters.
/// When the gate is off, only dark current contributes to the signal.
/// When the gate is on, signal is proportional to exposure time, kVp^2, and mAs.
/// </summary>
public sealed class GateResponseModel
{
    private readonly GateResponseConfig _config;

    // Empirical scaling factor to map (exposureMs * kVp^2 * mAs) to a realistic DN range.
    // Tuned so that typical diagnostic parameters (80 kVp, 10 mAs, 100 ms) produce ~30-50% of saturation.
    private const double SignalScaleFactor = 0.005;

    /// <summary>
    /// Initializes a new instance of the GateResponseModel.
    /// </summary>
    /// <param name="config">Gate response configuration. Uses defaults if null.</param>
    public GateResponseModel(GateResponseConfig? config = null)
    {
        _config = config ?? new GateResponseConfig();

        if (_config.MaxExposureTimeMs <= 0)
        {
            throw new ArgumentException(
                "Max exposure time must be positive.", nameof(config));
        }

        if (_config.DarkCurrentPerMs < 0)
        {
            throw new ArgumentException(
                "Dark current must be non-negative.", nameof(config));
        }

        if (_config.SaturationLevel == 0)
        {
            throw new ArgumentException(
                "Saturation level must be greater than zero.", nameof(config));
        }
    }

    /// <summary>
    /// Gets the current configuration.
    /// </summary>
    public GateResponseConfig Config => _config;

    /// <summary>
    /// Calculates the signal level in DN for given X-ray and gate parameters.
    /// When gateOn is false, returns dark current contribution only.
    /// When gateOn is true, signal is proportional to exposureTime * kVp^2 * mAs,
    /// plus dark current, clamped to [0, saturationLevel].
    /// </summary>
    /// <param name="gateOn">Whether the gate (TFT switch) is open for signal integration.</param>
    /// <param name="exposureTimeMs">Exposure time in milliseconds.</param>
    /// <param name="kvp">Tube voltage in kilovolts peak.</param>
    /// <param name="mAs">Tube current-time product in milliampere-seconds.</param>
    /// <returns>Signal level in digital numbers (DN), clamped to [0, saturationLevel].</returns>
    public ushort CalculateSignalLevel(bool gateOn, double exposureTimeMs, double kvp, double mAs)
    {
        if (exposureTimeMs < 0)
        {
            throw new ArgumentException(
                "Exposure time must be non-negative.", nameof(exposureTimeMs));
        }

        // Dark current is always present, proportional to exposure time
        double darkCurrent = _config.DarkCurrentPerMs * exposureTimeMs;

        if (!gateOn)
        {
            return ClampToSaturation(darkCurrent);
        }

        // Signal proportional to exposure * kVp^2 * mAs
        double signal = SignalScaleFactor * exposureTimeMs * kvp * kvp * mAs;
        double totalSignal = signal + darkCurrent;

        return ClampToSaturation(totalSignal);
    }

    /// <summary>
    /// Applies gate response scaling to an existing frame.
    /// Scales pixel values based on the ratio of the given exposure time to max exposure time.
    /// When gate is off, pixels are replaced with dark current level.
    /// When gate is on, pixels are scaled by (exposureTimeMs / maxExposureTimeMs) and
    /// dark current is added.
    /// </summary>
    /// <param name="frame">Input frame to apply gate response to.</param>
    /// <param name="gateOn">Whether the gate is open.</param>
    /// <param name="exposureTimeMs">Exposure time in milliseconds.</param>
    /// <returns>New frame with gate response applied.</returns>
    public ushort[,] ApplyGateResponse(ushort[,] frame, bool gateOn, double exposureTimeMs)
    {
        if (frame == null)
        {
            throw new ArgumentNullException(nameof(frame));
        }

        if (exposureTimeMs < 0)
        {
            throw new ArgumentException(
                "Exposure time must be non-negative.", nameof(exposureTimeMs));
        }

        int rows = frame.GetLength(0);
        int cols = frame.GetLength(1);
        var result = new ushort[rows, cols];

        double darkCurrent = _config.DarkCurrentPerMs * exposureTimeMs;

        if (!gateOn)
        {
            // Gate off: only dark current, no X-ray signal
            ushort darkValue = ClampToSaturation(darkCurrent);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    result[r, c] = darkValue;
                }
            }

            return result;
        }

        // Gate on: scale existing signal by exposure ratio and add dark current
        double exposureRatio = exposureTimeMs / _config.MaxExposureTimeMs;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                double scaledPixel = frame[r, c] * exposureRatio + darkCurrent;
                result[r, c] = ClampToSaturation(scaledPixel);
            }
        }

        return result;
    }

    /// <summary>
    /// Clamps a double value to [0, saturationLevel] and converts to ushort.
    /// </summary>
    private ushort ClampToSaturation(double value)
    {
        if (value < 0) return 0;
        if (value > _config.SaturationLevel) return _config.SaturationLevel;
        return (ushort)Math.Round(value);
    }
}
