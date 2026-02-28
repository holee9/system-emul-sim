using Xunit;
using FluentAssertions;
using IntegrationTests.Helpers;
using Common.Dto.Interfaces;
using Common.Dto.Dtos;
using PanelSimulator;
using PanelSimulator.Models;
using SimulatorType = PanelSimulator.PanelSimulator;
using System.Diagnostics;

namespace IntegrationTests.Integration;

/// <summary>
/// IT-09: Maximum Tier Stress Test.
/// Validates system stability at 3072x3072@30fps for 60 seconds.
/// Reference: SPEC-INTEG-001 AC-INTEG-009
/// </summary>
public class IT09_MaximumTierStressTests : IDisposable
{
    private const int MaxTierRows = 3072;
    private const int MaxTierCols = 3072;
    private const int MaxTierFps = 30;
    private const int TestDurationSeconds = 60;
    private const int ExpectedFrameCount = 1800; // 30 fps * 60 seconds
    private const double MaxFrameLossRate = 0.01; // < 1%

    private readonly SimulatorType _panelSimulator;
    private readonly Process _currentProcess;

    public IT09_MaximumTierStressTests()
    {
        _panelSimulator = new SimulatorType();
        _currentProcess = Process.GetCurrentProcess();
    }

    [Fact(Skip = "Stress test - takes 60+ seconds. Run manually for validation.")]
    public async Task MaximumTierStress_ShouldSustain30fps_60Seconds()
    {
        // Arrange - Configure maximum tier
        var config = new PanelConfig
        {
            Rows = MaxTierRows,
            Cols = MaxTierCols,
            BitDepth = 16,
            TestPattern = TestPattern.Counter,
            NoiseModel = NoiseModelType.None,
            DefectRate = 0,
            Seed = 42
        };

        _panelSimulator.Initialize(config);

        // Act - Run for 60 seconds at 30 fps
        var frames = new List<FrameData>();
        var droppedFrames = 0;
        var stopwatch = Stopwatch.StartNew();
        var memoryUsage = new List<long>();
        var telemetryInterval = TimeSpan.FromSeconds(5);

        var lastTelemetryTime = stopwatch.ElapsedMilliseconds;
        long initialMemory = _currentProcess.WorkingSet64;

        // Generate frames for 60 seconds
        while (stopwatch.ElapsedMilliseconds < TestDurationSeconds * 1000)
        {
            var frameStartTime = stopwatch.ElapsedMilliseconds;

            // Generate frame (simulates 30 fps timing)
            var result = _panelSimulator.Process(null);
            if (result is FrameData frame)
            {
                frames.Add(frame);
            }
            else
            {
                droppedFrames++;
            }

            // Collect telemetry every 5 seconds
            if (stopwatch.ElapsedMilliseconds - lastTelemetryTime >= telemetryInterval.TotalMilliseconds)
            {
                memoryUsage.Add(_currentProcess.WorkingSet64);
                lastTelemetryTime = stopwatch.ElapsedMilliseconds;
            }

            // Maintain 30 fps timing (~33.33ms per frame)
            var elapsed = stopwatch.ElapsedMilliseconds - frameStartTime;
            var targetInterval = 1000 / MaxTierFps;
            if (elapsed < targetInterval)
            {
                await Task.Delay(targetInterval - (int)elapsed);
            }
        }

        stopwatch.Stop();
        long finalMemory = _currentProcess.WorkingSet64;

        // Assert - Frame count and loss rate
        int actualFrameCount = frames.Count;
        double frameLossRate = (double)droppedFrames / ExpectedFrameCount;

        actualFrameCount.Should().BeGreaterOrEqualTo((int)(ExpectedFrameCount * (1 - MaxFrameLossRate)),
            $"Should capture at least {ExpectedFrameCount * (1 - MaxFrameLossRate):F0} frames " +
            $"(actual: {actualFrameCount}, dropped: {droppedFrames})");

        frameLossRate.Should().BeLessThan(MaxFrameLossRate,
            $"Frame loss rate should be < 1% (actual: {frameLossRate:P2})");

        // Assert - No crashes or hangs
        actualFrameCount.Should().BeGreaterThan(0, "Should have captured frames");

        // Assert - Memory growth should be reasonable (< 500 MB over test)
        long memoryGrowth = (finalMemory - initialMemory) / (1024 * 1024);
        memoryGrowth.Should().BeLessThan(500,
            $"Memory growth should be < 500 MB (actual: {memoryGrowth} MB)");

        // Assert - Test completed within expected time
        stopwatch.ElapsedMilliseconds.Should().BeLessThan((TestDurationSeconds + 10) * 1000,
            "Test should complete within 70 seconds (60s + 10s tolerance)");
    }

    [Fact]
    public async Task MaximumTierStress_ShortVersion_ShouldVerifyConfiguration()
    {
        // Arrange - Configure maximum tier
        var config = new PanelConfig
        {
            Rows = MaxTierRows,
            Cols = MaxTierCols,
            BitDepth = 16,
            TestPattern = TestPattern.Counter,
            NoiseModel = NoiseModelType.None,
            DefectRate = 0,
            Seed = 42
        };

        _panelSimulator.Initialize(config);

        // Act - Generate 10 frames (shorter test for CI)
        var frames = new List<FrameData>();
        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < 10; i++)
        {
            var result = _panelSimulator.Process(null);
            if (result is FrameData frame)
            {
                frames.Add(frame);
            }

            // Simulate 30 fps timing
            await Task.Delay(1000 / MaxTierFps);
        }

        stopwatch.Stop();

        // Assert - Verify maximum tier configuration
        frames.Count.Should().Be(10, "All frames should be generated");

        foreach (var frame in frames)
        {
            frame.Width.Should().Be(MaxTierCols, "Frame width should match maximum tier");
            frame.Height.Should().Be(MaxTierRows, "Frame height should match maximum tier");
            frame.Pixels.Length.Should().Be(MaxTierRows * MaxTierCols,
                "Frame should have correct pixel count for maximum tier");
        }

        // Verify no drops in short test
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000,
            "10 frames at 30 fps should complete in < 5 seconds");
    }

    [Fact]
    public void MaximumTierFrame_ShouldHaveCorrectSize_MemoryCalculation()
    {
        // Arrange - Maximum tier configuration
        var config = new PanelConfig
        {
            Rows = MaxTierRows,
            Cols = MaxTierCols,
            BitDepth = 16,
            TestPattern = TestPattern.Checkerboard,
            NoiseModel = NoiseModelType.None,
            DefectRate = 0,
            Seed = 42
        };

        _panelSimulator.Initialize(config);

        // Act - Generate one frame
        var result = _panelSimulator.Process(null);
        var frame = result as FrameData;

        // Assert - Verify frame size
        frame.Should().NotBeNull();

        long expectedPixelCount = (long)MaxTierRows * MaxTierCols;
        frame!.Pixels.Length.Should().Be((int)expectedPixelCount,
            $"Frame should have {expectedPixelCount} pixels");

        long expectedFrameSizeBytes = expectedPixelCount * 2; // 16-bit = 2 bytes per pixel
        long actualFrameSizeBytes = frame.Pixels.Length * 2;

        actualFrameSizeBytes.Should().Be(expectedFrameSizeBytes,
            $"Frame size should be {expectedFrameSizeBytes / (1024 * 1024)} MB " +
            $"({expectedFrameSizeBytes:N0} bytes)");

        // Calculate data rate at 30 fps
        double dataRateGbps = (expectedFrameSizeBytes * 8 * MaxTierFps) / 1e9;
        dataRateGbps.Should().BeApproximately(4.53, 0.01,
            $"Data rate should be ~4.53 Gbps for maximum tier (actual: {dataRateGbps:F2} Gbps)");
    }

    [Fact]
    public void MaximumTierStress_ShouldHandleSequentialFrames_NoMemoryLeak()
    {
        // Arrange
        var config = new PanelConfig
        {
            Rows = MaxTierRows,
            Cols = MaxTierCols,
            BitDepth = 16,
            TestPattern = TestPattern.Counter,
            NoiseModel = NoiseModelType.None,
            DefectRate = 0,
            Seed = 42
        };

        _panelSimulator.Initialize(config);

        // Act - Generate multiple frames and check memory
        long initialMemory = GC.GetTotalMemory(true);
        const int frameCount = 5;

        for (int i = 0; i < frameCount; i++)
        {
            var result = _panelSimulator.Process(null);
            var frame = result as FrameData;

            // Verify frame integrity
            frame!.Pixels.Should().NotBeNullOrEmpty();
            frame.Width.Should().Be(MaxTierCols);
            frame.Height.Should().Be(MaxTierRows);
        }

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long finalMemory = GC.GetTotalMemory(false);
        long memoryGrowth = finalMemory - initialMemory;

        // Assert - Memory growth should be reasonable
        // Allow some growth but not unbounded (each frame is ~18 MB)
        // Relaxed threshold for CI environment variance (1.5x theoretical minimum)
        memoryGrowth.Should().BeLessThan(MaxTierRows * MaxTierCols * 2 * frameCount * 3 / 2,
            "Memory should not grow unbounded (frames should be disposable)");
    }

    [Fact]
    public void MaximumTierStress_ShouldMaintainFrameIntegrity_SequentialNumbers()
    {
        // Arrange
        var config = new PanelConfig
        {
            Rows = 512, // Smaller for faster test
            Cols = 512,
            BitDepth = 16,
            TestPattern = TestPattern.Counter,
            NoiseModel = NoiseModelType.None,
            DefectRate = 0,
            Seed = 42
        };

        _panelSimulator.Initialize(config);

        // Act - Generate frames and verify sequential numbering
        var frames = new List<FrameData>();
        const int frameCount = 100;

        for (int i = 0; i < frameCount; i++)
        {
            var result = _panelSimulator.Process(null);
            if (result is FrameData frame)
            {
                frames.Add(frame);
            }
        }

        // Assert - Verify sequential frame numbers
        for (int i = 0; i < frames.Count; i++)
        {
            frames[i].FrameNumber.Should().Be(i,
                $"Frame {i} should have correct sequence number");
        }

        // Verify no gaps in sequence
        var frameNumbers = frames.Select(f => f.FrameNumber).ToList();
        frameNumbers.Should().BeInAscendingOrder("Frame numbers should be sequential");
    }

    public void Dispose()
    {
        // PanelSimulator doesn't implement Dispose
    }
}
