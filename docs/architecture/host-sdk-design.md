# Host SDK Architecture Design

**Project**: X-ray Detector Panel System
**Target Platform**: Windows/Linux x86-64, .NET 8.0+ (C#)
**Document Version**: 1.0.0
**Status**: Reviewed - Approved
**Last Updated**: 2026-02-17

---

## 1. Overview

### 1.1 Purpose

This document describes the Host SDK architecture for the X-ray Detector Panel System. The SDK provides a managed API for controlling the detector, receiving frames, storing images, and displaying real-time previews on the Host PC.

### 1.2 Responsibilities

The Host SDK (DetectorClient) handles:
- **Network Communication**: Receive frame data from SoC via 10 GbE UDP
- **Frame Reassembly**: Reconstruct 2D images from UDP packet fragments
- **Detector Control**: Send scan commands (start, stop, configure) to SoC
- **Storage**: Save frames as TIFF, RAW, or optionally DICOM
- **Real-Time Display**: Provide 15 fps preview with brightness/contrast adjustment
- **Discovery**: Automatic detector device discovery on the network

### 1.3 Platform Requirements

| Requirement | Specification |
|-------------|-------------|
| Runtime | .NET 8.0 LTS or later |
| Language | C# 12 |
| OS | Windows 10/11, Linux (Ubuntu 22.04+) |
| Network | 10 GbE NIC recommended, 1 GbE minimum (Minimum tier only) |
| Memory | 8 GB RAM minimum (16 GB recommended for Target tier) |
| Storage | SSD recommended for continuous frame capture |

---

## 2. Module Architecture

### 2.1 Component Diagram

```
+------------------------------------------------------------------+
|                      Host Application                             |
|  +-------------------+  +-------------------+  +---------------+ |
|  | GUI.Application   |  | IntegrationRunner |  | Custom Apps   | |
|  | (WPF Viewer)      |  | (CLI Test)        |  | (User Code)   | |
|  +--------+----------+  +--------+----------+  +-------+-------+ |
+-----------|----------------------|----------------------|--------+
            |                      |                      |
+-----------|---v---v---v---v---v---v---v---v---v---v---v--|--------+
|                    DetectorClient SDK (Public API)                 |
|                                                                   |
|  +-------------------+  +-------------------+  +---------------+ |
|  | IDetectorClient   |  | IFrameBuffer      |  | IFrameStore   | |
|  | (Control API)     |  | (Frame Access)     |  | (Persistence) | |
|  +--------+----------+  +--------+----------+  +-------+-------+ |
|           |                      |                      |         |
|  +--------v----------+  +--------v----------+  +-------v-------+ |
|  | DetectorClient     |  | FrameBuffer       |  | FrameStore    | |
|  | Implementation     |  | Implementation    |  | (TIFF/RAW/    | |
|  |                    |  |                   |  |  DICOM)       | |
|  +--------+----------+  +--------+----------+  +-------+-------+ |
+-----------|----------------------|----------------------|--------+
            |                      |                      |
+-----------|----------------------|----------------------|--------+
|                    Internal Modules                               |
|  +--------v----------+  +--------v----------+  +-------v-------+ |
|  | PacketReceiver     |  | FrameReassembler  |  | ImageEncoder  | |
|  | (UDP Socket,       |  | (Out-of-order     |  | (TIFF, RAW,   | |
|  |  Multi-threaded)   |  |  reassembly)      |  |  DICOM write) | |
|  +--------+----------+  +--------+----------+  +-------+-------+ |
|           |                      |                                 |
|  +--------v----------+  +--------v----------+                     |
|  | PacketProtocol     |  | FrameValidator    |                    |
|  | (Header parse,     |  | (CRC check,       |                    |
|  |  CRC validate)     |  |  completeness)    |                    |
|  +-------------------+  +-------------------+                     |
+------------------------------------------------------------------+
            |
     [10 GbE / 1 GbE UDP]
     (from SoC Controller)
```

### 2.2 Module Dependency Rules

All modules depend only on `Common.Dto` (shared interfaces and DTOs). No circular dependencies exist between internal modules.

```
Common.Dto  <-- DetectorClient
            <-- PacketReceiver
            <-- FrameReassembler
            <-- PacketProtocol
            <-- FrameBuffer
            <-- FrameStore
            <-- ImageEncoder
            <-- FrameValidator
```

---

## 3. Public API (IDetectorClient)

### 3.1 Interface Definition

```csharp
namespace XrayDetector.Sdk;

/// <summary>
/// Primary API for controlling an X-ray detector device.
/// </summary>
public interface IDetectorClient : IDisposable
{
    // Connection Management
    Task ConnectAsync(string host, int port = 8000, CancellationToken ct = default);
    Task DisconnectAsync();
    bool IsConnected { get; }
    DetectorInfo DeviceInfo { get; }

    // Scan Control
    Task StartScanAsync(ScanMode mode, CancellationToken ct = default);
    Task StopScanAsync();
    ScanStatus CurrentStatus { get; }

    // Frame Access
    Task<Frame> GetFrameAsync(TimeSpan timeout, CancellationToken ct = default);
    IAsyncEnumerable<Frame> StreamFramesAsync(CancellationToken ct = default);

    // Configuration
    Task SetConfigAsync(DetectorConfig config);
    Task<DetectorConfig> GetConfigAsync();

    // Storage
    Task SaveFrameAsync(Frame frame, string path, ImageFormat format);

    // Events
    event EventHandler<FrameReceivedEventArgs> FrameReceived;
    event EventHandler<ErrorEventArgs> ErrorOccurred;
    event EventHandler<ConnectionChangedEventArgs> ConnectionChanged;
}
```

### 3.2 Supporting Types

```csharp
// Scan modes
public enum ScanMode
{
    Single,       // Capture one frame, return to idle
    Continuous,   // Continuous capture until stopped
    Calibration   // Dark frame calibration (gate OFF)
}

// Scan status
public record ScanStatus(
    bool IsScanning,
    uint FrameCount,
    uint DroppedFrames,
    double Fps,
    PerformanceTier ActiveTier
);

// Detector information
public record DetectorInfo(
    string DeviceId,
    string FirmwareVersion,
    PerformanceTier MaxTier,
    int MaxWidth,
    int MaxHeight,
    int BitDepth
);

// Performance tier
public enum PerformanceTier
{
    Minimum,        // 1024x1024 @ 14-bit @ 15 fps
    IntermediateA,  // 2048x2048 @ 16-bit @ 15 fps
    IntermediateB,  // 2048x2048 @ 16-bit @ 30 fps
    Target          // 3072x3072 @ 16-bit @ 15 fps
}

// Image formats
public enum ImageFormat
{
    Raw,    // Unprocessed binary (width * height * 2 bytes)
    Tiff,   // 16-bit grayscale TIFF (lossless)
    Dicom   // DICOM format (optional, requires DICOM library)
}

// Frame data
public class Frame : IDisposable
{
    public uint SequenceNumber { get; }
    public long TimestampUs { get; }
    public int Width { get; }
    public int Height { get; }
    public int BitDepth { get; }
    public ReadOnlyMemory<ushort> PixelData { get; }

    // Pixel access
    public ushort GetPixel(int row, int col);
    public ReadOnlySpan<ushort> GetRow(int row);
}

// Configuration
public record DetectorConfig(
    int Width,
    int Height,
    int BitDepth,
    int TargetFps,
    ScanMode DefaultMode
);
```

### 3.3 Usage Examples

```csharp
// Basic single frame capture
using var client = new DetectorClient();
await client.ConnectAsync("192.168.1.100");
await client.StartScanAsync(ScanMode.Single);
var frame = await client.GetFrameAsync(TimeSpan.FromSeconds(5));
await client.SaveFrameAsync(frame, "capture_001.tiff", ImageFormat.Tiff);

// Continuous streaming with event handler
using var client = new DetectorClient();
await client.ConnectAsync("192.168.1.100");
client.FrameReceived += (sender, e) => {
    Console.WriteLine($"Frame {e.Frame.SequenceNumber}: {e.Frame.Width}x{e.Frame.Height}");
};
await client.StartScanAsync(ScanMode.Continuous);
// ... scan runs until stopped
await client.StopScanAsync();

// Async enumerable streaming
await foreach (var frame in client.StreamFramesAsync(cts.Token))
{
    await client.SaveFrameAsync(frame, $"frame_{frame.SequenceNumber:D4}.tiff", ImageFormat.Tiff);
}
```

---

## 4. Network Communication

### 4.1 PacketReceiver

Receives UDP packets from SoC on a dedicated high-priority thread.

```csharp
internal class PacketReceiver : IDisposable
{
    private readonly UdpClient _udpClient;
    private readonly Channel<RawPacket> _packetChannel;
    private readonly Thread _receiveThread;

    public PacketReceiver(int port, int receiveBufferSize = 16 * 1024 * 1024)
    {
        _udpClient = new UdpClient(port);
        _udpClient.Client.ReceiveBufferSize = receiveBufferSize;  // 16 MB
        _packetChannel = Channel.CreateBounded<RawPacket>(
            new BoundedChannelOptions(4096) { FullMode = BoundedChannelFullMode.DropOldest }
        );
    }

    // High-priority receive loop (dedicated thread)
    private void ReceiveLoop()
    {
        Thread.CurrentThread.Priority = ThreadPriority.Highest;
        var remoteEp = new IPEndPoint(IPAddress.Any, 0);

        while (!_disposed)
        {
            byte[] data = _udpClient.Receive(ref remoteEp);
            _packetChannel.Writer.TryWrite(new RawPacket(data, remoteEp));
        }
    }
}
```

### 4.2 PacketProtocol

Parses frame headers and validates CRC.

```csharp
internal static class PacketProtocol
{
    public const uint FrameMagic = 0xDEADBEEF;
    public const uint CommandMagic = 0xBEEFCAFE;
    public const uint ResponseMagic = 0xCAFEBEEF;
    public const int HeaderSize = 32;  // FrameHeader size

    public static bool TryParseHeader(ReadOnlySpan<byte> data, out FrameHeader header)
    {
        header = default;
        if (data.Length < HeaderSize) return false;

        header = new FrameHeader
        {
            Magic = BinaryPrimitives.ReadUInt32LittleEndian(data),
            FrameSeq = BinaryPrimitives.ReadUInt32LittleEndian(data[4..]),
            TimestampUs = BinaryPrimitives.ReadUInt64LittleEndian(data[8..]),
            Width = BinaryPrimitives.ReadUInt16LittleEndian(data[16..]),
            Height = BinaryPrimitives.ReadUInt16LittleEndian(data[18..]),
            BitDepth = BinaryPrimitives.ReadUInt16LittleEndian(data[20..]),
            PacketIndex = BinaryPrimitives.ReadUInt16LittleEndian(data[22..]),
            TotalPackets = BinaryPrimitives.ReadUInt16LittleEndian(data[24..]),
            Flags = BinaryPrimitives.ReadUInt16LittleEndian(data[26..]),
            Crc16 = BinaryPrimitives.ReadUInt16LittleEndian(data[28..]),
        };

        if (header.Magic != FrameMagic) return false;

        // Validate CRC (over header excluding CRC field)
        ushort calculatedCrc = Crc16.Calculate(data[..28]);
        return calculatedCrc == header.Crc16;
    }
}
```

---

## 5. Frame Reassembly

### 5.1 Reassembly Algorithm

The FrameReassembler handles out-of-order UDP packet delivery:

```
Incoming packets (may arrive out of order):
  [Frame 5, Pkt 3] [Frame 5, Pkt 1] [Frame 6, Pkt 0] [Frame 5, Pkt 0] [Frame 5, Pkt 2]

Reassembly buffer (per frame):
  Frame 5: [Pkt 0: ok] [Pkt 1: ok] [Pkt 2: ok] [Pkt 3: ok] --> COMPLETE
  Frame 6: [Pkt 0: ok] [Pkt 1: --] [Pkt 2: --] ...         --> PARTIAL

Completion check:
  received_count == total_packets --> Frame complete, emit to consumer
```

### 5.2 Implementation

```csharp
internal class FrameReassembler
{
    // Active reassembly slots (key: frame_seq)
    private readonly Dictionary<uint, ReassemblySlot> _slots = new();
    private readonly int _maxActiveSlots = 8;
    private readonly TimeSpan _slotTimeout = TimeSpan.FromSeconds(2);

    public Frame? ProcessPacket(FrameHeader header, ReadOnlySpan<byte> payload)
    {
        // Get or create reassembly slot
        if (!_slots.TryGetValue(header.FrameSeq, out var slot))
        {
            if (_slots.Count >= _maxActiveSlots)
                EvictOldestSlot();

            slot = new ReassemblySlot(header);
            _slots[header.FrameSeq] = slot;
        }

        // Insert packet payload at correct offset
        slot.InsertPacket(header.PacketIndex, payload);

        // Check if frame is complete
        if (slot.IsComplete)
        {
            _slots.Remove(header.FrameSeq);
            return slot.BuildFrame();
        }

        return null;  // Frame not yet complete
    }

    private void EvictOldestSlot()
    {
        // Remove oldest incomplete frame (timeout-based or lowest seq)
        var oldest = _slots.MinBy(kv => kv.Value.CreatedAt);
        if (oldest.Value != null)
        {
            _slots.Remove(oldest.Key);
            // Log: dropped incomplete frame
        }
    }
}

internal class ReassemblySlot
{
    private readonly byte[] _buffer;       // Full frame pixel buffer
    private readonly BitArray _received;   // Packet receipt bitmap
    private int _receivedCount;

    public bool IsComplete => _receivedCount == TotalPackets;

    public void InsertPacket(int packetIndex, ReadOnlySpan<byte> payload)
    {
        if (_received[packetIndex]) return;  // Duplicate packet

        int offset = packetIndex * MaxPayloadSize;
        payload.CopyTo(_buffer.AsSpan(offset));
        _received[packetIndex] = true;
        _receivedCount++;
    }

    public Frame BuildFrame()
    {
        var pixels = new ushort[Width * Height];
        Buffer.BlockCopy(_buffer, 0, pixels, 0, Width * Height * 2);
        return new Frame(FrameSeq, TimestampUs, Width, Height, BitDepth, pixels);
    }
}
```

### 5.3 Missing Packet Handling

| Strategy | Behavior | When Used |
|----------|----------|-----------|
| Wait | Hold slot open until timeout (2s) | Default, allows late packets |
| Zero-fill | Fill missing regions with 0x0000 | After timeout expires |
| Drop | Discard incomplete frame entirely | If > 10% packets missing |
| Retransmit | Request missing packets from SoC | Optional ARQ layer |

---

## 6. Storage Module

### 6.1 Supported Formats

| Format | Extension | Compression | Metadata | Library |
|--------|-----------|-------------|----------|---------|
| RAW | .raw | None | External sidecar (.json) | Built-in |
| TIFF | .tiff | None (16-bit) | TIFF tags (resolution, bit depth) | LibTiff.NET |
| DICOM | .dcm | Optional JPEG-LS | Full DICOM tags | fo-dicom (optional) |

### 6.2 RAW Format

```
File: capture_001.raw
Content: width * height * 2 bytes (little-endian uint16)
Sidecar: capture_001.json
{
    "width": 2048,
    "height": 2048,
    "bitDepth": 16,
    "frameSequence": 42,
    "timestampUs": 1708185600000000,
    "pixelFormat": "RAW16"
}
```

### 6.3 TIFF Format

```csharp
internal class TiffWriter
{
    public static void Save(Frame frame, string path)
    {
        using var tiff = Tiff.Open(path, "w");
        tiff.SetField(TiffTag.IMAGEWIDTH, frame.Width);
        tiff.SetField(TiffTag.IMAGELENGTH, frame.Height);
        tiff.SetField(TiffTag.BITSPERSAMPLE, 16);
        tiff.SetField(TiffTag.SAMPLESPERPIXEL, 1);
        tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISBLACK);
        tiff.SetField(TiffTag.COMPRESSION, Compression.NONE);
        tiff.SetField(TiffTag.ROWSPERSTRIP, frame.Height);

        // Write pixel data row by row
        byte[] rowBuffer = new byte[frame.Width * 2];
        for (int row = 0; row < frame.Height; row++)
        {
            var rowSpan = frame.GetRow(row);
            MemoryMarshal.AsBytes(rowSpan).CopyTo(rowBuffer);
            tiff.WriteScanline(rowBuffer, row);
        }
    }
}
```

---

## 7. Real-Time Display

### 7.1 Display Pipeline

```
Frame (16-bit)                     Display (8-bit)
    |                                   ^
    v                                   |
[Window/Level]  -->  [LUT Mapping]  -->  [WriteableBitmap]  -->  [WPF Image]
    |
    +-- brightness: center value (0-65535)
    +-- contrast: window width (1-65535)
```

### 7.2 Window/Level Mapping

```csharp
internal static class WindowLevel
{
    public static byte[] Apply(ReadOnlySpan<ushort> pixels, ushort center, ushort width)
    {
        byte[] output = new byte[pixels.Length];
        int halfWidth = width / 2;
        int minVal = center - halfWidth;
        int maxVal = center + halfWidth;

        for (int i = 0; i < pixels.Length; i++)
        {
            int val = pixels[i];
            if (val <= minVal) output[i] = 0;
            else if (val >= maxVal) output[i] = 255;
            else output[i] = (byte)((val - minVal) * 255 / width);
        }
        return output;
    }
}
```

### 7.3 Display Performance

| Tier | Frame Size | Display @ 15fps | CPU Load (est.) |
|------|-----------|-----------------|-----------------|
| Minimum | 1024x1024 | 2 MP x 15 = 30 MP/s | < 5% |
| Intermediate-A | 2048x2048 | 4 MP x 15 = 63 MP/s | ~10% |
| Target | 3072x3072 | 9.4 MP x 15 = 141 MP/s | ~25% |

For Target tier, display resolution may be downsampled (e.g., 1024x1024 preview) to maintain 15 fps.

---

## 8. Device Discovery

### 8.1 Discovery Protocol

```
Host PC (broadcast):
  UDP broadcast to 255.255.255.255:8002
  Payload: { magic: 0xD15C0000, version: 1, host_ip: "192.168.1.1" }

SoC (response):
  UDP unicast to host_ip:8002
  Payload: { magic: 0xD15C0001, device_id: "XR-DET-001", ip: "192.168.1.100",
             firmware_version: "1.0.0", max_tier: "target" }
```

### 8.2 Discovery API

```csharp
public static class DetectorDiscovery
{
    /// <summary>
    /// Discover detector devices on the local network.
    /// </summary>
    public static async Task<IReadOnlyList<DetectorInfo>> DiscoverAsync(
        TimeSpan timeout = default,
        CancellationToken ct = default)
    {
        timeout = timeout == default ? TimeSpan.FromSeconds(3) : timeout;
        // Broadcast discovery packet, collect responses
        // ...
    }
}
```

---

## 9. Threading Model

### 9.1 Thread Architecture

```
[Main Thread]           Application logic, GUI event loop
    |
    +-- [Receive Thread]    High-priority UDP packet reception (1 thread)
    |                       Reads from socket, writes to Channel<RawPacket>
    |
    +-- [Reassembly Thread] Packet processing and frame assembly (1 thread)
    |                       Reads from Channel<RawPacket>, writes to Channel<Frame>
    |
    +-- [TX Thread]         Command transmission to SoC (1 thread)
    |                       Serializes commands, sends via UDP port 8001
    |
    +-- [Storage Thread]    Asynchronous frame writing (1 thread, optional)
                            Reads from save queue, writes TIFF/RAW files
```

### 9.2 Thread Synchronization

| Resource | Mechanism | Notes |
|----------|-----------|-------|
| Raw packet buffer | `Channel<RawPacket>` (bounded, 4096) | Lock-free, drop-oldest |
| Completed frames | `Channel<Frame>` (bounded, 16) | Backpressure to reassembler |
| Reassembly slots | Dictionary + lock | Low contention (single writer) |
| Status updates | `volatile` fields | Single writer, multiple readers |
| Events | `SynchronizationContext` | Marshaled to UI thread (WPF) |

---

## 10. Error Handling

### 10.1 Error Categories

| Category | Source | SDK Response |
|----------|--------|-------------|
| Connection Lost | Socket exception | Raise ConnectionChanged event, attempt reconnect |
| Frame Timeout | No frame within timeout | Raise ErrorOccurred, return null from GetFrameAsync |
| Incomplete Frame | Missing packets after timeout | Zero-fill or drop, increment DroppedFrames |
| CRC Mismatch | Header CRC validation fails | Discard packet, log warning |
| Scan Error | SoC reports FPGA error | Raise ErrorOccurred with error code |
| Storage Error | Disk write failure | Raise ErrorOccurred, skip frame save |

### 10.2 Resilience Patterns

- **Auto-reconnect**: On connection loss, retry every 5 seconds (configurable)
- **Frame timeout**: Default 2 seconds per frame (configurable per tier)
- **Packet dedup**: Ignore duplicate packets (same frame_seq + packet_index)
- **Buffer overflow**: Drop oldest incomplete frames when slot limit reached

---

## 11. Project Structure

```
sdk/
  XrayDetector.Sdk/
    XrayDetector.Sdk.csproj           # SDK library project
    IDetectorClient.cs                # Public API interface
    DetectorClient.cs                 # Implementation
    DetectorDiscovery.cs              # Network discovery
    Models/
      Frame.cs                        # Frame data model
      DetectorInfo.cs                 # Device information
      DetectorConfig.cs               # Configuration model
      ScanMode.cs                     # Enumerations
    Internal/
      PacketReceiver.cs               # UDP reception thread
      PacketProtocol.cs               # Header parsing, CRC
      FrameReassembler.cs             # Out-of-order reassembly
      FrameValidator.cs               # Frame completeness check
      ImageEncoder.cs                 # TIFF/RAW/DICOM encoding
      WindowLevel.cs                  # Display mapping
  XrayDetector.Sdk.Tests/
    XrayDetector.Sdk.Tests.csproj     # Unit test project
    PacketProtocolTests.cs            # TDD: header parse, CRC
    FrameReassemblerTests.cs          # TDD: reassembly logic
    FrameValidatorTests.cs            # TDD: completeness checks
    WindowLevelTests.cs               # TDD: display mapping
    DetectorClientTests.cs            # TDD: API behavior
    CrcTests.cs                       # TDD: CRC reference vectors
```

---

## 12. Verification Strategy

### 12.1 Development Methodology

Per project `quality.yaml` (Hybrid mode):
- **All SDK code is NEW**: TDD (RED-GREEN-REFACTOR)
- Coverage target: 85%+

### 12.2 Test Cases

| ID | Test | Component | Criteria |
|----|------|-----------|---------|
| SDK-UT-01 | Frame header parse/serialize | PacketProtocol | Round-trip fidelity |
| SDK-UT-02 | CRC-16 calculation | PacketProtocol | Match reference vectors |
| SDK-UT-03 | In-order reassembly | FrameReassembler | Complete frame from sequential packets |
| SDK-UT-04 | Out-of-order reassembly | FrameReassembler | Complete frame from shuffled packets |
| SDK-UT-05 | Missing packet handling | FrameReassembler | Timeout + zero-fill or drop |
| SDK-UT-06 | Duplicate packet rejection | FrameReassembler | No data corruption from duplicates |
| SDK-UT-07 | Window/Level mapping | WindowLevel | Correct 16-bit to 8-bit conversion |
| SDK-UT-08 | TIFF write/read round-trip | ImageEncoder | Pixel-accurate save/load |
| SDK-UT-09 | RAW write/read round-trip | ImageEncoder | Binary-accurate save/load |
| SDK-UT-10 | Frame completeness check | FrameValidator | Detect missing/corrupt regions |
| SDK-IT-01 | Single frame capture | DetectorClient | End-to-end with HostSimulator |
| SDK-IT-02 | Continuous streaming | DetectorClient | 100 frames, < 1% drops |
| SDK-IT-03 | Connection loss recovery | DetectorClient | Auto-reconnect within 10s |

---

## 13. Design Decisions Log

| ID | Decision | Rationale | Date |
|----|----------|-----------|------|
| DD-SDK-01 | C# / .NET 8.0 | Cross-platform, rich ecosystem, WPF for GUI | 2026-02-17 |
| DD-SDK-02 | UDP (not TCP) for frame data | Lower latency, matches SoC TX protocol | 2026-02-17 |
| DD-SDK-03 | Channel<T> for thread communication | Lock-free, bounded, built-in .NET 8 | 2026-02-17 |
| DD-SDK-04 | Separate command port (8001) | Isolate control from data traffic | 2026-02-17 |
| DD-SDK-05 | IDetectorClient interface | Testability, mock-friendly, DI support | 2026-02-17 |
| DD-SDK-06 | ReadOnlyMemory<ushort> for pixel data | Zero-copy, GC-friendly, span support | 2026-02-17 |
| DD-SDK-07 | TIFF as primary storage format | Lossless, widely supported, 16-bit native | 2026-02-17 |
| DD-SDK-08 | 8-slot reassembly limit | Bounds memory usage, handles 8 concurrent frames | 2026-02-17 |

---

## 14. Performance Considerations

### 14.1 Memory Budget

| Tier | Frame Size | Recv Buffer | Reassembly (8 slots) | Total |
|------|-----------|-------------|---------------------|-------|
| Minimum | 2 MB | 16 MB | 16 MB | ~34 MB |
| Intermediate-A | 8 MB | 16 MB | 64 MB | ~88 MB |
| Target | 18 MB | 16 MB | 144 MB | ~178 MB |

### 14.2 Throughput Requirements

| Tier | Data Rate | Packets/s | CPU Usage (est.) |
|------|-----------|-----------|-----------------|
| Minimum | 30 MB/s | ~3,840 | < 5% |
| Intermediate-A | 120 MB/s | ~15,360 | ~15% |
| Target | 270 MB/s | ~34,560 | ~30% |

### 14.3 Optimization Strategies

- **Socket buffer**: 16 MB UDP receive buffer to absorb burst traffic
- **Pooled buffers**: ArrayPool<byte> for packet and frame buffers
- **SIMD**: Use Vector<ushort> for Window/Level mapping acceleration
- **Pinned memory**: GCHandle.Alloc for zero-copy DMA-friendly buffers

---

## 15. Document Traceability

**Implements**:
- SPEC-ARCH-001 (REQ-ARCH-008, REQ-ARCH-009, REQ-ARCH-015)
- X-ray_Detector_Optimal_Project_Plan.md Section 5.4 Phase 5 (Host SDK Development)
- System Architecture (docs/architecture/system-architecture.md) Layer 3

**References**:
- docs/architecture/soc-firmware-design.md (network protocol, frame header structure)
- docs/architecture/fpga-design.md (pixel format, data path)
- Common.Dto/ (ISimulator interface, shared DTOs)

**Feeds Into**:
- SPEC-SDK-001 (Host SDK detailed requirements)
- HostSimulator golden reference model
- GUI.Application (WPF viewer)
- IntegrationRunner (CLI test tool)

---

## 16. Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (architect) | Initial Host SDK architecture design document |

---

**Approval**:
- [ ] SDK Lead
- [ ] System Architect
- [ ] Project Manager

---

## Review Record

| Reviewer | Date | TRUST 5 Score | Decision |
|---------|------|--------------|---------|
| manager-quality | 2026-02-17 | T:5 R:5 U:5 S:5 T:5 | APPROVED |

### Review Notes

**TRUST 5 Assessment**

- **Testable (5/5)**: Section 12 defines 13 test cases (SDK-UT-01 through SDK-IT-03) covering all critical code paths. TDD methodology explicitly stated for all new SDK code. Coverage target 85%+ specified. Test project structure matches source (XrayDetector.Sdk.Tests/). Concrete pass criteria defined (e.g., "100 frames <1% drops", "auto-reconnect within 10s").
- **Readable (5/5)**: Component diagram with dependency arrows clearly shows module hierarchy and Common.Dto isolation. Public API (IDetectorClient) defined with C# XML documentation comments. Usage examples in Section 3.3 demonstrate three common patterns. Frame reassembly algorithm explained with ASCII packet-ordering diagram.
- **Unified (5/5)**: Network protocol matches soc-firmware-design.md exactly (FrameHeader 32 bytes, magic 0xDEADBEEF, port 8000, 8192-byte payload). Control channel on port 8001 consistent. Performance tiers (Minimum/IntermediateA/IntermediateB/Target) match system-architecture.md definitions. PerformanceTier enum values and frame sizes are consistent. BinaryPrimitives.ReadUInt32LittleEndian usage is consistent with FrameHeader little-endian declaration in soc-firmware-design.md.
- **Secured (5/5)**: Section 10 documents all 6 error categories with SDK responses. Resilience patterns include auto-reconnect, packet dedup (frame_seq + packet_index), buffer overflow drop policy, and configurable timeouts. Channel bounded capacity (4096 packets, 16 frames) prevents unbounded memory growth. CRC-16 validation on every packet header is enforced in PacketProtocol.TryParseHeader.
- **Trackable (5/5)**: Document metadata complete. Section 13 lists 8 design decisions with rationale and dates. Section 15 provides traceability to SPEC-ARCH-001 requirements, project plan, and downstream artifacts. Revision history present.

**Minor Observations (non-blocking)**

- Section 5.2 FrameReassembler uses `_slots.MinBy(kv => kv.Value.CreatedAt)` - MinBy returns KeyValuePair, so `oldest.Value` on the next line is checking the wrong type (should be `oldest.Key` and `oldest.Value`). The eviction logic looks correct structurally but may have a minor code inconsistency. Recommend noting in SPEC-SDK-001 to verify this in TDD test SDK-UT-05 (missing packet handling / eviction).
- Window/Level Apply method does integer arithmetic `(val - minVal) * 255 / width` which can overflow for large 16-bit values. Recommend casting to long or using checked arithmetic. SIMD optimization path with Vector<ushort> mentioned in Section 14.3 - add SDK-UT-07 to explicitly test boundary conditions (minVal=0, maxVal=65535).
- DICOM support is documented as "optional, requires DICOM library (fo-dicom)" - acceptable deferral for Phase 1.
