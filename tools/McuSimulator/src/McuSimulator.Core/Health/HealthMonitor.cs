namespace McuSimulator.Core.Health;

/// <summary>
/// MCU health monitoring subsystem (1:1 port from fw/include/health_monitor.h).
/// Tracks watchdog liveness, runtime statistics, and system status.
/// </summary>
public sealed class HealthMonitor
{
    /// <summary>Interval at which the watchdog must be petted (ms).</summary>
    public const int WatchdogPetIntervalMs = 1000;

    /// <summary>Maximum time without a pet before the watchdog considers the system dead (ms).</summary>
    public const int WatchdogTimeoutMs = 5000;

    /// <summary>Maximum allowed latency for a status response (ms).</summary>
    public const int StatusResponseMaxMs = 50;

    private readonly ISimulationClock _clock;
    private readonly RuntimeStatistics _stats = new();
    private readonly List<(LogLevel Level, string Module, string Message, long TimestampMs)> _logEntries = new();
    private readonly object _lock = new();

    private long _startTimeMs;
    private long _lastPetTimeMs;
    private bool _isAlive;
    private LogLevel _logLevel = LogLevel.Info;

    /// <summary>
    /// Initializes a new <see cref="HealthMonitor"/> instance.
    /// </summary>
    /// <param name="clock">Optional clock for deterministic testing; defaults to <see cref="SystemClock"/>.</param>
    public HealthMonitor(ISimulationClock? clock = null)
    {
        _clock = clock ?? new SystemClock();
        var now = _clock.GetCurrentTimeMs();
        _startTimeMs = now;
        _lastPetTimeMs = now;
        _isAlive = true;
    }

    /// <summary>
    /// Gets a value indicating whether the system is alive.
    /// Returns <c>true</c> if the elapsed time since the last pet is within
    /// <see cref="WatchdogTimeoutMs"/>. When the timeout is exceeded the first time,
    /// <see cref="RuntimeStatistics.WatchdogResets"/> is incremented.
    /// </summary>
    public bool IsAlive
    {
        get
        {
            lock (_lock)
            {
                if (!_isAlive)
                    return false;

                var elapsed = _clock.GetCurrentTimeMs() - _lastPetTimeMs;
                if (elapsed > WatchdogTimeoutMs)
                {
                    _stats.WatchdogResets++;
                    _isAlive = false;
                    return false;
                }

                return true;
            }
        }
    }

    /// <summary>
    /// Gets or sets the current log filter level.
    /// Only messages at or above this level are stored.
    /// </summary>
    public LogLevel LogLevel
    {
        get { lock (_lock) return _logLevel; }
        set { lock (_lock) _logLevel = value; }
    }

    /// <summary>
    /// Records a watchdog pet, resetting the liveness timer.
    /// </summary>
    public void PetWatchdog()
    {
        lock (_lock)
        {
            _lastPetTimeMs = _clock.GetCurrentTimeMs();
            _isAlive = true;
        }
    }

    /// <summary>
    /// Increments a named counter in <see cref="RuntimeStatistics"/> by the given delta.
    /// </summary>
    /// <param name="name">
    /// Counter name using firmware snake_case convention:
    /// frames_received, frames_sent, frames_dropped, spi_errors, csi2_errors,
    /// packets_sent, bytes_sent, auth_failures, watchdog_resets.
    /// </param>
    /// <param name="delta">Value to add to the counter.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is not a known counter.</exception>
    public void UpdateStat(string name, long delta)
    {
        lock (_lock)
        {
            switch (name)
            {
                case "frames_received":
                    _stats.FramesReceived += delta;
                    break;
                case "frames_sent":
                    _stats.FramesSent += delta;
                    break;
                case "frames_dropped":
                    _stats.FramesDropped += delta;
                    break;
                case "spi_errors":
                    _stats.SpiErrors += delta;
                    break;
                case "csi2_errors":
                    _stats.Csi2Errors += delta;
                    break;
                case "packets_sent":
                    _stats.PacketsSent += delta;
                    break;
                case "bytes_sent":
                    _stats.BytesSent += (ulong)delta;
                    break;
                case "auth_failures":
                    _stats.AuthFailures += delta;
                    break;
                case "watchdog_resets":
                    _stats.WatchdogResets += delta;
                    break;
                default:
                    throw new ArgumentException($"Unknown stat counter: '{name}'", nameof(name));
            }
        }
    }

    /// <summary>
    /// Returns a deep copy of the current runtime statistics.
    /// </summary>
    public RuntimeStatistics GetStats()
    {
        lock (_lock)
        {
            return _stats.Clone();
        }
    }

    /// <summary>
    /// Builds a point-in-time <see cref="SystemStatus"/> snapshot.
    /// </summary>
    /// <param name="sequenceState">Current sequence engine state byte.</param>
    /// <returns>A fully populated <see cref="SystemStatus"/>.</returns>
    public SystemStatus GetStatus(byte sequenceState)
    {
        lock (_lock)
        {
            var now = _clock.GetCurrentTimeMs();
            return new SystemStatus
            {
                State = sequenceState,
                Stats = _stats.Clone(),
                BatterySoc = 100,
                BatteryMv = 4200,
                UptimeSec = (uint)((now - _startTimeMs) / 1000),
                FpgaTemp = 0
            };
        }
    }

    /// <summary>
    /// Records a log entry if the given level meets or exceeds the current filter threshold.
    /// </summary>
    /// <param name="level">Severity of the log entry.</param>
    /// <param name="module">Source module name.</param>
    /// <param name="message">Log message text.</param>
    public void Log(LogLevel level, string module, string message)
    {
        lock (_lock)
        {
            if (level >= _logLevel)
            {
                _logEntries.Add((level, module, message, _clock.GetCurrentTimeMs()));
            }
        }
    }

    /// <summary>
    /// Sets the minimum log level filter.
    /// </summary>
    /// <param name="level">New minimum severity threshold.</param>
    public void SetLogLevel(LogLevel level)
    {
        lock (_lock)
        {
            _logLevel = level;
        }
    }

    /// <summary>
    /// Resets all internal state to initial values, as if freshly constructed.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            var now = _clock.GetCurrentTimeMs();
            _startTimeMs = now;
            _lastPetTimeMs = now;
            _isAlive = true;
            _logLevel = LogLevel.Info;

            _stats.FramesReceived = 0;
            _stats.FramesSent = 0;
            _stats.FramesDropped = 0;
            _stats.SpiErrors = 0;
            _stats.Csi2Errors = 0;
            _stats.PacketsSent = 0;
            _stats.BytesSent = 0;
            _stats.AuthFailures = 0;
            _stats.WatchdogResets = 0;

            _logEntries.Clear();
        }
    }
}
