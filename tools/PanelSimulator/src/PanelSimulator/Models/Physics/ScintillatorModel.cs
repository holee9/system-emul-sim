using System;

namespace PanelSimulator.Models.Physics;

/// <summary>
/// Configuration for the scintillator physics model.
/// </summary>
/// <param name="KVp">Tube voltage in kilovolts peak (typical: 40-150 kVp).</param>
/// <param name="MAs">Tube current-time product in milliampere-seconds.</param>
/// <param name="LightYieldPerMeV">CsI(Tl) scintillation light yield (photons/MeV). Default ~54,000.</param>
/// <param name="PixelPitchMm">Pixel pitch in millimeters (typical: 0.075-0.2 mm).</param>
/// <param name="ScintillatorThicknessMm">Scintillator layer thickness in mm (typical: 0.3-0.6 mm).</param>
public sealed record ScintillatorConfig(
    double KVp = 80.0,
    double MAs = 10.0,
    double LightYieldPerMeV = 54_000.0,
    double PixelPitchMm = 0.1,
    double ScintillatorThicknessMm = 0.5);

/// <summary>
/// Models CsI(Tl) scintillator X-ray response characteristics.
/// Converts X-ray tube parameters (kVp/mAs) to photon counts and pixel-level signals.
/// </summary>
public sealed class ScintillatorModel
{
    private readonly ScintillatorConfig _config;

    // Physical constants and empirical factors
    private const double PhotonsPerMAsAt1m = 5.0e9;  // Approximate X-ray photons per mAs at 1m (diagnostic range)
    private const double SourceDistanceM = 1.0;       // Source-to-detector distance in meters
    private const double EvPerKeV = 1000.0;
    private const double MeVPerKeV = 0.001;

    /// <summary>
    /// Initializes a new instance of the ScintillatorModel.
    /// </summary>
    /// <param name="config">Scintillator configuration. Uses defaults if null.</param>
    public ScintillatorModel(ScintillatorConfig? config = null)
    {
        _config = config ?? new ScintillatorConfig();

        if (_config.KVp <= 0)
        {
            throw new ArgumentException("KVp must be positive.", nameof(config));
        }

        if (_config.MAs <= 0)
        {
            throw new ArgumentException("mAs must be positive.", nameof(config));
        }

        if (_config.LightYieldPerMeV <= 0)
        {
            throw new ArgumentException("Light yield must be positive.", nameof(config));
        }

        if (_config.PixelPitchMm <= 0)
        {
            throw new ArgumentException("Pixel pitch must be positive.", nameof(config));
        }

        if (_config.ScintillatorThicknessMm <= 0)
        {
            throw new ArgumentException("Scintillator thickness must be positive.", nameof(config));
        }
    }

    /// <summary>
    /// Gets the current configuration.
    /// </summary>
    public ScintillatorConfig Config => _config;

    /// <summary>
    /// Calculates the mean photon energy for a given kVp.
    /// Empirical approximation: mean energy ~ kVp * 0.33 (after filtration).
    /// </summary>
    /// <param name="kVp">Tube voltage in kVp.</param>
    /// <returns>Mean photon energy in keV.</returns>
    public static double GetMeanPhotonEnergyKeV(double kVp)
    {
        if (kVp <= 0)
        {
            throw new ArgumentException("kVp must be positive.", nameof(kVp));
        }

        // Empirical: mean X-ray energy is approximately 1/3 of kVp after standard filtration
        return kVp * 0.33;
    }

    /// <summary>
    /// Calculates energy-dependent quantum efficiency (QE) of the CsI(Tl) scintillator.
    /// Models the exponential attenuation based on photon energy and scintillator thickness.
    /// </summary>
    /// <param name="energyKeV">Photon energy in keV.</param>
    /// <returns>Quantum efficiency as a fraction [0, 1].</returns>
    public double GetQuantumEfficiency(double energyKeV)
    {
        if (energyKeV <= 0)
        {
            throw new ArgumentException("Energy must be positive.", nameof(energyKeV));
        }

        // CsI(Tl) mass attenuation coefficient approximation (simplified model)
        // At ~60 keV, QE is ~70-80% for typical thickness
        // At higher energies, QE decreases; at lower energies (above K-edge), QE is higher
        // Simplified exponential attenuation: QE = 1 - exp(-mu * thickness)
        double linearAttenuationCoeff = GetLinearAttenuationCoefficient(energyKeV);
        double qe = 1.0 - Math.Exp(-linearAttenuationCoeff * _config.ScintillatorThicknessMm);

        return Math.Clamp(qe, 0.0, 1.0);
    }

    /// <summary>
    /// Converts kVp/mAs to incident photon count per pixel.
    /// </summary>
    /// <returns>Estimated incident photon count per pixel.</returns>
    public double GetIncidentPhotonsPerPixel()
    {
        // Total X-ray photons from tube (proportional to kVp^2 * mAs)
        double kVpFactor = (_config.KVp / 80.0) * (_config.KVp / 80.0);
        double totalPhotonsPerSteradian = PhotonsPerMAsAt1m * _config.MAs * kVpFactor;

        // Pixel solid angle = (pixel_pitch^2) / (distance^2)
        double pixelPitchM = _config.PixelPitchMm * 0.001;
        double pixelSolidAngle = (pixelPitchM * pixelPitchM) / (SourceDistanceM * SourceDistanceM);

        return totalPhotonsPerSteradian * pixelSolidAngle;
    }

    /// <summary>
    /// Calculates the mean detected signal per pixel in digital numbers (DN).
    /// Combines incident photon count, quantum efficiency, and scintillation light yield.
    /// </summary>
    /// <param name="gainElectronsPerPhoton">Detector gain in electrons per visible photon (typical: 0.1-1.0).</param>
    /// <param name="adcBits">ADC bit depth (14 or 16).</param>
    /// <param name="fullWellCapacity">Full well capacity in electrons (typical: 500,000-2,000,000).</param>
    /// <returns>Mean signal in ADC digital numbers (DN).</returns>
    public double CalculatePixelSignalDN(
        double gainElectronsPerPhoton = 0.5,
        int adcBits = 16,
        double fullWellCapacity = 1_000_000.0)
    {
        if (gainElectronsPerPhoton <= 0)
        {
            throw new ArgumentException("Gain must be positive.", nameof(gainElectronsPerPhoton));
        }

        if (adcBits < 1 || adcBits > 16)
        {
            throw new ArgumentException("ADC bits must be between 1 and 16.", nameof(adcBits));
        }

        if (fullWellCapacity <= 0)
        {
            throw new ArgumentException("Full well capacity must be positive.", nameof(fullWellCapacity));
        }

        double meanEnergyKeV = GetMeanPhotonEnergyKeV(_config.KVp);
        double qe = GetQuantumEfficiency(meanEnergyKeV);
        double incidentPhotons = GetIncidentPhotonsPerPixel();

        // Absorbed X-ray photons
        double absorbedPhotons = incidentPhotons * qe;

        // Scintillation light photons per absorbed X-ray photon
        double lightPhotonsPerXray = meanEnergyKeV * MeVPerKeV * _config.LightYieldPerMeV;

        // Total visible photons collected (assume ~50% collection efficiency)
        double collectionEfficiency = 0.5;
        double visiblePhotons = absorbedPhotons * lightPhotonsPerXray * collectionEfficiency;

        // Convert to electrons
        double electrons = visiblePhotons * gainElectronsPerPhoton;

        // ADC conversion
        double adcMaxValue = (1 << adcBits) - 1;
        double electronsPerDN = fullWellCapacity / adcMaxValue;
        double signalDN = electrons / electronsPerDN;

        return Math.Clamp(signalDN, 0.0, adcMaxValue);
    }

    /// <summary>
    /// Generates a uniform signal frame based on current X-ray parameters.
    /// Each pixel receives the same mean signal (before noise).
    /// </summary>
    /// <param name="rows">Number of rows.</param>
    /// <param name="cols">Number of columns.</param>
    /// <param name="adcBits">ADC bit depth.</param>
    /// <returns>2D frame with uniform X-ray signal.</returns>
    public ushort[,] GenerateSignalFrame(int rows, int cols, int adcBits = 16)
    {
        if (rows <= 0)
        {
            throw new ArgumentException("Rows must be positive.", nameof(rows));
        }

        if (cols <= 0)
        {
            throw new ArgumentException("Cols must be positive.", nameof(cols));
        }

        double signalDN = CalculatePixelSignalDN(adcBits: adcBits);
        ushort signalValue = (ushort)Math.Clamp(Math.Round(signalDN), 0, 65535);

        var frame = new ushort[rows, cols];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                frame[r, c] = signalValue;
            }
        }

        return frame;
    }

    /// <summary>
    /// Simplified linear attenuation coefficient for CsI(Tl) as a function of photon energy.
    /// Uses power-law approximation: mu ~ A * E^(-n) with K-edge adjustment at 33 keV (Cs) and 36 keV (I).
    /// </summary>
    private static double GetLinearAttenuationCoefficient(double energyKeV)
    {
        // CsI(Tl) density ~ 4.51 g/cm^3
        // Simplified power-law model with K-edge region boost
        double density = 4.51; // g/cm^3

        // Base mass attenuation (cm^2/g) approximation
        double massAtten;
        if (energyKeV < 33.0)
        {
            // Below Cs K-edge: higher attenuation
            massAtten = 50.0 * Math.Pow(energyKeV / 10.0, -2.8);
        }
        else if (energyKeV < 40.0)
        {
            // K-edge region (Cs ~33 keV, I ~33.2 keV): absorption jump
            massAtten = 8.0 * Math.Pow(energyKeV / 33.0, -2.5);
        }
        else
        {
            // Above K-edge: standard power-law decrease
            massAtten = 5.0 * Math.Pow(energyKeV / 40.0, -2.8);
        }

        // Convert to linear attenuation coefficient (1/mm)
        // density in g/cm^3 * massAtten in cm^2/g = 1/cm, then /10 for 1/mm
        return density * massAtten / 10.0;
    }
}
