using FluentAssertions;
using Xunit;
using XrayDetector.Gui.E2ETests.Infrastructure;

namespace XrayDetector.Gui.E2ETests.Tests.Unit;

/// <summary>
/// Unit tests for RetryHelper.
/// TAG-006: Tests must pass without desktop/GUI.
/// </summary>
public sealed class RetryTests
{
    [Fact]
    public async Task RetryHelper_RunWithRetryAsync_SucceedsOnFirstAttempt()
    {
        int callCount = 0;
        await RetryHelper.RunWithRetryAsync(() =>
        {
            callCount++;
            return Task.CompletedTask;
        }, maxRetries: 2);

        callCount.Should().Be(1);
    }

    [Fact]
    public async Task RetryHelper_RunWithRetryAsync_RetriesOnFailure()
    {
        int callCount = 0;
        await RetryHelper.RunWithRetryAsync(() =>
        {
            callCount++;
            if (callCount < 2) throw new InvalidOperationException("Transient failure");
            return Task.CompletedTask;
        }, maxRetries: 3, retryDelayMs: 10);

        callCount.Should().Be(2, "should succeed on second attempt");
    }

    [Fact]
    public async Task RetryHelper_RunWithRetryAsync_ThrowsAfterMaxRetries()
    {
        int callCount = 0;
        var act = async () =>
        {
            await RetryHelper.RunWithRetryAsync(() =>
            {
                callCount++;
                throw new InvalidOperationException("always fails");
#pragma warning disable CS0162
                return Task.CompletedTask;
#pragma warning restore CS0162
            }, maxRetries: 2, retryDelayMs: 10);
        };

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("always fails");
        callCount.Should().Be(3, "initial attempt + 2 retries = 3 total calls");
    }

    [Fact]
    public async Task RetryHelper_LogsEachAttempt()
    {
        // Verify that RetryHelper logs retry attempts via E2ELogger
        var loggedMessages = new List<string>();
        var collector = new MessageCollector(loggedMessages);
        E2ELogger.SetTestOutput(collector);

        try
        {
            int attempt = 0;
            await RetryHelper.RunWithRetryAsync(() =>
            {
                attempt++;
                if (attempt < 2) throw new InvalidOperationException("retry me");
                return Task.CompletedTask;
            }, maxRetries: 2, retryDelayMs: 10);
        }
        finally
        {
            E2ELogger.ClearTestOutput();
        }

        // At least one retry message should have been logged
        loggedMessages.Should().ContainMatch("*retry*attempt*", "RetryHelper should log retry attempts");
    }

    [Fact]
    public void RetryFactAttribute_HasDefaultProperties()
    {
        var attr = new RetryFactAttribute();
        attr.MaxRetries.Should().BeGreaterThanOrEqualTo(1);
        attr.RetryDelayMs.Should().BeGreaterThanOrEqualTo(0);
    }

    /// <summary>Minimal ITestOutputHelper that collects raw messages.</summary>
    private sealed class MessageCollector : Xunit.Abstractions.ITestOutputHelper
    {
        private readonly List<string> _messages;
        public MessageCollector(List<string> messages) => _messages = messages;
        public void WriteLine(string message) => _messages.Add(message);
        public void WriteLine(string format, params object[] args) => _messages.Add(string.Format(format, args));
    }
}
