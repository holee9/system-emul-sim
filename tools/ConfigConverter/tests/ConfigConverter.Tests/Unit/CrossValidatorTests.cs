using FluentAssertions;
using Xunit;
using ConfigConverter.Models;
using ConfigConverter.Validators;

namespace ConfigConverter.Tests.Unit;

public class CrossValidatorTests
{
    private readonly DetectorConfig _validConfig;

    public CrossValidatorTests()
    {
        _validConfig = new DetectorConfig
        {
            Panel = new PanelConfig { Rows = 2048, Cols = 2048, BitDepth = 16, PixelPitchUm = 150 },
            Fpga = new FpgaConfig
            {
                DataInterface = new DataInterfaceConfig
                {
                    Primary = "csi2",
                    Csi2 = new Csi2Config { LaneCount = 4, LaneSpeedMbps = 400, DataType = "RAW16" }
                }
            },
            Controller = new ControllerConfig
            {
                Ethernet = new EthernetConfig { Speed = "10gbe", PayloadSize = 8192, Mtu = 9000 }
            },
            Host = new HostConfig
            {
                Display = new DisplayConfig { Fps = 15 },
                Network = new NetworkConfig { ReceiveBufferMb = 64 }
            }
        };
    }

    [Fact]
    public void Validate_ValidConfig_PassesCrossValidation()
    {
        // Arrange
        var validator = new CrossValidator();

        // Act
        var result = validator.Validate(_validConfig);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_BandwidthExceeds80Percent_GeneratesWarning()
    {
        // Arrange
        _validConfig.Panel.Rows = 3072;
        _validConfig.Panel.Cols = 3072;
        _validConfig.Panel.BitDepth = 16;
        _validConfig.Host.Display.Fps = 30;
        _validConfig.Fpga.DataInterface.Csi2.LaneSpeedMbps = 400; // Total 1.6 Gbps capacity
        var validator = new CrossValidator();

        // Act
        var result = validator.Validate(_validConfig);

        // Assert
        // 3072x3072x16x30fps = ~4.5 Gbps >> 1.6 Gbps CSI-2 capacity
        result.IsValid.Should().BeFalse(); // Should fail validation
        result.Errors.Should().Contain(e => e.Contains("bandwidth") || e.Contains("exceeds"));
    }

    [Fact]
    public void Validate_Ethernet1GbeForHighRes_GeneratesError()
    {
        // Arrange
        _validConfig.Panel.Rows = 2048;
        _validConfig.Panel.Cols = 2048;
        _validConfig.Panel.BitDepth = 16;
        _validConfig.Host.Display.Fps = 15;
        _validConfig.Controller.Ethernet.Speed = "1gbe"; // Too slow for ~1.0 Gbps data rate
        var validator = new CrossValidator();

        // Act
        var result = validator.Validate(_validConfig);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("ethernet", StringComparison.OrdinalIgnoreCase) || e.Contains("insufficient", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_MtuSmallerThanPayload_GeneratesError()
    {
        // Arrange
        _validConfig.Controller.Ethernet.PayloadSize = 8192;
        _validConfig.Controller.Ethernet.Mtu = 1500; // Smaller than payload
        var validator = new CrossValidator();

        // Act
        var result = validator.Validate(_validConfig);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("MTU") || e.Contains("payload"));
    }

    [Fact]
    public void Validate_NullConfig_ThrowsArgumentNullException()
    {
        // Arrange
        var validator = new CrossValidator();

        // Act
        Action act = () => validator.Validate(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
