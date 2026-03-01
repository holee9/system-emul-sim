using FpgaSimulator.Core.Csi2;
using FpgaSimulator.Core.Fsm;
using FpgaSimulator.Core.Spi;
using McuSimulator.Core.Buffer;
using McuSimulator.Core.Command;
using McuSimulator.Core.Frame;
using McuSimulator.Core.Health;
using McuSimulator.Core.Network;
using McuSimulator.Core.Sequence;
using McuSimulator.Core.Spi;

namespace McuSimulator.Core;

/// <summary>
/// Top-level MCU simulator orchestrating all MCU sub-modules.
/// Composes SequenceEngine, FrameReassembler, FrameBufferManager,
/// UdpFrameTransmitter, CommandProtocol, HealthMonitor, and optional SpiMaster.
/// Implements <see cref="ISequenceCallback"/> to bridge SequenceEngine to SPI.
/// </summary>
public sealed class McuTopSimulator : ISequenceCallback
{
    // SPI register bits for CONTROL register writes
    private const ushort ControlStartBit = 0x0001;
    private const ushort ControlStopBit = 0x0002;
    private const ushort ControlArmBit = 0x0004;

    private uint _frameCounter;

    /// <summary>Sequence engine managing scan lifecycle FSM.</summary>
    public SequenceEngine SequenceEngine { get; }

    /// <summary>Frame reassembler for CSI-2 packet to frame conversion.</summary>
    public FrameReassembler FrameReassembler { get; }

    /// <summary>Ring buffer manager for frame storage.</summary>
    public FrameBufferManager FrameBufferManager { get; }

    /// <summary>UDP transmitter for fragmenting frames into packets.</summary>
    public UdpFrameTransmitter UdpFrameTransmitter { get; }

    /// <summary>Command protocol handler for host command authentication.</summary>
    public CommandProtocol CommandProtocol { get; }

    /// <summary>Health monitor for watchdog, statistics, and logging.</summary>
    public HealthMonitor HealthMonitor { get; }

    /// <summary>Optional SPI master for FPGA register access. Null when running standalone.</summary>
    public SpiMasterSimulator? SpiMaster { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="McuTopSimulator"/> class.
    /// </summary>
    /// <param name="frameBufferManager">Frame buffer manager instance.</param>
    /// <param name="udpFrameTransmitter">UDP frame transmitter instance.</param>
    /// <param name="commandProtocol">Command protocol handler instance.</param>
    /// <param name="healthMonitor">Health monitor instance.</param>
    /// <param name="spiMaster">Optional SPI master (null for standalone testing).</param>
    public McuTopSimulator(
        FrameBufferManager frameBufferManager,
        UdpFrameTransmitter udpFrameTransmitter,
        CommandProtocol commandProtocol,
        HealthMonitor healthMonitor,
        SpiMasterSimulator? spiMaster = null)
    {
        ArgumentNullException.ThrowIfNull(frameBufferManager);
        ArgumentNullException.ThrowIfNull(udpFrameTransmitter);
        ArgumentNullException.ThrowIfNull(commandProtocol);
        ArgumentNullException.ThrowIfNull(healthMonitor);

        FrameBufferManager = frameBufferManager;
        UdpFrameTransmitter = udpFrameTransmitter;
        CommandProtocol = commandProtocol;
        HealthMonitor = healthMonitor;
        SpiMaster = spiMaster;

        // SequenceEngine uses this instance as the callback for SPI bridging
        SequenceEngine = new SequenceEngine(callback: this);
        FrameReassembler = new FrameReassembler();
    }

    /// <summary>
    /// Processes incoming CSI-2 packets through the full MCU pipeline:
    /// reassemble, buffer, fragment, and transmit.
    /// </summary>
    /// <param name="packets">Array of CSI-2 packets forming a frame.</param>
    /// <returns>List of UDP packets ready for transmission, or empty if frame incomplete.</returns>
    public List<UdpFramePacket> ProcessFrame(Csi2Packet[] packets)
    {
        // 1. Feed packets to reassembler
        FrameReassembler.Reset();
        foreach (var packet in packets)
        {
            FrameReassembler.AddPacket(packet);
        }

        // 2. Check if frame is complete
        if (!FrameReassembler.IsFrameComplete)
        {
            return new List<UdpFramePacket>();
        }

        var frame = FrameReassembler.GetFrame();
        if (!frame.IsValid)
        {
            return new List<UdpFramePacket>();
        }

        // 3. Signal FrameReady to SequenceEngine
        SequenceEngine.HandleEvent(SequenceEvent.FrameReady);

        // 4. Acquire buffer from ring, copy frame data, commit
        uint frameNumber = _frameCounter++;
        int result = FrameBufferManager.GetBuffer(frameNumber, out var buffer, out var size);
        if (result < 0)
        {
            HealthMonitor.Log(LogLevel.Error, "McuTop", "Failed to acquire frame buffer");
            return new List<UdpFramePacket>();
        }

        // Copy pixel data into buffer (row-major, 2 bytes per pixel LE)
        int rows = frame.Rows;
        int cols = frame.Cols;
        int byteIndex = 0;
        for (int row = 0; row < rows && byteIndex + 1 < size; row++)
        {
            for (int col = 0; col < cols && byteIndex + 1 < size; col++)
            {
                buffer[byteIndex++] = (byte)(frame.Pixels[row, col] & 0xFF);
                buffer[byteIndex++] = (byte)((frame.Pixels[row, col] >> 8) & 0xFF);
            }
        }

        FrameBufferManager.CommitBuffer(frameNumber);

        // 5. Signal Complete to SequenceEngine (transitions STREAMING -> next state)
        SequenceEngine.HandleEvent(SequenceEvent.Complete);

        // 6. Get ready buffer for transmission
        int getResult = FrameBufferManager.GetReadyBuffer(out var txBuffer, out var txSize, out var txFrameNumber);
        if (getResult < 0)
        {
            return new List<UdpFramePacket>();
        }

        // 7. Fragment via UdpFrameTransmitter
        var udpPackets = UdpFrameTransmitter.FragmentFrame(frame.Pixels, txFrameNumber);

        // 8. Release the buffer
        FrameBufferManager.ReleaseBuffer(txFrameNumber);

        // 9. Update health statistics
        HealthMonitor.UpdateStat("frames_sent", 1);

        return udpPackets;
    }

    /// <summary>
    /// Processes an incoming host command through authentication and dispatch.
    /// </summary>
    /// <param name="msg">The command message from the host.</param>
    /// <returns>
    /// A tuple of (success, statusCode) indicating the validation/dispatch result.
    /// </returns>
    public (bool Success, ushort StatusCode) ProcessCommand(CommandMessage msg)
    {
        var (success, statusCode) = CommandProtocol.ValidateAndDispatch(msg);

        if (!success)
        {
            if (statusCode == CommandProtocol.StatusAuthFailed ||
                statusCode == CommandProtocol.StatusInvalidCmd)
            {
                HealthMonitor.UpdateStat("auth_failures", 1);
            }

            return (false, statusCode);
        }

        // Dispatch validated command
        switch (msg.CommandId)
        {
            case CommandType.StartScan:
                // Extract scan mode from payload (first byte, default Single)
                var mode = ScanMode.Single;
                if (msg.Payload is { Length: > 0 })
                {
                    mode = (ScanMode)msg.Payload[0];
                }

                SequenceEngine.StartScan(mode);
                break;

            case CommandType.StopScan:
                SequenceEngine.StopScan();
                break;

            case CommandType.GetStatus:
                // Status is returned via HealthMonitor; caller reads it separately
                break;

            case CommandType.Reset:
                SequenceEngine.Reset();
                FrameReassembler.Reset();
                FrameBufferManager.Reset();
                HealthMonitor.Reset();
                _frameCounter = 0;
                break;

            case CommandType.SetConfig:
                // Configuration payload handling is delegated to caller
                break;
        }

        return (true, CommandProtocol.StatusOk);
    }

    /// <summary>
    /// Returns the current system status snapshot.
    /// </summary>
    /// <returns>System status from the health monitor.</returns>
    public SystemStatus GetStatus()
    {
        return HealthMonitor.GetStatus((byte)SequenceEngine.State);
    }

    #region ISequenceCallback Implementation

    /// <inheritdoc />
    void ISequenceCallback.OnConfigure(ScanMode mode)
    {
        if (SpiMaster is null)
        {
            // Standalone mode: auto-advance past CONFIGURE
            SequenceEngine.HandleEvent(SequenceEvent.ConfigDone);
            return;
        }

        // Write scan mode to CONTROL register with start bit
        ushort modeValue = (ushort)(((ushort)mode << 2) | ControlStartBit);
        SpiMaster.WriteRegister(SpiRegisterAddresses.CONTROL, modeValue);
    }

    /// <inheritdoc />
    void ISequenceCallback.OnArm()
    {
        if (SpiMaster is null)
        {
            // Standalone mode: auto-advance past ARM
            SequenceEngine.HandleEvent(SequenceEvent.ArmDone);
            return;
        }

        SpiMaster.WriteRegister(SpiRegisterAddresses.CONTROL, ControlArmBit);
    }

    /// <inheritdoc />
    void ISequenceCallback.OnStop()
    {
        SpiMaster?.WriteRegister(SpiRegisterAddresses.CONTROL, ControlStopBit);
    }

    /// <inheritdoc />
    void ISequenceCallback.OnError(SequenceState state, string reason)
    {
        HealthMonitor.Log(LogLevel.Error, "McuTop", $"SequenceEngine error in {state}: {reason}");
    }

    #endregion
}
