using Common.Dto.Dtos;
using FluentAssertions;
using IntegrationTests.Helpers;
using Xunit;

namespace IntegrationTests.Helpers;

/// <summary>
/// Tests for SimulatorPipelineBuilder using TDD approach.
/// </summary>
public class SimulatorPipelineBuilderTests
{
    [Fact]
    public void Constructor_InitializesWithTargetTier()
    {
        // Arrange & Act
        var builder = new SimulatorPipelineBuilder();

        // Assert
        builder.CurrentTier.Should().Be(PerformanceTier.Target);
        builder.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void BuildPipeline_WithDefaultTier_ReturnsTargetConfiguration()
    {
        // Arrange
        var builder = new SimulatorPipelineBuilder();

        // Act
        var config = builder.BuildPipeline();

        // Assert
        config.Name.Should().Be("Target");
        config.FrameRate.Should().Be(30);
        config.BufferSize.Should().Be(1024);
        config.Parallelism.Should().Be(4);
    }

    [Fact]
    public void ConfigureForTier_Minimum_SetsMinimumConfiguration()
    {
        // Arrange
        var builder = new SimulatorPipelineBuilder();

        // Act
        builder.ConfigureForTier(PerformanceTier.Minimum);
        var config = builder.BuildPipeline();

        // Assert
        config.Name.Should().Be("Minimum");
        config.FrameRate.Should().Be(1);
        config.BufferSize.Should().Be(64);
        config.Parallelism.Should().Be(1);
    }

    [Fact]
    public void ConfigureForTier_Maximum_SetsMaximumConfiguration()
    {
        // Arrange
        var builder = new SimulatorPipelineBuilder();

        // Act
        builder.ConfigureForTier(PerformanceTier.Maximum);
        var config = builder.BuildPipeline();

        // Assert
        config.Name.Should().Be("Maximum");
        config.FrameRate.Should().Be(60);
        config.BufferSize.Should().Be(4096);
        config.Parallelism.Should().Be(8);
    }

    [Fact]
    public async Task StartAsync_WhenStopped_StartsPipeline()
    {
        // Arrange
        var builder = new SimulatorPipelineBuilder();

        // Act
        await builder.StartAsync();

        // Assert
        builder.IsRunning.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyRunning_ThrowsException()
    {
        // Arrange
        var builder = new SimulatorPipelineBuilder();
        await builder.StartAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => builder.StartAsync());
    }

    [Fact]
    public async Task StopAsync_WhenRunning_StopsPipeline()
    {
        // Arrange
        var builder = new SimulatorPipelineBuilder();
        await builder.StartAsync();

        // Act
        await builder.StopAsync();

        // Assert
        builder.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task StopAsync_WhenNotRunning_ThrowsException()
    {
        // Arrange
        var builder = new SimulatorPipelineBuilder();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => builder.StopAsync());
    }

    [Fact]
    public void ConfigureForTier_WhileRunning_ThrowsException()
    {
        // Arrange
        var builder = new SimulatorPipelineBuilder();

        // Act
        var startTask = builder.StartAsync();

        // Assert
        Assert.Throws<InvalidOperationException>(() =>
            builder.ConfigureForTier(PerformanceTier.Maximum));

        // Cleanup
        builder.StopAsync().Wait();
    }

    [Fact]
    public void GetConfiguration_ReturnsTierConfiguration()
    {
        // Arrange
        var builder = new SimulatorPipelineBuilder();

        // Act
        var minConfig = builder.GetConfiguration(PerformanceTier.Minimum);
        var targetConfig = builder.GetConfiguration(PerformanceTier.Target);
        var maxConfig = builder.GetConfiguration(PerformanceTier.Maximum);

        // Assert
        minConfig.FrameRate.Should().Be(1);
        targetConfig.FrameRate.Should().Be(30);
        maxConfig.FrameRate.Should().Be(60);
    }

    [Fact]
    public void StateChanged_RaisesEventOnStateChange()
    {
        // Arrange
        var builder = new SimulatorPipelineBuilder();
        List<PipelineStateChangedEventArgs> events = new();

        builder.StateChanged += (s, e) => events.Add(e);

        // Act
        builder.ConfigureForTier(PerformanceTier.Maximum);

        // Assert
        events.Should().HaveCount(1);
        events[0].Tier.Should().Be(PerformanceTier.Maximum);
        events[0].IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task StateChanged_RaisesEventOnStartAndStop()
    {
        // Arrange
        var builder = new SimulatorPipelineBuilder();
        List<bool> runningStates = new();

        builder.StateChanged += (s, e) => runningStates.Add(e.IsRunning);

        // Act
        await builder.StartAsync();
        await builder.StopAsync();

        // Assert
        runningStates.Should().HaveCount(2);
        runningStates[0].Should().BeTrue();  // Start
        runningStates[1].Should().BeFalse(); // Stop
    }

    [Fact]
    public void SetCustomConfiguration_UpdatesTierConfiguration()
    {
        // Arrange
        var builder = new SimulatorPipelineBuilder();
        var customConfig = new PipelineConfiguration(15, 512, 2, "Custom");

        // Act
        builder.SetCustomConfiguration(PerformanceTier.Target, customConfig);
        var config = builder.GetConfiguration(PerformanceTier.Target);

        // Assert
        config.FrameRate.Should().Be(15);
        config.BufferSize.Should().Be(512);
        config.Parallelism.Should().Be(2);
    }
}
