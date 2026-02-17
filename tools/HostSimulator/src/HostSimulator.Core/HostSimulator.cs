using Common.Dto.Dtos;
using Common.Dto.Interfaces;
using HostSimulator.Core.Configuration;
using HostSimulator.Core.Reassembly;
using HostSimulator.Core.Storage;

namespace HostSimulator.Core;

/// <summary>
/// Simulates the Host PC SDK functionality.
/// REQ-SIM-040: Receive UDP packets and reassemble complete frames.
/// REQ-SIM-041: Correctly reassemble frame using packet_index when packets arrive out of order.
/// REQ-SIM-042: Mark frame as incomplete and report missing packets after timeout.
/// REQ-SIM-043: Save frames in TIFF format (16-bit grayscale) and RAW format.
/// REQ-SIM-044: Support multi-threaded packet reception.
/// </summary>
public sealed class HostSimulator : ISimulator
{
    private FrameReassembler? _reassembler;
    private HostConfig? _config;
    private int _framesReceived;
    private int _framesCompleted;
    private int _framesIncomplete;

    /// <summary>
    /// Initializes the simulator with the specified configuration.
    /// </summary>
    /// <param name="config">Configuration object for the simulator.</param>
    public void Initialize(object config)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        _config = config as HostConfig ?? throw new ArgumentException($"Config must be of type {nameof(HostConfig)}", nameof(config));

        var timeout = TimeSpan.FromMilliseconds(_config.PacketTimeoutMs);
        _reassembler = new FrameReassembler(timeout);

        Reset();
    }

    /// <summary>
    /// Processes the input data through the simulator.
    /// For HostSimulator, input can be FrameData (direct) or byte[] (UDP packet).
    /// </summary>
    /// <param name="input">Input data to process.</param>
    /// <returns>Processed output data (FrameData or null).</returns>
    public object Process(object input)
    {
        if (_reassembler == null)
            throw new InvalidOperationException("Simulator not initialized. Call Initialize() first.");

        return input switch
        {
            FrameData frame => ProcessFrameData(frame)!,
            byte[] bytes => ProcessUdpPacket(bytes)!,
            _ => throw new ArgumentException($"Unsupported input type: {input.GetType().Name}")
        };
    }

    /// <summary>
    /// Resets the simulator to its initial state.
    /// </summary>
    public void Reset()
    {
        _reassembler?.Reset();
        _framesReceived = 0;
        _framesCompleted = 0;
        _framesIncomplete = 0;
    }

    /// <summary>
    /// Gets the current status of the simulator.
    /// </summary>
    /// <returns>Status description string.</returns>
    public string GetStatus()
    {
        int pendingFrames = _reassembler?.GetPendingFrameCount() ?? 0;
        return $"HostSimulator: Received={_framesReceived}, Completed={_framesCompleted}, Incomplete={_framesIncomplete}, Pending={pendingFrames}";
    }

    /// <summary>
    /// Processes a frame directly (for testing without UDP).
    /// </summary>
    private FrameData ProcessFrameData(FrameData frame)
    {
        _framesReceived++;
        _framesCompleted++;
        return frame;
    }

    /// <summary>
    /// Processes a UDP packet.
    /// </summary>
    private FrameData? ProcessUdpPacket(byte[] packet)
    {
        if (_reassembler == null)
            return null;

        // Parse frame header (first 32 bytes)
        if (!FrameHeader.TryParse(packet, out var header) || header == null)
        {
            // Invalid header, ignore packet
            return null;
        }

        // Extract payload (after 32-byte header)
        var payload = new byte[packet.Length - FrameHeader.HEADER_SIZE];
        Array.Copy(packet, FrameHeader.HEADER_SIZE, payload, 0, payload.Length);

        // Process packet
        var result = _reassembler.ProcessPacket(header, payload);

        if (result == null)
            return null; // Duplicate packet

        _framesReceived++;

        if (result.Status == FrameReassemblyStatus.Complete)
        {
            _framesCompleted++;
            return result.Frame;
        }
        else if (result.Status == FrameReassemblyStatus.Incomplete)
        {
            _framesIncomplete++;
        }

        return null;
    }

    /// <summary>
    /// Gets the number of frames received.
    /// </summary>
    public int FramesReceived => _framesReceived;

    /// <summary>
    /// Gets the number of frames completed.
    /// </summary>
    public int FramesCompleted => _framesCompleted;

    /// <summary>
    /// Gets the number of frames incomplete (timed out).
    /// </summary>
    public int FramesIncomplete => _framesIncomplete;
}
