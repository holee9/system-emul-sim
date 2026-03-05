namespace IntegrationRunner.Core.Models;

/// <summary>
/// Defines integration test scenarios IT-01 through IT-10.
/// REQ-TOOLS-030: IntegrationRunner shall execute IT-01 through IT-10.
/// </summary>
public enum TestScenario
{
    /// <summary>IT-01: Single Frame, Minimum Tier</summary>
    IT01_SingleFrameMinimum,

    /// <summary>IT-02: 1000-Frame Continuous, Intermediate-A Tier</summary>
    IT02_1000FrameContinuous,

    /// <summary>IT-03: Out-of-Order UDP Packet Handling</summary>
    IT03_OutOfOrderPackets,

    /// <summary>IT-04: Error Injection and Recovery</summary>
    IT04_ErrorInjection,

    /// <summary>IT-05: Runtime Configuration Change</summary>
    IT05_ConfigurationChange,

    /// <summary>IT-06: Mode Transition</summary>
    IT06_ModeTransition,

    /// <summary>IT-07: Packet Loss and Network Resilience</summary>
    IT07_PacketLoss,

    /// <summary>IT-08: Simultaneous Connection Requests</summary>
    IT08_SimultaneousConnections,

    /// <summary>IT-09: 10,000-Frame Long-Duration Stability</summary>
    IT09_LongDurationStability,

    /// <summary>IT-10: Bandwidth Limit Testing (all sub-tests)</summary>
    IT10_BandwidthLimits,

    /// <summary>All scenarios (IT-01 through IT-10)</summary>
    All
}

/// <summary>
/// Performance tier definitions for detector configurations.
/// </summary>
public enum PerformanceTier
{
    /// <summary>Minimum: 1024x1024, 14-bit, 15fps (~0.21 Gbps)</summary>
    Minimum,

    /// <summary>Intermediate-A: 2048x2048, 16-bit, 15fps (~1.01 Gbps)</summary>
    IntermediateA,

    /// <summary>Intermediate-B: 2048x2048, 16-bit, 30fps (~2.01 Gbps)</summary>
    IntermediateB,

    /// <summary>Target: 3072x3072, 16-bit, 15fps (~2.26 Gbps)</summary>
    Target
}

/// <summary>
/// Test result status.
/// </summary>
public enum TestStatus
{
    /// <summary>Test not yet executed</summary>
    Pending,

    /// <summary>Test currently running</summary>
    Running,

    /// <summary>Test passed all criteria</summary>
    Passed,

    /// <summary>Test failed one or more criteria</summary>
    Failed,

    /// <summary>Test skipped (e.g., conditional on 800M debugging)</summary>
    Skipped
}

/// <summary>
/// Represents the result of a single test scenario execution.
/// REQ-TOOLS-032: Report pass/fail with metrics.
/// </summary>
public class TestResult
{
    /// <summary>Scenario identifier</summary>
    public TestScenario Scenario { get; set; }

    /// <summary>Test status</summary>
    public TestStatus Status { get; set; }

    /// <summary>Execution time in milliseconds</summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>Number of frames processed</summary>
    public int FramesProcessed { get; set; }

    /// <summary>Number of bit errors detected</summary>
    public int BitErrors { get; set; }

    /// <summary>Number of frames dropped</summary>
    public int FrameDrops { get; set; }

    /// <summary>Measured throughput in Gbps</summary>
    public double ThroughputGbps { get; set; }

    /// <summary>List of failure messages (empty if passed)</summary>
    public List<string> FailureMessages { get; set; } = new();

    /// <summary>List of warnings (non-fatal issues)</summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>Additional metrics as key-value pairs</summary>
    public Dictionary<string, object> AdditionalMetrics { get; set; } = new();

    /// <summary>Gets whether the test passed</summary>
    public bool Passed => Status == TestStatus.Passed;

    /// <summary>Gets a summary string for console output</summary>
    public string GetSummary()
    {
        string statusIcon = Status switch
        {
            TestStatus.Passed => "[PASS]",
            TestStatus.Failed => "[FAIL]",
            TestStatus.Skipped => "[SKIP]",
            TestStatus.Running => "[RUN ]",
            _ => "[PEND]"
        };

        return $"{statusIcon} {Scenario} | " +
               $"Frames: {FramesProcessed} | " +
               $"Errors: {BitErrors} | " +
               $"Drops: {FrameDrops} | " +
               $"Throughput: {ThroughputGbps:F3} Gbps | " +
               $"Time: {ExecutionTimeMs} ms";
    }
}

/// <summary>
/// Aggregate results for --all flag execution.
/// REQ-TOOLS-033: Support --all flag for aggregate results.
/// </summary>
public class AggregateResults
{
    /// <summary>Total number of tests executed</summary>
    public int TotalTests { get; set; }

    /// <summary>Number of tests passed</summary>
    public int PassedTests { get; set; }

    /// <summary>Number of tests failed</summary>
    public int FailedTests { get; set; }

    /// <summary>Number of tests skipped</summary>
    public int SkippedTests { get; set; }

    /// <summary>Individual test results</summary>
    public List<TestResult> TestResults { get; set; } = new();

    /// <summary>Total execution time in milliseconds</summary>
    public long TotalExecutionTimeMs { get; set; }

    /// <summary>Gets whether all tests passed (non-skipped)</summary>
    public bool AllPassed => FailedTests == 0;

    /// <summary>Gets pass rate as percentage</summary>
    public double PassRate => TotalTests > 0 ? (double)PassedTests / TotalTests * 100 : 0;

    /// <summary>Prints aggregate summary to console</summary>
    public void PrintSummary()
    {
        Console.WriteLine();
        Console.WriteLine("=== Aggregate Test Results ===");
        Console.WriteLine($"Total Tests:   {TotalTests}");
        Console.WriteLine($"Passed:        {PassedTests} ({PassRate:F1}%)");
        Console.WriteLine($"Failed:        {FailedTests}");
        Console.WriteLine($"Skipped:       {SkippedTests}");
        Console.WriteLine($"Total Time:    {TotalExecutionTimeMs / 1000.0:F2} seconds");
        Console.WriteLine();

        foreach (var result in TestResults)
        {
            Console.WriteLine(result.GetSummary());
            foreach (var warning in result.Warnings)
            {
                Console.WriteLine($"  [WARN] {warning}");
            }
            foreach (var error in result.FailureMessages)
            {
                Console.WriteLine($"  [ERROR] {error}");
            }
        }

        Console.WriteLine();
        Console.WriteLine(AllPassed ? "[SUCCESS] All tests passed!" : "[FAILURE] Some tests failed!");
    }
}
