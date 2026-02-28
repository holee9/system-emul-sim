using System.Net;
using System.Security.Cryptography;
using System.Text;
using Common.Dto.Dtos;

namespace IntegrationTests.Helpers;

/// <summary>
/// Factory for creating CSI-2 and Ethernet protocol test packets.
/// Supports CSI-2 magic values, CRC-16/CCITT, and HMAC-SHA256 authentication.
/// </summary>
public static class PacketFactory
{
    /// <summary>
    /// CSI-2 magic number for packet identification.
    /// </summary>
    public const uint Csi2Magic = 0xD7E01234;

    /// <summary>
    /// CRC-16/CCITT polynomial (poly 0x1021, non-reflected).
    /// Matches HostSimulator.Core.Reassembly.Crc16Ccitt implementation.
    /// </summary>
    private const ushort Crc16CciitPolynomial = 0x1021;

    /// <summary>
    /// Precomputed CRC table for fast calculation.
    /// </summary>
    private static readonly ushort[] CrcTable = new ushort[256];

    static PacketFactory()
    {
        // Initialize CRC table
        for (uint i = 0; i < 256; i++)
        {
            ushort crc = (ushort)(i << 8);
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 0x8000) != 0)
                {
                    crc = (ushort)((crc << 1) ^ Crc16CciitPolynomial);
                }
                else
                {
                    crc <<= 1;
                }
            }
            CrcTable[i] = crc;
        }
    }

    /// <summary>
    /// Creates a CSI-2 packet with standard test payload.
    /// </summary>
    /// <param name="dataType">CSI-2 data type (default: Raw16 for X-ray detector).</param>
    /// <param name="virtualChannel">Virtual channel number (0-3).</param>
    /// <param name="payloadSize">Payload size in bytes.</param>
    /// <returns>CSI-2 test packet.</returns>
    public static Csi2Packet CreateCsi2Packet(Csi2DataType dataType = Csi2DataType.Raw16, int virtualChannel = 0, int payloadSize = 256)
    {
        if (virtualChannel < 0 || virtualChannel > 3)
            throw new ArgumentException("VirtualChannel must be between 0 and 3.", nameof(virtualChannel));

        if (payloadSize < 1)
            throw new ArgumentException("PayloadSize must be at least 1.", nameof(payloadSize));

        byte[] payload = CreateTestPayload(payloadSize);
        return new Csi2Packet(dataType, virtualChannel, payload);
    }

    /// <summary>
    /// Creates an Ethernet frame packet with standard test payload.
    /// </summary>
    /// <param name="sourceIp">Source IP address.</param>
    /// <param name="sourcePort">Source port number.</param>
    /// <param name="destinationIp">Destination IP address.</param>
    /// <param name="destinationPort">Destination port number.</param>
    /// <param name="payloadSize">Payload size in bytes.</param>
    /// <returns>UDP test packet.</returns>
    public static UdpPacket CreateEthernetFramePacket(
        IPAddress? sourceIp = null,
        int sourcePort = 5000,
        IPAddress? destinationIp = null,
        int destinationPort = 6000,
        int payloadSize = 128)
    {
        byte[] payload = CreateTestPayload(payloadSize);
        sourceIp ??= IPAddress.Parse("192.168.1.100");
        destinationIp ??= IPAddress.Parse("192.168.1.1");

        return new UdpPacket(sourceIp, sourcePort, destinationIp, destinationPort, payload);
    }

    /// <summary>
    /// Creates an HMAC-SHA256 authenticated command packet.
    /// </summary>
    /// <param name="command">Command string to authenticate.</param>
    /// <param name="key">HMAC key (32 bytes for SHA-256).</param>
    /// <returns>Packet containing command and HMAC signature.</returns>
    public static (byte[] CommandBytes, byte[] HmacSignature) CreateHmacAuthenticatedCommand(string command, byte[]? key = null)
    {
        if (string.IsNullOrEmpty(command))
            throw new ArgumentException("Command cannot be null or empty.", nameof(command));

        key ??= GetDefaultHmacKey();

        byte[] commandBytes = Encoding.UTF8.GetBytes(command);
        byte[] hmac = CalculateHmac(commandBytes, key);

        return (commandBytes, hmac);
    }

    /// <summary>
    /// Calculates CRC-16/CCITT checksum for data.
    /// Uses polynomial 0x8408 (reflected) with initial value 0xFFFF.
    /// </summary>
    /// <param name="data">Data to calculate CRC for.</param>
    /// <returns>CRC-16 checksum.</returns>
    public static ushort CalculateCrc16Ccitt(byte[] data)
    {
        if (data == null || data.Length == 0)
            return 0xFFFF; // Initial value

        ushort crc = 0xFFFF;

        for (int i = 0; i < data.Length; i++)
        {
            ushort index = (ushort)(((crc >> 8) ^ data[i]) & 0xFF);
            crc = (ushort)((crc << 8) ^ CrcTable[index]);
        }

        return crc;
    }

    /// <summary>
    /// Calculates HMAC-SHA256 for data using the specified key.
    /// </summary>
    /// <param name="data">Data to authenticate.</param>
    /// <param name="key">HMAC key.</param>
    /// <returns>HMAC-SHA256 signature (32 bytes).</returns>
    public static byte[] CalculateHmac(byte[] data, byte[] key)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        if (key == null || key.Length < 1)
            throw new ArgumentException("Key cannot be null or empty.", nameof(key));

        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(data);
    }

    /// <summary>
    /// Creates a test payload with sequential bytes (0x00, 0x01, 0x02, ...).
    /// </summary>
    /// <param name="size">Payload size in bytes.</param>
    /// <returns>Test payload byte array.</returns>
    public static byte[] CreateTestPayload(int size)
    {
        if (size < 1)
            throw new ArgumentException("Size must be at least 1.", nameof(size));

        byte[] payload = new byte[size];
        for (int i = 0; i < size; i++)
        {
            payload[i] = (byte)(i & 0xFF);
        }
        return payload;
    }

    /// <summary>
    /// Gets the default HMAC key for testing (32 bytes of 0x01).
    /// </summary>
    /// <returns>Default HMAC key.</returns>
    public static byte[] GetDefaultHmacKey()
    {
        byte[] key = new byte[32];
        Array.Fill(key, (byte)0x01);
        return key;
    }

    /// <summary>
    /// Validates a CRC-16/CCITT checksum.
    /// </summary>
    /// <param name="data">Data that was checksummed.</param>
    /// <param name="expectedCrc">Expected CRC value.</param>
    /// <returns>True if CRC matches, false otherwise.</returns>
    public static bool ValidateCrc16Ccitt(byte[] data, ushort expectedCrc)
    {
        ushort calculatedCrc = CalculateCrc16Ccitt(data);
        return calculatedCrc == expectedCrc;
    }
}
