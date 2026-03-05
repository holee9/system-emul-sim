using FluentAssertions;
using Xunit;
using ConfigConverter.Models;
using ConfigConverter.Converters;
using System.Text.Json;

namespace ConfigConverter.Tests.Unit;

public class JsonConverterTests
{
    private readonly DetectorConfig _validConfig;

    public JsonConverterTests()
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
                Platform = "imx8mp",
                Ethernet = new EthernetConfig { Speed = "10gbe", Port = 8000, PayloadSize = 8192 }
            },
            Host = new HostConfig
            {
                Storage = new StorageConfig { Format = "tiff", Path = "./frames" },
                Display = new DisplayConfig { Fps = 15, ColorMap = "gray" },
                Network = new NetworkConfig { ReceiveBufferMb = 64, ReceiveThreads = 2 }
            }
        };
    }

    [Fact]
    public void Convert_ValidConfig_GeneratesJsonContent()
    {
        // Arrange
        var converter = new JsonConverter();

        // Act
        var result = converter.Convert(_validConfig);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var jsonDoc = JsonDocument.Parse(result);
        jsonDoc.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public void Convert_IncludesFrameSizeBytes_ComputedCorrectly()
    {
        // Arrange
        var converter = new JsonConverter();

        // Act
        var result = converter.Convert(_validConfig);
        var jsonDoc = JsonDocument.Parse(result);

        // Assert
        // 2048 * 2048 * 16 bits = 2048 * 2048 * 2 bytes = 8,388,608 bytes
        var frameSize = jsonDoc.RootElement.GetProperty("computed").GetProperty("frameSizeBytes").GetInt64();
        frameSize.Should().Be(2048L * 2048 * 2);
    }

    [Fact]
    public void Convert_IncludesPacketsPerFrame_ComputedCorrectly()
    {
        // Arrange
        var converter = new JsonConverter();

        // Act
        var result = converter.Convert(_validConfig);
        var jsonDoc = JsonDocument.Parse(result);

        // Assert
        // Frame size: 2048 * 2048 * 2 = 8,388,608 bytes
        // Payload size: 8192 bytes
        // Packets: 8,388,608 / 8192 = 1024
        var packetsPerFrame = jsonDoc.RootElement.GetProperty("computed").GetProperty("packetsPerFrame").GetInt32();
        packetsPerFrame.Should().Be(1024);
    }

    [Fact]
    public void Convert_NullConfig_ThrowsArgumentNullException()
    {
        // Arrange
        var converter = new JsonConverter();

        // Act
        Action act = () => converter.Convert(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
