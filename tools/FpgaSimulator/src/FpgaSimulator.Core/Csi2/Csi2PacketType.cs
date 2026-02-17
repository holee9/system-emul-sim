namespace FpgaSimulator.Core.Csi2;

/// <summary>
/// CSI-2 packet types per MIPI CSI-2 specification.
/// </summary>
public enum Csi2PacketType : byte
{
    /// <summary>Frame Start packet (Data ID = 0x00)</summary>
    FrameStart = 0x00,

    /// <summary>Frame End packet (Data ID = 0x01)</summary>
    FrameEnd = 0x01,

    /// <summary>Line Start packet (Data ID = 0x02)</summary>
    LineStart = 0x02,

    /// <summary>Line End packet (Data ID = 0x03)</summary>
    LineEnd = 0x03,

    /// <summary>Line Data with payload (Data ID = 0x2E for RAW16)</summary>
    LineData = 0x2E
}
