// Copyright (c) 2026 ABYZ Lab. All rights reserved.

namespace McuSimulator.Core.Buffer;

/// <summary>
/// Frame Manager statistics.
/// Maps from frame_stats_t in fw/include/frame_manager.h.
/// REQ-FW-111: Runtime statistics.
/// </summary>
public class FrameManagerStatistics
{
    /// <summary>Total frames received (FILLING -> READY transitions).</summary>
    public ulong FramesReceived { get; set; }

    /// <summary>Total frames sent (SENDING -> FREE transitions).</summary>
    public ulong FramesSent { get; set; }

    /// <summary>Total frames dropped via oldest-drop policy.</summary>
    public ulong FramesDropped { get; set; }

    /// <summary>Total packets sent.</summary>
    public ulong PacketsSent { get; set; }

    /// <summary>Total bytes sent.</summary>
    public ulong BytesSent { get; set; }

    /// <summary>Buffer overrun count.</summary>
    public ulong Overruns { get; set; }

    /// <summary>
    /// Creates a deep copy of the current statistics.
    /// </summary>
    public FrameManagerStatistics Clone()
    {
        return new FrameManagerStatistics
        {
            FramesReceived = FramesReceived,
            FramesSent = FramesSent,
            FramesDropped = FramesDropped,
            PacketsSent = PacketsSent,
            BytesSent = BytesSent,
            Overruns = Overruns
        };
    }
}
