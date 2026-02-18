using System.IO;
using System.Text.RegularExpressions;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using ParameterExtractor.Core.Models;

namespace ParameterExtractor.Core.Services;

/// <summary>
/// PDF parser implementation using iTextSharp for extracting tabular data.
/// </summary>
public class PdfParser : IPdfParser
{
    private static readonly Regex TablePattern = new(
        @"(\w[\w\s]+)\s*[:=]?\s*([\d.]+)\s*([a-zA-Z]*)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <inheritdoc />
    public bool CanParse(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        if (!File.Exists(filePath))
            return false;

        var ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        return ext == ".pdf";
    }

    /// <inheritdoc />
    public async Task<ExtractionResult> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var result = new ExtractionResult
        {
            SourceFile = filePath
        };

        if (!CanParse(filePath))
        {
            result.IsSuccessful = false;
            result.Messages.Add("File is not a valid PDF or does not exist.");
            return result;
        }

        return await Task.Run(() =>
        {
            try
            {
                using var reader = new PdfReader(filePath);
                var pageCount = reader.NumberOfPages;

                for (int page = 1; page <= pageCount; page++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var text = PdfTextExtractor.GetTextFromPage(reader, page);
                    ExtractParametersFromText(text, result.Parameters);
                }

                result.Messages.Add($"Extracted {result.ExtractedCount} parameters from {pageCount} pages.");
            }
            catch (Exception ex)
            {
                result.IsSuccessful = false;
                result.Messages.Add($"Error parsing PDF: {ex.Message}");
            }

            return result;
        }, cancellationToken);
    }

    private void ExtractParametersFromText(string text, List<ParameterInfo> parameters)
    {
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            var matches = TablePattern.Matches(trimmed);
            foreach (Match match in matches)
            {
                if (match.Groups.Count < 4)
                    continue;

                var name = match.Groups[1].Value.Trim();
                var value = match.Groups[2].Value.Trim();
                var unit = match.Groups[3].Value.Trim();

                // Skip if we already have this parameter
                if (parameters.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var param = new ParameterInfo
                {
                    Name = name,
                    Value = value,
                    Unit = unit,
                    Category = InferCategory(name)
                };

                parameters.Add(param);
            }
        }
    }

    private string InferCategory(string parameterName)
    {
        var nameLower = parameterName.ToLowerInvariant();

        if (nameLower.Contains("pixel") || nameLower.Contains("pitch") ||
            nameLower.Contains("row") || nameLower.Contains("col") ||
            nameLower.Contains("bit") || nameLower.Contains("depth"))
        {
            return "panel";
        }

        if (nameLower.Contains("timing") || nameLower.Contains("gate") ||
            nameLower.Contains("adc") || nameLower.Contains("settle"))
        {
            return "fpga.timing";
        }

        if (nameLower.Contains("csi") || nameLower.Contains("lane") ||
            nameLower.Contains("mbps") || nameLower.Contains("d-phy"))
        {
            return "fpga.data_interface.csi2";
        }

        if (nameLower.Contains("spi") || nameLower.Contains("clock"))
        {
            return "fpga.spi";
        }

        if (nameLower.Contains("ethernet") || nameLower.Contains("port") ||
            nameLower.Contains("mtu") || nameLower.Contains("payload"))
        {
            return "controller.ethernet";
        }

        return "unknown";
    }
}
