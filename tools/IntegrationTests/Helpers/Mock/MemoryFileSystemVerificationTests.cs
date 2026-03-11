using FluentAssertions;
using Xunit;
using System.IO;

namespace IntegrationTests.Helpers.Mock;

/// <summary>
/// Verification tests to ensure MemoryFileSystem performs no disk I/O.
/// These tests confirm the hardware-independent requirement from SPEC-INTSIM-001.
/// </summary>
public class MemoryFileSystemVerificationTests
{
    [Fact]
    public void MemoryFileSystem_ShouldNotCreateFilesOnDisk()
    {
        // Arrange
        var fs = new MemoryFileSystem();
        string testPath = "/test/verification/file.txt";
        string diskPath = Path.Combine(Path.GetTempPath(), "test", "verification", "file.txt");

        // Ensure cleanup from any previous failed test
        if (File.Exists(diskPath))
        {
            File.Delete(diskPath);
        }

        try
        {
            // Act - Create file in memory
            using (var stream = fs.CreateFile(testPath))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write("This should NOT be on disk");
            }

            // Assert - Verify no file was created on disk
            File.Exists(diskPath).Should().BeFalse(
                "MemoryFileSystem should not create files on the actual disk");
        }
        finally
        {
            // Cleanup (just in case)
            if (File.Exists(diskPath))
            {
                File.Delete(diskPath);
            }
        }
    }

    [Fact]
    public void MemoryFileSystem_ShouldNotCreateDirectoriesOnDisk()
    {
        // Arrange
        var fs = new MemoryFileSystem();
        string testPath = "/test/verification/directory";
        string diskPath = Path.Combine(Path.GetTempPath(), "test", "verification", "directory");

        // Ensure cleanup from any previous failed test
        if (Directory.Exists(diskPath))
        {
            Directory.Delete(diskPath, recursive: true);
        }

        try
        {
            // Act - Create directory in memory
            fs.CreateDirectory(testPath);

            // Assert - Verify no directory was created on disk
            Directory.Exists(diskPath).Should().BeFalse(
                "MemoryFileSystem should not create directories on the actual disk");
        }
        finally
        {
            // Cleanup (just in case)
            if (Directory.Exists(diskPath))
            {
                Directory.Delete(diskPath, recursive: true);
            }
        }
    }

    [Fact]
    public void MemoryFileSystem_MultipleInstances_ShouldBeIsolated()
    {
        // Arrange
        var fs1 = new MemoryFileSystem();
        var fs2 = new MemoryFileSystem();
        string filePath = "/test/file.txt";

        // Act - Create same path in different instances
        using (var stream = fs1.CreateFile(filePath))
        using (var writer = new StreamWriter(stream))
        {
            writer.Write("Instance 1");
        }

        using (var stream = fs2.CreateFile(filePath))
        using (var writer = new StreamWriter(stream))
        {
            writer.Write("Instance 2");
        }

        // Assert - Each instance should have independent state
        fs1.FileExists(filePath).Should().BeTrue();
        fs2.FileExists(filePath).Should().BeTrue();

        using (var stream1 = fs1.OpenRead(filePath))
        using (var reader1 = new StreamReader(stream1))
        using (var stream2 = fs2.OpenRead(filePath))
        using (var reader2 = new StreamReader(stream2))
        {
            reader1.ReadToEnd().Should().Be("Instance 1");
            reader2.ReadToEnd().Should().Be("Instance 2");
        }
    }

    [Fact]
    public void MemoryFileSystem_DisposeStream_ShouldKeepContentInMemory()
    {
        // Arrange
        var fs = new MemoryFileSystem();
        string filePath = "/test/file.txt";
        string expectedContent = "Content after disposal";

        // Act - Create and write file, then dispose stream
        using (var stream = fs.CreateFile(filePath))
        using (var writer = new StreamWriter(stream))
        {
            writer.Write(expectedContent);
        }

        // Assert - Content should still be accessible after disposal
        fs.FileExists(filePath).Should().BeTrue();

        using (var readStream = fs.OpenRead(filePath))
        using (var reader = new StreamReader(readStream))
        {
            string actualContent = reader.ReadToEnd();
            actualContent.Should().Be(expectedContent);
        }
    }
}
