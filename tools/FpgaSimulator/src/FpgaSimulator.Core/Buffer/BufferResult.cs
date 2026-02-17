namespace FpgaSimulator.Core.Buffer;

/// <summary>
/// Result of a buffer operation.
/// </summary>
/// <typeparam name="T">Type of data returned on success</typeparam>
public readonly record struct BufferResult<T>
{
    /// <summary>True if the operation succeeded</summary>
    public bool IsSuccess { get; init; }

    /// <summary>Error type if operation failed</summary>
    public BufferError Error { get; init; }

    /// <summary>Data returned on success</summary>
    public T? Data { get; init; }

    /// <summary>
    /// Creates a successful result with data.
    /// </summary>
    public static BufferResult<T> Success(T data) => new()
    {
        IsSuccess = true,
        Error = BufferError.None,
        Data = data
    };

    /// <summary>
    /// Creates a failed result with error.
    /// </summary>
    public static BufferResult<T> Failure(BufferError error) => new()
    {
        IsSuccess = false,
        Error = error,
        Data = default
    };
}
