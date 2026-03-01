using System.CommandLine;
using System.Diagnostics;
using Common.Cli;
using Common.Dto.Serialization;
using FpgaSimulator.Core.Csi2;
using HostSimulator.Core.Configuration;
using McuSimulator.Core.Csi2;
using McuSimulator.Core.Network;
using PanelSimulator.Models.Physics;

namespace IntegrationRunner.Cli;

/// <summary>
/// CLI application for end-to-end integration pipeline.
/// Runs the full 4-layer chain: Panel -> FPGA CSI-2 TX -> MCU reassemble+fragment -> Host reassemble.
/// </summary>
public sealed class IntegrationRunnerCli : CliFramework
{
    private static readonly Option<int> FramesOption = new("--frames")
    {
        Description = "Number of frames to process",
        DefaultValueFactory = _ => 1
    };

    private static readonly Option<int> RowsOption = new("--rows", "-r")
    {
        Description = "Panel rows (height in pixels)",
        DefaultValueFactory = _ => 256
    };

    private static readonly Option<int> ColsOption = new("--cols")
    {
        Description = "Panel columns (width in pixels)",
        DefaultValueFactory = _ => 256
    };

    private static readonly Option<double> LossRateOption = new("--loss-rate")
    {
        Description = "Network packet loss rate (0.0 - 1.0)",
        DefaultValueFactory = _ => 0.0
    };

    private static readonly Option<double> ReorderRateOption = new("--reorder-rate")
    {
        Description = "Network packet reorder rate (0.0 - 1.0)",
        DefaultValueFactory = _ => 0.0
    };

    /// <inheritdoc />
    protected override string CommandDescription =>
        "Integration Runner CLI - End-to-end 4-layer simulation pipeline";

    /// <inheritdoc />
    protected override RootCommand BuildCommand()
    {
        var root = CreateRootCommand();
        root.Add(FramesOption);
        root.Add(RowsOption);
        root.Add(ColsOption);
        root.Add(LossRateOption);
        root.Add(ReorderRateOption);
        root.Add(OutputOption);

        root.SetAction(parseResult =>
        {
            int frames = parseResult.GetValue(FramesOption);
            int rows = parseResult.GetValue(RowsOption);
            int cols = parseResult.GetValue(ColsOption);
            double lossRate = parseResult.GetValue(LossRateOption);
            double reorderRate = parseResult.GetValue(ReorderRateOption);
            string? output = parseResult.GetValue(OutputOption);
            bool verbose = parseResult.GetValue(VerboseOption);
            int? seed = parseResult.GetValue(SeedOption);

            WriteVerbose(verbose, $"Pipeline: {rows}x{cols}, {frames} frame(s)");
            WriteVerbose(verbose, $"Loss rate: {lossRate}, Reorder rate: {reorderRate}");

            var rng = seed.HasValue ? new Random(seed.Value) : new Random();
            var totalSw = Stopwatch.StartNew();

            // Initialize pipeline components
            var scintillatorConfig = new ScintillatorConfig();
            var scintillatorModel = new ScintillatorModel(scintillatorConfig);
            var csi2Tx = new Csi2TxPacketGenerator();
            var csi2Rx = new Csi2RxPacketParser();
            var udpTx = new UdpFrameTransmitter();
            var hostConfig = new HostConfig { PacketTimeoutMs = 5000 };
            var host = new HostSimulator.Core.HostSimulator();
            host.Initialize(hostConfig);

            int totalPacketsGenerated = 0;
            int framesCompleted = 0;

            // Ensure output directory exists
            if (output != null)
            {
                Directory.CreateDirectory(output);
            }

            for (int f = 0; f < frames; f++)
            {
                var frameSw = Stopwatch.StartNew();

                // Stage 1: Panel - Generate signal frame
                WriteVerbose(verbose, $"[Frame {f + 1}] Stage 1: Panel signal generation ({rows}x{cols})");
                ushort[,] panelFrame = scintillatorModel.GenerateSignalFrame(rows, cols);

                // Stage 2: FPGA CSI-2 TX - Encode frame as CSI-2 packets
                WriteVerbose(verbose, $"[Frame {f + 1}] Stage 2: FPGA CSI-2 TX encoding");
                Csi2Packet[] csi2Packets = csi2Tx.GenerateFullFrame(panelFrame);
                WriteVerbose(verbose, $"  Generated {csi2Packets.Length} CSI-2 packets");

                // Stage 3: MCU - Reassemble CSI-2 -> frame, then fragment -> UDP
                WriteVerbose(verbose, $"[Frame {f + 1}] Stage 3: MCU reassemble + fragment");
                var frameResult = csi2Rx.ParseFullFrame(csi2Packets);
                if (!frameResult.IsValid)
                {
                    Console.Error.WriteLine($"[Frame {f + 1}] Error: MCU failed to reassemble CSI-2 frame");
                    continue;
                }

                var udpPackets = udpTx.FragmentFrame(frameResult.Pixels, frameId: (uint)(f + 1));
                totalPacketsGenerated += udpPackets.Count;
                WriteVerbose(verbose, $"  Fragmented into {udpPackets.Count} UDP packets");

                // Apply network impairments (loss and reorder)
                var deliveredPackets = ApplyNetworkImpairments(udpPackets, lossRate, reorderRate, rng);
                WriteVerbose(verbose, $"  After impairments: {deliveredPackets.Count} packets delivered");

                // Stage 4: Host - Reassemble UDP packets into frame
                WriteVerbose(verbose, $"[Frame {f + 1}] Stage 4: Host reassembly");
                host.Reset();
                host.Initialize(hostConfig);

                Common.Dto.Dtos.FrameData? outputFrame = null;
                foreach (var pkt in deliveredPackets)
                {
                    var result = host.Process(pkt.Data);
                    if (result is Common.Dto.Dtos.FrameData fd)
                    {
                        outputFrame = fd;
                    }
                }

                frameSw.Stop();

                if (outputFrame != null)
                {
                    framesCompleted++;
                    WriteVerbose(verbose, $"[Frame {f + 1}] Completed in {frameSw.ElapsedMilliseconds}ms");

                    // Save output if directory specified
                    if (output != null)
                    {
                        string framePath = Path.Combine(output, $"frame_{f + 1:D4}.raw");
                        // Convert 1D pixel array to 2D for serialization
                        var frame2D = new ushort[outputFrame.Height, outputFrame.Width];
                        for (int r = 0; r < outputFrame.Height; r++)
                        {
                            for (int c = 0; c < outputFrame.Width; c++)
                            {
                                frame2D[r, c] = outputFrame.Pixels[r * outputFrame.Width + c];
                            }
                        }
                        FrameDataSerializer.WriteToFile(frame2D, framePath);
                        WriteVerbose(verbose, $"  Saved to: {framePath}");
                    }
                }
                else
                {
                    WriteVerbose(verbose, $"[Frame {f + 1}] Incomplete (loss rate may be too high)");
                }
            }

            totalSw.Stop();

            // Print summary statistics
            Console.WriteLine($"Pipeline complete: {framesCompleted}/{frames} frames");
            Console.WriteLine($"Frame size: {rows}x{cols}");

            var stats = new Dictionary<string, object>
            {
                ["Frame Size"] = $"{rows}x{cols}",
                ["Frames Requested"] = frames,
                ["Frames Completed"] = framesCompleted,
                ["Total UDP Packets"] = totalPacketsGenerated,
                ["Loss Rate"] = lossRate,
                ["Reorder Rate"] = reorderRate,
                ["Elapsed (ms)"] = totalSw.ElapsedMilliseconds
            };
            OutputFormatter.WriteTable(stats);

            return framesCompleted == frames ? 0 : 1;
        });

        return root;
    }

    /// <summary>
    /// Applies simulated network impairments (packet loss and reordering) to UDP packets.
    /// </summary>
    private static List<UdpFramePacket> ApplyNetworkImpairments(
        List<UdpFramePacket> packets, double lossRate, double reorderRate, Random rng)
    {
        if (lossRate <= 0.0 && reorderRate <= 0.0)
            return packets;

        // Apply packet loss
        var surviving = new List<UdpFramePacket>(packets.Count);
        foreach (var pkt in packets)
        {
            if (rng.NextDouble() >= lossRate)
            {
                surviving.Add(pkt);
            }
        }

        // Apply reordering (Fisher-Yates partial shuffle)
        if (reorderRate > 0.0)
        {
            for (int i = surviving.Count - 1; i > 0; i--)
            {
                if (rng.NextDouble() < reorderRate)
                {
                    int j = rng.Next(0, i + 1);
                    (surviving[i], surviving[j]) = (surviving[j], surviving[i]);
                }
            }
        }

        return surviving;
    }
}

/// <summary>
/// Entry point for IntegrationRunner CLI.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        return new IntegrationRunnerCli().ParseAndRun(args);
    }
}
