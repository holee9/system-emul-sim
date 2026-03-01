using FpgaSimulator.Core.Csi2;
using FpgaSimulator.Core.Fsm;
using McuSimulator.Core;
using McuSimulator.Core.Buffer;
using McuSimulator.Core.Command;
using McuSimulator.Core.Health;
using McuSimulator.Core.Network;
using McuSimulator.Core.Sequence;
using Xunit;

namespace McuSimulator.Tests;

/// <summary>
/// Tests for McuTopSimulator top-level orchestration.
/// Follows TDD: RED-GREEN-REFACTOR cycle.
/// REQ-SIM-010: McuTopSimulator shall orchestrate all MCU sub-modules.
/// </summary>
public sealed class McuTopSimulatorTests
{
    private const string TestHmacKey = "test-key";

    #region Constructor Tests

    [Fact]
    public void Constructor_NullFrameBufferManager_ThrowsArgumentNullException()
    {
        // Arrange
        var udp = new UdpFrameTransmitter();
        var cmd = new CommandProtocol(TestHmacKey);
        var health = new HealthMonitor();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new McuTopSimulator(null!, udp, cmd, health));
    }

    [Fact]
    public void Constructor_NullUdpTransmitter_ThrowsArgumentNullException()
    {
        // Arrange
        var fbm = CreateFrameBufferManager();
        var cmd = new CommandProtocol(TestHmacKey);
        var health = new HealthMonitor();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new McuTopSimulator(fbm, null!, cmd, health));
    }

    [Fact]
    public void Constructor_NullCommandProtocol_ThrowsArgumentNullException()
    {
        // Arrange
        var fbm = CreateFrameBufferManager();
        var udp = new UdpFrameTransmitter();
        var health = new HealthMonitor();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new McuTopSimulator(fbm, udp, null!, health));
    }

    [Fact]
    public void Constructor_NullHealthMonitor_ThrowsArgumentNullException()
    {
        // Arrange
        var fbm = CreateFrameBufferManager();
        var udp = new UdpFrameTransmitter();
        var cmd = new CommandProtocol(TestHmacKey);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new McuTopSimulator(fbm, udp, cmd, null!));
    }

    [Fact]
    public void Constructor_NullSpiMaster_IsAllowed()
    {
        // Arrange & Act
        var sim = CreateSimulator();

        // Assert
        Assert.NotNull(sim);
        Assert.Null(sim.SpiMaster);
    }

    [Fact]
    public void Constructor_InitializesAllSubModules()
    {
        // Arrange & Act
        var sim = CreateSimulator();

        // Assert
        Assert.NotNull(sim.SequenceEngine);
        Assert.NotNull(sim.FrameReassembler);
        Assert.NotNull(sim.FrameBufferManager);
        Assert.NotNull(sim.UdpFrameTransmitter);
        Assert.NotNull(sim.CommandProtocol);
        Assert.NotNull(sim.HealthMonitor);
    }

    #endregion

    #region ProcessCommand - StartScan

    [Fact]
    public void ProcessCommand_StartScan_TransitionsToScanning()
    {
        // Arrange
        var sim = CreateSimulator();
        var msg = CreateSignedCommand(sequence: 1, CommandType.StartScan);

        // Act
        var (success, statusCode) = sim.ProcessCommand(msg);

        // Assert
        Assert.True(success);
        Assert.Equal(CommandProtocol.StatusOk, statusCode);
        // In standalone mode (SpiMaster=null), OnConfigure auto-fires ConfigDone,
        // which transitions to Arm, then OnArm auto-fires ArmDone -> Scanning
        Assert.Equal(SequenceState.Scanning, sim.SequenceEngine.State);
    }

    [Fact]
    public void ProcessCommand_StartScan_WithContinuousMode_TransitionsToScanning()
    {
        // Arrange
        var sim = CreateSimulator();
        var payload = new byte[] { (byte)ScanMode.Continuous };
        var msg = CreateSignedCommand(sequence: 1, CommandType.StartScan, payload);

        // Act
        var (success, _) = sim.ProcessCommand(msg);

        // Assert
        Assert.True(success);
        Assert.Equal(SequenceState.Scanning, sim.SequenceEngine.State);
        Assert.Equal(ScanMode.Continuous, sim.SequenceEngine.Mode);
    }

    #endregion

    #region ProcessCommand - StopScan

    [Fact]
    public void ProcessCommand_StopScan_ReturnsToIdle()
    {
        // Arrange
        var sim = CreateSimulator();
        // First start a scan
        var startMsg = CreateSignedCommand(sequence: 1, CommandType.StartScan);
        sim.ProcessCommand(startMsg);
        Assert.Equal(SequenceState.Scanning, sim.SequenceEngine.State);

        // Act
        var stopMsg = CreateSignedCommand(sequence: 2, CommandType.StopScan);
        var (success, statusCode) = sim.ProcessCommand(stopMsg);

        // Assert
        Assert.True(success);
        Assert.Equal(CommandProtocol.StatusOk, statusCode);
        Assert.Equal(SequenceState.Idle, sim.SequenceEngine.State);
    }

    #endregion

    #region ProcessCommand - GetStatus

    [Fact]
    public void ProcessCommand_GetStatus_ReturnsSuccess()
    {
        // Arrange
        var sim = CreateSimulator();
        var msg = CreateSignedCommand(sequence: 1, CommandType.GetStatus);

        // Act
        var (success, statusCode) = sim.ProcessCommand(msg);

        // Assert
        Assert.True(success);
        Assert.Equal(CommandProtocol.StatusOk, statusCode);
    }

    [Fact]
    public void GetStatus_ReturnsSystemStatusWithCorrectState()
    {
        // Arrange
        var sim = CreateSimulator();

        // Act
        var status = sim.GetStatus();

        // Assert
        Assert.NotNull(status);
        Assert.Equal((byte)SequenceState.Idle, status.State);
    }

    [Fact]
    public void GetStatus_AfterStartScan_ReflectsNewState()
    {
        // Arrange
        var sim = CreateSimulator();
        var msg = CreateSignedCommand(sequence: 1, CommandType.StartScan);
        sim.ProcessCommand(msg);

        // Act
        var status = sim.GetStatus();

        // Assert
        Assert.Equal((byte)SequenceState.Scanning, status.State);
    }

    #endregion

    #region ProcessCommand - Reset

    [Fact]
    public void ProcessCommand_Reset_ResetsAllSubModules()
    {
        // Arrange
        var sim = CreateSimulator();

        // Start a scan to change state
        var startMsg = CreateSignedCommand(sequence: 1, CommandType.StartScan);
        sim.ProcessCommand(startMsg);
        Assert.Equal(SequenceState.Scanning, sim.SequenceEngine.State);

        // Act
        var resetMsg = CreateSignedCommand(sequence: 2, CommandType.Reset);
        var (success, statusCode) = sim.ProcessCommand(resetMsg);

        // Assert
        Assert.True(success);
        Assert.Equal(CommandProtocol.StatusOk, statusCode);
        Assert.Equal(SequenceState.Idle, sim.SequenceEngine.State);
    }

    #endregion

    #region ProcessCommand - Invalid Auth

    [Fact]
    public void ProcessCommand_InvalidAuth_ReturnsFailure()
    {
        // Arrange
        var sim = CreateSimulator();
        var msg = new CommandMessage
        {
            Magic = 0xDEADBEEF, // Bad magic
            Sequence = 1,
            CommandId = CommandType.GetStatus,
            PayloadLength = 0,
            Hmac = new byte[CommandMessage.HmacSize],
            Payload = Array.Empty<byte>()
        };

        // Act
        var (success, statusCode) = sim.ProcessCommand(msg);

        // Assert
        Assert.False(success);
        Assert.Equal(CommandProtocol.StatusInvalidCmd, statusCode);
    }

    [Fact]
    public void ProcessCommand_BadHmac_ReturnsAuthFailed()
    {
        // Arrange
        var sim = CreateSimulator();
        var msg = new CommandMessage
        {
            Magic = CommandMessage.MagicCommand,
            Sequence = 1,
            CommandId = CommandType.GetStatus,
            PayloadLength = 0,
            Hmac = new byte[CommandMessage.HmacSize], // Wrong HMAC
            Payload = Array.Empty<byte>()
        };

        // Act
        var (success, statusCode) = sim.ProcessCommand(msg);

        // Assert
        Assert.False(success);
        Assert.Equal(CommandProtocol.StatusAuthFailed, statusCode);
    }

    #endregion

    #region ProcessFrame - Complete Frame

    [Fact]
    public void ProcessFrame_CompleteFrame_ReturnsUdpPackets()
    {
        // Arrange
        var sim = CreateSimulator();

        // Start scan first (required for SequenceEngine to be in Scanning state)
        var startMsg = CreateSignedCommand(sequence: 1, CommandType.StartScan,
            new byte[] { (byte)ScanMode.Continuous });
        sim.ProcessCommand(startMsg);
        Assert.Equal(SequenceState.Scanning, sim.SequenceEngine.State);

        // Create a small test frame via CSI-2 packets
        var generator = new Csi2TxPacketGenerator();
        var frame = new ushort[,] { { 0x0100, 0x0200 }, { 0x0300, 0x0400 } };
        var packets = generator.GenerateFullFrame(frame);

        // Act
        var udpPackets = sim.ProcessFrame(packets);

        // Assert
        Assert.NotEmpty(udpPackets);
        Assert.True(udpPackets.Count > 0);
    }

    [Fact]
    public void ProcessFrame_CompleteFrame_UpdatesHealthStats()
    {
        // Arrange
        var sim = CreateSimulator();
        var startMsg = CreateSignedCommand(sequence: 1, CommandType.StartScan,
            new byte[] { (byte)ScanMode.Continuous });
        sim.ProcessCommand(startMsg);

        var generator = new Csi2TxPacketGenerator();
        var frame = new ushort[,] { { 0x0100, 0x0200 }, { 0x0300, 0x0400 } };
        var packets = generator.GenerateFullFrame(frame);

        // Act
        sim.ProcessFrame(packets);

        // Assert
        var stats = sim.HealthMonitor.GetStats();
        Assert.Equal(1, stats.FramesSent);
    }

    #endregion

    #region ProcessFrame - Incomplete Frame

    [Fact]
    public void ProcessFrame_IncompleteFrame_ReturnsEmptyList()
    {
        // Arrange
        var sim = CreateSimulator();
        var startMsg = CreateSignedCommand(sequence: 1, CommandType.StartScan);
        sim.ProcessCommand(startMsg);

        // Create incomplete frame (FS only, no lines, no FE)
        var generator = new Csi2TxPacketGenerator();
        var fsPacket = generator.GenerateFrameStart();

        // Act
        var udpPackets = sim.ProcessFrame(new[] { fsPacket });

        // Assert
        Assert.Empty(udpPackets);
    }

    [Fact]
    public void ProcessFrame_MissingFrameEnd_ReturnsEmptyList()
    {
        // Arrange
        var sim = CreateSimulator();
        var startMsg = CreateSignedCommand(sequence: 1, CommandType.StartScan);
        sim.ProcessCommand(startMsg);

        var generator = new Csi2TxPacketGenerator();
        var fsPacket = generator.GenerateFrameStart();
        var linePacket = generator.GenerateLineData(new ushort[] { 0x0100 }, lineNumber: 0);

        // Act - no FrameEnd packet
        var udpPackets = sim.ProcessFrame(new[] { fsPacket, linePacket });

        // Assert
        Assert.Empty(udpPackets);
    }

    #endregion

    #region ISequenceCallback - Standalone Mode

    [Fact]
    public void StandaloneMode_OnConfigure_AutoFiresConfigDone()
    {
        // Arrange
        var sim = CreateSimulator(); // SpiMaster = null -> standalone

        // Act - StartScan triggers OnConfigure which auto-fires ConfigDone
        sim.SequenceEngine.StartScan(ScanMode.Single);

        // Assert - should have advanced past Configure (to Arm, then auto ArmDone -> Scanning)
        Assert.NotEqual(SequenceState.Configure, sim.SequenceEngine.State);
    }

    [Fact]
    public void StandaloneMode_OnArm_AutoFiresArmDone()
    {
        // Arrange
        var sim = CreateSimulator(); // SpiMaster = null -> standalone

        // Act
        sim.SequenceEngine.StartScan(ScanMode.Single);

        // Assert - should be in Scanning (past both Configure and Arm)
        Assert.Equal(SequenceState.Scanning, sim.SequenceEngine.State);
    }

    [Fact]
    public void StandaloneMode_FullScanLifecycle_WorksWithoutSpi()
    {
        // Arrange
        var sim = CreateSimulator();

        // Act - Start scan (auto-advances to Scanning)
        sim.SequenceEngine.StartScan(ScanMode.Continuous);
        Assert.Equal(SequenceState.Scanning, sim.SequenceEngine.State);

        // Stop scan
        sim.SequenceEngine.StopScan();

        // Assert
        Assert.Equal(SequenceState.Idle, sim.SequenceEngine.State);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Creates a McuTopSimulator with small buffers for testing.
    /// Uses a 4x4 frame size to minimize memory for test speed.
    /// </summary>
    private static McuTopSimulator CreateSimulator()
    {
        var config = new FrameManagerConfig
        {
            Rows = 4,
            Cols = 4,
            BitDepth = 16,
            NumBuffers = 4
        };
        var fbm = new FrameBufferManager(config);
        var udp = new UdpFrameTransmitter();
        var cmd = new CommandProtocol(TestHmacKey);
        var health = new HealthMonitor();

        return new McuTopSimulator(fbm, udp, cmd, health, spiMaster: null);
    }

    private static FrameBufferManager CreateFrameBufferManager()
    {
        var config = new FrameManagerConfig
        {
            Rows = 4,
            Cols = 4,
            BitDepth = 16,
            NumBuffers = 4
        };
        return new FrameBufferManager(config);
    }

    private static CommandMessage CreateSignedCommand(
        uint sequence,
        CommandType cmdId,
        byte[]? payload = null)
    {
        payload ??= Array.Empty<byte>();
        var msg = new CommandMessage
        {
            Magic = CommandMessage.MagicCommand,
            Sequence = sequence,
            CommandId = cmdId,
            PayloadLength = (ushort)payload.Length,
            Hmac = Array.Empty<byte>(),
            Payload = payload
        };

        var key = System.Text.Encoding.UTF8.GetBytes(TestHmacKey);
        var hmac = CommandProtocol.ComputeHmac(msg, key);
        return msg with { Hmac = hmac };
    }

    #endregion
}
