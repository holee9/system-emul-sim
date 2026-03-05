namespace CodeGenerator.Core.Models;

using System;
using YamlDotNet.Serialization;

/// <summary>
/// Detector configuration model parsed from detector_config.yaml.
/// Single source of truth for all target-specific code generation.
/// SPEC-TOOLS-001 REQ-TOOLS-010~013
/// </summary>
public class DetectorConfig
{
    /// <summary>
    /// Panel physical characteristics.
    /// </summary>
    [YamlMember(Alias = "panel")]
    public PanelConfig Panel { get; set; } = new();

    /// <summary>
    /// FPGA hardware configuration.
    /// </summary>
    [YamlMember(Alias = "fpga")]
    public FpgaConfig Fpga { get; set; } = new();

    /// <summary>
    /// SoC controller configuration.
    /// </summary>
    [YamlMember(Alias = "controller")]
    public ControllerConfig Controller { get; set; } = new();

    /// <summary>
    /// Host SDK configuration.
    /// </summary>
    [YamlMember(Alias = "host")]
    public HostConfig Host { get; set; } = new();

    /// <summary>
    /// Parse detector configuration from YAML string.
    /// </summary>
    public static DetectorConfig ParseFromYaml(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build();

        return deserializer.Deserialize<DetectorConfig>(yaml)
            ?? throw new InvalidOperationException("Failed to parse detector configuration YAML.");
    }

    /// <summary>
    /// Parse detector configuration from YAML file.
    /// </summary>
    public static async Task<DetectorConfig> LoadFromFileAsync(string filePath)
    {
        var yaml = await File.ReadAllTextAsync(filePath);
        return ParseFromYaml(yaml);
    }
}

/// <summary>
/// Panel physical characteristics configuration.
/// </summary>
public class PanelConfig
{
    /// <summary>
    /// Number of pixel rows (height). Range: 256-4096.
    /// </summary>
    [YamlMember(Alias = "rows")]
    public int Rows { get; set; } = 2048;

    /// <summary>
    /// Number of pixel columns (width). Range: 256-4096.
    /// </summary>
    [YamlMember(Alias = "cols")]
    public int Cols { get; set; } = 2048;

    /// <summary>
    /// Pixel pitch in micrometers. Range: 50-500.
    /// </summary>
    [YamlMember(Alias = "pixel_pitch_um")]
    public double PixelPitchUm { get; set; } = 150;

    /// <summary>
    /// ADC bit depth per pixel. 14 or 16.
    /// </summary>
    [YamlMember(Alias = "bit_depth")]
    public int BitDepth { get; set; } = 16;
}

/// <summary>
/// FPGA hardware configuration.
/// </summary>
public class FpgaConfig
{
    /// <summary>
    /// Panel scan timing parameters.
    /// </summary>
    [YamlMember(Alias = "timing")]
    public TimingConfig Timing { get; set; } = new();

    /// <summary>
    /// Ping-pong BRAM line buffer configuration.
    /// </summary>
    [YamlMember(Alias = "line_buffer")]
    public LineBufferConfig LineBuffer { get; set; } = new();

    /// <summary>
    /// High-speed data interface configuration.
    /// </summary>
    [YamlMember(Alias = "data_interface")]
    public DataInterfaceConfig DataInterface { get; set; } = new();

    /// <summary>
    /// SPI slave interface configuration.
    /// </summary>
    [YamlMember(Alias = "spi")]
    public SpiConfig Spi { get; set; } = new();

    /// <summary>
    /// Protection logic configuration.
    /// </summary>
    [YamlMember(Alias = "protection")]
    public ProtectionConfig? Protection { get; set; }
}

/// <summary>
/// Panel scan timing parameters.
/// </summary>
public class TimingConfig
{
    /// <summary>
    /// Gate-ON duration in microseconds for X-ray exposure.
    /// </summary>
    [YamlMember(Alias = "gate_on_us")]
    public double GateOnUs { get; set; } = 10.0;

    /// <summary>
    /// Gate-OFF duration in microseconds between lines.
    /// </summary>
    [YamlMember(Alias = "gate_off_us")]
    public double GateOffUs { get; set; } = 5.0;

    /// <summary>
    /// ROIC settling time in microseconds after gate transition.
    /// </summary>
    [YamlMember(Alias = "roic_settle_us")]
    public double RoicSettleUs { get; set; } = 1.0;

    /// <summary>
    /// ADC conversion time in microseconds per line.
    /// </summary>
    [YamlMember(Alias = "adc_conv_us")]
    public double AdcConvUs { get; set; } = 2.0;
}

/// <summary>
/// Ping-pong BRAM line buffer configuration.
/// </summary>
public class LineBufferConfig
{
    /// <summary>
    /// Number of line buffers. 2 = Ping-Pong.
    /// </summary>
    [YamlMember(Alias = "depth_lines")]
    public int DepthLines { get; set; } = 2;

    /// <summary>
    /// BRAM data width in bits.
    /// </summary>
    [YamlMember(Alias = "bram_width_bits")]
    public int BramWidthBits { get; set; } = 16;
}

/// <summary>
/// High-speed data interface configuration.
/// </summary>
public class DataInterfaceConfig
{
    /// <summary>
    /// Primary data interface. Always "csi2" for Artix-7 35T.
    /// </summary>
    [YamlMember(Alias = "primary")]
    public string Primary { get; set; } = "csi2";

    /// <summary>
    /// CSI-2 MIPI D-PHY configuration.
    /// </summary>
    [YamlMember(Alias = "csi2")]
    public Csi2Config Csi2 { get; set; } = new();
}

/// <summary>
/// CSI-2 MIPI D-PHY configuration.
/// </summary>
public class Csi2Config
{
    /// <summary>
    /// Number of D-PHY data lanes. 1, 2, or 4.
    /// </summary>
    [YamlMember(Alias = "lane_count")]
    public int LaneCount { get; set; } = 4;

    /// <summary>
    /// CSI-2 data type. RAW14 or RAW16.
    /// </summary>
    [YamlMember(Alias = "data_type")]
    public string DataType { get; set; } = "RAW16";

    /// <summary>
    /// CSI-2 virtual channel. 0-3.
    /// </summary>
    [YamlMember(Alias = "virtual_channel")]
    public int VirtualChannel { get; set; } = 0;

    /// <summary>
    /// Per-lane speed in Mbps.
    /// </summary>
    [YamlMember(Alias = "lane_speed_mbps")]
    public int LaneSpeedMbps { get; set; } = 400;

    /// <summary>
    /// Line blanking period in pixel clocks.
    /// </summary>
    [YamlMember(Alias = "line_blanking_clocks")]
    public int LineBlankingClocks { get; set; } = 100;

    /// <summary>
    /// Frame blanking period in line times.
    /// </summary>
    [YamlMember(Alias = "frame_blanking_lines")]
    public int FrameBlankingLines { get; set; } = 10;
}

/// <summary>
/// SPI slave interface configuration.
/// </summary>
public class SpiConfig
{
    /// <summary>
    /// SPI clock frequency in Hz.
    /// </summary>
    [YamlMember(Alias = "clock_hz")]
    public int ClockHz { get; set; } = 50000000;

    /// <summary>
    /// SPI mode (CPOL, CPHA). 0-3.
    /// </summary>
    [YamlMember(Alias = "mode")]
    public int Mode { get; set; } = 0;

    /// <summary>
    /// SPI word size in bits.
    /// </summary>
    [YamlMember(Alias = "word_size_bits")]
    public int WordSizeBits { get; set; } = 32;
}

/// <summary>
/// Protection logic configuration.
/// </summary>
public class ProtectionConfig
{
    /// <summary>
    /// Watchdog timeout in milliseconds.
    /// </summary>
    [YamlMember(Alias = "timeout_ms")]
    public double TimeoutMs { get; set; } = 100;

    /// <summary>
    /// Pixel value threshold for overexposure detection.
    /// </summary>
    [YamlMember(Alias = "overexposure_threshold")]
    public int OverexposureThreshold { get; set; } = 60000;

    /// <summary>
    /// Action when line buffer overflows.
    /// </summary>
    [YamlMember(Alias = "overflow_action")]
    public string OverflowAction { get; set; } = "stop";
}

/// <summary>
/// SoC controller configuration.
/// </summary>
public class ControllerConfig
{
    /// <summary>
    /// SoC platform identifier.
    /// </summary>
    [YamlMember(Alias = "platform")]
    public string Platform { get; set; } = "imx8mp";

    /// <summary>
    /// Ethernet streaming configuration.
    /// </summary>
    [YamlMember(Alias = "ethernet")]
    public EthernetConfig Ethernet { get; set; } = new();

    /// <summary>
    /// DDR4 frame buffer allocation.
    /// </summary>
    [YamlMember(Alias = "frame_buffer")]
    public FrameBufferConfig? FrameBuffer { get; set; }

    /// <summary>
    /// CSI-2 receiver configuration.
    /// </summary>
    [YamlMember(Alias = "csi2_rx")]
    public Csi2RxConfig? Csi2Rx { get; set; }
}

/// <summary>
/// Ethernet streaming configuration.
/// </summary>
public class EthernetConfig
{
    /// <summary>
    /// Link speed. 1gbe or 10gbe.
    /// </summary>
    [YamlMember(Alias = "speed")]
    public string Speed { get; set; } = "10gbe";

    /// <summary>
    /// Transport protocol. udp or tcp.
    /// </summary>
    [YamlMember(Alias = "protocol")]
    public string Protocol { get; set; } = "udp";

    /// <summary>
    /// UDP/TCP port number.
    /// </summary>
    [YamlMember(Alias = "port")]
    public int Port { get; set; } = 8000;

    /// <summary>
    /// Maximum Transmission Unit in bytes.
    /// </summary>
    [YamlMember(Alias = "mtu")]
    public int Mtu { get; set; } = 9000;

    /// <summary>
    /// UDP payload size in bytes per packet.
    /// </summary>
    [YamlMember(Alias = "payload_size")]
    public int PayloadSize { get; set; } = 8192;
}

/// <summary>
/// DDR4 frame buffer allocation.
/// </summary>
public class FrameBufferConfig
{
    /// <summary>
    /// Number of frame buffers.
    /// </summary>
    [YamlMember(Alias = "count")]
    public int Count { get; set; } = 4;

    /// <summary>
    /// Total DDR4 allocation in MB.
    /// </summary>
    [YamlMember(Alias = "allocation_mb")]
    public int AllocationMb { get; set; } = 128;
}

/// <summary>
/// CSI-2 receiver configuration.
/// </summary>
public class Csi2RxConfig
{
    /// <summary>
    /// CSI-2 interface index on SoC.
    /// </summary>
    [YamlMember(Alias = "interface_index")]
    public int InterfaceIndex { get; set; } = 0;

    /// <summary>
    /// DMA burst length in bytes.
    /// </summary>
    [YamlMember(Alias = "dma_burst_length")]
    public int DmaBurstLength { get; set; } = 256;
}

/// <summary>
/// Host SDK configuration.
/// </summary>
public class HostConfig
{
    /// <summary>
    /// Frame storage configuration.
    /// </summary>
    [YamlMember(Alias = "storage")]
    public StorageConfig Storage { get; set; } = new();

    /// <summary>
    /// Real-time display configuration.
    /// </summary>
    [YamlMember(Alias = "display")]
    public DisplayConfig Display { get; set; } = new();

    /// <summary>
    /// Network receive configuration.
    /// </summary>
    [YamlMember(Alias = "network")]
    public NetworkConfig? Network { get; set; }
}

/// <summary>
/// Frame storage configuration.
/// </summary>
public class StorageConfig
{
    /// <summary>
    /// Primary storage format.
    /// </summary>
    [YamlMember(Alias = "format")]
    public string Format { get; set; } = "tiff";

    /// <summary>
    /// Directory path for frame storage.
    /// </summary>
    [YamlMember(Alias = "path")]
    public string Path { get; set; } = "./frames";

    /// <summary>
    /// TIFF compression method.
    /// </summary>
    [YamlMember(Alias = "compression")]
    public string Compression { get; set; } = "none";

    /// <summary>
    /// Automatically save each captured frame.
    /// </summary>
    [YamlMember(Alias = "auto_save")]
    public bool AutoSave { get; set; } = false;
}

/// <summary>
/// Real-time display configuration.
/// </summary>
public class DisplayConfig
{
    /// <summary>
    /// Display refresh rate in fps.
    /// </summary>
    [YamlMember(Alias = "fps")]
    public int Fps { get; set; } = 15;

    /// <summary>
    /// Color mapping for 16-bit display.
    /// </summary>
    [YamlMember(Alias = "color_map")]
    public string ColorMap { get; set; } = "gray";

    /// <summary>
    /// Display window scale factor.
    /// </summary>
    [YamlMember(Alias = "window_scale")]
    public double WindowScale { get; set; } = 1.0;
}

/// <summary>
/// Network receive configuration.
/// </summary>
public class NetworkConfig
{
    /// <summary>
    /// UDP receive buffer size in MB.
    /// </summary>
    [YamlMember(Alias = "receive_buffer_mb")]
    public int ReceiveBufferMb { get; set; } = 64;

    /// <summary>
    /// Number of receive threads.
    /// </summary>
    [YamlMember(Alias = "receive_threads")]
    public int ReceiveThreads { get; set; } = 2;

    /// <summary>
    /// Timeout in ms for missing packets.
    /// </summary>
    [YamlMember(Alias = "packet_timeout_ms")]
    public int PacketTimeoutMs { get; set; } = 1000;
}
