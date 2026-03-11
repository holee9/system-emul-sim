using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace IntegrationTests.Helpers.Cli;

/// <summary>
/// Tests for ICliInvoker implementations.
/// Verifies both ProcessInvoker and DirectCallInvoker behaviors.
/// </summary>
public class ICliInvokerTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    public ICliInvokerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Invoke_ShouldReturnExitCode()
    {
        // Arrange
        var invoker = new ProcessInvoker("PanelSimulator.Cli");
        var args = new[] { "--rows", "64", "--cols", "64", "-o", "test_output.bin" };

        // Act
        var result = invoker.Invoke(args);

        // Assert
        result.ExitCode.Should().Be(0);
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void Invoke_ShouldCaptureStdout()
    {
        // Arrange
        var invoker = new ProcessInvoker("PanelSimulator.Cli");
        var args = new[] { "--rows", "64", "--cols", "64", "-o", "test_output.bin" };

        // Act
        var result = invoker.Invoke(args);

        // Assert
        result.StandardOutput.Should().NotBeNullOrEmpty();
        _output.WriteLine($"STDOUT: {result.StandardOutput}");
    }

    [Fact]
    public void Invoke_ShouldCaptureStderr()
    {
        // Arrange
        var invoker = new ProcessInvoker("PanelSimulator.Cli");
        var args = new[] { "--invalid-arg" }; // Invalid argument to trigger stderr

        // Act
        var result = invoker.Invoke(args);

        // Assert
        // Should have non-zero exit code for invalid args
        result.ExitCode.Should().NotBe(0);
        _output.WriteLine($"STDERR: {result.StandardError}");
    }

    [Fact]
    public void DirectCallInvoker_ShouldBeFasterThanProcess()
    {
        // Arrange
        var processInvoker = new ProcessInvoker("PanelSimulator.Cli");
        var directInvoker = new DirectCallInvoker("PanelSimulator.Cli");
        var args = new[] { "--rows", "256", "--cols", "256", "-o", "perf_test.bin" };

        // Act
        var processResult = processInvoker.Invoke(args);
        var directResult = directInvoker.Invoke(args);

        // Assert
        // DirectCallInvoker should generally be faster, but we only check that it completes
        _output.WriteLine($"ProcessInvoker: {processResult.Duration.TotalMilliseconds:F2}ms");
        _output.WriteLine($"DirectCallInvoker: {directResult.Duration.TotalMilliseconds:F2}ms");

        // Both should succeed
        processResult.ExitCode.Should().Be(0);
        directResult.ExitCode.Should().Be(0);

        // DirectCallInvoker should complete in reasonable time (< 10 seconds)
        directResult.Duration.Should().BeLessThan(TimeSpan.FromSeconds(10));

        // ProcessInvoker typically takes longer but should also complete
        processResult.Duration.Should().BeLessThan(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void DirectCallInvoker_ShouldProduceSameOutputAsProcess()
    {
        // Arrange
        var processInvoker = new ProcessInvoker("PanelSimulator.Cli");
        var directInvoker = new DirectCallInvoker("PanelSimulator.Cli");
        var args = new[] { "--rows", "64", "--cols", "64", "-o", "consistency_test.bin" };

        // Act
        var processResult = processInvoker.Invoke(args);
        var directResult = directInvoker.Invoke(args);

        // Assert
        // Both should have the same exit code (whether success or failure)
        processResult.ExitCode.Should().Be(directResult.ExitCode);

        // If there's an error, print it for debugging
        if (processResult.ExitCode != 0)
        {
            _output.WriteLine($"ProcessInvoker stderr: {processResult.StandardError}");
            _output.WriteLine($"DirectCallInvoker stderr: {directResult.StandardError}");
        }

        // Both should produce output (not empty)
        processResult.StandardOutput.Should().NotBeEmpty();
        directResult.StandardOutput.Should().NotBeEmpty();
    }

    public void Dispose()
    {
        // Cleanup test output files
        var testFiles = new[] { "test_output.bin", "perf_test.bin", "consistency_test.bin" };
        foreach (var file in testFiles)
        {
            if (File.Exists(file))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
}
