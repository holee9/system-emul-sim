using FluentAssertions;
using IntegrationTests.Helpers;
using Xunit;

namespace IntegrationTests.Helpers;

/// <summary>
/// Tests for LatencyMeasurer using TDD approach.
/// </summary>
public class LatencyMeasurerTests
{
    [Fact]
    public void Constructor_InitializesWithNoSamples()
    {
        // Arrange & Act
        var measurer = new LatencyMeasurer();

        // Assert
        measurer.SampleCount.Should().Be(0);
    }

    [Fact]
    public void RecordLatency_IncreasesSampleCount()
    {
        // Arrange
        var measurer = new LatencyMeasurer();

        // Act
        measurer.RecordLatency(100);
        measurer.RecordLatency(200);

        // Assert
        measurer.SampleCount.Should().Be(2);
    }

    [Fact]
    public void RecordLatency_WithTimeSpan_WorksCorrectly()
    {
        // Arrange
        var measurer = new LatencyMeasurer();

        // Act
        measurer.RecordLatency(TimeSpan.FromMilliseconds(50));

        // Assert
        measurer.SampleCount.Should().Be(1);
    }

    [Fact]
    public void CalculatePercentiles_WithNoSamples_ReturnsZeros()
    {
        // Arrange
        var measurer = new LatencyMeasurer();

        // Act
        var result = measurer.CalculatePercentiles();

        // Assert
        result.P50.Should().Be(0);
        result.P95.Should().Be(0);
        result.P99.Should().Be(0);
        result.Average.Should().Be(0);
    }

    [Fact]
    public void CalculatePercentiles_WithSamples_ReturnsCorrectValues()
    {
        // Arrange
        var measurer = new LatencyMeasurer();
        for (int i = 1; i <= 100; i++)
        {
            measurer.RecordLatency(i);
        }

        // Act
        var result = measurer.CalculatePercentiles();

        // Assert
        result.P50.Should().BeApproximately(50.5, 1);
        result.P95.Should().BeApproximately(95, 1);
        result.P99.Should().BeApproximately(99, 1);
        result.Average.Should().Be(50.5);
    }

    [Fact]
    public void CalculatePercentiles_WithSingleSample_ReturnsThatSample()
    {
        // Arrange
        var measurer = new LatencyMeasurer();
        measurer.RecordLatency(42);

        // Act
        var result = measurer.CalculatePercentiles();

        // Assert
        result.P50.Should().Be(42);
        result.P95.Should().Be(42);
        result.P99.Should().Be(42);
        result.Average.Should().Be(42);
    }

    [Fact]
    public void GenerateHistogram_WithNoSamples_ReturnsEmptyHistogram()
    {
        // Arrange
        var measurer = new LatencyMeasurer();

        // Act
        var histogram = measurer.GenerateHistogram();

        // Assert
        histogram.Boundaries.Length.Should().Be(0);
        histogram.Buckets.Length.Should().Be(0);
    }

    [Fact]
    public void GenerateHistogram_WithSamples_DistributesCorrectly()
    {
        // Arrange
        var measurer = new LatencyMeasurer();
        for (int i = 0; i < 100; i++)
        {
            measurer.RecordLatency(i);
        }

        // Act
        var histogram = measurer.GenerateHistogram(bucketCount: 10);

        // Assert
        histogram.Boundaries.Length.Should().Be(11);
        histogram.Buckets.Length.Should().Be(10);
        histogram.Buckets.Sum().Should().Be(100);
    }

    [Fact]
    public void GenerateHistogram_WithCustomBucketCount_UsesSpecifiedBuckets()
    {
        // Arrange
        var measurer = new LatencyMeasurer();
        for (int i = 0; i < 50; i++)
        {
            measurer.RecordLatency(i);
        }

        // Act
        var histogram = measurer.GenerateHistogram(bucketCount: 5);

        // Assert
        histogram.Buckets.Length.Should().Be(5);
    }

    [Fact]
    public void Clear_RemovesAllSamples()
    {
        // Arrange
        var measurer = new LatencyMeasurer();
        measurer.RecordLatency(100);
        measurer.RecordLatency(200);

        // Act
        measurer.Clear();

        // Assert
        measurer.SampleCount.Should().Be(0);
    }

    [Fact]
    public void RecordLatency_ThreadSafe_AllowsConcurrentWrites()
    {
        // Arrange
        var measurer = new LatencyMeasurer();
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < 100; i++)
        {
            int value = i;
            tasks.Add(Task.Run(() => measurer.RecordLatency(value)));
        }
        Task.WaitAll(tasks.ToArray());

        // Assert
        measurer.SampleCount.Should().Be(100);
    }

    [Fact]
    public void CalculatePercentiles_ThreadSafe_AllowsConcurrentReads()
    {
        // Arrange
        var measurer = new LatencyMeasurer();
        for (int i = 1; i <= 1000; i++)
        {
            measurer.RecordLatency(i);
        }

        // Act
        var tasks = new List<Task<PercentileResult>>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() => measurer.CalculatePercentiles()));
        }
        var results = Task.WhenAll(tasks).Result;

        // Assert - All results should be identical
        var first = results[0];
        results.Should().OnlyContain(r => r.P50 == first.P50);
        results.Should().OnlyContain(r => r.P95 == first.P95);
        results.Should().OnlyContain(r => r.P99 == first.P99);
    }
}
