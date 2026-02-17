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
    FlatField
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
    Uniform
}
