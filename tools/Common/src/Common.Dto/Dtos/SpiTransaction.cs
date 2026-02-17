using System.Text.Json;
using System.Text.Json.Serialization;

namespace Common.Dto.Dtos;

/// <summary>
/// Represents a SPI bus transaction for configuration and control.
/// REQ-SIM-051: Common.Dto shall define data transfer objects including SpiTransaction.
/// </summary>
public record SpiTransaction
{
    /// <summary>
    /// SPI command type.
    /// </summary>
    [JsonPropertyName("command")]
    public SpiCommand Command { get; init; }

    /// <summary>
    /// Register address.
    /// </summary>
    [JsonPropertyName("address")]
    public uint Address { get; init; }

    /// <summary>
    /// Data to write (null for read-only transactions).
    /// </summary>
    [JsonPropertyName("writeData")]
    public byte[]? WriteData { get; init; }

    /// <summary>
    /// Data read back (null for write-only transactions).
    /// </summary>
    [JsonPropertyName("readData")]
    public byte[]? ReadData { get; init; }

    /// <summary>
    /// Creates a new SpiTransaction instance with validation.
    /// </summary>
    public SpiTransaction(SpiCommand command, uint address, byte[]? writeData, byte[]? readData)
    {
        if (!Enum.IsDefined(typeof(SpiCommand), command))
        {
            throw new ArgumentException("Invalid SPI command.", nameof(command));
        }

        // For write operations, writeData should not be null
        if (command is SpiCommand.Write or SpiCommand.WriteThenRead && writeData == null)
        {
            throw new ArgumentException("WriteData must not be null for Write operations.", nameof(writeData));
        }

        // For write operations, writeData should not be empty
        if (command is SpiCommand.Write or SpiCommand.WriteThenRead && writeData != null && writeData.Length == 0)
        {
            throw new ArgumentException("WriteData must not be empty for Write operations.", nameof(writeData));
        }

        Command = command;
        Address = address;
        WriteData = writeData;
        ReadData = readData;
    }

    /// <summary>
    /// Returns a string representation of the SPI transaction for debugging.
    /// </summary>
    public override string ToString()
    {
        return $"SpiTransaction {{ Command = {Command}, Address = 0x{Address:X4}, WriteDataLength = {WriteData?.Length ?? 0}, ReadDataLength = {ReadData?.Length ?? 0} }}";
    }
}

/// <summary>
/// SPI command enumeration.
/// Defines the types of SPI transactions supported.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SpiCommand
{
    /// <summary>Read operation.</summary>
    Read = 0,

    /// <summary>Write operation.</summary>
    Write = 1,

    /// <summary>Write followed by read operation.</summary>
    WriteThenRead = 2
}
