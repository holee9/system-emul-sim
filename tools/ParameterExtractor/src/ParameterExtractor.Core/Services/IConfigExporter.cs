using ParameterExtractor.Core.Models;

namespace ParameterExtractor.Core.Services;

/// <summary>
/// Service for exporting parameters to detector_config.yaml format.
/// </summary>
public interface IConfigExporter
{
    /// <summary>
    /// Exports the given parameters to a YAML file conforming to detector_config schema.
    /// </summary>
    /// <param name="parameters">The parameters to export.</param>
    /// <param name="outputPath">Path where the YAML file will be written.</param>
    /// <param name="schemaPath">Path to the JSON schema for validation.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Result indicating success and any validation errors.</returns>
    Task<ExportResult> ExportAsync(
        IEnumerable<ParameterInfo> parameters,
        string outputPath,
        string? schemaPath = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the configuration against the JSON schema.
    /// </summary>
    /// <param name="config">Configuration to validate.</param>
    /// <param name="schemaPath">Path to the JSON schema.</param>
    /// <returns>Validation result with any errors.</returns>
    Task<SchemaValidationResult> ValidateSchemaAsync(DetectorConfig config, string schemaPath);
}

/// <summary>
/// Result of a configuration export operation.
/// </summary>
public class ExportResult
{
    /// <summary>
    /// Gets or sets a value indicating whether export was successful.
    /// </summary>
    public bool IsSuccessful { get; set; }

    /// <summary>
    /// Gets or sets the output file path.
    /// </summary>
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets any validation errors that occurred.
    /// </summary>
    public List<SchemaValidationError> ValidationErrors { get; set; } = new();

    /// <summary>
    /// Gets or sets general error messages.
    /// </summary>
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Result of JSON schema validation.
/// </summary>
public class SchemaValidationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether validation passed.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets validation errors with field paths.
    /// </summary>
    public List<SchemaValidationError> Errors { get; set; } = new();
}

/// <summary>
/// Schema validation error with field path.
/// </summary>
public class SchemaValidationError
{
    /// <summary>
    /// Gets or sets the JSON path to the field with the error.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error kind (required, type, minimum, etc.).
    /// </summary>
    public string Kind { get; set; } = string.Empty;
}
