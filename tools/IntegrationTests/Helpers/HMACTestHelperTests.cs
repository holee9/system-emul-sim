using FluentAssertions;
using IntegrationTests.Helpers;
using System.Text;
using Xunit;

namespace IntegrationTests.Helpers;

/// <summary>
/// Tests for HMACTestHelper using TDD approach.
/// </summary>
public class HMACTestHelperTests
{
    [Fact]
    public void DefaultKey_Is32BytesOfOnes()
    {
        // Arrange & Act
        var key = HMACTestHelper.DefaultKey;

        // Assert
        key.Length.Should().Be(32);
        key.Should().OnlyContain(b => b == 0x01);
    }

    [Fact]
    public void CalculateHmac_WithSameDataAndKey_ReturnsSameSignature()
    {
        // Arrange
        byte[] data = Encoding.UTF8.GetBytes("test");
        byte[] key = HMACTestHelper.DefaultKey;

        // Act
        var hmac1 = HMACTestHelper.CalculateHmac(data, key);
        var hmac2 = HMACTestHelper.CalculateHmac(data, key);

        // Assert
        hmac1.Should().BeEquivalentTo(hmac2);
    }

    [Fact]
    public void CalculateHmac_WithDifferentData_ReturnsDifferentSignature()
    {
        // Arrange
        byte[] data1 = Encoding.UTF8.GetBytes("test1");
        byte[] data2 = Encoding.UTF8.GetBytes("test2");
        byte[] key = HMACTestHelper.DefaultKey;

        // Act
        var hmac1 = HMACTestHelper.CalculateHmac(data1, key);
        var hmac2 = HMACTestHelper.CalculateHmac(data2, key);

        // Assert
        hmac1.Should().NotBeEquivalentTo(hmac2);
    }

    [Fact]
    public void CalculateHmac_WithDifferentKey_ReturnsDifferentSignature()
    {
        // Arrange
        byte[] data = Encoding.UTF8.GetBytes("test");
        byte[] key1 = Encoding.UTF8.GetBytes("key1");
        byte[] key2 = Encoding.UTF8.GetBytes("key2");

        // Act
        var hmac1 = HMACTestHelper.CalculateHmac(data, key1);
        var hmac2 = HMACTestHelper.CalculateHmac(data, key2);

        // Assert
        hmac1.Should().NotBeEquivalentTo(hmac2);
    }

    [Fact]
    public void CalculateHmac_Returns32ByteSignature()
    {
        // Arrange
        byte[] data = Encoding.UTF8.GetBytes("test");
        byte[] key = HMACTestHelper.DefaultKey;

        // Act
        var hmac = HMACTestHelper.CalculateHmac(data, key);

        // Assert
        hmac.Length.Should().Be(32);
    }

    [Fact]
    public void CalculateHmac_WithNullData_ThrowsException()
    {
        // Arrange
        byte[] key = HMACTestHelper.DefaultKey;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            HMACTestHelper.CalculateHmac(null!, key));
    }

    [Fact]
    public void CalculateHmac_WithEmptyKey_ThrowsException()
    {
        // Arrange
        byte[] data = Encoding.UTF8.GetBytes("test");
        byte[] key = Array.Empty<byte>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            HMACTestHelper.CalculateHmac(data, key));
    }

    [Fact]
    public void GetValidTestVector_ReturnsValidHmac()
    {
        // Arrange & Act
        var vector = HMACTestHelper.GetValidTestVector();

        // Assert
        vector.IsValid.Should().BeTrue();
        vector.Data.Should().NotBeEmpty();
        vector.ExpectedHmac.Length.Should().Be(32);
    }

    [Fact]
    public void GetValidTestVector_Validate_ReturnsTrue()
    {
        // Arrange & Act
        var vector = HMACTestHelper.GetValidTestVector();

        // Act
        bool isValid = vector.Validate();

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void GetInvalidTestVector_ReturnsInvalidHmac()
    {
        // Arrange & Act
        var vector = HMACTestHelper.GetInvalidTestVector();

        // Assert
        vector.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GetInvalidTestVector_Validate_ReturnsFalse()
    {
        // Arrange & Act
        var vector = HMACTestHelper.GetInvalidTestVector();

        // Act
        bool isValid = vector.Validate();

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void CreateTestVector_WithCommand_ReturnsValidHmac()
    {
        // Arrange & Act
        var vector = HMACTestHelper.CreateTestVector("CUSTOM_COMMAND");

        // Assert
        vector.IsValid.Should().BeTrue();
        vector.Validate().Should().BeTrue();
    }

    [Fact]
    public void CreateTestVector_WithEmptyCommand_ThrowsException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() =>
            HMACTestHelper.CreateTestVector(""));
    }

    [Fact]
    public void ValidateHmac_WithCorrectSignature_ReturnsTrue()
    {
        // Arrange
        byte[] data = Encoding.UTF8.GetBytes("test");
        byte[] key = HMACTestHelper.DefaultKey;
        byte[] signature = HMACTestHelper.CalculateHmac(data, key);

        // Act
        bool isValid = HMACTestHelper.ValidateHmac(data, key, signature);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateHmac_WithIncorrectSignature_ReturnsFalse()
    {
        // Arrange
        byte[] data = Encoding.UTF8.GetBytes("test");
        byte[] key = HMACTestHelper.DefaultKey;
        byte[] wrongSignature = new byte[32];

        // Act
        bool isValid = HMACTestHelper.ValidateHmac(data, key, wrongSignature);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateHmac_WithWrongLengthSignature_ReturnsFalse()
    {
        // Arrange
        byte[] data = Encoding.UTF8.GetBytes("test");
        byte[] key = HMACTestHelper.DefaultKey;
        byte[] shortSignature = new byte[16];

        // Act
        bool isValid = HMACTestHelper.ValidateHmac(data, key, shortSignature);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void ValidateHmac_WithNullData_ReturnsFalse()
    {
        // Arrange
        byte[] key = HMACTestHelper.DefaultKey;
        byte[] signature = new byte[32];

        // Act
        bool isValid = HMACTestHelper.ValidateHmac(null!, key, signature);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void HmacTestVector_ToString_ReturnsFormattedString()
    {
        // Arrange
        var vector = HMACTestHelper.GetValidTestVector();

        // Act
        string str = vector.ToString();

        // Assert
        str.Should().Contain("ValidTestCommand");
        str.Should().Contain("True");
    }
}
