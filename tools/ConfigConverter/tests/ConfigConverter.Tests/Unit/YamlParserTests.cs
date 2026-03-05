using FluentAssertions;
using Xunit;
using ConfigConverter.Models;
using ConfigConverter.Services;

namespace ConfigConverter.Tests.Unit;

public class YamlParserTests
{
    [Fact]
    public void Parse_ValidYaml_ReturnsDetectorConfig()
    {
        // Arrange
        var yaml = @"
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

  spi:
    clock_hz: 50000000
    mode: 0

controller:
  platform: imx8mp
  ethernet:
    speed: 10gbe
    protocol: udp
    port: 8000

host:
  storage:
    format: tiff
    path: ./frames
  display:
    fps: 15
    color_map: gray
";
        var parser = new YamlParser();

        // Act
        var result = parser.Parse(yaml);

        // Assert
        result.Should().NotBeNull();
        result.Panel.Rows.Should().Be(2048);
        result.Panel.Cols.Should().Be(2048);
        result.Panel.BitDepth.Should().Be(16);
        result.Fpga.DataInterface.Csi2.LaneCount.Should().Be(4);
        result.Fpga.DataInterface.Csi2.LaneSpeedMbps.Should().Be(400);
        result.Controller.Platform.Should().Be("imx8mp");
    }

    [Fact]
    public void Parse_InvalidYaml_ThrowsException()
    {
        // Arrange
        var invalidYaml = "panel: [unclosed";
        var parser = new YamlParser();

        // Act
        Action act = () => parser.Parse(invalidYaml);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Failed to parse YAML*");
    }

    [Fact]
    public void Parse_EmptyYaml_ThrowsException()
    {
        // Arrange
        var emptyYaml = "";
        var parser = new YamlParser();

        // Act
        Action act = () => parser.Parse(emptyYaml);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*YAML content cannot be empty*");
    }
}
