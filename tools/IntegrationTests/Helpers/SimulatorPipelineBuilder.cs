namespace IntegrationTests.Helpers;

/// <summary>
/// Performance tier configuration for simulator pipeline.
/// </summary>
public enum PerformanceTier
{
    /// <summary>Minimum performance configuration.</summary>
    Minimum,

    /// <summary>Target performance configuration.</summary>
    Target,

    /// <summary>Maximum performance configuration.</summary>
    Maximum
}

/// <summary>
/// Builder for setting up FPGA -> SoC -> Host simulator pipeline.
/// Configures performance tiers and manages pipeline lifecycle.
/// </summary>
public class SimulatorPipelineBuilder
{
    private PerformanceTier _tier = PerformanceTier.Target;
    private bool _isRunning = false;
    private readonly Dictionary<PerformanceTier, PipelineConfiguration> _configurations;

    /// <summary>
    /// Gets the current performance tier.
    /// </summary>
    public PerformanceTier CurrentTier => _tier;

    /// <summary>
    /// Gets whether the pipeline is currently running.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Event raised when pipeline state changes.
    /// </summary>
    public event EventHandler<PipelineStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Creates a new SimulatorPipelineBuilder with default configurations.
    /// </summary>
    public SimulatorPipelineBuilder()
    {
        _configurations = new Dictionary<PerformanceTier, PipelineConfiguration>
        {
            [PerformanceTier.Minimum] = new PipelineConfiguration(
                frameRate: 1,
                bufferSize: 64,
                parallelism: 1,
                "Minimum"
            ),
            [PerformanceTier.Target] = new PipelineConfiguration(
                frameRate: 30,
                bufferSize: 1024,
                parallelism: 4,
                "Target"
            ),
            [PerformanceTier.Maximum] = new PipelineConfiguration(
                frameRate: 60,
                bufferSize: 4096,
                parallelism: 8,
                "Maximum"
            )
        };
    }

    /// <summary>
    /// Builds and returns the pipeline configuration.
    /// </summary>
    /// <returns>Current pipeline configuration.</returns>
    public PipelineConfiguration BuildPipeline()
    {
        return _configurations[_tier];
    }

    /// <summary>
    /// Configures the pipeline for the specified performance tier.
    /// </summary>
    /// <param name="tier">Performance tier to configure.</param>
    /// <returns>This builder for method chaining.</returns>
    public SimulatorPipelineBuilder ConfigureForTier(PerformanceTier tier)
    {
        if (_isRunning)
            throw new InvalidOperationException("Cannot change tier while pipeline is running. Stop the pipeline first.");

        _tier = tier;
        OnStateChanged(new PipelineStateChangedEventArgs(tier, _configurations[tier], false));
        return this;
    }

    /// <summary>
    /// Starts the pipeline asynchronously.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
            throw new InvalidOperationException("Pipeline is already running.");

        _isRunning = true;
        OnStateChanged(new PipelineStateChangedEventArgs(_tier, _configurations[_tier], true));

        // In a real implementation, this would start the actual simulators
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the pipeline asynchronously.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
            throw new InvalidOperationException("Pipeline is not running.");

        _isRunning = false;
        OnStateChanged(new PipelineStateChangedEventArgs(_tier, _configurations[_tier], false));

        // In a real implementation, this would stop the actual simulators
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the configuration for a specific tier.
    /// </summary>
    /// <param name="tier">Performance tier.</param>
    /// <returns>Pipeline configuration for the tier.</returns>
    public PipelineConfiguration GetConfiguration(PerformanceTier tier)
    {
        return _configurations[tier];
    }

    /// <summary>
    /// Creates a custom configuration for a tier.
    /// </summary>
    /// <param name="tier">Tier to configure.</param>
    /// <param name="configuration">Custom configuration.</param>
    public void SetCustomConfiguration(PerformanceTier tier, PipelineConfiguration configuration)
    {
        if (_isRunning)
            throw new InvalidOperationException("Cannot modify configuration while pipeline is running.");

        _configurations[tier] = configuration;
    }

    private void OnStateChanged(PipelineStateChangedEventArgs e)
    {
        StateChanged?.Invoke(this, e);
    }
}

/// <summary>
/// Pipeline configuration parameters.
/// </summary>
public sealed class PipelineConfiguration
{
    /// <summary>Target frame rate in fps.</summary>
    public int FrameRate { get; }

    /// <summary>Buffer size in frames.</summary>
    public int BufferSize { get; }

    /// <summary>Parallelism level.</summary>
    public int Parallelism { get; }

    /// <summary>Configuration name/description.</summary>
    public string Name { get; }

    public PipelineConfiguration(int frameRate, int bufferSize, int parallelism, string name)
    {
        FrameRate = frameRate;
        BufferSize = bufferSize;
        Parallelism = parallelism;
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public override string ToString()
    {
        return $"PipelineConfiguration: {Name}, {FrameRate} fps, Buffer={BufferSize}, Parallelism={Parallelism}";
    }
}

/// <summary>
/// Event arguments for pipeline state changes.
/// </summary>
public sealed class PipelineStateChangedEventArgs : EventArgs
{
    /// <summary>Performance tier.</summary>
    public PerformanceTier Tier { get; }

    /// <summary>Pipeline configuration.</summary>
    public PipelineConfiguration Configuration { get; }

    /// <summary>Whether pipeline is running.</summary>
    public bool IsRunning { get; }

    public PipelineStateChangedEventArgs(PerformanceTier tier, PipelineConfiguration configuration, bool isRunning)
    {
        Tier = tier;
        Configuration = configuration;
        IsRunning = isRunning;
    }
}
