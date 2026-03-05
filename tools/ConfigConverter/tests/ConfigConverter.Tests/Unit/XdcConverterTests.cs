using FluentAssertions;
using Xunit;
using ConfigConverter.Models;
using ConfigConverter.Converters;

namespace ConfigConverter.Tests.Unit;

public class XdcConverterTests
{
    private readonly DetectorConfig _validConfig;

    public XdcConverterTests()
    {
        _validConfig = new DetectorConfig
        {
            Panel = new PanelConfig { Rows = 2048, Cols = 2048, BitDepth = 16 },
            Fpga = new FpgaConfig
            {
                Timing = new TimingConfig { GateOnUs = 10.0, GateOffUs = 5.0 },
                DataInterface = new DataInterfaceConfig
                {
                    Primary = "csi2",
                    Csi2 = new Csi2Config
                    {
                        LaneCount = 4,
                        LaneSpeedMbps = 400,
                        DataType = "RAW16"
                    }
                },
                Spi = new SpiConfig { ClockHz = 50000000, Mode = 0 }
            }
        };
    }

    [Fact]
    public void Convert_ValidConfig_GeneratesXdcContent()
    {
        // Arrange
        var converter = new XdcConverter();

        // Act
        var result = converter.Convert(_validConfig);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("# Xilinx Design Constraints");
        result.Should().Contain("auto-generated");
    }

    [Fact]
    public void Convert_IncludesClockConstraints()
    {
        // Arrange
        var converter = new XdcConverter();

        // Act
        var result = converter.Convert(_validConfig);

        // Assert
        result.Should().Contain("create_clock");
        result.Should().Contain("-period");
        result.Should().Contain("clk_spi");
    }

    [Fact]
    public void Convert_CalculatesSpiClockPeriod()
    {
        // Arrange
        _validConfig.Fpga.Spi.ClockHz = 50000000; // 50 MHz = 20 ns period
        var converter = new XdcConverter();

        // Act
        var result = converter.Convert(_validConfig);

        // Assert
        result.Should().Contain("20.000"); // 20 ns period for 50 MHz
    }

    [Fact]
    public void Convert_IncludesCsi2ByteClock()
    {
        // Arrange
        var converter = new XdcConverter();

        // Act
        var result = converter.Convert(_validConfig);

        // Assert
        result.Should().Contain("csi2_byte_clk");
        result.Should().Contain("5.000"); // 400 Mbps/lane * 4 lanes / 8 = 200 MHz = 5 ns
    }

    [Fact]
    public void Convert_NullConfig_ThrowsArgumentNullException()
    {
        // Arrange
        var converter = new XdcConverter();

        // Act
        Action act = () => converter.Convert(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
