using System.Text.Json;
using System.Text.Json.Serialization;

namespace Common.Dto.Dtos;

/// <summary>
/// Represents a MIPI CSI-2 packet transmitted between FPGA and SoC.
/// REQ-SIM-051: Common.Dto shall define data transfer objects including Csi2Packet.
/// </summary>
public record Csi2Packet
{
    /// <summary>
    /// CSI-2 data type format.
    /// </summary>
    [JsonPropertyName("dataType")]
    public Csi2DataType DataType { get; init; }

    /// <summary>
    /// Virtual channel number (0-3).
    /// </summary>
    [JsonPropertyName("virtualChannel")]
    public int VirtualChannel { get; init; }

    /// <summary>
    /// Packet payload data.
    /// </summary>
    [JsonPropertyName("payload")]
    public byte[] Payload { get; init; }

    /// <summary>
    /// Creates a new Csi2Packet instance with validation.
    /// </summary>
    public Csi2Packet(Csi2DataType dataType, int virtualChannel, byte[] payload)
    {
        if (virtualChannel < 0 || virtualChannel > 3)
        {
            throw new ArgumentException("VirtualChannel must be between 0 and 3.", nameof(virtualChannel));
        }

        if (payload == null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        if (payload.Length == 0)
        {
            throw new ArgumentException("Payload must not be empty.", nameof(payload));
        }

        DataType = dataType;
        VirtualChannel = virtualChannel;
        Payload = payload;
    }

    /// <summary>
    /// Returns a string representation of the CSI-2 packet for debugging.
    /// </summary>
    public override string ToString()
    {
        return $"Csi2Packet {{ DataType = {DataType}, VirtualChannel = {VirtualChannel}, PayloadLength = {Payload.Length} }}";
    }
}

/// <summary>
/// CSI-2 data type enumeration.
/// Defines the standard MIPI CSI-2 data type formats.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Csi2DataType
{
    /// <summary>Raw 8-bit data.</summary>
    Raw8 = 0x30,

    /// <summary>Raw 10-bit data.</summary>
    Raw10 = 0x31,

    /// <summary>Raw 12-bit data.</summary>
    Raw12 = 0x32,

    /// <summary>Raw 14-bit data.</summary>
    Raw14 = 0x33,

    /// <summary>Raw 16-bit data (default for X-ray detector).</summary>
    Raw16 = 0x34,

    /// <summary>YUV422 8-bit data.</summary>
    Yuv4228Bit = 0x1E,

    /// <summary>RGB565 data.</summary>
    Rgb565 = 0x22
}
