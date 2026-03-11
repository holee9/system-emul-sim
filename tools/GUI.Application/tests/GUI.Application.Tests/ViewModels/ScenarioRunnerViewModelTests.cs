using FluentAssertions;
using XrayDetector.Gui.ViewModels;
using Xunit;

namespace XrayDetector.Gui.Tests.ViewModels;

/// <summary>
/// TDD tests for ScenarioRunnerViewModel (REQ-UI-014).
/// RED phase: Define expected behavior for scenario selection and execution.
/// </summary>
public class ScenarioRunnerViewModelTests
{
    [Fact]
    public void Scenarios_initially_contains_predefined_scenarios()
    {
        // Arrange & Act
        var vm = new ScenarioRunnerViewModel();

        // Assert
        vm.Scenarios.Should().NotBeEmpty();
    }

    [Fact]
    public void SelectedScenario_can_be_set()
    {
        // Arrange
        var vm = new ScenarioRunnerViewModel();
        var scenario = vm.Scenarios[0];

        // Act
        vm.SelectedScenario = scenario;

        // Assert
        vm.SelectedScenario.Should().Be(scenario);
    }

    [Fact]
    public void ExecuteCommand_is_initially_enabled()
    {
        // Arrange
        var vm = new ScenarioRunnerViewModel();

        // Assert
        vm.ExecuteCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void Progress_initially_is_zero()
    {
        // Arrange
        var vm = new ScenarioRunnerViewModel();

        // Assert
        vm.Progress.Should().Be(0);
    }

    [Fact]
    public void Result_initially_is_null()
    {
        // Arrange
        var vm = new ScenarioRunnerViewModel();

        // Assert
        vm.Result.Should().BeNull();
    }
}
