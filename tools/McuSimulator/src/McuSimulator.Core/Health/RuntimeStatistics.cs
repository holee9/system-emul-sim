namespace McuSimulator.Core.Health;

/// <summary>
/// Aggregated runtime counters (1:1 mapping from runtime_stats_t in fw/include/health_monitor.h).
/// </summary>
public sealed class RuntimeStatistics
{
    /// <summary>Total frames received from the FPGA.</summary>
    public long FramesReceived { get; set; }

    /// <summary>Total frames sent to the host.</summary>
    public long FramesSent { get; set; }

    /// <summary>Total frames dropped due to overflow or error.</summary>
    public long FramesDropped { get; set; }

    /// <summary>Cumulative SPI bus errors.</summary>
    public long SpiErrors { get; set; }

    /// <summary>Cumulative CSI-2 link errors.</summary>
    public long Csi2Errors { get; set; }

    /// <summary>Total network packets sent.</summary>
    public long PacketsSent { get; set; }

    /// <summary>Total bytes transmitted.</summary>
    public ulong BytesSent { get; set; }

    /// <summary>Total authentication failures.</summary>
    public long AuthFailures { get; set; }

    /// <summary>Total watchdog timeout resets.</summary>
    public long WatchdogResets { get; set; }

    /// <summary>
    /// Returns a deep copy of this instance.
    /// </summary>
    public RuntimeStatistics Clone()
    {
        return new RuntimeStatistics
        {
            FramesReceived = FramesReceived,
            FramesSent = FramesSent,
            FramesDropped = FramesDropped,
            SpiErrors = SpiErrors,
            Csi2Errors = Csi2Errors,
            PacketsSent = PacketsSent,
            BytesSent = BytesSent,
            AuthFailures = AuthFailures,
            WatchdogResets = WatchdogResets
        };
    }
}
