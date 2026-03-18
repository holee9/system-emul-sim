// @MX:NOTE: PDF 데이터시트에서 파라미터 추출을 위한 서비스 클래스
// ParameterExtractor.Core를 래핑하여 GUI 통합을 제공합니다
using System.Diagnostics;
using System.IO;
using IntegrationRunner.Core.Models;
using ParameterExtractor.Core.Services;
using ParamExtractorModels = ParameterExtractor.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace XrayDetector.Gui.Services;

/// <summary>
/// Service for extracting detector parameters from PDF datasheets.
/// Wraps ParameterExtractor.Core for GUI integration.
/// </summary>
public sealed class ParameterExtractorService
{
    private readonly IPdfParser _pdfParser;
    private readonly IRuleValidator _ruleValidator;
    private readonly IDeserializer _yamlDeserializer;

    /// <summary>
    /// Creates a new ParameterExtractorService.
    /// </summary>
    public ParameterExtractorService()
    {
        _pdfParser = new PdfParser();
        _ruleValidator = new RuleValidator();
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    /// Parses a PDF datasheet and extracts parameters.
    /// </summary>
    /// <param name="filePath">Path to the PDF file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Extraction result with parameters and status.</returns>
    // @MX:ANCHOR: PDF 파싱 핵심 진입점 - ParameterExtractorViewModel에서 호출
    public async Task<ParamExtractorModels.ExtractionResult> ParsePdfAsync(string filePath, CancellationToken cancellationToken = default)
    {
        // @MX:WARN: Path validation to prevent directory traversal attacks - DO NOT REMOVE
        // CWE-22: Improper Limitation of a Pathname to a Restricted Directory
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty", nameof(filePath));

        var normalizedPath = Path.GetFullPath(filePath);

        // Validate file extension
        if (!normalizedPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only PDF files are allowed", nameof(filePath));

        // Check for path traversal patterns
        if (filePath.Contains("..") || filePath.Contains('~'))
            throw new ArgumentException("Invalid path: directory traversal not allowed", nameof(filePath));

        try
        {
            var result = await _pdfParser.ParseAsync(normalizedPath, cancellationToken);

            if (result.IsSuccessful && result.Parameters.Any())
            {
                // Auto-validate extracted parameters
                foreach (var param in result.Parameters)
                {
                    _ruleValidator.Validate(param);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PDF parsing error: {ex}");
            return new ParamExtractorModels.ExtractionResult
            {
                IsSuccessful = false,
                Parameters = [],
                Messages = [$"Failed to parse PDF: {ex.Message}"]
            };
        }
    }

    /// <summary>
    /// Converts extracted parameters to DetectorConfig.
    /// </summary>
    /// <param name="parameters">Extracted parameter list.</param>
    /// <returns>DetectorConfig with populated values.</returns>
    // @MX:ANCHOR: 추출된 파라미터를 DetectorConfig로 변환 - ParameterExtractorViewModel에서 호출
    // Uses keyword-based semantic matching because extracted parameter names come from raw
    // PDF text (e.g. "gate", "Pixel pitch") and never match exact dictionary keys.
    public DetectorConfig ToDetectorConfig(IEnumerable<ParamExtractorModels.ParameterInfo> parameters)
    {
        var validParams = parameters
            .Where(p => p.ValidationStatus == ParamExtractorModels.ValidationStatus.Valid ||
                        p.ValidationStatus == ParamExtractorModels.ValidationStatus.Warning)
            .ToList();

        var config = new DetectorConfig
        {
            Panel = new PanelConfig()
        };

        foreach (var param in validParams)
        {
            if (!param.NumericValue.HasValue) continue;

            var nameLower = param.Name.ToLowerInvariant();
            var val = param.NumericValue.Value;

            // Rows: bare "gate" = gate line count; or explicit row keywords.
            // Exclude gate timing params (on/off/time/pulse) to avoid false mapping.
            bool isGateTiming = nameLower.Contains("time") || nameLower.Contains("timing") ||
                                  nameLower.Contains(" on") || nameLower.Contains(" off") ||
                                  nameLower.Contains("pulse");
            bool isGateLineCount = (nameLower == "gate" || nameLower.Contains("gate line") ||
                                    nameLower.Contains("gate count") || nameLower.Contains("number of gate"))
                                   && !isGateTiming;
            if ((isGateLineCount || nameLower.Contains("row") || nameLower.Contains("num row")) &&
                (int)val > 0)
            {
                config.Panel!.Rows = (int)val;
                continue;
            }

            // Cols: source line count or explicit column keywords.
            if ((nameLower == "source" || nameLower.Contains("source line") ||
                 nameLower.Contains("source count") || nameLower.Contains("number of source") ||
                 nameLower == "data" || nameLower.Contains("data line") ||
                 nameLower.Contains("num col") || nameLower.Contains("col")) &&
                (int)val > 0)
            {
                config.Panel!.Cols = (int)val;
                continue;
            }

            // Pixel pitch — convert mm → um when needed.
            if (nameLower.Contains("pixel") && nameLower.Contains("pitch"))
            {
                var pitchUm = string.Equals(param.Unit, "mm", StringComparison.OrdinalIgnoreCase)
                    ? val * 1000.0
                    : val;
                config.Panel!.PixelPitchUm = pitchUm;
                continue;
            }

            // Bit depth
            if ((nameLower.Contains("bit") && nameLower.Contains("depth")) ||
                (nameLower.Contains("adc") && nameLower.Contains("bit")) ||
                nameLower.Contains("bit depth"))
            {
                if ((int)val > 0) config.Panel!.BitDepth = (int)val;
                continue;
            }

            // FPGA: CSI-2 lanes
            if (nameLower.Contains("lane") && !nameLower.Contains("blank") &&
                nameLower.Contains("csi") && (int)val > 0)
            {
                config.Fpga ??= new FpgaConfig();
                config.Fpga.Csi2Lanes = (int)val;
                continue;
            }

            // FPGA: CSI-2 data rate (Mbps)
            if ((nameLower.Contains("data rate") || nameLower.Contains("mbps")) &&
                (nameLower.Contains("csi") || nameLower.Contains("lane")))
            {
                config.Fpga ??= new FpgaConfig();
                config.Fpga.Csi2DataRateMbps = (int)val;
                continue;
            }
        }

        return config;
    }

    /// <summary>
    /// Loads DetectorConfig from a YAML file.
    /// </summary>
    /// <param name="filePath">Path to the YAML file.</param>
    /// <returns>Loaded DetectorConfig or null if failed.</returns>
    public DetectorConfig? LoadFromYaml(string filePath)
    {
        // @MX:WARN: Path validation to prevent directory traversal attacks - DO NOT REMOVE
        if (string.IsNullOrWhiteSpace(filePath))
            return null;

        var normalizedPath = Path.GetFullPath(filePath);

        // Validate file extension
        if (!normalizedPath.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) &&
            !normalizedPath.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
            return null;

        // Check for path traversal patterns
        if (filePath.Contains("..") || filePath.Contains('~'))
            return null;

        try
        {
            var yaml = File.ReadAllText(normalizedPath);
            return _yamlDeserializer.Deserialize<DetectorConfig>(yaml);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"YAML loading error: {ex}");
            return null;
        }
    }

    /// <summary>
    /// Saves DetectorConfig to a YAML file.
    /// </summary>
    /// <param name="config">Configuration to save.</param>
    /// <param name="filePath">Target file path.</param>
    /// <returns>True if successful.</returns>
    public bool SaveToYaml(DetectorConfig config, string filePath)
    {
        // @MX:WARN: Path validation to prevent directory traversal attacks - DO NOT REMOVE
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        var normalizedPath = Path.GetFullPath(filePath);

        // Validate file extension
        if (!normalizedPath.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) &&
            !normalizedPath.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
            return false;

        // Check for path traversal patterns
        if (filePath.Contains("..") || filePath.Contains('~'))
            return false;

        try
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var yaml = serializer.Serialize(config);
            File.WriteAllText(filePath, yaml);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"YAML saving error: {ex}");
            return false;
        }
    }

    /// <summary>
    /// Checks if a file can be parsed as a PDF.
    /// </summary>
    /// <param name="filePath">Path to check.</param>
    /// <returns>True if file is a valid PDF.</returns>
    public bool CanParse(string filePath)
    {
        return _pdfParser.CanParse(filePath);
    }

}
