using XrayDetector.Gui.ViewModels;

namespace XrayDetector.Gui.Tests.ViewModels;

/// <summary>
/// TDD tests for WelcomeWizardViewModel (SPEC-HELP-001 Wave 2).
/// RED phase: Tests written before implementation.
/// </summary>
public class WelcomeWizardViewModelTests
{
    private readonly WelcomeWizardViewModel _sut;

    public WelcomeWizardViewModelTests()
    {
        _sut = new WelcomeWizardViewModel();
    }

    [Fact]
    public void Constructor_ShouldInitializeAtStepOne()
    {
        // Assert
        _sut.CurrentStep.Should().Be(1, "wizard starts at step 1");
    }

    [Fact]
    public void Constructor_ShouldHaveDontShowAgainFalse()
    {
        // Assert
        _sut.DontShowAgain.Should().BeFalse("DontShowAgain should default to false");
    }

    [Fact]
    public void StepTitle_AtStepOne_ShouldBeWelcome()
    {
        // Assert
        _sut.StepTitle.Should().NotBeNullOrWhiteSpace("step title should be set");
    }

    [Fact]
    public void StepContent_AtStepOne_ShouldBeSet()
    {
        // Assert
        _sut.StepContent.Should().NotBeNullOrWhiteSpace("step content should be set");
    }

    [Fact]
    public void NextCommand_FromStepOne_ShouldAdvanceToStepTwo()
    {
        // Act
        _sut.NextCommand.Execute(null);

        // Assert
        _sut.CurrentStep.Should().Be(2, "next command should advance to step 2");
    }

    [Fact]
    public void NextCommand_FromStepTwo_ShouldAdvanceToStepThree()
    {
        // Arrange
        _sut.NextCommand.Execute(null); // step 1 -> 2

        // Act
        _sut.NextCommand.Execute(null); // step 2 -> 3

        // Assert
        _sut.CurrentStep.Should().Be(3, "next command should advance to step 3");
    }

    [Fact]
    public void NextCommand_CanExecute_AtStepThree_ShouldBeFalse()
    {
        // Arrange - advance to last step
        _sut.NextCommand.Execute(null);
        _sut.NextCommand.Execute(null);

        // Assert
        _sut.NextCommand.CanExecute(null).Should().BeFalse("cannot go next from last step");
    }

    [Fact]
    public void PreviousCommand_CanExecute_AtStepOne_ShouldBeFalse()
    {
        // Assert
        _sut.PreviousCommand.CanExecute(null).Should().BeFalse("cannot go back from first step");
    }

    [Fact]
    public void PreviousCommand_FromStepTwo_ShouldGoBackToStepOne()
    {
        // Arrange
        _sut.NextCommand.Execute(null); // go to step 2

        // Act
        _sut.PreviousCommand.Execute(null);

        // Assert
        _sut.CurrentStep.Should().Be(1, "previous should go back to step 1");
    }

    [Fact]
    public void FinishCommand_CanExecute_AtStepThree_ShouldBeTrue()
    {
        // Arrange
        _sut.NextCommand.Execute(null);
        _sut.NextCommand.Execute(null);

        // Assert
        _sut.FinishCommand.CanExecute(null).Should().BeTrue("finish available at last step");
    }

    [Fact]
    public void FinishCommand_CanExecute_AtStepOne_ShouldBeFalse()
    {
        // Assert
        _sut.FinishCommand.CanExecute(null).Should().BeFalse("finish not available at step 1");
    }

    [Fact]
    public void FinishCommand_ShouldInvokeFinishAction()
    {
        // Arrange
        _sut.NextCommand.Execute(null);
        _sut.NextCommand.Execute(null); // at step 3

        bool actionInvoked = false;
        _sut.FinishAction = () => actionInvoked = true;

        // Act
        _sut.FinishCommand.Execute(null);

        // Assert
        actionInvoked.Should().BeTrue("finish action should be invoked");
    }

    [Fact]
    public void DontShowAgain_WhenChanged_ShouldRaisePropertyChanged()
    {
        // Arrange
        var propertyChangedEvents = new List<string?>();
        _sut.PropertyChanged += (_, e) => propertyChangedEvents.Add(e.PropertyName);

        // Act
        _sut.DontShowAgain = true;

        // Assert
        propertyChangedEvents.Should().Contain("DontShowAgain");
    }

    [Fact]
    public void StepTitle_WhenStepChanges_ShouldUpdate()
    {
        // Arrange
        var titleAtStep1 = _sut.StepTitle;

        // Act
        _sut.NextCommand.Execute(null);

        // Assert
        _sut.StepTitle.Should().NotBe(titleAtStep1, "title should change when step changes (or both are distinct)");
    }

    [Fact]
    public void CurrentStep_WhenChanged_ShouldRaisePropertyChangedForStepTitle()
    {
        // Arrange
        var propertyChangedEvents = new List<string?>();
        _sut.PropertyChanged += (_, e) => propertyChangedEvents.Add(e.PropertyName);

        // Act
        _sut.NextCommand.Execute(null);

        // Assert
        propertyChangedEvents.Should().Contain("StepTitle");
        propertyChangedEvents.Should().Contain("StepContent");
    }
}
