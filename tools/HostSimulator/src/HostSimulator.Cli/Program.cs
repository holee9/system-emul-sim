using System.CommandLine;
using System.Diagnostics;
using Common.Cli;
using Common.Dto.Dtos;
using Common.Dto.Serialization;
using HostSimulator.Core.Configuration;

namespace HostSimulator.Cli;

/// <summary>
/// CLI application for the Host Simulator module.
/// Reads UDP packet files and reassembles complete frames.
/// </summary>
public sealed class HostSimulatorCli : CliFramework
{
    private static readonly Option<string> InputOption = new("--input", "-i")
    {
        Description = "Input UDP packet file (.udp)",
        Required = true
    };

    private static readonly Option<int> TimeoutOption = new("--timeout", "-t")
    {
        Description = "Packet reassembly timeout in milliseconds",
        DefaultValueFactory = _ => 1000
    };

    /// <inheritdoc />
    protected override string CommandDescription =>
        "Host Simulator CLI - Reassembles frames from UDP packets";

    /// <inheritdoc />
    protected override RootCommand BuildCommand()
    {
        var root = CreateRootCommand();
        root.Add(InputOption);
        root.Add(TimeoutOption);
        root.Add(OutputOption);

        root.SetAction(parseResult =>
        {
            string input = parseResult.GetValue(InputOption)!;
            int timeout = parseResult.GetValue(TimeoutOption);
            string? output = parseResult.GetValue(OutputOption);
            bool verbose = parseResult.GetValue(VerboseOption);

            WriteVerbose(verbose, $"Reading UDP packets from: {input}");
            WriteVerbose(verbose, $"Reassembly timeout: {timeout}ms");

            if (!File.Exists(input))
            {
                Console.Error.WriteLine($"Error: Input file not found: {input}");
                return 1;
            }

            var sw = Stopwatch.StartNew();

            // Read UDP packets from file
            UdpPacketEntry[] packets = UdpPacketSerializer.ReadFromFile(input);
            WriteVerbose(verbose, $"Loaded {packets.Length} UDP packets");

            // Initialize HostSimulator with config
            var hostConfig = new HostConfig { PacketTimeoutMs = timeout };
            var simulator = new HostSimulator.Core.HostSimulator();
            simulator.Initialize(hostConfig);

            // Feed each packet's raw data to the simulator
            FrameData? reassembledFrame = null;
            int processedCount = 0;

            foreach (var packet in packets)
            {
                processedCount++;
                var result = simulator.Process(packet.Data);
                if (result is FrameData frame)
                {
                    reassembledFrame = frame;
                    WriteVerbose(verbose, $"Frame reassembled after {processedCount} packets");
                }
            }

            if (reassembledFrame == null)
            {
                Console.Error.WriteLine("Error: Could not reassemble a complete frame from the input packets.");
                Console.Error.WriteLine(simulator.GetStatus());
                return 1;
            }

            // Determine output path
            output ??= Path.ChangeExtension(input, ".raw");

            // Convert 1D pixel array to 2D for serialization
            int frameRows = reassembledFrame.Height;
            int frameCols = reassembledFrame.Width;
            var frame2D = new ushort[frameRows, frameCols];
            for (int r = 0; r < frameRows; r++)
            {
                for (int c = 0; c < frameCols; c++)
                {
                    frame2D[r, c] = reassembledFrame.Pixels[r * frameCols + c];
                }
            }

            // Write reassembled frame
            FrameDataSerializer.WriteToFile(frame2D, output);

            sw.Stop();

            // Print statistics
            Console.WriteLine($"Frame written to: {output}");
            Console.WriteLine($"Frame: {frameRows}x{frameCols}");
            Console.WriteLine($"Packets processed: {processedCount}, Timeout: {timeout}ms");

            var stats = new Dictionary<string, object>
            {
                ["Frame Rows"] = frameRows,
                ["Frame Cols"] = frameCols,
                ["Total Pixels"] = frameRows * frameCols,
                ["UDP Packets In"] = packets.Length,
                ["Packets Processed"] = processedCount,
                ["Elapsed (ms)"] = sw.ElapsedMilliseconds
            };
            OutputFormatter.WriteTable(stats);

            return 0;
        });

        return root;
    }
}

/// <summary>
/// Entry point for HostSimulator CLI.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        return new HostSimulatorCli().ParseAndRun(args);
    }
}
