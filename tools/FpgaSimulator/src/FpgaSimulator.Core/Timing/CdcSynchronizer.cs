namespace FpgaSimulator.Core.Timing;

/// <summary>
/// Models a 2-stage flip-flop CDC (Clock Domain Crossing) synchronizer.
/// Simulates the latency introduced by crossing clock domain boundaries.
/// In RTL, this corresponds to a 2-FF synchronizer chain on single-bit signals.
/// </summary>
public sealed class CdcSynchronizer
{
    private readonly object _lock = new();
    private readonly int _stages;
    private readonly ClockDomain _sourceDomain;
    private readonly ClockDomain _destinationDomain;

    // Shift register for each synchronized signal (circular buffer approach)
    private readonly bool[] _pipeline;
    private int _writeIndex;

    /// <summary>
    /// Initializes a new CDC synchronizer with the specified parameters.
    /// </summary>
    /// <param name="sourceDomain">Source clock domain</param>
    /// <param name="destinationDomain">Destination clock domain</param>
    /// <param name="stages">Number of synchronizer stages (default: 2)</param>
    public CdcSynchronizer(ClockDomain sourceDomain, ClockDomain destinationDomain, int stages = 2)
    {
        _sourceDomain = sourceDomain;
        _destinationDomain = destinationDomain;
        _stages = Math.Max(stages, 2);
        _pipeline = new bool[_stages];
        _writeIndex = 0;
    }

    /// <summary>Source clock domain</summary>
    public ClockDomain SourceDomain => _sourceDomain;

    /// <summary>Destination clock domain</summary>
    public ClockDomain DestinationDomain => _destinationDomain;

    /// <summary>Number of synchronizer stages</summary>
    public int Stages => _stages;

    /// <summary>
    /// Latency in destination clock cycles introduced by the synchronizer.
    /// For a 2-stage synchronizer, worst case is stages + 1 destination clocks.
    /// </summary>
    public int LatencyCycles => _stages + 1;

    /// <summary>
    /// Estimated latency in nanoseconds based on destination clock period.
    /// </summary>
    public double LatencyNs => LatencyCycles * _destinationDomain.PeriodNs;

    /// <summary>
    /// Pushes a new input value into the synchronizer pipeline.
    /// Models the source domain registering a value.
    /// </summary>
    /// <param name="value">Input signal value</param>
    public void PushInput(bool value)
    {
        lock (_lock)
        {
            _pipeline[_writeIndex] = value;
            _writeIndex = (_writeIndex + 1) % _stages;
        }
    }

    /// <summary>
    /// Reads the synchronized output value (after all pipeline stages).
    /// Models the destination domain reading the synchronized value.
    /// </summary>
    /// <returns>Synchronized output value</returns>
    public bool GetOutput()
    {
        lock (_lock)
        {
            // Output is the oldest value in the pipeline
            return _pipeline[_writeIndex];
        }
    }

    /// <summary>
    /// Advances the pipeline by one destination clock cycle.
    /// Call this once per destination clock tick.
    /// </summary>
    /// <param name="inputValue">Current input value from source domain</param>
    /// <returns>Synchronized output value</returns>
    public bool Clock(bool inputValue)
    {
        lock (_lock)
        {
            // Read output before shifting (oldest value)
            bool output = _pipeline[_writeIndex];

            // Shift new value into pipeline
            _pipeline[_writeIndex] = inputValue;
            _writeIndex = (_writeIndex + 1) % _stages;

            return output;
        }
    }

    /// <summary>
    /// Resets all pipeline stages to false.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            Array.Clear(_pipeline);
            _writeIndex = 0;
        }
    }
}
