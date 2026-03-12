using XrayDetector.Gui.Core;

namespace XrayDetector.Gui.Tests.Core;

/// <summary>
/// TDD tests for FirstRunManager (SPEC-HELP-001 Wave 2).
/// RED phase: Tests written before implementation.
/// </summary>
public class FirstRunManagerTests : IDisposable
{
    private readonly string _testSettingsPath;
    private readonly FirstRunManager _sut;

    public FirstRunManagerTests()
    {
        // Use a temp path for tests to avoid touching real settings
        _testSettingsPath = Path.Combine(Path.GetTempPath(), $"XrayDetectorTest_{Guid.NewGuid()}", "settings.json");
        _sut = new FirstRunManager(_testSettingsPath);
    }

    [Fact]
    public void IsFirstRun_WhenSettingsFileDoesNotExist_ShouldReturnTrue()
    {
        // Assert - file doesn't exist yet
        File.Exists(_testSettingsPath).Should().BeFalse();
        _sut.IsFirstRun().Should().BeTrue("first run flag is true when settings file doesn't exist");
    }

    [Fact]
    public void MarkFirstRunComplete_ShouldCreateSettingsFile()
    {
        // Act
        _sut.MarkFirstRunComplete();

        // Assert
        File.Exists(_testSettingsPath).Should().BeTrue("settings file should be created");
    }

    [Fact]
    public void MarkFirstRunComplete_ShouldWriteFirstRunCompletedJson()
    {
        // Act
        _sut.MarkFirstRunComplete();

        // Assert
        var content = File.ReadAllText(_testSettingsPath);
        content.Should().Contain("firstRunCompleted", "settings should contain the flag key");
        content.Should().Contain("true", "firstRunCompleted should be set to true");
    }

    [Fact]
    public void IsFirstRun_AfterMarkComplete_ShouldReturnFalse()
    {
        // Arrange
        _sut.MarkFirstRunComplete();

        // Act
        var result = _sut.IsFirstRun();

        // Assert
        result.Should().BeFalse("first run should be false after marking complete");
    }

    [Fact]
    public void MarkFirstRunComplete_ShouldCreateDirectory_IfNotExists()
    {
        // Arrange - ensure directory doesn't exist
        var dir = Path.GetDirectoryName(_testSettingsPath)!;
        if (Directory.Exists(dir))
            Directory.Delete(dir, true);

        // Act
        _sut.MarkFirstRunComplete();

        // Assert
        Directory.Exists(dir).Should().BeTrue("directory should be created");
    }

    [Fact]
    public void IsFirstRun_WhenSettingsFileHasFalseFlag_ShouldReturnFalse()
    {
        // Arrange - write a settings file with firstRunCompleted: true
        var dir = Path.GetDirectoryName(_testSettingsPath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(_testSettingsPath, "{\"firstRunCompleted\": true}");

        // Act
        var result = _sut.IsFirstRun();

        // Assert
        result.Should().BeFalse("settings file exists with completed flag");
    }

    public void Dispose()
    {
        // Cleanup test files
        try
        {
            var dir = Path.GetDirectoryName(_testSettingsPath)!;
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
