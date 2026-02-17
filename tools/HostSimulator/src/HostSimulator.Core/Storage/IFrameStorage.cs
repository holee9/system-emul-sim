using Common.Dto.Dtos;

namespace HostSimulator.Core.Storage;

/// <summary>
/// Interface for frame storage implementations.
/// </summary>
public interface IFrameStorage
{
    /// <summary>
    /// Saves a frame to storage.
    /// </summary>
    /// <param name="filePath">Destination file path.</param>
    /// <param name="frame">Frame data to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(string filePath, FrameData frame, CancellationToken cancellationToken = default);
}
