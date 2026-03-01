using System.Buffers.Binary;
using System.Security.Cryptography;

namespace McuSimulator.Core.Command;

/// <summary>
/// Command protocol handler implementing authentication, anti-replay, and dispatch.
/// Mirrors fw/src/protocol/command_protocol.c validation logic.
/// </summary>
public sealed class CommandProtocol
{
    /// <summary>Status code: command executed successfully.</summary>
    public const ushort StatusOk = 0x0000;

    /// <summary>Status code: invalid magic number.</summary>
    public const ushort StatusInvalidCmd = 0x0003;

    /// <summary>Status code: HMAC verification failed.</summary>
    public const ushort StatusAuthFailed = 0x0004;

    /// <summary>Status code: sequence number replay detected.</summary>
    public const ushort StatusReplay = 0x0005;

    private readonly byte[] _hmacKey;
    private uint _lastSequence;

    /// <summary>
    /// Gets the cumulative count of authentication failures (bad magic or bad HMAC).
    /// </summary>
    public uint AuthFailures { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandProtocol"/> class.
    /// </summary>
    /// <param name="hmacKey">Shared secret for HMAC-SHA256 authentication.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="hmacKey"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="hmacKey"/> is empty.</exception>
    public CommandProtocol(string hmacKey)
    {
        ArgumentNullException.ThrowIfNull(hmacKey);
        if (hmacKey.Length == 0)
            throw new ArgumentException("HMAC key must not be empty.", nameof(hmacKey));

        _hmacKey = System.Text.Encoding.UTF8.GetBytes(hmacKey);
    }

    /// <summary>
    /// Validates a command message and dispatches it if all checks pass.
    /// Validation order: magic, HMAC, anti-replay.
    /// </summary>
    /// <param name="msg">The command message to validate.</param>
    /// <returns>
    /// A tuple of (success, statusCode) where success indicates whether the command
    /// was dispatched, and statusCode contains the protocol status.
    /// </returns>
    public (bool Success, ushort StatusCode) ValidateAndDispatch(CommandMessage msg)
    {
        // 1. Validate magic
        if (msg.Magic != CommandMessage.MagicCommand)
        {
            AuthFailures++;
            return (false, StatusInvalidCmd);
        }

        // 2. Validate HMAC-SHA256
        var expectedHmac = ComputeHmac(msg, _hmacKey);
        if (!CryptographicOperations.FixedTimeEquals(expectedHmac, msg.Hmac))
        {
            AuthFailures++;
            return (false, StatusAuthFailed);
        }

        // 3. Anti-replay: sequence must be strictly increasing
        if (msg.Sequence <= _lastSequence && _lastSequence != 0)
        {
            return (false, StatusReplay);
        }

        _lastSequence = msg.Sequence;

        // 4. Dispatch (actual handling is done by McuTopSimulator)
        return (true, StatusOk);
    }

    /// <summary>
    /// Computes HMAC-SHA256 over the canonical message fields for signing or verification.
    /// Fields covered: Magic(4) + Sequence(4) + CommandId(2) + PayloadLength(2) + Payload(N).
    /// </summary>
    /// <param name="msg">The command message to sign.</param>
    /// <param name="key">HMAC key bytes.</param>
    /// <returns>32-byte HMAC-SHA256 digest.</returns>
    public static byte[] ComputeHmac(CommandMessage msg, byte[] key)
    {
        // Build canonical byte sequence: magic + sequence + commandId + payloadLength + payload
        int headerSize = 4 + 4 + 2 + 2; // 12 bytes
        int totalSize = headerSize + (msg.Payload?.Length ?? 0);
        var data = new byte[totalSize];

        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0, 4), msg.Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4, 4), msg.Sequence);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(8, 2), (ushort)msg.CommandId);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(10, 2), msg.PayloadLength);

        if (msg.Payload is { Length: > 0 })
        {
            msg.Payload.CopyTo(data.AsSpan(headerSize));
        }

        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(data);
    }
}
