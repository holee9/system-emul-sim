using FluentAssertions;
using Xunit;
using ConfigConverter.Models;
using ConfigConverter.Validators;

namespace ConfigConverter.Tests.Unit;

public class SchemaValidatorTests
{
    [Fact]
    public void Validate_ValidConfig_ReturnsSuccess()
    {
        // Arrange
        var config = new DetectorConfig
        {
            Panel = new PanelConfig { Rows = 2048, Cols = 2048, BitDepth = 16, PixelPitchUm = 150 },
            Fpga = new FpgaConfig
            {
                Timing = new TimingConfig(),
                LineBuffer = new LineBufferConfig(),
                DataInterface = new DataInterfaceConfig { Primary = "csi2", Csi2 = new Csi2Config() },
                Spi = new SpiConfig()
            },
            Controller = new ControllerConfig
            {
                Platform = "imx8mp",
                Ethernet = new EthernetConfig()
            },
            Host = new HostConfig
            {
                Storage = new StorageConfig(),
                Display = new DisplayConfig()
            }
        };
        var validator = new SchemaValidator();

        // Act
        var result = validator.Validate(config);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_MissingPanelRows_ReturnsError()
    {
        // Arrange
        var config = new DetectorConfig
        {
            Panel = new PanelConfig { Rows = 0 }, // Invalid: must be >= 256
            Fpga = new FpgaConfig(),
            Controller = new ControllerConfig(),
            Host = new HostConfig()
        };
        var validator = new SchemaValidator();

        // Act
        var result = validator.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("rows") && e.Contains("256"));
    }

    [Fact]
    public void Validate_InvalidBitDepth_ReturnsError()
    {
        // Arrange
        var config = new DetectorConfig
        {
            Panel = new PanelConfig { Rows = 2048, Cols = 2048, BitDepth = 12 }, // Invalid: must be 14 or 16
            Fpga = new FpgaConfig(),
            Controller = new ControllerConfig(),
            Host = new HostConfig()
        };
        var validator = new SchemaValidator();

        // Act
        var result = validator.Validate(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("bitDepth"));
    }

    [Fact]
    public void Validate_NullConfig_ThrowsArgumentNullException()
    {
        // Arrange
        var validator = new SchemaValidator();

        // Act
        Action act = () => validator.Validate(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
