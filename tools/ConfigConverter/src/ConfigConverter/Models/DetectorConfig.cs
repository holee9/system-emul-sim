namespace ConfigConverter.Models;

/// <summary>
/// X-ray Detector Panel Configuration model.
/// Single source of truth for all target-specific configurations.
/// </summary>
public class DetectorConfig
{
    /// <summary>
    /// X-ray detector panel physical characteristics
    /// </summary>
    public PanelConfig Panel { get; set; } = new();

    /// <summary>
    /// FPGA hardware configuration (Xilinx Artix-7 XC7A35T-FGG484)
    /// </summary>
    public FpgaConfig Fpga { get; set; } = new();

    /// <summary>
    /// SoC controller configuration (NXP i.MX8M Plus recommended)
    /// </summary>
    public ControllerConfig Controller { get; set; } = new();

    /// <summary>
    /// Host PC SDK and application configuration
    /// </summary>
    public HostConfig Host { get; set; } = new();
}

/// <summary>
/// X-ray detector panel physical characteristics
/// </summary>
public class PanelConfig
{
    /// <summary>
    /// Number of pixel rows (height). Min tier: 1024, Target: 2048, Max: 3072
    /// </summary>
    public int Rows { get; set; } = 2048;

    /// <summary>
    /// Number of pixel columns (width). Min tier: 1024, Target: 2048, Max: 3072
    /// </summary>
    public int Cols { get; set; } = 2048;

    /// <summary>
    /// Pixel pitch in micrometers. Typical medical imaging: 100-200 um
    /// </summary>
    public double PixelPitchUm { get; set; } = 150;

    /// <summary>
    /// ADC bit depth per pixel. 14-bit for minimum tier, 16-bit for target/max tiers
    /// </summary>
    public int BitDepth { get; set; } = 16;
}

/// <summary>
/// FPGA hardware configuration
/// </summary>
public class FpgaConfig
{
    /// <summary>
    /// Panel scan timing parameters (microseconds)
    /// </summary>
    public TimingConfig Timing { get; set; } = new();

    /// <summary>
    /// Ping-Pong BRAM line buffer configuration
    /// </summary>
    public LineBufferConfig LineBuffer { get; set; } = new();

    /// <summary>
    /// High-speed data interface from FPGA to SoC
    /// </summary>
    public DataInterfaceConfig DataInterface { get; set; } = new();

    /// <summary>
    /// SPI slave interface configuration for FPGA control
    /// </summary>
    public SpiConfig Spi { get; set; } = new();

    /// <summary>
    /// FPGA protection logic configuration
    /// </summary>
    public ProtectionConfig? Protection { get; set; }
}

/// <summary>
/// Panel scan timing parameters (microseconds)
/// </summary>
public class TimingConfig
{
    /// <summary>
    /// Gate-ON duration in microseconds for X-ray exposure
    /// </summary>
    public double GateOnUs { get; set; } = 10.0;

    /// <summary>
    /// Gate-OFF duration in microseconds between lines
    /// </summary>
    public double GateOffUs { get; set; } = 5.0;

    /// <summary>
    /// ROIC settling time in microseconds after gate transition
    /// </summary>
    public double RoicSettleUs { get; set; } = 1.0;

    /// <summary>
    /// ADC conversion time in microseconds per line
    /// </summary>
    public double AdcConvUs { get; set; } = 2.0;
}

/// <summary>
/// Ping-Pong BRAM line buffer configuration
/// </summary>
public class LineBufferConfig
{
    /// <summary>
    /// Number of line buffers (2 = Ping-Pong). Must be >= 2 for continuous streaming
    /// </summary>
    public int DepthLines { get; set; } = 2;

    /// <summary>
    /// BRAM data width in bits. Matches pixel bit depth for RAW16
    /// </summary>
    public int BramWidthBits { get; set; } = 16;
}

/// <summary>
/// High-speed data interface from FPGA to SoC
/// </summary>
public class DataInterfaceConfig
{
    /// <summary>
    /// Primary data interface. Fixed to CSI-2 (USB 3.x is not feasible on Artix-7 35T)
    /// </summary>
    public string Primary { get; set; } = "csi2";

    /// <summary>
    /// CSI-2 MIPI D-PHY configuration
    /// </summary>
    public Csi2Config Csi2 { get; set; } = new();
}

/// <summary>
/// CSI-2 MIPI D-PHY configuration
/// </summary>
public class Csi2Config
{
    /// <summary>
    /// Number of D-PHY data lanes. 4-lane provides ~4-5 Gbps aggregate
    /// </summary>
    public int LaneCount { get; set; } = 4;

    /// <summary>
    /// CSI-2 data type. RAW14 (0x2D) or RAW16 (0x2C)
    /// </summary>
    public string DataType { get; set; } = "RAW16";

    /// <summary>
    /// CSI-2 virtual channel (VC0-VC3). VC0 for single-panel systems
    /// </summary>
    public int VirtualChannel { get; set; } = 0;

    /// <summary>
    /// Per-lane speed in Mbps. Artix-7 OSERDES limit: 1000-1250 Mbps
    /// </summary>
    public int LaneSpeedMbps { get; set; } = 400;

    /// <summary>
    /// Line blanking period in pixel clocks between CSI-2 line packets
    /// </summary>
    public int LineBlankingClocks { get; set; } = 100;

    /// <summary>
    /// Frame blanking period in line times between CSI-2 frames
    /// </summary>
    public int FrameBlankingLines { get; set; } = 10;
}

/// <summary>
/// SPI slave interface configuration for FPGA control
/// </summary>
public class SpiConfig
{
    /// <summary>
    /// SPI clock frequency in Hz. Maximum 50 MHz
    /// </summary>
    public int ClockHz { get; set; } = 50000000;

    /// <summary>
    /// SPI mode (CPOL, CPHA). Mode 0: CPOL=0, CPHA=0
    /// </summary>
    public int Mode { get; set; } = 0;

    /// <summary>
    /// SPI word size in bits for register access
    /// </summary>
    public int WordSizeBits { get; set; } = 32;
}

/// <summary>
/// FPGA protection logic configuration
/// </summary>
public class ProtectionConfig
{
    /// <summary>
    /// Watchdog timeout in milliseconds. Triggers safe shutdown if no SPI activity
    /// </summary>
    public double TimeoutMs { get; set; } = 100;

    /// <summary>
    /// Pixel value threshold for overexposure detection (16-bit scale)
    /// </summary>
    public int OverexposureThreshold { get; set; } = 60000;

    /// <summary>
    /// Action when line buffer overflows: stop scanning, drop data, or overwrite oldest
    /// </summary>
    public string OverflowAction { get; set; } = "stop";
}

/// <summary>
/// SoC controller configuration
/// </summary>
public class ControllerConfig
{
    /// <summary>
    /// SoC platform identifier. imx8mp = NXP i.MX8M Plus (recommended)
    /// </summary>
    public string Platform { get; set; } = "imx8mp";

    /// <summary>
    /// Ethernet streaming configuration (SoC to Host PC)
    /// </summary>
    public EthernetConfig Ethernet { get; set; } = new();

    /// <summary>
    /// DDR4 frame buffer allocation
    /// </summary>
    public FrameBufferConfig? FrameBuffer { get; set; }

    /// <summary>
    /// CSI-2 receiver configuration on SoC side
    /// </summary>
    public Csi2RxConfig? Csi2Rx { get; set; }
}

/// <summary>
/// Ethernet streaming configuration (SoC to Host PC)
/// </summary>
public class EthernetConfig
{
    /// <summary>
    /// Ethernet link speed. 10gbe required for target/max tiers. 1gbe for minimum tier only
    /// </summary>
    public string Speed { get; set; } = "10gbe";

    /// <summary>
    /// Transport protocol. UDP for low-latency streaming, TCP for reliable transfer
    /// </summary>
    public string Protocol { get; set; } = "udp";

    /// <summary>
    /// UDP/TCP port number for frame data streaming
    /// </summary>
    public int Port { get; set; } = 8000;

    /// <summary>
    /// Maximum Transmission Unit in bytes. 9000 for jumbo frames (recommended with 10gbe)
    /// </summary>
    public int Mtu { get; set; } = 9000;

    /// <summary>
    /// UDP payload size in bytes per packet
    /// </summary>
    public int PayloadSize { get; set; } = 8192;
}

/// <summary>
/// DDR4 frame buffer allocation
/// </summary>
public class FrameBufferConfig
{
    /// <summary>
    /// Number of frame buffers (ping-pong + double-buffering)
    /// </summary>
    public int Count { get; set; } = 4;

    /// <summary>
    /// Total DDR4 allocation for frame buffers in MB
    /// </summary>
    public int AllocationMb { get; set; } = 128;
}

/// <summary>
/// CSI-2 receiver configuration on SoC side
/// </summary>
public class Csi2RxConfig
{
    /// <summary>
    /// CSI-2 interface index on SoC (0 or 1 for dual-interface SoCs)
    /// </summary>
    public int InterfaceIndex { get; set; } = 0;

    /// <summary>
    /// DMA burst length in bytes for CSI-2 RX data transfer to DDR4
    /// </summary>
    public int DmaBurstLength { get; set; } = 256;
}

/// <summary>
/// Host PC SDK and application configuration
/// </summary>
public class HostConfig
{
    /// <summary>
    /// Frame storage configuration
    /// </summary>
    public StorageConfig Storage { get; set; } = new();

    /// <summary>
    /// Real-time display configuration
    /// </summary>
    public DisplayConfig Display { get; set; } = new();

    /// <summary>
    /// Host network receive configuration
    /// </summary>
    public NetworkConfig? Network { get; set; }
}

/// <summary>
/// Frame storage configuration
/// </summary>
public class StorageConfig
{
    /// <summary>
    /// Primary storage format. TIFF (lossless), RAW (unprocessed), DICOM (medical standard)
    /// </summary>
    public string Format { get; set; } = "tiff";

    /// <summary>
    /// Directory path for frame file storage
    /// </summary>
    public string Path { get; set; } = "./frames";

    /// <summary>
    /// TIFF compression method. 'none' for fastest write, 'lzw' for moderate compression
    /// </summary>
    public string Compression { get; set; } = "none";

    /// <summary>
    /// Automatically save each captured frame to disk
    /// </summary>
    public bool AutoSave { get; set; } = false;
}

/// <summary>
/// Real-time display configuration
/// </summary>
public class DisplayConfig
{
    /// <summary>
    /// Display refresh rate in frames per second for live preview
    /// </summary>
    public int Fps { get; set; } = 15;

    /// <summary>
    /// Color mapping for 16-bit to display conversion
    /// </summary>
    public string ColorMap { get; set; } = "gray";

    /// <summary>
    /// Display window scale factor (1.0 = native resolution)
    /// </summary>
    public double WindowScale { get; set; } = 1.0;
}

/// <summary>
/// Host network receive configuration
/// </summary>
public class NetworkConfig
{
    /// <summary>
    /// UDP receive buffer size in MB for packet reassembly
    /// </summary>
    public int ReceiveBufferMb { get; set; } = 64;

    /// <summary>
    /// Number of receive threads for multi-threaded packet processing
    /// </summary>
    public int ReceiveThreads { get; set; } = 2;

    /// <summary>
    /// Timeout in ms for missing packets before declaring frame incomplete
    /// </summary>
    public int PacketTimeoutMs { get; set; } = 1000;
}
