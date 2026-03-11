using System.IO;
using System.Collections.Concurrent;

namespace IntegrationTests.Helpers.Mock;

/// <summary>
/// In-memory file system implementation for hardware-independent testing.
/// Provides full file system semantics without actual disk I/O operations.
/// </summary>
public sealed class MemoryFileSystem : IFileSystem
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _directories = new();

    public MemoryFileSystem()
    {
        // Initialize root and temp directories
        _directories.TryAdd("/", new HashSet<string>());
        string tempPath = GetTempPath();
        _directories.TryAdd(tempPath, new HashSet<string>());
    }

    /// <inheritdoc/>
    public void CreateDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string normalizedPath = NormalizePath(path);

        // Create parent directories if they don't exist
        string parentPath = GetParentPath(normalizedPath);
        if (!string.IsNullOrEmpty(parentPath) && !_directories.ContainsKey(parentPath))
        {
            CreateDirectory(parentPath);
        }

        // Add directory to parent's child list
        if (!string.IsNullOrEmpty(parentPath) && _directories.TryGetValue(parentPath, out var children))
        {
            children.Add(normalizedPath);
        }

        // Create the directory if it doesn't exist
        _directories.TryAdd(normalizedPath, new HashSet<string>());
    }

    /// <inheritdoc/>
    public bool DirectoryExists(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string normalizedPath = NormalizePath(path);
        return _directories.ContainsKey(normalizedPath);
    }

    /// <inheritdoc/>
    public bool FileExists(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string normalizedPath = NormalizePath(path);
        return _files.ContainsKey(normalizedPath);
    }

    /// <inheritdoc/>
    public Stream CreateFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string normalizedPath = NormalizePath(path);

        // Create parent directories if they don't exist
        string parentPath = GetParentPath(normalizedPath);
        if (!string.IsNullOrEmpty(parentPath) && !_directories.ContainsKey(parentPath))
        {
            CreateDirectory(parentPath);
        }

        // Return a memory stream that writes to the file dictionary on disposal
        return new MemoryFileStream(
            normalizedPath,
            _files,
            canWrite: true,
            parentDirectory: parentPath,
            directories: _directories);
    }

    /// <inheritdoc/>
    public Stream OpenRead(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string normalizedPath = NormalizePath(path);

        if (!_files.TryGetValue(normalizedPath, out byte[]? content))
        {
            throw new FileNotFoundException($"File not found: {path}", path);
        }

        return new MemoryFileStream(
            normalizedPath,
            _files,
            canWrite: false,
            parentDirectory: null,
            directories: _directories);
    }

    /// <inheritdoc/>
    public void DeleteDirectory(string path, bool recursive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string normalizedPath = NormalizePath(path);

        if (!_directories.TryGetValue(normalizedPath, out var children))
        {
            return; // Directory doesn't exist, nothing to delete
        }

        if (!recursive && children.Count > 0)
        {
            throw new IOException($"Directory not empty: {path}");
        }

        // Recursively delete all children
        foreach (string child in children.ToList())
        {
            if (_directories.ContainsKey(child))
            {
                DeleteDirectory(child, recursive: true);
            }
        }

        // Delete all files in this directory
        foreach (string filePath in _files.Keys.ToList())
        {
            if (filePath.StartsWith(normalizedPath + "/", StringComparison.Ordinal))
            {
                _files.TryRemove(filePath, out _);
            }
        }

        // Remove from parent's child list
        string parentPath = GetParentPath(normalizedPath);
        if (!string.IsNullOrEmpty(parentPath) && _directories.TryGetValue(parentPath, out var siblings))
        {
            siblings.Remove(normalizedPath);
        }

        // Remove the directory itself
        _directories.TryRemove(normalizedPath, out _);
    }

    /// <inheritdoc/>
    public string GetTempPath() => "/tmp";

    private static string NormalizePath(string path)
    {
        // Convert backslashes to forward slashes
        string normalized = path.Replace('\\', '/');

        // Remove leading drive letter (e.g., "C:")
        if (normalized.Length >= 2 && normalized[1] == ':')
        {
            normalized = normalized.Substring(2);
        }

        // Ensure path starts with /
        if (!normalized.StartsWith('/'))
        {
            normalized = '/' + normalized;
        }

        // Remove trailing slash (except for root)
        if (normalized.Length > 1 && normalized.EndsWith('/'))
        {
            normalized = normalized.TrimEnd('/');
        }

        return normalized;
    }

    private static string GetParentPath(string path)
    {
        if (path == "/" || !path.Contains('/'))
        {
            return "/";
        }

        int lastSlash = path.LastIndexOf('/');
        if (lastSlash == 0)
        {
            return "/";
        }

        return path.Substring(0, lastSlash);
    }

    /// <summary>
    /// Memory-based file stream that writes to the file dictionary on disposal.
    /// </summary>
    private sealed class MemoryFileStream : Stream
    {
        private readonly string _path;
        private readonly ConcurrentDictionary<string, byte[]> _files;
        private readonly bool _canWrite;
        private readonly string? _parentDirectory;
        private readonly ConcurrentDictionary<string, HashSet<string>> _directories;
        private readonly MemoryStream _innerStream;
        private bool _disposed;

        public MemoryFileStream(
            string path,
            ConcurrentDictionary<string, byte[]> files,
            bool canWrite,
            string? parentDirectory,
            ConcurrentDictionary<string, HashSet<string>> directories)
        {
            _path = path;
            _files = files;
            _canWrite = canWrite;
            _parentDirectory = parentDirectory;
            _directories = directories;

            if (!canWrite)
            {
                // Initialize stream with existing content for reading
                byte[] content = files[path];
                _innerStream = new MemoryStream(content, writable: false);
            }
            else
            {
                // Create writable stream
                _innerStream = new MemoryStream();
            }
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => _canWrite;
        public override long Length => _innerStream.Length;
        public override long Position
        {
            get => _innerStream.Position;
            set => _innerStream.Position = value;
        }

        public override void Flush() => _innerStream.Flush();

        public override int Read(byte[] buffer, int offset, int count) =>
            _innerStream.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) =>
            _innerStream.Seek(offset, origin);

        public override void SetLength(long value) => _innerStream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) =>
            _innerStream.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing && _canWrite)
                {
                    // Save content to dictionary on disposal
                    _innerStream.Position = 0;
                    _files[_path] = _innerStream.ToArray();

                    // Add to parent directory's file list
                    if (!string.IsNullOrEmpty(_parentDirectory) &&
                        _directories.TryGetValue(_parentDirectory, out var children))
                    {
                        children.Add(_path);
                    }
                }

                _innerStream.Dispose();
                _disposed = true;
            }

            base.Dispose(disposing);
        }
    }
}
