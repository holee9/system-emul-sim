namespace FpgaSimulator.Core.Protection;

/// <summary>
/// Configuration parameters for the protection logic subsystem.
/// Default values match the FPGA RTL defaults from fpga-design.md Section 7.
/// </summary>
/// <param name="WatchdogTimeoutMs">Watchdog timer timeout in milliseconds (default: 100ms)</param>
/// <param name="ReadoutTimeoutUs">Readout timeout in microseconds (default: 100us)</param>
/// <param name="ShutdownResponseClocks">Maximum clocks to complete safety shutdown (default: 10)</param>
/// <param name="WatchdogEnabled">Whether watchdog timer is enabled (default: true)</param>
/// <param name="ReadoutTimeoutEnabled">Whether readout timeout is enabled (default: true)</param>
public sealed record ProtectionConfig(
    double WatchdogTimeoutMs = 100.0,
    double ReadoutTimeoutUs = 100.0,
    int ShutdownResponseClocks = 10,
    bool WatchdogEnabled = true,
    bool ReadoutTimeoutEnabled = true);
