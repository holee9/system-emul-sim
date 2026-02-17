namespace FpgaSimulator.Core.Csi2;

/// <summary>
/// CSI-2 data types per MIPI CSI-2 v1.3 specification.
/// </summary>
public enum Csi2DataType : byte
{
    /// <summary>RAW16 16-bit pixel format (Data Type = 0x2E)</summary>
    Raw16 = 0x2E,

    /// <summary>RAW14 14-bit pixel format (Data Type = 0x2D)</summary>
    Raw14 = 0x2D,

    /// <summary>RGB888 24-bit RGB (Data Type = 0x24)</summary>
    Rgb888 = 0x24,

    /// <summary>YUV422 8-bit (Data Type = 0x1E)</summary>
    Yuv422_8bit = 0x1E
}
