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
    public async Task<ParamExtractorModels.ExtractionResult> ParsePdfAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _pdfParser.ParseAsync(filePath, cancellationToken);

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
    public DetectorConfig ToDetectorConfig(IEnumerable<ParamExtractorModels.ParameterInfo> parameters)
    {
        var paramDict = parameters
            .Where(p => p.ValidationStatus == ParamExtractorModels.ValidationStatus.Valid || p.ValidationStatus == ParamExtractorModels.ValidationStatus.Warning)
            .ToDictionary(p => p.Category + ":" + p.Name, p => p, StringComparer.OrdinalIgnoreCase);

        var config = new DetectorConfig();

        // Panel parameters
        if (TryGetParam(paramDict, "panel", "Rows", out var rowsParam))
            config.Panel ??= new PanelConfig();
        if (TryGetParam(paramDict, "panel", "Rows", out var rowsVal) && int.TryParse(rowsVal.Value, out var rows))
            (config.Panel ??= new PanelConfig()).Rows = rows;

        if (TryGetParam(paramDict, "panel", "Cols", out var colsVal) && int.TryParse(colsVal.Value, out var cols))
            (config.Panel ??= new PanelConfig()).Cols = cols;

        if (TryGetParam(paramDict, "panel", "BitDepth", out var bitDepthVal) && int.TryParse(bitDepthVal.Value, out var bitDepth))
            (config.Panel ??= new PanelConfig()).BitDepth = bitDepth;

        // FPGA parameters
        if (TryGetParam(paramDict, "fpga", "Csi2Lanes", out var lanesVal) && int.TryParse(lanesVal.Value, out var lanes))
            (config.Fpga ??= new FpgaConfig()).Csi2Lanes = lanes;

        if (TryGetParam(paramDict, "fpga", "Csi2DataRateMbps", out var rateVal) && int.TryParse(rateVal.Value, out var rate))
            (config.Fpga ??= new FpgaConfig()).Csi2DataRateMbps = rate;

        // SoC parameters
        if (TryGetParam(paramDict, "controller", "FrameBufferCount", out var fbVal) && int.TryParse(fbVal.Value, out var fb))
            (config.Soc ??= new SocConfig()).FrameBufferCount = fb;

        if (TryGetParam(paramDict, "controller", "UdpPort", out var portVal) && int.TryParse(portVal.Value, out var port))
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
        try
        {
            var yaml = File.ReadAllText(filePath);
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
