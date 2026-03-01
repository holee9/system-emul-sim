using System;

namespace PanelSimulator.Models.Readout;

/// <summary>
/// Configuration for the ROIC readout model.
/// </summary>
/// <param name="SettleTimeUs">ROIC settle time per row in microseconds (typical: 2-10 us).</param>
/// <param name="AdcConversionTimeUs">ADC conversion time per row in microseconds (typical: 1-5 us).</param>
/// <param name="AdcBits">ADC bit depth (typical: 14 or 16).</param>
public sealed record RoicReadoutConfig(
    double SettleTimeUs = 5.0,
    double AdcConversionTimeUs = 2.0,
    int AdcBits = 16);

/// <summary>
/// Row-level readout data produced by the ROIC during frame scanning.
/// </summary>
/// <param name="Pixels">Pixel data for this row.</param>
/// <param name="RowIndex">Row index in the frame.</param>
/// <param name="SettleTimeUs">Actual settle time applied for this row.</param>
/// <param name="ReadoutTimeUs">Total readout time for this row (settle + ADC conversion).</param>
public sealed record LineData(
    ushort[] Pixels,
    int RowIndex,
    double SettleTimeUs,
    double ReadoutTimeUs);

/// <summary>
/// Models ROIC (Read-Out Integrated Circuit) row-by-row readout behavior.
/// Simulates sequential row scanning with settle time and ADC conversion per row,
/// producing per-row timing metadata alongside pixel data.
/// </summary>
public sealed class RoicReadoutModel
{
    private readonly RoicReadoutConfig _config;
    private int _lastFrameRows;

    /// <summary>
    /// Initializes a new instance of the RoicReadoutModel.
    /// </summary>
    /// <param name="config">Readout configuration. Uses defaults if null.</param>
    public RoicReadoutModel(RoicReadoutConfig? config = null)
    {
        _config = config ?? new RoicReadoutConfig();

        if (_config.SettleTimeUs < 0)
        {
            throw new ArgumentException(
                "Settle time must be non-negative.", nameof(config));
        }

        if (_config.AdcConversionTimeUs < 0)
        {
            throw new ArgumentException(
                "ADC conversion time must be non-negative.", nameof(config));
        }

        if (_config.AdcBits < 1 || _config.AdcBits > 16)
        {
            throw new ArgumentException(
                "ADC bits must be between 1 and 16.", nameof(config));
        }
    }

    /// <summary>
    /// Gets the current configuration.
    /// </summary>
    public RoicReadoutConfig Config => _config;

    /// <summary>
    /// Reads a 2D frame row by row, producing per-row LineData with timing information.
    /// Pixel values are preserved exactly (no modification).
    /// </summary>
    /// <param name="frame">Input frame as a 2D ushort array [rows, cols].</param>
    /// <returns>Array of LineData, one per row, with timing metadata.</returns>
    public LineData[] ReadFrame(ushort[,] frame)
    {
        if (frame == null)
        {
            throw new ArgumentNullException(nameof(frame));
        }

        int rows = frame.GetLength(0);
        int cols = frame.GetLength(1);
        _lastFrameRows = rows;

        var result = new LineData[rows];
        double rowReadoutTime = _config.SettleTimeUs + _config.AdcConversionTimeUs;

        for (int r = 0; r < rows; r++)
        {
            var pixels = new ushort[cols];
            for (int c = 0; c < cols; c++)
            {
                pixels[c] = frame[r, c];
            }

            result[r] = new LineData(
                Pixels: pixels,
                RowIndex: r,
                SettleTimeUs: _config.SettleTimeUs,
                ReadoutTimeUs: rowReadoutTime);
        }

        return result;
    }

    /// <summary>
    /// Calculates the total readout time for a full frame in milliseconds.
    /// Total = rows * (settleTime + adcConversionTime) / 1000.
    /// Uses the row count from the last ReadFrame call, or the provided row count.
    /// </summary>
    /// <param name="rows">Number of rows. If null, uses the last frame's row count.</param>
    /// <returns>Total readout time in milliseconds.</returns>
    public double CalculateTotalReadoutTimeMs(int? rows = null)
    {
        int rowCount = rows ?? _lastFrameRows;

        if (rowCount <= 0)
        {
            throw new ArgumentException(
                "Row count must be positive. Call ReadFrame first or provide a row count.",
                nameof(rows));
        }

        double rowTimeUs = _config.SettleTimeUs + _config.AdcConversionTimeUs;
        return rowCount * rowTimeUs / 1000.0;
    }
}
