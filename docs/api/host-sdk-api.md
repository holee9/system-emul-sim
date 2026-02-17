# Host SDK API Reference

**Project**: X-ray Detector Panel System
**SDK**: XrayDetector.Sdk (.NET 8.0+, C#)
**Version**: 1.0.0
**Last Updated**: 2026-02-17

---

## 1. Installation

### NuGet Package

```bash
dotnet add package XrayDetector.Sdk
```

### Project Reference

```xml
<ItemGroup>
    <ProjectReference Include="../XrayDetector.Sdk/XrayDetector.Sdk.csproj" />
</ItemGroup>
```

---

## 2. Quick Start

```csharp
using XrayDetector.Sdk;

// Single frame capture
using var client = new DetectorClient();
await client.ConnectAsync("192.168.1.100");

await client.StartScanAsync(ScanMode.Single);
var frame = await client.GetFrameAsync(TimeSpan.FromSeconds(5));

Console.WriteLine($"Captured: {frame.Width}x{frame.Height}, {frame.BitDepth}-bit");
await client.SaveFrameAsync(frame, "capture.tiff", ImageFormat.Tiff);
```

---

## 3. Core API

### 3.1 IDetectorClient

The primary interface for all detector operations.

#### ConnectAsync

```csharp
Task ConnectAsync(string host, int port = 8000, CancellationToken ct = default);
```

Establishes connection to a detector device.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| host | string | (required) | SoC IP address or hostname |
| port | int | 8000 | Data port number |
| ct | CancellationToken | default | Cancellation token |

**Throws**: `DetectorConnectionException` if connection fails within 10 seconds.

**Example**:
```csharp
var client = new DetectorClient();
await client.ConnectAsync("192.168.1.100");
// or with custom port:
await client.ConnectAsync("192.168.1.100", 9000);
```

---

#### DisconnectAsync

```csharp
Task DisconnectAsync();
```

Gracefully disconnects from the detector. Stops any active scan.

---

#### IsConnected

```csharp
bool IsConnected { get; }
```

Returns `true` if the client is connected to a detector device.

---

#### DeviceInfo

```csharp
DetectorInfo DeviceInfo { get; }
```

Returns device information after connection. `null` if not connected.

---

#### StartScanAsync

```csharp
Task StartScanAsync(ScanMode mode, CancellationToken ct = default);
```

Starts frame acquisition.

| Parameter | Type | Description |
|-----------|------|-------------|
| mode | ScanMode | Single, Continuous, or Calibration |
| ct | CancellationToken | Cancellation token |

**Throws**: `InvalidOperationException` if not connected or scan already active.

**Example**:
```csharp
// Single frame capture
await client.StartScanAsync(ScanMode.Single);

// Continuous streaming
await client.StartScanAsync(ScanMode.Continuous);

// Dark frame calibration
await client.StartScanAsync(ScanMode.Calibration);
```

---

#### StopScanAsync

```csharp
Task StopScanAsync();
```

Stops an active continuous scan. No-op if scan is not active.

---

#### CurrentStatus

```csharp
ScanStatus CurrentStatus { get; }
```

Returns current scan status.

```csharp
var status = client.CurrentStatus;
Console.WriteLine($"Scanning: {status.IsScanning}");
Console.WriteLine($"Frames: {status.FrameCount}");
Console.WriteLine($"Dropped: {status.DroppedFrames}");
Console.WriteLine($"FPS: {status.Fps:F1}");
```

---

#### GetFrameAsync

```csharp
Task<Frame> GetFrameAsync(TimeSpan timeout, CancellationToken ct = default);
```

Waits for and returns the next complete frame.

| Parameter | Type | Description |
|-----------|------|-------------|
| timeout | TimeSpan | Maximum wait time for frame |
| ct | CancellationToken | Cancellation token |

**Returns**: `Frame` object with pixel data.

**Throws**: `TimeoutException` if no frame received within timeout.

**Example**:
```csharp
var frame = await client.GetFrameAsync(TimeSpan.FromSeconds(5));
ushort centerPixel = frame.GetPixel(frame.Height / 2, frame.Width / 2);
```

---

#### StreamFramesAsync

```csharp
IAsyncEnumerable<Frame> StreamFramesAsync(CancellationToken ct = default);
```

Returns an async stream of frames for continuous capture.

**Example**:
```csharp
await client.StartScanAsync(ScanMode.Continuous);

int count = 0;
await foreach (var frame in client.StreamFramesAsync(cts.Token))
{
    await client.SaveFrameAsync(frame, $"frame_{count:D4}.tiff", ImageFormat.Tiff);
    count++;
    if (count >= 100) break;
}

await client.StopScanAsync();
```

---

#### SetConfigAsync

```csharp
Task SetConfigAsync(DetectorConfig config);
```

Updates detector configuration.

| Parameter | Type | Description |
|-----------|------|-------------|
| config | DetectorConfig | New configuration parameters |

**Note**: Some parameters require scan restart (resolution, bit depth, lane speed). The SDK handles this automatically.

**Example**:
```csharp
var config = new DetectorConfig(
    Width: 2048,
    Height: 2048,
    BitDepth: 16,
    TargetFps: 15,
    DefaultMode: ScanMode.Single
);
await client.SetConfigAsync(config);
```

---

#### GetConfigAsync

```csharp
Task<DetectorConfig> GetConfigAsync();
```

Retrieves current detector configuration.

---

#### SaveFrameAsync

```csharp
Task SaveFrameAsync(Frame frame, string path, ImageFormat format);
```

Saves a frame to disk.

| Parameter | Type | Description |
|-----------|------|-------------|
| frame | Frame | Frame to save |
| path | string | Output file path |
| format | ImageFormat | Raw, Tiff, or Dicom |

**Example**:
```csharp
// TIFF (recommended for medical imaging)
await client.SaveFrameAsync(frame, "image.tiff", ImageFormat.Tiff);

// RAW binary (with JSON sidecar)
await client.SaveFrameAsync(frame, "image.raw", ImageFormat.Raw);
```

---

### 3.2 Events

#### FrameReceived

```csharp
event EventHandler<FrameReceivedEventArgs> FrameReceived;
```

Raised when a complete frame is received.

```csharp
public class FrameReceivedEventArgs : EventArgs
{
    public Frame Frame { get; }
    public int QueueDepth { get; }  // Frames waiting in buffer
}
```

**Example**:
```csharp
client.FrameReceived += (sender, e) => {
    Console.WriteLine($"Frame #{e.Frame.SequenceNumber} received ({e.QueueDepth} queued)");
};
```

---

#### ErrorOccurred

```csharp
event EventHandler<ErrorEventArgs> ErrorOccurred;
```

Raised when an error occurs (FPGA error, network error, etc.).

```csharp
public class ErrorEventArgs : EventArgs
{
    public DetectorError Error { get; }
    public string Message { get; }
    public bool IsRecoverable { get; }
}
```

---

#### ConnectionChanged

```csharp
event EventHandler<ConnectionChangedEventArgs> ConnectionChanged;
```

Raised when connection state changes (connected, disconnected, reconnecting).

```csharp
public class ConnectionChangedEventArgs : EventArgs
{
    public ConnectionState State { get; }  // Connected, Disconnected, Reconnecting
    public string Reason { get; }
}
```

---

## 4. Data Types

### 4.1 Frame

```csharp
public class Frame : IDisposable
{
    // Metadata
    public uint SequenceNumber { get; }
    public long TimestampUs { get; }
    public int Width { get; }
    public int Height { get; }
    public int BitDepth { get; }

    // Pixel access
    public ReadOnlyMemory<ushort> PixelData { get; }
    public ushort GetPixel(int row, int col);
    public ReadOnlySpan<ushort> GetRow(int row);

    // Statistics
    public ushort MinValue { get; }
    public ushort MaxValue { get; }
    public double MeanValue { get; }
}
```

**Memory Management**: Frames use pooled buffers internally. Call `Dispose()` to return the buffer to the pool. Frames not disposed are reclaimed by the garbage collector.

---

### 4.2 DetectorConfig

```csharp
public record DetectorConfig(
    int Width,          // 1024, 2048, or 3072
    int Height,         // 1024, 2048, or 3072
    int BitDepth,       // 14 or 16
    int TargetFps,      // 10, 15, or 30
    ScanMode DefaultMode
);
```

---

### 4.3 DetectorInfo

```csharp
public record DetectorInfo(
    string DeviceId,            // e.g., "XR-DET-001"
    string FirmwareVersion,     // e.g., "1.0.0"
    PerformanceTier MaxTier,    // Maximum supported tier
    int MaxWidth,               // Maximum width (3072)
    int MaxHeight,              // Maximum height (3072)
    int BitDepth                // Maximum bit depth (16)
);
```

---

### 4.4 Enumerations

```csharp
public enum ScanMode { Single, Continuous, Calibration }
public enum ImageFormat { Raw, Tiff, Dicom }
public enum PerformanceTier { Minimum, IntermediateA, IntermediateB, Target }
public enum ConnectionState { Disconnected, Connecting, Connected, Reconnecting }
```

---

## 5. Static Utilities

### 5.1 DetectorDiscovery

```csharp
public static class DetectorDiscovery
{
    /// <summary>
    /// Discover detector devices on the local network via UDP broadcast.
    /// </summary>
    /// <param name="timeout">Discovery timeout (default: 3 seconds)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of discovered detectors</returns>
    public static async Task<IReadOnlyList<DetectorInfo>> DiscoverAsync(
        TimeSpan timeout = default,
        CancellationToken ct = default);
}
```

**Example**:
```csharp
var detectors = await DetectorDiscovery.DiscoverAsync(TimeSpan.FromSeconds(5));
foreach (var det in detectors)
{
    Console.WriteLine($"Found: {det.DeviceId} at {det.FirmwareVersion}");
}

if (detectors.Count > 0)
{
    var client = new DetectorClient();
    await client.ConnectAsync(detectors[0].DeviceId);
}
```

---

## 6. Advanced Usage

### 6.1 Custom Frame Processing Pipeline

```csharp
// Process frames in parallel with async pipeline
await client.StartScanAsync(ScanMode.Continuous);

var processChannel = Channel.CreateBounded<Frame>(8);

// Producer: receive frames
var producer = Task.Run(async () => {
    await foreach (var frame in client.StreamFramesAsync(cts.Token))
    {
        await processChannel.Writer.WriteAsync(frame);
    }
    processChannel.Writer.Complete();
});

// Consumer: process frames
var consumer = Task.Run(async () => {
    await foreach (var frame in processChannel.Reader.ReadAllAsync())
    {
        using (frame)
        {
            // Custom processing
            var stats = AnalyzeFrame(frame);
            await SaveResults(stats);
        }
    }
});

await Task.WhenAll(producer, consumer);
```

### 6.2 Multiple Detector Support

```csharp
// Connect to two detectors simultaneously
var client1 = new DetectorClient();
var client2 = new DetectorClient();

await Task.WhenAll(
    client1.ConnectAsync("192.168.1.100"),
    client2.ConnectAsync("192.168.1.101")
);

// Synchronized capture
await Task.WhenAll(
    client1.StartScanAsync(ScanMode.Single),
    client2.StartScanAsync(ScanMode.Single)
);

var frame1 = await client1.GetFrameAsync(TimeSpan.FromSeconds(5));
var frame2 = await client2.GetFrameAsync(TimeSpan.FromSeconds(5));
```

### 6.3 Window/Level Display Integration

```csharp
// For WPF integration
public WriteableBitmap CreateDisplayBitmap(Frame frame, ushort center, ushort window)
{
    byte[] displayPixels = WindowLevel.Apply(frame.PixelData.Span, center, window);

    var bitmap = new WriteableBitmap(
        frame.Width, frame.Height,
        96, 96,
        PixelFormats.Gray8,
        null
    );

    bitmap.Lock();
    Marshal.Copy(displayPixels, 0, bitmap.BackBuffer, displayPixels.Length);
    bitmap.AddDirtyRect(new Int32Rect(0, 0, frame.Width, frame.Height));
    bitmap.Unlock();

    return bitmap;
}
```

---

## 7. Error Types

### 7.1 Exception Hierarchy

```
Exception
  +-- DetectorException (base)
        +-- DetectorConnectionException
        +-- DetectorScanException
        +-- DetectorConfigException
        +-- DetectorStorageException
```

### 7.2 DetectorError Codes

| Code | Name | Description |
|------|------|-------------|
| 0x0001 | CONNECTION_LOST | Network connection to SoC lost |
| 0x0002 | FRAME_TIMEOUT | No frame received within timeout |
| 0x0003 | INCOMPLETE_FRAME | Frame has missing packets |
| 0x0004 | CRC_ERROR | Packet header CRC mismatch |
| 0x0010 | FPGA_TIMEOUT | FPGA readout timeout |
| 0x0020 | FPGA_OVERFLOW | FPGA buffer overflow |
| 0x0040 | FPGA_DPHY_ERROR | D-PHY link failure |
| 0x0100 | SCAN_REJECTED | Scan command rejected by SoC |
| 0x0200 | CONFIG_INVALID | Invalid configuration parameter |

---

## 8. Thread Safety

### 8.1 Thread-Safe Operations

The following operations are safe to call from any thread:
- `IsConnected` (property read)
- `CurrentStatus` (property read)
- `DeviceInfo` (property read)
- Event subscription/unsubscription

### 8.2 Non-Thread-Safe Operations

The following operations must not be called concurrently:
- `ConnectAsync` / `DisconnectAsync`
- `StartScanAsync` / `StopScanAsync`
- `GetFrameAsync` (single consumer pattern)

### 8.3 Event Threading

Events are raised on the thread pool by default. For WPF applications, use `SynchronizationContext`:

```csharp
client.FrameReceived += (sender, e) => {
    // This runs on thread pool thread
    Dispatcher.Invoke(() => {
        // This runs on UI thread
        UpdateDisplay(e.Frame);
    });
};
```

---

## 9. Performance Guidelines

### 9.1 Recommended Practices

1. **Dispose frames promptly** to return buffers to the pool
2. **Use streaming API** (`StreamFramesAsync`) for continuous capture
3. **Process frames asynchronously** to avoid blocking the receive pipeline
4. **Enable jumbo frames** (MTU 9000) on both SoC and Host NICs
5. **Increase UDP receive buffer** to 16 MB for high-throughput tiers

### 9.2 Buffer Configuration

```csharp
var options = new DetectorClientOptions
{
    ReceiveBufferSize = 16 * 1024 * 1024,  // 16 MB UDP buffer
    MaxReassemblySlots = 8,                 // Max concurrent frame reassembly
    FrameTimeoutMs = 2000,                  // Frame completion timeout
    AutoReconnect = true,                   // Auto-reconnect on disconnect
    ReconnectIntervalMs = 5000,             // Reconnect retry interval
};

var client = new DetectorClient(options);
```

---

## 10. Document Traceability

**Implements**: docs/architecture/host-sdk-design.md

**References**:
- docs/api/ethernet-protocol.md (network protocol specification)
- docs/api/spi-register-map.md (FPGA control registers)

---
