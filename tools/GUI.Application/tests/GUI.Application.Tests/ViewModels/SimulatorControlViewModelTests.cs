using System.ComponentModel;
using FluentAssertions;
using IntegrationRunner.Core.Models;
using IntegrationRunner.Core.Network;
using XrayDetector.Gui.ViewModels;
using Xunit;

namespace XrayDetector.Gui.Tests.ViewModels;

/// <summary>
/// TDD tests for SimulatorControlViewModel (REQ-UI-012).
/// RED phase: Define expected behavior for simulator parameter control.
/// </summary>
public class SimulatorControlViewModelTests : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    [Fact]
    public void Implements_INotifyPropertyChanged()
    {
        // Arrange & Act
        var vm = new SimulatorControlViewModel();

        // Assert
        vm.Should().BeAssignableTo<INotifyPropertyChanged>();
    }

    [Fact]
    public void PanelRows_property_raises_PropertyChanged()
    {
        // Arrange
        var vm = new SimulatorControlViewModel();
        PropertyChangedEventHandler? handler = null;
        var propertyChanged = false;
        vm.PropertyChanged += handler = (s, e) =>
        {
            if (e.PropertyName == nameof(SimulatorControlViewModel.PanelRows))
                propertyChanged = true;
        };

        // Act
        vm.PanelRows = 512;

        // Assert
        propertyChanged.Should().BeTrue("PanelRows should raise PropertyChanged");
        vm.PropertyChanged -= handler;
    }

    [Fact]
    public void PanelCols_property_raises_PropertyChanged()
    {
        // Arrange
        var vm = new SimulatorControlViewModel();
        var propertyChanged = false;
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SimulatorControlViewModel.PanelCols))
                propertyChanged = true;
        };

        // Act
        vm.PanelCols = 512;

        // Assert
        propertyChanged.Should().BeTrue();
    }

    [Fact]
    public void PanelKvp_validates_range_40_to_150()
    {
        // Arrange
        var vm = new SimulatorControlViewModel();

        // Act & Assert - Valid values
        vm.PanelKvp = 40;
        vm.PanelKvp.Should().Be(40);

        vm.PanelKvp = 150;
        vm.PanelKvp.Should().Be(150);

        vm.PanelKvp = 80;
        vm.PanelKvp.Should().Be(80);
    }

    [Fact]
    public void PacketLossRate_validates_range_0_to_1()
    {
        // Arrange
        var vm = new SimulatorControlViewModel();

        // Act & Assert - Valid values
        vm.PacketLossRate = 0.0;
        vm.PacketLossRate.Should().Be(0.0);

        vm.PacketLossRate = 1.0;
        vm.PacketLossRate.Should().Be(1.0);

        vm.PacketLossRate = 0.5;
        vm.PacketLossRate.Should().Be(0.5);
    }

    [Fact]
    public void FrameBufferCount_validates_range_1_to_8()
    {
        // Arrange
        var vm = new SimulatorControlViewModel();

        // Act & Assert - Valid values
        vm.FrameBufferCount = 1;
        vm.FrameBufferCount.Should().Be(1);

        vm.FrameBufferCount = 8;
        vm.FrameBufferCount.Should().Be(8);

        vm.FrameBufferCount = 4;
        vm.FrameBufferCount.Should().Be(4);
    }

    [Fact]
    public void StartCommand_is_initially_enabled()
    {
        // Arrange
        var vm = new SimulatorControlViewModel();

        // Assert
        vm.StartCommand.Should().NotBeNull();
        vm.StartCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void StopCommand_is_initially_disabled()
    {
        // Arrange
        var vm = new SimulatorControlViewModel();

        // Assert
        vm.StopCommand.Should().NotBeNull();
        vm.StopCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void ResetCommand_is_initially_enabled()
    {
        // Arrange
        var vm = new SimulatorControlViewModel();

        // Assert
        vm.ResetCommand.Should().NotBeNull();
        vm.ResetCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void ToDetectorConfig_creates_valid_configuration()
    {
        // Arrange
        var vm = new SimulatorControlViewModel
        {
            PanelRows = 64,
            PanelCols = 64,
            PanelBitDepth = 14,
            PanelKvp = 80.0,
            PanelMas = 1.0,
            FrameBufferCount = 4,
            PacketLossRate = 0.1,
            ReorderRate = 0.05,
            CorruptionRate = 0.02,
            MinDelayMs = 10,
            MaxDelayMs = 50
        };

        // Act
        var config = vm.ToDetectorConfig();

        // Assert
        config.Should().NotBeNull();
        config!.Panel.Should().NotBeNull();
        config.Panel.Rows.Should().Be(64);
        config.Panel.Cols.Should().Be(64);
        config.Panel.BitDepth.Should().Be(14);
        config.Soc.Should().NotBeNull();
        config.Soc.FrameBufferCount.Should().Be(4);
    }

    [Fact]
    public void McuBufferState_defaults_to_Free()
    {
        // Arrange
        var vm = new SimulatorControlViewModel();

        // Assert
        vm.McuBufferState.Should().Be("Free");
    }

    [Fact]
    public void McuBufferState_can_be_updated()
    {
        // Arrange
        var vm = new SimulatorControlViewModel();
        var propertyChanged = false;
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SimulatorControlViewModel.McuBufferState))
                propertyChanged = true;
        };

        // Act
        vm.McuBufferState = "Filling";

        // Assert
        vm.McuBufferState.Should().Be("Filling");
        propertyChanged.Should().BeTrue();
    }

    [Fact]
    public void UpdateFromConfig_applies_configuration_values()
    {
        // Arrange
        var vm = new SimulatorControlViewModel();
        var config = new DetectorConfig
        {
            Panel = new PanelConfig
            {
                Rows = 128,
                Cols = 128,
                BitDepth = 16
            },
            Soc = new SocConfig
            {
                FrameBufferCount = 6
            },
            Simulation = new SimulationConfig
            {
                MaxFrames = 100
            }
        };

        // Act
        vm.UpdateFromConfig(config);

        // Assert
        vm.PanelRows.Should().Be(128);
        vm.PanelCols.Should().Be(128);
        vm.PanelBitDepth.Should().Be(16);
        vm.FrameBufferCount.Should().Be(6);
    }

    [Fact]
    public void Default_values_are_correct()
    {
        // Arrange & Act
        var vm = new SimulatorControlViewModel();

        // Assert - Default values from spec
        vm.PanelRows.Should().Be(1024);
        vm.PanelCols.Should().Be(1024);
        vm.PanelBitDepth.Should().Be(14);
        vm.PanelKvp.Should().Be(80.0);
        vm.PanelMas.Should().Be(1.0);
        vm.FrameBufferCount.Should().Be(4);
        vm.PacketLossRate.Should().Be(0.0);
        vm.ReorderRate.Should().Be(0.0);
        vm.CorruptionRate.Should().Be(0.0);
        vm.MinDelayMs.Should().Be(0);
        vm.MaxDelayMs.Should().Be(0);
    }
}
