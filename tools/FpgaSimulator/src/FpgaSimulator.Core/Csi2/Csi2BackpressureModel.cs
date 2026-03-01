namespace FpgaSimulator.Core.Csi2;

/// <summary>
/// Simulates AXI4-Stream backpressure for the CSI-2 TX path.
/// Models tready/tvalid flow control between the line buffer and CSI-2 TX.
/// When tready is deasserted, the TX must stall and hold data.
/// </summary>
public sealed class Csi2BackpressureModel
{
    private readonly object _lock = new();
    private readonly int _fifoDepth;
    private int _fifoLevel;
    private int _stallCycles;
    private long _totalBytesTransferred;
    private long _totalStallCycles;

    /// <summary>
    /// Initializes a new instance with specified FIFO depth.
    /// </summary>
    /// <param name="fifoDepth">Internal FIFO depth in bytes (default: 256)</param>
    public Csi2BackpressureModel(int fifoDepth = 256)
    {
        _fifoDepth = Math.Max(fifoDepth, 1);
        _fifoLevel = 0;
        _stallCycles = 0;
        _totalBytesTransferred = 0;
        _totalStallCycles = 0;
    }

    /// <summary>FIFO depth in bytes</summary>
    public int FifoDepth => _fifoDepth;

    /// <summary>Current FIFO fill level in bytes</summary>
    public int FifoLevel
    {
        get { lock (_lock) { return _fifoLevel; } }
    }

    /// <summary>
    /// True when the TX path can accept data (tready asserted).
    /// Deasserted when FIFO is full.
    /// </summary>
    public bool TReady
    {
        get { lock (_lock) { return _fifoLevel < _fifoDepth; } }
    }

    /// <summary>
    /// True when data is being presented on the AXI4-Stream interface (tvalid).
    /// Set by the data producer.
    /// </summary>
    public bool TValid { get; private set; }

    /// <summary>
    /// True when a valid data transfer occurs (tready AND tvalid).
    /// </summary>
    public bool TransferActive
    {
        get { lock (_lock) { return TReady && TValid; } }
    }

    /// <summary>Number of consecutive stall cycles (tvalid high, tready low)</summary>
    public int StallCycles
    {
        get { lock (_lock) { return _stallCycles; } }
    }

    /// <summary>Total bytes successfully transferred</summary>
    public long TotalBytesTransferred
    {
        get { lock (_lock) { return _totalBytesTransferred; } }
    }

    /// <summary>Total accumulated stall cycles</summary>
    public long TotalStallCycles
    {
        get { lock (_lock) { return _totalStallCycles; } }
    }

    /// <summary>
    /// Asserts tvalid, indicating the producer has data to send.
    /// </summary>
    public void AssertValid()
    {
        lock (_lock)
        {
            TValid = true;
        }
    }

    /// <summary>
    /// Deasserts tvalid, indicating the producer has no data.
    /// </summary>
    public void DeassertValid()
    {
        lock (_lock)
        {
            TValid = false;
            _stallCycles = 0;
        }
    }

    /// <summary>
    /// Processes one clock cycle of the backpressure model.
    /// A transfer occurs when both tready and tvalid are asserted.
    /// </summary>
    /// <param name="bytesPerBeat">Bytes per AXI4-Stream beat (default: 4 for 32-bit bus)</param>
    /// <returns>True if a data transfer occurred this cycle</returns>
    public bool ProcessCycle(int bytesPerBeat = 4)
    {
        lock (_lock)
        {
            if (TValid && _fifoLevel < _fifoDepth)
            {
                // Transfer occurs: tready AND tvalid
                _fifoLevel = Math.Min(_fifoLevel + bytesPerBeat, _fifoDepth);
                _totalBytesTransferred += bytesPerBeat;
                _stallCycles = 0;
                return true;
            }

            if (TValid && _fifoLevel >= _fifoDepth)
            {
                // Stall: tvalid high but tready low (FIFO full)
                _stallCycles++;
                _totalStallCycles++;
            }

            return false;
        }
    }

    /// <summary>
    /// Drains bytes from the FIFO (consumed by the CSI-2 PHY layer).
    /// </summary>
    /// <param name="bytes">Number of bytes consumed</param>
    public void DrainFifo(int bytes)
    {
        lock (_lock)
        {
            _fifoLevel = Math.Max(_fifoLevel - bytes, 0);
        }
    }

    /// <summary>
    /// Resets the backpressure model to initial state.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _fifoLevel = 0;
            _stallCycles = 0;
            _totalBytesTransferred = 0;
            _totalStallCycles = 0;
            TValid = false;
        }
    }
}
