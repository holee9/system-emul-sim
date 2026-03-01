namespace McuSimulator.Core.Sequence;

/// <summary>
/// Runtime statistics for the Sequence Engine (1:1 mapping from seq_stats_t in fw/include/sequence_engine.h).
/// </summary>
public class SequenceStatistics
{
    /// <summary>Total frames received from FPGA readout.</summary>
    public uint FramesReceived { get; set; }

    /// <summary>Total frames sent (streamed) to the host.</summary>
    public uint FramesSent { get; set; }

    /// <summary>Total error count.</summary>
    public uint Errors { get; set; }

    /// <summary>Total retry count after error-cleared events.</summary>
    public uint Retries { get; set; }

    /// <summary>Reset all counters to zero.</summary>
    public void Reset()
    {
        FramesReceived = 0;
        FramesSent = 0;
        Errors = 0;
        Retries = 0;
    }
}
