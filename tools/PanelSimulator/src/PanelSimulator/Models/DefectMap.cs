using System;

namespace PanelSimulator.Models;

/// <summary>
/// Models pixel defects in the detector panel.
/// REQ-SIM-012: Pixel defect injection (dead pixels and hot pixels).
/// </summary>
public class DefectMap
{
    private readonly double _defectRate;
    private readonly Random _random;

    /// <summary>
    /// Initializes a new instance of the DefectMap.
    /// </summary>
    /// <param name="defectRate">Defect rate (0.0 to 1.0).</param>
    /// <param name="seed">Random seed for deterministic defect pattern.</param>
    public DefectMap(double defectRate, int seed)
    {
        if (defectRate < 0 || defectRate > 1)
        {
            throw new ArgumentException("Defect rate must be between 0 and 1.", nameof(defectRate));
        }

        _defectRate = defectRate;
        _random = new Random(seed);
    }

    /// <summary>
    /// Applies defects to the input pixel array.
    /// Dead pixels are set to 0, hot pixels are set to max value (65535).
    /// </summary>
    /// <param name="pixels">Input pixel array.</param>
    /// <returns>Pixel array with defects applied.</returns>
    public ushort[] ApplyDefects(ushort[] pixels)
    {
        if (pixels == null)
        {
            throw new ArgumentNullException(nameof(pixels));
        }

        if (pixels.Length == 0)
        {
            return Array.Empty<ushort>();
        }

        ushort[] defectivePixels = new ushort[pixels.Length];
        Array.Copy(pixels, defectivePixels, pixels.Length);

        for (int i = 0; i < defectivePixels.Length; i++)
        {
            // Determine if this pixel should be defective
            double roll = _random.NextDouble();

            if (roll < _defectRate)
            {
                // Determine if it's a dead pixel (0) or hot pixel (max)
                // 50/50 chance for each type
                defectivePixels[i] = (_random.NextDouble() < 0.5) ? (ushort)0 : ushort.MaxValue;
            }
        }

        return defectivePixels;
    }
}
