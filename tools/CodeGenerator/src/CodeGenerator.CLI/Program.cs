namespace CodeGenerator.CLI;

using System;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;
using CodeGenerator.Core.Generators;
using CodeGenerator.Core.Models;

/// <summary>
/// CodeGenerator CLI entry point.
/// Generates skeleton code from detector_config.yaml.
/// SPEC-TOOLS-001 REQ-TOOLS-010~013
/// </summary>
public static class Program
{
    private const string Version = "1.0.0";
    private const string AutoGenComment = "// AUTO-GENERATED - DO NOT EDIT";

    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand(
            $"CodeGenerator CLI v{Version} - Generate skeleton code from detector_config.yaml");

        // Global options
        var configOption = new Option<FileInfo>(
            new[] { "-c", "--config" },
            () => new FileInfo("config/detector_config.yaml"),
            "Path to detector_config.yaml");
        var outputOption = new Option<DirectoryInfo>(
            new[] { "-o", "--output" },
            () => new DirectoryInfo("./generated"),
            "Output directory for generated files");
        var verboseOption = new Option<bool>(
            new[] { "-v", "--verbose" },
            "Enable verbose output");

        rootCommand.AddGlobalOption(configOption);
        rootCommand.AddGlobalOption(outputOption);
        rootCommand.AddGlobalOption(verboseOption);

        // Command: generate-all
        var generateAllCommand = new Command("generate-all", "Generate all code artifacts");
        generateAllCommand.SetHandler(async (FileInfo config, DirectoryInfo output, bool verbose) =>
        {
            await GenerateAllAsync(config, output, verbose);
        }, configOption, outputOption, verboseOption);
        rootCommand.AddCommand(generateAllCommand);

        // Command: generate-rtl
        var generateRtlCommand = new Command("generate-rtl", "Generate SystemVerilog RTL files");
        generateRtlCommand.SetHandler(async (FileInfo config, DirectoryInfo output, bool verbose) =>
        {
            await GenerateRtlAsync(config, output, verbose);
        }, configOption, outputOption, verboseOption);
        rootCommand.AddCommand(generateRtlCommand);

        // Command: generate-headers
        var generateHeadersCommand = new Command("generate-headers", "Generate C header files");
        generateHeadersCommand.SetHandler(async (FileInfo config, DirectoryInfo output, bool verbose) =>
        {
            await GenerateHeadersAsync(config, output, verbose);
        }, configOption, outputOption, verboseOption);
        rootCommand.AddCommand(generateHeadersCommand);

        // Command: generate-sdk
        var generateSdkCommand = new Command("generate-sdk", "Generate C# SDK files");
        generateSdkCommand.SetHandler(async (FileInfo config, DirectoryInfo output, bool verbose) =>
        {
            await GenerateSdkAsync(config, output, verbose);
        }, configOption, outputOption, verboseOption);
        rootCommand.AddCommand(generateSdkCommand);

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task<int> GenerateAllAsync(FileInfo configFile, DirectoryInfo outputDir, bool verbose)
    {
        try
        {
            if (verbose)
            {
                Console.WriteLine($"CodeGenerator CLI v{Version}");
                Console.WriteLine($"Config: {configFile.FullName}");
                Console.WriteLine($"Output: {outputDir.FullName}");
            }

            // Load configuration
            var config = await DetectorConfig.LoadFromFileAsync(configFile.FullName);

            // Create output directory
            outputDir.Create();

            // Generate all artifacts
            var rtlDir = Path.Combine(outputDir.FullName, "rtl");
            var includeDir = Path.Combine(outputDir.FullName, "include");
            var sdkDir = Path.Combine(outputDir.FullName, "sdk");

            var svGenerator = new SystemVerilogGenerator();
            var cHeaderGenerator = new CHeaderGenerator();
            var cSharpGenerator = new CSharpGenerator();

            await svGenerator.GenerateAllAsync(config, rtlDir);
            if (verbose)
                Console.WriteLine($"[OK] Generated RTL files to {rtlDir}");

            await cHeaderGenerator.GenerateAllAsync(config, includeDir);
            if (verbose)
                Console.WriteLine($"[OK] Generated C headers to {includeDir}");

            await cSharpGenerator.GenerateAllAsync(config, sdkDir);
            if (verbose)
                Console.WriteLine($"[OK] Generated C# SDK files to {sdkDir}");

            Console.WriteLine("Code generation complete.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] {ex.Message}");
            if (verbose)
                Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    private static async Task<int> GenerateRtlAsync(FileInfo configFile, DirectoryInfo outputDir, bool verbose)
    {
        try
        {
            var config = await DetectorConfig.LoadFromFileAsync(configFile.FullName);
            outputDir.Create();

            var generator = new SystemVerilogGenerator();
            await generator.GenerateAllAsync(config, outputDir.FullName);

            if (verbose)
                Console.WriteLine($"[OK] Generated RTL files to {outputDir.FullName}");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> GenerateHeadersAsync(FileInfo configFile, DirectoryInfo outputDir, bool verbose)
    {
        try
        {
            var config = await DetectorConfig.LoadFromFileAsync(configFile.FullName);
            outputDir.Create();

            var generator = new CHeaderGenerator();
            await generator.GenerateAllAsync(config, outputDir.FullName);

            if (verbose)
                Console.WriteLine($"[OK] Generated C headers to {outputDir.FullName}");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> GenerateSdkAsync(FileInfo configFile, DirectoryInfo outputDir, bool verbose)
    {
        try
        {
            var config = await DetectorConfig.LoadFromFileAsync(configFile.FullName);
            outputDir.Create();

            var generator = new CSharpGenerator();
            await generator.GenerateAllAsync(config, outputDir.FullName);

            if (verbose)
                Console.WriteLine($"[OK] Generated C# SDK files to {outputDir.FullName}");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] {ex.Message}");
            return 1;
        }
    }
}
