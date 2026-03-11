using FluentAssertions;
using System.Text.Json;
using XrayDetector.Gui.Services;
using XrayDetector.Gui.ViewModels;
using Xunit;

namespace XrayDetector.Gui.Tests.Services;

/// <summary>
/// TDD tests for ScenarioRunner (REQ-UI-014).
/// RED phase: Define expected behavior for scenario execution.
/// </summary>
public class ScenarioRunnerTests
{
    [Fact]
    public void GetPredefinedScenarios_returns_non_empty_list()
    {
        // Arrange & Act
        var scenarios = ScenarioRunner.GetPredefinedScenarios();

        // Assert
        scenarios.Should().NotBeEmpty();
        scenarios.Should().Contain(s => s.Name == "IT01_FullPipeline");
    }

    [Fact]
    public void ExecuteScenarioAsync_reports_progress()
    {
        // Arrange
        var runner = new ScenarioRunner();
        var scenario = ScenarioRunner.GetPredefinedScenarios()[0];
        var progressValues = new List<int>();
        var progress = new Progress<int>(p => progressValues.Add(p));

        // Act
        var task = runner.ExecuteScenarioAsync(scenario, progress, CancellationToken.None);
        task.Wait(TimeSpan.FromSeconds(5));

        // Assert
        progressValues.Should().NotBeEmpty();
        progressValues.Last().Should().Be(100);
    }

    [Fact]
    public void ExecuteScenarioAsync_returns_result()
    {
        // Arrange
        var runner = new ScenarioRunner();
        var scenario = ScenarioRunner.GetPredefinedScenarios()[0];
        var progress = new Progress<int>();

        // Act
        var result = runner.ExecuteScenarioAsync(scenario, progress, CancellationToken.None);
        result.Wait(TimeSpan.FromSeconds(5));

        // Assert
        result.Result.Should().NotBeNull();
        result.Result.Passed.Should().BeTrue();
    }
}
