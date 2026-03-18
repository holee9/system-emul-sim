using System.IO;
using System.Text.RegularExpressions;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using ParameterExtractor.Core.Models;

namespace ParameterExtractor.Core.Services;

/// <summary>
/// PDF parser implementation using iTextSharp for extracting tabular data.
/// Only simulation-relevant parameters are extracted; packaging, shipping,
/// ESD handling text, and TOC entries are filtered out.
/// </summary>
public class PdfParser : IPdfParser
{
    // Name group: letters+spaces only (no digits) so "Pixel pitch um 140" → name="Pixel pitch um", value="140"
    // Previously \w in name group consumed digits, causing "Pixel pitch um 14" name / "0" value split.
    private static readonly Regex TablePattern = new(
        @"([a-zA-Z][a-zA-Z\s]+?)\s+([\d.]+)\s*([a-zA-Z]*)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

    // Keywords that indicate a name is simulation-relevant (applied to lower-cased name).
    private static readonly string[] SimulationKeywords =
    [
        "pixel pitch", "pixel count", "pixel size",
        "rows", "cols", "columns", "num row", "num col",
        "bit depth", "adc",
        "gate time", "gate on", "gate off", "gate pulse",
        "csi", "lane", "data rate", "lane speed",
        "line buffer", "line blank", "frame blank",
        "noise floor", "dark current",
        "frame rate", "fps",
        "settle", "roic settle",
        "spi clock", "spi clk",
        "ethernet port", "mtu",
        "buffer depth", "bram",
        // Detector physics parameters (panel spec)
        "scintillator", "light yield",
        "quantum efficiency", "full well", "well capacity",
        "gain",
        // Defect and noise parameters (panel spec)
        "defect rate", "dead pixel",
        "noise std", "read noise"
    ];

    // Words that indicate a parameter is an instruction / warning / ESD text, not a value.
    private static readonly string[] InstructionKeywords =
    [
        "avoid", "stored", "calculated", "damage", "static",
        "bare hand", "beyond", "cause", "handling", "esd",
        "packaging", "shipping", "caution", "warning", "note",
        "do not", "must not", "should not", "please", "refer to"
    ];

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

                var rawName = match.Groups[1].Value.Trim();
                var value = match.Groups[2].Value.Trim();
                var unit = match.Groups[3].Value.Trim();

                // Strip trailing unit suffix from name (e.g. "Pixel pitch um" → name="Pixel pitch", unit="um")
                var (name, inferredUnit) = StripUnitSuffix(rawName);
                if (string.IsNullOrEmpty(unit))
                    unit = inferredUnit;

                var param = new ParameterInfo
                {
                    Name = name,
                    Value = value,
                    Unit = unit,
                    Category = InferCategory(name)
                };

                // Apply simulation relevance filter before adding
                if (!IsSimulationRelevant(param))
                    continue;

                // Skip if we already have this parameter (dedup after filter to avoid keeping irrelevant first-seen)
                if (parameters.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                parameters.Add(param);
            }
        }
    }

    /// <summary>
    /// Returns true only when the parameter is simulation-relevant.
    /// Filters out: packaging/shipping text, ESD warnings, TOC entries,
    /// instruction sentences, and any "unknown" category params lacking
    /// a recognised simulation keyword.
    /// </summary>
    public static bool IsSimulationRelevant(ParameterInfo param)
    {
        // Must have a positive numeric value — page numbers, zero-values and
        // non-parseable strings are all rejected.
        if (!param.NumericValue.HasValue || param.NumericValue.Value <= 0)
            return false;

        var nameLower = param.Name.ToLowerInvariant();

        // Reject TOC entries: names containing "......" are page-reference lines.
        if (nameLower.Contains("......"))
            return false;

        // Reject instruction / warning sentences.
        foreach (var keyword in InstructionKeywords)
        {
            if (nameLower.Contains(keyword))
                return false;
        }

        // Accept any known non-unknown category immediately.
        if (param.Category != "unknown")
            return true;

        // For unknown category, only accept if the name contains a recognised simulation keyword.
        foreach (var keyword in SimulationKeywords)
        {
            if (nameLower.Contains(keyword))
                return true;
        }

        return false;
    }

    private static readonly string[] KnownUnits = ["um", "mm", "MHz", "Gbps", "Mbps", "ns", "us", "ms", "sec", "V", "mV", "mA", "nF", "pF", "bit", "bits", "dB"];

    /// <summary>
    /// Strips trailing unit token from parameter name.
    /// E.g. "Pixel pitch um" → ("Pixel pitch", "um")
    /// </summary>
    private static (string name, string unit) StripUnitSuffix(string rawName)
    {
        foreach (var unit in KnownUnits)
        {
            if (rawName.EndsWith(" " + unit, StringComparison.OrdinalIgnoreCase))
                return (rawName[..^(unit.Length + 1)].TrimEnd(), unit);
        }
        return (rawName, string.Empty);
    }

    private string InferCategory(string parameterName)
    {
        var nameLower = parameterName.ToLowerInvariant();

        if (nameLower.Contains("pixel") || nameLower.Contains("pitch") ||
            nameLower.Contains("row") || nameLower.Contains("col") ||
            nameLower.Contains("bit") || nameLower.Contains("depth") ||
            nameLower.Contains("dark current") || nameLower.Contains("noise") ||
            nameLower.Contains("scintillator") || nameLower.Contains("light yield") ||
            nameLower.Contains("quantum efficiency") || nameLower.Contains("full well") ||
            nameLower.Contains("well capacity") || nameLower.Contains("defect rate") ||
            nameLower.Contains("dead pixel") || nameLower.Contains("gain") ||
            nameLower.Contains("read noise"))
        {
            return "panel";
        }

        if (nameLower.Contains("timing") || nameLower.Contains("gate") ||
            nameLower.Contains("adc") || nameLower.Contains("settle") ||
            nameLower.Contains("line buffer") || nameLower.Contains("bram"))
        {
            return "fpga.timing";
        }

        if (nameLower.Contains("csi") || nameLower.Contains("lane") ||
            nameLower.Contains("mbps") || nameLower.Contains("d-phy") ||
            nameLower.Contains("data rate") || nameLower.Contains("line blank") ||
            nameLower.Contains("frame blank"))
        {
            return "fpga.data_interface.csi2";
        }

        if (nameLower.Contains("spi") || nameLower.Contains("spi clock") ||
            nameLower.Contains("spi clk"))
        {
            return "fpga.spi";
        }

        if (nameLower.Contains("ethernet") || nameLower.Contains("port") ||
            nameLower.Contains("mtu") || nameLower.Contains("payload") ||
            nameLower.Contains("fps") || nameLower.Contains("frame rate"))
        {
            return "controller.ethernet";
        }

        return "unknown";
    }
}
