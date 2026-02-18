using ParameterExtractor.Core.Models;

namespace ParameterExtractor.Core.Services;

/// <summary>
/// Service for parsing PDF datasheet files and extracting parameter data.
/// </summary>
public interface IPdfParser
{
    /// <summary>
    /// Parses a PDF file and extracts tabular parameter data.
    /// </summary>
    /// <param name="filePath">Path to the PDF file.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Extraction result containing extracted parameters.</returns>
    Task<ExtractionResult> ParseAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines if the file is a valid PDF that can be parsed.
    /// </summary>
    /// <param name="filePath">Path to the file to validate.</param>
    /// <returns>True if the file is a valid PDF.</returns>
    bool CanParse(string filePath);
}
