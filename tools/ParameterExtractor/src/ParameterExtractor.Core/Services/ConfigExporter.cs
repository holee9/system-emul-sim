using NJsonSchema;
using NJsonSchema.Validation;
using ParameterExtractor.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ParameterExtractor.Core.Services;

/// <summary>
/// Configuration exporter implementation with JSON schema validation.
/// </summary>
public class ConfigExporter : IConfigExporter
{
    private readonly IDeserializer _yamlDeserializer;
    private readonly ISerializer _yamlSerializer;

    public ConfigExporter()
    {
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        _yamlSerializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();
    }

    /// <inheritdoc />
    public async Task<ExportResult> ExportAsync(
        IEnumerable<ParameterInfo> parameters,
        string outputPath,
        string? schemaPath = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ExportResult
        {
            OutputPath = outputPath
        };

        try
        {
            // Convert parameters to DetectorConfig
            var config = BuildDetectorConfig(parameters);

            // Validate against schema if provided
            if (!string.IsNullOrWhiteSpace(schemaPath) && File.Exists(schemaPath))
            {
                var validationResult = await ValidateSchemaAsync(config, schemaPath);
                if (!validationResult.IsValid)
                {
                    result.IsSuccessful = false;
                    result.ValidationErrors = validationResult.Errors;
                    return result;
                }
            }

            // Serialize to YAML
            var yaml = _yamlSerializer.Serialize(config);

            // Ensure output directory exists
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write to file
            await File.WriteAllTextAsync(outputPath, yaml, cancellationToken);
            result.IsSuccessful = true;
        }
        catch (Exception ex)
        {
            result.IsSuccessful = false;
            result.Errors.Add($"Export failed: {ex.Message}");
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<SchemaValidationResult> ValidateSchemaAsync(DetectorConfig config, string schemaPath)
    {
        var result = new SchemaValidationResult();

        try
        {
            // Load schema
            var schema = await JsonSchema.FromFileAsync(schemaPath);
            var yaml = _yamlSerializer.Serialize(config);

            // Convert YAML to JSON for validation
            using var reader = new StringReader(yaml);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var yamlObject = deserializer.Deserialize(yaml);
            var json = System.Text.Json.JsonSerializer.Serialize(yamlObject);

            // Validate
            var errors = schema.Validate(json);
            result.IsValid = !errors.Any();

            foreach (var error in errors)
            {
                result.Errors.Add(new SchemaValidationError
                {
                    Path = error.Path,
                    Kind = error.Kind.ToString(),
                    Message = error.LineNumber > 0
                        ? $"Line {error.LineNumber}: {error.Kind}"
                        : error.Kind.ToString()
                });
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add(new SchemaValidationError
            {
                Path = string.Empty,
                Message = $"Schema validation error: {ex.Message}",
                Kind = "Exception"
            });
        }

        return result;
    }

    private DetectorConfig BuildDetectorConfig(IEnumerable<ParameterInfo> parameters)
    {
        var paramList = parameters.ToList();
        var config = new DetectorConfig
        {
            Panel = new PanelConfig(),
            Fpga = new FpgaConfig
            {
                Timing = new TimingConfig(),
                LineBuffer = new LineBufferConfig(),
                DataInterface = new DataInterfaceConfig
                {
                    Csi2 = new Csi2Config()
                },
                Spi = new SpiConfig(),
                Protection = new ProtectionConfig()
            },
            Controller = new ControllerConfig
            {
                Ethernet = new EthernetConfig(),
                FrameBuffer = new FrameBufferConfig(),
                Csi2Rx = new Csi2RxConfig()
            },
            Host = new HostConfig
            {
                Storage = new StorageConfig(),
                Display = new DisplayConfig(),
                Network = new NetworkConfig()
            }
        };

        // Map parameters to config by category
        foreach (var param in paramList)
        {
            MapParameterToConfig(param, config);
        }

        return config;
    }

    private void MapParameterToConfig(ParameterInfo param, DetectorConfig config)
    {
        var nameLower = param.Name.ToLowerInvariant();

        // Panel parameters
        if (nameLower.Contains("row") && param.NumericValue.HasValue)
            config.Panel!.Rows = (int)param.NumericValue.Value;

        if (nameLower.Contains("col") && param.NumericValue.HasValue)
            config.Panel!.Cols = (int)param.NumericValue.Value;

        if (nameLower.Contains("pixel") && nameLower.Contains("pitch") && param.NumericValue.HasValue)
            config.Panel!.PixelPitchUm = param.NumericValue.Value;

        if ((nameLower.Contains("bit") || nameLower.Contains("depth")) && param.NumericValue.HasValue)
            config.Panel!.BitDepth = (int)param.NumericValue.Value;

        // FPGA timing parameters
        if (nameLower.Contains("gate") && nameLower.Contains("on") && param.NumericValue.HasValue)
            config.Fpga!.Timing!.GateOnUs = param.NumericValue.Value;

        if (nameLower.Contains("gate") && nameLower.Contains("off") && param.NumericValue.HasValue)
            config.Fpga!.Timing!.GateOffUs = param.NumericValue.Value;

        if (nameLower.Contains("settle") && param.NumericValue.HasValue)
            config.Fpga!.Timing!.RoicSettleUs = param.NumericValue.Value;

        if (nameLower.Contains("adc") && param.NumericValue.HasValue)
            config.Fpga!.Timing!.AdcConvUs = param.NumericValue.Value;

        // CSI-2 parameters
        if (nameLower.Contains("lane") && !nameLower.Contains("blank") && param.NumericValue.HasValue)
            config.Fpga!.DataInterface!.Csi2!.LaneCount = (int)param.NumericValue.Value;

        if (nameLower.Contains("mbps") && param.NumericValue.HasValue)
            config.Fpga!.DataInterface!.Csi2!.LaneSpeedMbps = (int)param.NumericValue.Value;

        // SPI parameters
        if (nameLower.Contains("spi") && nameLower.Contains("clock") && param.NumericValue.HasValue)
            config.Fpga!.Spi!.ClockHz = (long)(param.NumericValue.Value * 1_000_000);

        // Ethernet parameters
        if (nameLower.Contains("port") && param.NumericValue.HasValue)
            config.Controller!.Ethernet!.Port = (int)param.NumericValue.Value;

        if (nameLower.Contains("mtu") && param.NumericValue.HasValue)
            config.Controller!.Ethernet!.Mtu = (int)param.NumericValue.Value;
    }
}
