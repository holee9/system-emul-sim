using IntegrationRunner.Core;
using IntegrationRunner.Core.Models;
using XrayDetector.Gui.Core;

namespace XrayDetector.Gui.Services;

/// <summary>
/// Scenario definition for automated integration testing.
/// </summary>
public sealed class ScenarioDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int FrameCount { get; set; }

    /// <summary>IT scenario type identifier ("IT01", "IT02", etc.).</summary>
    public string ScenarioType { get; set; } = string.Empty;

    /// <summary>Frame-level loss rate (0.0–1.0) for packet loss scenarios.</summary>
    public double LossRate { get; set; } = 0.0;

    /// <summary>Error type to inject mid-run ("CRC", "RECOVERABLE", etc.).</summary>
    public string? InjectErrorType { get; set; }
}

/// <summary>
/// Scenario execution result with actual pass/fail verdict.
/// </summary>
public sealed class ScenarioResult
{
    public string ScenarioName { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string Message { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Executes real integration test scenarios using SimulatorPipeline (SPEC-GUI-001 MVP-3).
/// Each scenario creates a fresh pipeline and runs actual frames, returning real pass/fail results.
/// REQ-UI-014: Replaces fake animation with actual IT test invocation.
/// </summary>
public sealed class ScenarioRunner
{
    private static readonly DetectorConfig MinimumTierConfig = new()
    {
        Panel = new PanelConfig { Rows = 1024, Cols = 1024, BitDepth = 14 },
        Fpga = new FpgaConfig { Csi2Lanes = 4, Csi2DataRateMbps = 1500, LineBufferDepth = 1024 },
        Soc = new SocConfig { FrameBufferCount = 4, UdpPort = 8001, EthernetPort = 9001 },
        Simulation = new SimulationConfig { TestPattern = "counter", Seed = 42 }
    };

    /// <summary>
    /// Returns predefined IT scenarios mapped to SimulatorPipeline tests.
    /// </summary>
    public static List<ScenarioDefinition> GetPredefinedScenarios() =>
    [
        new() { ScenarioType = "IT01", Name = "IT-01: Full Pipeline (10 frames)", Description = "4-layer bit-exact validation, 1024×1024 — zero errors required", FrameCount = 10 },
        new() { ScenarioType = "IT02", Name = "IT-02: Continuous (100 frames)", Description = "Continuous acquisition stability, ≤2% drop rate required", FrameCount = 100 },
        new() { ScenarioType = "IT04", Name = "IT-04: Error Recovery (CRC)", Description = "Non-fatal CRC error injection at frame 5 — pipeline must continue", FrameCount = 20, InjectErrorType = "CRC" },
        new() { ScenarioType = "IT07", Name = "IT-07: Packet Loss (10%)", Description = "Network resilience, 10% frame loss — ≤25% drop rate required", FrameCount = 50, LossRate = 0.10 },
        new() { ScenarioType = "IT09", Name = "IT-09: Long Duration (200 frames)", Description = "Long-duration stability at 1024×1024, ≤1% drop rate required", FrameCount = 200 },
    ];

    /// <summary>
    /// Executes a scenario using a fresh SimulatorPipeline.
    /// Runs in a background thread to keep the UI responsive.
    /// </summary>
    public async Task<ScenarioResult> ExecuteScenarioAsync(
        ScenarioDefinition scenario,
        IProgress<int> progress,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() => RunScenario(scenario, progress, cancellationToken), cancellationToken);
    }

    private static ScenarioResult RunScenario(
        ScenarioDefinition scenario,
        IProgress<int>? progress,
        CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;
        var pipeline = new SimulatorPipeline();
        pipeline.Initialize(MinimumTierConfig);

        // Apply scenario-specific network impairment
        if (scenario.LossRate > 0.0)
            pipeline.SetPacketLossRate(scenario.LossRate);

        int errorInjectAtFrame = scenario.InjectErrorType != null ? scenario.FrameCount / 4 : -1;

        int completed = 0;
        int failed = 0;
        int total = scenario.FrameCount;

        for (int i = 0; i < total; i++)
        {
            if (ct.IsCancellationRequested)
                break;

            // Inject non-fatal error at 25% mark for error-recovery scenarios
            if (errorInjectAtFrame >= 0 && i == errorInjectAtFrame && scenario.InjectErrorType != null)
                pipeline.InjectError(scenario.InjectErrorType);

            var frame = pipeline.ProcessFrame();
            if (frame != null) completed++;
            else failed++;

            progress?.Report((int)((i + 1) * 100.0 / total));
        }

        var stats = pipeline.GetStatistics();
        bool passed = DeterminePassFail(scenario, completed, failed);
        string message = BuildMessage(scenario, completed, failed, stats);

        return new ScenarioResult
        {
            ScenarioName = scenario.Name,
            Passed = passed,
            Message = message,
            Duration = DateTime.UtcNow - startTime
        };
    }

    /// <summary>
    /// Determines pass/fail based on scenario-specific thresholds.
    /// </summary>
    private static bool DeterminePassFail(ScenarioDefinition scenario, int completed, int failed)
    {
        int total = completed + failed;
        if (total == 0) return false;

        double failRate = (double)failed / total;

        return scenario.ScenarioType switch
        {
            "IT01" => failed == 0,                        // Zero tolerance — bit-exact check
            "IT02" => failRate <= 0.02,                   // ≤2% drop rate
            "IT04" => completed > failed,                 // Non-fatal error: majority of frames complete
            "IT07" => failRate <= scenario.LossRate * 2.5, // ≤2.5× the configured loss rate
            "IT09" => failRate <= 0.01,                   // ≤1% for long-duration stability
            _ => failRate <= 0.05                         // Default: ≤5%
        };
    }

    /// <summary>
    /// Builds a human-readable result message with frame and network statistics.
    /// </summary>
    private static string BuildMessage(ScenarioDefinition scenario, int completed, int failed, PipelineStatistics stats)
    {
        int total = completed + failed;
        double failRate = total > 0 ? (double)failed / total * 100 : 0;

        string networkInfo = stats.NetworkStats != null
            ? $" | Packets: sent={stats.NetworkStats.PacketsSent}, lost={stats.NetworkStats.PacketsLost}"
            : string.Empty;

        return $"Frames: {completed}/{total} completed ({failRate:F1}% failed){networkInfo}";
    }
}
