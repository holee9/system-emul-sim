using System.Collections;
using Common.Dto.Dtos;
using McuSimulator.Core.Csi2;
using McuSimulator.Core.Frame;
using McuSimulator.Core.Network;
using HostSimulator.Core.Configuration;
using PanelSimulator.Models;
using Csi2Packet = FpgaSimulator.Core.Csi2.Csi2Packet;
using Csi2TxPacketGenerator = FpgaSimulator.Core.Csi2.Csi2TxPacketGenerator;

namespace IntegrationTests.Helpers;

/// <summary>
/// Performance tier configuration for simulator pipeline.
/// </summary>
public enum PerformanceTier
{
    /// <summary>Minimum performance configuration.</summary>
    Minimum,

    /// <summary>Target performance configuration.</summary>
    Target,

    /// <summary>Maximum performance configuration.</summary>
    Maximum
}

/// <summary>
/// Result of a full 4-layer pipeline execution with intermediate checkpoints.
/// </summary>
public sealed class PipelineCheckpointResult
{
    /// <summary>Whether the entire pipeline completed successfully.</summary>
    public required bool IsValid { get; init; }

    /// <summary>Panel output: 1D pixel array as FrameData.</summary>
    public required FrameData PanelOutput { get; init; }

    /// <summary>Panel output converted to 2D pixel array [rows, cols].</summary>
    public required ushort[,] PanelPixels2D { get; init; }

    /// <summary>FPGA output: CSI-2 packets (FS + LineData[] + FE).</summary>
    public required Csi2Packet[] FpgaCsi2Packets { get; init; }

    /// <summary>MCU reassembled frame from CSI-2 packets.</summary>
    public required ReassembledFrame McuReassembledFrame { get; init; }

    /// <summary>MCU UDP packets after fragmentation.</summary>
    public required List<UdpFramePacket> McuUdpPackets { get; init; }

    /// <summary>Host output: final reassembled FrameData.</summary>
    public required FrameData? HostOutput { get; init; }
}

/// <summary>
/// Builder for setting up Panel -> FPGA -> MCU -> Host simulator pipeline.
/// Configures performance tiers and manages actual 4-layer pipeline execution.
/// </summary>
public class SimulatorPipelineBuilder
{
    private PerformanceTier _tier = PerformanceTier.Target;
    private bool _isRunning = false;
    private readonly Dictionary<PerformanceTier, PipelineConfiguration> _configurations;
    private NetworkChannelConfig? _networkChannelConfig;
    private bool _enableCheckpoints = false;
    private HostSimulator.Core.Configuration.HostConfig? _hostConfig;

    /// <summary>
    /// Gets the current performance tier.
    /// </summary>
    public PerformanceTier CurrentTier => _tier;

    /// <summary>
    /// Gets whether the pipeline is currently running.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Event raised when pipeline state changes.
    /// </summary>
    public event EventHandler<PipelineStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Creates a new SimulatorPipelineBuilder with default configurations.
    /// </summary>
    public SimulatorPipelineBuilder()
    {
        _configurations = new Dictionary<PerformanceTier, PipelineConfiguration>
        {
            [PerformanceTier.Minimum] = new PipelineConfiguration(
                frameRate: 1,
                bufferSize: 64,
                parallelism: 1,
                "Minimum"
            ),
            [PerformanceTier.Target] = new PipelineConfiguration(
                frameRate: 30,
                bufferSize: 1024,
                parallelism: 4,
                "Target"
            ),
            [PerformanceTier.Maximum] = new PipelineConfiguration(
                frameRate: 60,
                bufferSize: 4096,
                parallelism: 8,
                "Maximum"
            )
        };
    }

    /// <summary>
    /// Builds and returns the pipeline configuration.
    /// </summary>
    public PipelineConfiguration BuildPipeline()
    {
        return _configurations[_tier];
    }

    /// <summary>
    /// Configures the pipeline for the specified performance tier.
    /// </summary>
    public SimulatorPipelineBuilder ConfigureForTier(PerformanceTier tier)
    {
        if (_isRunning)
            throw new InvalidOperationException("Cannot change tier while pipeline is running. Stop the pipeline first.");

        _tier = tier;
        OnStateChanged(new PipelineStateChangedEventArgs(tier, _configurations[tier], false));
        return this;
    }

    /// <summary>
    /// Starts the pipeline asynchronously.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
            throw new InvalidOperationException("Pipeline is already running.");

        _isRunning = true;
        OnStateChanged(new PipelineStateChangedEventArgs(_tier, _configurations[_tier], true));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the pipeline asynchronously.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
            throw new InvalidOperationException("Pipeline is not running.");

        _isRunning = false;
        OnStateChanged(new PipelineStateChangedEventArgs(_tier, _configurations[_tier], false));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the configuration for a specific tier.
    /// </summary>
    public PipelineConfiguration GetConfiguration(PerformanceTier tier)
    {
        return _configurations[tier];
    }

    /// <summary>
    /// Creates a custom configuration for a tier.
    /// </summary>
    public void SetCustomConfiguration(PerformanceTier tier, PipelineConfiguration configuration)
    {
        if (_isRunning)
            throw new InvalidOperationException("Cannot modify configuration while pipeline is running.");

        _configurations[tier] = configuration;
    }

    /// <summary>
    /// Configures the network channel with impairment settings.
    /// </summary>
    /// <param name="config">Network channel configuration.</param>
    /// <returns>This builder instance for chaining.</returns>
    public SimulatorPipelineBuilder WithNetworkChannel(NetworkChannelConfig config)
    {
        _networkChannelConfig = config ?? throw new ArgumentNullException(nameof(config));
        return this;
    }

    /// <summary>
    /// Enables or disables per-layer checkpoint capture.
    /// </summary>
    /// <param name="enabled">Whether to enable checkpoints.</param>
    /// <returns>This builder instance for chaining.</returns>
    public SimulatorPipelineBuilder WithCheckpoints(bool enabled)
    {
        _enableCheckpoints = enabled;
        return this;
    }

    /// <summary>
    /// Configures the host simulator settings.
    /// </summary>
    /// <param name="hostConfig">Host configuration.</param>
    /// <returns>This builder instance for chaining.</returns>
    public SimulatorPipelineBuilder WithHostConfig(HostSimulator.Core.Configuration.HostConfig hostConfig)
    {
        _hostConfig = hostConfig ?? throw new ArgumentNullException(nameof(hostConfig));
        return this;
    }

    /// <summary>
    /// Builds a SimulatorPipeline with the specified panel config and all builder settings.
    /// </summary>
    /// <param name="panelConfig">Panel simulator configuration.</param>
    /// <returns>Configured SimulatorPipeline instance.</returns>
    public SimulatorPipeline Build(PanelSimulator.Models.PanelConfig panelConfig)
    {
        ArgumentNullException.ThrowIfNull(panelConfig);

        var networkChannel = _networkChannelConfig != null
            ? new NetworkChannel(_networkChannelConfig)
            : null;

        return new SimulatorPipeline(
            panelConfig: panelConfig,
            networkChannel: networkChannel,
            hostConfig: _hostConfig,
            enableCheckpoints: _enableCheckpoints);
    }

    /// <summary>
    /// Executes the full 4-layer pipeline: Panel -> FPGA -> MCU -> Host.
    /// Returns the final FrameData from the Host layer.
    /// </summary>
    /// <param name="panelConfig">Panel simulator configuration.</param>
    /// <returns>Final FrameData after full pipeline processing.</returns>
    public FrameData ProcessFrame(PanelConfig panelConfig)
    {
        var result = ProcessFrameWithCheckpoints(panelConfig);
        return result.HostOutput ?? throw new InvalidOperationException("Pipeline failed to produce output.");
    }

    /// <summary>
    /// Executes the full 4-layer pipeline with intermediate checkpoints.
    /// Returns detailed results at each layer boundary for data integrity verification.
    /// </summary>
    /// <param name="panelConfig">Panel simulator configuration.</param>
    /// <returns>Checkpoint result with intermediate data at each layer.</returns>
    public PipelineCheckpointResult ProcessFrameWithCheckpoints(PanelConfig panelConfig)
    {
        // Layer 1: Panel - Generate pixel data
        var panel = new PanelSimulator.PanelSimulator();
        panel.Initialize(panelConfig);
        var panelOutput = (FrameData)panel.Process(new object());

        // Convert 1D pixels to 2D array for FPGA input
        var pixels2D = ConvertTo2D(panelOutput.Pixels, panelOutput.Height, panelOutput.Width);

        // Layer 2: FPGA - Convert to CSI-2 packets
        var csi2Tx = new Csi2TxPacketGenerator();
        var csi2Packets = csi2Tx.GenerateFullFrame(pixels2D);

        // Layer 3: MCU - Reassemble CSI-2 packets into frame, then fragment to UDP
        var mcuReassembler = new FrameReassembler();
        foreach (var packet in csi2Packets)
        {
            mcuReassembler.AddPacket(packet);
        }
        var mcuFrame = mcuReassembler.GetFrame();

        // MCU UDP transmission
        var udpTransmitter = new UdpFrameTransmitter();
        var udpPackets = udpTransmitter.FragmentFrame(mcuFrame.Pixels, (uint)panelOutput.FrameNumber);

        // Layer 4: Host - Receive UDP packets and reassemble
        var hostSim = new HostSimulator.Core.HostSimulator();
        hostSim.Initialize(new HostConfig { PacketTimeoutMs = 5000 });

        FrameData? hostOutput = null;
        foreach (var udpPacket in udpPackets)
        {
            var result = hostSim.Process(udpPacket.Data);
            if (result is FrameData fd)
            {
                hostOutput = fd;
            }
        }

        return new PipelineCheckpointResult
        {
            IsValid = hostOutput != null,
            PanelOutput = panelOutput,
            PanelPixels2D = pixels2D,
            FpgaCsi2Packets = csi2Packets,
            McuReassembledFrame = mcuFrame,
            McuUdpPackets = udpPackets,
            HostOutput = hostOutput
        };
    }

    /// <summary>
    /// Converts a 1D pixel array to a 2D array [rows, cols].
    /// </summary>
    internal static ushort[,] ConvertTo2D(ushort[] pixels, int rows, int cols)
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

    /// <summary>
    /// Converts a 2D pixel array [rows, cols] to a 1D array.
    /// </summary>
    internal static ushort[] ConvertTo1D(ushort[,] pixels)
    {
        int rows = pixels.GetLength(0);
        int cols = pixels.GetLength(1);
        var result = new ushort[rows * cols];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                result[r * cols + c] = pixels[r, c];
            }
        }
        return result;
    }

    private void OnStateChanged(PipelineStateChangedEventArgs e)
    {
        StateChanged?.Invoke(this, e);
    }
}

/// <summary>
/// Pipeline configuration parameters.
/// </summary>
public sealed class PipelineConfiguration
{
    /// <summary>Target frame rate in fps.</summary>
    public int FrameRate { get; }

    /// <summary>Buffer size in frames.</summary>
    public int BufferSize { get; }

    /// <summary>Parallelism level.</summary>
    public int Parallelism { get; }

    /// <summary>Configuration name/description.</summary>
    public string Name { get; }

    public PipelineConfiguration(int frameRate, int bufferSize, int parallelism, string name)
    {
        FrameRate = frameRate;
        BufferSize = bufferSize;
        Parallelism = parallelism;
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public override string ToString()
    {
        return $"PipelineConfiguration: {Name}, {FrameRate} fps, Buffer={BufferSize}, Parallelism={Parallelism}";
    }
}

/// <summary>
/// Event arguments for pipeline state changes.
/// </summary>
public sealed class PipelineStateChangedEventArgs : EventArgs
{
    /// <summary>Performance tier.</summary>
    public PerformanceTier Tier { get; }

    /// <summary>Pipeline configuration.</summary>
    public PipelineConfiguration Configuration { get; }

    /// <summary>Whether pipeline is running.</summary>
    public bool IsRunning { get; }

    public PipelineStateChangedEventArgs(PerformanceTier tier, PipelineConfiguration configuration, bool isRunning)
    {
        Tier = tier;
        Configuration = configuration;
        IsRunning = isRunning;
    }
}
