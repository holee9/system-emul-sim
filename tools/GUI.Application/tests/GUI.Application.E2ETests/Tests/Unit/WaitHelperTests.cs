using FluentAssertions;
using Xunit;
using XrayDetector.Gui.E2ETests.Infrastructure;

namespace XrayDetector.Gui.E2ETests.Tests.Unit;

/// <summary>
/// Unit tests for WaitHelper improvements.
/// TAG-003: Tests must pass without desktop/GUI.
/// </summary>
public sealed class WaitHelperTests
{
    [Fact]
    public async Task WaitUntilAsync_ReturnsTrueWhenConditionMet()
    {
        var result = await WaitHelper.WaitUntilAsync(() => true, timeoutMs: 1000);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task WaitUntilAsync_ReturnsFalseOnTimeout()
    {
        var result = await WaitHelper.WaitUntilAsync(() => false, timeoutMs: 100, pollIntervalMs: 10);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task WaitUntilAsync_WithDescription_ReturnsTrue_WhenConditionMet()
    {
        // description parameter is backward-compatible with default value
        var result = await WaitHelper.WaitUntilAsync(() => true, timeoutMs: 1000, description: "TestCondition");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task WaitUntilAsync_WithDescription_ReturnsFalse_OnTimeout()
    {
        var result = await WaitHelper.WaitUntilAsync(() => false, timeoutMs: 100, pollIntervalMs: 10,
            description: "NeverTrue");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task WaitUntilAsync_RespectsEnvironmentTimeout_Override()
    {
        // When XRAY_E2E_TIMEOUT_MS is not set, uses the passed timeoutMs directly.
        // This test verifies no crash when the env var is absent.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await WaitHelper.WaitUntilAsync(() => false, timeoutMs: 150, pollIntervalMs: 10);
        sw.Stop();

        result.Should().BeFalse();
        // Should have elapsed at most ~500ms (far less than any excessive wait)
        sw.ElapsedMilliseconds.Should().BeLessThan(500);
    }
}
