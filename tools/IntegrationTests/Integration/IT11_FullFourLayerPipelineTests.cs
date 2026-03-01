using Xunit;
using FluentAssertions;
using IntegrationTests.Helpers;
using PanelSimulator.Models;
using Common.Dto.Dtos;
using McuSimulator.Core.Frame;
using FpgaSimulator.Core.Csi2;

namespace IntegrationTests.Integration;

/// <summary>
/// IT-11: Full 4-Layer Pipeline Test.
/// Validates Panel -> FPGA -> MCU -> Host -> Storage bit-exact data integrity.
/// Reference: SPEC-INTEG-001
/// </summary>
public class IT11_FullFourLayerPipelineTests
{
    private readonly SimulatorPipelineBuilder _pipeline;

    public IT11_FullFourLayerPipelineTests()
    {
        _pipeline = new SimulatorPipelineBuilder();
    }

    [Fact]
    public void CounterPattern_SmallFrame_BitExactMatch()
    {
        // Arrange - 256x256 Counter pattern (no noise, no defects)
        var config = new PanelConfig
        {
            Rows = 256,
            Cols = 256,
            BitDepth = 16,
            TestPattern = TestPattern.Counter,
            NoiseModel = NoiseModelType.None,
            NoiseStdDev = 0,
            DefectRate = 0,
            Seed = 42
        };

        // Act - Run full 4-layer pipeline with checkpoints
        var result = _pipeline.ProcessFrameWithCheckpoints(config);

        // Assert - Pipeline completed successfully
        result.IsValid.Should().BeTrue("pipeline should complete successfully");
        result.HostOutput.Should().NotBeNull("host should produce output");

        // Checkpoint 1: Panel -> FPGA (packet count)
        result.FpgaCsi2Packets.Should().HaveCount(256 + 2,
            "CSI-2 packets should be rows + 2 (FS + lines + FE)");
        result.FpgaCsi2Packets[0].PacketType.Should().Be(Csi2PacketType.FrameStart);
        result.FpgaCsi2Packets[^1].PacketType.Should().Be(Csi2PacketType.FrameEnd);

        // Checkpoint 2: FPGA -> MCU (reassembled frame matches original)
        result.McuReassembledFrame.IsValid.Should().BeTrue();
        result.McuReassembledFrame.Rows.Should().Be(256);
        result.McuReassembledFrame.Cols.Should().Be(256);
        AssertPixels2DMatch(result.PanelPixels2D, result.McuReassembledFrame.Pixels, 256, 256);

        // Checkpoint 3: MCU -> Host (final output matches original)
        var hostPixels = result.HostOutput!;
        hostPixels.Width.Should().Be(256);
        hostPixels.Height.Should().Be(256);
        AssertPixels1DMatch(result.PanelOutput.Pixels, hostPixels.Pixels);
    }

    [Fact]
    public void Checkerboard_StandardFrame_CheckpointIntegrity()
    {
        // Arrange - 1024x1024 Checkerboard pattern
        var config = new PanelConfig
        {
            Rows = 1024,
            Cols = 1024,
            BitDepth = 16,
            TestPattern = TestPattern.Checkerboard,
            NoiseModel = NoiseModelType.None,
            NoiseStdDev = 0,
            DefectRate = 0,
            Seed = 42
        };

        // Act
        var result = _pipeline.ProcessFrameWithCheckpoints(config);

        // Assert
        result.IsValid.Should().BeTrue();

        // Checkpoint 1: Packet count
        result.FpgaCsi2Packets.Should().HaveCount(1024 + 2);

        // Checkpoint 2: MCU reassembly matches Panel output
        result.McuReassembledFrame.IsValid.Should().BeTrue();
        AssertPixels2DMatch(result.PanelPixels2D, result.McuReassembledFrame.Pixels, 1024, 1024);

        // Checkpoint 3: Host output matches Panel output
        result.HostOutput.Should().NotBeNull();
        AssertPixels1DMatch(result.PanelOutput.Pixels, result.HostOutput!.Pixels);
    }

    [Fact]
    public void FlatField_LargeFrame_PipelineStability()
    {
        // Arrange - 2048x2048 FlatField (tests large frame pipeline stability)
        var config = new PanelConfig
        {
            Rows = 2048,
            Cols = 2048,
            BitDepth = 16,
            TestPattern = TestPattern.FlatField,
            NoiseModel = NoiseModelType.None,
            NoiseStdDev = 0,
            DefectRate = 0,
            Seed = 42
        };

        // Act
        var result = _pipeline.ProcessFrameWithCheckpoints(config);

        // Assert
        result.IsValid.Should().BeTrue("large frame pipeline should complete");

        // Checkpoint 1: Packet count
        result.FpgaCsi2Packets.Should().HaveCount(2048 + 2);

        // Checkpoint 2: MCU reassembly
        result.McuReassembledFrame.IsValid.Should().BeTrue();
        result.McuReassembledFrame.Rows.Should().Be(2048);
        result.McuReassembledFrame.Cols.Should().Be(2048);

        // Verify BitArray bitmap covers all 2048 rows
        result.McuReassembledFrame.ReceivedLineBitmap.Length.Should().Be(2048);
        for (int i = 0; i < 2048; i++)
        {
            result.McuReassembledFrame.ReceivedLineBitmap[i].Should().BeTrue(
                $"line {i} should be received");
        }

        // Checkpoint 3: Pixel data integrity
        AssertPixels2DMatch(result.PanelPixels2D, result.McuReassembledFrame.Pixels, 2048, 2048);

        // Checkpoint 4: Host output
        result.HostOutput.Should().NotBeNull();
        AssertPixels1DMatch(result.PanelOutput.Pixels, result.HostOutput!.Pixels);
    }

    [Fact]
    public void NoiseFrame_DataPreservation()
    {
        // Arrange - 512x512 FlatField with Gaussian noise
        var config = new PanelConfig
        {
            Rows = 512,
            Cols = 512,
            BitDepth = 16,
            TestPattern = TestPattern.FlatField,
            NoiseModel = NoiseModelType.Gaussian,
            NoiseStdDev = 50.0,
            DefectRate = 0,
            Seed = 42
        };

        // Act
        var result = _pipeline.ProcessFrameWithCheckpoints(config);

        // Assert - Pipeline succeeds
        result.IsValid.Should().BeTrue();

        // Noise data should be preserved through pipeline (bit-exact)
        AssertPixels2DMatch(result.PanelPixels2D, result.McuReassembledFrame.Pixels, 512, 512);
        AssertPixels1DMatch(result.PanelOutput.Pixels, result.HostOutput!.Pixels);

        // Verify noise is present (not all pixels identical)
        var pixels = result.PanelOutput.Pixels;
        var distinctValues = new HashSet<ushort>(pixels);
        distinctValues.Count.Should().BeGreaterThan(1,
            "noisy frame should have multiple distinct pixel values");
    }

    [Fact]
    public void ProcessFrame_ReturnsValidFrameData()
    {
        // Arrange - Simple counter pattern
        var config = new PanelConfig
        {
            Rows = 64,
            Cols = 64,
            BitDepth = 16,
            TestPattern = TestPattern.Counter,
            NoiseModel = NoiseModelType.None,
            NoiseStdDev = 0,
            DefectRate = 0,
            Seed = 42
        };

        // Act - Use simple ProcessFrame API
        var result = _pipeline.ProcessFrame(config);

        // Assert
        result.Should().NotBeNull();
        result.Width.Should().Be(64);
        result.Height.Should().Be(64);
        result.Pixels.Length.Should().Be(64 * 64);

        // Verify counter pattern preserved
        for (int r = 0; r < 64; r++)
        {
            for (int c = 0; c < 64; c++)
            {
                int expected = (r * 64 + c) & 0xFFFF;
                result.Pixels[r * 64 + c].Should().Be((ushort)expected,
                    $"pixel at [{r},{c}] should match counter pattern");
            }
        }
    }

    [Fact]
    public void UdpPackets_ContainValidHeaders()
    {
        // Arrange
        var config = new PanelConfig
        {
            Rows = 128,
            Cols = 128,
            BitDepth = 16,
            TestPattern = TestPattern.Counter,
            NoiseModel = NoiseModelType.None,
            NoiseStdDev = 0,
            DefectRate = 0,
            Seed = 42
        };

        // Act
        var result = _pipeline.ProcessFrameWithCheckpoints(config);

        // Assert - UDP packets should have valid structure
        result.McuUdpPackets.Should().NotBeEmpty();

        // All packets should have 32+ bytes (header + payload)
        foreach (var packet in result.McuUdpPackets)
        {
            packet.Data.Length.Should().BeGreaterThanOrEqualTo(32,
                "each UDP packet must have at least a 32-byte header");
        }

        // Last packet should have last_packet flag
        result.McuUdpPackets[^1].Flags.Should().Be(0x01,
            "last packet should have last_packet flag set");

        // Packet indices should be sequential
        for (int i = 0; i < result.McuUdpPackets.Count; i++)
        {
            result.McuUdpPackets[i].PacketIndex.Should().Be(i);
            result.McuUdpPackets[i].TotalPackets.Should().Be(result.McuUdpPackets.Count);
        }
    }

    /// <summary>
    /// Asserts that two 2D pixel arrays are identical.
    /// </summary>
    private static void AssertPixels2DMatch(ushort[,] expected, ushort[,] actual, int rows, int cols)
    {
        actual.GetLength(0).Should().Be(rows);
        actual.GetLength(1).Should().Be(cols);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (actual[r, c] != expected[r, c])
                {
                    actual[r, c].Should().Be(expected[r, c],
                        $"pixel mismatch at [{r},{c}]: expected {expected[r, c]}, got {actual[r, c]}");
                }
            }
        }
    }

    /// <summary>
    /// Asserts that two 1D pixel arrays are identical.
    /// </summary>
    private static void AssertPixels1DMatch(ushort[] expected, ushort[] actual)
    {
        actual.Length.Should().Be(expected.Length, "pixel array lengths should match");

        for (int i = 0; i < expected.Length; i++)
        {
            if (actual[i] != expected[i])
            {
                actual[i].Should().Be(expected[i],
                    $"pixel mismatch at index {i}: expected {expected[i]}, got {actual[i]}");
            }
        }
    }
}
