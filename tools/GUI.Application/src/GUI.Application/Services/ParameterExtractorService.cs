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
    public DetectorConfig ToDetectorConfig(IEnumerable<ParamExtractorModels.ParameterInfo> parameters)
    {
        var paramDict = parameters
            .Where(p => p.ValidationStatus == ParamExtractorModels.ValidationStatus.Valid || p.ValidationStatus == ParamExtractorModels.ValidationStatus.Warning)
            .ToDictionary(p => p.Category + ":" + p.Name, p => p, StringComparer.OrdinalIgnoreCase);

        var config = new DetectorConfig();

        // Panel parameters
        if (TryGetParam(paramDict, "panel", "Rows", out var rowsParam))
            config.Panel ??= new PanelConfig();
        if (TryGetParam(paramDict, "panel", "Rows", out var rowsVal) && rowsVal is not null && int.TryParse(rowsVal.Value, out var rows))
            (config.Panel ??= new PanelConfig()).Rows = rows;

        if (TryGetParam(paramDict, "panel", "Cols", out var colsVal) && colsVal is not null && int.TryParse(colsVal.Value, out var cols))
            (config.Panel ??= new PanelConfig()).Cols = cols;

        if (TryGetParam(paramDict, "panel", "BitDepth", out var bitDepthVal) && bitDepthVal is not null && int.TryParse(bitDepthVal.Value, out var bitDepth))
            (config.Panel ??= new PanelConfig()).BitDepth = bitDepth;

        // FPGA parameters
        if (TryGetParam(paramDict, "fpga", "Csi2Lanes", out var lanesVal) && lanesVal is not null && int.TryParse(lanesVal.Value, out var lanes))
            (config.Fpga ??= new FpgaConfig()).Csi2Lanes = lanes;

        if (TryGetParam(paramDict, "fpga", "Csi2DataRateMbps", out var rateVal) && rateVal is not null && int.TryParse(rateVal.Value, out var rate))
            (config.Fpga ??= new FpgaConfig()).Csi2DataRateMbps = rate;

        // SoC parameters
        if (TryGetParam(paramDict, "controller", "FrameBufferCount", out var fbVal) && fbVal is not null && int.TryParse(fbVal.Value, out var fb))
            (config.Soc ??= new SocConfig()).FrameBufferCount = fb;

        if (TryGetParam(paramDict, "controller", "UdpPort", out var portVal) && portVal is not null && int.TryParse(portVal.Value, out var port))
            (config.Soc ??= new SocConfig()).UdpPort = port;

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

    private static bool TryGetParam(Dictionary<string, ParamExtractorModels.ParameterInfo> dict, string category, string name, out ParamExtractorModels.ParameterInfo? param)
    {
        var key = category + ":" + name;
        return dict.TryGetValue(key, out param);
    }
}
