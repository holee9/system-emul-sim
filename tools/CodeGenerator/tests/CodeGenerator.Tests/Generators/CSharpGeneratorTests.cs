namespace CodeGenerator.Tests.Generators;

using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using CodeGenerator.Core.Generators;
using CodeGenerator.Core.Models;

/// <summary>
/// TDD tests for C# SDK class skeleton generation.
/// Implements AC-TOOLS-012 from SPEC-TOOLS-001.
/// </summary>
public class CSharpGeneratorTests
{
    private const string TestConfigYaml = """
        panel:
          rows: 2048
          cols: 2048
          pixel_pitch_um: 150
          bit_depth: 16

        fpga:
          timing:
            gate_on_us: 10.0
            gate_off_us: 5.0
            roic_settle_us: 1.0
            adc_conv_us: 2.0

          line_buffer:
            depth_lines: 2
            bram_width_bits: 16

          data_interface:
            primary: csi2
            csi2:
              lane_count: 4
              data_type: RAW16
              virtual_channel: 0
              lane_speed_mbps: 400
              line_blanking_clocks: 100
              frame_blanking_lines: 10

          spi:
            clock_hz: 50000000
            mode: 0
            word_size_bits: 32

          protection:
            timeout_ms: 100
            overexposure_threshold: 60000
            overflow_action: stop

        controller:
          platform: imx8mp
          ethernet:
            speed: 10gbe
            protocol: udp
            port: 8000
            mtu: 9000
            payload_size: 8192

          frame_buffer:
            count: 4
            allocation_mb: 128

          csi2_rx:
            interface_index: 0
            dma_burst_length: 256

        host:
          storage:
            format: tiff
            path: ./frames
            compression: none
            auto_save: false

          display:
            fps: 15
            color_map: gray
            window_scale: 1.0

          network:
            receive_buffer_mb: 64
            receive_threads: 2
            packet_timeout_ms: 1000
        """;

    [Fact]
    public async Task GenerateDetectorConfigAsync_ShouldCreateClassWithDefaults()
    {
        // Arrange
        var config = DetectorConfig.ParseFromYaml(TestConfigYaml);
        var generator = new CSharpGenerator();
        var outputPath = Path.Combine(Path.GetTempPath(), "DetectorConfig.g.cs");

        try
        {
            // Act
            await generator.GenerateDetectorConfigAsync(config, outputPath);

            // Assert
            var content = await File.ReadAllTextAsync(outputPath);
            content.Should().Contain("namespace SystemEmulSim.Sdk");
            content.Should().Contain("class DetectorConfig");
            content.Should().Contain("// AUTO-GENERATED - DO NOT EDIT");
            content.Should().Contain("DefaultRows");
            content.Should().Contain("DefaultCols");
            content.Should().Contain("DefaultBitDepth");
            content.Should().Contain("DefaultGateOnUs");
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public async Task GenerateFrameHeaderAsync_ShouldCreateStructWithCorrectLayout()
    {
        // Arrange
        var config = DetectorConfig.ParseFromYaml(TestConfigYaml);
        var generator = new CSharpGenerator();
        var outputPath = Path.Combine(Path.GetTempPath(), "FrameHeader.g.cs");

        try
        {
            // Act
            await generator.GenerateFrameHeaderAsync(config, outputPath);

            // Assert
            var content = await File.ReadAllTextAsync(outputPath);
            content.Should().Contain("namespace SystemEmulSim.Sdk");
            content.Should().Contain("StructLayout");
            content.Should().Contain("LayoutKind.Sequential");
            content.Should().Contain("public struct FrameHeader");
            content.Should().Contain("// AUTO-GENERATED - DO NOT EDIT");
            content.Should().Contain("public uint FrameNumber");
            content.Should().Contain("public ushort Rows");
            content.Should().Contain("public ushort Cols");
            content.Should().Contain("public ushort BitDepth");
            content.Should().Contain("public ulong TimestampUs");
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public async Task GenerateAllAsync_ShouldCreateAllCSharpFiles()
    {
        // Arrange
        var config = DetectorConfig.ParseFromYaml(TestConfigYaml);
        var generator = new CSharpGenerator();
        var outputDir = Path.Combine(Path.GetTempPath(), $"sdk_{Guid.NewGuid()}");

        try
        {
            // Act
            await generator.GenerateAllAsync(config, outputDir);

            // Assert
            Directory.Exists(outputDir).Should().BeTrue();
            File.Exists(Path.Combine(outputDir, "DetectorConfig.g.cs")).Should().BeTrue();
            File.Exists(Path.Combine(outputDir, "FrameHeader.g.cs")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, true);
            }
        }
    }
}
