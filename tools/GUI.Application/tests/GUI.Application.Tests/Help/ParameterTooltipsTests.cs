using XrayDetector.Gui.Help;

namespace XrayDetector.Gui.Tests.Help;

/// <summary>
/// TDD tests for ParameterTooltips (SPEC-HELP-001 Wave 2).
/// RED phase: Tests written before implementation.
/// </summary>
public class ParameterTooltipsTests
{
    [Fact]
    public void Items_ShouldNotBeEmpty()
    {
        // Assert
        ParameterTooltips.Items.Should().NotBeEmpty("parameter tooltips should be defined");
    }

    [Fact]
    public void Items_ShouldContainKvpEntry()
    {
        // Assert
        ParameterTooltips.Items.Should().ContainKey("kVp", "kVp parameter tooltip should be defined");
    }

    [Fact]
    public void Items_ShouldContainMasEntry()
    {
        // Assert
        ParameterTooltips.Items.Should().ContainKey("mAs", "mAs parameter tooltip should be defined");
    }

    [Fact]
    public void Items_ShouldContainDefectRateEntry()
    {
        // Assert
        ParameterTooltips.Items.Should().ContainKey("DefectRate", "DefectRate parameter tooltip should be defined");
    }

    [Fact]
    public void Items_ShouldContainPacketLossRateEntry()
    {
        // Assert
        ParameterTooltips.Items.Should().ContainKey("PacketLossRate", "PacketLossRate parameter tooltip should be defined");
    }

    [Fact]
    public void Items_ShouldContainReorderRateEntry()
    {
        // Assert
        ParameterTooltips.Items.Should().ContainKey("ReorderRate", "ReorderRate parameter tooltip should be defined");
    }

    [Fact]
    public void Items_ShouldContainCorruptionRateEntry()
    {
        // Assert
        ParameterTooltips.Items.Should().ContainKey("CorruptionRate", "CorruptionRate parameter tooltip should be defined");
    }

    [Fact]
    public void KvpEntry_ShouldHaveValidRangeDescription()
    {
        // Act
        var info = ParameterTooltips.Items["kVp"];

        // Assert
        info.RangeDescription.Should().NotBeNullOrWhiteSpace();
        info.RangeDescription.Should().Contain("40", "kVp range starts at 40");
        info.RangeDescription.Should().Contain("150", "kVp range ends at 150");
    }

    [Fact]
    public void KvpEntry_ShouldHavePhysicalMeaning()
    {
        // Act
        var info = ParameterTooltips.Items["kVp"];

        // Assert
        info.PhysicalMeaning.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void MasEntry_ShouldHaveValidRangeDescription()
    {
        // Act
        var info = ParameterTooltips.Items["mAs"];

        // Assert
        info.RangeDescription.Should().NotBeNullOrWhiteSpace();
        info.RangeDescription.Should().Contain("0.1", "mAs range starts at 0.1");
    }

    [Fact]
    public void ParameterTooltipInfo_Record_ShouldHaveCorrectProperties()
    {
        // Arrange
        var info = new ParameterTooltipInfo("kVp", "Range: 40-150 kV", "Test meaning");

        // Assert
        info.Name.Should().Be("kVp");
        info.RangeDescription.Should().Be("Range: 40-150 kV");
        info.PhysicalMeaning.Should().Be("Test meaning");
    }

    [Fact]
    public void Items_ShouldHaveSixEntries()
    {
        // Assert
        ParameterTooltips.Items.Should().HaveCount(6, "6 parameter tooltips are defined");
    }
}
