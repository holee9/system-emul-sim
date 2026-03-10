using YamlDotNet.Serialization;

namespace IntegrationRunner.Core.Models;

/// <summary>
/// Detector configuration loaded from detector_config.yaml.
/// REQ-SIM-002: All simulators shall be configurable via detector_config.yaml.
/// </summary>
public class DetectorConfig
{
    /// <summary>Panel configuration section</summary>
    [YamlMember(Alias = "panel")]
    public PanelConfig? Panel { get; set; }

    /// <summary>FPGA configuration section</summary>
    [YamlMember(Alias = "fpga")]
    public FpgaConfig? Fpga { get; set; }

    /// <summary>SoC configuration section</summary>
    [YamlMember(Alias = "soc")]
    public SocConfig? Soc { get; set; }

    /// <summary>Host configuration section</summary>
    [YamlMember(Alias = "host")]
    public HostConfig? Host { get; set; }

    /// <summary>Simulation configuration section</summary>
    [YamlMember(Alias = "simulation")]
    public SimulationConfig? Simulation { get; set; }
}

/// <summary>Panel detector configuration.</summary>
public class PanelConfig
{
    [YamlMember(Alias = "rows")]
    public int Rows { get; set; }

    [YamlMember(Alias = "cols")]
    public int Cols { get; set; }

    [YamlMember(Alias = "bit_depth")]
    public int BitDepth { get; set; }

    [YamlMember(Alias = "pixel_pitch_um")]
    public double PixelPitchUm { get; set; }
}

/// <summary>FPGA configuration.</summary>
public class FpgaConfig
{
    [YamlMember(Alias = "csi2_lanes")]
    public int Csi2Lanes { get; set; }

    [YamlMember(Alias = "csi2_data_rate_mbps")]
    public int Csi2DataRateMbps { get; set; }

    [YamlMember(Alias = "line_buffer_depth")]
    public int LineBufferDepth { get; set; }
}

/// <summary>SoC configuration.</summary>
public class SocConfig
{
    [YamlMember(Alias = "ethernet_port")]
    public int EthernetPort { get; set; }

    [YamlMember(Alias = "udp_port")]
    public int UdpPort { get; set; }

    [YamlMember(Alias = "tcp_port")]
    public int TcpPort { get; set; }

    [YamlMember(Alias = "frame_buffer_count")]
    public int FrameBufferCount { get; set; }
}

/// <summary>Host configuration.</summary>
public class HostConfig
{
    [YamlMember(Alias = "ip_address")]
    public string IpAddress { get; set; } = "127.0.0.1";

    [YamlMember(Alias = "packet_timeout_ms")]
    public int PacketTimeoutMs { get; set; }

    [YamlMember(Alias = "receive_threads")]
    public int ReceiveThreads { get; set; }
}

/// <summary>Simulation configuration.</summary>
public class SimulationConfig
{
    [YamlMember(Alias = "mode")]
    public string Mode { get; set; } = "fast";

    [YamlMember(Alias = "seed")]
    public int Seed { get; set; } = 42;

    [YamlMember(Alias = "test_pattern")]
    public string TestPattern { get; set; } = "counter";

    [YamlMember(Alias = "noise_stddev")]
    public double NoiseStdDev { get; set; }

    /// <summary>
    /// Maximum frames per scenario (0 = use scenario default).
    /// Used to limit frame count in unit tests for fast execution.
    /// </summary>
    [YamlMember(Alias = "max_frames")]
    public int MaxFrames { get; set; }
}
