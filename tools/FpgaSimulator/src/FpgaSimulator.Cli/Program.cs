using System.CommandLine;
using System.Diagnostics;
using Common.Cli;
using Common.Dto.Serialization;
using FpgaSimulator.Core.Csi2;

using DtoCsi2Packet = Common.Dto.Dtos.Csi2Packet;
using DtoCsi2DataType = Common.Dto.Dtos.Csi2DataType;

namespace FpgaSimulator.Cli;

/// <summary>
/// CLI application for the FPGA Simulator module.
/// Reads panel frames and encodes them as CSI-2 packets.
/// </summary>
public sealed class FpgaSimulatorCli : CliFramework
{
    private static readonly Option<string> InputOption = new("--input", "-i")
    {
        Description = "Input panel frame file (.raw)",
        Required = true
    };

    private static readonly Option<string> ModeOption = new("--mode", "-m")
    {
        Description = "Scan mode: single, continuous, calibration",
        DefaultValueFactory = _ => "single"
    };

    private static readonly Option<string> ProtectionOption = new("--protection", "-p")
    {
        Description = "Protection logic: on or off",
        DefaultValueFactory = _ => "on"
    };

    /// <inheritdoc />
    protected override string CommandDescription =>
        "FPGA Simulator CLI - Encodes panel frames to CSI-2 packets";

    /// <inheritdoc />
    protected override RootCommand BuildCommand()
    {
        var root = CreateRootCommand();
        root.Add(InputOption);
        root.Add(ModeOption);
        root.Add(ProtectionOption);
        root.Add(OutputOption);

        root.SetAction(parseResult =>
        {
            string input = parseResult.GetValue(InputOption)!;
            string mode = parseResult.GetValue(ModeOption) ?? "single";
            string protection = parseResult.GetValue(ProtectionOption) ?? "on";
            string? output = parseResult.GetValue(OutputOption);
            bool verbose = parseResult.GetValue(VerboseOption);

            WriteVerbose(verbose, $"Reading panel frame from: {input}");
            WriteVerbose(verbose, $"Mode: {mode}, Protection: {protection}");

            if (!File.Exists(input))
            {
                Console.Error.WriteLine($"Error: Input file not found: {input}");
                return 1;
            }

            var sw = Stopwatch.StartNew();

            // Read the panel frame
            var frame = FrameDataSerializer.ReadFromFile(input);
            int rows = frame.GetLength(0);
            int cols = frame.GetLength(1);

            WriteVerbose(verbose, $"Frame loaded: {rows}x{cols} ({rows * cols} pixels)");

            // Generate CSI-2 packets using the FPGA TX packet generator
            var txGenerator = new Csi2TxPacketGenerator();
            Csi2Packet[] fpgaPackets = txGenerator.GenerateFullFrame(frame);

            WriteVerbose(verbose, $"Generated {fpgaPackets.Length} CSI-2 packets");

            // Convert FPGA Csi2Packets to Common.Dto Csi2Packets for serialization
            var dtoPackets = ConvertToDtoPackets(fpgaPackets);

            // Determine output path
            output ??= Path.ChangeExtension(input, ".csi2");

            // Write CSI-2 packets
            Csi2PacketSerializer.WriteToFile(dtoPackets, output);

            sw.Stop();
            Console.WriteLine($"CSI-2 packets written to: {output}");
            Console.WriteLine($"Frame: {rows}x{cols}, Packets: {fpgaPackets.Length}, Mode: {mode}");
            Console.WriteLine($"Elapsed: {sw.ElapsedMilliseconds}ms");

            return 0;
        });

        return root;
    }

    /// <summary>
    /// Converts FPGA-internal Csi2Packet records to Common.Dto Csi2Packet records for serialization.
    /// </summary>
    private static DtoCsi2Packet[] ConvertToDtoPackets(Csi2Packet[] fpgaPackets)
    {
        var dtoPackets = new DtoCsi2Packet[fpgaPackets.Length];
        for (int i = 0; i < fpgaPackets.Length; i++)
        {
            var fpga = fpgaPackets[i];
            var dataType = MapDataType(fpga.PacketType);
            dtoPackets[i] = new DtoCsi2Packet(dataType, fpga.VirtualChannel, fpga.Data);
        }
        return dtoPackets;
    }

    /// <summary>
    /// Maps FPGA packet type to DTO CSI-2 data type.
    /// </summary>
    private static DtoCsi2DataType MapDataType(Csi2PacketType packetType)
    {
        return packetType switch
        {
            Csi2PacketType.LineData => DtoCsi2DataType.Raw16,
            Csi2PacketType.FrameStart => DtoCsi2DataType.Raw8,
            Csi2PacketType.FrameEnd => DtoCsi2DataType.Raw8,
            Csi2PacketType.LineStart => DtoCsi2DataType.Raw8,
            Csi2PacketType.LineEnd => DtoCsi2DataType.Raw8,
            _ => DtoCsi2DataType.Raw16
        };
    }
}

/// <summary>
/// Entry point for FpgaSimulator CLI.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        return new FpgaSimulatorCli().ParseAndRun(args);
    }
}
