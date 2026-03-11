using FluentAssertions;
using Xunit;
using System.IO;

namespace IntegrationTests.Helpers.Mock;

/// <summary>
/// Unit tests for MemoryFileSystem - TDD RED phase.
/// Tests are written first to verify desired behavior before implementation.
/// </summary>
public class MemoryFileSystemTests
{
    [Fact]
    public void CreateDirectory_ShouldCreateVirtualDirectory()
    {
        // Arrange
        var fs = new MemoryFileSystem();
        string testPath = "/test/directory";

        // Act
        fs.CreateDirectory(testPath);

        // Assert
        fs.DirectoryExists(testPath).Should().BeTrue("directory should exist after creation");
    }

    [Fact]
    public void DirectoryExists_ShouldReturnFalseForNonExistentDirectory()
    {
        // Arrange
        var fs = new MemoryFileSystem();
        string nonExistentPath = "/non/existent/path";

        // Act & Assert
        fs.DirectoryExists(nonExistentPath).Should().BeFalse("non-existent directory should return false");
    }

    [Fact]
    public void FileExists_ShouldReturnTrueForCreatedFiles()
    {
        // Arrange
        var fs = new MemoryFileSystem();
        string filePath = "/test/file.txt";

        // Act
        using (var stream = fs.CreateFile(filePath))
        using (var writer = new StreamWriter(stream))
        {
            writer.Write("test content");
        }

        // Assert
        fs.FileExists(filePath).Should().BeTrue("file should exist after creation");
    }

    [Fact]
    public void FileExists_ShouldReturnFalseForNonExistentFile()
    {
        // Arrange
        var fs = new MemoryFileSystem();
        string nonExistentFile = "/non/existent/file.txt";

        // Act & Assert
        fs.FileExists(nonExistentFile).Should().BeFalse("non-existent file should return false");
    }

    [Fact]
    public void CreateFile_ShouldWriteToMemoryStream()
    {
        // Arrange
        var fs = new MemoryFileSystem();
        string filePath = "/test/file.txt";
        string expectedContent = "Hello, World!";

        // Act
        using (var stream = fs.CreateFile(filePath))
        using (var writer = new StreamWriter(stream))
        {
            writer.Write(expectedContent);
        }

        // Assert
        using (var readStream = fs.OpenRead(filePath))
        using (var reader = new StreamReader(readStream))
        {
            string actualContent = reader.ReadToEnd();
            actualContent.Should().Be(expectedContent, "file content should match what was written");
        }
    }

    [Fact]
    public void OpenRead_ShouldReturnFileStreamForExistingFile()
    {
        // Arrange
        var fs = new MemoryFileSystem();
        string filePath = "/test/file.txt";
        byte[] testData = new byte[] { 0x01, 0x02, 0x03, 0x04 };

        // Act
        using (var stream = fs.CreateFile(filePath))
        {
            stream.Write(testData, 0, testData.Length);
        }

        // Assert
        using (var readStream = fs.OpenRead(filePath))
        {
            readStream.Should().NotBeNull("should return valid stream");
            readStream.CanRead.Should().BeTrue("stream should be readable");
        }
    }

    [Fact]
    public void OpenRead_ShouldThrowFileNotFoundExceptionForNonExistentFile()
    {
        // Arrange
        var fs = new MemoryFileSystem();
        string nonExistentFile = "/non/existent/file.txt";

        // Act & Assert
        FluentActions.Invoking(() => fs.OpenRead(nonExistentFile))
            .Should().Throw<FileNotFoundException>("opening non-existent file should throw");
    }

    [Fact]
    public void DeleteDirectory_ShouldRemoveDirectoryAndContents()
    {
        // Arrange
        var fs = new MemoryFileSystem();
        string dirPath = "/test/directory";
        string filePath = "/test/directory/file.txt";

        fs.CreateDirectory(dirPath);
        using (var stream = fs.CreateFile(filePath))
        {
            stream.WriteByte(0x01);
        }

        // Act
        fs.DeleteDirectory(dirPath, recursive: true);

        // Assert
        fs.DirectoryExists(dirPath).Should().BeFalse("directory should be deleted");
        fs.FileExists(filePath).Should().BeFalse("file in directory should also be deleted");
    }

    [Fact]
    public void DeleteDirectory_NonRecursive_ShouldThrowIfNotEmpty()
    {
        // Arrange
        var fs = new MemoryFileSystem();
        string dirPath = "/test/directory";
        string filePath = "/test/directory/file.txt";

        fs.CreateDirectory(dirPath);
        using (var stream = fs.CreateFile(filePath))
        {
            stream.WriteByte(0x01);
        }

        // Act & Assert
        FluentActions.Invoking(() => fs.DeleteDirectory(dirPath, recursive: false))
            .Should().Throw<IOException>("deleting non-empty directory without recursive should throw");
    }

    [Fact]
    public void GetTempPath_ShouldReturnValidPath()
    {
        // Arrange
        var fs = new MemoryFileSystem();

        // Act
        string tempPath = fs.GetTempPath();

        // Assert
        tempPath.Should().NotBeNullOrEmpty("temp path should not be empty");
        tempPath.Should().StartWith("/", "temp path should be absolute");
    }

    [Fact]
    public void CreateFile_OverwriteExisting_ShouldReplaceContent()
    {
        // Arrange
        var fs = new MemoryFileSystem();
        string filePath = "/test/file.txt";
        string originalContent = "Original";
        string newContent = "New Content";

        // Act - Create original file
        using (var stream = fs.CreateFile(filePath))
        using (var writer = new StreamWriter(stream))
        {
            writer.Write(originalContent);
        }

        // Act - Overwrite with new content
        using (var stream = fs.CreateFile(filePath))
        using (var writer = new StreamWriter(stream))
        {
            writer.Write(newContent);
        }

        // Assert
        using (var readStream = fs.OpenRead(filePath))
        using (var reader = new StreamReader(readStream))
        {
            string actualContent = reader.ReadToEnd();
            actualContent.Should().Be(newContent, "file should contain new content");
        }
    }

    [Fact]
    public void CreateDirectory_NestedPaths_ShouldCreateAllDirectories()
    {
        // Arrange
        var fs = new MemoryFileSystem();
        string nestedPath = "/a/b/c/d";

        // Act
        fs.CreateDirectory(nestedPath);

        // Assert
        fs.DirectoryExists("/a").Should().BeTrue();
        fs.DirectoryExists("/a/b").Should().BeTrue();
        fs.DirectoryExists("/a/b/c").Should().BeTrue();
        fs.DirectoryExists("/a/b/c/d").Should().BeTrue();
    }

    [Fact]
    public void CreateFile_InNonExistentDirectory_ShouldCreateParentDirectories()
    {
        // Arrange
        var fs = new MemoryFileSystem();
        string filePath = "/non/existent/directory/file.txt";

        // Act
        using (var stream = fs.CreateFile(filePath))
        {
            stream.WriteByte(0x01);
        }

        // Assert
        fs.FileExists(filePath).Should().BeTrue();
        fs.DirectoryExists("/non/existent/directory").Should().BeTrue();
    }

    [Fact]
    public void MultipleFiles_InSameDirectory_ShouldBeIndependent()
    {
        // Arrange
        var fs = new MemoryFileSystem();
        string file1 = "/test/file1.txt";
        string file2 = "/test/file2.txt";

        // Act
        using (var stream = fs.CreateFile(file1))
        using (var writer = new StreamWriter(stream))
        {
            writer.Write("Content 1");
        }

        using (var stream = fs.CreateFile(file2))
        using (var writer = new StreamWriter(stream))
        {
            writer.Write("Content 2");
        }

        // Assert
        using (var stream1 = fs.OpenRead(file1))
        using (var reader1 = new StreamReader(stream1))
        using (var stream2 = fs.OpenRead(file2))
        using (var reader2 = new StreamReader(stream2))
        {
            reader1.ReadToEnd().Should().Be("Content 1");
            reader2.ReadToEnd().Should().Be("Content 2");
        }
    }
}
