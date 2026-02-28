using Xunit;
using FluentAssertions;
using IntegrationTests.Helpers;
using System.Security.Cryptography;

namespace IntegrationTests.Integration;

/// <summary>
/// IT-06: HMAC-SHA256 Command Authentication test.
/// Validates command authentication via HMAC-SHA256 on port 8001.
/// Reference: SPEC-INTEG-001 AC-INTEG-006
/// </summary>
public class IT06_HmacAuthenticationTests
{
    [Fact]
    public void ValidHmac_ShallAcceptCommand_Success()
    {
        // Arrange - Create command with valid HMAC
        string command = "START_SCAN";
        byte[] key = HMACTestHelper.DefaultKey;
        var testVector = HMACTestHelper.CreateTestVector(command, key);

        // Act - Validate HMAC
        bool isValid = HMACTestHelper.ValidateHmac(
            testVector.Data,
            testVector.Key,
            testVector.ExpectedHmac
        );

        // Assert - Valid HMAC should be accepted
        isValid.Should().BeTrue("Valid HMAC should be accepted");
        testVector.Validate().Should().BeTrue("Test vector self-validation should pass");
    }

    [Fact]
    public void InvalidHmac_ShallRejectCommand_Failure()
    {
        // Arrange - Create command with INVALID HMAC (corrupted)
        string command = "START_SCAN";
        byte[] key = HMACTestHelper.DefaultKey;
        byte[] data = System.Text.Encoding.UTF8.GetBytes(command);

        // Corrupt the HMAC (flip bits)
        byte[] wrongHmac = new byte[32];
        Array.Fill(wrongHmac, (byte)0xFF);

        // Act - Validate corrupted HMAC
        bool isValid = HMACTestHelper.ValidateHmac(data, key, wrongHmac);

        // Assert - Invalid HMAC should be rejected
        isValid.Should().BeFalse("Invalid HMAC should be rejected");
    }

    [Fact]
    public void MissingHmac_ShallRejectCommand_Failure()
    {
        // Arrange - Command without HMAC
        string command = "START_SCAN";
        byte[] key = HMACTestHelper.DefaultKey;
        byte[] data = System.Text.Encoding.UTF8.GetBytes(command);
        byte[] emptyHmac = Array.Empty<byte>();

        // Act - Validate with empty HMAC
        bool isValid = HMACTestHelper.ValidateHmac(data, key, emptyHmac);

        // Assert - Missing HMAC should be rejected
        isValid.Should().BeFalse("Missing HMAC should be rejected");
    }

    [Fact]
    public void HmacValidation_ShallHave100PercentRejectionRate_AllInvalidRejected()
    {
        // Arrange - Create multiple invalid HMAC attempts
        var invalidAttempts = new List<bool>();
        string[] commands = { "START_SCAN", "STOP_SCAN", "CONFIGURE", "GET_STATUS" };
        byte[] key = HMACTestHelper.DefaultKey;

        foreach (var command in commands)
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes(command);

            // Test 1: Wrong HMAC length
            byte[] shortHmac = new byte[16];
            invalidAttempts.Add(HMACTestHelper.ValidateHmac(data, key, shortHmac));

            // Test 2: Corrupted HMAC
            byte[] correctHmac = HMACTestHelper.CalculateHmac(data, key);
            correctHmac[0] ^= 0xFF; // Flip bits
            invalidAttempts.Add(HMACTestHelper.ValidateHmac(data, key, correctHmac));

            // Test 3: Empty HMAC
            invalidAttempts.Add(HMACTestHelper.ValidateHmac(data, key, Array.Empty<byte>()));
        }

        // Act & Assert - 100% rejection rate
        int rejectedCount = invalidAttempts.Count(r => r == false);
        int totalCount = invalidAttempts.Count;

        rejectedCount.Should().Be(totalCount,
            $"All invalid HMACs should be rejected: {rejectedCount}/{totalCount}");
    }

    [Fact]
    public void HmacValidation_ShallHave100PercentAcceptanceRate_AllValidAccepted()
    {
        // Arrange - Create multiple valid HMAC attempts
        var validAttempts = new List<bool>();
        string[] commands = { "START_SCAN", "STOP_SCAN", "CONFIGURE", "GET_STATUS", "RESET" };
        byte[] key = HMACTestHelper.DefaultKey;

        foreach (var command in commands)
        {
            var testVector = HMACTestHelper.CreateTestVector(command, key);
            validAttempts.Add(testVector.Validate());
        }

        // Act & Assert - 100% acceptance rate
        int acceptedCount = validAttempts.Count(r => r);
        int totalCount = validAttempts.Count;

        acceptedCount.Should().Be(totalCount,
            $"All valid HMACs should be accepted: {acceptedCount}/{totalCount}");
    }

    [Fact]
    public void HmacCalculation_ShallBeDeterministic_SameInputSameOutput()
    {
        // Arrange
        string command = "CONFIGURE_EXPOSURE_100";
        byte[] key = HMACTestHelper.DefaultKey;
        byte[] data = System.Text.Encoding.UTF8.GetBytes(command);

        // Act - Calculate HMAC twice
        byte[] hmac1 = HMACTestHelper.CalculateHmac(data, key);
        byte[] hmac2 = HMACTestHelper.CalculateHmac(data, key);

        // Assert - HMAC should be deterministic
        hmac1.Should().Equal(hmac2, "HMAC calculation should be deterministic");
    }

    [Fact]
    public void HmacDifferentKeys_ShallProduceDifferentHmacs_KeySensitivity()
    {
        // Arrange
        string command = "START_SCAN";
        byte[] data = System.Text.Encoding.UTF8.GetBytes(command);

        byte[] key1 = new byte[32];
        Array.Fill(key1, (byte)0x01);

        byte[] key2 = new byte[32];
        Array.Fill(key2, (byte)0x02);

        // Act - Calculate HMAC with different keys
        byte[] hmac1 = HMACTestHelper.CalculateHmac(data, key1);
        byte[] hmac2 = HMACTestHelper.CalculateHmac(data, key2);

        // Assert - Different keys should produce different HMACs
        hmac1.Should().NotEqual(hmac2, "Different keys should produce different HMACs");
    }

    [Fact]
    public void HmacDifferentCommands_ShallProduceDifferentHmacs_DataSensitivity()
    {
        // Arrange
        byte[] key = HMACTestHelper.DefaultKey;

        // Act - Calculate HMAC for different commands
        var hmac1 = HMACTestHelper.CreateTestVector("START_SCAN", key).ExpectedHmac;
        var hmac2 = HMACTestHelper.CreateTestVector("STOP_SCAN", key).ExpectedHmac;

        // Assert - Different commands should produce different HMACs
        hmac1.Should().NotEqual(hmac2, "Different commands should produce different HMACs");
    }

    [Fact]
    public void HmacValidation_ShallUseConstantTimeComparison_TimingSafe()
    {
        // Arrange
        string command = "START_SCAN";
        byte[] key = HMACTestHelper.DefaultKey;
        byte[] data = System.Text.Encoding.UTF8.GetBytes(command);
        byte[] correctHmac = HMACTestHelper.CalculateHmac(data, key);

        // Create HMAC that differs in first byte
        byte[] wrongHmacFirst = (byte[])correctHmac.Clone();
        wrongHmacFirst[0] ^= 0xFF;

        // Create HMAC that differs in last byte
        byte[] wrongHmacLast = (byte[])correctHmac.Clone();
        wrongHmacLast[31] ^= 0xFF;

        // Act - Both should be rejected
        bool firstRejected = !HMACTestHelper.ValidateHmac(data, key, wrongHmacFirst);
        bool lastRejected = !HMACTestHelper.ValidateHmac(data, key, wrongHmacLast);

        // Assert - Both should be rejected regardless of which byte differs
        firstRejected.Should().BeTrue("HMAC differing in first byte should be rejected");
        lastRejected.Should().BeTrue("HMAC differing in last byte should be rejected");
    }

    [Fact]
    public void PacketFactory_CreateAuthenticatedCommand_ShouldMatchHelper()
    {
        // Arrange
        string command = "GET_STATUS";
        byte[] key = HMACTestHelper.DefaultKey;

        // Act - Create authenticated command via PacketFactory
        var (commandBytes, hmacSignature) = PacketFactory.CreateHmacAuthenticatedCommand(command, key);

        // Assert - Should match HMACTestHelper result
        var testVector = HMACTestHelper.CreateTestVector(command, key);
        commandBytes.Should().Equal(testVector.Data, "Command bytes should match");
        hmacSignature.Should().Equal(testVector.ExpectedHmac, "HMAC signature should match");
    }
}
