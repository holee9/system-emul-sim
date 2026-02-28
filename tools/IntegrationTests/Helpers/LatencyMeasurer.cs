namespace IntegrationTests.Helpers;

/// <summary>
/// Measures and analyzes latency percentiles (p50, p95, p99).
/// Thread-safe for concurrent measurements.
/// </summary>
public class LatencyMeasurer
{
    private readonly List<long> _latencies;
    private readonly object _lock = new();

    /// <summary>
    /// Gets the number of recorded latency samples.
    /// </summary>
    public int SampleCount
    {
        get
        {
            lock (_lock)
            {
                return _latencies.Count;
            }
        }
    }

    /// <summary>
    /// Creates a new LatencyMeasurer.
    /// </summary>
    public LatencyMeasurer()
    {
        _latencies = new List<long>();
    }

    /// <summary>
    /// Records a latency measurement.
    /// </summary>
    /// <param name="timestamp">Timestamp in ticks or microseconds.</param>
    public void RecordLatency(long timestamp)
    {
        lock (_lock)
        {
            _latencies.Add(timestamp);
        }
    }

    /// <summary>
    /// Records a latency measurement using TimeSpan.
    /// </summary>
    /// <param name="latency">Latency duration.</param>
    public void RecordLatency(TimeSpan latency)
    {
        RecordLatency(latency.Ticks);
    }

    /// <summary>
    /// Calculates latency percentiles (p50, p95, p99).
    /// </summary>
    /// <returns>Percentile results.</returns>
    public PercentileResult CalculatePercentiles()
    {
        long[] samples;
        lock (_lock)
        {
            if (_latencies.Count == 0)
                return new PercentileResult(0, 0, 0, 0);

            samples = _latencies.ToArray();
        }

        Array.Sort(samples);

        double p50 = CalculatePercentile(samples, 50);
        double p95 = CalculatePercentile(samples, 95);
        double p99 = CalculatePercentile(samples, 99);
        double avg = samples.Average();

        return new PercentileResult(p50, p95, p99, avg);
    }

    /// <summary>
    /// Generates a histogram of latency values.
    /// </summary>
    /// <param name="bucketCount">Number of histogram buckets.</param>
    /// <returns>Histogram with bucket boundaries and counts.</returns>
    public HistogramResult GenerateHistogram(int bucketCount = 10)
    {
        long[] samples;
        lock (_lock)
        {
            if (_latencies.Count == 0)
                return new HistogramResult(Array.Empty<long>(), Array.Empty<int>());

            samples = _latencies.ToArray();
        }

        Array.Sort(samples);

        long min = samples[0];
        long max = samples[^1];
        double bucketSize = (max - min) / (double)bucketCount;

        int[] buckets = new int[bucketCount];
        long[] boundaries = new long[bucketCount + 1];

        for (int i = 0; i <= bucketCount; i++)
        {
            boundaries[i] = min + (long)(i * bucketSize);
        }

        foreach (long sample in samples)
        {
            int bucketIndex = Math.Min((int)((sample - min) / bucketSize), bucketCount - 1);
            buckets[bucketIndex]++;
        }

        return new HistogramResult(boundaries, buckets);
    }

    /// <summary>
    /// Clears all recorded samples.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _latencies.Clear();
        }
    }

    private static double CalculatePercentile(long[] sortedSamples, int percentile)
    {
        if (sortedSamples.Length == 0)
            return 0;

        double index = (percentile / 100.0) * (sortedSamples.Length - 1);
        int lowerIndex = (int)Math.Floor(index);
        int upperIndex = (int)Math.Ceiling(index);

        if (lowerIndex == upperIndex)
            return sortedSamples[lowerIndex];

        double fraction = index - lowerIndex;
        return sortedSamples[lowerIndex] * (1 - fraction) + sortedSamples[upperIndex] * fraction;
    }
}

/// <summary>
/// Result of percentile calculation.
/// </summary>
public sealed class PercentileResult
{
    /// <summary>50th percentile (median).</summary>
    public double P50 { get; }

    /// <summary>95th percentile.</summary>
    public double P95 { get; }

    /// <summary>99th percentile.</summary>
    public double P99 { get; }

    /// <summary>Average latency.</summary>
    public double Average { get; }

    public PercentileResult(double p50, double p95, double p99, double average)
    {
        P50 = p50;
        P95 = p95;
        P99 = p99;
        Average = average;
    }

    public override string ToString()
    {
        return $"P50: {P50:F2}, P95: {P95:F2}, P99: {P99:F2}, Avg: {Average:F2}";
    }
}

/// <summary>
/// Result of histogram generation.
/// </summary>
public sealed class HistogramResult
{
    /// <summary>Bucket boundaries (bucketCount + 1 values).</summary>
    public long[] Boundaries { get; }

    /// <summary>Count of samples in each bucket.</summary>
    public int[] Buckets { get; }

    public HistogramResult(long[] boundaries, int[] buckets)
    {
        Boundaries = boundaries;
        Buckets = buckets;
    }

    /// <summary>
    /// Gets a formatted string representation of the histogram.
    /// </summary>
    public string FormatHistogram()
    {
        if (Boundaries.Length == 0)
            return "No data";

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < Buckets.Length; i++)
        {
            sb.AppendLine($"[{Boundaries[i]}, {Boundaries[i + 1]}): {Buckets[i]}");
        }
        return sb.ToString();
    }
}
