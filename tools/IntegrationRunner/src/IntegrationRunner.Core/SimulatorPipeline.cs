using System.Diagnostics;
using Common.Dto.Dtos;
using FpgaSimulator.Core.Fsm;
using FpgaSimulator.Core.Protection;
using FpgaCsi2 = FpgaSimulator.Core.Csi2;
using IntegrationRunner.Core.Models;
using IntegrationRunner.Core.Network;
using McuSimulator.Core.Frame;
using McuSimulator.Core.Network;
using McuSimulator.Core.Sequence;
using HostSimulatorConfig = HostSimulator.Core.Configuration.HostConfig;
using PanelConfig = PanelSimulator.Models.PanelConfig;
using TestPattern = PanelSimulator.Models.TestPattern;
using NoiseModelType = PanelSimulator.Models.NoiseModelType;

namespace IntegrationRunner.Core;

/// <summary>
/// Pipeline statistics snapshot.
/// </summary>
public sealed class PipelineStatistics
{
    /// <summary>Total frames processed through the pipeline.</summary>
    public int FramesProcessed { get; init; }

    /// <summary>Total frames successfully completed.</summary>
    public int FramesCompleted { get; init; }

    /// <summary>Total frames that failed due to errors or packet loss.</summary>
    public int FramesFailed { get; init; }

    /// <summary>Network channel statistics (null if no channel configured).</summary>
    public NetworkChannelStats? NetworkStats { get; init; }
}

/// <summary>
/// Network statistics snapshot.
/// </summary>
public sealed class NetworkChannelStats
{
    /// <summary>Packets sent through the channel.</summary>
    public long PacketsSent { get; init; }

    /// <summary>Packets lost due to simulated loss.</summary>
    public long PacketsLost { get; init; }

    /// <summary>Packets reordered by the channel.</summary>
    public long PacketsReordered { get; init; }

    /// <summary>Packets corrupted by the channel.</summary>
    public long PacketsCorrupted { get; init; }
}

/// <summary>
/// Manages the simulator pipeline for integration testing.
/// REQ-TOOLS-031: Instantiate all required simulators, connect in pipeline order.
/// Pipeline: Panel -> FPGA -> MCU -> Network -> Host
/// </summary>
public class SimulatorPipeline
{
    private readonly object _lock = new();
    private PanelSimulator.PanelSimulator? _panelSimulator;
    private FpgaCsi2.Csi2TxPacketGenerator? _csi2Generator;
    private ProtectionLogicSimulator? _protectionLogic;
    private SequenceEngine? _sequenceEngine;
    private NetworkChannel? _networkChannel;
    private bool _isInitialized;
    private bool _hasFatalError;
    private double _frameLossRate;    // Frame-level loss rate for IT07 simulation
    private readonly Random _random = new(42);
    private uint _frameCounter;
    private int _framesProcessed;
    private int _framesCompleted;
    private int _framesFailed;

    /// <summary>Gets whether the pipeline is initialized.</summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>Gets the current configuration.</summary>
    public DetectorConfig? Config { get; private set; }

    /// <summary>Gets the frame counter.</summary>
    public uint FrameCounter => _frameCounter;

    /// <summary>Gets whether the pipeline has a fatal error that stops processing.</summary>
    public bool HasFatalError => _hasFatalError;

    /// <summary>
    /// Initializes the simulator pipeline with the given configuration.
    /// Resets all state including any previously injected errors.
    /// </summary>
    public void Initialize(DetectorConfig config)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));

        int rows = config.Panel?.Rows ?? 1024;
        int cols = config.Panel?.Cols ?? 1024;
        int bitDepth = config.Panel?.BitDepth ?? 14;

        // Initialize PanelSimulator
        _panelSimulator = new PanelSimulator.PanelSimulator();
        var panelConfig = new PanelConfig
        {
            Rows = rows,
            Cols = cols,
            BitDepth = bitDepth,
            TestPattern = ParseTestPattern(config.Simulation?.TestPattern ?? "counter"),
            NoiseModel = NoiseModelType.None,
            NoiseStdDev = config.Simulation?.NoiseStdDev ?? 0,
            DefectRate = 0,
            Seed = config.Simulation?.Seed ?? 42
        };
        _panelSimulator.Initialize(panelConfig);

        // Initialize FPGA CSI-2 packet generator
        _csi2Generator = new FpgaCsi2.Csi2TxPacketGenerator(virtualChannel: 0, FpgaCsi2.Csi2DataType.Raw16);

        // Initialize protection logic (fresh, no errors)
        _protectionLogic = new ProtectionLogicSimulator();

        // Initialize sequence engine (fresh, Idle state)
        _sequenceEngine = new SequenceEngine();

        // Network channel is reset to null (no impairment by default)
        _networkChannel = new NetworkChannel(new NetworkChannelConfig());

        // Reset all state
        _hasFatalError = false;
        _frameLossRate = 0.0;
        _frameCounter = 0;
        _framesProcessed = 0;
        _framesCompleted = 0;
        _framesFailed = 0;
        _isInitialized = true;
    }

    /// <summary>
    /// Resets all simulators to their initial state.
    /// </summary>
    public void Reset()
    {
        _panelSimulator?.Reset();
        _protectionLogic?.Reset();
        _hasFatalError = false;
        _frameCounter = 0;
        _framesProcessed = 0;
        _framesCompleted = 0;
        _framesFailed = 0;
    }

    /// <summary>
    /// Processes a single frame through the full 4-layer pipeline.
    /// Panel -> FPGA CSI-2 -> MCU UDP -> Network -> Host
    /// Returns null if pipeline has a fatal error or if frame fails to reassemble.
    /// </summary>
    public FrameData? ProcessFrame()
    {
        lock (_lock)
        {
            if (!_isInitialized || _panelSimulator == null || _csi2Generator == null)
                throw new InvalidOperationException("Pipeline not initialized. Call Initialize first.");

            _framesProcessed++;

            // Fatal error short-circuit: stops all frame processing
            if (_hasFatalError)
            {
                _framesFailed++;
                return null;
            }

            // Frame-level loss simulation (for IT07 and similar scenarios)
            if (_frameLossRate > 0.0 && _random.NextDouble() < _frameLossRate)
            {
                _framesFailed++;
                return null;
            }

            try
            {
                // Layer 1: Panel - Generate pixel data (1D FrameData)
                var panelOutput = (FrameData)_panelSimulator.Process(new object());

                int rows = panelOutput.Height;
                int cols = panelOutput.Width;

                // Convert 1D to 2D for FPGA input
                var pixels2D = ConvertTo2D(panelOutput.Pixels, rows, cols);

                // Layer 2: FPGA - Encode to CSI-2 packets
                var csi2Packets = _csi2Generator.GenerateFullFrame(pixels2D);

                // Layer 3: MCU - Reassemble CSI-2, then fragment to UDP
                var mcuReassembler = new FrameReassembler();
                foreach (var pkt in csi2Packets)
                {
                    mcuReassembler.AddPacket(pkt);
                }
                var reassembledFrame = mcuReassembler.GetFrame();

                var udpTransmitter = new UdpFrameTransmitter();
                var udpPackets = udpTransmitter.FragmentFrame(reassembledFrame.Pixels, _frameCounter);

                // Network channel - Apply loss/reorder/corruption
                List<UdpFramePacket> networkOutput;
                if (_networkChannel != null)
                {
                    networkOutput = _networkChannel.TransmitPackets(udpPackets);
                }
                else
                {
                    networkOutput = udpPackets;
                }

                // Layer 4: Host - Reassemble UDP packets into FrameData
                var hostConfig = new HostSimulatorConfig { PacketTimeoutMs = 5000 };
                var hostSim = new HostSimulator.Core.HostSimulator();
                hostSim.Initialize(hostConfig);

                FrameData? hostOutput = null;
                foreach (var udpPacket in networkOutput)
                {
                    var result = hostSim.Process(udpPacket.Data);
                    if (result is FrameData fd)
                    {
                        hostOutput = fd;
                    }
                }

                _frameCounter++;

                if (hostOutput != null)
                {
                    _framesCompleted++;
                    return hostOutput;
                }
                else
                {
                    _framesFailed++;
                    return null;
                }
            }
            catch
            {
                _framesFailed++;
                return null;
            }
        }
    }

    /// <summary>
    /// Processes multiple frames through the pipeline.
    /// Null frames (from errors or packet loss) are skipped.
    /// </summary>
    public List<FrameData> ProcessFrames(int count)
    {
        var frames = new List<FrameData>(count);

        for (int i = 0; i < count; i++)
        {
            var frame = ProcessFrame();
            if (frame != null)
            {
                frames.Add(frame);
            }
        }

        return frames;
    }

    /// <summary>
    /// Gets the status of all simulators in the pipeline.
    /// </summary>
    public string GetPipelineStatus()
    {
        var status = new List<string>
        {
            $"Pipeline: {(IsInitialized ? "Initialized" : "Not Initialized")}",
            $"Frame Counter: {_frameCounter}",
            $"Fatal Error: {_hasFatalError}",
            _panelSimulator?.GetStatus() ?? "Panel: Not Initialized",
            $"CSI-2 Generator: {(_csi2Generator != null ? "Ready" : "Not Initialized")}",
            $"Protection Logic: {(_protectionLogic?.ErrorFlags.ToString() ?? "Not Initialized")}",
            $"Sequence Engine: {(_sequenceEngine?.State.ToString() ?? "Not Initialized")}"
        };

        return string.Join(Environment.NewLine, status);
    }

    /// <summary>
    /// Gets a snapshot of pipeline statistics.
    /// </summary>
    public PipelineStatistics GetStatistics()
    {
        lock (_lock)
        {
            NetworkChannelStats? networkStats = null;
            if (_networkChannel != null)
            {
                networkStats = new NetworkChannelStats
                {
                    PacketsSent = _networkChannel.PacketsSent,
                    PacketsLost = _networkChannel.PacketsLost,
                    PacketsReordered = _networkChannel.PacketsReordered,
                    PacketsCorrupted = _networkChannel.PacketsCorrupted
                };
            }

            return new PipelineStatistics
            {
                FramesProcessed = _framesProcessed,
                FramesCompleted = _framesCompleted,
                FramesFailed = _framesFailed,
                NetworkStats = networkStats
            };
        }
    }

    /// <summary>
    /// Injects an error into the pipeline for testing error handling.
    /// Fatal errors (TIMEOUT, OVERFLOW) stop all subsequent frame processing.
    /// Non-fatal errors (CRC, RECOVERABLE) allow processing to continue.
    /// Used by IT-04 (Error Injection and Recovery).
    /// </summary>
    /// <param name="errorType">Type of error: TIMEOUT, OVERFLOW, CRC, RECOVERABLE, or watchdog/readout_timeout/buffer_overflow/csi2_error/roic_fault/config_error</param>
    public void InjectError(string errorType)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("Pipeline not initialized.");
        if (_protectionLogic == null)
            throw new InvalidOperationException("Protection logic not initialized.");

        var normalized = errorType.ToUpperInvariant();

        var (error, isFatal) = normalized switch
        {
            // TestScenarioExecutor strings
            "TIMEOUT" => (ProtectionError.WatchdogTimeout, true),
            "OVERFLOW" => (ProtectionError.BufferOverflow, true),
            "CRC" => (ProtectionError.Csi2Error, false),
            "RECOVERABLE" => (ProtectionError.ReadoutTimeout, false),
            // Reference implementation strings (lowercase)
            "WATCHDOG" => (ProtectionError.WatchdogTimeout, true),
            "READOUT_TIMEOUT" => (ProtectionError.ReadoutTimeout, false),
            "BUFFER_OVERFLOW" => (ProtectionError.BufferOverflow, false),
            "CSI2_ERROR" => (ProtectionError.Csi2Error, false),
            "ROIC_FAULT" => (ProtectionError.RoicFault, true),
            "CONFIG_ERROR" => (ProtectionError.ConfigError, true),
            _ => throw new ArgumentException($"Unknown error type: {errorType}", nameof(errorType))
        };

        _protectionLogic.ReportError(error, isFatal);

        if (isFatal)
        {
            _hasFatalError = true;
        }
    }

    /// <summary>
    /// Reconfigures the pipeline for a new configuration.
    /// Used by IT-05 (Runtime Configuration Change).
    /// </summary>
    public void Reconfigure(DetectorConfig newConfig)
    {
        if (newConfig == null)
            throw new ArgumentNullException(nameof(newConfig));

        Reset();
        Initialize(newConfig);
    }

    /// <summary>
    /// Sets the scan mode via the SequenceEngine FSM.
    /// Used by IT-06 (Mode Transition).
    /// </summary>
    public void SetScanMode(ScanMode mode)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("Pipeline not initialized.");

        _sequenceEngine?.StartScan(mode);
    }

    /// <summary>
    /// Sets the packet loss rate for the pipeline.
    /// Applies frame-level drop simulation: frames are dropped with this probability.
    /// Also configures the network channel for packet-level loss when available.
    /// Used by IT-07 (Packet Loss and Network Resilience).
    /// </summary>
    /// <param name="lossRate">Packet loss rate (0.0 to 1.0).</param>
    public void SetPacketLossRate(double lossRate)
    {
        if (lossRate < 0.0 || lossRate > 1.0)
            throw new ArgumentOutOfRangeException(nameof(lossRate), "Loss rate must be between 0.0 and 1.0");

        _frameLossRate = lossRate;
        _networkChannel?.SetLossRate(lossRate);
    }

    /// <summary>
    /// Sets the packet reorder rate on the network channel.
    /// Used by IT-03 (Out-of-Order UDP Packet Handling).
    /// </summary>
    /// <param name="reorderRate">Packet reorder rate (0.0 to 1.0).</param>
    public void SetPacketReorderRate(double reorderRate)
    {
        if (reorderRate < 0.0 || reorderRate > 1.0)
            throw new ArgumentOutOfRangeException(nameof(reorderRate), "Reorder rate must be between 0.0 and 1.0");

        _networkChannel?.SetReorderRate(reorderRate);
    }

    /// <summary>
    /// Converts a 1D pixel array to 2D [rows, cols].
    /// </summary>
    private static ushort[,] ConvertTo2D(ushort[] pixels, int rows, int cols)
    {
        var result = new ushort[rows, cols];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                result[r, c] = pixels[r * cols + c];
            }
        }
        return result;
    }

    private static TestPattern ParseTestPattern(string pattern)
    {
        return pattern.ToLowerInvariant() switch
        {
            "counter" => TestPattern.Counter,
            "checkerboard" => TestPattern.Checkerboard,
            "flatfield" or "flat_field" => TestPattern.FlatField,
            _ => TestPattern.Counter
        };
    }
}

/// <summary>
/// Test scenario executor.
/// Executes integration test scenarios and reports results.
/// REQ-TOOLS-030: Execute IT-01 through IT-10.
/// REQ-TOOLS-032: Report pass/fail with metrics.
/// </summary>
public class TestScenarioExecutor
{
    private readonly SimulatorPipeline _pipeline;
    private readonly Stopwatch _stopwatch;

    public TestScenarioExecutor()
    {
        _pipeline = new SimulatorPipeline();
        _stopwatch = new Stopwatch();
    }

    /// <summary>
    /// Executes a single test scenario.
    /// </summary>
    public TestResult ExecuteScenario(TestScenario scenario, DetectorConfig config)
    {
        var result = new TestResult
        {
            Scenario = scenario,
            Status = TestStatus.Running
        };

        _stopwatch.Restart();

        try
        {
            var testConfig = GetConfigForScenario(scenario, config);
            _pipeline.Initialize(testConfig);

            result = scenario switch
            {
                TestScenario.IT01_SingleFrameMinimum => ExecuteIT01(testConfig),
                TestScenario.IT02_1000FrameContinuous => ExecuteIT02(testConfig),
                TestScenario.IT03_OutOfOrderPackets => ExecuteIT03(testConfig),
                TestScenario.IT04_ErrorInjection => ExecuteIT04(testConfig),
                TestScenario.IT05_ConfigurationChange => ExecuteIT05(testConfig),
                TestScenario.IT06_ModeTransition => ExecuteIT06(testConfig),
                TestScenario.IT07_PacketLoss => ExecuteIT07(testConfig),
                TestScenario.IT08_SimultaneousConnections => ExecuteIT08(testConfig),
                TestScenario.IT09_LongDurationStability => ExecuteIT09(testConfig),
                TestScenario.IT10_BandwidthLimits => ExecuteIT10(testConfig),
                TestScenario.All => new TestResult { Scenario = scenario, Status = TestStatus.Skipped, Warnings = { "Use ExecuteAllScenarios for all tests" } },
                _ => new TestResult { Scenario = scenario, Status = TestStatus.Skipped, Warnings = { $"Scenario {scenario} not yet implemented" } }
            };
        }
        catch (Exception ex)
        {
            result.Status = TestStatus.Failed;
            result.FailureMessages.Add($"Exception: {ex.Message}");
        }
        finally
        {
            _stopwatch.Stop();
            result.ExecutionTimeMs = _stopwatch.ElapsedMilliseconds;
        }

        return result;
    }

    /// <summary>
    /// Executes multiple scenarios and returns aggregate results.
    /// REQ-TOOLS-033: Support --all flag for aggregate results.
    /// </summary>
    public AggregateResults ExecuteAllScenarios(DetectorConfig config)
    {
        var results = new AggregateResults();
        var sw = Stopwatch.StartNew();

        foreach (TestScenario scenario in Enum.GetValues(typeof(TestScenario)))
        {
            if (scenario == TestScenario.All) continue;

            var result = ExecuteScenario(scenario, config);
            results.TestResults.Add(result);

            switch (result.Status)
            {
                case TestStatus.Passed:
                    results.PassedTests++;
                    break;
                case TestStatus.Failed:
                    results.FailedTests++;
                    break;
                case TestStatus.Skipped:
                    results.SkippedTests++;
                    break;
            }

            results.TotalTests++;
        }

        sw.Stop();
        results.TotalExecutionTimeMs = sw.ElapsedMilliseconds;

        return results;
    }

    /// <summary>
    /// Returns the frame count for a scenario, respecting MaxFrames limit from config.
    /// MaxFrames=0 means use the scenario's default frame count.
    /// </summary>
    private static int GetFrameCount(int defaultCount, DetectorConfig config)
    {
        int maxFrames = config.Simulation?.MaxFrames ?? 0;
        return maxFrames > 0 ? Math.Min(maxFrames, defaultCount) : defaultCount;
    }

    private TestResult ExecuteIT01(DetectorConfig config)
    {
        var result = new TestResult
        {
            Scenario = TestScenario.IT01_SingleFrameMinimum,
            Status = TestStatus.Running
        };

        try
        {
            var frames = _pipeline.ProcessFrames(1);
            result.FramesProcessed = frames.Count;

            if (frames.Count == 1)
            {
                var frame = frames[0];
                bool dimensionsMatch = frame.Width == 1024 && frame.Height == 1024;
                bool pixelCountMatch = frame.Pixels.Length == 1024 * 1024;

                if (dimensionsMatch && pixelCountMatch)
                {
                    result.BitErrors = VerifyCounterPattern(frame, 14);

                    if (result.BitErrors == 0)
                    {
                        result.Status = TestStatus.Passed;
                    }
                    else
                    {
                        result.Status = TestStatus.Failed;
                        result.FailureMessages.Add($"Bit errors detected: {result.BitErrors}");
                    }
                }
                else
                {
                    result.Status = TestStatus.Failed;
                    result.FailureMessages.Add($"Frame dimensions incorrect: {frame.Width}x{frame.Height}");
                }

                double frameSizeBits = frame.Pixels.Length * 16.0;
                double throughputGbps = frameSizeBits / (_stopwatch.ElapsedMilliseconds / 1000.0) / 1e9;
                result.ThroughputGbps = throughputGbps;
            }
            else
            {
                result.Status = TestStatus.Failed;
                result.FailureMessages.Add($"Expected 1 frame, got {frames.Count}");
            }
        }
        catch (Exception ex)
        {
            result.Status = TestStatus.Failed;
            result.FailureMessages.Add(ex.Message);
        }

        return result;
    }

    private TestResult ExecuteIT02(DetectorConfig config)
    {
        var result = new TestResult
        {
            Scenario = TestScenario.IT02_1000FrameContinuous,
            Status = TestStatus.Running
        };

        try
        {
            int targetFrames = GetFrameCount(1000, config);
            var frames = _pipeline.ProcessFrames(targetFrames);
            result.FramesProcessed = frames.Count;

            if (frames.Count == targetFrames)
            {
                int firstErrors = VerifyCounterPattern(frames[0], 16);
                int lastErrors = VerifyCounterPattern(frames[frames.Count - 1], 16);

                result.BitErrors = firstErrors + lastErrors;
                result.FrameDrops = 0;

                if (result.BitErrors == 0)
                {
                    result.Status = TestStatus.Passed;

                    double frameSizeBits = 2048.0 * 2048.0 * 16.0;
                    double totalTimeSec = _stopwatch.ElapsedMilliseconds / 1000.0;
                    result.ThroughputGbps = (frameSizeBits * targetFrames) / totalTimeSec / 1e9;

                    if (targetFrames == 1000 && result.ThroughputGbps < 0.96)
                    {
                        result.Warnings.Add($"Throughput below target: {result.ThroughputGbps:F3} Gbps < 0.96 Gbps");
                    }
                }
                else
                {
                    result.Status = TestStatus.Failed;
                    result.FailureMessages.Add($"Bit errors detected: {result.BitErrors}");
                }
            }
            else
            {
                result.Status = TestStatus.Failed;
                result.FailureMessages.Add($"Expected {targetFrames} frames, got {frames.Count}");
                result.FrameDrops = targetFrames - frames.Count;
            }
        }
        catch (Exception ex)
        {
            result.Status = TestStatus.Failed;
            result.FailureMessages.Add(ex.Message);
        }

        return result;
    }

    private TestResult ExecuteIT03(DetectorConfig config)
    {
        var result = new TestResult
        {
            Scenario = TestScenario.IT03_OutOfOrderPackets,
            Status = TestStatus.Running
        };

        try
        {
            // Configure 5% packet reordering
            _pipeline.SetPacketReorderRate(0.05);

            var frames = _pipeline.ProcessFrames(100);
            result.FramesProcessed = frames.Count;

            // Verify all frames correctly reassembled despite reordering
            int totalErrors = 0;
            foreach (var frame in frames)
            {
                totalErrors += VerifyCounterPattern(frame, 16);
            }

            result.BitErrors = totalErrors;

            if (totalErrors == 0 && frames.Count == 100)
            {
                result.Status = TestStatus.Passed;
            }
            else
            {
                result.Status = TestStatus.Failed;
                result.FailureMessages.Add($"Frames received: {frames.Count}/100, Bit errors: {totalErrors}");
            }

            _pipeline.SetPacketReorderRate(0.0);
        }
        catch (Exception ex)
        {
            result.Status = TestStatus.Failed;
            result.FailureMessages.Add(ex.Message);
        }

        return result;
    }

    private TestResult ExecuteIT04(DetectorConfig config)
    {
        var result = new TestResult
        {
            Scenario = TestScenario.IT04_ErrorInjection,
            Status = TestStatus.Running
        };

        try
        {
            var subTestResults = new List<string>();
            int failedSubTests = 0;

            // Sub-test A: TIMEOUT Error (Fatal) - stops frame processing
            _pipeline.Initialize(config);
            _pipeline.InjectError("TIMEOUT");
            var framesA = _pipeline.ProcessFrames(10);
            if (framesA.Count < 10)
            {
                subTestResults.Add($"A (TIMEOUT): PASS - Fatal error detected ({framesA.Count}/10 frames)");
            }
            else
            {
                subTestResults.Add("A (TIMEOUT): FAIL - Error not detected");
                failedSubTests++;
            }

            // Sub-test B: OVERFLOW Error (Fatal) - stops frame processing
            _pipeline.Initialize(config);
            _pipeline.InjectError("OVERFLOW");
            var framesB = _pipeline.ProcessFrames(10);
            if (framesB.Count < 10)
            {
                subTestResults.Add($"B (OVERFLOW): PASS - Overflow error detected ({framesB.Count}/10 frames)");
            }
            else
            {
                subTestResults.Add("B (OVERFLOW): FAIL - Error not detected");
                failedSubTests++;
            }

            // Sub-test C: CRC Error (Non-Fatal) - processing continues
            _pipeline.Initialize(config);
            _pipeline.InjectError("CRC");
            var framesC = _pipeline.ProcessFrames(10);
            if (framesC.Count == 10)
            {
                subTestResults.Add("C (CRC): PASS - Non-fatal error, frames continue");
            }
            else
            {
                subTestResults.Add($"C (CRC): FAIL - CRC error should be non-fatal ({framesC.Count}/10 frames)");
                failedSubTests++;
            }

            // Sub-test G: Post-Recovery Normal Operation
            _pipeline.Initialize(config);
            _pipeline.InjectError("RECOVERABLE");
            var framesG = _pipeline.ProcessFrames(100);
            if (framesG.Count == 100)
            {
                subTestResults.Add("G (Post-Recovery): PASS - 100 frames after recovery");
            }
            else
            {
                subTestResults.Add($"G (Post-Recovery): FAIL - Only {framesG.Count}/100 frames");
                failedSubTests++;
            }

            result.AdditionalMetrics["SubTestResults"] = subTestResults;

            if (failedSubTests == 0)
            {
                result.Status = TestStatus.Passed;
                result.FramesProcessed = framesG.Count;
            }
            else
            {
                result.Status = TestStatus.Failed;
                result.FailureMessages.AddRange(subTestResults.FindAll(r => r.Contains("FAIL")));
            }
        }
        catch (Exception ex)
        {
            result.Status = TestStatus.Failed;
            result.FailureMessages.Add(ex.Message);
        }

        return result;
    }

    private TestResult ExecuteIT05(DetectorConfig config)
    {
        var result = new TestResult
        {
            Scenario = TestScenario.IT05_ConfigurationChange,
            Status = TestStatus.Running
        };

        try
        {
            var minConfig = new DetectorConfig
            {
                Panel = new Models.PanelConfig { Rows = 1024, Cols = 1024, BitDepth = 14, PixelPitchUm = 100.0 },
                Fpga = config.Fpga,
                Soc = config.Soc,
                Host = config.Host,
                Simulation = config.Simulation
            };
            _pipeline.Initialize(minConfig);
            var framesBefore = _pipeline.ProcessFrames(10);

            var intAConfig = new DetectorConfig
            {
                Panel = new Models.PanelConfig { Rows = 2048, Cols = 2048, BitDepth = 16, PixelPitchUm = 100.0 },
                Fpga = config.Fpga,
                Soc = config.Soc,
                Host = config.Host,
                Simulation = config.Simulation
            };
            _pipeline.Reconfigure(intAConfig);
            var framesAfter = _pipeline.ProcessFrames(10);

            result.FramesProcessed = framesBefore.Count + framesAfter.Count;

            bool beforeCorrect = framesBefore.Count == 10 && framesBefore[0].Width == 1024;
            bool afterCorrect = framesAfter.Count == 10 && framesAfter[0].Width == 2048;

            if (beforeCorrect && afterCorrect)
            {
                result.Status = TestStatus.Passed;
            }
            else
            {
                result.Status = TestStatus.Failed;
                result.FailureMessages.Add($"Configuration change failed: Before={beforeCorrect}, After={afterCorrect}");
            }
        }
        catch (Exception ex)
        {
            result.Status = TestStatus.Failed;
            result.FailureMessages.Add(ex.Message);
        }

        return result;
    }

    private TestResult ExecuteIT06(DetectorConfig config)
    {
        var result = new TestResult
        {
            Scenario = TestScenario.IT06_ModeTransition,
            Status = TestStatus.Running
        };

        try
        {
            var subTestResults = new List<string>();
            int failedSubTests = 0;

            // Sub-test A: Continuous to Single-Shot
            _pipeline.Initialize(config);
            _pipeline.SetScanMode(ScanMode.Continuous);
            var framesCont = _pipeline.ProcessFrames(50);
            _pipeline.SetScanMode(ScanMode.Single);
            var framesSingle = _pipeline.ProcessFrames(10);

            if (framesCont.Count == 50 && framesSingle.Count == 10)
            {
                subTestResults.Add("A (Cont->Single): PASS");
            }
            else
            {
                subTestResults.Add($"A (Cont->Single): FAIL - {framesCont.Count}+{framesSingle.Count} frames");
                failedSubTests++;
            }

            // Sub-test B: Calibration Mode
            _pipeline.Initialize(config);
            _pipeline.SetScanMode(ScanMode.Calibration);
            var framesCal = _pipeline.ProcessFrames(1);

            if (framesCal.Count == 1)
            {
                subTestResults.Add("B (Calibration): PASS");
            }
            else
            {
                subTestResults.Add($"B (Calibration): FAIL - {framesCal.Count} frames");
                failedSubTests++;
            }

            // Sub-test C: Mode Transition Sequence
            _pipeline.Initialize(config);
            _pipeline.SetScanMode(ScanMode.Single);
            var framesS1 = _pipeline.ProcessFrames(5);
            _pipeline.SetScanMode(ScanMode.Continuous);
            var framesC = _pipeline.ProcessFrames(5);
            _pipeline.SetScanMode(ScanMode.Calibration);
            var framesCal2 = _pipeline.ProcessFrames(5);
            _pipeline.SetScanMode(ScanMode.Single);
            var framesS2 = _pipeline.ProcessFrames(5);

            bool sequenceCorrect = framesS1.Count == 5 && framesC.Count == 5 &&
                                  framesCal2.Count == 5 && framesS2.Count == 5;

            if (sequenceCorrect)
            {
                subTestResults.Add("C (Sequence): PASS");
            }
            else
            {
                subTestResults.Add($"C (Sequence): FAIL");
                failedSubTests++;
            }

            result.AdditionalMetrics["SubTestResults"] = subTestResults;
            result.FramesProcessed = framesS1.Count + framesC.Count + framesCal2.Count + framesS2.Count;

            if (failedSubTests == 0)
            {
                result.Status = TestStatus.Passed;
            }
            else
            {
                result.Status = TestStatus.Failed;
                result.FailureMessages.AddRange(subTestResults.FindAll(r => r.Contains("FAIL")));
            }
        }
        catch (Exception ex)
        {
            result.Status = TestStatus.Failed;
            result.FailureMessages.Add(ex.Message);
        }

        return result;
    }

    private TestResult ExecuteIT07(DetectorConfig config)
    {
        var result = new TestResult
        {
            Scenario = TestScenario.IT07_PacketLoss,
            Status = TestStatus.Running
        };

        try
        {
            var subTestResults = new List<string>();
            int failedSubTests = 0;

            int targetFrames = GetFrameCount(1000, config);

            // Sub-test A: 5% Random Packet Loss
            _pipeline.Initialize(config);
            _pipeline.SetPacketLossRate(0.05);
            var framesLoss = _pipeline.ProcessFrames(targetFrames);

            // With 5% packet loss, some frames will fail to reassemble - allow tolerance
            int lossThreshold = (int)(targetFrames * 0.9);
            if (framesLoss.Count >= lossThreshold)
            {
                subTestResults.Add($"A (Packet Loss): PASS - {framesLoss.Count}/{targetFrames} frames received");
            }
            else
            {
                subTestResults.Add($"A (Packet Loss): FAIL - Only {framesLoss.Count}/{targetFrames} frames");
                failedSubTests++;
            }

            // Sub-test B: No Packet Loss
            _pipeline.Initialize(config);
            _pipeline.SetPacketLossRate(0.0);
            var framesLatency = _pipeline.ProcessFrames(targetFrames);

            if (framesLatency.Count == targetFrames)
            {
                subTestResults.Add("B (No Loss): PASS");
            }
            else
            {
                subTestResults.Add($"B (No Loss): FAIL - {framesLatency.Count}/{targetFrames} frames");
                failedSubTests++;
            }

            result.AdditionalMetrics["SubTestResults"] = subTestResults;
            result.FramesProcessed = framesLoss.Count + framesLatency.Count;
            result.FrameDrops = (targetFrames * 2) - (framesLoss.Count + framesLatency.Count);

            if (failedSubTests == 0)
            {
                result.Status = TestStatus.Passed;
            }
            else
            {
                result.Status = TestStatus.Failed;
                result.FailureMessages.AddRange(subTestResults.FindAll(r => r.Contains("FAIL")));
            }
        }
        catch (Exception ex)
        {
            result.Status = TestStatus.Failed;
            result.FailureMessages.Add(ex.Message);
        }

        return result;
    }

    private TestResult ExecuteIT08(DetectorConfig config)
    {
        var result = new TestResult
        {
            Scenario = TestScenario.IT08_SimultaneousConnections,
            Status = TestStatus.Running
        };

        try
        {
            _pipeline.Initialize(config);
            var frames1 = _pipeline.ProcessFrames(10);
            var frames2 = _pipeline.ProcessFrames(10);

            result.FramesProcessed = frames1.Count + frames2.Count;

            if (frames1.Count == 10 && frames2.Count == 10)
            {
                result.Status = TestStatus.Passed;
            }
            else
            {
                result.Status = TestStatus.Failed;
                result.FailureMessages.Add($"Connection handling failed: {frames1.Count}+{frames2.Count} frames");
            }
        }
        catch (Exception ex)
        {
            result.Status = TestStatus.Failed;
            result.FailureMessages.Add(ex.Message);
        }

        return result;
    }

    private TestResult ExecuteIT09(DetectorConfig config)
    {
        var result = new TestResult
        {
            Scenario = TestScenario.IT09_LongDurationStability,
            Status = TestStatus.Running
        };

        try
        {
            int targetFrames = GetFrameCount(10000, config);
            var frames = _pipeline.ProcessFrames(targetFrames);
            result.FramesProcessed = frames.Count;

            if (frames.Count == targetFrames)
            {
                int totalErrors = 0;
                int checkInterval = Math.Max(1, targetFrames / 10);
                for (int i = 0; i < targetFrames; i += checkInterval)
                {
                    totalErrors += VerifyCounterPattern(frames[i], 16);
                }

                result.BitErrors = totalErrors;
                result.FrameDrops = 0;

                if (result.BitErrors == 0)
                {
                    result.Status = TestStatus.Passed;

                    double frameSizeBits = 2048.0 * 2048.0 * 16.0;
                    double totalTimeSec = _stopwatch.ElapsedMilliseconds / 1000.0;
                    result.ThroughputGbps = (frameSizeBits * targetFrames) / totalTimeSec / 1e9;
                }
                else
                {
                    result.Status = TestStatus.Failed;
                    result.FailureMessages.Add($"Bit errors in spot-check: {totalErrors}");
                }
            }
            else
            {
                result.Status = TestStatus.Failed;
                result.FailureMessages.Add($"Expected {targetFrames} frames, got {frames.Count}");
                result.FrameDrops = targetFrames - frames.Count;
            }
        }
        catch (Exception ex)
        {
            result.Status = TestStatus.Failed;
            result.FailureMessages.Add(ex.Message);
        }

        return result;
    }

    private TestResult ExecuteIT10(DetectorConfig config)
    {
        var result = new TestResult
        {
            Scenario = TestScenario.IT10_BandwidthLimits,
            Status = TestStatus.Running
        };

        try
        {
            var tiers = new[]
            {
                (PerformanceTier.Minimum, 1024, 1024, 14, 0.22),
                (PerformanceTier.IntermediateA, 2048, 2048, 16, 1.01),
                (PerformanceTier.IntermediateB, 2048, 2048, 16, 2.01),
                (PerformanceTier.Target, 3072, 3072, 16, 2.26)
            };

            int totalErrors = 0;
            var tierResults = new List<string>();

            foreach (var (tier, rows, cols, bitDepth, minThroughput) in tiers)
            {
                if (tier == PerformanceTier.Target)
                {
                    result.Warnings.Add("IT-10C (Target tier) skipped - conditional on 800M debugging");
                    tierResults.Add("IT-10C (Target): SKIPPED");
                    continue;
                }

                var tierConfig = new DetectorConfig
                {
                    Panel = new Models.PanelConfig
                    {
                        Rows = rows,
                        Cols = cols,
                        BitDepth = bitDepth,
                        PixelPitchUm = config.Panel?.PixelPitchUm ?? 100.0
                    },
                    Fpga = config.Fpga,
                    Soc = config.Soc,
                    Host = config.Host,
                    Simulation = config.Simulation
                };

                int tierFrameCount = GetFrameCount(100, config);
                _pipeline.Initialize(tierConfig);
                var frames = _pipeline.ProcessFrames(tierFrameCount);

                double frameSizeBits = (double)(rows * cols * 16);
                double totalTimeSec = _stopwatch.ElapsedMilliseconds / 1000.0;
                double throughputGbps = (frameSizeBits * tierFrameCount) / totalTimeSec / 1e9;

                bool passed = throughputGbps >= minThroughput * 0.95;
                tierResults.Add($"{tier}: {throughputGbps:F3} Gbps (target: {minThroughput:F3} Gbps) - {(passed ? "PASS" : "FAIL")}");

                if (!passed)
                {
                    totalErrors++;
                }
            }

            result.BitErrors = totalErrors;
            result.AdditionalMetrics["TierResults"] = tierResults;

            if (totalErrors == 0)
            {
                result.Status = TestStatus.Passed;
            }
            else
            {
                result.Status = TestStatus.Failed;
                result.FailureMessages.AddRange(tierResults.FindAll(r => r.Contains("FAIL")));
            }
        }
        catch (Exception ex)
        {
            result.Status = TestStatus.Failed;
            result.FailureMessages.Add(ex.Message);
        }

        return result;
    }

    private DetectorConfig GetConfigForScenario(TestScenario scenario, DetectorConfig baseConfig)
    {
        return scenario switch
        {
            TestScenario.IT01_SingleFrameMinimum => new DetectorConfig
            {
                Panel = new Models.PanelConfig { Rows = 1024, Cols = 1024, BitDepth = 14, PixelPitchUm = baseConfig.Panel?.PixelPitchUm ?? 100.0 },
                Fpga = baseConfig.Fpga,
                Soc = baseConfig.Soc,
                Host = baseConfig.Host,
                Simulation = baseConfig.Simulation
            },
            TestScenario.IT02_1000FrameContinuous or TestScenario.IT09_LongDurationStability => new DetectorConfig
            {
                Panel = new Models.PanelConfig { Rows = 2048, Cols = 2048, BitDepth = 16, PixelPitchUm = baseConfig.Panel?.PixelPitchUm ?? 100.0 },
                Fpga = baseConfig.Fpga,
                Soc = baseConfig.Soc,
                Host = baseConfig.Host,
                Simulation = baseConfig.Simulation
            },
            _ => baseConfig
        };
    }

    private static int VerifyCounterPattern(FrameData frame, int bitDepth)
    {
        int errors = 0;
        int width = frame.Width;
        int height = frame.Height;
        int mask = (1 << bitDepth) - 1;

        for (int r = 0; r < height; r++)
        {
            for (int c = 0; c < width; c++)
            {
                int expected = (r * width + c) & mask;
                int actual = frame.Pixels[r * width + c];
                if (expected != actual)
                {
                    errors++;
                    if (errors > 100)
                    {
                        return errors;
                    }
                }
            }
        }

        return errors;
    }
}
