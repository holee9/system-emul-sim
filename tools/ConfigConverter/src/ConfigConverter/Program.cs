using Microsoft.Extensions.Logging;
using ConfigConverter.Services;
using ConfigConverter.Converters;
using ConfigConverter.Validators;
using ConfigConverter.Models;

namespace ConfigConverter;

/// <summary>
/// ConfigConverter CLI entry point.
/// Converts detector_config.yaml to target-specific formats (.xdc, .dts, .json).
/// </summary>
public class Program
{
    private static ILogger<Program>? _logger;
    private static readonly ConsoleColor OriginalColor = Console.ForegroundColor;

    public static int Main(string[] args)
    {
        // Configure logging
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddSimpleConsole(options =>
            {
                options.IncludeScopes = false;
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            });
            builder.SetMinimumLevel(LogLevel.Information);
        });

        _logger = loggerFactory.CreateLogger<Program>();

        try
        {
            return Run(args);
        }
        catch (Exception ex)
        {
            LogError($"Fatal error: {ex.Message}");
            if (ex.InnerException != null)
            {
                LogError($"  Inner: {ex.InnerException.Message}");
            }
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        if (args.Length == 0)
        {
            ShowUsage();
            return 0;
        }

        var inputFile = args[0];
        var targets = ParseTargets(args.Skip(1).ToArray());

        if (!File.Exists(inputFile))
        {
            LogError($"Input file not found: {inputFile}");
            return 1;
        }

        LogInfo($"ConfigConverter v1.0.0");
        LogInfo($"Input: {inputFile}");
        LogInfo($"Targets: {string.Join(", ", targets)}");
        LogInfo();

        // Parse YAML
        LogInfo("Parsing YAML configuration...");
        var yamlParser = new YamlParser();
        var config = yamlParser.ParseFile(inputFile);
        LogInfo($"  Loaded: {config.Panel.Rows}x{config.Panel.Cols}, {config.Panel.BitDepth}-bit");
        LogInfo();

        // Schema validation
        LogInfo("Validating against schema...");
        var schemaValidator = new SchemaValidator();
        var schemaResult = schemaValidator.Validate(config);

        if (!schemaResult.IsValid)
        {
            LogError("Schema validation failed:");
            foreach (var error in schemaResult.Errors)
            {
                LogError($"  - {error}");
            }
            return 1;
        }
        LogInfo("  Schema validation passed.");
        LogInfo();

        // Cross-validation
        LogInfo("Running cross-validation checks...");
        var crossValidator = new CrossValidator();
        var crossResult = crossValidator.Validate(config);

        if (!crossResult.IsValid)
        {
            LogError("Cross-validation failed:");
            foreach (var error in crossResult.Errors)
            {
                LogError($"  - {error}");
            }
            return 1;
        }

        if (crossResult.Warnings.Count > 0)
        {
            LogWarning("Cross-validation warnings:");
            foreach (var warning in crossResult.Warnings)
            {
                LogWarning($"  - {warning}");
            }
            LogWarning("  Continuing with warnings...");
            LogInfo();
        }
        else
        {
            LogInfo("  Cross-validation passed.");
            LogInfo();
        }

        // Convert to target formats
        var baseName = Path.GetFileNameWithoutExtension(inputFile);
        var outputDir = Path.GetDirectoryName(inputFile) ?? ".";

        foreach (var target in targets)
        {
            try
            {
                ConvertTarget(config, target, baseName, outputDir);
            }
            catch (Exception ex)
            {
                LogError($"Failed to convert to {target}: {ex.Message}");
                return 1;
            }
        }

        LogInfo();
        LogSuccess("Conversion complete.");
        return 0;
    }

    private static void ConvertTarget(DetectorConfig config, string target, string baseName, string outputDir)
    {
        LogInfo($"Converting to {target.ToUpper()}...");

        var outputFile = Path.Combine(outputDir, $"{baseName}.{target}");

        switch (target.ToLower())
        {
            case "xdc":
                var xdcConverter = new XdcConverter();
                var xdcContent = xdcConverter.Convert(config);
                File.WriteAllText(outputFile, xdcContent);
                LogInfo($"  Written: {outputFile}");
                break;

            case "dts":
                var dtsConverter = new DtsConverter();
                var dtsContent = dtsConverter.Convert(config);
                File.WriteAllText(outputFile, dtsContent);
                LogInfo($"  Written: {outputFile}");
                break;

            case "json":
                var jsonConverter = new JsonConverter();
                var jsonContent = jsonConverter.Convert(config);
                File.WriteAllText(outputFile, jsonContent);
                LogInfo($"  Written: {outputFile}");
                break;

            default:
                LogWarning($"Unknown target: {target}");
                break;
        }
    }

    private static HashSet<string> ParseTargets(string[] args)
    {
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var arg in args)
        {
            if (arg.StartsWith("--target=") || arg.StartsWith("-t="))
            {
                var value = arg.Contains('=') ? arg.Split('=')[1] : arg;
                foreach (var t in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    targets.Add(t.Trim());
                }
            }
            else if (arg == "--all" || arg == "-a")
            {
                targets.Add("xdc");
                targets.Add("dts");
                targets.Add("json");
            }
        }

        // Default: all targets if none specified
        if (targets.Count == 0)
        {
            targets.Add("xdc");
            targets.Add("dts");
            targets.Add("json");
        }

        return targets;
    }

    private static void ShowUsage()
    {
        Console.WriteLine();
        Console.WriteLine("ConfigConverter - X-ray Detector Configuration Converter");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  ConfigConverter <input.yaml> [options]");
        Console.WriteLine();
        Console.WriteLine("Arguments:");
        Console.WriteLine("  <input.yaml>    Path to detector_config.yaml file");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -t, --target=<targets>   Target format(s): xdc, dts, json (comma-separated)");
        Console.WriteLine("  -a, --all               Convert to all target formats (default)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  ConfigConverter config/detector_config.yaml");
        Console.WriteLine("  ConfigConverter config/detector_config.yaml --target=xdc,json");
        Console.WriteLine("  ConfigConverter config/detector_config.yaml --all");
        Console.WriteLine();
        Console.WriteLine("Requirements Implemented:");
        Console.WriteLine("  REQ-TOOLS-020: Convert to .xdc (FPGA constraints)");
        Console.WriteLine("  REQ-TOOLS-021: Convert to .dts (device tree overlay)");
        Console.WriteLine("  REQ-TOOLS-022: Convert to .json (Host SDK config)");
        Console.WriteLine("  REQ-TOOLS-023: Schema validation");
        Console.WriteLine("  REQ-TOOLS-024: Cross-validation checks");
        Console.WriteLine();
    }

    #region Logging Helpers

    private static void LogInfo(string? message = null)
    {
        if (!string.IsNullOrEmpty(message))
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(message);
            Console.ForegroundColor = OriginalColor;
        }
    }

    private static void LogSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(message);
        Console.ForegroundColor = OriginalColor;
    }

    private static void LogWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(message);
        Console.ForegroundColor = OriginalColor;
    }

    private static void LogError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ForegroundColor = OriginalColor;
    }

    #endregion
}
