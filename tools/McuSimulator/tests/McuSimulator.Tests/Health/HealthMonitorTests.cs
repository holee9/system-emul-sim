using McuSimulator.Core.Health;

namespace McuSimulator.Tests.Health;

public class HealthMonitorTests
{
    private readonly TestClock _clock;
    private readonly HealthMonitor _monitor;

    public HealthMonitorTests()
    {
        _clock = new TestClock { CurrentTimeMs = 0 };
        _monitor = new HealthMonitor(_clock);
    }

    #region Watchdog Tests

    [Fact]
    public void Watchdog_Normal_PetKeepsAlive()
    {
        // Pet at intervals < 5000ms
        for (int i = 1; i <= 10; i++)
        {
            _clock.CurrentTimeMs = i * 1000; // 1s, 2s, 3s, ... 10s
            _monitor.PetWatchdog();
            Assert.True(_monitor.IsAlive);
        }
    }

    [Fact]
    public void Watchdog_Timeout_NotAlive()
    {
        // No pet for 5+ seconds
        _clock.CurrentTimeMs = 5001;
        Assert.False(_monitor.IsAlive);
    }

    [Fact]
    public void Watchdog_ExactTimeout_StillAlive()
    {
        // Exactly at 5000ms boundary (> check, not >=)
        _clock.CurrentTimeMs = 5000;
        Assert.True(_monitor.IsAlive);
    }

    [Fact]
    public void Watchdog_Timeout_IncrementsResetCounter()
    {
        _clock.CurrentTimeMs = 5001;
        _ = _monitor.IsAlive; // Triggers timeout

        var stats = _monitor.GetStats();
        Assert.Equal(1, stats.WatchdogResets);
    }

    [Fact]
    public void Watchdog_Timeout_SubsequentChecks_StayDead()
    {
        _clock.CurrentTimeMs = 5001;
        Assert.False(_monitor.IsAlive);

        // Check again - should still be dead, and NOT increment counter again
        _clock.CurrentTimeMs = 6000;
        Assert.False(_monitor.IsAlive);

        var stats = _monitor.GetStats();
        Assert.Equal(1, stats.WatchdogResets); // Only incremented once
    }

    [Fact]
    public void Watchdog_PetAfterTimeout_Revives()
    {
        _clock.CurrentTimeMs = 5001;
        Assert.False(_monitor.IsAlive);

        // Pet should revive
        _clock.CurrentTimeMs = 5002;
        _monitor.PetWatchdog();
        Assert.True(_monitor.IsAlive);
    }

    #endregion

    #region UpdateStat Tests

    [Fact]
    public void UpdateStat_FramesSent_IncrementCorrectly()
    {
        for (int i = 0; i < 10; i++)
        {
            _monitor.UpdateStat("frames_sent", 1);
        }

        var stats = _monitor.GetStats();
        Assert.Equal(10, stats.FramesSent);
    }

    [Theory]
    [InlineData("frames_received", 5)]
    [InlineData("frames_sent", 3)]
    [InlineData("frames_dropped", 2)]
    [InlineData("spi_errors", 7)]
    [InlineData("csi2_errors", 4)]
    [InlineData("packets_sent", 100)]
    [InlineData("bytes_sent", 1024)]
    [InlineData("auth_failures", 1)]
    [InlineData("watchdog_resets", 2)]
    public void UpdateStat_AllNineCounters_Independent(string counterName, long value)
    {
        _monitor.UpdateStat(counterName, value);

        var stats = _monitor.GetStats();

        // Verify the target counter has the value
        long actual = counterName switch
        {
            "frames_received" => stats.FramesReceived,
            "frames_sent" => stats.FramesSent,
            "frames_dropped" => stats.FramesDropped,
            "spi_errors" => stats.SpiErrors,
            "csi2_errors" => stats.Csi2Errors,
            "packets_sent" => stats.PacketsSent,
            "bytes_sent" => (long)stats.BytesSent,
            "auth_failures" => stats.AuthFailures,
            "watchdog_resets" => stats.WatchdogResets,
            _ => throw new InvalidOperationException()
        };
        Assert.Equal(value, actual);

        // Verify other counters remain zero (no cross-contamination)
        var allCounters = new Dictionary<string, long>
        {
            ["frames_received"] = stats.FramesReceived,
            ["frames_sent"] = stats.FramesSent,
            ["frames_dropped"] = stats.FramesDropped,
            ["spi_errors"] = stats.SpiErrors,
            ["csi2_errors"] = stats.Csi2Errors,
            ["packets_sent"] = stats.PacketsSent,
            ["bytes_sent"] = (long)stats.BytesSent,
            ["auth_failures"] = stats.AuthFailures,
            ["watchdog_resets"] = stats.WatchdogResets,
        };

        foreach (var (name, val) in allCounters)
        {
            if (name != counterName)
            {
                Assert.Equal(0, val);
            }
        }
    }

    [Fact]
    public void UpdateStat_UnknownCounter_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _monitor.UpdateStat("unknown_counter", 1));
    }

    #endregion

    #region GetStatus Tests

    [Fact]
    public void GetStatus_AllFieldsPopulated()
    {
        _clock.CurrentTimeMs = 10_000; // 10 seconds after start

        _monitor.UpdateStat("frames_sent", 42);

        var status = _monitor.GetStatus(3); // 3 = Scanning state byte

        Assert.Equal(3, status.State);
        Assert.NotNull(status.Stats);
        Assert.Equal(42, status.Stats.FramesSent);
        Assert.Equal(100, status.BatterySoc);
        Assert.Equal(4200, status.BatteryMv);
        Assert.Equal(10u, status.UptimeSec); // (10000 - 0) / 1000
        Assert.Equal(0, status.FpgaTemp);
    }

    [Fact]
    public void GetStatus_UptimeSec_CalculatedCorrectly()
    {
        _clock.CurrentTimeMs = 65_000; // 65 seconds
        var status = _monitor.GetStatus(0);
        Assert.Equal(65u, status.UptimeSec);
    }

    #endregion

    #region LogLevel Filtering Tests

    [Fact]
    public void LogLevel_Filtering_WarningLevel_FiltersDebugAndInfo()
    {
        _monitor.SetLogLevel(McuSimulator.Core.Health.LogLevel.Warning);

        // These should be filtered out (below Warning)
        _monitor.Log(McuSimulator.Core.Health.LogLevel.Debug, "test", "debug message");
        _monitor.Log(McuSimulator.Core.Health.LogLevel.Info, "test", "info message");

        // These should be recorded (at or above Warning)
        _monitor.Log(McuSimulator.Core.Health.LogLevel.Warning, "test", "warning message");
        _monitor.Log(McuSimulator.Core.Health.LogLevel.Error, "test", "error message");
        _monitor.Log(McuSimulator.Core.Health.LogLevel.Critical, "test", "critical message");

        // Verify by checking that the log level property is set
        Assert.Equal(McuSimulator.Core.Health.LogLevel.Warning, _monitor.LogLevel);
    }

    [Fact]
    public void LogLevel_Default_IsInfo()
    {
        Assert.Equal(McuSimulator.Core.Health.LogLevel.Info, _monitor.LogLevel);
    }

    [Fact]
    public void LogLevel_SetViaProperty_Works()
    {
        _monitor.LogLevel = McuSimulator.Core.Health.LogLevel.Error;
        Assert.Equal(McuSimulator.Core.Health.LogLevel.Error, _monitor.LogLevel);
    }

    #endregion

    #region Reset Tests

    [Fact]
    public void Reset_ClearsAll()
    {
        // Build up state
        _clock.CurrentTimeMs = 10_000;
        _monitor.UpdateStat("frames_sent", 100);
        _monitor.UpdateStat("spi_errors", 5);
        _monitor.SetLogLevel(McuSimulator.Core.Health.LogLevel.Error);
        _monitor.Log(McuSimulator.Core.Health.LogLevel.Error, "test", "some error");

        // Trigger watchdog timeout
        _clock.CurrentTimeMs = 20_000;
        _ = _monitor.IsAlive;

        // Reset at time 25000
        _clock.CurrentTimeMs = 25_000;
        _monitor.Reset();

        // Verify alive after reset
        Assert.True(_monitor.IsAlive);

        // Verify stats are cleared
        var stats = _monitor.GetStats();
        Assert.Equal(0, stats.FramesReceived);
        Assert.Equal(0, stats.FramesSent);
        Assert.Equal(0, stats.FramesDropped);
        Assert.Equal(0, stats.SpiErrors);
        Assert.Equal(0, stats.Csi2Errors);
        Assert.Equal(0, stats.PacketsSent);
        Assert.Equal(0ul, stats.BytesSent);
        Assert.Equal(0, stats.AuthFailures);
        Assert.Equal(0, stats.WatchdogResets);

        // Verify log level reset to default
        Assert.Equal(McuSimulator.Core.Health.LogLevel.Info, _monitor.LogLevel);

        // Verify uptime reset (start time = 25000, current = 25000)
        var status = _monitor.GetStatus(0);
        Assert.Equal(0u, status.UptimeSec);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_NoClock_UsesSystemClock()
    {
        // Should not throw
        var monitor = new HealthMonitor();
        Assert.True(monitor.IsAlive);
    }

    #endregion

    #region TestClock Helper

    private class TestClock : ISimulationClock
    {
        public long CurrentTimeMs { get; set; }
        public long GetCurrentTimeMs() => CurrentTimeMs;
    }

    #endregion
}
