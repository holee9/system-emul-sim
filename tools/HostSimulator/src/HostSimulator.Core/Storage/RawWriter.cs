using Common.Dto.Dtos;

namespace HostSimulator.Core.Storage;

/// <summary>
/// Writes frames in RAW format (flat binary).
/// REQ-SIM-043: Save frames in RAW format (flat binary file, rows * cols * 2 bytes).
/// </summary>
public sealed class RawWriter : IFrameStorage
{
    /// <summary>
    /// Saves a frame to a RAW file.
    /// </summary>
    /// <param name="filePath">Destination file path.</param>
    /// <param name="frame">Frame data to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SaveAsync(string filePath, FrameData frame, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        if (frame == null)
            throw new ArgumentNullException(nameof(frame));

        // Convert ushort array to byte array (little-endian)
        int byteCount = frame.Pixels.Length * 2;
        var bytes = new byte[byteCount];

        for (int i = 0; i < frame.Pixels.Length; i++)
        {
            ushort pixel = frame.Pixels[i];
            bytes[i * 2] = (byte)(pixel & 0xFF); // Low byte
            bytes[i * 2 + 1] = (byte)((pixel >> 8) & 0xFF); // High byte
        }

        await File.WriteAllBytesAsync(filePath, bytes, cancellationToken);
    }

    /// <summary>
    /// Writes a frame to a RAW file (synchronous version).
    /// </summary>
    public async Task WriteAsync(string filePath, FrameData frame, CancellationToken cancellationToken = default)
    {
        await SaveAsync(filePath, frame, cancellationToken);
    }
}
