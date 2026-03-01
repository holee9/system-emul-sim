namespace FpgaSimulator.Tests.Csi2;

using FluentAssertions;
using FpgaSimulator.Core.Csi2;
using Xunit;

public class Csi2BackpressureModelTests
{
    // ---------------------------------------------------------------
    // Constructor and initial state tests
    // ---------------------------------------------------------------

    [Fact]
    public void Constructor_Default_ShouldInitialize256ByteFifo()
    {
        // Arrange & Act
        var bp = new Csi2BackpressureModel();

        // Assert
        bp.FifoDepth.Should().Be(256);
        bp.FifoLevel.Should().Be(0);
        bp.TReady.Should().BeTrue("empty FIFO should assert tready");
        bp.TValid.Should().BeFalse("no data presented initially");
        bp.StallCycles.Should().Be(0);
        bp.TotalBytesTransferred.Should().Be(0);
        bp.TotalStallCycles.Should().Be(0);
    }

    [Fact]
    public void Constructor_CustomDepth_ShouldSetFifoDepth()
    {
        // Arrange & Act
        var bp = new Csi2BackpressureModel(fifoDepth: 512);

        // Assert
        bp.FifoDepth.Should().Be(512);
    }

    [Fact]
    public void Constructor_ZeroDepth_ShouldClampToMinimum1()
    {
        // Arrange & Act
        var bp = new Csi2BackpressureModel(fifoDepth: 0);

        // Assert
        bp.FifoDepth.Should().Be(1);
    }

    // ---------------------------------------------------------------
    // TReady / TValid flow control tests
    // ---------------------------------------------------------------

    [Fact]
    public void TReady_WhenFifoEmpty_ShouldBeTrue()
    {
        var bp = new Csi2BackpressureModel();
        bp.TReady.Should().BeTrue();
    }

    [Fact]
    public void TReady_WhenFifoFull_ShouldBeFalse()
    {
        // Arrange
        var bp = new Csi2BackpressureModel(fifoDepth: 8);
        bp.AssertValid();

        // Act - fill FIFO (8 bytes at 4 bytes per beat = 2 beats)
        bp.ProcessCycle(bytesPerBeat: 4);
        bp.ProcessCycle(bytesPerBeat: 4);

        // Assert
        bp.TReady.Should().BeFalse();
        bp.FifoLevel.Should().Be(8);
    }

    [Fact]
    public void TransferActive_WhenBothTReadyAndTValid_ShouldBeTrue()
    {
        // Arrange
        var bp = new Csi2BackpressureModel();
        bp.AssertValid();

        // Assert - FIFO empty + tvalid = transfer active
        bp.TransferActive.Should().BeTrue();
    }

    [Fact]
    public void TransferActive_WhenTValidFalse_ShouldBeFalse()
    {
        // Arrange
        var bp = new Csi2BackpressureModel();

        // Assert - no tvalid = no transfer
        bp.TransferActive.Should().BeFalse();
    }

    [Fact]
    public void AssertValid_ShouldSetTValidTrue()
    {
        // Arrange
        var bp = new Csi2BackpressureModel();

        // Act
        bp.AssertValid();

        // Assert
        bp.TValid.Should().BeTrue();
    }

    [Fact]
    public void DeassertValid_ShouldClearTValidAndStallCycles()
    {
        // Arrange
        var bp = new Csi2BackpressureModel(fifoDepth: 4);
        bp.AssertValid();

        // Fill FIFO to cause stall
        bp.ProcessCycle(bytesPerBeat: 4);
        bp.ProcessCycle(bytesPerBeat: 4); // Stall on second cycle (FIFO full)

        // Act
        bp.DeassertValid();

        // Assert
        bp.TValid.Should().BeFalse();
        bp.StallCycles.Should().Be(0);
    }

    // ---------------------------------------------------------------
    // ProcessCycle transfer tests
    // ---------------------------------------------------------------

    [Fact]
    public void ProcessCycle_ValidTransfer_ShouldReturnTrueAndUpdateCounters()
    {
        // Arrange
        var bp = new Csi2BackpressureModel();
        bp.AssertValid();

        // Act
        var transferred = bp.ProcessCycle(bytesPerBeat: 4);

        // Assert
        transferred.Should().BeTrue();
        bp.FifoLevel.Should().Be(4);
        bp.TotalBytesTransferred.Should().Be(4);
    }

    [Fact]
    public void ProcessCycle_NoTValid_ShouldReturnFalse()
    {
        // Arrange
        var bp = new Csi2BackpressureModel();

        // Act - no tvalid asserted
        var transferred = bp.ProcessCycle();

        // Assert
        transferred.Should().BeFalse();
        bp.FifoLevel.Should().Be(0);
    }

    [Fact]
    public void ProcessCycle_MultipleTransfers_ShouldAccumulateBytes()
    {
        // Arrange
        var bp = new Csi2BackpressureModel(fifoDepth: 256);
        bp.AssertValid();

        // Act
        for (int i = 0; i < 10; i++)
        {
            bp.ProcessCycle(bytesPerBeat: 4);
        }

        // Assert
        bp.FifoLevel.Should().Be(40);
        bp.TotalBytesTransferred.Should().Be(40);
    }

    // ---------------------------------------------------------------
    // FIFO overflow / backpressure tests
    // ---------------------------------------------------------------

    [Fact]
    public void FifoOverflow_ShouldCapAtDepth()
    {
        // Arrange - small FIFO for easy testing
        var bp = new Csi2BackpressureModel(fifoDepth: 8);
        bp.AssertValid();

        // Act - try to push more than FIFO depth
        bp.ProcessCycle(bytesPerBeat: 4); // level: 4
        bp.ProcessCycle(bytesPerBeat: 4); // level: 8 (capped at depth)

        // Assert
        bp.FifoLevel.Should().Be(8);
        bp.TReady.Should().BeFalse("FIFO is full");
    }

    [Fact]
    public void Stall_WhenFifoFullAndTValid_ShouldCountStallCycles()
    {
        // Arrange
        var bp = new Csi2BackpressureModel(fifoDepth: 4);
        bp.AssertValid();

        // Fill FIFO
        bp.ProcessCycle(bytesPerBeat: 4); // level: 4 (full)

        // Act - try to transfer when full
        var result1 = bp.ProcessCycle(bytesPerBeat: 4);
        var result2 = bp.ProcessCycle(bytesPerBeat: 4);
        var result3 = bp.ProcessCycle(bytesPerBeat: 4);

        // Assert
        result1.Should().BeFalse();
        result2.Should().BeFalse();
        result3.Should().BeFalse();
        bp.StallCycles.Should().Be(3);
        bp.TotalStallCycles.Should().Be(3);
    }

    [Fact]
    public void StallCycles_ShouldResetOnSuccessfulTransfer()
    {
        // Arrange
        var bp = new Csi2BackpressureModel(fifoDepth: 4);
        bp.AssertValid();

        // Fill FIFO and stall
        bp.ProcessCycle(bytesPerBeat: 4);
        bp.ProcessCycle(bytesPerBeat: 4); // stall
        bp.ProcessCycle(bytesPerBeat: 4); // stall

        // Drain to allow new transfer
        bp.DrainFifo(4);
        bp.ProcessCycle(bytesPerBeat: 4); // successful transfer

        // Assert
        bp.StallCycles.Should().Be(0, "stall counter resets on successful transfer");
    }

    // ---------------------------------------------------------------
    // DrainFifo consumption tests
    // ---------------------------------------------------------------

    [Fact]
    public void DrainFifo_ShouldReduceFifoLevel()
    {
        // Arrange
        var bp = new Csi2BackpressureModel();
        bp.AssertValid();
        bp.ProcessCycle(bytesPerBeat: 4); // level: 4
        bp.ProcessCycle(bytesPerBeat: 4); // level: 8

        // Act
        bp.DrainFifo(3);

        // Assert
        bp.FifoLevel.Should().Be(5);
    }

    [Fact]
    public void DrainFifo_ExceedingLevel_ShouldClampToZero()
    {
        // Arrange
        var bp = new Csi2BackpressureModel();
        bp.AssertValid();
        bp.ProcessCycle(bytesPerBeat: 4); // level: 4

        // Act - drain more than available
        bp.DrainFifo(100);

        // Assert
        bp.FifoLevel.Should().Be(0);
    }

    [Fact]
    public void DrainFifo_ShouldRestoreTReady()
    {
        // Arrange
        var bp = new Csi2BackpressureModel(fifoDepth: 8);
        bp.AssertValid();
        bp.ProcessCycle(bytesPerBeat: 4);
        bp.ProcessCycle(bytesPerBeat: 4); // full

        bp.TReady.Should().BeFalse();

        // Act
        bp.DrainFifo(1);

        // Assert
        bp.TReady.Should().BeTrue("draining should restore tready");
    }

    // ---------------------------------------------------------------
    // Stall cycle tracking tests
    // ---------------------------------------------------------------

    [Fact]
    public void TotalStallCycles_ShouldAccumulateAcrossMultipleStallPeriods()
    {
        // Arrange
        var bp = new Csi2BackpressureModel(fifoDepth: 4);
        bp.AssertValid();

        // First stall period
        bp.ProcessCycle(bytesPerBeat: 4); // fill
        bp.ProcessCycle(); // stall 1
        bp.ProcessCycle(); // stall 2

        // Drain and transfer
        bp.DrainFifo(4);
        bp.ProcessCycle(bytesPerBeat: 4); // fill again

        // Second stall period
        bp.ProcessCycle(); // stall 3

        // Assert
        bp.TotalStallCycles.Should().Be(3);
    }

    // ---------------------------------------------------------------
    // Reset tests
    // ---------------------------------------------------------------

    [Fact]
    public void Reset_ShouldClearAllState()
    {
        // Arrange
        var bp = new Csi2BackpressureModel();
        bp.AssertValid();
        bp.ProcessCycle(bytesPerBeat: 4);

        // Act
        bp.Reset();

        // Assert
        bp.FifoLevel.Should().Be(0);
        bp.StallCycles.Should().Be(0);
        bp.TotalBytesTransferred.Should().Be(0);
        bp.TotalStallCycles.Should().Be(0);
        bp.TValid.Should().BeFalse();
        bp.TReady.Should().BeTrue();
    }
}
