using System.Diagnostics;
using System.Text;
using Xunit.Abstractions;

namespace XrayDetector.Gui.E2ETests.Infrastructure;

/// <summary>
/// Structured logger for E2E test sessions.
/// Writes timestamped entries to TestResults/Logs/ and supports flush to xUnit ITestOutputHelper.
/// SPEC-E2E-002: REQ-E2E2-001
/// </summary>
public sealed class E2ELogger : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly StringBuilder _buffer = new();
    private readonly Stopwatch _sessionTimer = Stopwatch.StartNew();
    private bool _disposed;

    public E2ELogger()
    {
        var dir = Path.Combine("TestResults", "Logs");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"e2e_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        _writer = new StreamWriter(path, append: false) { AutoFlush = true };
        Info($"=== E2E Session Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
    }

    /// <summary>Logs an informational message.</summary>
    public void Info(string message) => Write("INFO", message);

    /// <summary>Logs a test step (highlighted in output).</summary>
    public void Step(string message) => Write("STEP", message);

    /// <summary>Logs a warning.</summary>
    public void Warn(string message) => Write("WARN", message);

    /// <summary>Logs a failure with context.</summary>
    public void Fail(string message) => Write("FAIL", message);

    private void Write(string level, string message)
    {
        var entry = $"[{DateTime.Now:HH:mm:ss.fff}] [{level,-4}] {message}";
        _buffer.AppendLine(entry);
        _writer.WriteLine(entry);
        Trace.WriteLine(entry);
    }

    /// <summary>Flushes accumulated log buffer to xUnit test output and clears the buffer.</summary>
    public void FlushTo(ITestOutputHelper output)
    {
        output.WriteLine(_buffer.ToString());
        _buffer.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Info($"=== E2E Session Ended. Total: {_sessionTimer.Elapsed.TotalSeconds:F1}s ===");
        _writer.Dispose();
    }
}
