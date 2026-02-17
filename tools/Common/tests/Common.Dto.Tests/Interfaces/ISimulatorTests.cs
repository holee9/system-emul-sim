using Common.Dto.Interfaces;
using FluentAssertions;
using Xunit;

namespace Common.Dto.Tests.Interfaces;

/// <summary>
/// Tests for ISimulator interface specification.
/// REQ-SIM-050: Common.Dto shall define the ISimulator interface with methods.
/// </summary>
public class ISimulatorTests
{
    [Fact]
    public void Interface_shall_exist()
    {
        // Arrange & Act
        var interfaceType = typeof(ISimulator);

        // Assert
        interfaceType.Should().NotBeNull();
        interfaceType.IsInterface.Should().BeTrue();
        interfaceType.IsPublic.Should().BeTrue();
    }

    [Fact]
    public void Interface_shall_have_Initialize_method()
    {
        // Arrange
        var interfaceType = typeof(ISimulator);

        // Act
        var method = interfaceType.GetMethod("Initialize");

        // Assert
        method.Should().NotBeNull();
        method.ReturnType.Should().Be(typeof(void));
        method.GetParameters().Should().HaveCount(1);
        method.GetParameters()[0].Name.Should().Be("config");
        method.GetParameters()[0].ParameterType.Should().Be(typeof(object));
    }

    [Fact]
    public void Interface_shall_have_Process_method()
    {
        // Arrange
        var interfaceType = typeof(ISimulator);

        // Act
        var method = interfaceType.GetMethod("Process");

        // Assert
        method.Should().NotBeNull();
        method.ReturnType.Should().Be(typeof(object));
        method.GetParameters().Should().HaveCount(1);
        method.GetParameters()[0].Name.Should().Be("input");
        method.GetParameters()[0].ParameterType.Should().Be(typeof(object));
    }

    [Fact]
    public void Interface_shall_have_Reset_method()
    {
        // Arrange
        var interfaceType = typeof(ISimulator);

        // Act
        var method = interfaceType.GetMethod("Reset");

        // Assert
        method.Should().NotBeNull();
        method.ReturnType.Should().Be(typeof(void));
        method.GetParameters().Should().BeEmpty();
    }

    [Fact]
    public void Interface_shall_have_GetStatus_method()
    {
        // Arrange
        var interfaceType = typeof(ISimulator);

        // Act
        var method = interfaceType.GetMethod("GetStatus");

        // Assert
        method.Should().NotBeNull();
        method.ReturnType.Should().Be(typeof(string));
        method.GetParameters().Should().BeEmpty();
    }
}
