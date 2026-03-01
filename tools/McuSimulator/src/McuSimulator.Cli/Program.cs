using System.CommandLine;
using System.Diagnostics;
using Common.Cli;
using Common.Dto.Serialization;
using FpgaSimulator.Core.Csi2;
using McuSimulator.Core.Csi2;
using McuSimulator.Core.Network;

using DtoCsi2Packet = Common.Dto.Dtos.Csi2Packet;

namespace McuSimulator.Cli;

/// <summary>
/// CLI application for the MCU Simulator module.
/// Reads CSI-2 packets and processes them through the MCU pipeline,
/// producing UDP frame packets and statistics.
/// </summary>
public sealed class McuSimulatorCli : CliFramework
{
    private static readonly Option<string> InputOption = new("--input", "-i")
    {
        Description = "Input CSI-2 packet file (.csi2)",
        Required = true
    };

    private static readonly Option<int> BuffersOption = new("--buffers", "-b")
    {
        Description = "Number of frame buffers",
        DefaultValueFactory = _ => 4
    };

    private static readonly Option<string> CommandOption = new("--command")
    {
        Description = "MCU command: start_scan, stop_scan, status",
        DefaultValueFactory = _ => "start_scan"
    };

    /// <inheritdoc />
    protected override string CommandDescription =>
        "MCU Simulator CLI - Processes CSI-2 packets through MCU pipeline";

    /// <inheritdoc />
    protected override RootCommand BuildCommand()
    {
        var root = CreateRootCommand();
        root.Add(InputOption);
        root.Add(BuffersOption);
        root.Add(CommandOption);
        root.Add(OutputOption);

        root.SetAction(parseResult =>
        {
            string input = parseResult.GetValue(InputOption)!;
            int buffers = parseResult.GetValue(BuffersOption);
            string command = parseResult.GetValue(CommandOption) ?? "start_scan";
            string? output = parseResult.GetValue(OutputOption);
            bool verbose = parseResult.GetValue(VerboseOption);

            WriteVerbose(verbose, $"Reading CSI-2 packets from: {input}");
            WriteVerbose(verbose, $"Buffers: {buffers}, Command: {command}");

            if (!File.Exists(input))
            {
                Console.Error.WriteLine($"Error: Input file not found: {input}");
                return 1;
            }

            var sw = Stopwatch.StartNew();

            // Read CSI-2 packets (DTO format)
            DtoCsi2Packet[] dtoPackets = Csi2PacketSerializer.ReadFromFile(input);
            WriteVerbose(verbose, $"Loaded {dtoPackets.Length} CSI-2 packets");

            // Convert DTO packets to FPGA Csi2Packets for the RX parser
            var fpgaPackets = ConvertToFpgaPackets(dtoPackets);

            // Parse CSI-2 packets through RX parser to reconstruct the frame
            var parser = new Csi2RxPacketParser();
            var frameResult = parser.ParseFullFrame(fpgaPackets);

            if (!frameResult.IsValid)
            {
                Console.Error.WriteLine("Error: Failed to reassemble frame from CSI-2 packets.");
                return 1;
            }

            WriteVerbose(verbose, $"Frame reassembled: {frameResult.Rows}x{frameResult.Cols}");

            // Fragment the frame into UDP packets using the transmitter
            var transmitter = new UdpFrameTransmitter();
            var udpPackets = transmitter.FragmentFrame(frameResult.Pixels, frameId: 1);

            WriteVerbose(verbose, $"Fragmented into {udpPackets.Count} UDP packets");

            // Convert to serializable entries
            var entries = new UdpPacketEntry[udpPackets.Count];
            for (int i = 0; i < udpPackets.Count; i++)
            {
                entries[i] = new UdpPacketEntry
                {
                    Data = udpPackets[i].Data,
                    PacketIndex = udpPackets[i].PacketIndex,
                    TotalPackets = udpPackets[i].TotalPackets,
                    Flags = udpPackets[i].Flags
                };
            }

            // Determine output path
            output ??= Path.ChangeExtension(input, ".udp");

            // Write UDP packets
            UdpPacketSerializer.WriteToFile(entries, output);

            sw.Stop();

            // Print statistics
            Console.WriteLine($"UDP packets written to: {output}");
            Console.WriteLine($"Frame: {frameResult.Rows}x{frameResult.Cols}");
            Console.WriteLine($"CSI-2 packets: {dtoPackets.Length} -> UDP packets: {udpPackets.Count}");
            Console.WriteLine($"Command: {command}, Buffers: {buffers}");

            // Write stats table
            var stats = new Dictionary<string, object>
            {
                ["Frame Rows"] = frameResult.Rows,
                ["Frame Cols"] = frameResult.Cols,
                ["Total Pixels"] = frameResult.TotalPixels,
                ["CSI-2 Packets In"] = dtoPackets.Length,
                ["UDP Packets Out"] = udpPackets.Count,
                ["Elapsed (ms)"] = sw.ElapsedMilliseconds
            };
            OutputFormatter.WriteTable(stats);

            return 0;
        });

        return root;
    }

    /// <summary>
    /// Converts Common.Dto CSI-2 packets back to FPGA-internal Csi2Packet records
    /// for processing by the RX parser.
    /// </summary>
    private static FpgaSimulator.Core.Csi2.Csi2Packet[] ConvertToFpgaPackets(
        DtoCsi2Packet[] dtoPackets)
    {
        var fpgaPackets = new FpgaSimulator.Core.Csi2.Csi2Packet[dtoPackets.Length];
        for (int i = 0; i < dtoPackets.Length; i++)
        {
            var dto = dtoPackets[i];
            byte[] data = dto.Payload;

            // Infer packet type from the data content
            Csi2PacketType packetType = InferPacketType(data);

            fpgaPackets[i] = new FpgaSimulator.Core.Csi2.Csi2Packet
            {
                PacketType = packetType,
                VirtualChannel = dto.VirtualChannel,
                Data = data,
                LineNumber = packetType == Csi2PacketType.LineData ? i - 1 : 0, // Approximate
                PixelCount = packetType == Csi2PacketType.LineData ? (data.Length - 6) / 2 : 0,
                Crc16 = 0
            };
        }
        return fpgaPackets;
    }

    /// <summary>
    /// Infers the CSI-2 packet type from the raw data.
    /// Short packets (4 bytes) are FS/FE, long packets are line data.
    /// </summary>
    private static Csi2PacketType InferPacketType(byte[] data)
    {
        if (data.Length == 4)
        {
            // Short packet: extract data type from first byte (lower 6 bits)
            int dataTypeId = data[0] & 0x3F;
            return dataTypeId switch
            {
                0x00 => Csi2PacketType.FrameStart,
                0x01 => Csi2PacketType.FrameEnd,
                0x02 => Csi2PacketType.LineStart,
                0x03 => Csi2PacketType.LineEnd,
                _ => Csi2PacketType.FrameStart
            };
        }

        // Long packet: assume line data
        return Csi2PacketType.LineData;
    }
}

/// <summary>
/// Entry point for McuSimulator CLI.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        return new McuSimulatorCli().ParseAndRun(args);
    }
}
