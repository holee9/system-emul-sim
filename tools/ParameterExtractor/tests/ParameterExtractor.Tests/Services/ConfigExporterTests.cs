using FluentAssertions;
using ParameterExtractor.Core.Models;
using ParameterExtractor.Core.Services;
using Xunit;
using Xunit.Abstractions;
using System.IO;

namespace ParameterExtractor.Tests.Services;

/// <summary>
/// Unit tests for ConfigExporter following TDD methodology.
/// AC-TOOLS-004: Schema conformance for detector_config.yaml export.
/// </summary>
public class ConfigExporterTests
{
    private readonly ConfigExporter _sut = new();
    private readonly ITestOutputHelper _output;

    public ConfigExporterTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string GetSchemaPath()
    {
        var basePath = AppDomain.CurrentDomain.BaseDirectory;

        // Try several possible paths
        var candidates = new[]
        {
            Path.Combine(basePath, "..", "..", "..", "..", "..", "config", "schema", "detector-config-schema.json"),
            Path.Combine(basePath, "..", "..", "..", "..", "config", "schema", "detector-config-schema.json"),
            Path.Combine(basePath, "..", "..", "config", "schema", "detector-config-schema.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "config", "schema", "detector-config-schema.json")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);
        }

        return string.Empty;
    }

    [Fact]
    public async Task ExportAsync_ShouldCreateYamlFile_WhenGivenValidParameters()
    {
        // Arrange
        var parameters = new List<ParameterInfo>
        {
            new() { Name = "Rows", Value = "2048", Category = "panel" },
            new() { Name = "Cols", Value = "2048", Category = "panel" },
            new() { Name = "Pixel Pitch", Value = "150", Unit = "um", Category = "panel" },
            new() { Name = "Bit Depth", Value = "16", Category = "panel" }
        };

        var outputFile = Path.GetTempFileName() + ".yaml";

        try
        {
            // Act
            var result = await _sut.ExportAsync(parameters, outputFile);

            // Assert
            result.IsSuccessful.Should().BeTrue();
            result.OutputPath.Should().Be(outputFile);
            File.Exists(outputFile).Should().BeTrue();

            var yaml = await File.ReadAllTextAsync(outputFile);
            yaml.Should().Contain("rows: 2048");
            yaml.Should().Contain("cols: 2048");
            yaml.Should().Contain("pixel_pitch_um: 150");
            yaml.Should().Contain("bit_depth: 16");
        }
        finally
        {
            if (File.Exists(outputFile))
                File.Delete(outputFile);
        }
    }

    [Fact]
    public async Task ExportAsync_ShouldCreateOutputDirectory_WhenNotExists()
    {
        // Arrange
        var parameters = new List<ParameterInfo>
        {
            new() { Name = "Rows", Value = "1024", Category = "panel" }
        };

        var tempDir = Path.Combine(Path.GetTempPath(), "test_export_" + Guid.NewGuid());
        var outputFile = Path.Combine(tempDir, "config.yaml");

        try
        {
            // Act
            var result = await _sut.ExportAsync(parameters, outputFile);

            // Assert
            result.IsSuccessful.Should().BeTrue();
            Directory.Exists(tempDir).Should().BeTrue();
            File.Exists(outputFile).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ExportAsync_ShouldIncludeAllTopLevelSections()
    {
        // Arrange
        var parameters = new List<ParameterInfo>
        {
            new() { Name = "Rows", Value = "2048", Category = "panel" },
            new() { Name = "Gate ON", Value = "10", Category = "fpga.timing" },
            new() { Name = "Port", Value = "8000", Category = "controller.ethernet" },
            new() { Name = "Format", Value = "tiff", Category = "host.storage" }
        };

        var outputFile = Path.GetTempFileName() + ".yaml";

        try
        {
            // Act
            var result = await _sut.ExportAsync(parameters, outputFile);

            // Assert
            result.IsSuccessful.Should().BeTrue();
            var yaml = await File.ReadAllTextAsync(outputFile);
            yaml.Should().Contain("panel:");
            yaml.Should().Contain("fpga:");
            yaml.Should().Contain("controller:");
            yaml.Should().Contain("host:");
        }
        finally
        {
            if (File.Exists(outputFile))
                File.Delete(outputFile);
        }
    }

    [Fact]
    public async Task ExportAsync_ShouldUseUnderscoreNamingConvention()
    {
        // Arrange
        var parameters = new List<ParameterInfo>
        {
            new() { Name = "Pixel Pitch", Value = "150", Unit = "um", Category = "panel" }
        };

        var outputFile = Path.GetTempFileName() + ".yaml";

        try
        {
            // Act
            await _sut.ExportAsync(parameters, outputFile);
            var yaml = await File.ReadAllTextAsync(outputFile);

            // Assert
            yaml.Should().Contain("pixel_pitch_um:");
            yaml.Should().NotContain("PixelPitch");
        }
        finally
        {
            if (File.Exists(outputFile))
                File.Delete(outputFile);
        }
    }

    [Fact]
    public async Task ExportAsync_ShouldMapParametersByCategory()
    {
        // Arrange
        var parameters = new List<ParameterInfo>
        {
            new() { Name = "Gate ON", Value = "10.5", Category = "fpga.timing" },
            new() { Name = "Gate OFF", Value = "5.0", Category = "fpga.timing" },
            new() { Name = "ROIC Settle", Value = "1.0", Category = "fpga.timing" },
            new() { Name = "ADC Conv", Value = "2.0", Category = "fpga.timing" }
        };

        var outputFile = Path.GetTempFileName() + ".yaml";

        try
        {
            // Act
            await _sut.ExportAsync(parameters, outputFile);
            var yaml = await File.ReadAllTextAsync(outputFile);

            // Assert
            yaml.Should().Contain("timing:");
            yaml.Should().Contain("gate_on_us: 10.5");
            yaml.Should().Contain("gate_off_us: 5");
            yaml.Should().Contain("roic_settle_us: 1");
            yaml.Should().Contain("adc_conv_us: 2");
        }
        finally
        {
            if (File.Exists(outputFile))
                File.Delete(outputFile);
        }
    }

    [Fact]
    public async Task ExportAsync_ShouldMapCsi2Parameters()
    {
        // Arrange
        var parameters = new List<ParameterInfo>
        {
            new() { Name = "CSI Lane Count", Value = "4", Category = "fpga.data_interface.csi2" },
            new() { Name = "Lane Speed Mbps", Value = "400", Unit = "Mbps", Category = "fpga.data_interface.csi2" }
        };

        var outputFile = Path.GetTempFileName() + ".yaml";

        try
        {
            // Act
            await _sut.ExportAsync(parameters, outputFile);
            var yaml = await File.ReadAllTextAsync(outputFile);

            // Assert
            yaml.Should().Contain("lane_count: 4");
            yaml.Should().Contain("lane_speed_mbps: 400");
        }
        finally
        {
            if (File.Exists(outputFile))
                File.Delete(outputFile);
        }
    }

    [Fact]
    public async Task ExportAsync_ShouldMapEthernetParameters()
    {
        // Arrange
        var parameters = new List<ParameterInfo>
        {
            new() { Name = "Port", Value = "8000", Category = "controller.ethernet" },
            new() { Name = "MTU", Value = "9000", Category = "controller.ethernet" }
        };

        var outputFile = Path.GetTempFileName() + ".yaml";

        try
        {
            // Act
            await _sut.ExportAsync(parameters, outputFile);
            var yaml = await File.ReadAllTextAsync(outputFile);

            // Assert
            yaml.Should().Contain("port: 8000");
            yaml.Should().Contain("mtu: 9000");
        }
        finally
        {
            if (File.Exists(outputFile))
                File.Delete(outputFile);
        }
    }

    [Fact]
    public async Task ValidateSchemaAsync_ShouldReturnValid_ForConfigWithAllRequiredFields()
    {
        // Arrange
        var config = new DetectorConfig
        {
            Panel = new PanelConfig
            {
                Rows = 2048,
                Cols = 2048,
                PixelPitchUm = 150,
                BitDepth = 16
            },
            Fpga = new FpgaConfig
            {
                Timing = new TimingConfig
                {
                    GateOnUs = 10,
                    GateOffUs = 5,
                    RoicSettleUs = 1,
                    AdcConvUs = 2
                },
                LineBuffer = new LineBufferConfig
                {
                    DepthLines = 2,
                    BramWidthBits = 16
                },
                DataInterface = new DataInterfaceConfig
                {
                    Primary = "csi2",
                    Csi2 = new Csi2Config
                    {
                        LaneCount = 4,
                        DataType = "RAW16",
                        VirtualChannel = 0,
                        LaneSpeedMbps = 400
                    }
                },
                Spi = new SpiConfig
                {
                    ClockHz = 50000000,
                    Mode = 0,
                    WordSizeBits = 32
                }
            },
            Controller = new ControllerConfig
            {
                Platform = "imx8mp",
                Ethernet = new EthernetConfig
                {
                    Speed = "10gbe",
                    Protocol = "udp",
                    Port = 8000,
                    Mtu = 9000,
                    PayloadSize = 8192
                },
                FrameBuffer = new FrameBufferConfig
                {
                    Count = 4,
                    AllocationMb = 128
                }
            },
            Host = new HostConfig
            {
                Storage = new StorageConfig
                {
                    Format = "tiff",
                    Path = "./frames"
                },
                Display = new DisplayConfig
                {
                    Fps = 15,
                    ColorMap = "gray"
                }
            }
        };

        var schemaPath = GetSchemaPath();

        if (string.IsNullOrEmpty(schemaPath))
        {
            _output.WriteLine("Schema not found, skipping test");
            return;
        }

        // Act
        var result = await _sut.ValidateSchemaAsync(config, schemaPath);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ExportAsync_ShouldReturnErrors_WhenSchemaValidationFails()
    {
        // Arrange
        var parameters = new List<ParameterInfo>
        {
            // Missing required panel.rows
            new() { Name = "Pixel Pitch", Value = "150", Category = "panel" }
        };

        var outputFile = Path.GetTempFileName() + ".yaml";
        var schemaPath = GetSchemaPath();

        if (string.IsNullOrEmpty(schemaPath))
        {
            _output.WriteLine("Schema not found, skipping test");
            return;
        }

        try
        {
            // Act
            var result = await _sut.ExportAsync(parameters, outputFile, schemaPath);

            // Assert
            result.IsSuccessful.Should().BeFalse();
            result.ValidationErrors.Should().NotBeEmpty();
        }
        finally
        {
            if (File.Exists(outputFile))
                File.Delete(outputFile);
        }
    }

    [Fact]
    public void DetectorConfig_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var config = new DetectorConfig
        {
            Panel = new PanelConfig(),
            Fpga = new FpgaConfig
            {
                DataInterface = new DataInterfaceConfig()
            },
            Controller = new ControllerConfig
            {
                Ethernet = new EthernetConfig(),
                FrameBuffer = new FrameBufferConfig()
            },
            Host = new HostConfig
            {
                Storage = new StorageConfig(),
                Display = new DisplayConfig(),
                Network = new NetworkConfig()
            }
        };

        // Assert
        config.Fpga!.DataInterface!.Primary.Should().Be("csi2");
        config.Controller!.Platform.Should().Be("imx8mp");
        config.Host!.Storage!.Format.Should().Be("tiff");
    }
}
