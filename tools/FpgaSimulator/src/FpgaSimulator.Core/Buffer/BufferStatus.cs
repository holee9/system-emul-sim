namespace FpgaSimulator.Core.Buffer;

/// <summary>
/// Status snapshot of the line buffer.
/// </summary>
public record BufferStatus
{
    /// <summary>Total buffer capacity (in pixels)</summary>
    public required int Capacity { get; init; }

    /// <summary>Currently active write bank (0 or 1)</summary>
    public required int ActiveWriteBank { get; init; }

    /// <summary>Currently active read bank (0 or 1)</summary>
    public required int ActiveReadBank { get; init; }

    /// <summary>Whether an overflow has occurred</summary>
    public required bool HasOverflow { get; init; }

    /// <summary>Number of elements used in active write bank</summary>
    public required int WriteBankUsedCount { get; init; }

    /// <summary>Number of elements used in active read bank</summary>
    public required int ReadBankUsedCount { get; init; }

    /// <summary>Total lines written since reset</summary>
    public required int TotalLinesWritten { get; init; }

    /// <summary>Total lines read since reset</summary>
    public required int TotalLinesRead { get; init; }
}
