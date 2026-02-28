using Xunit;
using FluentAssertions;
using IntegrationTests.Helpers;
using Common.Dto.Dtos;
using HostSimulator.Core;
using HostSimulator.Core.Configuration;
using HostSimulator.Core.Reassembly;
using CoreHostSimulator = HostSimulator.Core.HostSimulator;
using System.Diagnostics;
using System.Buffers.Binary;

/// <summary>
/// IT-08: 10GbE Packet Loss and Retransmission test.
/// Validates system resilience to network packet loss.
/// Reference: SPEC-INTEG-001 AC-INTEG-008
/// </summary>
public class IT08_PacketLossRetransmissionTests : IDisposable
{
    private const double PacketLossRate = 0.001; // 0.1%
    private const int FrameTimeoutMs = 2000; // 2 seconds

    private readonly CoreHostSimulator _hostSimulator;
    private readonly FrameReassembler _reassembler;
    private readonly Random _random;

    public IT08_PacketLossRetransmissionTests()
    {
        var config = new HostConfig { PacketTimeoutMs = FrameTimeoutMs };
        _hostSimulator = new CoreHostSimulator();
        _hostSimulator.Initialize(config);
        _reassembler = new FrameReassembler(TimeSpan.FromMilliseconds(FrameTimeoutMs));
        _random = new Random(42); // Fixed seed for reproducibility
    }

    [Fact]
    public async Task PacketLossInjection_ShouldRecoverFrames_NoPermanentLoss()
    {
        // Arrange - Create 100 test frames
        int targetFrameCount = 100;
        var frames = new List<FrameData>();
        for (int i = 0; i < targetFrameCount; i++)
        {
            var frame = TestFrameFactory.Create2048Gradient(i);
            frames.Add(new FrameData(
                i,
                frame.Width,
                frame.Height,
                frame.Pixels
            ));
        }

        // Act - Process packets with 0.1% packet loss injection
        var receivedFrames = new List<FrameData>();
        var recoveryLatencies = new List<long>();
        var stopwatch = Stopwatch.StartNew();

        foreach (var frame in frames)
        {
            var packets = CreatePacketsForFrame(frame);
            var frameStartTime = stopwatch.ElapsedMilliseconds;

            // Process with simulated packet loss
            bool frameComplete = false;
            foreach (var packet in packets)
            {
                // Simulate 0.1% packet loss
                if (_random.NextDouble() < PacketLossRate)
                    continue; // Drop packet

                if (!FrameHeader.TryParse(packet, out var header))
                {
                    continue;
                }
                var payload = new byte[packet.Length - FrameHeader.HEADER_SIZE];
                Array.Copy(packet, FrameHeader.HEADER_SIZE, payload, 0, payload.Length);

                var result = _reassembler.ProcessPacket(header, payload);
                if (result?.Status == FrameReassemblyStatus.Complete)
                {
                    receivedFrames.Add(result.Frame);
                    recoveryLatencies.Add(stopwatch.ElapsedMilliseconds - frameStartTime);
                    frameComplete = true;
                }
            }

            // Simulate retransmission delay for incomplete frames
            if (!frameComplete)
            {
                await Task.Delay(50); // Retransmission delay

                // Retry with all packets (simulating retransmission)
                foreach (var packet in packets)
                {
                    if (!FrameHeader.TryParse(packet, out var header))
                    {
                        continue;
                    }
                    var payload = new byte[packet.Length - FrameHeader.HEADER_SIZE];
                    Array.Copy(packet, FrameHeader.HEADER_SIZE, payload, 0, payload.Length);

                    var result = _reassembler.ProcessPacket(header, payload);
                    if (result?.Status == FrameReassemblyStatus.Complete)
                    {
                        receivedFrames.Add(result.Frame);
                        recoveryLatencies.Add(stopwatch.ElapsedMilliseconds - frameStartTime);
                        break;
                    }
                }
            }
        }

        stopwatch.Stop();

        // Assert - All frames should be recovered
        receivedFrames.Count.Should().Be(targetFrameCount,
            $"All {targetFrameCount} frames should be recovered after retransmission");

        // Verify frame payload integrity
        foreach (var frame in receivedFrames)
        {
            frame.Pixels.Should().NotBeNullOrEmpty("Recovered frames should contain data");
            frame.Width.Should().Be(2048);
            frame.Height.Should().Be(2048);
        }
    }

    [Fact]
    public async Task PacketLossRecovery_ShouldCompleteWithinTimeout_AllFramesRecover()
    {
        // Arrange - Create frames with high packet loss rate for testing
        int frameCount = 10;
        double highLossRate = 0.05; // 5% loss for faster testing
        var frames = new List<FrameData>();

        for (int i = 0; i < frameCount; i++)
        {
            var frame = TestFrameFactory.Create1024Gradient(i);
            frames.Add(new FrameData(
                i,
                frame.Width,
                frame.Height,
                frame.Pixels
            ));
        }

        // Act - Process with retransmission
        var receivedFrames = new List<FrameData>();
        var stopwatch = Stopwatch.StartNew();

        foreach (var frame in frames)
        {
            var packets = CreatePacketsForFrame(frame);
            var frameStartTime = stopwatch.ElapsedMilliseconds;

            // Initial attempt with packet loss
            bool frameComplete = false;
            foreach (var packet in packets)
            {
                if (_random.NextDouble() < highLossRate)
                    continue;

                if (!FrameHeader.TryParse(packet, out var header))
                {
                    continue;
                }
                var payload = new byte[packet.Length - FrameHeader.HEADER_SIZE];
                Array.Copy(packet, FrameHeader.HEADER_SIZE, payload, 0, payload.Length);

                var result = _reassembler.ProcessPacket(header, payload);
                if (result?.Status == FrameReassemblyStatus.Complete)
                {
                    receivedFrames.Add(result.Frame);
                    frameComplete = true;
                }
            }

            // Retransmission if needed
            if (!frameComplete)
            {
                await Task.Delay(100);

                // Retry all packets
                foreach (var packet in packets)
                {
                    if (!FrameHeader.TryParse(packet, out var header))
                    {
                        continue; // Skip invalid packets
                    }
                    var payload = new byte[packet.Length - FrameHeader.HEADER_SIZE];
                    Array.Copy(packet, FrameHeader.HEADER_SIZE, payload, 0, payload.Length);

                    var result = _reassembler.ProcessPacket(header, payload);
                    if (result?.Status == FrameReassemblyStatus.Complete)
                    {
                        receivedFrames.Add(result.Frame);
                        long latency = stopwatch.ElapsedMilliseconds - frameStartTime;
                        latency.Should().BeLessThan(FrameTimeoutMs,
                            $"Frame recovery should complete within {FrameTimeoutMs}ms timeout");
                        break;
                    }
                }
            }
        }

        stopwatch.Stop();

        // Assert - All frames recovered
        receivedFrames.Count.Should().Be(frameCount,
            "All frames should be recovered after retransmission");
    }

    [Fact]
    public void PacketLossRecovery_Latency_ShouldBeWithinP95Threshold()
    {
        // Arrange - Create frames for latency measurement
        int frameCount = 50;
        var frames = new List<FrameData>();
        var latencyMeasurer = new LatencyMeasurer();

        for (int i = 0; i < frameCount; i++)
        {
            var frame = TestFrameFactory.Create1024Gradient(i);
            frames.Add(new FrameData(
                i,
                frame.Width,
                frame.Height,
                frame.Pixels
            ));
        }

        // Act - Process with packet loss and measure latency
        var stopwatch = Stopwatch.StartNew();

        foreach (var frame in frames)
        {
            var packets = CreatePacketsForFrame(frame);
            var frameStartTime = stopwatch.ElapsedMilliseconds;

            // Simulate packet loss and recovery
            foreach (var packet in packets)
            {
                // 10% loss for testing
                if (_random.NextDouble() < 0.1)
                    continue;

                if (!FrameHeader.TryParse(packet, out var header))
                {
                    continue;
                }
                var payload = new byte[packet.Length - FrameHeader.HEADER_SIZE];
                Array.Copy(packet, FrameHeader.HEADER_SIZE, payload, 0, payload.Length);

                var result = _reassembler.ProcessPacket(header, payload);
                if (result?.Status == FrameReassemblyStatus.Complete)
                {
                    long latency = stopwatch.ElapsedMilliseconds - frameStartTime;
                    latencyMeasurer.RecordLatency(latency);
                    break;
                }
            }
        }

        stopwatch.Stop();

        // Calculate percentiles
        var percentiles = latencyMeasurer.CalculatePercentiles();

        // Assert - P95 latency should be < 500ms
        percentiles.P95.Should().BeLessThan(500,
            $"P95 recovery latency ({percentiles.P95:F2}ms) should be < 500ms");
    }

    [Fact]
    public async Task PacketLoss_ShouldNotCauseDataCorruption_IntegrityVerified()
    {
        // Arrange - Use gradient pattern for integrity check
        var frames = new List<FrameData>();
        for (int i = 0; i < 20; i++)
        {
            var frame = TestFrameFactory.CreateGradientFrame(512, 512, i);
            frames.Add(new FrameData(
                i,
                frame.Width,
                frame.Height,
                frame.Pixels
            ));
        }

        // Act - Process with packet loss
        var receivedFrames = new List<FrameData>();

        foreach (var frame in frames)
        {
            var packets = CreatePacketsForFrame(frame);

            foreach (var packet in packets)
            {
                if (_random.NextDouble() < PacketLossRate)
                    continue;

                if (!FrameHeader.TryParse(packet, out var header))
                {
                    continue;
                }
                var payload = new byte[packet.Length - FrameHeader.HEADER_SIZE];
                Array.Copy(packet, FrameHeader.HEADER_SIZE, payload, 0, payload.Length);

                var result = _reassembler.ProcessPacket(header, payload);
                if (result?.Status == FrameReassemblyStatus.Complete)
                {
                    receivedFrames.Add(result.Frame);
                }
            }

            // Retry once for incomplete frames
            await Task.Delay(10);
            foreach (var packet in packets)
            {
                if (!FrameHeader.TryParse(packet, out var header))
                {
                    continue; // Skip invalid packets
                }
                var payload = new byte[packet.Length - FrameHeader.HEADER_SIZE];
                Array.Copy(packet, FrameHeader.HEADER_SIZE, payload, 0, payload.Length);

                var result = _reassembler.ProcessPacket(header, payload);
                if (result?.Status == FrameReassemblyStatus.Complete && !receivedFrames.Any(f => f.FrameNumber == frame.FrameNumber))
                {
                    receivedFrames.Add(result.Frame);
                    break;
                }
            }
        }

        // Assert - Verify no data corruption
        foreach (var frame in receivedFrames)
        {
            // Check gradient pattern integrity
            ushort firstPixel = frame.Pixels[0];
            ushort lastPixel = frame.Pixels[frame.Pixels.Length - 1];

            firstPixel.Should().Be(0, "Gradient pattern: first pixel should be 0");
            lastPixel.Should().Be(65535, "Gradient pattern: last pixel should be 65535");
        }
    }

    /// <summary>
    /// Creates UDP packets for a frame.
    /// </summary>
    private static byte[][] CreatePacketsForFrame(FrameData frame)
    {
        // Simplified: create multiple packets for frame
        int packetsPerFrame = 10;
        int pixelsPerPacket = (frame.Pixels.Length + packetsPerFrame - 1) / packetsPerFrame;
        var packets = new byte[packetsPerFrame][];

        for (int i = 0; i < packetsPerFrame; i++)
        {
            int pixelStart = i * pixelsPerPacket;
            int pixelCount = Math.Min(pixelsPerPacket, frame.Pixels.Length - pixelStart);
            int payloadSize = pixelCount * 2;

            packets[i] = new byte[FrameHeader.HEADER_SIZE + payloadSize];
            var span = new System.Span<byte>(packets[i]);

            // Magic (0xD7E01234 little-endian)
            span[0] = 0x34; span[1] = 0x12; span[2] = 0xE0; span[3] = 0xD7;

            // Version
            span[4] = 0x01;

            // Frame ID (little-endian)
            uint frameId = (uint)frame.FrameNumber;
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(8), frameId);

            // Packet sequence (little-endian)
            BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(12), (ushort)i);

            // Total packets (little-endian)
            BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(14), (ushort)packetsPerFrame);

            // Dimensions (little-endian)
            BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(24), (ushort)frame.Height);
            BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(26), (ushort)frame.Width);

            // Pixel data
            for (int p = 0; p < pixelCount; p++)
            {
                int offset = FrameHeader.HEADER_SIZE + (p * 2);
                ushort pixel = frame.Pixels[pixelStart + p];
                span[offset] = (byte)(pixel & 0xFF);
                span[offset + 1] = (byte)((pixel >> 8) & 0xFF);
            }

            // Calculate CRC over bytes 0-27 using PacketFactory
            var headerForCrc = packets[i].AsSpan(0, 28).ToArray();
            ushort crc = PacketFactory.CalculateCrc16Ccitt(headerForCrc);
            BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(28), crc);

            // Bit depth and flags
            span[30] = 16;
            span[31] = 0;
        }

        return packets;
    }

    public void Dispose()
    {
        // FrameReassembler doesn't implement Dispose
    }
}
