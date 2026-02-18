namespace ParameterExtractor.Core.Models;

/// <summary>
/// Represents a single parameter extracted from a datasheet.
/// </summary>
public class ParameterInfo
{
    /// <summary>
    /// Gets or sets the parameter name (e.g., "Pixel Pitch", "ADC Bit Depth").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the parameter value as a string for flexible parsing.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unit of measurement (e.g., "um", "bits", "MHz").
    /// </summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the minimum allowed value (optional).
    /// </summary>
    public double? Min { get; set; }

    /// <summary>
    /// Gets or sets the maximum allowed value (optional).
    /// </summary>
    public double? Max { get; set; }

    /// <summary>
    /// Gets or sets the category/group this parameter belongs to (e.g., "panel", "fpga.timing").
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the validation status after rule evaluation.
    /// </summary>
    public ValidationStatus ValidationStatus { get; set; } = ValidationStatus.Pending;

    /// <summary>
    /// Gets or sets the validation message describing any errors or warnings.
    /// </summary>
    public string ValidationMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets the parsed numeric value if applicable.
    /// </summary>
    public double? NumericValue => double.TryParse(Value, out var val) ? val : null;
}

/// <summary>
/// Validation status for extracted parameters.
/// </summary>
public enum ValidationStatus
{
    /// <summary>
    /// Validation has not been performed yet.
    /// </summary>
    Pending,

    /// <summary>
    /// Parameter passed all validation rules.
    /// </summary>
    Valid,

    /// <summary>
    /// Parameter failed validation with a warning.
    /// </summary>
    Warning,

    /// <summary>
    /// Parameter failed validation with an error.
    /// </summary>
    Error
}
