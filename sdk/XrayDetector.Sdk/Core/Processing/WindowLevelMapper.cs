namespace XrayDetector.Core.Processing;

/// <summary>
/// Maps 16-bit grayscale pixel data to 8-bit for display.
/// Uses window/level technique commonly used in medical imaging.
/// </summary>
public sealed class WindowLevelMapper
{
    private const ushort MaxValue = 65535;
    private double _window;
    private double _level;

    /// <summary>
    /// Creates a new mapper with default window/level.
    /// Default: Window = 65535 (full range), Level = 32768 (midpoint).
    /// </summary>
    public WindowLevelMapper()
        : this(window: MaxValue, level: MaxValue / 2.0)
    {
    }

    /// <summary>
    /// Creates a new mapper with specific window/level.
    /// </summary>
    /// <param name="window">Window width (range of values to display).</param>
    /// <param name="level">Level (center of window).</param>
    public WindowLevelMapper(double window, double level)
    {
        if (window <= 0)
            throw new ArgumentException("Window must be positive", nameof(window));

        _window = window;
        _level = level;
    }

    /// <summary>Current window width.</summary>
    public double Window => _window;

    /// <summary>Current level (center).</summary>
    public double Level => _level;

    /// <summary>
    /// Maps 16-bit grayscale data to 8-bit display values.
    /// </summary>
    /// <param name="input16">Input 16-bit grayscale pixel data.</param>
    /// <returns>8-bit pixel data suitable for display.</returns>
    public byte[] Map(ushort[] input16)
    {
        if (input16 == null)
            throw new ArgumentNullException(nameof(input16));

        byte[] output8 = new byte[input16.Length];

        if (input16.Length == 0)
        {
            return output8;
        }

        double windowHalf = _window / 2.0;
        double windowMin = _level - windowHalf;
        double windowMax = _level + windowHalf;

        for (int i = 0; i < input16.Length; i++)
        {
            double value = input16[i];

            // Clamp to window range
            if (value <= windowMin)
            {
                output8[i] = 0;
            }
            else if (value >= windowMax)
            {
                output8[i] = 255;
            }
            else
            {
                // Map to 0-255 range
                // At level (center), value should map to 128
                double normalized = (value - windowMin) / _window;
                output8[i] = (byte)Math.Round(normalized * 255.0);
            }
        }

        return output8;
    }

    /// <summary>
    /// Updates the window and level values.
    /// </summary>
    /// <param name="window">New window width.</param>
    /// <param name="level">New level (center).</param>
    public void UpdateWindowLevel(double window, double level)
    {
        if (window <= 0)
            throw new ArgumentException("Window must be positive", nameof(window));

        _window = window;
        _level = level;
    }

    /// <summary>
    /// Auto-calculates optimal window/level from data range.
    /// </summary>
    /// <param name="data">Input pixel data.</param>
    /// <param name="percentRange">Percent of data range to use (default 95%).</param>
    public void AutoWindowLevel(ushort[] data, double percentRange = 0.95)
    {
        if (data == null || data.Length == 0)
            return;

        // Find min/max
        ushort min = data[0];
        ushort max = data[0];
        foreach (ushort pixel in data)
        {
            if (pixel < min) min = pixel;
            if (pixel > max) max = pixel;
        }

        double range = (max - min) * percentRange;
        double center = (max + min) / 2.0;

        UpdateWindowLevel(window: range, level: center);
    }
}
