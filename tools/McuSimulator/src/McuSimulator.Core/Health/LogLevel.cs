namespace McuSimulator.Core.Health;

/// <summary>
/// Log severity levels (1:1 mapping from log_level_t in fw/include/health_monitor.h).
/// </summary>
public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
    Critical = 4
}
