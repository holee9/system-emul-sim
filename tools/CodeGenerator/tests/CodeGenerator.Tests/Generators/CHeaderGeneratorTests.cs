namespace CodeGenerator.Tests.Generators;

using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using CodeGenerator.Core.Generators;
using CodeGenerator.Core.Models;

/// <summary>
/// TDD tests for C header file generation with FPGA register map.
/// Implements AC-TOOLS-009 from SPEC-TOOLS-001.
/// </summary>
public class CHeaderGeneratorTests
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
    public async Task GenerateFpgaRegistersAsync_ShouldCreateHeaderWithRegisterAddresses()
    {
        // Arrange
        var config = DetectorConfig.ParseFromYaml(TestConfigYaml);
        var generator = new CHeaderGenerator();
        var outputPath = Path.Combine(Path.GetTempPath(), "fpga_registers.h");

        try
        {
            // Act
            await generator.GenerateFpgaRegistersAsync(config, outputPath);

            // Assert
            var content = await File.ReadAllTextAsync(outputPath);
            content.Should().Contain("#ifndef FPGA_REGISTERS_H");
            content.Should().Contain("#define FPGA_REGISTERS_H");
            content.Should().Contain("// AUTO-GENERATED - DO NOT EDIT");
            content.Should().Contain("#define FPGA_REG_PANEL_ROWS");
            content.Should().Contain("#define FPGA_REG_PANEL_COLS");
            content.Should().Contain("#define FPGA_REG_GATE_ON_US");
            content.Should().Contain("#define FPGA_REG_GATE_OFF_US");
            content.Should().Contain("#endif // FPGA_REGISTERS_H");
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
    public async Task GenerateFpgaRegistersAsync_ShouldIncludeBitFieldsAndDocumentation()
    {
        // Arrange
        var config = DetectorConfig.ParseFromYaml(TestConfigYaml);
        var generator = new CHeaderGenerator();
        var outputPath = Path.Combine(Path.GetTempPath(), "fpga_registers.h");

        try
        {
            // Act
            await generator.GenerateFpgaRegistersAsync(config, outputPath);

            // Assert
            var content = await File.ReadAllTextAsync(outputPath);
            content.Should().Contain("/*");
            content.Should().Contain("Register map for FPGA-SoC SPI communication");
            content.Should().Contain("Panel configuration registers");
            content.Should().Contain("*/");
            content.Should().Contain("typedef struct");
            content.Should().Contain("fpga_register_map");
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
    public async Task GenerateAllAsync_ShouldCreateCompilableHeader()
    {
        // Arrange
        var config = DetectorConfig.ParseFromYaml(TestConfigYaml);
        var generator = new CHeaderGenerator();
        var outputDir = Path.Combine(Path.GetTempPath(), $"include_{Guid.NewGuid()}");

        try
        {
            // Act
            await generator.GenerateAllAsync(config, outputDir);

            // Assert
            Directory.Exists(outputDir).Should().BeTrue();
            var headerPath = Path.Combine(outputDir, "fpga_registers.h");
            File.Exists(headerPath).Should().BeTrue();

            var content = await File.ReadAllTextAsync(headerPath);
            content.Should().Contain("#ifndef FPGA_REGISTERS_H");
            content.Should().Contain("#include <stdint.h>");
            content.Should().Contain("typedef struct");
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
