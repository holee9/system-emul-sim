using YamlDotNet.Serialization;

namespace ParameterExtractor.Core.Models;

/// <summary>
/// Detector configuration model matching detector_config.yaml schema.
/// Used for YAML export with schema validation.
/// </summary>
public class DetectorConfig
{
    [YamlMember(Alias = "panel")]
    public PanelConfig? Panel { get; set; }

    [YamlMember(Alias = "fpga")]
    public FpgaConfig? Fpga { get; set; }

    [YamlMember(Alias = "controller")]
    public ControllerConfig? Controller { get; set; }

    [YamlMember(Alias = "host")]
    public HostConfig? Host { get; set; }
}

public class PanelConfig
{
    [YamlMember(Alias = "rows")]
    public int Rows { get; set; }

    [YamlMember(Alias = "cols")]
    public int Cols { get; set; }

    [YamlMember(Alias = "pixel_pitch_um")]
    public double PixelPitchUm { get; set; }

    [YamlMember(Alias = "bit_depth")]
    public int BitDepth { get; set; }
}

public class FpgaConfig
{
    [YamlMember(Alias = "timing")]
    public TimingConfig? Timing { get; set; }

    [YamlMember(Alias = "line_buffer")]
    public LineBufferConfig? LineBuffer { get; set; }

    [YamlMember(Alias = "data_interface")]
    public DataInterfaceConfig? DataInterface { get; set; }

    [YamlMember(Alias = "spi")]
    public SpiConfig? Spi { get; set; }

    [YamlMember(Alias = "protection")]
    public ProtectionConfig? Protection { get; set; }
}

public class TimingConfig
{
    [YamlMember(Alias = "gate_on_us")]
    public double GateOnUs { get; set; }

    [YamlMember(Alias = "gate_off_us")]
    public double GateOffUs { get; set; }

    [YamlMember(Alias = "roic_settle_us")]
    public double RoicSettleUs { get; set; }

    [YamlMember(Alias = "adc_conv_us")]
    public double AdcConvUs { get; set; }
}

public class LineBufferConfig
{
    [YamlMember(Alias = "depth_lines")]
    public int DepthLines { get; set; }

    [YamlMember(Alias = "bram_width_bits")]
    public int BramWidthBits { get; set; }
}

public class DataInterfaceConfig
{
    [YamlMember(Alias = "primary")]
    public string Primary { get; set; } = "csi2";

    [YamlMember(Alias = "csi2")]
    public Csi2Config? Csi2 { get; set; }
}

public class Csi2Config
{
    [YamlMember(Alias = "lane_count")]
    public int LaneCount { get; set; }

    [YamlMember(Alias = "data_type")]
    public string DataType { get; set; } = "RAW16";

    [YamlMember(Alias = "virtual_channel")]
    public int VirtualChannel { get; set; }

    [YamlMember(Alias = "lane_speed_mbps")]
    public int LaneSpeedMbps { get; set; }

    [YamlMember(Alias = "line_blanking_clocks")]
    public int LineBlankingClocks { get; set; }

    [YamlMember(Alias = "frame_blanking_lines")]
    public int FrameBlankingLines { get; set; }
}

public class SpiConfig
{
    [YamlMember(Alias = "clock_hz")]
    public long ClockHz { get; set; }

    [YamlMember(Alias = "mode")]
    public int Mode { get; set; }

    [YamlMember(Alias = "word_size_bits")]
    public int WordSizeBits { get; set; }
}

public class ProtectionConfig
{
    [YamlMember(Alias = "timeout_ms")]
    public double TimeoutMs { get; set; }

    [YamlMember(Alias = "overexposure_threshold")]
    public int OverexposureThreshold { get; set; }

    [YamlMember(Alias = "overflow_action")]
    public string OverflowAction { get; set; } = "stop";
}

public class ControllerConfig
{
    [YamlMember(Alias = "platform")]
    public string Platform { get; set; } = "imx8mp";

    [YamlMember(Alias = "ethernet")]
    public EthernetConfig? Ethernet { get; set; }

    [YamlMember(Alias = "frame_buffer")]
    public FrameBufferConfig? FrameBuffer { get; set; }

    [YamlMember(Alias = "csi2_rx")]
    public Csi2RxConfig? Csi2Rx { get; set; }
}

public class EthernetConfig
{
    [YamlMember(Alias = "speed")]
    public string Speed { get; set; } = "10gbe";

    [YamlMember(Alias = "protocol")]
    public string Protocol { get; set; } = "udp";

    [YamlMember(Alias = "port")]
    public int Port { get; set; }

    [YamlMember(Alias = "mtu")]
    public int Mtu { get; set; }

    [YamlMember(Alias = "payload_size")]
    public int PayloadSize { get; set; }
}

public class FrameBufferConfig
{
    [YamlMember(Alias = "count")]
    public int Count { get; set; }

    [YamlMember(Alias = "allocation_mb")]
    public int AllocationMb { get; set; }
}

public class Csi2RxConfig
{
    [YamlMember(Alias = "interface_index")]
    public int InterfaceIndex { get; set; }

    [YamlMember(Alias = "dma_burst_length")]
    public int DmaBurstLength { get; set; }
}

public class HostConfig
{
    [YamlMember(Alias = "storage")]
    public StorageConfig? Storage { get; set; }

    [YamlMember(Alias = "display")]
    public DisplayConfig? Display { get; set; }

    [YamlMember(Alias = "network")]
    public NetworkConfig? Network { get; set; }
}

public class StorageConfig
{
    [YamlMember(Alias = "format")]
    public string Format { get; set; } = "tiff";

    [YamlMember(Alias = "path")]
    public string Path { get; set; } = "./frames";

    [YamlMember(Alias = "compression")]
    public string Compression { get; set; } = "none";

    [YamlMember(Alias = "auto_save")]
    public bool AutoSave { get; set; }
}

public class DisplayConfig
{
    [YamlMember(Alias = "fps")]
    public int Fps { get; set; }

    [YamlMember(Alias = "color_map")]
    public string ColorMap { get; set; } = "gray";

    [YamlMember(Alias = "window_scale")]
    public double WindowScale { get; set; }
}

public class NetworkConfig
{
    [YamlMember(Alias = "receive_buffer_mb")]
    public int ReceiveBufferMb { get; set; }

    [YamlMember(Alias = "receive_threads")]
    public int ReceiveThreads { get; set; }

    [YamlMember(Alias = "packet_timeout_ms")]
    public int PacketTimeoutMs { get; set; }
}
