namespace CodeGenerator.Tests.Generators;

using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using CodeGenerator.Core.Generators;
using CodeGenerator.Core.Models;

/// <summary>
/// TDD tests for SystemVerilog RTL skeleton generation.
/// Implements AC-TOOLS-010 and AC-TOOLS-002 from SPEC-TOOLS-001.
/// </summary>
public class SystemVerilogGeneratorTests
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
    public async Task GeneratePanelScanFsmAsync_ShouldCreateModuleWithParameterizedTiming()
    {
        // Arrange
        var config = DetectorConfig.ParseFromYaml(TestConfigYaml);
        var generator = new SystemVerilogGenerator();
        var outputPath = Path.Combine(Path.GetTempPath(), "panel_scan_fsm.sv");

        try
        {
            // Act
            await generator.GeneratePanelScanFsmAsync(config, outputPath);

            // Assert
            var content = await File.ReadAllTextAsync(outputPath);
            content.Should().Contain("module panel_scan_fsm");
            content.Should().Contain("parameter PANEL_ROWS");
            content.Should().Contain("parameter PANEL_COLS");
            content.Should().Contain("parameter GATE_ON_US");
            content.Should().Contain("parameter GATE_OFF_US");
            content.Should().Contain("parameter ROIC_SETTLE_US");
            content.Should().Contain("parameter ADC_CONV_US");
            content.Should().Contain("// AUTO-GENERATED - DO NOT EDIT");
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
    public async Task GenerateLineBufferAsync_ShouldCreateModuleWithCorrectBramSizing()
    {
        // Arrange
        var config = DetectorConfig.ParseFromYaml(TestConfigYaml);
        var generator = new SystemVerilogGenerator();
        var outputPath = Path.Combine(Path.GetTempPath(), "line_buffer.sv");

        try
        {
            // Act
            await generator.GenerateLineBufferAsync(config, outputPath);

            // Assert
            var content = await File.ReadAllTextAsync(outputPath);
            content.Should().Contain("module line_buffer");
            content.Should().Contain("parameter PANEL_COLS");
            content.Should().Contain("DEPTH_LINES");
            content.Should().Contain("DATA_WIDTH_BITS");
            content.Should().Contain("ADDR_WIDTH");
            content.Should().Contain("// AUTO-GENERATED - DO NOT EDIT");
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
    public async Task GenerateAllAsync_ShouldCreateAllRtlFiles()
    {
        // Arrange
        var config = DetectorConfig.ParseFromYaml(TestConfigYaml);
        var generator = new SystemVerilogGenerator();
        var outputDir = Path.Combine(Path.GetTempPath(), $"rtl_{Guid.NewGuid()}");

        try
        {
            // Act
            await generator.GenerateAllAsync(config, outputDir);

            // Assert
            Directory.Exists(outputDir).Should().BeTrue();
            File.Exists(Path.Combine(outputDir, "panel_scan_fsm.sv")).Should().BeTrue();
            File.Exists(Path.Combine(outputDir, "line_buffer.sv")).Should().BeTrue();
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
