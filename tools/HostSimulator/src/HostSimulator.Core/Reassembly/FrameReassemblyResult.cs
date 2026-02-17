using Common.Dto.Dtos;

namespace HostSimulator.Core.Reassembly;

/// <summary>
/// Result of a frame reassembly operation.
/// </summary>
public sealed class FrameReassemblyResult
{
    /// <summary>
    /// Gets the reassembly status.
    /// </summary>
    public FrameReassemblyStatus Status { get; init; }

    /// <summary>
    /// Gets the reassembled frame (null if incomplete or pending).
    /// </summary>
    public FrameData? Frame { get; init; }

    /// <summary>
    /// Gets the frame identifier.
    /// </summary>
    public uint FrameId { get; init; }

    /// <summary>
    /// Gets the list of missing packet indices (only populated for incomplete frames).
    /// </summary>
    public ushort[] MissingPackets { get; init; } = Array.Empty<ushort>();

    /// <summary>
    /// Creates a pending result.
    /// </summary>
    public static FrameReassemblyResult Pending(uint frameId)
    {
        return new FrameReassemblyResult
        {
            Status = FrameReassemblyStatus.Pending,
            FrameId = frameId
        };
    }

    /// <summary>
    /// Creates a complete result with frame data.
    /// </summary>
    public static FrameReassemblyResult Complete(uint frameId, FrameData frame)
    {
        return new FrameReassemblyResult
        {
            Status = FrameReassemblyStatus.Complete,
            FrameId = frameId,
            Frame = frame
        };
    }

    /// <summary>
    /// Creates an incomplete result with missing packet information.
    /// </summary>
    public static FrameReassemblyResult Incomplete(uint frameId, ushort[] missingPackets)
    {
        return new FrameReassemblyResult
        {
            Status = FrameReassemblyStatus.Incomplete,
            FrameId = frameId,
            MissingPackets = missingPackets
        };
    }

    /// <summary>
    /// Returns a string representation of the result.
    /// </summary>
    public override string ToString()
    {
        return Status switch
        {
            FrameReassemblyStatus.Pending => $"FrameReassemblyResult {{ FrameId={FrameId}, Status=Pending }}",
            FrameReassemblyStatus.Complete => $"FrameReassemblyResult {{ FrameId={FrameId}, Status=Complete }}",
            FrameReassemblyStatus.Incomplete => $"FrameReassemblyResult {{ FrameId={FrameId}, Status=Incomplete, MissingPackets=[{string.Join(", ", MissingPackets)}] }}",
            _ => $"FrameReassemblyResult {{ FrameId={FrameId}, Status={Status} }}"
        };
    }
}
