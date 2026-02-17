# User Manual

**Document Version**: 1.0.0
**Status**: Reviewed
**Last Updated**: 2026-02-17

## Table of Contents

1. [Introduction](#1-introduction)
2. [Quick Start (5 Steps)](#2-quick-start-5-steps)
3. [GUI Application Walkthrough](#3-gui-application-walkthrough)
4. [CLI Operation (Headless Mode)](#4-cli-operation-headless-mode)
5. [Performance Tier Selection](#5-performance-tier-selection)
6. [Monitoring and Status](#6-monitoring-and-status)
7. [Image Storage](#7-image-storage)
8. [SDK API Reference](#8-sdk-api-reference)
9. [Error Handling](#9-error-handling)
10. [Troubleshooting Quick Reference](#10-troubleshooting-quick-reference)
11. [Revision History](#11-revision-history)

---

## 1. Introduction

This manual describes how to operate the X-ray Detector Panel System. The system captures 16-bit X-ray images through a three-layer hardware pipeline:

```
X-ray Panel -> FPGA (Artix-7) -> SoC (i.MX8M Plus) -> Host PC (your workstation)
```

The Host PC communicates with the SoC controller over a 10 GbE UDP connection. The Host SDK (`XrayDetector.Sdk`) provides a C# API for connecting to the detector, configuring imaging parameters, capturing frames, monitoring system health, and saving images to disk.

### 1.1 Intended Audience

This manual is for:

- Equipment operators who capture and store X-ray images
- Application developers integrating the SDK into custom imaging software
- QA engineers performing acceptance testing

### 1.2 System Requirements

| Requirement | Specification |
|-------------|--------------|
| OS | Windows 10/11 (64-bit) or Linux x86-64 |
| Runtime | .NET 8.0 or later |
| Network | 10 GbE NIC (required for Intermediate and Final tiers) |
| RAM | 8 GB minimum, 16 GB recommended |
| Storage | 500 MB for SDK; 8+ GB per 1000 frames at Intermediate tier |

---

## 2. Quick Start (5 Steps)

### Step 1 - Power On

Power on the FPGA evaluation board first, then the SoC board. Wait approximately 30 seconds for the SoC to complete its boot sequence. The heartbeat LED on the FPGA board blinks at ~1 Hz when ready.

### Step 2 - Launch Host GUI

```bash
dotnet run --project tools/GUI.Application
```

On Windows you can also run the published executable directly:

```
XrayDetector.GUI.exe
```

### Step 3 - Discover and Connect

In the Connection panel at the top of the GUI:

1. Click **Discover Devices**. The application sends a UDP broadcast and lists all detectors found on the local network.
2. Select your detector from the dropdown (identified by its IP address `192.168.1.100` and device ID `D7E00001`).
3. Click **Connect**. The status indicator turns green and shows the firmware version.

If no devices are found, enter the IP address manually and click **Connect**.

### Step 4 - Configure

In the Configuration panel:

1. Select a performance tier from the **Tier** dropdown (Minimum / Intermediate / Final).
2. Click **Apply Configuration**.

### Step 5 - Start Acquisition

1. Click **Start Acquisition** to begin continuous frame capture.
2. Frames appear in the display panel in real time.
3. Click **Stop** to end the acquisition session.

For a single frame, click **Single Frame** instead of **Start Acquisition**.

---

## 3. GUI Application Walkthrough

### 3.1 Connection Panel

Located at the top of the main window.

| Control | Function |
|---------|----------|
| IP Address field | Enter SoC IP (default: 192.168.1.100) |
| Port field | Frame data port (default: 8000) |
| Discover Devices button | Broadcast UDP discovery, populate dropdown |
| Device dropdown | Select connected detector |
| Connect / Disconnect button | Establish or close connection |
| Status indicator | Green = connected, Red = disconnected, Yellow = connecting |
| Device info label | Shows firmware version and device ID when connected |

### 3.2 Configuration Panel

| Control | Function |
|---------|----------|
| Tier dropdown | Select preset: Minimum, Intermediate, Final |
| Rows / Cols fields | Custom resolution (overrides tier preset) |
| Bit Depth dropdown | 14-bit or 16-bit |
| FPS field | Target frame rate (10, 15, or 30 fps) |
| Scan Mode dropdown | Single, Continuous, or Calibration |
| Apply Configuration button | Push new settings to the SoC |

When a tier is selected from the dropdown, the Rows, Cols, Bit Depth, and FPS fields are populated automatically. You may override individual fields for custom configurations.

### 3.3 Acquisition Controls

| Button | Action |
|--------|--------|
| Start Acquisition | Begin continuous frame capture |
| Stop | Stop continuous capture |
| Single Frame | Capture exactly one frame |
| Burst (N) | Capture N frames and stop (set N in the adjacent field) |

### 3.4 Display Panel

The display panel shows the most recently received frame. Controls:

| Control | Function |
|---------|----------|
| Window slider | Width of the display window (contrast) |
| Level slider | Center of the display window (brightness) |
| Auto W/L button | Automatically set window/level from frame statistics |
| Zoom In / Out | Mouse wheel or +/- buttons |
| Pan | Click and drag |
| Pixel value tooltip | Hover over any pixel to see its 16-bit value |
| Histogram | Toggle button shows pixel value distribution |

### 3.5 Storage Panel

| Control | Function |
|---------|----------|
| Output path field | Directory where frames are saved |
| Browse button | Open folder selection dialog |
| Format dropdown | RAW, TIFF, or DICOM |
| Auto-save toggle | Automatically save every captured frame |
| Save Last Frame button | Save the currently displayed frame |
| File counter | Shows number of frames saved this session |

### 3.6 Monitoring Dashboard

Located in the lower status bar:

| Indicator | Description |
|-----------|-------------|
| Frame rate | Actual frames per second (updated every second) |
| Drop rate | Dropped frames as a percentage of total frames |
| Latency | Average frame-to-display latency in milliseconds |
| Battery | SoC battery state of charge (BQ40z50 via SMBus) |
| SoC temperature | CPU temperature read from thermal zone |
| Error count | Number of FPGA errors since last reset |

---

## 4. CLI Operation (Headless Mode)

For server environments or automated acquisition without a GUI, use the CLI tool:

```bash
# Capture 100 frames from 192.168.1.100 at Intermediate tier
XrayDetector.CLI.exe \
    --host 192.168.1.100 \
    --rows 2048 --cols 2048 \
    --fps 15 \
    --bit-depth 16 \
    --frames 100 \
    --output ./frames \
    --format tiff

# Using dotnet run from source
dotnet run --project tools/XrayDetector.CLI -- \
    --host 192.168.1.100 \
    --rows 2048 --cols 2048 \
    --fps 15 --bit-depth 16 \
    --frames 100 \
    --output ./frames --format tiff
```

CLI options:

| Option | Description | Default |
|--------|------------|---------|
| `--host` | SoC IP address | 192.168.1.100 |
| `--port` | Frame data port | 8000 |
| `--rows` | Panel rows | 2048 |
| `--cols` | Panel columns | 2048 |
| `--bit-depth` | 14 or 16 | 16 |
| `--fps` | Target frame rate | 15 |
| `--frames` | Number of frames to capture (0 = continuous) | 1 |
| `--output` | Output directory | `./frames` |
| `--format` | raw, tiff, or dicom | tiff |
| `--timeout` | Frame receive timeout in ms | 5000 |
| `--verbose` | Print per-frame statistics | false |

**Example: Continuous capture until Ctrl+C:**

```bash
XrayDetector.CLI.exe --host 192.168.1.100 --rows 2048 --cols 2048 \
    --fps 15 --frames 0 --output ./frames --format tiff --verbose
```

**Example: Single calibration (dark) frame:**

```bash
XrayDetector.CLI.exe --host 192.168.1.100 --mode calibration \
    --frames 1 --output ./calibration --format tiff
```

---

## 5. Performance Tier Selection

Three pre-defined tiers cover the range from development testing to clinical imaging.

| Tier | Rows | Cols | Bit Depth | FPS | Raw Data Rate | CSI-2 Required | Use Case |
|------|------|------|-----------|-----|---------------|----------------|---------|
| Minimum | 1024 | 1024 | 14 | 15 | ~0.21 Gbps | 400 Mbps/lane | Development, debug |
| Intermediate | 2048 | 2048 | 16 | 15 | ~1.01 Gbps | 400 Mbps/lane | Standard imaging |
| Final | 3072 | 3072 | 16 | 15 | ~2.26 Gbps | 800 Mbps/lane | Clinical imaging |

**Selecting the right tier:**

- Use **Minimum** during initial hardware bring-up and software development. The 1024x1024 resolution is fast to process and tolerant of marginal network conditions.
- Use **Intermediate** for standard X-ray imaging sessions. This tier is validated at 400 Mbps/lane (CSI-2 stable) and comfortably fits within a 10 GbE link.
- Use **Final** for clinical imaging requiring the full 3072x3072 resolution. This tier requires the CSI-2 D-PHY to operate at 800 Mbps/lane (see project status note below).

> **Note on Final tier availability**: The 800 Mbps/lane D-PHY speed is operational but undergoing debugging on the current hardware (Artix-7 35T to i.MX8M Plus). Until debugging is complete, use the Intermediate tier for production captures. See the project MEMORY.md for current status.

**Data rate and storage calculations:**

| Tier | Frame Size | 1 hour at 15 fps | Disk/hour |
|------|-----------|-----------------|-----------|
| Minimum | 2.0 MB | 108,000 frames | ~207 GB |
| Intermediate | 8.0 MB | 108,000 frames | ~826 GB |
| Final | 18.0 MB | 108,000 frames | ~1.86 TB |

For long sessions, use RAW format (no header overhead) and ensure sufficient disk space before starting.

---

## 6. Monitoring and Status

### 6.1 Real-Time Frame Statistics

The GUI status bar and the SDK's `CurrentStatus` property expose these metrics:

| Metric | Description | Acceptable Range |
|--------|------------|-----------------|
| Frame rate (fps) | Actual frames received per second | Within 5% of target |
| Drop rate (%) | Percentage of frames lost in transit | < 0.01% |
| Latency (ms) | Frame-to-display time | < 100 ms |
| Queue depth | Frames waiting in reassembly buffer | < 4 frames |

### 6.2 Battery Status (BQ40z50)

The SoC reads battery state from the BQ40z50 fuel gauge on SMBus address `0x0b`. The GUI displays state of charge (%) and estimated remaining time. From the SoC command line:

```bash
# Read state of charge (register 0x0d)
i2cget -y 0 0x0b 0x0d w
# Example output: 0x004b = 75%

# Read battery voltage (register 0x09)
i2cget -y 0 0x0b 0x09 w
# Example output: 0x34F0 = 13552 mV
```

### 6.3 SoC Temperature

SoC CPU temperature is read from the Linux thermal subsystem:

```bash
cat /sys/class/thermal/thermal_zone0/temp
# Output in millidegrees Celsius, e.g., 45000 = 45 C
```

The daemon reports temperature in the system status response. The GUI displays this in the monitoring bar. Normal operating range: 30-75 C. Above 85 C, the daemon logs a warning.

### 6.4 BMI160 IMU Status

The Bosch BMI160 IMU (I2C bus 7, address 0x68) is available for orientation monitoring. Check that it is detected:

```bash
i2cdetect -y 7
# Expected: device at 0x68

# Read chip ID (should be 0xD1)
i2cget -y 7 0x68 0x00
```

The IMU is not used by the core acquisition pipeline but is available for mounting angle logging in future firmware versions.

---

## 7. Image Storage

### 7.1 Supported Formats

| Format | Extension | Description | Recommended Use |
|--------|-----------|-------------|----------------|
| TIFF | `.tiff` | 16-bit grayscale TIFF (BigTIFF for large frames) | General purpose, interoperable |
| RAW | `.raw` + `.json` | Raw binary pixel data with JSON metadata sidecar | High-speed storage, batch post-processing |
| DICOM | `.dcm` | DICOM Part 10 with required tags | Clinical PACS integration |

### 7.2 Storage Estimation

| Tier | Frame Size (TIFF) | 100 Frames | 1000 Frames |
|------|------------------|------------|-------------|
| Minimum (1024x1024, 14-bit) | ~2.1 MB | ~210 MB | ~2.1 GB |
| Intermediate (2048x2048, 16-bit) | ~8.4 MB | ~840 MB | ~8.4 GB |
| Final (3072x3072, 16-bit) | ~18.9 MB | ~1.9 GB | ~18.9 GB |

### 7.3 RAW Format Sidecar

When saving in RAW format, a `.json` sidecar is created alongside the binary file:

```json
{
  "frame_number": 42,
  "timestamp_utc": "2026-02-17T10:23:45.123Z",
  "rows": 2048,
  "cols": 2048,
  "bit_depth": 16,
  "bytes_per_pixel": 2,
  "pixel_order": "row_major_little_endian",
  "min_value": 124,
  "max_value": 63871,
  "mean_value": 12456.3,
  "detector_id": "D7E00001",
  "firmware_version": "0.1.0"
}
```

---

## 8. SDK API Reference

### 8.1 Basic Usage Pattern

```csharp
using XrayDetector.Sdk;

using var client = new DetectorClient();
await client.ConnectAsync("192.168.1.100");

// Configure for Intermediate tier
await client.SetConfigAsync(new DetectorConfig(
    Rows: 2048, Cols: 2048, BitDepth: 16, TargetFps: 15));

// Capture a single frame
await client.StartScanAsync(ScanMode.Single);
using var frame = await client.GetFrameAsync(TimeSpan.FromSeconds(5));

Console.WriteLine($"Captured {frame.Width}x{frame.Height}, " +
                  $"min={frame.MinValue}, max={frame.MaxValue}");

await client.SaveFrameAsync(frame, "output.tiff", ImageFormat.Tiff);
```

### 8.2 Continuous Streaming

```csharp
await client.StartScanAsync(ScanMode.Continuous);

int count = 0;
await foreach (var frame in client.StreamFramesAsync())
{
    await client.SaveFrameAsync(
        frame,
        $"frames/frame_{count:D6}.tiff",
        ImageFormat.Tiff);

    frame.Dispose();  // Return buffer to pool immediately
    count++;

    if (count >= 100) break;
}

await client.StopScanAsync();
```

### 8.3 Advanced Client Options

For the Final tier or high-reliability scenarios:

```csharp
var options = new DetectorClientOptions
{
    ReceiveBufferSize   = 32 * 1024 * 1024,  // 32 MB UDP receive buffer
    MaxReassemblySlots  = 8,                  // Concurrent frame reassembly slots
    FrameTimeoutMs      = 3000,              // Frame completion timeout
    AutoReconnect       = true,
    ReconnectIntervalMs = 5000,
};

var client = new DetectorClient(options);
```

Recommended settings by tier:

| Setting | Minimum Tier | Intermediate Tier | Final Tier |
|---------|-------------|-------------------|-----------|
| ReceiveBufferSize | 8 MB | 16 MB | 32 MB |
| MaxReassemblySlots | 4 | 8 | 8 |
| FrameTimeoutMs | 2000 | 2000 | 3000 |

### 8.4 Event Handling

```csharp
// Frame received notification
client.FrameReceived += (sender, e) =>
    Console.WriteLine($"Frame #{e.Frame.SequenceNumber} received");

// Error notification
client.ErrorOccurred += (sender, e) =>
{
    Console.WriteLine($"FPGA error 0x{(int)e.Error:X4}: {e.Message}");
    if (!e.IsRecoverable)
        Console.WriteLine("Fatal error. Reconnection required.");
};

// Connection state change
client.ConnectionChanged += (sender, e) =>
    Console.WriteLine($"Connection: {e.State} - {e.Reason}");
```

### 8.5 FPGA Hardware Error Codes

Reported through the `ErrorOccurred` event:

| Code | Name | Severity | Description |
|------|------|----------|-------------|
| 0x01 | TIMEOUT | Warning | Readout exceeded TIMING_LINE_PERIOD x 2 |
| 0x02 | OVERFLOW | Error | Line buffer bank collision; data lost |
| 0x04 | CRC_ERROR | Error | CSI-2 self-check CRC mismatch |
| 0x08 | OVEREXPOSURE | Warning | Pixel values reached saturation threshold |
| 0x10 | ROIC_FAULT | Error | No valid ROIC data within line period |
| 0x20 | DPHY_ERROR | Error | D-PHY initialization failed or link loss |
| 0x40 | CONFIG_ERROR | Error | Invalid register configuration detected |
| 0x80 | WATCHDOG | Warning | Heartbeat timer expired (SoC comm lost) |

---

## 9. Error Handling

### 9.1 Exception Types

| Exception | Cause | Recovery |
|-----------|-------|----------|
| `DetectorConnectionException` | Network connection failed | Check IP, port, firewall |
| `DetectorScanException` | Scan operation failed | Check detector state, retry |
| `DetectorConfigException` | Invalid configuration | Verify parameter values |
| `DetectorStorageException` | File save failed | Check disk space and permissions |
| `TimeoutException` | No frame within timeout period | Increase timeout or check scan state |

### 9.2 Recommended Error Handling Pattern

```csharp
try
{
    using var client = new DetectorClient();
    await client.ConnectAsync("192.168.1.100");
    await client.StartScanAsync(ScanMode.Single);
    using var frame = await client.GetFrameAsync(TimeSpan.FromSeconds(5));
    await client.SaveFrameAsync(frame, "output.tiff", ImageFormat.Tiff);
}
catch (DetectorConnectionException ex)
{
    Console.Error.WriteLine($"Connection failed: {ex.Message}");
    // Check SoC power, network cable, and IP address
}
catch (TimeoutException)
{
    Console.Error.WriteLine("No frame received within 5 seconds. " +
                            "Verify X-ray source and detector state.");
}
catch (DetectorStorageException ex)
{
    Console.Error.WriteLine($"Save failed: {ex.Message}");
    // Check disk space and file write permissions
}
```

---

## 10. Troubleshooting Quick Reference

| Symptom | Probable Cause | Solution |
|---------|---------------|---------|
| "Discover Devices" finds nothing | UDP broadcast blocked | Connect directly by IP address |
| Connection timeout | Wrong IP or SoC not booted | Verify IP, wait 30 s after power on |
| No frames received | Scan not started or X-ray source off | Call StartScanAsync, verify source |
| Drop rate > 0.01% | Network buffer too small | Enable jumbo frames, increase ReceiveBufferSize |
| Saturated frames (max value = 65535) | Overexposure | Reduce exposure time |
| Dark frames (near-zero values) | No X-ray or calibration mode | Check source, verify ScanMode is not Calibration |
| CRC errors in log | CSI-2 cable signal integrity | Reseat FPC cable at both ends |
| BQ40z50 not shown | SMBus driver not loaded | Check `lsmod | grep bq27xxx` on SoC |

For detailed hardware diagnostics, see the [Troubleshooting Guide](troubleshooting-guide.md).

---

## 11. Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Docs Agent | Complete user manual with quick start, GUI walkthrough, CLI operation, and monitoring |
| 1.0.1 | 2026-02-17 | manager-quality | Fix FPGA error codes (Section 8.5) to match spi-register-map.md bit definitions. Corrected SPI_TIMEOUT->WATCHDOG(0x80), FRAME_INCOMPLETE->ROIC_FAULT(0x10), SYSTEM_ERROR->CONFIG_ERROR(0x40). |

---

## Review Record

- Date: 2026-02-17
- Reviewer: manager-quality
- Status: Approved (with corrections applied)
- TRUST 5: T:4 R:5 U:4 S:4 T:4
