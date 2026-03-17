using FluentAssertions;
using XrayDetector.Gui.Services;
using Xunit;

namespace XrayDetector.Gui.Tests.Services;

/// <summary>
/// TDD tests for ScenarioRunner (REQ-UI-014, SPEC-GUI-001 MVP-3).
/// Verifies real pipeline execution — scenarios must return actual pass/fail results.
/// </summary>
public class ScenarioRunnerTests
{
    [Fact]
    public void GetPredefinedScenarios_returns_non_empty_list()
    {
        // Arrange & Act
        var scenarios = ScenarioRunner.GetPredefinedScenarios();

        // Assert — must include at least one IT01 full-pipeline scenario
        scenarios.Should().NotBeEmpty();
        scenarios.Should().Contain(s => s.ScenarioType == "IT01");
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
