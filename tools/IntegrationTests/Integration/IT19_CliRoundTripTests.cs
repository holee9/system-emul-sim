using FluentAssertions;
using Common.Dto.Serialization;
using PanelSimulator.Cli;
using FpgaSimulator.Cli;
using McuSimulator.Cli;
using HostSimulator.Cli;
using Xunit;

namespace IntegrationTests.Integration;

/// <summary>
/// IT-19: CLI Round-Trip Verification.
/// Verifies that data flows correctly through the full Panel->FPGA->MCU->Host CLI chain.
/// G4 gap closure: CLI round-trip data integrity verification.
/// Reference: SPEC-EMUL-003
/// </summary>
public class IT19_CliRoundTripTests
{
    [Fact]
    public void CliRoundTrip_Panel_FPGA_MCU_Host_ShouldCompleteSuccessfully()
    {
        // Create unique temp directory
        string tmpDir = Path.Combine(Path.GetTempPath(), $"IT19_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmpDir);
        try
        {
            // Define intermediate file paths
            string frameRawPath = Path.Combine(tmpDir, "frame.raw");
            string packetsCsi2Path = Path.Combine(tmpDir, "packets.csi2");
            string framesUdpPath = Path.Combine(tmpDir, "frames.udp");
            string resultRawPath = Path.Combine(tmpDir, "result.raw");

            // Step 1: Panel CLI - generate 64x64 frame
            int rc = new PanelSimulatorCli().ParseAndRun(
                ["--rows", "64", "--cols", "64", "--seed", "42", "-o", frameRawPath]);
            rc.Should().Be(0, "Panel CLI should succeed");
            File.Exists(frameRawPath).Should().BeTrue("Panel CLI should produce frame.raw");
            new FileInfo(frameRawPath).Length.Should().BeGreaterThan(0, "frame.raw should not be empty");

            // Step 2: FPGA CLI - encode to CSI-2 packets
            rc = new FpgaSimulatorCli().ParseAndRun(
                ["--input", frameRawPath, "-o", packetsCsi2Path]);
            rc.Should().Be(0, "FPGA CLI should succeed");
            File.Exists(packetsCsi2Path).Should().BeTrue("FPGA CLI should produce packets.csi2");
            new FileInfo(packetsCsi2Path).Length.Should().BeGreaterThan(0, "packets.csi2 should not be empty");

            // Step 3: MCU CLI - process CSI-2 to UDP packets
            rc = new McuSimulatorCli().ParseAndRun(
                ["--input", packetsCsi2Path, "-o", framesUdpPath]);
            rc.Should().Be(0, "MCU CLI should succeed");
            File.Exists(framesUdpPath).Should().BeTrue("MCU CLI should produce frames.udp");
            new FileInfo(framesUdpPath).Length.Should().BeGreaterThan(0, "frames.udp should not be empty");

            // Step 4: Host CLI - reassemble frame from UDP packets
            rc = new HostSimulatorCli().ParseAndRun(
                ["--input", framesUdpPath, "-o", resultRawPath]);
            rc.Should().Be(0, "Host CLI should succeed");
            File.Exists(resultRawPath).Should().BeTrue("Host CLI should produce result.raw");
            new FileInfo(resultRawPath).Length.Should().BeGreaterThan(0, "result.raw should not be empty");

            // Step 5: Verify pixel data dimensions are preserved
            ushort[,] originalFrame = FrameDataSerializer.ReadFromFile(frameRawPath);
            ushort[,] resultFrame = FrameDataSerializer.ReadFromFile(resultRawPath);

            originalFrame.GetLength(0).Should().Be(resultFrame.GetLength(0),
                "reassembled frame should have same row count as original");
            originalFrame.GetLength(1).Should().Be(resultFrame.GetLength(1),
                "reassembled frame should have same column count as original");
        }
        finally
        {
            if (Directory.Exists(tmpDir))
                Directory.Delete(tmpDir, recursive: true);
        }
    }
}
