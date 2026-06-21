namespace PanelSimulator.Models;

/// <summary>
/// Configuration for PanelSimulator.
/// REQ-SIM-002: All simulators shall be configurable via detector_config.yaml.
/// </summary>
public class PanelConfig
{
    /// <summary>
    /// Number of rows in the panel.
    /// </summary>
    public int Rows { get; set; }

    /// <summary>
    /// Number of columns in the panel.
    /// </summary>
    public int Cols { get; set; }

    /// <summary>
    /// Bit depth per pixel (14 or 16).
    /// </summary>
    public int BitDepth { get; set; }

    /// <summary>
    /// Test pattern mode.
    /// </summary>
    public TestPattern TestPattern { get; set; }

    /// <summary>
    /// Noise model type.
    /// </summary>
    public NoiseModelType NoiseModel { get; set; }

    /// <summary>
    /// Standard deviation for Gaussian noise model.
    /// </summary>
    public double NoiseStdDev { get; set; }

    /// <summary>
    /// Defect rate (0.0 to 1.0).
    /// </summary>
    public double DefectRate { get; set; }

    /// <summary>
    /// Random seed for deterministic output.
    /// REQ-SIM-003: Deterministic output for same input and configuration.
    /// </summary>
    public int Seed { get; set; }

    /// <summary>
    /// X-ray tube peak voltage in kilovolts (kVp). Used by PhysicsBased pattern.
    /// </summary>
    public double KVp { get; set; } = 80.0;

    /// <summary>
    /// X-ray tube current-time product in milliampere-seconds (mAs). Used by PhysicsBased pattern.
    /// </summary>
    public double MAs { get; set; } = 10.0;

    /// <summary>
    /// Gate integration/exposure time in milliseconds. Used by PhysicsBased pattern.
    /// </summary>
    public double ExposureTimeMs { get; set; } = 100.0;

    // ── ROIC Voltage Parameters (REQ-PHY-004) ──────────────────────────────

    /// <summary>
    /// Gate High voltage in volts. Nominal: 30V. Controls TFT ON state.
    /// Out-of-range → horizontal stripe artifact.
    /// </summary>
    public double VghVolts { get; set; } = 30.0;

    /// <summary>
    /// Gate Low voltage in volts. Nominal: -10V. Controls TFT OFF state.
    /// Insufficient negative voltage → vertical stripe leakage artifact.
    /// </summary>
    public double VglVolts { get; set; } = -10.0;

    /// <summary>
    /// Back-bias (reverse bias) voltage in volts. Nominal: -15V.
    /// Affects dark current exponentially: Idark ∝ |Vback|^2.
    /// </summary>
    public double VbackVolts { get; set; } = -15.0;

    /// <summary>
    /// Detector temperature in Celsius. Affects dark current: Idark(T) = Idark(25°C) × 2^((T-25)/8).
    /// </summary>
    public double TemperatureCelsius { get; set; } = 25.0;

    // ── Composite Noise Parameters (REQ-PHY-001 ~ REQ-PHY-003) ─────────────

    /// <summary>
    /// Electronic readout noise in electrons RMS. Reference: PaxScan 4030CB low-gain = 5948 e⁻.
    /// </summary>
    public double ReadoutNoiseElectrons { get; set; } = 5948.0;

    /// <summary>
    /// Fixed Pattern Noise amplitude as percentage of signal (1-3% typical). REQ-PHY-003.
    /// </summary>
    public double FpnAmplitudePct { get; set; } = 1.5;

    // ── Image Lag Parameters (REQ-PHY-006) ─────────────────────────────────

    /// <summary>
    /// Enable image lag (ghosting) simulation. Default: false.
    /// </summary>
    public bool EnableImageLag { get; set; } = false;

    /// <summary>
    /// Fraction of previous frame signal retained (image lag). Typical: 0.02-0.10.
    /// </summary>
    public double ImageLagFraction { get; set; } = 0.02;
}

/// <summary>
/// Test pattern enumeration.
/// </summary>
public enum TestPattern
{
    /// <summary>Sequential counter pattern.</summary>
    Counter,

    /// <summary>Alternating max/zero checkerboard.</summary>
    Checkerboard,

    /// <summary>Flat field (uniform value).</summary>
    FlatField,

    /// <summary>Physics-based X-ray simulation using kVp/mAs parameters.</summary>
    PhysicsBased
}

/// <summary>
/// Noise model type enumeration.
/// </summary>
public enum NoiseModelType
{
    /// <summary>No noise applied.</summary>
    None,

    /// <summary>Gaussian (normal) distribution noise.</summary>
    Gaussian,

    /// <summary>Poisson distribution noise (optional per REQ-SIM-070).</summary>
    Poisson,

    /// <summary>Uniform distribution noise (optional per REQ-SIM-070).</summary>
    Uniform,

    /// <summary>
    /// Composite noise model: Poisson (shot) + Gaussian (readout) + 1/f (flicker).
    /// REQ-PHY-001: Realistic X-ray detector noise for PhysicsBased pattern.
    /// </summary>
    Composite
}
