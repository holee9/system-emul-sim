using System.Collections;
using FpgaSimulator.Core.Csi2;
using McuSimulator.Core.Csi2;

namespace McuSimulator.Core.Frame;

/// <summary>
/// Result of frame reassembly.
/// </summary>
public readonly record struct ReassembledFrame
{
    /// <summary>True if frame is valid and complete</summary>
    public required bool IsValid { get; init; }

    /// <summary>Frame height in rows</summary>
    public required int Rows { get; init; }

    /// <summary>Frame width in columns</summary>
    public required int Cols { get; init; }

    /// <summary>Total expected pixel count</summary>
    public required int TotalPixels { get; init; }

    /// <summary>Number of lines actually received</summary>
    public required int ReceivedLineCount { get; init; }

    /// <summary>2D pixel array [rows, cols] - contains zeros for missing packets</summary>
    public required ushort[,] Pixels { get; init; }

    /// <summary>Bitmap of received lines (bit N set = line N received). Supports arbitrary frame sizes.</summary>
    public required BitArray ReceivedLineBitmap { get; init; }
}

/// <summary>
/// Reassembles CSI-2 packets into complete frames.
/// Handles missing packets and out-of-order delivery.
/// Implements ethernet-protocol.md Section 3 frame reassembly algorithm.
/// </summary>
public sealed class FrameReassembler
{
    private bool _hasFrameStart;
    private bool _hasFrameEnd;
    private readonly Dictionary<int, ushort[]> _lines;
    private int _maxCols;
    private ushort _frameNumber;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    public FrameReassembler()
    {
        _hasFrameStart = false;
        _hasFrameEnd = false;
        _lines = new Dictionary<int, ushort[]>();
        _maxCols = 0;
        _frameNumber = 0;
    }

    /// <summary>True if Frame Start packet has been received</summary>
    public bool HasFrameStart => _hasFrameStart;

    /// <summary>Number of line data packets received</summary>
    public int ReceivedLineCount => _lines.Count;

    /// <summary>True if Frame End packet has been received (frame complete)</summary>
    public bool IsFrameComplete => _hasFrameStart && _hasFrameEnd;

    /// <summary>
    /// Adds a CSI-2 packet to the reassembly buffer.
    /// </summary>
    /// <param name="packet">CSI-2 packet</param>
    public void AddPacket(Csi2Packet packet)
    {
        switch (packet.PacketType)
        {
            case Csi2PacketType.FrameStart:
                HandleFrameStart(packet);
                break;

            case Csi2PacketType.LineData:
                HandleLineData(packet);
                break;

            case Csi2PacketType.FrameEnd:
                HandleFrameEnd(packet);
                break;
        }
    }

    /// <summary>
    /// Gets the reassembled frame if complete.
    /// Returns invalid result if frame is incomplete.
    /// </summary>
    /// <returns>Reassembled frame</returns>
    public ReassembledFrame GetFrame()
    {
        if (!IsFrameComplete)
            return new ReassembledFrame
            {
                IsValid = false,
                Rows = 0,
                Cols = 0,
                TotalPixels = 0,
                ReceivedLineCount = _lines.Count,
                Pixels = new ushort[0, 0],
                ReceivedLineBitmap = new BitArray(0)
            };

        // Determine dimensions from max line number + 1
        int rows = _lines.Keys.Count > 0 ? _lines.Keys.Max() + 1 : _lines.Count;
        int cols = _maxCols;

        if (rows == 0 || cols == 0)
            return new ReassembledFrame
            {
                IsValid = false,
                Rows = rows,
                Cols = cols,
                TotalPixels = 0,
                ReceivedLineCount = _lines.Count,
                Pixels = new ushort[0, 0],
                ReceivedLineBitmap = new BitArray(0)
            };

        // Build bitmap of received lines (supports arbitrary frame sizes)
        var bitmap = new BitArray(rows);
        foreach (var lineNum in _lines.Keys)
        {
            if (lineNum < rows)
                bitmap[lineNum] = true;
        }

        // Assemble frame, handling missing lines
        var pixels = new ushort[rows, cols];
        for (int row = 0; row < rows; row++)
        {
            if (_lines.TryGetValue(row, out var lineData))
            {
                // Copy received line
                for (int col = 0; col < Math.Min(lineData.Length, cols); col++)
                {
                    pixels[row, col] = lineData[col];
                }
            }
            // Missing lines remain as zeros
        }

        return new ReassembledFrame
        {
            IsValid = true,
            Rows = rows,
            Cols = cols,
            TotalPixels = rows * cols,
            ReceivedLineCount = _lines.Count,
            Pixels = pixels,
            ReceivedLineBitmap = bitmap
        };
    }

    /// <summary>
    /// Resets the reassembly buffer for a new frame.
    /// </summary>
    public void Reset()
    {
        _hasFrameStart = false;
        _hasFrameEnd = false;
        _lines.Clear();
        _maxCols = 0;
        _frameNumber = 0;
    }

    private void HandleFrameStart(Csi2Packet packet)
    {
        // Reset state for new frame
        Reset();

        _hasFrameStart = true;

        // Extract frame number if available
        if (packet.Data != null && packet.Data.Length >= 4)
        {
            _frameNumber = (ushort)((packet.Data[2] << 8) | packet.Data[1]);
        }
    }

    private void HandleLineData(Csi2Packet packet)
    {
        if (_hasFrameEnd)
            return; // Ignore lines after FE

        // Auto-generate FrameStart if not present (for testing flexibility)
        if (!_hasFrameStart)
            _hasFrameStart = true;

        // Parse line data using Csi2RxPacketParser
        var parser = new Csi2RxPacketParser();
        var result = parser.ParseLineData(packet);

        if (result.IsValid && result.Pixels != null)
        {
            _lines[result.LineNumber] = result.Pixels;
            _maxCols = Math.Max(_maxCols, result.PixelCount);
        }
    }

    private void HandleFrameEnd(Csi2Packet packet)
    {
        if (!_hasFrameStart)
            return; // Ignore FE without FS

        _hasFrameEnd = true;
    }
}
