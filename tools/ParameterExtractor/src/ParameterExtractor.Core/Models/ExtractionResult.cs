namespace ParameterExtractor.Core.Models;

/// <summary>
/// Result of a PDF parameter extraction operation.
/// </summary>
public class ExtractionResult
{
    /// <summary>
    /// Gets or sets the list of extracted parameters.
    /// </summary>
    public List<ParameterInfo> Parameters { get; set; } = new();

    /// <summary>
    /// Gets or sets the source file path.
    /// </summary>
    public string SourceFile { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the extraction timestamp.
    /// </summary>
    public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets any warnings or errors that occurred during extraction.
    /// </summary>
    public List<string> Messages { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether extraction was successful.
    /// </summary>
    public bool IsSuccessful { get; set; } = true;

    /// <summary>
    /// Gets the number of parameters successfully extracted.
    /// </summary>
    public int ExtractedCount => Parameters.Count;
}
