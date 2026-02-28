using FluentAssertions;
using IntegrationTests.Helpers;
using Common.Dto.Dtos;
using CoreHostSimulator = HostSimulator.Core.HostSimulator;
using HostSimulator.Core.Configuration;
using HostSimulator.Core.Reassembly;
using Xunit;

namespace IntegrationTests.Integration;

/// <summary>
/// Integration test IT-01: Full pipeline data integrity (refactored with helpers).
/// Verifies HostSimulator frame reassembly from UDP packets.
/// Reference: SPEC-SIM-001 AC-SIM-007, AC-SIM-008
/// Refactored to use TestFrameFactory and PacketFactory helpers.
/// </summary>
public class It01FullPipelineTests
{
    [Fact]
    public void HostSimulator_ReassembleFrame_FromUdpPackets_CompleteFrame()
    {
        // Arrange - Using TestFrameFactory for consistent test data
        var testFrame = TestFrameFactory.Create1024Gradient(frameNumber: 1);
        var hostConfig = new HostConfig { PacketTimeoutMs = 5000 };
        var host = new CoreHostSimulator();
        host.Initialize(hostConfig);

        // Create packets using helper
        var packets = CreateUdpPacketsFromFrameData(testFrame);

        // Act - Process packets
        FrameData? result = null;
        foreach (var packet in packets)
        {
            var processResult = host.Process(packet);
            if (processResult is FrameData frame)
                result = frame;
        }

        // Assert - Bit-exact match using TestFrameFactory frame
        result.Should().NotBeNull();
        result!.FrameNumber.Should().Be(1);
        result.Width.Should().Be(1024);
        result.Height.Should().Be(1024);
        result.Pixels.Should().Equal(testFrame.Pixels);
    }

    [Fact]
    public void HostSimulator_ReassembleFrame_OutOfOrderPackets_CompleteFrame()
    {
        // Arrange - Using TestFrameFactory with small frame for testing
        var testFrame = TestFrameFactory.CreateSolidFrame(64, 64, frameNumber: 5);
        var hostConfig = new HostConfig { PacketTimeoutMs = 5000 };
        var host = new CoreHostSimulator();
        host.Initialize(hostConfig);

        // Create packets (will be sent out of order)
        var packets = CreateUdpPacketsFromFrameData(testFrame);

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
        result!.Pixels.Should().Equal(testFrame.Pixels);
    }

    [Fact]
    public void FrameReassembler_CheckerboardPattern_PreservesPattern()
    {
        // Arrange - Using TestFrameFactory checkerboard pattern
        var testFrame = TestFrameFactory.CreateTestFrame(32, 32, TestFrameFactory.PatternType.Checkerboard, frameNumber: 1);
        var reassembler = new FrameReassembler(TimeSpan.FromSeconds(1));

        // Create packets (1 pixel per packet for testing)
        var packets = CreateUdpPacketsFromFrameData(testFrame);

        // Act - Process packets
        FrameReassemblyResult? result = null;
        foreach (var packet in packets)
        {
            result = reassembler.ProcessPacket(ParseHeader(packet), GetPayload(packet));
        }

        // Assert - Checkerboard pattern must be preserved
        result.Should().NotBeNull();
        result!.Status.Should().Be(FrameReassemblyStatus.Complete);
        result.Frame.Should().NotBeNull();
        result.Frame!.Pixels.Should().Equal(testFrame.Pixels);
    }

    [Fact]
    public void FrameReassembler_GradientPattern_PreservesGradient()
    {
        // Arrange - Using TestFrameFactory gradient pattern
        var testFrame = TestFrameFactory.CreateGradientFrame(128, 64, frameNumber: 2);
        var reassembler = new FrameReassembler(TimeSpan.FromSeconds(1));

        // Create packets (4 pixels per packet for efficiency)
        var packets = CreateUdpPacketsWithPayloadSize(testFrame, pixelsPerPacket: 4);

        // Act - Process packets
        FrameReassemblyResult? result = null;
        foreach (var packet in packets)
        {
            result = reassembler.ProcessPacket(ParseHeader(packet), GetPayload(packet));
        }

        // Assert - Gradient must be preserved
        result.Should().NotBeNull();
        result!.Status.Should().Be(FrameReassemblyStatus.Complete);
        result.Frame.Should().NotBeNull();
        result.Frame!.Pixels.Should().Equal(testFrame.Pixels);
        // Verify gradient properties
        result.Frame.Pixels[0].Should().Be(0);
        result.Frame.Pixels[127].Should().Be(65535);
    }

    [Fact]
    public void FrameReassembler_VerifyCrc16_RejectsCorruptedPackets()
    {
        // Arrange - Using PacketFactory for CRC testing
        var testData = PacketFactory.CreateTestPayload(32);
        ushort validCrc = PacketFactory.CalculateCrc16Ccitt(testData);

        // Corrupt the test data
        var corruptedData = (byte[])testData.Clone();
        corruptedData[0] ^= 0xFF;

        ushort corruptedCrc = PacketFactory.CalculateCrc16Ccitt(corruptedData);

        // Assert - CRC should detect corruption
        corruptedCrc.Should().NotBe(validCrc);
        PacketFactory.ValidateCrc16Ccitt(testData, validCrc).Should().BeTrue();
        PacketFactory.ValidateCrc16Ccitt(corruptedData, validCrc).Should().BeFalse();
    }

    /// <summary>
    /// Creates UDP packets for a frame (1 pixel per packet for testing).
    /// Uses FrameData from TestFrameFactory.
    /// </summary>
    private static byte[][] CreateUdpPacketsFromFrameData(FrameData frame)
    {
        return CreateUdpPacketsWithPayloadSize(frame, pixelsPerPacket: 1);
    }

    /// <summary>
    /// Creates UDP packets with specified payload size.
    /// </summary>
    private static byte[][] CreateUdpPacketsWithPayloadSize(FrameData frame, int pixelsPerPacket)
    {
        int totalPackets = (frame.Pixels.Length + pixelsPerPacket - 1) / pixelsPerPacket;
        var packets = new byte[totalPackets][];

        for (int i = 0; i < totalPackets; i++)
        {
            int startPixel = i * pixelsPerPacket;
            int pixelCount = Math.Min(pixelsPerPacket, frame.Pixels.Length - startPixel);
            var pixels = new ushort[pixelCount];
            Array.Copy(frame.Pixels, startPixel, pixels, 0, pixelCount);

            packets[i] = CreateUdpPacket(
                frameId: (uint)frame.FrameNumber,
                packetSeq: (ushort)i,
                totalPackets: (ushort)totalPackets,
                rows: (ushort)frame.Height,
                cols: (ushort)frame.Width,
                pixels: pixels
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

        // Calculate CRC over bytes 0-27 using PacketFactory
        var headerForCrc = new byte[28];
        Array.Copy(packet, 0, headerForCrc, 0, 28);
        ushort crc = PacketFactory.CalculateCrc16Ccitt(headerForCrc);
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
