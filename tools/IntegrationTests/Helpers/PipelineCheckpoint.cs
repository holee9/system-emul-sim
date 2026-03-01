using System.Diagnostics;

namespace IntegrationTests.Helpers;

/// <summary>
/// Captures a per-layer input/output snapshot during pipeline execution.
/// Used for diagnostics and data integrity verification at layer boundaries.
/// </summary>
public sealed class PipelineCheckpoint
{
    /// <summary>Name of the layer (e.g., "Panel", "FPGA", "MCU", "Network", "Host").</summary>
    public required string LayerName { get; init; }

    /// <summary>Input data provided to this layer (typed per layer).</summary>
    public required object InputData { get; init; }

    /// <summary>Output data produced by this layer.</summary>
    public required object OutputData { get; init; }

    /// <summary>Processing latency in milliseconds.</summary>
    public required double LatencyMs { get; init; }

    /// <summary>Timestamp when this checkpoint was captured.</summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// Captures a checkpoint by executing a processing function and measuring its latency.
    /// </summary>
    /// <param name="layerName">Name of the pipeline layer.</param>
    /// <param name="input">Input data to the layer.</param>
    /// <param name="process">Processing function that produces output from input.</param>
    /// <returns>A PipelineCheckpoint with timing and data snapshots.</returns>
    public static PipelineCheckpoint Capture(string layerName, object input, Func<object> process)
    {
        ArgumentNullException.ThrowIfNull(layerName);
        ArgumentNullException.ThrowIfNull(process);

        var sw = Stopwatch.StartNew();
        var output = process();
        sw.Stop();

        return new PipelineCheckpoint
        {
            LayerName = layerName,
            InputData = input ?? new object(),
            OutputData = output ?? new object(),
            LatencyMs = sw.Elapsed.TotalMilliseconds,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Returns a human-readable string representation of this checkpoint.
    /// </summary>
    public override string ToString()
    {
        return $"PipelineCheckpoint {{ Layer={LayerName}, Latency={LatencyMs:F3}ms, Timestamp={Timestamp:O} }}";
    }
}
