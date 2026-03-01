using System.CommandLine;
using System.Diagnostics;
using Common.Cli;
using Common.Dto.Serialization;
using PanelSimulator.Models.Physics;

namespace PanelSimulator.Cli;

/// <summary>
/// CLI application for the Panel Simulator module.
/// Generates X-ray detector panel frames using scintillator physics model.
/// </summary>
public sealed class PanelSimulatorCli : CliFramework
{
    private static readonly Option<int> RowsOption = new("--rows", "-r")
    {
        Description = "Frame height in pixels",
        DefaultValueFactory = _ => 2048
    };

    private static readonly Option<int> ColsOption = new("--cols")
    {
        Description = "Frame width in pixels",
        DefaultValueFactory = _ => 2048
    };

    private static readonly Option<double> KvpOption = new("--kvp")
    {
        Description = "Tube voltage in kilovolts peak (40-150)",
        DefaultValueFactory = _ => 80.0
    };

    private static readonly Option<double> MasOption = new("--mas")
    {
        Description = "Tube current-time product in milliampere-seconds",
        DefaultValueFactory = _ => 10.0
    };

    private static readonly Option<string> NoiseOption = new("--noise", "-n")
    {
        Description = "Noise model: none, gaussian, composite",
        DefaultValueFactory = _ => "none"
    };

    /// <inheritdoc />
    protected override string CommandDescription =>
        "Panel Simulator CLI - Generates X-ray detector panel frames";

    /// <inheritdoc />
    protected override RootCommand BuildCommand()
    {
        var root = CreateRootCommand();
        root.Add(RowsOption);
        root.Add(ColsOption);
        root.Add(KvpOption);
        root.Add(MasOption);
        root.Add(NoiseOption);
        root.Add(OutputOption);

        root.SetAction(parseResult =>
        {
            int rows = parseResult.GetValue(RowsOption);
            int cols = parseResult.GetValue(ColsOption);
            double kvp = parseResult.GetValue(KvpOption);
            double mas = parseResult.GetValue(MasOption);
            string noise = parseResult.GetValue(NoiseOption) ?? "none";
            string? output = parseResult.GetValue(OutputOption);
            bool verbose = parseResult.GetValue(VerboseOption);
            int? seed = parseResult.GetValue(SeedOption);

            WriteVerbose(verbose, $"Generating {rows}x{cols} frame: kVp={kvp}, mAs={mas}, noise={noise}");

            var sw = Stopwatch.StartNew();

            // Generate base signal frame using scintillator model
            var config = new ScintillatorConfig(KVp: kvp, MAs: mas);
            var model = new ScintillatorModel(config);
            var frame = model.GenerateSignalFrame(rows, cols);

            WriteVerbose(verbose, $"Base signal generated in {sw.ElapsedMilliseconds}ms");

            // Apply noise if requested
            if (noise != "none")
            {
                var rng = seed.HasValue ? new Random(seed.Value) : new Random();
                ApplyNoise(frame, noise, rng);
                WriteVerbose(verbose, $"Noise applied: {noise}");
            }

            // Determine output path
            output ??= "panel_output.raw";

            // Write output
            FrameDataSerializer.WriteToFile(frame, output);

            sw.Stop();
            Console.WriteLine($"Frame written to: {output}");
            Console.WriteLine($"Dimensions: {rows}x{cols}, Signal: kVp={kvp} mAs={mas}");
            Console.WriteLine($"Elapsed: {sw.ElapsedMilliseconds}ms");

            return 0;
        });

        return root;
    }

    /// <summary>
    /// Applies noise to a frame in-place.
    /// </summary>
    private static void ApplyNoise(ushort[,] frame, string noiseType, Random rng)
    {
        int rows = frame.GetLength(0);
        int cols = frame.GetLength(1);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                double original = frame[r, c];
                double noiseValue = noiseType switch
                {
                    "gaussian" => BoxMullerGaussian(rng) * Math.Sqrt(original) * 0.5,
                    "composite" => BoxMullerGaussian(rng) * Math.Sqrt(original) * 0.5
                                   + (rng.NextDouble() - 0.5) * 10.0, // readout noise
                    _ => 0.0
                };

                double result = original + noiseValue;
                frame[r, c] = (ushort)Math.Clamp(Math.Round(result), 0, 65535);
            }
        }
    }

    /// <summary>
    /// Generates a standard normal random value using the Box-Muller transform.
    /// </summary>
    private static double BoxMullerGaussian(Random rng)
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}

/// <summary>
/// Entry point for PanelSimulator CLI.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        return new PanelSimulatorCli().ParseAndRun(args);
    }
}
