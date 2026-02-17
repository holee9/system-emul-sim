using Xunit;
using FluentAssertions;
using Common.Dto.Dtos;
using CoreHostSimulator = HostSimulator.Core.HostSimulator;
using HostSimulator.Core.Configuration;
using HostSimulator.Core.Reassembly;

namespace IntegrationTests.Integration;

/// <summary>
/// Integration test IT-01: Full pipeline data integrity (simplified version).
/// Verifies HostSimulator frame reassembly from UDP packets.
/// Reference: SPEC-SIM-001 AC-SIM-007, AC-SIM-008
/// </summary>
public class It01FullPipelineTests
{
    [Fact]
    public void HostSimulator_ReassembleFrame_FromUdpPackets_CompleteFrame()
    {
        // Arrange
        var hostConfig = new HostConfig { PacketTimeoutMs = 5000 };
        var host = new CoreHostSimulator();
        host.Initialize(hostConfig);

        // Create test frame: 2x2 pixels = {0, 1, 2, 3}
        var inputPixels = new ushort[] { 0, 1, 2, 3 };
        var inputFrame = new FrameData(frameNumber: 1, width: 2, height: 2, pixels: inputPixels);

        // Simulate UDP packets with frame header
        var packets = CreateUdpPackets(inputFrame);

        // Act - Process packets
        FrameData? result = null;
        foreach (var packet in packets)
        {
            var processResult = host.Process(packet);
            if (processResult is FrameData frame)
                result = frame;
        }

        // Assert - Bit-exact match
        result.Should().NotBeNull();
        result!.FrameNumber.Should().Be(1);
        result.Width.Should().Be(2);
        result.Height.Should().Be(2);
        result.Pixels.Should().Equal(inputPixels);
    }

    [Fact]
    public void HostSimulator_ReassembleFrame_OutOfOrderPackets_CompleteFrame()
    {
        // Arrange
        var hostConfig = new HostConfig { PacketTimeoutMs = 5000 };
        var host = new CoreHostSimulator();
        host.Initialize(hostConfig);

        // Create test frame: 4x1 pixels = {10, 20, 30, 40}
        var inputPixels = new ushort[] { 10, 20, 30, 40 };
        var inputFrame = new FrameData(frameNumber: 5, width: 4, height: 1, pixels: inputPixels);

        // Simulate UDP packets (will be sent out of order)
        var packets = CreateUdpPackets(inputFrame);

        // Act - Process packets in reverse order (out of order)
        Array.Reverse(packets);
        FrameData? result = null;
        foreach (var packet in packets)
        {
            var processResult = host.Process(packet);
            if (processResult is FrameData frame)
                result = frame;
        }

        // Assert - Should still reassemble correctly
        result.Should().NotBeNull();
        result!.Pixels.Should().Equal(inputPixels);
    }

    [Fact]
    public void FrameReassembler_MultiplePackets_CompleteFrame()
    {
        // Arrange
        var reassembler = new FrameReassembler(TimeSpan.FromSeconds(1));

        // Create 4 packets for a 2x2 frame
        var pixels = new ushort[] { 100, 200, 300, 400 };
        var packets = new byte[4][];

        for (int i = 0; i < 4; i++)
        {
            packets[i] = CreateUdpPacket(
                frameId: 1,
                packetSeq: (ushort)i,
                totalPackets: 4,
                rows: 2,
                cols: 2,
                pixels: new ushort[] { pixels[i] }
            );
        }

        // Act - Process packets
        FrameReassemblyResult? result = null;
        foreach (var packet in packets)
        {
            result = reassembler.ProcessPacket(ParseHeader(packet), GetPayload(packet));
        }

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be(FrameReassemblyStatus.Complete);
        result.Frame.Should().NotBeNull();
        result.Frame!.Pixels.Should().Equal(pixels);
    }

    /// <summary>
    /// Creates UDP packets for a frame (simplified - 1 pixel per packet for testing).
    /// </summary>
    private static byte[][] CreateUdpPackets(FrameData frame)
    {
        int totalPackets = frame.Pixels.Length;
        var packets = new byte[totalPackets][];

        for (int i = 0; i < totalPackets; i++)
        {
            packets[i] = CreateUdpPacket(
                frameId: (uint)frame.FrameNumber,
                packetSeq: (ushort)i,
                totalPackets: (ushort)totalPackets,
                rows: (ushort)frame.Height,
                cols: (ushort)frame.Width,
                pixels: new ushort[] { frame.Pixels[i] }
            );
        }

        return packets;
    }

    /// <summary>
    /// Creates a single UDP packet with frame header and pixel payload.
    /// </summary>
    private static byte[] CreateUdpPacket(uint frameId, ushort packetSeq, ushort totalPackets, ushort rows, ushort cols, ushort[] pixels)
    {
        var packet = new byte[FrameHeader.HEADER_SIZE + (pixels.Length * 2)];
        var span = new Span<byte>(packet);

        // Write frame header
        span[0] = 0x34; // Magic: 0xD7E01234 (little-endian)
        span[1] = 0x12;
        span[2] = 0xE0;
        span[3] = 0xD7;

        span[4] = 0x01; // Version
        span[5] = 0x00;
        span[6] = 0x00;
        span[7] = 0x00;

        // Frame ID
        span[8] = (byte)(frameId & 0xFF);
        span[9] = (byte)((frameId >> 8) & 0xFF);
        span[10] = (byte)((frameId >> 16) & 0xFF);
        span[11] = (byte)((frameId >> 24) & 0xFF);

        // Packet sequence
        span[12] = (byte)(packetSeq & 0xFF);
        span[13] = (byte)((packetSeq >> 8) & 0xFF);

        // Total packets
        span[14] = (byte)(totalPackets & 0xFF);
        span[15] = (byte)((totalPackets >> 8) & 0xFF);

        // Timestamp (8 bytes) - zeros for testing
        // (already zero-initialized)

        // Rows (offset 24, 2 bytes)
        span[24] = (byte)(rows & 0xFF);
        span[25] = (byte)((rows >> 8) & 0xFF);

        // Cols (offset 26, 2 bytes)
        span[26] = (byte)(cols & 0xFF);
        span[27] = (byte)((cols >> 8) & 0xFF);

        // Write pixel data
        for (int i = 0; i < pixels.Length; i++)
        {
            int offset = FrameHeader.HEADER_SIZE + (i * 2);
            span[offset] = (byte)(pixels[i] & 0xFF);
            span[offset + 1] = (byte)((pixels[i] >> 8) & 0xFF);
        }

        // Calculate CRC over bytes 0-27
        ushort crc = Crc16Ccitt.Calculate(packet, 0, 28);
        span[28] = (byte)(crc & 0xFF);
        span[29] = (byte)((crc >> 8) & 0xFF);

        // Bit depth (offset 30)
        span[30] = 16;

        // Flags (offset 31)
        span[31] = 0;

        return packet;
    }

    private static FrameHeader ParseHeader(byte[] packet)
    {
        FrameHeader.TryParse(packet, out var header).Should().BeTrue();
        return header!;
    }

    private static byte[] GetPayload(byte[] packet)
    {
        var payload = new byte[packet.Length - FrameHeader.HEADER_SIZE];
        Array.Copy(packet, FrameHeader.HEADER_SIZE, payload, 0, payload.Length);
        return payload;
    }
}
