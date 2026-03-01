using McuSimulator.Core.Command;
using Xunit;

namespace McuSimulator.Tests.Command;

/// <summary>
/// Tests for CommandProtocol authentication, anti-replay, and dispatch logic.
/// Follows TDD: RED-GREEN-REFACTOR cycle.
/// REQ-SIM-009: Command protocol HMAC-SHA256 authentication and anti-replay.
/// </summary>
public sealed class CommandProtocolTests
{
    private const string TestHmacKey = "test-secret-key-for-hmac";

    #region Constructor Tests

    [Fact]
    public void Constructor_NullKey_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CommandProtocol(null!));
    }

    [Fact]
    public void Constructor_EmptyKey_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => new CommandProtocol(string.Empty));
        Assert.Contains("HMAC key must not be empty", ex.Message);
    }

    [Fact]
    public void Constructor_ValidKey_CreatesInstance()
    {
        // Act
        var protocol = new CommandProtocol(TestHmacKey);

        // Assert
        Assert.NotNull(protocol);
        Assert.Equal(0u, protocol.AuthFailures);
    }

    #endregion

    #region ValidateAndDispatch - Magic Validation

    [Fact]
    public void ValidateAndDispatch_BadMagic_ReturnsInvalidCmd()
    {
        // Arrange
        var protocol = new CommandProtocol(TestHmacKey);
        var msg = new CommandMessage
        {
            Magic = 0xDEADBEEF, // Wrong magic
            Sequence = 1,
            CommandId = CommandType.GetStatus,
            PayloadLength = 0,
            Hmac = new byte[CommandMessage.HmacSize],
            Payload = Array.Empty<byte>()
        };

        // Act
        var (success, statusCode) = protocol.ValidateAndDispatch(msg);

        // Assert
        Assert.False(success);
        Assert.Equal(CommandProtocol.StatusInvalidCmd, statusCode);
    }

    [Fact]
    public void ValidateAndDispatch_BadMagic_IncrementsAuthFailures()
    {
        // Arrange
        var protocol = new CommandProtocol(TestHmacKey);
        var msg = new CommandMessage
        {
            Magic = 0x00000000,
            Sequence = 1,
            CommandId = CommandType.GetStatus,
            PayloadLength = 0,
            Hmac = new byte[CommandMessage.HmacSize],
            Payload = Array.Empty<byte>()
        };

        // Act
        protocol.ValidateAndDispatch(msg);

        // Assert
        Assert.Equal(1u, protocol.AuthFailures);
    }

    #endregion

    #region ValidateAndDispatch - HMAC Validation

    [Fact]
    public void ValidateAndDispatch_CorrectMagic_BadHmac_ReturnsAuthFailed()
    {
        // Arrange
        var protocol = new CommandProtocol(TestHmacKey);
        var msg = new CommandMessage
        {
            Magic = CommandMessage.MagicCommand,
            Sequence = 1,
            CommandId = CommandType.GetStatus,
            PayloadLength = 0,
            Hmac = new byte[CommandMessage.HmacSize], // All zeros = wrong HMAC
            Payload = Array.Empty<byte>()
        };

        // Act
        var (success, statusCode) = protocol.ValidateAndDispatch(msg);

        // Assert
        Assert.False(success);
        Assert.Equal(CommandProtocol.StatusAuthFailed, statusCode);
    }

    [Fact]
    public void ValidateAndDispatch_BadHmac_IncrementsAuthFailures()
    {
        // Arrange
        var protocol = new CommandProtocol(TestHmacKey);
        var msg = new CommandMessage
        {
            Magic = CommandMessage.MagicCommand,
            Sequence = 1,
            CommandId = CommandType.GetStatus,
            PayloadLength = 0,
            Hmac = new byte[CommandMessage.HmacSize],
            Payload = Array.Empty<byte>()
        };

        // Act
        protocol.ValidateAndDispatch(msg);

        // Assert
        Assert.Equal(1u, protocol.AuthFailures);
    }

    #endregion

    #region ValidateAndDispatch - Anti-Replay

    [Fact]
    public void ValidateAndDispatch_FirstMessageSequenceZero_Succeeds()
    {
        // Arrange
        var protocol = new CommandProtocol(TestHmacKey);
        var msg = CreateSignedMessage(TestHmacKey, sequence: 0, CommandType.GetStatus);

        // Act
        var (success, statusCode) = protocol.ValidateAndDispatch(msg);

        // Assert
        Assert.True(success);
        Assert.Equal(CommandProtocol.StatusOk, statusCode);
    }

    [Fact]
    public void ValidateAndDispatch_ReplayedSequence_ReturnsReplay()
    {
        // Arrange
        var protocol = new CommandProtocol(TestHmacKey);

        // First message with sequence 5
        var msg1 = CreateSignedMessage(TestHmacKey, sequence: 5, CommandType.GetStatus);
        protocol.ValidateAndDispatch(msg1);

        // Replay with same sequence
        var msg2 = CreateSignedMessage(TestHmacKey, sequence: 5, CommandType.GetStatus);

        // Act
        var (success, statusCode) = protocol.ValidateAndDispatch(msg2);

        // Assert
        Assert.False(success);
        Assert.Equal(CommandProtocol.StatusReplay, statusCode);
    }

    [Fact]
    public void ValidateAndDispatch_LowerSequence_ReturnsReplay()
    {
        // Arrange
        var protocol = new CommandProtocol(TestHmacKey);

        // First message with sequence 10
        var msg1 = CreateSignedMessage(TestHmacKey, sequence: 10, CommandType.GetStatus);
        protocol.ValidateAndDispatch(msg1);

        // Lower sequence
        var msg2 = CreateSignedMessage(TestHmacKey, sequence: 3, CommandType.GetStatus);

        // Act
        var (success, statusCode) = protocol.ValidateAndDispatch(msg2);

        // Assert
        Assert.False(success);
        Assert.Equal(CommandProtocol.StatusReplay, statusCode);
    }

    [Fact]
    public void ValidateAndDispatch_IncreasingSequence_Succeeds()
    {
        // Arrange
        var protocol = new CommandProtocol(TestHmacKey);

        var msg1 = CreateSignedMessage(TestHmacKey, sequence: 1, CommandType.GetStatus);
        var msg2 = CreateSignedMessage(TestHmacKey, sequence: 2, CommandType.GetStatus);
        var msg3 = CreateSignedMessage(TestHmacKey, sequence: 100, CommandType.GetStatus);

        // Act & Assert
        var r1 = protocol.ValidateAndDispatch(msg1);
        Assert.True(r1.Success);

        var r2 = protocol.ValidateAndDispatch(msg2);
        Assert.True(r2.Success);

        var r3 = protocol.ValidateAndDispatch(msg3);
        Assert.True(r3.Success);
    }

    #endregion

    #region ValidateAndDispatch - Success

    [Fact]
    public void ValidateAndDispatch_ValidMessage_ReturnsSuccess()
    {
        // Arrange
        var protocol = new CommandProtocol(TestHmacKey);
        var msg = CreateSignedMessage(TestHmacKey, sequence: 1, CommandType.StartScan);

        // Act
        var (success, statusCode) = protocol.ValidateAndDispatch(msg);

        // Assert
        Assert.True(success);
        Assert.Equal(CommandProtocol.StatusOk, statusCode);
    }

    [Fact]
    public void ValidateAndDispatch_ValidMessageWithPayload_ReturnsSuccess()
    {
        // Arrange
        var protocol = new CommandProtocol(TestHmacKey);
        var payload = new byte[] { 0x01, 0x02, 0x03 };
        var msg = CreateSignedMessage(TestHmacKey, sequence: 1, CommandType.SetConfig, payload);

        // Act
        var (success, statusCode) = protocol.ValidateAndDispatch(msg);

        // Assert
        Assert.True(success);
        Assert.Equal(CommandProtocol.StatusOk, statusCode);
    }

    #endregion

    #region ComputeHmac

    [Fact]
    public void ComputeHmac_Deterministic_SameInputSameOutput()
    {
        // Arrange
        var key = System.Text.Encoding.UTF8.GetBytes(TestHmacKey);
        var msg = new CommandMessage
        {
            Magic = CommandMessage.MagicCommand,
            Sequence = 42,
            CommandId = CommandType.GetStatus,
            PayloadLength = 0,
            Hmac = Array.Empty<byte>(),
            Payload = Array.Empty<byte>()
        };

        // Act
        var hmac1 = CommandProtocol.ComputeHmac(msg, key);
        var hmac2 = CommandProtocol.ComputeHmac(msg, key);

        // Assert
        Assert.Equal(hmac1, hmac2);
    }

    [Fact]
    public void ComputeHmac_DifferentSequence_DifferentOutput()
    {
        // Arrange
        var key = System.Text.Encoding.UTF8.GetBytes(TestHmacKey);
        var msg1 = new CommandMessage
        {
            Magic = CommandMessage.MagicCommand,
            Sequence = 1,
            CommandId = CommandType.GetStatus,
            PayloadLength = 0,
            Hmac = Array.Empty<byte>(),
            Payload = Array.Empty<byte>()
        };
        var msg2 = msg1 with { Sequence = 2 };

        // Act
        var hmac1 = CommandProtocol.ComputeHmac(msg1, key);
        var hmac2 = CommandProtocol.ComputeHmac(msg2, key);

        // Assert
        Assert.NotEqual(hmac1, hmac2);
    }

    [Fact]
    public void ComputeHmac_DifferentKey_DifferentOutput()
    {
        // Arrange
        var key1 = System.Text.Encoding.UTF8.GetBytes("key-one");
        var key2 = System.Text.Encoding.UTF8.GetBytes("key-two");
        var msg = new CommandMessage
        {
            Magic = CommandMessage.MagicCommand,
            Sequence = 1,
            CommandId = CommandType.GetStatus,
            PayloadLength = 0,
            Hmac = Array.Empty<byte>(),
            Payload = Array.Empty<byte>()
        };

        // Act
        var hmac1 = CommandProtocol.ComputeHmac(msg, key1);
        var hmac2 = CommandProtocol.ComputeHmac(msg, key2);

        // Assert
        Assert.NotEqual(hmac1, hmac2);
    }

    [Fact]
    public void ComputeHmac_Returns32Bytes()
    {
        // Arrange
        var key = System.Text.Encoding.UTF8.GetBytes(TestHmacKey);
        var msg = new CommandMessage
        {
            Magic = CommandMessage.MagicCommand,
            Sequence = 1,
            CommandId = CommandType.GetStatus,
            PayloadLength = 0,
            Hmac = Array.Empty<byte>(),
            Payload = Array.Empty<byte>()
        };

        // Act
        var hmac = CommandProtocol.ComputeHmac(msg, key);

        // Assert
        Assert.Equal(32, hmac.Length);
    }

    #endregion

    #region AuthFailures Counter

    [Fact]
    public void AuthFailures_CumulativeAcrossMultipleFailures()
    {
        // Arrange
        var protocol = new CommandProtocol(TestHmacKey);
        var badMagicMsg = new CommandMessage
        {
            Magic = 0xDEADBEEF,
            Sequence = 1,
            CommandId = CommandType.GetStatus,
            PayloadLength = 0,
            Hmac = new byte[CommandMessage.HmacSize],
            Payload = Array.Empty<byte>()
        };
        var badHmacMsg = new CommandMessage
        {
            Magic = CommandMessage.MagicCommand,
            Sequence = 1,
            CommandId = CommandType.GetStatus,
            PayloadLength = 0,
            Hmac = new byte[CommandMessage.HmacSize], // Wrong HMAC
            Payload = Array.Empty<byte>()
        };

        // Act
        protocol.ValidateAndDispatch(badMagicMsg);  // +1
        protocol.ValidateAndDispatch(badMagicMsg);  // +1
        protocol.ValidateAndDispatch(badHmacMsg);   // +1

        // Assert
        Assert.Equal(3u, protocol.AuthFailures);
    }

    [Fact]
    public void AuthFailures_NotIncrementedOnReplay()
    {
        // Arrange
        var protocol = new CommandProtocol(TestHmacKey);

        // Valid message first
        var msg1 = CreateSignedMessage(TestHmacKey, sequence: 5, CommandType.GetStatus);
        protocol.ValidateAndDispatch(msg1);

        // Replay (valid auth, but replayed sequence)
        var msg2 = CreateSignedMessage(TestHmacKey, sequence: 3, CommandType.GetStatus);
        protocol.ValidateAndDispatch(msg2);

        // Assert - replay does NOT increment AuthFailures
        Assert.Equal(0u, protocol.AuthFailures);
    }

    #endregion

    #region End-to-End

    [Fact]
    public void EndToEnd_SignedMessage_PassesFullValidation()
    {
        // Arrange
        var protocol = new CommandProtocol(TestHmacKey);
        var payload = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };
        var msg = CreateSignedMessage(TestHmacKey, sequence: 1, CommandType.SetConfig, payload);

        // Act
        var (success, statusCode) = protocol.ValidateAndDispatch(msg);

        // Assert
        Assert.True(success);
        Assert.Equal(CommandProtocol.StatusOk, statusCode);
        Assert.Equal(0u, protocol.AuthFailures);
    }

    [Fact]
    public void EndToEnd_MultipleSignedMessages_AllPass()
    {
        // Arrange
        var protocol = new CommandProtocol(TestHmacKey);

        // Act & Assert
        for (uint seq = 1; seq <= 10; seq++)
        {
            var msg = CreateSignedMessage(TestHmacKey, seq, CommandType.GetStatus);
            var (success, statusCode) = protocol.ValidateAndDispatch(msg);
            Assert.True(success, $"Message with sequence {seq} should pass");
            Assert.Equal(CommandProtocol.StatusOk, statusCode);
        }

        Assert.Equal(0u, protocol.AuthFailures);
    }

    #endregion

    #region Helpers

    private static CommandMessage CreateSignedMessage(
        string hmacKey,
        uint sequence,
        CommandType cmdId,
        byte[]? payload = null)
    {
        payload ??= Array.Empty<byte>();
        var msg = new CommandMessage
        {
            Magic = CommandMessage.MagicCommand,
            Sequence = sequence,
            CommandId = cmdId,
            PayloadLength = (ushort)payload.Length,
            Hmac = Array.Empty<byte>(), // placeholder
            Payload = payload
        };

        // Compute real HMAC
        var key = System.Text.Encoding.UTF8.GetBytes(hmacKey);
        var hmac = CommandProtocol.ComputeHmac(msg, key);
        return msg with { Hmac = hmac };
    }

    #endregion
}
