// Copyright (c) 2026 ABYZ Lab. All rights reserved.

namespace McuSimulator.Core.Buffer;

/// <summary>
/// Frame Manager configuration.
/// Maps from frame_mgr_config_t in fw/include/frame_manager.h.
/// Default values match FRAME_MGR_DEFAULT_* defines.
/// </summary>
public class FrameManagerConfig
{
    /// <summary>Frame rows (height). Default: 2048.</summary>
    public ushort Rows { get; set; } = 2048;

    /// <summary>Frame columns (width). Default: 2048.</summary>
    public ushort Cols { get; set; } = 2048;

    /// <summary>Bits per pixel. Default: 16.</summary>
    public byte BitDepth { get; set; } = 16;

    /// <summary>
    /// Total frame size in bytes.
    /// Calculated as Rows * Cols * (BitDepth / 8) if not explicitly set.
    /// </summary>
    public int FrameSize => Rows * Cols * (BitDepth / 8);

    /// <summary>Number of buffers (fixed at 4 per REQ-FW-050). Default: 4.</summary>
    public int NumBuffers { get; set; } = 4;
}
