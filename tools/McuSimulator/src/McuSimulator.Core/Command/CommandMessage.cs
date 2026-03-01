namespace McuSimulator.Core.Command;

/// <summary>
/// Represents a command message exchanged between Host and MCU.
/// Wire format: [Magic(4)] [Sequence(4)] [CommandId(2)] [PayloadLength(2)] [HMAC(32)] [Payload(N)]
/// </summary>
public sealed record CommandMessage
{
    /// <summary>Magic value for command messages (Host to MCU).</summary>
    public const uint MagicCommand = 0xBEEFCAFE;

    /// <summary>Magic value for response messages (MCU to Host).</summary>
    public const uint MagicResponse = 0xCAFEBEEF;

    /// <summary>HMAC-SHA256 digest size in bytes.</summary>
    public const int HmacSize = 32;

    /// <summary>Protocol magic number identifying the message direction.</summary>
    public required uint Magic { get; init; }

    /// <summary>Monotonically increasing sequence number for anti-replay.</summary>
    public required uint Sequence { get; init; }

    /// <summary>Command identifier.</summary>
    public required CommandType CommandId { get; init; }

    /// <summary>Length of the payload in bytes.</summary>
    public required ushort PayloadLength { get; init; }

    /// <summary>HMAC-SHA256 authentication tag computed over header + payload.</summary>
    public required byte[] Hmac { get; init; }

    /// <summary>Variable-length command payload.</summary>
    public required byte[] Payload { get; init; }
}
