using Common.Dto;
using System.Buffers.Binary;
using System.Text;

namespace McuSimulator.Core.Network;

/// <summary>
/// Internal UDP packet representation for frame transmission.
/// Simplified version focusing on frame data rather than network endpoints.
/// </summary>
public readonly record struct UdpFramePacket
{
    /// <summary>Complete packet data including header and payload</summary>
    public required byte[] Data { get; init; }

    /// <summary>Packet index within frame</summary>
    public required int PacketIndex { get; init; }

    /// <summary>Total packets in frame</summary>
    public required int TotalPackets { get; init; }

    /// <summary>Flags byte (bit 0 = last_packet)</summary>
    public required byte Flags { get; init; }
}

/// <summary>
/// Transmits reassembled frames as UDP packets.
/// Implements ethernet-protocol.md Section 2 frame data protocol.
/// </summary>
public sealed class UdpFrameTransmitter
{
    private readonly int _maxPayload;

    /// <summary>
    /// Initializes a new instance with default 8192-byte payload.
    /// </summary>
    public UdpFrameTransmitter() : this(maxPayload: 8192)
    {
    }

    /// <summary>
    /// Initializes a new instance with specified payload size.
    /// </summary>
    /// <param name="maxPayload">Maximum payload per packet (default: 8192)</param>
    public UdpFrameTransmitter(int maxPayload)
    {
        _maxPayload = maxPayload;
    }

    /// <summary>
    /// Creates a 32-byte frame header per ethernet-protocol.md Section 2.1.
    /// </summary>
    /// <param name="frameData">2D pixel array</param>
    /// <param name="frameId">Frame identifier</param>
    /// <returns>32-byte header array</returns>
    public byte[] CreateFrameHeader(ushort[,] frameData, uint frameId)
    {
        var header = new byte[32];
        int rows = frameData.GetLength(0);
        int cols = frameData.GetLength(1);

        // Offset 0-3: magic (0xD7E01234, little-endian)
        header[0] = 0x34;
        header[1] = 0x12;
        header[2] = 0xE0;
        header[3] = 0xD7;

        // Offset 4: version (0x01)
        header[4] = 0x01;

        // Offset 5-7: reserved (0x00)
        header[5] = 0x00;
        header[6] = 0x00;
        header[7] = 0x00;

        // Offset 8-11: frame_id (little-endian)
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8, 4), frameId);

        // Offset 12-13: packet_seq (will be set per packet)
        header[12] = 0x00;
        header[13] = 0x00;

        // Offset 14-15: total_packets (will be set per packet)
        header[14] = 0x00;
        header[15] = 0x00;

        // Offset 16-23: timestamp_ns (using current time)
        long timestamp = DateTime.UtcNow.Ticks * 100; // Convert to nanoseconds
        BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(16, 8), timestamp);

        // Offset 24-25: rows
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(24, 2), (ushort)rows);

        // Offset 26-27: cols
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(26, 2), (ushort)cols);

        // Offset 28-29: crc16 (calculated over bytes 0-27)
        ushort crc = CalculateCrc16(header.AsSpan(0, 28).ToArray());
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(28, 2), crc);

        // Offset 30: bit_depth (16 for our system)
        header[30] = 16;

        // Offset 31: flags (will be set per packet)
        header[31] = 0x00;

        return header;
    }

    /// <summary>
    /// Fragments a frame into multiple UDP packets.
    /// </summary>
    /// <param name="frameData">2D pixel array</param>
    /// <param name="frameId">Frame identifier</param>
    /// <returns>List of UDP packets</returns>
    public List<UdpFramePacket> FragmentFrame(ushort[,] frameData, uint frameId)
    {
        int rows = frameData.GetLength(0);
        int cols = frameData.GetLength(1);
        int totalBytes = rows * cols * 2; // 2 bytes per pixel

        int totalPackets = (totalBytes + _maxPayload - 1) / _maxPayload;
        var packets = new List<UdpFramePacket>(totalPackets);

        // Create base header (will be cloned and modified per packet)
        byte[] baseHeader = CreateFrameHeader(frameData, frameId);

        // Set total_packets in base header
        BinaryPrimitives.WriteUInt16LittleEndian(baseHeader.AsSpan(14, 2), (ushort)totalPackets);

        // Fragment frame data
        for (int i = 0; i < totalPackets; i++)
        {
            int offset = i * _maxPayload;
            int remainingBytes = totalBytes - offset;
            int payloadSize = Math.Min(_maxPayload, remainingBytes);

            // Clone header for this packet
            var packetHeader = (byte[])baseHeader.Clone();

            // Set packet_seq
            BinaryPrimitives.WriteUInt16LittleEndian(packetHeader.AsSpan(12, 2), (ushort)i);

            // Set flags (last_packet bit)
            if (i == totalPackets - 1)
            {
                packetHeader[31] |= 0x01; // Set last_packet flag
            }

            // Recalculate CRC with updated fields
            ushort crc = CalculateCrc16(packetHeader.AsSpan(0, 28).ToArray());
            BinaryPrimitives.WriteUInt16LittleEndian(packetHeader.AsSpan(28, 2), crc);

            // Assemble packet: header + payload
            var packetData = new byte[32 + payloadSize];
            Array.Copy(packetHeader, 0, packetData, 0, 32);

            // Copy pixel data for this packet
            int startPixel = offset / 2;
            int pixelsInPacket = payloadSize / 2;
            for (int p = 0; p < pixelsInPacket; p++)
            {
                int pixelIndex = startPixel + p;
                int row = pixelIndex / cols;
                int col = pixelIndex % cols;

                if (row < rows && col < cols)
                {
                    ushort pixel = frameData[row, col];
                    packetData[32 + p * 2] = (byte)(pixel & 0xFF);         // LSB
                    packetData[32 + p * 2 + 1] = (byte)((pixel >> 8) & 0xFF); // MSB
                }
            }

            packets.Add(new UdpFramePacket
            {
                Data = packetData,
                PacketIndex = i,
                TotalPackets = totalPackets,
                Flags = packetHeader[31]
            });
        }

        return packets;
    }

    /// <summary>
    /// Transmits a frame as UDP packets.
    /// </summary>
    /// <param name="frameData">2D pixel array</param>
    /// <param name="frameId">Frame identifier</param>
    /// <returns>List of UDP packets ready for transmission</returns>
    public List<UdpFramePacket> TransmitFrame(ushort[,] frameData, uint frameId)
    {
        return FragmentFrame(frameData, frameId);
    }

    /// <summary>
    /// Calculates CRC-16/CCITT over data bytes.
    /// Implements ethernet-protocol.md Section 7.
    /// </summary>
    /// <param name="data">Input data</param>
    /// <returns>CRC-16 checksum</returns>
    public ushort CalculateCrc16(byte[] data)
    {
        ushort crc = 0xFFFF;

        for (int i = 0; i < data.Length; i++)
        {
            crc ^= (ushort)data[i];
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 0x0001) != 0)
                    crc = (ushort)((crc >> 1) ^ 0x8408);
                else
                    crc >>= 1;
            }
        }

        return crc;
    }
}
