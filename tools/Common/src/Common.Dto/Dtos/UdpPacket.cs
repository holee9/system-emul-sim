using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net;

namespace Common.Dto.Dtos;

/// <summary>
/// Custom JSON converter for IPAddress to handle serialization as string.
/// </summary>
public class IPAddressJsonConverter : JsonConverter<IPAddress>
{
    /// <inheritdoc />
    public override IPAddress Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var ipString = reader.GetString();
        return IPAddress.Parse(ipString!);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, IPAddress value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

/// <summary>
/// Represents a UDP network packet for data transmission.
/// REQ-SIM-051: Common.Dto shall define data transfer objects including UdpPacket.
/// </summary>
public record UdpPacket
{
    /// <summary>
    /// Source IP address.
    /// </summary>
    [JsonPropertyName("sourceIp")]
    [JsonConverter(typeof(IPAddressJsonConverter))]
    public IPAddress SourceIp { get; init; }

    /// <summary>
    /// Source port number (1-65535).
    /// </summary>
    [JsonPropertyName("sourcePort")]
    public int SourcePort { get; init; }

    /// <summary>
    /// Destination IP address.
    /// </summary>
    [JsonPropertyName("destinationIp")]
    [JsonConverter(typeof(IPAddressJsonConverter))]
    public IPAddress DestinationIp { get; init; }

    /// <summary>
    /// Destination port number (1-65535).
    /// </summary>
    [JsonPropertyName("destinationPort")]
    public int DestinationPort { get; init; }

    /// <summary>
    /// Packet payload data.
    /// </summary>
    [JsonPropertyName("payload")]
    public byte[] Payload { get; init; }

    /// <summary>
    /// Creates a new UdpPacket instance with validation.
    /// </summary>
    /// <param name="sourceIp">Source IP address.</param>
    /// <param name="sourcePort">Source port number (1-65535).</param>
    /// <param name="destinationIp">Destination IP address.</param>
    /// <param name="destinationPort">Destination port number (1-65535).</param>
    /// <param name="payload">Packet payload data.</param>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
    /// <exception cref="ArgumentException">Thrown when port numbers are out of valid range.</exception>
    public UdpPacket(IPAddress sourceIp, int sourcePort, IPAddress destinationIp, int destinationPort, byte[] payload)
    {
        if (sourceIp == null)
        {
            throw new ArgumentNullException(nameof(sourceIp));
        }

        if (sourcePort < 1 || sourcePort > 65535)
        {
            throw new ArgumentException("SourcePort must be between 1 and 65535.", nameof(sourcePort));
        }

        if (destinationIp == null)
        {
            throw new ArgumentNullException(nameof(destinationIp));
        }

        if (destinationPort < 1 || destinationPort > 65535)
        {
            throw new ArgumentException("DestinationPort must be between 1 and 65535.", nameof(destinationPort));
        }

        if (payload == null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        SourceIp = sourceIp;
        SourcePort = sourcePort;
        DestinationIp = destinationIp;
        DestinationPort = destinationPort;
        Payload = payload;
    }

    /// <summary>
    /// Returns a string representation of the UDP packet for debugging.
    /// </summary>
    /// <returns>String representation containing source/destination IP, ports, and payload length.</returns>
    public override string ToString()
    {
        return $"UdpPacket {{ SourceIp = {SourceIp}, SourcePort = {SourcePort}, DestinationIp = {DestinationIp}, DestinationPort = {DestinationPort}, PayloadLength = {Payload.Length} }}";
    }
}
