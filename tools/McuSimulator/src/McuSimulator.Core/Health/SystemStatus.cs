namespace McuSimulator.Core.Health;

/// <summary>
/// Point-in-time system status snapshot (1:1 mapping from system_status_t in fw/include/health_monitor.h).
/// </summary>
public sealed class SystemStatus
{
    /// <summary>Current sequence engine state.</summary>
    public byte State { get; set; }

    /// <summary>Runtime statistics snapshot.</summary>
    public RuntimeStatistics Stats { get; set; } = new();

    /// <summary>Battery state-of-charge percentage (0-100).</summary>
    public byte BatterySoc { get; set; }

    /// <summary>Battery voltage in millivolts.</summary>
    public ushort BatteryMv { get; set; }

    /// <summary>System uptime in seconds since boot.</summary>
    public uint UptimeSec { get; set; }

    /// <summary>FPGA die temperature in raw ADC units.</summary>
    public ushort FpgaTemp { get; set; }
}
