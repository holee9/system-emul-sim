using System.Text.Json;
using ConfigConverter.Models;

namespace ConfigConverter.Converters;

/// <summary>
/// Converts DetectorConfig to Host SDK JSON configuration.
/// Implements REQ-TOOLS-022: Convert detector_config.yaml to Host SDK configuration.
/// </summary>
public class JsonConverter
{
    private readonly JsonSerializerOptions _options;

    public JsonConverter()
    {
        _options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <summary>
    /// Converts DetectorConfig to Host SDK JSON file content.
    /// </summary>
    /// <param name="config">Detector configuration</param>
    /// <returns>JSON file content as string</returns>
    /// <exception cref="ArgumentNullException">Thrown when config is null</exception>
    public string Convert(DetectorConfig config)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        // Create Host SDK configuration model
        var hostConfig = CreateHostConfigModel(config);

        return JsonSerializer.Serialize(hostConfig, _options);
    }

    private object CreateHostConfigModel(DetectorConfig config)
    {
        // Calculate computed values
        var frameSizeBytes = CalculateFrameSizeBytes(config);
        var packetsPerFrame = CalculatePacketsPerFrame(config, frameSizeBytes);
        var rawDataRateGbps = CalculateRawDataRateGbps(config, frameSizeBytes);

        return new
        {
            // Panel configuration
            panel = new
            {
                rows = config.Panel.Rows,
                cols = config.Panel.Cols,
                bitDepth = config.Panel.BitDepth,
                pixelPitchUm = config.Panel.PixelPitchUm
            },

            // Network configuration (SoC to Host)
            network = new
            {
                protocol = config.Controller.Ethernet.Protocol,
                port = config.Controller.Ethernet.Port,
                payloadSize = config.Controller.Ethernet.PayloadSize,
                mtu = config.Controller.Ethernet.Mtu,
                receiveBufferMb = config.Host.Network?.ReceiveBufferMb ?? 64,
                receiveThreads = config.Host.Network?.ReceiveThreads ?? 2,
                packetTimeoutMs = config.Host.Network?.PacketTimeoutMs ?? 1000
            },

            // Storage configuration
            storage = new
            {
                format = config.Host.Storage.Format,
                path = config.Host.Storage.Path,
                compression = config.Host.Storage.Compression,
                autoSave = config.Host.Storage.AutoSave
            },

            // Display configuration
            display = new
            {
                fps = config.Host.Display.Fps,
                colorMap = config.Host.Display.ColorMap,
                windowScale = config.Host.Display.WindowScale
            },

            // Computed values (derived from configuration)
            computed = new
            {
                frameSizeBytes = frameSizeBytes,
                packetsPerFrame = packetsPerFrame,
                rawDataRateGbps = Math.Round(rawDataRateGbps, 3),
                bandwidthUtilizationPercent = CalculateBandwidthUtilization(config, rawDataRateGbps)
            },

            // Metadata
            metadata = new
            {
                source = "detector_config.yaml",
                generatedAt = DateTime.UtcNow.ToString("o"),
                version = "1.0.0",
                platform = config.Controller.Platform,
                dataInterface = "csi2",
                csi2LaneCount = config.Fpga.DataInterface.Csi2.LaneCount,
                csi2LaneSpeedMbps = config.Fpga.DataInterface.Csi2.LaneSpeedMbps,
                ethernetSpeed = config.Controller.Ethernet.Speed
            }
        };
    }

    /// <summary>
    /// Calculates frame size in bytes.
    /// </summary>
    private long CalculateFrameSizeBytes(DetectorConfig config)
    {
        // Frame size = rows * cols * (bit_depth / 8)
        return (long)config.Panel.Rows * config.Panel.Cols * config.Panel.BitDepth / 8;
    }

    /// <summary>
    /// Calculates number of packets per frame.
    /// </summary>
    private int CalculatePacketsPerFrame(DetectorConfig config, long frameSizeBytes)
    {
        var payloadSize = config.Controller.Ethernet.PayloadSize;
        return (int)Math.Ceiling((double)frameSizeBytes / payloadSize);
    }

    /// <summary>
    /// Calculates raw data rate in Gbps.
    /// </summary>
    private double CalculateRawDataRateGbps(DetectorConfig config, long frameSizeBytes)
    {
        // Data rate = frame_size_bits * fps
        var frameSizeBits = frameSizeBytes * 8;
        var fps = config.Host.Display.Fps;
        var dataRateBps = frameSizeBits * fps;
        return dataRateBps / 1e9; // Convert to Gbps
    }

    /// <summary>
    /// Calculates CSI-2 bandwidth utilization percentage.
    /// </summary>
    private double CalculateBandwidthUtilization(DetectorConfig config, double rawDataRateGbps)
    {
        // CSI-2 capacity = lane_speed_mbps * lane_count / 1000
        var csi2 = config.Fpga.DataInterface.Csi2;
        var csi2CapacityGbps = csi2.LaneSpeedMbps * csi2.LaneCount / 1000.0;
        var utilization = (rawDataRateGbps / csi2CapacityGbps) * 100;
        return Math.Round(utilization, 2);
    }
}
