namespace XrayDetector.Core.Reassembly;

/// <summary>Frame reassembly status.</summary>
public enum ReassemblyStatus
{
    /// <summary>Frame is still being assembled (more packets expected).</summary>
    Processing,

    /// <summary>Frame is complete with all packets received.</summary>
    Complete,

    /// <summary>Frame is incomplete (timeout or missing packets, zero-filled).</summary>
    Partial,

    /// <summary>CRC validation failed.</summary>
    CrcError,

    /// <summary>Generic error occurred.</summary>
    Error
}

/// <summary>Frame reassembly result.</summary>
public record FrameReassemblyResult(
    ReassemblyStatus Status,
    uint FrameNumber,
    ushort[]? FrameData,
    string? ErrorMessage
);

/// <summary>Frame assembly status information.</summary>
public record FrameStatus(
    uint FrameNumber,
    uint TotalPackets,
    uint ReceivedPackets,
    bool IsComplete,
    TimeSpan Age
);
