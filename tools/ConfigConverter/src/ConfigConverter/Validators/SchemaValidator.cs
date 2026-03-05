using ConfigConverter.Models;

namespace ConfigConverter.Validators;

/// <summary>
/// Validation result for schema validation operations.
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Validates DetectorConfig against the JSON Schema constraints.
/// Implements REQ-TOOLS-023: Validate input against JSON Schema.
/// </summary>
public class SchemaValidator
{
    /// <summary>
    /// Validates DetectorConfig against schema constraints.
    /// </summary>
    /// <param name="config">Configuration to validate</param>
    /// <returns>Validation result with error list if invalid</returns>
    /// <exception cref="ArgumentNullException">Thrown when config is null</exception>
    public ValidationResult Validate(DetectorConfig config)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        var result = new ValidationResult { IsValid = true };
        var errors = new List<string>();

        // Validate Panel
        ValidatePanel(config.Panel, errors);

        // Validate FPGA
        ValidateFpga(config.Fpga, errors);

        // Validate Controller
        ValidateController(config.Controller, errors);

        // Validate Host
        ValidateHost(config.Host, errors);

        result.IsValid = errors.Count == 0;
        result.Errors = errors;

        return result;
    }

    private void ValidatePanel(PanelConfig panel, List<string> errors)
    {
        // Rows validation
        if (panel.Rows < 256 || panel.Rows > 4096)
        {
            errors.Add($"panel.rows must be between 256 and 4096, got {panel.Rows}");
        }

        // Cols validation
        if (panel.Cols < 256 || panel.Cols > 4096)
        {
            errors.Add($"panel.cols must be between 256 and 4096, got {panel.Cols}");
        }

        // Pixel pitch validation
        if (panel.PixelPitchUm < 50 || panel.PixelPitchUm > 500)
        {
            errors.Add($"panel.pixelPitchUm must be between 50 and 500, got {panel.PixelPitchUm}");
        }

        // Bit depth validation (enum: 14, 16)
        if (panel.BitDepth != 14 && panel.BitDepth != 16)
        {
            errors.Add($"panel.bitDepth must be 14 or 16, got {panel.BitDepth}");
        }
    }

    private void ValidateFpga(FpgaConfig fpga, List<string> errors)
    {
        // Validate Timing
        if (fpga.Timing == null)
        {
            errors.Add("fpga.timing is required");
        }
        else
        {
            ValidateTiming(fpga.Timing, errors);
        }

        // Validate LineBuffer
        if (fpga.LineBuffer == null)
        {
            errors.Add("fpga.lineBuffer is required");
        }
        else
        {
            ValidateLineBuffer(fpga.LineBuffer, errors);
        }

        // Validate DataInterface
        if (fpga.DataInterface == null)
        {
            errors.Add("fpga.dataInterface is required");
        }
        else
        {
            ValidateDataInterface(fpga.DataInterface, errors);
        }

        // Validate SPI
        if (fpga.Spi == null)
        {
            errors.Add("fpga.spi is required");
        }
        else
        {
            ValidateSpi(fpga.Spi, errors);
        }
    }

    private void ValidateTiming(TimingConfig timing, List<string> errors)
    {
        if (timing.GateOnUs < 0.1 || timing.GateOnUs > 1000)
        {
            errors.Add($"fpga.timing.gateOnUs must be between 0.1 and 1000, got {timing.GateOnUs}");
        }

        if (timing.GateOffUs < 0.1 || timing.GateOffUs > 1000)
        {
            errors.Add($"fpga.timing.gateOffUs must be between 0.1 and 1000, got {timing.GateOffUs}");
        }

        if (timing.RoicSettleUs < 0.01 || timing.RoicSettleUs > 100)
        {
            errors.Add($"fpga.timing.roicSettleUs must be between 0.01 and 100, got {timing.RoicSettleUs}");
        }

        if (timing.AdcConvUs < 0.1 || timing.AdcConvUs > 100)
        {
            errors.Add($"fpga.timing.adcConvUs must be between 0.1 and 100, got {timing.AdcConvUs}");
        }
    }

    private void ValidateLineBuffer(LineBufferConfig lineBuffer, List<string> errors)
    {
        if (lineBuffer.DepthLines < 1 || lineBuffer.DepthLines > 8)
        {
            errors.Add($"fpga.lineBuffer.depthLines must be between 1 and 8, got {lineBuffer.DepthLines}");
        }

        if (lineBuffer.BramWidthBits != 16 && lineBuffer.BramWidthBits != 32 && lineBuffer.BramWidthBits != 36)
        {
            errors.Add($"fpga.lineBuffer.bramWidthBits must be 16, 32, or 36, got {lineBuffer.BramWidthBits}");
        }
    }

    private void ValidateDataInterface(DataInterfaceConfig dataInterface, List<string> errors)
    {
        if (dataInterface.Primary != "csi2")
        {
            errors.Add($"fpga.dataInterface.primary must be 'csi2', got '{dataInterface.Primary}'");
        }

        if (dataInterface.Csi2 == null)
        {
            errors.Add("fpga.dataInterface.csi2 is required");
            return;
        }

        ValidateCsi2(dataInterface.Csi2, errors);
    }

    private void ValidateCsi2(Csi2Config csi2, List<string> errors)
    {
        // Lane count enum: 1, 2, 4
        if (csi2.LaneCount != 1 && csi2.LaneCount != 2 && csi2.LaneCount != 4)
        {
            errors.Add($"fpga.csi2.laneCount must be 1, 2, or 4, got {csi2.LaneCount}");
        }

        // Data type enum: RAW14, RAW16
        if (csi2.DataType != "RAW14" && csi2.DataType != "RAW16")
        {
            errors.Add($"fpga.csi2.dataType must be 'RAW14' or 'RAW16', got '{csi2.DataType}'");
        }

        // Virtual channel range: 0-3
        if (csi2.VirtualChannel < 0 || csi2.VirtualChannel > 3)
        {
            errors.Add($"fpga.csi2.virtualChannel must be between 0 and 3, got {csi2.VirtualChannel}");
        }

        // Lane speed range: 400-1250 Mbps
        if (csi2.LaneSpeedMbps < 400 || csi2.LaneSpeedMbps > 1250)
        {
            errors.Add($"fpga.csi2.laneSpeedMbps must be between 400 and 1250, got {csi2.LaneSpeedMbps}");
        }

        // Line blanking clocks
        if (csi2.LineBlankingClocks < 10 || csi2.LineBlankingClocks > 1000)
        {
            errors.Add($"fpga.csi2.lineBlankingClocks must be between 10 and 1000, got {csi2.LineBlankingClocks}");
        }

        // Frame blanking lines
        if (csi2.FrameBlankingLines < 1 || csi2.FrameBlankingLines > 100)
        {
            errors.Add($"fpga.csi2.frameBlankingLines must be between 1 and 100, got {csi2.FrameBlankingLines}");
        }
    }

    private void ValidateSpi(SpiConfig spi, List<string> errors)
    {
        // Clock frequency range: 100kHz - 50MHz
        if (spi.ClockHz < 100000 || spi.ClockHz > 50000000)
        {
            errors.Add($"fpga.spi.clockHz must be between 100000 and 50000000, got {spi.ClockHz}");
        }

        // SPI mode enum: 0, 1, 2, 3
        if (spi.Mode < 0 || spi.Mode > 3)
        {
            errors.Add($"fpga.spi.mode must be 0, 1, 2, or 3, got {spi.Mode}");
        }

        // Word size enum: 8, 16, 32
        if (spi.WordSizeBits != 8 && spi.WordSizeBits != 16 && spi.WordSizeBits != 32)
        {
            errors.Add($"fpga.spi.wordSizeBits must be 8, 16, or 32, got {spi.WordSizeBits}");
        }
    }

    private void ValidateController(ControllerConfig controller, List<string> errors)
    {
        // Platform enum
        if (controller.Platform != "imx8mp" && controller.Platform != "rk3588" && controller.Platform != "jetson_nano")
        {
            errors.Add($"controller.platform must be 'imx8mp', 'rk3588', or 'jetson_nano', got '{controller.Platform}'");
        }

        if (controller.Ethernet == null)
        {
            errors.Add("controller.ethernet is required");
            return;
        }

        ValidateEthernet(controller.Ethernet, errors);
    }

    private void ValidateEthernet(EthernetConfig ethernet, List<string> errors)
    {
        // Speed enum: 1gbe, 10gbe
        if (ethernet.Speed != "1gbe" && ethernet.Speed != "10gbe")
        {
            errors.Add($"controller.ethernet.speed must be '1gbe' or '10gbe', got '{ethernet.Speed}'");
        }

        // Protocol enum: udp, tcp
        if (ethernet.Protocol != "udp" && ethernet.Protocol != "tcp")
        {
            errors.Add($"controller.ethernet.protocol must be 'udp' or 'tcp', got '{ethernet.Protocol}'");
        }

        // Port range
        if (ethernet.Port < 1024 || ethernet.Port > 65535)
        {
            errors.Add($"controller.ethernet.port must be between 1024 and 65535, got {ethernet.Port}");
        }

        // MTU range
        if (ethernet.Mtu < 1500 || ethernet.Mtu > 9000)
        {
            errors.Add($"controller.ethernet.mtu must be between 1500 and 9000, got {ethernet.Mtu}");
        }

        // Payload size range
        if (ethernet.PayloadSize < 1024 || ethernet.PayloadSize > 8192)
        {
            errors.Add($"controller.ethernet.payloadSize must be between 1024 and 8192, got {ethernet.PayloadSize}");
        }
    }

    private void ValidateHost(HostConfig host, List<string> errors)
    {
        if (host.Storage == null)
        {
            errors.Add("host.storage is required");
        }
        else
        {
            ValidateStorage(host.Storage, errors);
        }

        if (host.Display == null)
        {
            errors.Add("host.display is required");
        }
        else
        {
            ValidateDisplay(host.Display, errors);
        }
    }

    private void ValidateStorage(StorageConfig storage, List<string> errors)
    {
        // Format enum: tiff, raw, dicom
        if (storage.Format != "tiff" && storage.Format != "raw" && storage.Format != "dicom")
        {
            errors.Add($"host.storage.format must be 'tiff', 'raw', or 'dicom', got '{storage.Format}'");
        }

        // Compression enum: none, lzw, zip
        if (storage.Compression != "none" && storage.Compression != "lzw" && storage.Compression != "zip")
        {
            errors.Add($"host.storage.compression must be 'none', 'lzw', or 'zip', got '{storage.Compression}'");
        }
    }

    private void ValidateDisplay(DisplayConfig display, List<string> errors)
    {
        // FPS range
        if (display.Fps < 1 || display.Fps > 60)
        {
            errors.Add($"host.display.fps must be between 1 and 60, got {display.Fps}");
        }

        // Color map enum
        if (display.ColorMap != "gray" && display.ColorMap != "jet" && display.ColorMap != "hot" && display.ColorMap != "bone")
        {
            errors.Add($"host.display.colorMap must be 'gray', 'jet', 'hot', or 'bone', got '{display.ColorMap}'");
        }

        // Window scale range
        if (display.WindowScale < 0.1 || display.WindowScale > 2.0)
        {
            errors.Add($"host.display.windowScale must be between 0.1 and 2.0, got {display.WindowScale}");
        }
    }
}
