using System.IO;

namespace IntegrationTests.Helpers.Mock;

/// <summary>
/// Abstract file system interface for testing.
/// Enables in-memory file operations without disk I/O for hardware-independent testing.
/// </summary>
public interface IFileSystem
{
    /// <summary>
    /// Creates a directory at the specified path.
    /// Creates parent directories if they don't exist.
    /// </summary>
    /// <param name="path">The directory path to create.</param>
    void CreateDirectory(string path);

    /// <summary>
    /// Determines whether the specified directory exists.
    /// </summary>
    /// <param name="path">The directory path to check.</param>
    /// <returns>true if the directory exists; otherwise, false.</returns>
    bool DirectoryExists(string path);

    /// <summary>
    /// Determines whether the specified file exists.
    /// </summary>
    /// <param name="path">The file path to check.</param>
    /// <returns>true if the file exists; otherwise, false.</returns>
    bool FileExists(string path);

    /// <summary>
    /// Creates or overwrites a file at the specified path.
    /// Creates parent directories if they don't exist.
    /// </summary>
    /// <param name="path">The file path to create.</param>
    /// <returns>A stream for writing to the file.</returns>
    Stream CreateFile(string path);

    /// <summary>
    /// Opens an existing file for reading.
    /// </summary>
    /// <param name="path">The file path to open.</param>
    /// <returns>A read-only stream for the file.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file doesn't exist.</exception>
    Stream OpenRead(string path);

    /// <summary>
    /// Deletes a directory and optionally all subdirectories and files.
    /// </summary>
    /// <param name="path">The directory path to delete.</param>
    /// <param name="recursive">true to delete directories, subdirectories, and files; otherwise, false.</param>
    /// <exception cref="IOException">Thrown when directory is not empty and recursive is false.</exception>
    void DeleteDirectory(string path, bool recursive);

    /// <summary>
    /// Gets the path to the temporary directory.
    /// </summary>
    /// <returns>The path to the temporary directory.</returns>
    string GetTempPath();
}
