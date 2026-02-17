namespace FpgaSimulator.Core.Buffer;

using System.Collections.Immutable;

/// <summary>
/// Simulates the Ping-Pong Line Buffer from the FPGA RTL.
/// Models dual-bank BRAM with CDC-safe write/read isolation.
/// Implements fpga-design.md Section 4 behavior.
/// </summary>
public sealed class LineBufferSimulator
{
    private readonly object _lock = new();
    private readonly ImmutableArray<ushort>.Builder _bankA;
    private readonly ImmutableArray<ushort>.Builder _bankB;
    private readonly int _capacity;
    private int _writeBankIndex;
    private int _readBankIndex;
    private bool _hasOverflow;
    private int _totalLinesWritten;
    private int _totalLinesRead;

    /// <summary>
    /// Initializes a new instance with default 3072 pixel capacity.
    /// </summary>
    public LineBufferSimulator() : this(capacity: 3072)
    {
    }

    /// <summary>
    /// Initializes a new instance with specified capacity.
    /// </summary>
    /// <param name="capacity">Maximum pixels per line (max 3072)</param>
    public LineBufferSimulator(int capacity)
    {
        _capacity = Math.Min(capacity, 3072);
        _bankA = ImmutableArray.CreateBuilder<ushort>(_capacity);
        _bankB = ImmutableArray.CreateBuilder<ushort>(_capacity);
        _writeBankIndex = 0;
        _readBankIndex = 1;
        _hasOverflow = false;
        _totalLinesWritten = 0;
        _totalLinesRead = 0;
    }

    /// <summary>Buffer capacity in pixels</summary>
    public int Capacity => _capacity;

    /// <summary>Currently active write bank (0=A, 1=B)</summary>
    public int ActiveWriteBank
    {
        get
        {
            lock (_lock)
            {
                return _writeBankIndex;
            }
        }
    }

    /// <summary>Currently active read bank (1=B, 0=A, opposite of write)</summary>
    public int ActiveReadBank
    {
        get
        {
            lock (_lock)
            {
                return _readBankIndex;
            }
        }
    }

    /// <summary>True if both banks are empty</summary>
    public bool IsEmpty
    {
        get
        {
            lock (_lock)
            {
                return _bankA.Count == 0 && _bankB.Count == 0;
            }
        }
    }

    /// <summary>True if active write bank has reached capacity</summary>
    public bool IsActiveWriteBankFull
    {
        get
        {
            lock (_lock)
            {
                var activeBank = _writeBankIndex == 0 ? _bankA : _bankB;
                return activeBank.Count >= _capacity;
            }
        }
    }

    /// <summary>True if an overflow has occurred since last clear</summary>
    public bool HasOverflowed
    {
        get
        {
            lock (_lock)
            {
                return _hasOverflow;
            }
        }
    }

    /// <summary>
    /// Writes a line of pixel data to the active write bank.
    /// </summary>
    /// <param name="data">Pixel data array (16-bit values)</param>
    /// <returns>Success if written, or error if overflow/bank full</returns>
    public BufferResult<object> WriteLine(ushort[] data)
    {
        lock (_lock)
        {
            var activeBank = _writeBankIndex == 0 ? _bankA : _bankB;

            // Check if data would exceed capacity
            if (data.Length > _capacity)
            {
                _hasOverflow = true;
                return BufferResult<object>.Failure(BufferError.Overflow);
            }

            // Check if bank is full from previous write
            if (activeBank.Count > 0)
            {
                _hasOverflow = true;
                return BufferResult<object>.Failure(BufferError.BankFull);
            }

            // Write data to active bank
            activeBank.Clear();
            foreach (var pixel in data)
            {
                activeBank.Add(pixel);
            }

            _totalLinesWritten++;
            return BufferResult<object>.Success(new object());
        }
    }

    /// <summary>
    /// Reads a line of pixel data from the active read bank.
    /// Clears the bank after reading to allow reuse.
    /// </summary>
    /// <returns>Success with pixel data, or success with empty array if bank empty</returns>
    public BufferResult<ushort[]> ReadLine()
    {
        lock (_lock)
        {
            var activeBank = _readBankIndex == 0 ? _bankA : _bankB;

            if (activeBank.Count == 0)
            {
                return BufferResult<ushort[]>.Success(Array.Empty<ushort>());
            }

            var data = activeBank.ToArray();
            activeBank.Clear(); // Clear after reading to allow bank reuse
            _totalLinesRead++;
            return BufferResult<ushort[]>.Success(data);
        }
    }

    /// <summary>
    /// Toggles the active write bank (A <-> B).
    /// Called after each line is written to implement ping-pong.
    /// </summary>
    public void ToggleWriteBank()
    {
        lock (_lock)
        {
            _writeBankIndex = 1 - _writeBankIndex;
        }
    }

    /// <summary>
    /// Toggles the active read bank (A <-> B).
    /// Called after each line is read to implement ping-pong.
    /// </summary>
    public void ToggleReadBank()
    {
        lock (_lock)
        {
            _readBankIndex = 1 - _readBankIndex;
        }
    }

    /// <summary>
    /// Clears both banks and resets all counters.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _bankA.Clear();
            _bankB.Clear();
            _hasOverflow = false;
            _totalLinesWritten = 0;
            _totalLinesRead = 0;
        }
    }

    /// <summary>
    /// Clears the overflow flag.
    /// </summary>
    public void ClearOverflow()
    {
        lock (_lock)
        {
            _hasOverflow = false;
        }
    }

    /// <summary>
    /// Gets current status snapshot.
    /// </summary>
    public BufferStatus GetStatus()
    {
        lock (_lock)
        {
            return new BufferStatus
            {
                Capacity = _capacity,
                ActiveWriteBank = _writeBankIndex,
                ActiveReadBank = _readBankIndex,
                HasOverflow = _hasOverflow,
                WriteBankUsedCount = _writeBankIndex == 0 ? _bankA.Count : _bankB.Count,
                ReadBankUsedCount = _readBankIndex == 0 ? _bankA.Count : _bankB.Count,
                TotalLinesWritten = _totalLinesWritten,
                TotalLinesRead = _totalLinesRead
            };
        }
    }
}
