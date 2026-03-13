using System.Diagnostics;
using System.Text;
using Xunit.Abstractions;

namespace XrayDetector.Gui.E2ETests.Infrastructure;

/// <summary>
/// Structured logger for E2E test sessions.
/// - Session sink: writes all entries to TestResults/Logs/e2e_{timestamp}.log (AutoFlush, survives crashes)
/// - Per-test buffer: accumulates entries since BeginTest(), flushed to ITestOutputHelper in EndTest()
/// - AsyncLocal bridge: forwards log entries to xUnit ITestOutputHelper in real-time
/// - Format: [HH:mm:ss.fff] [LEVEL] [TestName] message
/// SPEC-E2E-002: REQ-E2E2-001
/// TAG-002: AsyncLocal ITestOutputHelper bridge for real-time xUnit output
/// </summary>
public sealed class E2ELogger : IDisposable
{
    // AsyncLocal ensures per-async-context output (safe for parallel async tests)
    private static readonly AsyncLocal<ITestOutputHelper?> _testOutput = new();

    // TAG-005: XRAY_E2E_DEBUG=1 enables verbose logging (every WaitHelper attempt, not every 10)
    private static bool IsDebugMode =>
        Environment.GetEnvironmentVariable("XRAY_E2E_DEBUG") == "1";

    private readonly StreamWriter _writer;
    private readonly Stopwatch _sessionTimer = Stopwatch.StartNew();
    private readonly StringBuilder _testBuffer = new();
    private bool _disposed;
    private string _currentTest = "(fixture)";

    // ── Static AsyncLocal bridge ─────────────────────────────────────────────

    /// <summary>
    /// Sets the xUnit ITestOutputHelper for the current async context.
    /// Call from E2ETestBase constructor. TAG-002.
    /// </summary>
    public static void SetTestOutput(ITestOutputHelper output) => _testOutput.Value = output;

    /// <summary>
    /// Clears the xUnit ITestOutputHelper for the current async context.
    /// Call from E2ETestBase.DisposeAsync(). TAG-002.
    /// </summary>
    public static void ClearTestOutput() => _testOutput.Value = null;

    public E2ELogger()
    {
        var dir = Path.Combine("TestResults", "Logs");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"e2e_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        _writer = new StreamWriter(path, append: false) { AutoFlush = true };
        Info("=== E2E Session Started ===");
    }

    // ── Log level methods ───────────────────────────────────────────

    /// <summary>Logs an informational message.</summary>
    public void Info(string message) => Write("INFO", message);

    /// <summary>Logs a test step (fixture phases, warmup timing).</summary>
    public void Step(string message) => Write("STEP", message);

    /// <summary>Logs a FlaUI click action.</summary>
    public void Click(string automationId) => Write("CLCK", $"Click: {automationId}");

    /// <summary>Logs a FlaUI element find result.</summary>
    public void Find(string automationId, bool found) =>
        Write(found ? "FIND" : "MISS", $"Find: {automationId} → {(found ? "ok" : "null")}");

    /// <summary>Logs a warning.</summary>
    public void Warn(string message) => Write("WARN", message);

    /// <summary>Logs a failure with context (e.g. timeout tree dump).</summary>
    public void Fail(string message) => Write("FAIL", message);

    // ── Static helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Forwards a retry attempt log entry via the AsyncLocal xUnit output bridge.
    /// Called by RetryHelper (TAG-006). Does not require an E2ELogger instance.
    /// </summary>
    internal static void WriteRetryAttempt(int attempt, int maxRetries, string exceptionMessage)
    {
        var msg = $"[RetryHelper] retry attempt {attempt}/{maxRetries}: {exceptionMessage}";
        _testOutput.Value?.WriteLine($"[E2E] [{DateTime.Now:HH:mm:ss.fff}] [RTRY] {msg}");
    }

    // ── Per-test lifecycle ──────────────────────────────────────────

    /// <summary>
    /// Marks the start of a test. Clears the per-test buffer and sets test context.
    /// Called by E2ETestBase.InitializeAsync().
    /// </summary>
    public void BeginTest(string testName)
    {
        _currentTest = testName;
        _testBuffer.Clear();
        Write("TEST", $">>> BEGIN {testName}");
    }

    /// <summary>
    /// Flushes per-test buffer to xUnit ITestOutputHelper and clears it.
    /// Called by E2ETestBase.DisposeAsync().
    /// </summary>
    public void EndTest(ITestOutputHelper output, bool passed)
    {
        Write("TEST", $"<<< END {_currentTest} [{(passed ? "PASSED" : "FAILED")}]");
        output.WriteLine(_testBuffer.ToString());
        _testBuffer.Clear();
        _currentTest = "(fixture)";
    }

    // ── Internal ────────────────────────────────────────────────────

    private void Write(string level, string message)
    {
        var entry = $"[{DateTime.Now:HH:mm:ss.fff}] [{level,-4}] [{_currentTest}] {message}";
        _testBuffer.AppendLine(entry);
        _writer.WriteLine(entry);
        Trace.WriteLine(entry);
        // TAG-002: Real-time forward to xUnit output via AsyncLocal bridge
        _testOutput.Value?.WriteLine($"[E2E] {entry}");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Info($"=== E2E Session Ended. Total: {_sessionTimer.Elapsed.TotalSeconds:F1}s ===");
        _writer.Dispose();
    }
}
