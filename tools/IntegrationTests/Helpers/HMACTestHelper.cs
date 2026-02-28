using System.Security.Cryptography;
using System.Text;

namespace IntegrationTests.Helpers;

/// <summary>
/// Helper for HMAC-SHA256 test vectors and validation.
/// </summary>
public static class HMACTestHelper
{
    /// <summary>
    /// Default test key (32 bytes of 0x01).
    /// </summary>
    public static readonly byte[] DefaultKey = new byte[32];

    static HMACTestHelper()
    {
        Array.Fill(DefaultKey, (byte)0x01);
    }

    /// <summary>
    /// Calculates HMAC-SHA256 for data.
    /// </summary>
    /// <param name="data">Data to authenticate.</param>
    /// <param name="key">HMAC key.</param>
    /// <returns>HMAC-SHA256 signature (32 bytes).</returns>
    public static byte[] CalculateHmac(byte[] data, byte[] key)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        if (key == null || key.Length == 0)
            throw new ArgumentException("Key cannot be null or empty.", nameof(key));

        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(data);
    }

    /// <summary>
    /// Gets a valid test vector with known data and HMAC.
    /// </summary>
    /// <returns>Test vector with data and expected HMAC.</returns>
    public static HmacTestVector GetValidTestVector()
    {
        byte[] data = Encoding.UTF8.GetBytes("ValidTestCommand");
        byte[] expectedHmac = CalculateHmac(data, DefaultKey);

        return new HmacTestVector(data, DefaultKey, expectedHmac, true);
    }

    /// <summary>
    /// Gets an invalid test vector with incorrect HMAC.
    /// </summary>
    /// <returns>Test vector with data and wrong HMAC.</returns>
    public static HmacTestVector GetInvalidTestVector()
    {
        byte[] data = Encoding.UTF8.GetBytes("InvalidTestCommand");
        byte[] wrongHmac = new byte[32];
        Array.Fill(wrongHmac, (byte)0xFF);

        return new HmacTestVector(data, DefaultKey, wrongHmac, false);
    }

    /// <summary>
    /// Creates a custom test vector.
    /// </summary>
    /// <param name="command">Command string.</param>
    /// <param name="key">HMAC key.</param>
    /// <returns>Test vector with calculated HMAC.</returns>
    public static HmacTestVector CreateTestVector(string command, byte[]? key = null)
    {
        if (string.IsNullOrEmpty(command))
            throw new ArgumentException("Command cannot be null or empty.", nameof(command));

        key ??= DefaultKey;
        byte[] data = Encoding.UTF8.GetBytes(command);
        byte[] expectedHmac = CalculateHmac(data, key);

        return new HmacTestVector(data, key, expectedHmac, true);
    }

    /// <summary>
    /// Validates an HMAC signature.
    /// </summary>
    /// <param name="data">Original data.</param>
    /// <param name="key">HMAC key.</param>
    /// <param name="signature">Signature to validate.</param>
    /// <returns>True if signature is valid.</returns>
    public static bool ValidateHmac(byte[] data, byte[] key, byte[] signature)
    {
        if (data == null || key == null || signature == null)
            return false;

        if (signature.Length != 32)
            return false;

        byte[] calculatedHmac = CalculateHmac(data, key);
        return CryptographicOperations.FixedTimeEquals(calculatedHmac, signature);
    }
}

/// <summary>
/// Test vector for HMAC validation.
/// </summary>
public sealed class HmacTestVector
{
    /// <summary>Original data bytes.</summary>
    public byte[] Data { get; }

    /// <summary>HMAC key used.</summary>
    public byte[] Key { get; }

    /// <summary>Expected HMAC signature.</summary>
    public byte[] ExpectedHmac { get; }

    /// <summary>Whether this vector represents valid data.</summary>
    public bool IsValid { get; }

    public HmacTestVector(byte[] data, byte[] key, byte[] expectedHmac, bool isValid)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        Key = key ?? throw new ArgumentNullException(nameof(key));
        ExpectedHmac = expectedHmac ?? throw new ArgumentNullException(nameof(expectedHmac));
        IsValid = isValid;
    }

    /// <summary>
    /// Validates the test vector's HMAC.
    /// </summary>
    /// <returns>True if calculated HMAC matches expected.</returns>
    public bool Validate()
    {
        return HMACTestHelper.ValidateHmac(Data, Key, ExpectedHmac);
    }

    public override string ToString()
    {
        string dataStr = Encoding.UTF8.GetString(Data);
        return $"HmacTestVector: Data='{dataStr}', Valid={IsValid}";
    }
}
