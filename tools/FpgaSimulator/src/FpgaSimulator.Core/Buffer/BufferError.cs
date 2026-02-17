namespace FpgaSimulator.Core.Buffer;

/// <summary>
/// Error types for buffer operations.
/// </summary>
public enum BufferError
{
    /// <summary>No error</summary>
    None,

    /// <summary>Write data exceeds buffer capacity</summary>
    Overflow,

    /// <summary>Write bank is full, needs toggle</summary>
    BankFull,

    /// <summary>Invalid operation for current state</summary>
    InvalidState
}
