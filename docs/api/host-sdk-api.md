# Host SDK API Reference

**Document Version**: 1.0.0
**Status**: Reviewed
**Last Updated**: 2026-02-17

---

## Overview

The XrayDetector.SDK provides a managed .NET 8.0 client library for controlling X-ray detector devices and receiving frame data. The SDK encapsulates the UDP frame protocol, packet reassembly, device discovery, and image storage.

**Namespace**: XrayDetector.SDK

**Target frameworks**: .NET 8.0 (Windows 10+, Linux Ubuntu 22.04+)

**Key capabilities**:
- Async/await connection and scan control
- Automatic UDP packet reassembly with out-of-order handling
- Event-based and async enumerable frame streaming
- Network device discovery via UDP broadcast
- TIFF, RAW, and DICOM frame storage
- Thread-safe implementation with pooled buffers

---

## 1. Installation

### 1.1 NuGet Package

Install from the internal NuGet feed:

```bash
dotnet add package XrayDetector.SDK --version 1.0.0
```

Or add directly to the project file:

```xml
<ItemGroup>
  <PackageReference Include="XrayDetector.SDK" Version="1.0.0" />
</ItemGroup>
```

### 1.2 NuGet Package Configuration

The SDK package is hosted on an internal feed. Configure the NuGet source in `nuget.config` at the solution root:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="internal" value="https://gitea.internal/api/packages/xray-team/nuget/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <internal>
      <add key="Username" value="%NUGET_USERNAME%" />
      <add key="ClearTextPassword" value="%NUGET_TOKEN%" />
    </internal>
  </packageSourceCredentials>
</configuration>
```

Set environment variables before restoring packages:
- `NUGET_USERNAME`: Gitea username
- `NUGET_TOKEN`: Gitea personal access token with `package:read` scope

### 1.3 Project Reference (Development)

When working within the monorepo, reference the SDK project directly:

```xml
<ItemGroup>
  <ProjectReference Include="../XrayDetector.Sdk/XrayDetector.Sdk.csproj" />
</ItemGroup>
```

### 1.4 Platform Requirements

| Requirement | Minimum | Recommended |
|-------------|---------|-------------|
| .NET runtime | 8.0 LTS | 8.0 LTS |
| OS | Windows 10 / Ubuntu 22.04 | Windows 11 / Ubuntu 22.04 |
| NIC | 1 GbE | 10 GbE |
| RAM | 8 GB | 16 GB |
| Storage | HDD | NVMe SSD |

**Note**: 1 GbE NIC is only sufficient for the Minimum tier (1024x1024@15fps). Intermediate-A and Target tiers require a 10 GbE connection.

---

## 2. Quick Start

```csharp
using XrayDetector.Sdk;

// Single frame capture
using var client = new DetectorClient();
await client.ConnectAsync("192.168.1.100");

await client.StartAcquisitionAsync(new AcquisitionParams(ScanMode.Single, PerformanceTier.IntermediateA));
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
var frame = await client.CaptureFrameAsync(cts.Token);

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

#### StartAcquisitionAsync

```csharp
Task StartAcquisitionAsync(AcquisitionParams parameters, CancellationToken ct = default);
```

Starts frame acquisition.

| Parameter | Type | Description |
|-----------|------|-------------|
| parameters | AcquisitionParams | Acquisition mode, tier, and optional frame count |
| ct | CancellationToken | Cancellation token |

**Throws**: `InvalidOperationException` if not connected or scan already active.

**Example**:
```csharp
// Single frame capture
await client.StartAcquisitionAsync(new AcquisitionParams(ScanMode.Single, PerformanceTier.IntermediateA));

// Continuous streaming
await client.StartAcquisitionAsync(new AcquisitionParams(ScanMode.Continuous, PerformanceTier.Target));

// Dark frame calibration
await client.StartAcquisitionAsync(new AcquisitionParams(ScanMode.Calibration, PerformanceTier.Minimum));
```

---

#### StopAcquisitionAsync

```csharp
Task StopAcquisitionAsync();
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

#### CaptureFrameAsync

```csharp
Task<Frame> CaptureFrameAsync(CancellationToken ct = default);
```

Waits for and returns the next complete frame.

| Parameter | Type | Description |
|-----------|------|-------------|
| ct | CancellationToken | Cancellation token (use with timeout via CancellationTokenSource) |

**Returns**: `Frame` object with pixel data.

**Throws**: `TimeoutException` if no frame received within timeout (set via CancellationTokenSource).

**Example**:
```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
var frame = await client.CaptureFrameAsync(cts.Token);
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
await client.StartAcquisitionAsync(new AcquisitionParams(ScanMode.Continuous, PerformanceTier.Target));

int count = 0;
await foreach (var frame in client.StreamFramesAsync(cts.Token))
{
    await client.SaveFrameAsync(frame, $"frame_{count:D4}.tiff", ImageFormat.Tiff);
    count++;
    if (count >= 100) break;
}

await client.StopAcquisitionAsync();
```

---

#### ConfigureAsync

```csharp
Task ConfigureAsync(DetectorConfig config);
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
await client.ConfigureAsync(config);
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
event EventHandler<DetectorErrorEventArgs> ErrorOccurred;
```

Raised when an error occurs (FPGA error, network error, etc.).

```csharp
public class DetectorErrorEventArgs : EventArgs
{
    public DetectorError Error { get; }
    public string Message { get; }
    public bool IsRecoverable { get; }
}
```

---

#### ConnectionChanged

```csharp
event EventHandler<ConnectionStateChangedEventArgs> ConnectionChanged;
```

Raised when connection state changes (connected, disconnected, reconnecting).

```csharp
public class ConnectionStateChangedEventArgs : EventArgs
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

### 4.4 DetectorStatus

```csharp
/// <summary>
/// Comprehensive status snapshot from the detector system.
/// Retrieved via GetStatusAsync() for polling-based monitoring.
/// </summary>
public record DetectorStatus(
    bool IsScanning,                 // true if acquisition is active
    ScanMode ActiveMode,             // Current scan mode
    PerformanceTier ActiveTier,      // Current performance tier
    uint FrameCount,                 // Total frames captured in current session
    uint DroppedFrames,              // Frames dropped due to buffer overrun
    double CurrentFps,               // Measured frames per second (rolling average)
    FpgaStatus FpgaState,            // FPGA subsystem status
    double SoCTemperatureCelsius,    // SoC die temperature
    TimeSpan SystemUptime            // Time since SoC daemon started
);

public record FpgaStatus(
    string FsmState,                 // Current FSM state: "IDLE", "INTEGRATE", etc.
    bool CsiPhyReady,                // True if D-PHY link is established
    uint FpgaErrorFlags,             // ERROR_FLAGS register value (0 = no errors)
    uint TxFrameCount,               // FPGA-side transmitted frame counter
    uint TxErrorCount                // FPGA-side CSI-2 error counter
);
```

### 4.5 Enumerations

```csharp
namespace XrayDetector.SDK;

public enum ScanMode { Single, Continuous, Calibration }
public enum ImageFormat { Raw, Tiff, Dicom }
public enum PerformanceTier
{
    Minimum,        // 1024x1024 @ 14-bit @ 15fps (~0.21 Gbps)
    IntermediateA,  // 2048x2048 @ 16-bit @ 15fps (~1.01 Gbps)
    IntermediateB,  // 2048x2048 @ 16-bit @ 30fps (~2.01 Gbps)
    Target          // 3072x3072 @ 16-bit @ 15fps (~2.26 Gbps)
}
public enum ConnectionState { Disconnected, Connecting, Connected, Reconnecting }
```

---

## 5. Static Utilities

### 5.1 IDetectorClient (complete interface)

The complete interface definition including all methods:

```csharp
namespace XrayDetector.SDK;

/// <summary>
/// Primary API for controlling an X-ray detector device.
/// All async methods are thread-safe.
/// </summary>
public interface IDetectorClient : IAsyncDisposable
{
    // Connection management
    Task ConnectAsync(string host, int port = 8000, CancellationToken ct = default);
    Task DisconnectAsync();
    bool IsConnected { get; }
    DetectorInfo DeviceInfo { get; }

    // Scan control
    Task StartAcquisitionAsync(AcquisitionParams parameters, CancellationToken ct = default);
    Task StopAcquisitionAsync();
    ScanStatus CurrentStatus { get; }

    // Frame access (two patterns: polling and streaming)
    Task<Frame> CaptureFrameAsync(CancellationToken ct = default);
    IAsyncEnumerable<Frame> StreamFramesAsync(CancellationToken ct = default);

    // Configuration
    Task ConfigureAsync(DetectorConfig config);
    Task<DetectorConfig> GetConfigAsync();
    Task<DetectorStatus> GetStatusAsync();

    // Device discovery
    Task<IEnumerable<DeviceInfo>> DiscoverDevicesAsync(
        TimeSpan timeout = default, CancellationToken ct = default);

    // Storage
    Task SaveFrameAsync(Frame frame, string path, ImageFormat format);

    // Events
    event EventHandler<FrameReceivedEventArgs> FrameReceived;
    event EventHandler<DetectorErrorEventArgs> ErrorOccurred;
    event EventHandler<ConnectionStateChangedEventArgs> ConnectionChanged;
}
```

### 5.2 DetectorDiscovery

```csharp
public static class DetectorDiscovery
{
    /// <summary>
    /// Discovers X-ray detector devices on the local network via UDP broadcast on port 8001.
    /// Broadcasts a JSON discovery request and collects unicast responses within the timeout period.
    /// </summary>
    /// <param name="timeout">Discovery timeout (default: 3 seconds)</param>
    /// <param name="ct">Cancellation token for early termination</param>
    /// <returns>List of DeviceInfo for all responding detectors</returns>
    public static async Task<IEnumerable<DeviceInfo>> DiscoverDevicesAsync(
        TimeSpan timeout = default,
        CancellationToken ct = default);
}
```

**Example**:
```csharp
// Discover with 5-second timeout
var devices = await DetectorDiscovery.DiscoverDevicesAsync(TimeSpan.FromSeconds(5));
foreach (var device in devices)
{
    Console.WriteLine($"Found: {device.DeviceId} at {device.IpAddress}:{device.DataPort}");
    Console.WriteLine($"  Firmware: {device.FirmwareVersion}, MaxTier: {device.MaxTier}");
}

// Connect to first discovered device
if (devices.Any())
{
    var firstDevice = devices.First();
    using var client = new DetectorClient();
    await client.ConnectAsync(firstDevice.IpAddress, firstDevice.DataPort);
}
```

### 5.3 DeviceInfo

```csharp
public record DeviceInfo(
    string DeviceId,             // e.g., "XR-DET-001"
    string IpAddress,            // e.g., "192.168.1.100"
    int DataPort,                // Frame data port (default 8000)
    int CommandPort,             // Control port (default 8001)
    string FirmwareVersion,      // e.g., "1.0.0"
    PerformanceTier MaxTier,     // Maximum supported performance tier
    int MaxWidth,                // Maximum frame width in pixels
    int MaxHeight,               // Maximum frame height in pixels
    int MaxBitDepth              // Maximum pixel bit depth
);
```

### 5.4 AcquisitionParams

```csharp
public record AcquisitionParams(
    ScanMode Mode,                    // Single, Continuous, or Calibration
    PerformanceTier Tier,             // Target performance tier
    int? MaxFrames = null             // Maximum frames to capture (null = unlimited for Continuous)
);
```

---

## 6. Advanced Usage

### 6.1 Custom Frame Processing Pipeline

```csharp
// Process frames in parallel with async pipeline
await client.StartAcquisitionAsync(new AcquisitionParams(ScanMode.Continuous, PerformanceTier.Target));

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
    client1.StartAcquisitionAsync(new AcquisitionParams(ScanMode.Single, PerformanceTier.IntermediateA)),
    client2.StartAcquisitionAsync(new AcquisitionParams(ScanMode.Single, PerformanceTier.IntermediateA))
);

using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(5));
using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(5));
var frame1 = await client1.CaptureFrameAsync(cts1.Token);
var frame2 = await client2.CaptureFrameAsync(cts2.Token);
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
- `StartAcquisitionAsync` / `StopAcquisitionAsync`
- `CaptureFrameAsync` (single consumer pattern)

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

**Implements**: `docs/architecture/host-sdk-design.md`

**References**:
- `docs/api/ethernet-protocol.md` (UDP frame protocol: header structure, port assignments, discovery)
- `docs/api/spi-register-map.md` (FPGA register definitions for status and control)
- `docs/api/csi2-packet-format.md` (pixel format reference for RAW14/RAW16 interpretation)

**Feeds Into**:
- SPEC-SDK-001 (Host SDK detailed requirements)
- HostSimulator golden reference model
- GUI.Application (WPF real-time viewer)
- IntegrationRunner (CLI test harness)

---

## 11. Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (manager-docs) | Complete API reference with IDetectorClient, data models, DeviceInfo, DetectorStatus, FpgaStatus, AcquisitionParams, NuGet configuration, DiscoverDevicesAsync |
| 1.0.1 | 2026-02-17 | manager-quality | Fixed: API inconsistencies between Section 3 (StartScanAsync/StopScanAsync/GetFrameAsync) and Section 5.1 IDetectorClient interface (StartAcquisitionAsync/StopAcquisitionAsync/CaptureFrameAsync); aligned ErrorEventArgs -> DetectorErrorEventArgs, ConnectionChangedEventArgs -> ConnectionStateChangedEventArgs across all sections |

---

## Review Record

- Date: 2026-02-17
- Reviewer: manager-quality
- Status: Approved (with corrections applied)
- TRUST 5: T:5 R:4 U:3 S:5 T:5
