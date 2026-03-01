// Copyright (c) 2026 ABYZ Lab. All rights reserved.

namespace McuSimulator.Core.Buffer;

/// <summary>
/// Buffer state enumeration.
/// Maps from buf_state_t in fw/include/frame_manager.h.
/// </summary>
public enum BufferState
{
    /// <summary>Available for CSI-2 RX.</summary>
    Free = 0,

    /// <summary>Being filled by DMA.</summary>
    Filling = 1,

    /// <summary>Ready for TX.</summary>
    Ready = 2,

    /// <summary>Being transmitted.</summary>
    Sending = 3
}
