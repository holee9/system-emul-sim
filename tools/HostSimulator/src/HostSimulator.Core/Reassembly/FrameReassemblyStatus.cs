namespace HostSimulator.Core.Reassembly;

/// <summary>
/// Status of frame reassembly operation.
/// </summary>
public enum FrameReassemblyStatus
{
    /// <summary>
    /// Frame is still pending (more packets needed).
    /// </summary>
    Pending,

    /// <summary>
    /// Frame reassembly is complete.
    /// </summary>
    Complete,

    /// <summary>
    /// Frame reassembly failed due to missing packets (timeout).
    /// </summary>
    Incomplete
}
