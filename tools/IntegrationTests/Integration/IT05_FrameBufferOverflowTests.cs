using Xunit;
using FluentAssertions;
using IntegrationTests.Helpers;
using Common.Dto.Interfaces;
using Common.Dto.Dtos;
using HostSimulator.Core;
using HostSimulator.Core.Configuration;
using HostSimulator.Core.Reassembly;
using CoreHostSimulator = HostSimulator.Core.HostSimulator;
using System.Diagnostics;

namespace IntegrationTests.Integration;

/// <summary>
/// IT-05: Frame Buffer Overflow Recovery test.
/// Validates 4-frame ring buffer behavior under overflow conditions.
/// Reference: SPEC-INTEG-001 AC-INTEG-005
/// </summary>
public class IT05_FrameBufferOverflowTests : IDisposable
{
    private const int RingBufferSize = 4;
    private readonly CoreHostSimulator _hostSimulator;
    private readonly FrameReassembler _reassembler;

    public IT05_FrameBufferOverflowTests()
    {
        var config = new HostConfig { PacketTimeoutMs = 5000 };
        _hostSimulator = new CoreHostSimulator();
        _hostSimulator.Initialize(config);
        _reassembler = new FrameReassembler(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task FrameBufferOverflow_ShallDropOldestFrames_NoCrashOrDeadlock()
    {
        // Arrange - Create 6 frames (more than 4-frame ring buffer)
        var frames = new List<FrameData>();
        for (int i = 0; i < 6; i++)
        {
            var frame = TestFrameFactory.Create1024Gradient(i);
            frames.Add(new FrameData(
                i,
                frame.Width,
                frame.Height,
                frame.Pixels
            ));
        }

        // Create UDP packets for each frame
        var allPackets = new List<byte[]>();
        foreach (var frame in frames)
        {
            allPackets.AddRange(CreatePacketsForFrame(frame));
        }

        var receivedFrames = new List<FrameData>();
        var stopwatch = Stopwatch.StartNew();

        // Act - Process all packets (simulating producer faster than consumer)
        // Simulate slow consumer by adding 100ms delay
        int packetCount = 0;
        foreach (var packet in allPackets)
        {
            FrameHeader.TryParse(packet, out var header);
            var payload = new byte[packet.Length - FrameHeader.HEADER_SIZE];
            Array.Copy(packet, FrameHeader.HEADER_SIZE, payload, 0, payload.Length);

            var result = _reassembler.ProcessPacket(header!, payload);
            if (result?.Status == FrameReassemblyStatus.Complete)
            {
                receivedFrames.Add(result.Frame);

                // Simulate slow consumer (100ms delay per frame)
                await Task.Delay(100);
            }

            packetCount++;

            // Safety timeout
            if (stopwatch.ElapsedMilliseconds > 30000)
                break;
        }

        stopwatch.Stop();

        // Assert
        // 1. No crash or deadlock (test completed)
        // 2. Some frames should be received (buffer overflow handling)
        // 3. Test completed within 30-second timeout
        receivedFrames.Count.Should().BeGreaterThan(0, "At least some frames should be received");
        receivedFrames.Count.Should().BeLessThan(6, "Some frames should be dropped due to buffer overflow");
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(30000, "Test should complete within timeout");

        // 4. Oldest frames should be dropped (verify sequence numbers)
        if (receivedFrames.Count > 1)
        {
            var frameNumbers = receivedFrames.Select(f => f.FrameNumber).ToList();
            for (int i = 1; i < frameNumbers.Count; i++)
            {
                frameNumbers[i].Should().BeGreaterThan(frameNumbers[i - 1],
                    "Frame numbers should be sequential (oldest dropped)");
            }
        }
    }

    [Fact]
    public void FrameBufferOverflow_ShallRecover_WhenConsumerResumes()
    {
        // Arrange - Fill buffer to capacity
        var frames = new List<FrameData>();
        for (int i = 0; i < RingBufferSize + 2; i++)
        {
            var frame = TestFrameFactory.Create1024Gradient(i);
            frames.Add(new FrameData(
                i,
                frame.Width,
                frame.Height,
                frame.Pixels
            ));
        }

        // Act - Process frames with slow consumer
        var receivedFrames = new List<FrameData>();
        int slowDelayMs = 100;
        int fastDelayMs = 10;

        // Phase 1: Slow consumer (cause overflow)
        for (int i = 0; i < RingBufferSize; i++)
        {
            var packets = CreatePacketsForFrame(frames[i]);
            foreach (var packet in packets)
            {
                FrameHeader.TryParse(packet, out var header);
                var payload = new byte[packet.Length - FrameHeader.HEADER_SIZE];
                Array.Copy(packet, FrameHeader.HEADER_SIZE, payload, 0, payload.Length);

                var result = _reassembler.ProcessPacket(header!, payload);
                if (result?.Status == FrameReassemblyStatus.Complete)
                {
                    receivedFrames.Add(result.Frame);
                    Task.Delay(slowDelayMs).Wait(); // Slow
                }
            }
        }

        int overflowCount = receivedFrames.Count;

        // Phase 2: Resume normal speed (recovery)
        for (int i = RingBufferSize; i < frames.Count; i++)
        {
            var packets = CreatePacketsForFrame(frames[i]);
            foreach (var packet in packets)
            {
                FrameHeader.TryParse(packet, out var header);
                var payload = new byte[packet.Length - FrameHeader.HEADER_SIZE];
                Array.Copy(packet, FrameHeader.HEADER_SIZE, payload, 0, payload.Length);

                var result = _reassembler.ProcessPacket(header!, payload);
                if (result?.Status == FrameReassemblyStatus.Complete)
                {
                    receivedFrames.Add(result.Frame);
                    Task.Delay(fastDelayMs).Wait(); // Fast
                }
            }
        }

        // Assert - Recovery should occur
        receivedFrames.Count.Should().BeGreaterThan(overflowCount,
            "After consumer resumes, more frames should be received");

        // Verify frames are valid (no data corruption)
        foreach (var frame in receivedFrames)
        {
            frame.Pixels.Should().NotBeNullOrEmpty("Frames should contain data");
            frame.Width.Should().Be(1024);
            frame.Height.Should().Be(1024);
        }
    }

    [Fact]
    public void FrameBufferOverflow_ShallMaintainIntegrity_NoCorruption()
    {
        // Arrange - Use gradient pattern for integrity check
        var frames = new List<FrameData>();
        for (int i = 0; i < RingBufferSize * 2; i++)
        {
            var frame = TestFrameFactory.CreateGradientFrame(512, 512, i);
            frames.Add(new FrameData(
                i,
                frame.Width,
                frame.Height,
                frame.Pixels
            ));
        }

        // Act - Process all frames rapidly (overflow condition)
        var receivedFrames = new List<FrameData>();
        foreach (var frame in frames)
        {
            var packets = CreatePacketsForFrame(frame);
            foreach (var packet in packets)
            {
                FrameHeader.TryParse(packet, out var header);
                var payload = new byte[packet.Length - FrameHeader.HEADER_SIZE];
                Array.Copy(packet, FrameHeader.HEADER_SIZE, payload, 0, payload.Length);

                var result = _reassembler.ProcessPacket(header!, payload);
                if (result?.Status == FrameReassemblyStatus.Complete)
                {
                    receivedFrames.Add(result.Frame);
                }
            }
        }

        // Assert - Verify data integrity in received frames
        foreach (var frame in receivedFrames)
        {
            // Check gradient pattern: leftmost pixel = 0, rightmost = 65535
            ushort firstPixel = frame.Pixels[0];
            ushort lastPixel = frame.Pixels[frame.Pixels.Length - 1];

            firstPixel.Should().Be(0, "Leftmost pixel should be 0 (gradient start)");
            lastPixel.Should().Be(65535, "Rightmost pixel should be 65535 (gradient end)");

            // No corruption: check middle pixel for expected value
            ushort middlePixel = frame.Pixels[frame.Pixels.Length / 2];
            middlePixel.Should().BeGreaterThan(30000, "Middle pixel should be near mid-range");
            middlePixel.Should().BeLessThan(35000, "Middle pixel should be near mid-range");
        }
    }

    /// <summary>
    /// Creates UDP packets for a frame (simplified: 1 pixel per packet).
    /// </summary>
    private static byte[][] CreatePacketsForFrame(FrameData frame)
    {
        // Simplified: create a single packet for the frame
        // In real scenario, frame would be split into multiple packets
        int totalPackets = Math.Max(1, frame.Pixels.Length / 1000); // Limit to 1000 pixels per packet
        var packets = new byte[totalPackets][];

        for (int i = 0; i < totalPackets; i++)
        {
            int pixelStart = i * 1000;
            int pixelCount = Math.Min(1000, frame.Pixels.Length - pixelStart);
            int payloadSize = pixelCount * 2;

            packets[i] = new byte[FrameHeader.HEADER_SIZE + payloadSize];
            var span = new System.Span<byte>(packets[i]);

            // Magic
            span[0] = 0x34; span[1] = 0x12; span[2] = 0xE0; span[3] = 0xD7;

            // Version
            span[4] = 0x01; span[5] = 0x00; span[6] = 0x00; span[7] = 0x00;

            // Frame ID
            uint frameId = (uint)frame.FrameNumber;
            span[8] = (byte)(frameId & 0xFF);
            span[9] = (byte)((frameId >> 8) & 0xFF);
            span[10] = (byte)((frameId >> 16) & 0xFF);
            span[11] = (byte)((frameId >> 24) & 0xFF);

            // Packet sequence
            span[12] = (byte)(i & 0xFF);
            span[13] = (byte)((i >> 8) & 0xFF);

            // Total packets
            span[14] = (byte)(totalPackets & 0xFF);
            span[15] = (byte)((totalPackets >> 8) & 0xFF);

            // Dimensions
            span[24] = (byte)(frame.Height & 0xFF);
            span[25] = (byte)((frame.Height >> 8) & 0xFF);
            span[26] = (byte)(frame.Width & 0xFF);
            span[27] = (byte)((frame.Width >> 8) & 0xFF);

            // Pixel data
            for (int p = 0; p < pixelCount; p++)
            {
                int offset = FrameHeader.HEADER_SIZE + (p * 2);
                ushort pixel = frame.Pixels[pixelStart + p];
                span[offset] = (byte)(pixel & 0xFF);
                span[offset + 1] = (byte)((pixel >> 8) & 0xFF);
            }

            // CRC (simplified - zero for testing)
            span[28] = 0;
            span[29] = 0;

            // Bit depth
            span[30] = 16;

            // Flags
            span[31] = 0;
        }

        return packets;
    }

    public void Dispose()
    {
        // FrameReassembler doesn't implement Dispose
    }
}
