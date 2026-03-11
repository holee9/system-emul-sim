using Xunit;
using FluentAssertions;
using IntegrationTests.Helpers;
using McuSimulator.Core.Network;
using Common.Dto.Dtos;
using System.Diagnostics;

/// <summary>
/// IT-20: NetworkChannel Complex Scenarios test.
/// Validates combined network impairments (loss + reorder + corruption).
/// Reference: SPEC-INTSIM-001 ER-005
/// </summary>
public class IT20_NetworkComplexScenarios : IDisposable
{
    private const int PacketCount = 1000;
    private const double TolerancePercent = 2.0; // Allow 2% deviation from expected rates

    [Fact]
    public void ComplexScenarios_Loss10_Reorder5_Corruption2_ShouldMatchRates()
    {
        // Arrange
        var config = new NetworkChannelConfig
        {
            PacketLossRate = 0.10,    // 10% loss
            ReorderRate = 0.05,       // 5% reorder
            CorruptionRate = 0.02,    // 2% corruption
            Seed = 42
        };

        var channel = new NetworkChannel(config);
        var packets = CreateTestPackets(PacketCount);

        // Act
        var result = channel.TransmitPackets(packets);

        // Assert
        double actualLossRate = (double)channel.PacketsLost / PacketCount;
        double expectedLossRate = config.PacketLossRate;

        double actualCorruptionRate = (double)channel.PacketsCorrupted / PacketCount;
        double expectedCorruptionRate = config.CorruptionRate;

        // Verify loss rate
        actualLossRate.Should().BeApproximately(expectedLossRate, TolerancePercent / 100.0,
            $"loss rate should be around {expectedLossRate:P1}");

        // Verify corruption rate (corruption applies to surviving packets)
        int survivingPackets = PacketCount - (int)channel.PacketsLost;
        double expectedCorruptionOfTotal = expectedCorruptionRate * (1 - expectedLossRate);
        actualCorruptionRate.Should().BeApproximately(expectedCorruptionOfTotal, TolerancePercent / 100.0,
            $"corruption rate should be around {expectedCorruptionOfTotal:P1}");

        // Verify reordering occurred
        channel.PacketsReordered.Should().BeGreaterThan(0, "some packets should be reordered");

        // Verify result is valid
        result.Should().NotBeNull();
        result.Count.Should().BeLessThan(PacketCount, "some packets should be lost");
        result.Count.Should().BeGreaterThan(0, "not all packets should be lost");
    }

    [Fact]
    public void Boundary_Loss50Percent_ShouldHandleGracefully()
    {
        // Arrange - Extreme loss scenario
        var config = new NetworkChannelConfig
        {
            PacketLossRate = 0.50,    // 50% loss
            ReorderRate = 0.0,
            CorruptionRate = 0.0,
            Seed = 42
        };

        var channel = new NetworkChannel(config);
        var packets = CreateTestPackets(PacketCount);

        // Act
        var result = channel.TransmitPackets(packets);

        // Assert
        double actualLossRate = (double)channel.PacketsLost / PacketCount;
        actualLossRate.Should().BeApproximately(0.50, TolerancePercent / 100.0,
            "loss rate should be around 50%");

        // Verify system handles extreme loss gracefully
        result.Should().NotBeNull();
        result.Count.Should().BeGreaterThan(PacketCount / 3, "at least 33% should survive");
        result.Count.Should().BeLessThan(PacketCount * 2 / 3, "at most 67% should survive");

        // No corruption or reordering should occur
        channel.PacketsCorrupted.Should().Be(0, "no corruption should occur");
        channel.PacketsReordered.Should().Be(0, "no reordering should occur");
    }

    [Fact]
    public void Boundary_Reorder20Percent_ShouldHandleGracefully()
    {
        // Arrange - High reordering scenario
        var config = new NetworkChannelConfig
        {
            PacketLossRate = 0.0,
            ReorderRate = 0.20,       // 20% reorder
            CorruptionRate = 0.0,
            Seed = 42
        };

        var channel = new NetworkChannel(config);
        var packets = CreateTestPackets(PacketCount);

        // Act
        var result = channel.TransmitPackets(packets);

        // Assert
        // All packets should survive (no loss)
        result.Should().NotBeNull();
        result.Count.Should().Be(PacketCount, "all packets should survive without loss");

        // Verify reordering occurred
        channel.PacketsReordered.Should().BeGreaterThan((long)(PacketCount * 0.15),
            "at least 15% of packets should be reordered");

        // No corruption or loss should occur
        channel.PacketsLost.Should().Be(0, "no loss should occur");
        channel.PacketsCorrupted.Should().Be(0, "no corruption should occur");

        // Verify packets are actually reordered
        VerifyPacketReordering(result, packets).Should().BeTrue("packets should be reordered");
    }

    [Fact]
    public void Boundary_Corruption10Percent_ShouldHandleGracefully()
    {
        // Arrange - High corruption scenario
        var config = new NetworkChannelConfig
        {
            PacketLossRate = 0.0,
            ReorderRate = 0.0,
            CorruptionRate = 0.10,    // 10% corruption
            Seed = 42
        };

        var channel = new NetworkChannel(config);
        var packets = CreateTestPackets(PacketCount);

        // Act
        var result = channel.TransmitPackets(packets);

        // Assert
        double actualCorruptionRate = (double)channel.PacketsCorrupted / PacketCount;
        actualCorruptionRate.Should().BeApproximately(0.10, TolerancePercent / 100.0,
            "corruption rate should be around 10%");

        // Verify system handles high corruption gracefully
        result.Should().NotBeNull();
        result.Count.Should().Be(PacketCount, "all packets should survive");

        // No loss or reordering should occur
        channel.PacketsLost.Should().Be(0, "no loss should occur");
        channel.PacketsReordered.Should().Be(0, "no reordering should occur");

        // Verify corruption actually modified packets
        int corruptedCount = CountCorruptedPackets(result, packets);
        corruptedCount.Should().BeGreaterOrEqualTo((int)(PacketCount * 0.08),
            "at least 8% of packets should be corrupted");
    }

    [Fact]
    public void ComplexScenarios_AllImpairments_MaximumStress()
    {
        // Arrange - All impairments at moderate levels
        var config = new NetworkChannelConfig
        {
            PacketLossRate = 0.15,    // 15% loss
            ReorderRate = 0.10,       // 10% reorder
            CorruptionRate = 0.05,    // 5% corruption
            Seed = 123
        };

        var channel = new NetworkChannel(config);
        var packets = CreateTestPackets(PacketCount);

        // Act
        var result = channel.TransmitPackets(packets);

        // Assert
        double actualLossRate = (double)channel.PacketsLost / PacketCount;
        actualLossRate.Should().BeApproximately(0.15, TolerancePercent / 100.0);

        int survivingPackets = result.Count;
        survivingPackets.Should().BeLessThan(PacketCount, "some packets should be lost");
        survivingPackets.Should().BeGreaterThan(PacketCount / 2, "more than 50% should survive");

        // Verify reordering occurred
        channel.PacketsReordered.Should().BeGreaterThan(0, "some packets should be reordered");

        // Verify corruption occurred
        channel.PacketsCorrupted.Should().BeGreaterThan(0, "some packets should be corrupted");

        // Verify system is still functional
        result.Should().NotBeNull();
        result.Count.Should().BeGreaterThan(0, "system should still deliver packets");
    }

    /// <summary>
    /// Creates test packets with known data patterns.
    /// </summary>
    private List<UdpFramePacket> CreateTestPackets(int count)
    {
        var packets = new List<UdpFramePacket>(count);
        for (int i = 0; i < count; i++)
        {
            var data = new byte[256];
            // Fill with pattern based on index
            for (int j = 0; j < data.Length; j++)
            {
                data[j] = (byte)((i + j) % 256);
            }

            packets.Add(new UdpFramePacket
            {
                Data = data,
                PacketIndex = i,
                TotalPackets = count,
                Flags = 0
            });
        }
        return packets;
    }

    /// <summary>
    /// Verifies that packets have been reordered from their original sequence.
    /// </summary>
    private bool VerifyPacketReordering(List<UdpFramePacket> result, List<UdpFramePacket> original)
    {
        int outOfOrderCount = 0;
        for (int i = 0; i < Math.Min(result.Count, original.Count); i++)
        {
            if (result[i].PacketIndex != original[i].PacketIndex)
            {
                outOfOrderCount++;
            }
        }
        // At least 5% should be out of order
        return outOfOrderCount > result.Count * 0.05;
    }

    /// <summary>
    /// Counts packets that have been corrupted by comparing data.
    /// </summary>
    private int CountCorruptedPackets(List<UdpFramePacket> result, List<UdpFramePacket> original)
    {
        int corruptedCount = 0;
        for (int i = 0; i < Math.Min(result.Count, original.Count); i++)
        {
            if (!result[i].Data.SequenceEqual(original[i].Data))
            {
                corruptedCount++;
            }
        }
        return corruptedCount;
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}
