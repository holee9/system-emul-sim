using Xunit;
using FluentAssertions;
using IntegrationTests.Helpers;
using Common.Dto.Dtos;
using Common.Dto.Interfaces;
using PanelSimulator.Models;
using HostSimulator.Core.Configuration;
using McuSimulator.Core.Frame;
using McuSimulator.Core.Network;
using FpgaSimulator.Core.Csi2;

namespace IntegrationTests.Integration;

/// <summary>
/// IT-12: Module Isolation Tests.
/// Validates that each simulator module can be independently replaced/mocked
/// while the remaining pipeline continues to function correctly.
/// Reference: SPEC-INTEG-001
/// </summary>
public class IT12_ModuleIsolationTests
{
    [Fact]
    public void PanelSimulator_ImplementsISimulator_FullContract()
    {
        // Arrange
        ISimulator panel = new PanelSimulator.PanelSimulator();
        var config = new PanelConfig
        {
            Rows = 64, Cols = 64, BitDepth = 16,
            TestPattern = TestPattern.Counter,
            NoiseModel = NoiseModelType.None,
            NoiseStdDev = 0, DefectRate = 0, Seed = 42
        };

        // Act - Initialize
        panel.Initialize(config);
        var status = panel.GetStatus();

        // Assert - Status reports initialized state
        status.Should().Contain("Ready");
        status.Should().Contain("64x64");

        // Act - Process
        var output = panel.Process(new object());
        output.Should().BeOfType<FrameData>();
        var frame = (FrameData)output;
        frame.Width.Should().Be(64);
        frame.Height.Should().Be(64);

        // Act - Reset
        panel.Reset();
        var statusAfterReset = panel.GetStatus();
        statusAfterReset.Should().Contain("Frame Number: 0");
    }

    [Fact]
    public void HostSimulator_ImplementsISimulator_FullContract()
    {
        // Arrange
        ISimulator host = new HostSimulator.Core.HostSimulator();
        var config = new HostConfig { PacketTimeoutMs = 5000 };

        // Act - Initialize
        host.Initialize(config);
        var status = host.GetStatus();

        // Assert - Status reports initialized state
        status.Should().Contain("HostSimulator");
        status.Should().Contain("Received=0");

        // Act - Process with FrameData (direct pass-through)
        var testFrame = new FrameData(0, 32, 32, new ushort[32 * 32]);
        var output = host.Process(testFrame);
        output.Should().BeOfType<FrameData>();

        // Act - Reset
        host.Reset();
        var statusAfterReset = host.GetStatus();
        statusAfterReset.Should().Contain("Received=0");
        statusAfterReset.Should().Contain("Completed=0");
    }

    [Fact]
    public void FpgaBypass_McuAndHostStillWork()
    {
        // Arrange - Create a known 64x64 frame directly (bypassing Panel + FPGA)
        int rows = 64, cols = 64;
        var pixels2D = new ushort[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                pixels2D[r, c] = (ushort)((r * cols + c) & 0xFFFF);

        // Act - Feed directly to MCU (UdpFrameTransmitter) then to Host
        var udpTransmitter = new UdpFrameTransmitter();
        var udpPackets = udpTransmitter.FragmentFrame(pixels2D, frameId: 1);

        var host = new HostSimulator.Core.HostSimulator();
        host.Initialize(new HostConfig { PacketTimeoutMs = 5000 });

        FrameData? hostOutput = null;
        foreach (var packet in udpPackets)
        {
            var result = host.Process(packet.Data);
            if (result is FrameData fd)
                hostOutput = fd;
        }

        // Assert - Host correctly reassembles without FPGA involvement
        hostOutput.Should().NotBeNull();
        hostOutput!.Width.Should().Be(cols);
        hostOutput.Height.Should().Be(rows);

        // Verify pixel data matches original
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int idx = r * cols + c;
                hostOutput.Pixels[idx].Should().Be(pixels2D[r, c],
                    $"pixel at [{r},{c}] should match original");
            }
        }
    }

    [Fact]
    public void McuBypass_HostStillWorks_WithDirectFrame()
    {
        // Arrange - Create a frame and pass directly to Host (bypassing MCU entirely)
        int rows = 32, cols = 32;
        var pixels = new ushort[rows * cols];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = (ushort)(i & 0xFFFF);

        var frame = new FrameData(0, cols, rows, pixels);

        // Act - Pass directly to Host
        var host = new HostSimulator.Core.HostSimulator();
        host.Initialize(new HostConfig { PacketTimeoutMs = 5000 });
        var output = (FrameData)host.Process(frame);

        // Assert - Host returns identical frame
        output.Width.Should().Be(cols);
        output.Height.Should().Be(rows);
        output.Pixels.Should().BeEquivalentTo(pixels);
    }

    [Fact]
    public void Csi2Pipeline_WithoutUdp_McuReassemblesCorrectly()
    {
        // Arrange - Create frame, generate CSI-2 packets, reassemble (no UDP step)
        int rows = 128, cols = 128;
        var pixels2D = new ushort[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                pixels2D[r, c] = (ushort)((r * cols + c) & 0xFFFF);

        // Act - FPGA TX -> MCU RX (CSI-2 only, no UDP)
        var csi2Tx = new Csi2TxPacketGenerator();
        var packets = csi2Tx.GenerateFullFrame(pixels2D);

        var reassembler = new FrameReassembler();
        foreach (var packet in packets)
            reassembler.AddPacket(packet);

        var mcuFrame = reassembler.GetFrame();

        // Assert - MCU correctly reassembles without UDP involvement
        mcuFrame.IsValid.Should().BeTrue();
        mcuFrame.Rows.Should().Be(rows);
        mcuFrame.Cols.Should().Be(cols);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                mcuFrame.Pixels[r, c].Should().Be(pixels2D[r, c],
                    $"pixel at [{r},{c}]");
            }
        }
    }

    [Fact]
    public void PanelSimulator_Reset_ClearsFrameCounter()
    {
        // Arrange
        var panel = new PanelSimulator.PanelSimulator();
        panel.Initialize(new PanelConfig
        {
            Rows = 16, Cols = 16, BitDepth = 16,
            TestPattern = TestPattern.Counter,
            NoiseModel = NoiseModelType.None,
            NoiseStdDev = 0, DefectRate = 0, Seed = 1
        });

        // Act - Process 3 frames, then reset
        panel.Process(new object()); // frame 0
        panel.Process(new object()); // frame 1
        panel.Process(new object()); // frame 2
        panel.Reset();
        var afterReset = (FrameData)panel.Process(new object()); // should be frame 0 again

        // Assert
        afterReset.FrameNumber.Should().Be(0, "frame counter should reset to 0");
    }

    [Fact]
    public void HostSimulator_Reset_ClearsCounters()
    {
        // Arrange
        var host = new HostSimulator.Core.HostSimulator();
        host.Initialize(new HostConfig { PacketTimeoutMs = 5000 });

        // Process a frame
        var frame = new FrameData(0, 16, 16, new ushort[256]);
        host.Process(frame);
        host.FramesReceived.Should().Be(1);
        host.FramesCompleted.Should().Be(1);

        // Act
        host.Reset();

        // Assert
        host.FramesReceived.Should().Be(0);
        host.FramesCompleted.Should().Be(0);
        host.FramesIncomplete.Should().Be(0);
    }

    [Fact]
    public void McuFrameReassembler_Reset_ClearsState()
    {
        // Arrange - Add some packets
        var reassembler = new FrameReassembler();
        var tx = new Csi2TxPacketGenerator();
        var pixels = new ushort[16, 16];
        var packets = tx.GenerateFullFrame(pixels);

        foreach (var p in packets)
            reassembler.AddPacket(p);

        reassembler.IsFrameComplete.Should().BeTrue();

        // Act
        reassembler.Reset();

        // Assert
        reassembler.HasFrameStart.Should().BeFalse();
        reassembler.IsFrameComplete.Should().BeFalse();
        reassembler.ReceivedLineCount.Should().Be(0);
    }
}
