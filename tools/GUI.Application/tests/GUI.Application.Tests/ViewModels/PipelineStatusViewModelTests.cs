using FluentAssertions;
using IntegrationRunner.Core;
using XrayDetector.Gui.ViewModels;
using Xunit;

namespace XrayDetector.Gui.Tests.ViewModels;

/// <summary>
/// TDD tests for PipelineStatusViewModel (REQ-UI-013).
/// RED phase: Define expected behavior for pipeline statistics display.
/// </summary>
public class PipelineStatusViewModelTests
{
    [Fact]
    public void Implements_INotifyPropertyChanged()
    {
        // Arrange & Act
        var vm = new PipelineStatusViewModel();

        // Assert
        vm.Should().BeAssignableTo<System.ComponentModel.INotifyPropertyChanged>();
    }

    [Fact]
    public void Initial_values_are_zero_or_default()
    {
        // Arrange & Act
        var vm = new PipelineStatusViewModel();

        // Assert
        vm.FramesProcessed.Should().Be(0);
        vm.FramesFailed.Should().Be(0);
        vm.AvgProcessingTimeMs.Should().Be(0.0);
        vm.PacketsSent.Should().Be(0);
        vm.PacketsLost.Should().Be(0);
        vm.PacketsReordered.Should().Be(0);
        vm.PacketsCorrupted.Should().Be(0);
    }

    [Fact]
    public void UpdateStatistics_applies_values()
    {
        // Arrange
        var vm = new PipelineStatusViewModel();
        var stats = new PipelineStatistics
        {
            FramesProcessed = 100,
            FramesFailed = 2,
            NetworkStats = new NetworkChannelStats
            {
                PacketsSent = 1000,
                PacketsLost = 5,
                PacketsReordered = 3,
                PacketsCorrupted = 1
            }
        };

        // Act
        vm.UpdateStatistics(stats);

        // Assert
        vm.FramesProcessed.Should().Be(100);
        vm.FramesFailed.Should().Be(2);
        vm.PacketsSent.Should().Be(1000);
        vm.PacketsLost.Should().Be(5);
        vm.PacketsReordered.Should().Be(3);
        vm.PacketsCorrupted.Should().Be(1);
    }

    [Fact]
    public void UpdateStatistics_raises_PropertyChanged()
    {
        // Arrange
        var vm = new PipelineStatusViewModel();
        var stats = new PipelineStatistics
        {
            FramesProcessed = 50
        };
        var propertyChanged = false;
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(PipelineStatusViewModel.FramesProcessed))
                propertyChanged = true;
        };

        // Act
        vm.UpdateStatistics(stats);

        // Assert
        propertyChanged.Should().BeTrue();
    }

    [Fact]
    public void StatusIndicator_defaults_to_Green()
    {
        // Arrange & Act
        var vm = new PipelineStatusViewModel();

        // Assert
        vm.StatusIndicator.Should().Be("Green");
    }

    [Fact]
    public void StatusIndicator_changes_to_Yellow_on_failures()
    {
        // Arrange
        var vm = new PipelineStatusViewModel();
        var stats = new PipelineStatistics
        {
            FramesProcessed = 100,
            FramesFailed = 5 // 5% failure rate
        };

        // Act
        vm.UpdateStatistics(stats);

        // Assert
        vm.StatusIndicator.Should().Be("Yellow");
    }

    [Fact]
    public void StatusIndicator_changes_to_Red_on_high_failures()
    {
        // Arrange
        var vm = new PipelineStatusViewModel();
        var stats = new PipelineStatistics
        {
            FramesProcessed = 100,
            FramesFailed = 15 // 15% failure rate
        };

        // Act
        vm.UpdateStatistics(stats);

        // Assert
        vm.StatusIndicator.Should().Be("Red");
    }

    [Fact]
    public void Reset_clears_statistics()
    {
        // Arrange
        var vm = new PipelineStatusViewModel();
        var stats = new PipelineStatistics
        {
            FramesProcessed = 100,
            FramesFailed = 5
        };
        vm.UpdateStatistics(stats);

        // Act
        vm.Reset();

        // Assert
        vm.FramesProcessed.Should().Be(0);
        vm.FramesFailed.Should().Be(0);
        vm.AvgProcessingTimeMs.Should().Be(0.0);
    }
}
