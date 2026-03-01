// Copyright (c) 2026 ABYZ Lab. All rights reserved.

namespace McuSimulator.Core.Buffer;

/// <summary>
/// Frame buffer descriptor representing a single buffer slot.
/// Maps from frame_buffer_t in fw/include/frame_manager.h.
/// </summary>
public class FrameBufferDescriptor
{
    /// <summary>Buffer data.</summary>
    public byte[]? Data { get; set; }

    /// <summary>Buffer size in bytes.</summary>
    public int Size { get; set; }

    /// <summary>Current buffer state.</summary>
    public BufferState State { get; set; }

    /// <summary>Frame sequence number.</summary>
    public uint FrameNumber { get; set; }

    /// <summary>Total packets for transmission.</summary>
    public ushort TotalPackets { get; set; }

    /// <summary>Packets already sent.</summary>
    public ushort SentPackets { get; set; }
}
