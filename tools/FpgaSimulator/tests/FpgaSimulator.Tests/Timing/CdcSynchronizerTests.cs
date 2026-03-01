namespace FpgaSimulator.Tests.Timing;

using FluentAssertions;
using FpgaSimulator.Core.Timing;
using Xunit;

public class CdcSynchronizerTests
{
    // ---------------------------------------------------------------
    // Constructor and property tests
    // ---------------------------------------------------------------

    [Fact]
    public void Constructor_Default2Stages_ShouldSetProperties()
    {
        // Arrange & Act
        var cdc = new CdcSynchronizer(ClockDomain.System, ClockDomain.Csi2);

        // Assert
        cdc.Stages.Should().Be(2);
        cdc.SourceDomain.Should().Be(ClockDomain.System);
        cdc.DestinationDomain.Should().Be(ClockDomain.Csi2);
    }

    [Fact]
    public void Constructor_CustomStages_ShouldSetStages()
    {
        // Arrange & Act
        var cdc = new CdcSynchronizer(ClockDomain.Roic, ClockDomain.System, stages: 4);

        // Assert
        cdc.Stages.Should().Be(4);
    }

    [Fact]
    public void Constructor_StagesLessThan2_ShouldClampToMinimum2()
    {
        // Arrange & Act
        var cdc = new CdcSynchronizer(ClockDomain.System, ClockDomain.Csi2, stages: 1);

        // Assert
        cdc.Stages.Should().Be(2);
    }

    // ---------------------------------------------------------------
    // Latency calculation tests
    // ---------------------------------------------------------------

    [Fact]
    public void LatencyCycles_2Stage_ShouldBe3()
    {
        // stages + 1 = 3
        var cdc = new CdcSynchronizer(ClockDomain.System, ClockDomain.Csi2);
        cdc.LatencyCycles.Should().Be(3);
    }

    [Fact]
    public void LatencyCycles_4Stage_ShouldBe5()
    {
        var cdc = new CdcSynchronizer(ClockDomain.System, ClockDomain.Csi2, stages: 4);
        cdc.LatencyCycles.Should().Be(5);
    }

    [Fact]
    public void LatencyNs_2Stage_SysToCsi2_ShouldBeCorrect()
    {
        // 3 cycles * 8 ns (CSI2 125 MHz) = 24 ns
        var cdc = new CdcSynchronizer(ClockDomain.System, ClockDomain.Csi2);
        cdc.LatencyNs.Should().BeApproximately(24.0, 0.001);
    }

    [Fact]
    public void LatencyNs_2Stage_SysToRoic_ShouldBeCorrect()
    {
        // 3 cycles * 20 ns (ROIC 50 MHz) = 60 ns
        var cdc = new CdcSynchronizer(ClockDomain.System, ClockDomain.Roic);
        cdc.LatencyNs.Should().BeApproximately(60.0, 0.001);
    }

    // ---------------------------------------------------------------
    // Pipeline behavior: PushInput / GetOutput
    // ---------------------------------------------------------------

    [Fact]
    public void GetOutput_InitialState_ShouldReturnFalse()
    {
        var cdc = new CdcSynchronizer(ClockDomain.System, ClockDomain.Csi2);
        cdc.GetOutput().Should().BeFalse();
    }

    [Fact]
    public void PushInput_SingleTrue_OutputShouldNotAppearImmediately()
    {
        // Arrange
        var cdc = new CdcSynchronizer(ClockDomain.System, ClockDomain.Csi2);

        // Act - push one true value
        cdc.PushInput(true);

        // Assert - output is the oldest value (still false at write position 1)
        // After one push the write index has advanced, output reads position 1
        // which was initialized to false
        cdc.GetOutput().Should().BeFalse();
    }

    [Fact]
    public void PushInput_FillPipeline_OutputShouldPropagateAfterNStages()
    {
        // Arrange - 2-stage synchronizer
        var cdc = new CdcSynchronizer(ClockDomain.System, ClockDomain.Csi2);

        // Act - fill pipeline with true values
        cdc.PushInput(true);  // stage 0 = true, write index -> 1
        cdc.PushInput(true);  // stage 1 = true, write index -> 0

        // Assert - now output reads stage 0 which is true
        cdc.GetOutput().Should().BeTrue();
    }

    // ---------------------------------------------------------------
    // Clock method tests (combined push + read)
    // ---------------------------------------------------------------

    [Fact]
    public void Clock_InitialCycles_ShouldReturnFalseUntilPropagated()
    {
        // Arrange - 2-stage synchronizer
        var cdc = new CdcSynchronizer(ClockDomain.System, ClockDomain.Csi2);

        // Act & Assert - first clock should output false (initial pipeline value)
        var output1 = cdc.Clock(true);
        output1.Should().BeFalse();

        // Second clock: pipeline[1] was false initially
        var output2 = cdc.Clock(true);
        output2.Should().BeFalse();
    }

    [Fact]
    public void Clock_AfterFullPropagation_ShouldOutputInputValue()
    {
        // Arrange - 2-stage synchronizer
        var cdc = new CdcSynchronizer(ClockDomain.System, ClockDomain.Csi2);

        // Act - clock through the pipeline
        cdc.Clock(true);  // Output: false (initial), pipeline[0]=true
        cdc.Clock(true);  // Output: false (initial), pipeline[1]=true

        // After 2 clocks (= stages), the true value reaches output
        var output = cdc.Clock(true);
        output.Should().BeTrue();
    }

    [Fact]
    public void Clock_4Stage_ShouldRequire4CyclesForPropagation()
    {
        // Arrange
        var cdc = new CdcSynchronizer(ClockDomain.System, ClockDomain.Csi2, stages: 4);

        // Act - push true through 4-stage pipeline
        for (int i = 0; i < 4; i++)
        {
            var output = cdc.Clock(true);
            output.Should().BeFalse($"cycle {i} should still output false (pipeline not full)");
        }

        // After 4 cycles the first true arrives at output
        var propagated = cdc.Clock(true);
        propagated.Should().BeTrue();
    }

    [Fact]
    public void Clock_FalseAfterTrue_ShouldPropagateCorrectly()
    {
        // Arrange - 2-stage synchronizer
        var cdc = new CdcSynchronizer(ClockDomain.System, ClockDomain.Csi2);

        // Fill pipeline with true
        cdc.Clock(true);
        cdc.Clock(true);

        // Now send false - should take 2 cycles to propagate
        cdc.Clock(false); // output: true (old value)
        cdc.Clock(false); // output: true (old value)

        var output = cdc.Clock(false);
        output.Should().BeFalse("false should have propagated through 2-stage pipeline");
    }

    // ---------------------------------------------------------------
    // Reset tests
    // ---------------------------------------------------------------

    [Fact]
    public void Reset_ShouldClearPipelineToFalse()
    {
        // Arrange
        var cdc = new CdcSynchronizer(ClockDomain.System, ClockDomain.Csi2);
        cdc.PushInput(true);
        cdc.PushInput(true);

        // Act
        cdc.Reset();

        // Assert
        cdc.GetOutput().Should().BeFalse();
    }

    [Fact]
    public void Reset_ShouldAllowNewDataPropagation()
    {
        // Arrange
        var cdc = new CdcSynchronizer(ClockDomain.System, ClockDomain.Csi2);
        cdc.PushInput(true);
        cdc.PushInput(true);

        // Act
        cdc.Reset();

        // Verify pipeline restarts cleanly
        cdc.Clock(true).Should().BeFalse();
        cdc.Clock(true).Should().BeFalse();
        cdc.Clock(true).Should().BeTrue();
    }

    // ---------------------------------------------------------------
    // Thread safety tests
    // ---------------------------------------------------------------

    [Fact]
    public async Task ConcurrentAccess_ShouldNotThrow()
    {
        // Arrange
        var cdc = new CdcSynchronizer(ClockDomain.System, ClockDomain.Csi2);
        var exceptions = new List<Exception>();

        // Act - concurrent push/read from multiple threads
        var tasks = new Task[10];
        for (int t = 0; t < tasks.Length; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < 1000; i++)
                    {
                        cdc.Clock(i % 2 == 0);
                        cdc.GetOutput();
                        cdc.PushInput(true);
                    }
                }
                catch (Exception ex)
                {
                    lock (exceptions) { exceptions.Add(ex); }
                }
            });
        }

        await Task.WhenAll(tasks);

        // Assert
        exceptions.Should().BeEmpty("concurrent access should not cause exceptions");
    }
}
