using ConfigConverter.Models;

namespace ConfigConverter.Validators;

/// <summary>
/// Cross-validation result with warnings and errors.
/// </summary>
public class CrossValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Performs cross-validation checks across configuration sections.
/// Implements REQ-TOOLS-024: Perform cross-validation checks.
/// </summary>
public class CrossValidator
{
    private const double BandwidthWarningThreshold = 0.8; // 80%
    private const long MinFrameBufferBytes = 16 * 1024 * 1024; // 16 MB minimum

    /// <summary>
    /// Performs cross-validation checks on the configuration.
    /// </summary>
    /// <param name="config">Configuration to validate</param>
    /// <returns>Cross-validation result with warnings and errors</returns>
    /// <exception cref="ArgumentNullException">Thrown when config is null</exception>
    public CrossValidationResult Validate(DetectorConfig config)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        var result = new CrossValidationResult { IsValid = true };
        var warnings = new List<string>();
        var errors = new List<string>();

        // Calculate bandwidth requirements
        var dataRateGbps = CalculateDataRateGbps(config);
        var csi2 = config.Fpga.DataInterface.Csi2;
        var csi2CapacityGbps = csi2.LaneSpeedMbps * csi2.LaneCount / 1000.0;

        // Bandwidth validation
        ValidateBandwidth(config, dataRateGbps, csi2CapacityGbps, warnings, errors);

        // Ethernet validation
        ValidateEthernet(config, dataRateGbps, warnings, errors);

        // Buffer sizing validation
        ValidateBufferSizing(config, warnings, errors);

        // MTU validation
        ValidateMtu(config, errors);

        result.IsValid = errors.Count == 0;
        result.Warnings = warnings;
        result.Errors = errors;

        return result;
    }

    /// <summary>
    /// Calculates raw data rate in Gbps.
    /// </summary>
    private double CalculateDataRateGbps(DetectorConfig config)
    {
        var frameSizeBytes = (long)config.Panel.Rows * config.Panel.Cols * config.Panel.BitDepth / 8;
        var frameSizeBits = frameSizeBytes * 8;
        var fps = config.Host.Display.Fps;
        return frameSizeBits * fps / 1e9;
    }

    /// <summary>
    /// Validates bandwidth constraints.
    /// </summary>
    private void ValidateBandwidth(
        DetectorConfig config,
        double dataRateGbps,
        double csi2CapacityGbps,
        List<string> warnings,
        List<string> errors)
    {
        var utilization = dataRateGbps / csi2CapacityGbps;

        if (utilization > 1.0)
        {
            errors.Add($"Bandwidth constraint violation: Data rate ({dataRateGbps:F3} Gbps) exceeds CSI-2 capacity ({csi2CapacityGbps:F3} Gbps). " +
                      $"Reduce resolution, frame rate, or increase CSI-2 lane speed.");
        }
        else if (utilization > BandwidthWarningThreshold)
        {
            warnings.Add($"High bandwidth utilization: {utilization * 100:F1}% of CSI-2 capacity ({dataRateGbps:F3} / {csi2CapacityGbps:F3} Gbps). " +
                        $"Consider reducing resolution or frame rate for margin.");
        }
    }

    /// <summary>
    /// Validates Ethernet capacity.
    /// </summary>
    private void ValidateEthernet(
        DetectorConfig config,
        double dataRateGbps,
        List<string> warnings,
        List<string> errors)
    {
        var ethernetCapacityGbps = config.Controller.Ethernet.Speed.ToLower() switch
        {
            "10gbe" => 10.0,
            "1gbe" => 1.0,
            _ => 1.0
        };

        // Allow some margin for protocol overhead (~20%)
        var effectiveCapacity = ethernetCapacityGbps * 0.8;

        if (dataRateGbps > effectiveCapacity)
        {
            errors.Add($"Insufficient Ethernet capacity: Data rate ({dataRateGbps:F3} Gbps) exceeds effective capacity of {config.Controller.Ethernet.Speed} (~{effectiveCapacity:F2} Gbps). " +
                      $"Use 10GbE for intermediate and higher tiers.");
        }
    }

    /// <summary>
    /// Validates buffer sizing constraints.
    /// </summary>
    private void ValidateBufferSizing(
        DetectorConfig config,
        List<string> warnings,
        List<string> errors)
    {
        var frameSizeBytes = (long)config.Panel.Rows * config.Panel.Cols * config.Panel.BitDepth / 8;

        // Validate frame buffer allocation
        if (config.Controller.FrameBuffer != null)
        {
            var totalAllocationBytes = (long)config.Controller.FrameBuffer.AllocationMb * 1024 * 1024;
            var requiredBytes = frameSizeBytes * config.Controller.FrameBuffer.Count;

            if (totalAllocationBytes < requiredBytes)
            {
                errors.Add($"Insufficient frame buffer allocation: {config.Controller.FrameBuffer.AllocationMb} MB allocated, " +
                          $"but {requiredBytes / (1024 * 1024)} MB required for {config.Controller.FrameBuffer.Count} frames " +
                          $"of {config.Panel.Rows}x{config.Panel.Cols}x{config.Panel.BitDepth}-bit.");
            }
        }

        // Validate host receive buffer
        if (config.Host.Network != null)
        {
            var receiveBufferBytes = (long)config.Host.Network.ReceiveBufferMb * 1024 * 1024;

            // Receive buffer should accommodate at least 2 frames
            if (receiveBufferBytes < frameSizeBytes * 2)
            {
                warnings.Add($"Host receive buffer ({config.Host.Network.ReceiveBufferMb} MB) may be too small. " +
                           $"Recommended: at least {(frameSizeBytes * 2) / (1024 * 1024)} MB for {config.Panel.Rows}x{config.Panel.Cols}x{config.Panel.BitDepth}-bit frames.");
            }
        }
    }

    /// <summary>
    /// Validates MTU vs payload size consistency.
    /// </summary>
    private void ValidateMtu(DetectorConfig config, List<string> errors)
    {
        var payloadSize = config.Controller.Ethernet.PayloadSize;
        var mtu = config.Controller.Ethernet.Mtu;

        // MTU must be >= payload size + IP/UDP headers (typically 28 bytes)
        var maxPayloadWithHeaders = payloadSize + 28;

        if (mtu < maxPayloadWithHeaders)
        {
            errors.Add($"MTU ({mtu} bytes) is too small for payload size ({payloadSize} bytes). " +
                      $"MTU must be at least {maxPayloadWithHeaders} bytes to accommodate payload plus IP/UDP headers. " +
                      $"Increase MTU to {payloadSize + 100} or reduce payload size.");
        }
    }
}
