# User Manual

**Project**: X-ray Detector Panel System
**SDK**: XrayDetector.Sdk (.NET 8.0+)
**Version**: 1.0.0
**Last Updated**: 2026-02-17

---

## 1. Introduction

This manual describes how to operate the X-ray Detector Panel System using the Host SDK. The system captures X-ray images through a layered hardware/software pipeline:

```
X-ray Panel -> FPGA -> SoC Controller -> Host PC (your workstation)
```

The Host SDK (`XrayDetector.Sdk`) provides a C# API to connect to the detector, configure imaging parameters, capture frames, and save images to disk.

### 1.1 Intended Audience

This manual is intended for:

- Equipment operators who capture and store X-ray images
- Application developers integrating the detector into custom software
- Quality assurance engineers performing acceptance testing

### 1.2 System Requirements

| Requirement | Specification |
|-------------|---------------|
| Operating System | Windows 10/11 (64-bit) or Linux (x64) |
| Runtime | .NET 8.0 or later |
| Network | Ethernet connection to SoC controller |
| Recommended NIC | 10 GbE (for Target/Maximum resolution tiers) |
| Minimum NIC | 1 GbE (for Minimum resolution tier only) |
| RAM | 8 GB minimum, 16 GB recommended |
| Disk Space | 500 MB for SDK; storage varies by capture volume |

### 1.3 Performance Tiers

The detector supports three resolution tiers. Your network connection determines which tiers are available:

| Tier | Resolution | Bit Depth | Max FPS | Data Rate | Required Network |
|------|-----------|-----------|---------|-----------|-----------------|
| Minimum | 1024 x 1024 | 14-bit | 15 fps | ~0.21 Gbps | 1 GbE |
| Target | 2048 x 2048 | 16-bit | 30 fps | ~2.01 Gbps | 10 GbE |
| Maximum | 3072 x 3072 | 16-bit | 30 fps | ~4.53 Gbps | 10 GbE |

---

## 2. Getting Started

### 2.1 Installation

Install the SDK via NuGet:

```bash
dotnet add package XrayDetector.Sdk
```

Or add a project reference if working from source:

```xml
<ItemGroup>
    <ProjectReference Include="../XrayDetector.Sdk/XrayDetector.Sdk.csproj" />
</ItemGroup>
```

### 2.2 Network Configuration

1. Connect the Host PC to the same network as the SoC controller.
2. Verify connectivity:
   ```bash
   ping 192.168.1.100
   ```
3. For 10 GbE connections, enable jumbo frames (MTU 9000) on the Host NIC for optimal throughput.

### 2.3 First Connection

```csharp
using XrayDetector.Sdk;

// Create a client and connect
using var client = new DetectorClient();
await client.ConnectAsync("192.168.1.100");

// Verify connection
Console.WriteLine($"Connected: {client.IsConnected}");
Console.WriteLine($"Device: {client.DeviceInfo.DeviceId}");
Console.WriteLine($"Firmware: {client.DeviceInfo.FirmwareVersion}");
```

---

## 3. Basic Operations

### 3.1 Single Frame Capture

Capture a single X-ray image:

```csharp
using var client = new DetectorClient();
await client.ConnectAsync("192.168.1.100");

// Start a single-frame scan
await client.StartScanAsync(ScanMode.Single);

// Wait for the frame (5-second timeout)
var frame = await client.GetFrameAsync(TimeSpan.FromSeconds(5));

Console.WriteLine($"Captured: {frame.Width}x{frame.Height}, {frame.BitDepth}-bit");
Console.WriteLine($"Min: {frame.MinValue}, Max: {frame.MaxValue}, Mean: {frame.MeanValue:F1}");

// Save to TIFF
await client.SaveFrameAsync(frame, "capture.tiff", ImageFormat.Tiff);
```

### 3.2 Continuous Capture

Capture a series of frames continuously:

```csharp
using var client = new DetectorClient();
await client.ConnectAsync("192.168.1.100");

await client.StartScanAsync(ScanMode.Continuous);

int count = 0;
await foreach (var frame in client.StreamFramesAsync())
{
    await client.SaveFrameAsync(frame, $"frame_{count:D4}.tiff", ImageFormat.Tiff);
    frame.Dispose();  // Return buffer to pool
    count++;

    if (count >= 100) break;
}

await client.StopScanAsync();
Console.WriteLine($"Captured {count} frames.");
```

### 3.3 Calibration Capture

Perform dark frame calibration:

```csharp
// Block X-ray source before calibration
await client.StartScanAsync(ScanMode.Calibration);
var darkFrame = await client.GetFrameAsync(TimeSpan.FromSeconds(5));
await client.SaveFrameAsync(darkFrame, "dark_frame.tiff", ImageFormat.Tiff);
```

---

## 4. Configuration

### 4.1 Reading Current Configuration

```csharp
var config = await client.GetConfigAsync();
Console.WriteLine($"Resolution: {config.Width}x{config.Height}");
Console.WriteLine($"Bit Depth: {config.BitDepth}");
Console.WriteLine($"Target FPS: {config.TargetFps}");
Console.WriteLine($"Default Mode: {config.DefaultMode}");
```

### 4.2 Changing Configuration

```csharp
var newConfig = new DetectorConfig(
    Width: 2048,
    Height: 2048,
    BitDepth: 16,
    TargetFps: 30,
    DefaultMode: ScanMode.Single
);
await client.SetConfigAsync(newConfig);
```

**Note**: Changing resolution or bit depth may require the detector to restart its scan pipeline. The SDK handles this automatically.

### 4.3 Supported Configurations

| Parameter | Allowed Values |
|-----------|---------------|
| Width | 1024, 2048, 3072 |
| Height | 1024, 2048, 3072 |
| BitDepth | 14, 16 |
| TargetFps | 10, 15, 30 |
| DefaultMode | Single, Continuous, Calibration |

---

## 5. Image Storage

### 5.1 Supported Formats

| Format | Extension | Description | Use Case |
|--------|-----------|-------------|----------|
| TIFF | .tiff | 16-bit grayscale TIFF | Recommended for medical imaging |
| RAW | .raw | Binary pixel data + JSON sidecar | High-speed storage, post-processing |
| DICOM | .dcm | DICOM Part 10 (optional) | Clinical integration |

### 5.2 Saving Frames

```csharp
// TIFF (recommended)
await client.SaveFrameAsync(frame, "image.tiff", ImageFormat.Tiff);

// RAW binary with JSON metadata sidecar
await client.SaveFrameAsync(frame, "image.raw", ImageFormat.Raw);
// Creates: image.raw (pixel data) + image.raw.json (metadata)

// DICOM (optional, requires DICOM configuration)
await client.SaveFrameAsync(frame, "image.dcm", ImageFormat.Dicom);
```

### 5.3 Storage Estimation

| Tier | Frame Size | 100 Frames | 1000 Frames |
|------|-----------|------------|-------------|
| Minimum (1024x1024, 14-bit) | ~2.0 MB | ~200 MB | ~2.0 GB |
| Target (2048x2048, 16-bit) | ~8.0 MB | ~800 MB | ~8.0 GB |
| Maximum (3072x3072, 16-bit) | ~18.0 MB | ~1.8 GB | ~18.0 GB |

---

## 6. Monitoring and Status

### 6.1 Scan Status

```csharp
var status = client.CurrentStatus;
Console.WriteLine($"Scanning: {status.IsScanning}");
Console.WriteLine($"Frames captured: {status.FrameCount}");
Console.WriteLine($"Dropped frames: {status.DroppedFrames}");
Console.WriteLine($"Current FPS: {status.Fps:F1}");
```

### 6.2 Event Handling

Register handlers to receive notifications:

```csharp
// Frame received notification
client.FrameReceived += (sender, e) =>
{
    Console.WriteLine($"Frame #{e.Frame.SequenceNumber} received ({e.QueueDepth} queued)");
};

// Error notification
client.ErrorOccurred += (sender, e) =>
{
    Console.WriteLine($"Error: {e.Error} - {e.Message}");
    if (!e.IsRecoverable)
    {
        Console.WriteLine("Fatal error. Reconnection required.");
    }
};

// Connection state change
client.ConnectionChanged += (sender, e) =>
{
    Console.WriteLine($"Connection: {e.State} ({e.Reason})");
};
```

---

## 7. Device Discovery

Discover detector devices on the local network without knowing their IP addresses:

```csharp
var detectors = await DetectorDiscovery.DiscoverAsync(TimeSpan.FromSeconds(5));

foreach (var det in detectors)
{
    Console.WriteLine($"Found: {det.DeviceId} (FW {det.FirmwareVersion})");
    Console.WriteLine($"  Max resolution: {det.MaxWidth}x{det.MaxHeight}");
    Console.WriteLine($"  Max tier: {det.MaxTier}");
}

// Connect to the first discovered detector
if (detectors.Count > 0)
{
    var client = new DetectorClient();
    await client.ConnectAsync(detectors[0].DeviceId);
}
```

---

## 8. Advanced Client Options

For high-throughput scenarios, configure the client with custom buffer sizes:

```csharp
var options = new DetectorClientOptions
{
    ReceiveBufferSize = 16 * 1024 * 1024,  // 16 MB UDP receive buffer
    MaxReassemblySlots = 8,                 // Max concurrent frame reassembly
    FrameTimeoutMs = 2000,                  // Frame completion timeout (ms)
    AutoReconnect = true,                   // Auto-reconnect on disconnect
    ReconnectIntervalMs = 5000,             // Reconnect retry interval (ms)
};

var client = new DetectorClient(options);
await client.ConnectAsync("192.168.1.100");
```

### 8.1 Recommended Settings by Tier

| Setting | Minimum Tier | Target Tier | Maximum Tier |
|---------|-------------|-------------|--------------|
| ReceiveBufferSize | 4 MB | 16 MB | 32 MB |
| MaxReassemblySlots | 4 | 8 | 8 |
| FrameTimeoutMs | 2000 | 2000 | 3000 |

---

## 9. Pixel Data Access

### 9.1 Direct Pixel Access

```csharp
var frame = await client.GetFrameAsync(TimeSpan.FromSeconds(5));

// Single pixel (row, column)
ushort pixel = frame.GetPixel(100, 200);

// Entire row as span
ReadOnlySpan<ushort> row = frame.GetRow(100);

// Full pixel array
ReadOnlyMemory<ushort> allPixels = frame.PixelData;
```

### 9.2 Frame Statistics

```csharp
Console.WriteLine($"Min pixel value: {frame.MinValue}");
Console.WriteLine($"Max pixel value: {frame.MaxValue}");
Console.WriteLine($"Mean pixel value: {frame.MeanValue:F2}");
```

### 9.3 Memory Management

Frames use pooled memory buffers. Dispose frames promptly to avoid memory pressure:

```csharp
// Option 1: using statement
using var frame = await client.GetFrameAsync(TimeSpan.FromSeconds(5));
// frame is automatically disposed at end of scope

// Option 2: explicit dispose in loops
await foreach (var f in client.StreamFramesAsync())
{
    ProcessFrame(f);
    f.Dispose();  // Return buffer to pool immediately
}
```

---

## 10. Common Workflows

### 10.1 Batch Capture with Quality Check

```csharp
using var client = new DetectorClient();
await client.ConnectAsync("192.168.1.100");

await client.StartScanAsync(ScanMode.Continuous);

int saved = 0;
int rejected = 0;

await foreach (var frame in client.StreamFramesAsync())
{
    using (frame)
    {
        // Quality check: reject saturated frames
        if (frame.MaxValue >= 65535)
        {
            rejected++;
            continue;
        }

        await client.SaveFrameAsync(frame, $"batch/frame_{saved:D4}.tiff", ImageFormat.Tiff);
        saved++;

        if (saved >= 50) break;
    }
}

await client.StopScanAsync();
Console.WriteLine($"Saved: {saved}, Rejected: {rejected}");
```

### 10.2 Multi-Detector Synchronized Capture

```csharp
var client1 = new DetectorClient();
var client2 = new DetectorClient();

await Task.WhenAll(
    client1.ConnectAsync("192.168.1.100"),
    client2.ConnectAsync("192.168.1.101")
);

await Task.WhenAll(
    client1.StartScanAsync(ScanMode.Single),
    client2.StartScanAsync(ScanMode.Single)
);

var frame1 = await client1.GetFrameAsync(TimeSpan.FromSeconds(5));
var frame2 = await client2.GetFrameAsync(TimeSpan.FromSeconds(5));

await Task.WhenAll(
    client1.SaveFrameAsync(frame1, "detector1.tiff", ImageFormat.Tiff),
    client2.SaveFrameAsync(frame2, "detector2.tiff", ImageFormat.Tiff)
);
```

---

## 11. Error Handling

### 11.1 Exception Types

| Exception | Cause | Recovery |
|-----------|-------|----------|
| DetectorConnectionException | Network connection failed | Check IP, port, and network |
| DetectorScanException | Scan operation failed | Check detector state, retry |
| DetectorConfigException | Invalid configuration | Verify parameter values |
| DetectorStorageException | File save failed | Check disk space and permissions |
| TimeoutException | No frame within timeout | Increase timeout or check scan state |

### 11.2 Error Handling Pattern

```csharp
try
{
    using var client = new DetectorClient();
    await client.ConnectAsync("192.168.1.100");
    await client.StartScanAsync(ScanMode.Single);
    var frame = await client.GetFrameAsync(TimeSpan.FromSeconds(5));
    await client.SaveFrameAsync(frame, "output.tiff", ImageFormat.Tiff);
}
catch (DetectorConnectionException ex)
{
    Console.WriteLine($"Connection failed: {ex.Message}");
    // Check network connectivity and SoC power
}
catch (TimeoutException)
{
    Console.WriteLine("No frame received. Check X-ray source and detector state.");
}
catch (DetectorStorageException ex)
{
    Console.WriteLine($"Save failed: {ex.Message}");
    // Check disk space and file permissions
}
```

### 11.3 FPGA Error Codes

The detector reports hardware-level errors through the `ErrorOccurred` event:

| Code | Name | Description | Suggested Action |
|------|------|-------------|-----------------|
| 0x01 | READOUT_TIMEOUT | FPGA readout timed out | Check panel connection, retry scan |
| 0x02 | OVEREXPOSURE | Pixel values exceed threshold | Reduce exposure time |
| 0x04 | BUFFER_OVERFLOW | Line buffer overflow | Reduce frame rate or resolution |
| 0x08 | CRC_ERROR | CSI-2 packet CRC mismatch | Check cable, retry |
| 0x10 | SPI_TIMEOUT | SPI communication timeout | Check SoC-FPGA connection |
| 0x20 | DPHY_LINK_FAIL | D-PHY link failure | Check CSI-2 cable and connectors |
| 0x40 | FRAME_INCOMPLETE | Missing lines in frame | Check data path, retry |
| 0x80 | SYSTEM_ERROR | General system error | Power cycle detector |

---

## 12. Thread Safety

### 12.1 Safe Operations (any thread)

- Reading `IsConnected`, `CurrentStatus`, `DeviceInfo`
- Subscribing/unsubscribing from events

### 12.2 Unsafe Operations (single thread only)

- `ConnectAsync` / `DisconnectAsync`
- `StartScanAsync` / `StopScanAsync`
- `GetFrameAsync` (single consumer only)

### 12.3 WPF Integration

Events fire on thread pool threads. Use `Dispatcher.Invoke` for UI updates:

```csharp
client.FrameReceived += (sender, e) =>
{
    Dispatcher.Invoke(() =>
    {
        UpdateDisplay(e.Frame);
    });
};
```

---

## 13. Troubleshooting Quick Reference

| Symptom | Possible Cause | Solution |
|---------|---------------|----------|
| Connection timeout | Wrong IP/port, firewall | Verify IP, check firewall rules |
| No frames received | X-ray source off, scan not started | Verify source, call StartScanAsync |
| Frame drop > 0.01% | Network congestion, small buffers | Enable jumbo frames, increase buffer |
| Saturated images | Overexposure | Reduce exposure time via config |
| Dark images | Underexposure or no X-ray | Check source, adjust exposure |
| CRC errors | Cable issue, signal integrity | Check CSI-2 cable, reseat connectors |

For detailed troubleshooting procedures, see [Troubleshooting Guide](troubleshooting-guide.md).

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (analyst) | Initial user manual |

---
