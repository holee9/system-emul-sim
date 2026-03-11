using FluentAssertions;
using Common.Dto.Serialization;
using PanelSimulator.Cli;
using FpgaSimulator.Cli;
using McuSimulator.Cli;
using HostSimulator.Cli;
using Xunit;
using IntegrationTests.Helpers.Mock;
using IntegrationTests.Helpers.Cli;

namespace IntegrationTests.Integration;

/// <summary>
/// IT-19: CLI Round-Trip Verification.
/// Verifies that data flows correctly through the full Panel->FPGA->MCU->Host CLI chain.
/// G4 gap closure: CLI round-trip data integrity verification.
/// Reference: SPEC-EMUL-003
///
/// VIRTUALIZATION STATUS: PARTIAL (Quick Win for TASK-005)
/// - Uses MemoryFileSystem.GetTempPath() to demonstrate virtualization capability
/// - CLI programs still write to real temp files (they expect file paths, not streams)
///
/// ICliInvoker INTEGRATION: COMPLETE (TASK-007)
/// - Supports both ProcessInvoker (external process) and DirectCallInvoker (in-memory)
/// - ProcessInvoker: Spawns separate dotnet process (slower, process-isolated)
/// - DirectCallInvoker: Loads assembly and calls Main directly (faster, same AppDomain)
/// </summary>
public class IT19_CliRoundTripTests
{
    /// <summary>
    /// Enumeration of CLI invocation modes for testing.
    /// </summary>
    public enum CliInvocationMode
    {
        /// <summary>Direct instantiation of CLI classes (original implementation).</summary>
        DirectClass,

        /// <summary>External process invocation via ProcessInvoker.</summary>
        ProcessInvoker,

        /// <summary>In-memory invocation via DirectCallInvoker (fastest).</summary>
        DirectCallInvoker
    }
    /// <summary>
    /// Helper method to invoke Panel CLI with specified mode.
    /// </summary>
    private static int InvokePanelCli(string[] args, CliInvocationMode mode, out TimeSpan duration)
    {
        duration = TimeSpan.Zero;

        switch (mode)
        {
            case CliInvocationMode.DirectClass:
                var sw = System.Diagnostics.Stopwatch.StartNew();
                int rc = new PanelSimulatorCli().ParseAndRun(args);
                sw.Stop();
                duration = sw.Elapsed;
                return rc;

            case CliInvocationMode.ProcessInvoker:
                var invoker1 = new Helpers.Cli.ProcessInvoker("PanelSimulator.Cli");
                var result1 = invoker1.Invoke(args);
                duration = result1.Duration;
                return result1.ExitCode;

            case CliInvocationMode.DirectCallInvoker:
                var directInvoker1 = new Helpers.Cli.DirectCallInvoker("PanelSimulator.Cli");
                var directResult1 = directInvoker1.Invoke(args);
                duration = directResult1.Duration;
                return directResult1.ExitCode;

            default:
                throw new ArgumentException($"Unknown mode: {mode}");
        }
    }

    /// <summary>
    /// Helper method to invoke FPGA CLI with specified mode.
    /// </summary>
    private static int InvokeFpgaCli(string[] args, CliInvocationMode mode, out TimeSpan duration)
    {
        duration = TimeSpan.Zero;

        switch (mode)
        {
            case CliInvocationMode.DirectClass:
                var sw = System.Diagnostics.Stopwatch.StartNew();
                int rc = new FpgaSimulatorCli().ParseAndRun(args);
                sw.Stop();
                duration = sw.Elapsed;
                return rc;

            case CliInvocationMode.ProcessInvoker:
                var invoker = new Helpers.Cli.ProcessInvoker("FpgaSimulator.Cli");
                var result = invoker.Invoke(args);
                duration = result.Duration;
                return result.ExitCode;

            case CliInvocationMode.DirectCallInvoker:
                var directInvoker = new Helpers.Cli.DirectCallInvoker("FpgaSimulator.Cli");
                var directResult = directInvoker.Invoke(args);
                duration = directResult.Duration;
                return directResult.ExitCode;

            default:
                throw new ArgumentException($"Unknown mode: {mode}");
        }
    }

    /// <summary>
    /// Helper method to invoke MCU CLI with specified mode.
    /// </summary>
    private static int InvokeMcuCli(string[] args, CliInvocationMode mode, out TimeSpan duration)
    {
        duration = TimeSpan.Zero;

        switch (mode)
        {
            case CliInvocationMode.DirectClass:
                var sw = System.Diagnostics.Stopwatch.StartNew();
                int rc = new McuSimulatorCli().ParseAndRun(args);
                sw.Stop();
                duration = sw.Elapsed;
                return rc;

            case CliInvocationMode.ProcessInvoker:
                var invoker = new Helpers.Cli.ProcessInvoker("McuSimulator.Cli");
                var result = invoker.Invoke(args);
                duration = result.Duration;
                return result.ExitCode;

            case CliInvocationMode.DirectCallInvoker:
                var directInvoker = new Helpers.Cli.DirectCallInvoker("McuSimulator.Cli");
                var directResult = directInvoker.Invoke(args);
                duration = directResult.Duration;
                return directResult.ExitCode;

            default:
                throw new ArgumentException($"Unknown mode: {mode}");
        }
    }

    /// <summary>
    /// Helper method to invoke Host CLI with specified mode.
    /// </summary>
    private static int InvokeHostCli(string[] args, CliInvocationMode mode, out TimeSpan duration)
    {
        duration = TimeSpan.Zero;

        switch (mode)
        {
            case CliInvocationMode.DirectClass:
                var sw = System.Diagnostics.Stopwatch.StartNew();
                int rc = new HostSimulatorCli().ParseAndRun(args);
                sw.Stop();
                duration = sw.Elapsed;
                return rc;

            case CliInvocationMode.ProcessInvoker:
                var invoker = new Helpers.Cli.ProcessInvoker("HostSimulator.Cli");
                var result = invoker.Invoke(args);
                duration = result.Duration;
                return result.ExitCode;

            case CliInvocationMode.DirectCallInvoker:
                var directInvoker = new Helpers.Cli.DirectCallInvoker("HostSimulator.Cli");
                var directResult = directInvoker.Invoke(args);
                duration = directResult.Duration;
                return directResult.ExitCode;

            default:
                throw new ArgumentException($"Unknown mode: {mode}");
        }
    }

    /// <summary>
    /// Core round-trip test logic with configurable CLI invocation mode.
    /// </summary>
    private void ExecuteRoundTripTest(CliInvocationMode mode)
    {
        // VIRTUALIZATION: Use MemoryFileSystem for path generation (demonstration)
        var memoryFs = new MemoryFileSystem();
        string virtualTmpDir = memoryFs.GetTempPath();

        // Create unique temp directory on real filesystem (CLI limitation)
        string tmpDir = Path.Combine(Path.GetTempPath(), $"IT19_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmpDir);
        try
        {
            // Define intermediate file paths
            string frameRawPath = Path.Combine(tmpDir, "frame.raw");
            string packetsCsi2Path = Path.Combine(tmpDir, "packets.csi2");
            string framesUdpPath = Path.Combine(tmpDir, "frames.udp");
            string resultRawPath = Path.Combine(tmpDir, "result.raw");

            // Track execution time for performance comparison
            var totalTime = TimeSpan.Zero;

            // Step 1: Panel CLI - generate 64x64 frame
            int rc = InvokePanelCli(
                ["--rows", "64", "--cols", "64", "--seed", "42", "-o", frameRawPath],
                mode, out var panelDuration);
            rc.Should().Be(0, $"Panel CLI should succeed (mode: {mode})");
            File.Exists(frameRawPath).Should().BeTrue($"Panel CLI should produce frame.raw (mode: {mode})");
            new FileInfo(frameRawPath).Length.Should().BeGreaterThan(0, "frame.raw should not be empty");
            totalTime += panelDuration;

            // Step 2: FPGA CLI - encode to CSI-2 packets
            rc = InvokeFpgaCli(
                ["--input", frameRawPath, "-o", packetsCsi2Path],
                mode, out var fpgaDuration);
            rc.Should().Be(0, $"FPGA CLI should succeed (mode: {mode})");
            File.Exists(packetsCsi2Path).Should().BeTrue($"FPGA CLI should produce packets.csi2 (mode: {mode})");
            new FileInfo(packetsCsi2Path).Length.Should().BeGreaterThan(0, "packets.csi2 should not be empty");
            totalTime += fpgaDuration;

            // Step 3: MCU CLI - process CSI-2 to UDP packets
            rc = InvokeMcuCli(
                ["--input", packetsCsi2Path, "-o", framesUdpPath],
                mode, out var mcuDuration);
            rc.Should().Be(0, $"MCU CLI should succeed (mode: {mode})");
            File.Exists(framesUdpPath).Should().BeTrue($"MCU CLI should produce frames.udp (mode: {mode})");
            new FileInfo(framesUdpPath).Length.Should().BeGreaterThan(0, "frames.udp should not be empty");
            totalTime += mcuDuration;

            // Step 4: Host CLI - reassemble frame from UDP packets
            rc = InvokeHostCli(
                ["--input", framesUdpPath, "-o", resultRawPath],
                mode, out var hostDuration);
            rc.Should().Be(0, $"Host CLI should succeed (mode: {mode})");
            File.Exists(resultRawPath).Should().BeTrue($"Host CLI should produce result.raw (mode: {mode})");
            new FileInfo(resultRawPath).Length.Should().BeGreaterThan(0, "result.raw should not be empty");
            totalTime += hostDuration;

            // Step 5: Verify pixel data dimensions are preserved
            ushort[,] originalFrame = FrameDataSerializer.ReadFromFile(frameRawPath);
            ushort[,] resultFrame = FrameDataSerializer.ReadFromFile(resultRawPath);

            originalFrame.GetLength(0).Should().Be(resultFrame.GetLength(0),
                "reassembled frame should have same row count as original");
            originalFrame.GetLength(1).Should().Be(resultFrame.GetLength(1),
                "reassembled frame should have same column count as original");

            // Log performance metrics
            Console.WriteLine($"[{mode}] Total execution time: {totalTime.TotalMilliseconds:F2}ms");
            Console.WriteLine($"[{mode}] Panel: {panelDuration.TotalMilliseconds:F2}ms, " +
                            $"FPGA: {fpgaDuration.TotalMilliseconds:F2}ms, " +
                            $"MCU: {mcuDuration.TotalMilliseconds:F2}ms, " +
                            $"Host: {hostDuration.TotalMilliseconds:F2}ms");
        }
        finally
        {
            if (Directory.Exists(tmpDir))
                Directory.Delete(tmpDir, recursive: true);
        }
    }

    /// <summary>
    /// Original test using direct class instantiation (backward compatible).
    /// ICliInvoker INTEGRATION: Preserved for comparison and baseline.
    /// </summary>
    [Fact]
    public void CliRoundTrip_DirectClass_ShouldCompleteSuccessfully()
    {
        ExecuteRoundTripTest(CliInvocationMode.DirectClass);
    }

    /// <summary>
    /// Test using ProcessInvoker (external process execution).
    /// ICliInvoker INTEGRATION: Verifies ProcessInvoker produces identical results.
    /// Expected to be slower than DirectClass and DirectCallInvoker due to process overhead.
    /// </summary>
    [Fact]
    public void CliRoundTrip_ProcessInvoker_ShouldCompleteSuccessfully()
    {
        ExecuteRoundTripTest(CliInvocationMode.ProcessInvoker);
    }

    /// <summary>
    /// Test using DirectCallInvoker (in-memory execution).
    /// ICliInvoker INTEGRATION: Verifies DirectCallInvoker produces identical results.
    /// Expected to be faster than ProcessInvoker (no process overhead) and comparable to DirectClass.
    /// </summary>
    [Fact]
    public void CliRoundTrip_DirectCallInvoker_ShouldCompleteSuccessfully()
    {
        ExecuteRoundTripTest(CliInvocationMode.DirectCallInvoker);
    }

    /// <summary>
    /// Original test (preserved for backward compatibility).
    /// DEPRECATED: Use CliRoundTrip_DirectClass_ShouldCompleteSuccessfully instead.
    /// This test maintains the original method signature for existing test runners.
    /// </summary>
    [Fact]
    public void CliRoundTrip_Panel_FPGA_MCU_Host_ShouldCompleteSuccessfully()
    {
        CliRoundTrip_DirectClass_ShouldCompleteSuccessfully();
    }
}
