using Xunit;
using FluentAssertions;
using IntegrationTests.Helpers;
using PanelSimulator.Models;
using Common.Dto.Dtos;

namespace IntegrationTests.Integration;

/// <summary>
/// IT-13: Pipeline Realization Verification (4-layer bit-exact).
/// Validates that the full pipeline correctly transforms data through all 4 layers
/// with various patterns and configurations.
/// Reference: SPEC-EMUL-001
/// </summary>
public class IT13_PipelineRealizationTests
{
    private readonly SimulatorPipelineBuilder _builder;

    public IT13_PipelineRealizationTests()
    {
        _builder = new SimulatorPipelineBuilder();
    }

    [Fact]
    public void FullPipeline_CounterPattern_PreservesData()
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
        var result = _builder.ProcessFrameWithCheckpoints(config);

        // Assert - Pipeline completed successfully
        result.IsValid.Should().BeTrue("pipeline should complete without errors");

        // Panel -> FPGA: verify CSI-2 packet structure
        result.FpgaCsi2Packets.Should().NotBeEmpty("FPGA should produce CSI-2 packets");

        // FPGA -> MCU: verify reassembly matches panel output
        result.McuReassembledFrame.IsValid.Should().BeTrue("MCU should reassemble frame successfully");
        result.McuReassembledFrame.Rows.Should().Be(256);
        result.McuReassembledFrame.Cols.Should().Be(256);

        // MCU -> Host: verify final output is bit-exact with panel input
        result.HostOutput.Should().NotBeNull("host should produce output");
        result.HostOutput!.Width.Should().Be(256);
        result.HostOutput.Height.Should().Be(256);

        // Bit-exact verification: compare panel 1D pixels with host output
        for (int i = 0; i < result.PanelOutput.Pixels.Length; i++)
        {
            result.HostOutput.Pixels[i].Should().Be(result.PanelOutput.Pixels[i],
                $"pixel at index {i} should be bit-exact through pipeline");
        }
    }

    [Fact]
    public void FullPipeline_CheckerboardPattern_PreservesData()
    {
        // Arrange - 256x256 Checkerboard pattern
        var config = new PanelConfig
        {
            Rows = 256,
            Cols = 256,
            BitDepth = 16,
            TestPattern = TestPattern.Checkerboard,
            NoiseModel = NoiseModelType.None,
            NoiseStdDev = 0,
            DefectRate = 0,
            Seed = 42
        };

        // Act
        var result = _builder.ProcessFrameWithCheckpoints(config);

        // Assert - Pipeline valid and bit-exact
        result.IsValid.Should().BeTrue("pipeline should complete successfully");
        result.HostOutput.Should().NotBeNull("host should produce output");

        // Verify 2D panel pixels match MCU reassembled frame
        for (int r = 0; r < 256; r++)
        {
            for (int c = 0; c < 256; c++)
            {
                result.McuReassembledFrame.Pixels[r, c].Should().Be(result.PanelPixels2D[r, c],
                    $"MCU pixel at [{r},{c}] should match panel output");
            }
        }

        // Verify 1D host output matches panel output
        result.HostOutput!.Pixels.Length.Should().Be(result.PanelOutput.Pixels.Length);
        for (int i = 0; i < result.PanelOutput.Pixels.Length; i++)
        {
            result.HostOutput.Pixels[i].Should().Be(result.PanelOutput.Pixels[i],
                $"host pixel at index {i} should be bit-exact");
        }
    }

    [Fact]
    public void FullPipeline_WithNetworkChannel_ZeroLoss_PreservesData()
    {
        // Arrange - Pipeline with network channel at 0% loss
        var networkConfig = new NetworkChannelConfig
        {
            PacketLossRate = 0.0,
            ReorderRate = 0.0,
            CorruptionRate = 0.0,
            Seed = 42
        };

        var pipeline = new SimulatorPipelineBuilder()
            .WithNetworkChannel(networkConfig)
            .Build(new PanelConfig
            {
                Rows = 128,
                Cols = 128,
                BitDepth = 16,
                TestPattern = TestPattern.Counter,
                NoiseModel = NoiseModelType.None,
                NoiseStdDev = 0,
                DefectRate = 0,
                Seed = 42
            });

        // Act - Process frame through pipeline with network channel
        var hostOutput = pipeline.ProcessFrame();

        // Assert - Data should pass through network channel without modification
        hostOutput.Should().NotBeNull("host should produce output with zero-loss network");
        hostOutput!.Width.Should().Be(128);
        hostOutput.Height.Should().Be(128);
        hostOutput.Pixels.Length.Should().Be(128 * 128);

        // Verify counter pattern preserved through network channel
        for (int r = 0; r < 128; r++)
        {
            for (int c = 0; c < 128; c++)
            {
                int expected = (r * 128 + c) & 0xFFFF;
                hostOutput.Pixels[r * 128 + c].Should().Be((ushort)expected,
                    $"pixel at [{r},{c}] should match counter pattern after network transit");
            }
        }

        // Verify network statistics show zero loss
        var stats = pipeline.GetStatistics();
        stats.FramesProcessed.Should().Be(1);
        stats.FramesCompleted.Should().Be(1);
        stats.FramesFailed.Should().Be(0);
        stats.NetworkStats.Should().NotBeNull();
        stats.NetworkStats!.PacketsLost.Should().Be(0);
        stats.NetworkStats.PacketsCorrupted.Should().Be(0);
    }

    [Fact]
    public void FullPipeline_MultipleFrames_SequentialConsistency()
    {
        // Arrange - Pipeline for sequential frame processing
        var panelConfig = new PanelConfig
        {
            Rows = 64,
            Cols = 64,
            BitDepth = 16,
            TestPattern = TestPattern.FlatField,
            NoiseModel = NoiseModelType.None,
            NoiseStdDev = 0,
            DefectRate = 0,
            Seed = 42
        };

        var pipeline = new SimulatorPipelineBuilder()
            .Build(panelConfig);

        // Act - Process 3 frames sequentially
        var frame1 = pipeline.ProcessFrame();
        var frame2 = pipeline.ProcessFrame();
        var frame3 = pipeline.ProcessFrame();

        // Assert - Each frame should produce valid output
        frame1.Should().NotBeNull("first frame should produce output");
        frame2.Should().NotBeNull("second frame should produce output");
        frame3.Should().NotBeNull("third frame should produce output");

        // All frames should have correct dimensions
        frame1!.Width.Should().Be(64);
        frame1.Height.Should().Be(64);
        frame2!.Width.Should().Be(64);
        frame2.Height.Should().Be(64);
        frame3!.Width.Should().Be(64);
        frame3.Height.Should().Be(64);

        // Statistics should show 3 frames processed
        var stats = pipeline.GetStatistics();
        stats.FramesProcessed.Should().Be(3);
        stats.FramesCompleted.Should().Be(3);
        stats.FramesFailed.Should().Be(0);
        stats.UdpPacketsGenerated.Should().BeGreaterThan(0,
            "UDP packets should have been generated for all frames");
    }
}
