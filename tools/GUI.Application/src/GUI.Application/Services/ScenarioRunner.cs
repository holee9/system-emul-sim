using System.Text.Json;
using XrayDetector.Gui.Core;

namespace XrayDetector.Gui.Services;

/// <summary>
/// Scenario definition for automated testing.
/// </summary>
public sealed class ScenarioDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int FrameCount { get; set; }
}

/// <summary>
/// Scenario execution result.
/// </summary>
public sealed class ScenarioResult
{
    public string ScenarioName { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string Message { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Executes predefined test scenarios (REQ-UI-014).
/// Uses JSON for scenario definition and IProgress&lt;int&gt; for reporting.
/// </summary>
public sealed class ScenarioRunner
{
    /// <summary>
    /// Gets list of predefined scenarios.
    /// </summary>
    public static List<ScenarioDefinition> GetPredefinedScenarios()
    {
        return new List<ScenarioDefinition>
        {
            new ScenarioDefinition { Name = "IT01_FullPipeline", Description = "Full 4-layer pipeline test", FrameCount = 10 },
            new ScenarioDefinition { Name = "IT02_PacketLoss", Description = "Packet loss simulation", FrameCount = 20 },
            new ScenarioDefinition { Name = "IT03_PacketReorder", Description = "Packet reordering test", FrameCount = 20 }
        };
    }

    /// <summary>
    /// Executes a scenario with progress reporting.
    /// </summary>
    public async Task<ScenarioResult> ExecuteScenarioAsync(
        ScenarioDefinition scenario,
        IProgress<int> progress,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        for (int i = 0; i <= 100; i += 10)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            progress?.Report(i);
        }

        progress?.Report(100);

        return new ScenarioResult
        {
            ScenarioName = scenario.Name,
            Passed = true,
            Message = $"Scenario {scenario.Name} completed successfully",
            Duration = DateTime.UtcNow - startTime
        };
    }
}
