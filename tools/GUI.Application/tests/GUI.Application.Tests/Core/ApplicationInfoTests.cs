using XrayDetector.Gui.Core;

namespace XrayDetector.Gui.Tests.Core;

/// <summary>
/// Tests for ApplicationInfo singleton (DEC-003).
/// </summary>
public class ApplicationInfoTests
{
    [Fact]
    public void Instance_ShouldReturnSingletonInstance()
    {
        // Act
        var instance1 = ApplicationInfo.Instance;
        var instance2 = ApplicationInfo.Instance;

        // Assert
        instance1.Should().BeSameAs(instance2);
    }

    [Fact]
    public void Version_ShouldNotBeNullOrEmpty()
    {
        // Act
        var version = ApplicationInfo.Instance.Version;

        // Assert
        version.Should().NotBeNullOrEmpty("version should be set from assembly info");
    }

    [Fact]
    public void AppName_ShouldNotBeNullOrEmpty()
    {
        // Act
        var appName = ApplicationInfo.Instance.AppName;

        // Assert
        appName.Should().NotBeNullOrEmpty("app name should be set from assembly info");
    }

    [Fact]
    public void BuildDate_ShouldNotBeNullOrEmpty()
    {
        // Act
        var buildDate = ApplicationInfo.Instance.BuildDate;

        // Assert
        buildDate.Should().NotBeNullOrEmpty("build date should be set from AssemblyMetadataAttribute");
    }

    [Fact]
    public void BuildDate_ShouldMatchDateFormat()
    {
        // Act
        var buildDate = ApplicationInfo.Instance.BuildDate;

        // Assert - expect yyyy-MM-dd format or "Unknown"
        var isValidFormat = buildDate == "Unknown" ||
                            DateTime.TryParseExact(buildDate, "yyyy-MM-dd",
                                System.Globalization.CultureInfo.InvariantCulture,
                                System.Globalization.DateTimeStyles.None, out _);

        isValidFormat.Should().BeTrue($"build date '{buildDate}' should be in yyyy-MM-dd format or 'Unknown'");
    }
}
