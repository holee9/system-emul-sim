# Troubleshooting Guide

**Project**: X-ray Detector Panel System
**Version**: 1.0.0
**Last Updated**: 2026-02-17

---

## 1. Quick Diagnostic Checklist

Before investigating specific issues, verify these common prerequisites:

1. SoC controller is powered on and booted
2. Ethernet cable is connected (Host PC to SoC)
3. Host PC can ping the SoC IP address (default: `192.168.1.100`)
4. .NET 8.0+ runtime is installed on Host PC
5. No other application is using the detector port (default: 8000)

---

## 2. Connection Issues

### 2.1 Connection Timeout

**Symptom**: `DetectorConnectionException` thrown when calling `ConnectAsync`.

**Possible Causes and Solutions**:

| Cause | Diagnostic | Solution |
|-------|-----------|----------|
| Wrong IP address | `ping <ip>` fails | Verify SoC IP in network settings |
| Wrong port | `ping` succeeds but connection fails | Check port number (default: 8000) |
| Firewall blocking | `telnet <ip> <port>` fails | Add firewall exception for port 8000 |
| SoC not booted | No ping response | Wait for SoC boot (30-60 seconds after power on) |
| Network misconfiguration | Ping timeout | Verify subnet mask and gateway settings |

**Diagnostic Steps**:

```bash
# Step 1: Verify network connectivity
ping 192.168.1.100

# Step 2: Verify port is reachable (Windows)
Test-NetConnection -ComputerName 192.168.1.100 -Port 8000

# Step 3: Verify port is reachable (Linux)
nc -zv 192.168.1.100 8000
```

### 2.2 Connection Drops

**Symptom**: `ConnectionChanged` event fires with `State = Disconnected` during operation.

**Possible Causes**:

| Cause | Diagnostic | Solution |
|-------|-----------|----------|
| Network cable loose | Check physical connection | Reseat Ethernet cable |
| SoC reboot | SoC console shows reboot log | Investigate SoC stability |
| Network congestion | Frame drop rate increasing | Use dedicated network segment |
| Idle timeout | No scan activity for extended period | Enable auto-reconnect in client options |

**Auto-Reconnect Configuration**:

```csharp
var options = new DetectorClientOptions
{
    AutoReconnect = true,
    ReconnectIntervalMs = 5000,
};
var client = new DetectorClient(options);
```

### 2.3 Discovery Returns No Devices

**Symptom**: `DetectorDiscovery.DiscoverAsync()` returns an empty list.

**Solutions**:

1. Verify Host PC and SoC are on the same subnet
2. Check that UDP broadcast is not blocked by firewall
3. Increase discovery timeout: `DiscoverAsync(TimeSpan.FromSeconds(10))`
4. Connect directly using the known IP address instead

---

## 3. Frame Capture Issues

### 3.1 No Frames Received

**Symptom**: `GetFrameAsync` throws `TimeoutException`.

| Cause | Diagnostic | Solution |
|-------|-----------|----------|
| Scan not started | `CurrentStatus.IsScanning == false` | Call `StartScanAsync()` first |
| X-ray source off | Dark frame has near-zero values | Power on X-ray source |
| FPGA in error state | Error event fires with FPGA code | Clear error, restart scan |
| Wrong scan mode | Mode mismatch | Verify `ScanMode` parameter |
| Timeout too short | Frame time exceeds timeout | Increase timeout value |

**Diagnostic Code**:

```csharp
// Check scan state
Console.WriteLine($"Is scanning: {client.CurrentStatus.IsScanning}");
Console.WriteLine($"Frame count: {client.CurrentStatus.FrameCount}");
Console.WriteLine($"Dropped: {client.CurrentStatus.DroppedFrames}");

// Register error handler to catch FPGA errors
client.ErrorOccurred += (s, e) =>
    Console.WriteLine($"Error: 0x{(int)e.Error:X4} - {e.Message}");
```

### 3.2 Frame Drops

**Symptom**: `CurrentStatus.DroppedFrames` increases during continuous capture.

| Cause | Diagnostic | Solution |
|-------|-----------|----------|
| Network bandwidth insufficient | Frame rate exceeds NIC capacity | Use 10 GbE for Target/Maximum tiers |
| Small receive buffer | Buffer overflow on host | Increase `ReceiveBufferSize` to 16-32 MB |
| Slow frame processing | Queue depth grows | Process frames asynchronously |
| Jumbo frames disabled | Small MTU causes fragmentation | Enable jumbo frames (MTU 9000) |

**Network Optimization**:

```bash
# Windows: Enable jumbo frames (MTU 9000)
# Open Network Adapter Properties > Configure > Advanced > Jumbo Frame > 9014 Bytes

# Linux: Set MTU
sudo ip link set eth0 mtu 9000

# Linux: Increase UDP receive buffer
sudo sysctl -w net.core.rmem_max=33554432
sudo sysctl -w net.core.rmem_default=16777216
```

### 3.3 Incomplete Frames

**Symptom**: Frame data has missing regions (appears as black bands or stripes).

| Cause | Diagnostic | Solution |
|-------|-----------|----------|
| Packet loss | Missing packet error in log | Increase buffer, check network |
| CSI-2 link error | D-PHY error flag set | Check CSI-2 cable and connectors |
| Buffer overflow on SoC | SoC log shows overflow | Reduce frame rate |

### 3.4 Corrupted Frame Data

**Symptom**: Frame contains unexpected pixel values, random patterns, or CRC errors.

| Cause | Diagnostic | Solution |
|-------|-----------|----------|
| CRC mismatch | `ErrorOccurred` with CRC_ERROR | Check data path, reseat cables |
| Bit errors | Random pixel corruption | Check signal integrity, cables |
| Configuration mismatch | Wrong resolution/bit depth | Verify `DetectorConfig` matches hardware |

---

## 4. FPGA Error Codes

The FPGA reports errors through the protection logic module. Each error sets a flag in the error register (address 0x10).

### 4.1 Error Code Reference

| Code | Flag | Name | Description | Severity | Recovery |
|------|------|------|-------------|----------|----------|
| 0x01 | Bit 0 | READOUT_TIMEOUT | Line readout exceeded configured timeout | Warning | Retry scan |
| 0x02 | Bit 1 | OVEREXPOSURE | Pixel values exceed overexposure threshold | Warning | Reduce exposure time |
| 0x04 | Bit 2 | BUFFER_OVERFLOW | Line buffer write exceeded capacity | Error | Reduce resolution or FPS |
| 0x08 | Bit 3 | CRC_ERROR | CSI-2 packet CRC-16 mismatch | Error | Check data path integrity |
| 0x10 | Bit 4 | SPI_TIMEOUT | SPI watchdog timer expired (no SPI activity) | Warning | Check SoC-FPGA SPI connection |
| 0x20 | Bit 5 | DPHY_LINK_FAIL | D-PHY lane synchronization lost | Error | Check CSI-2 cable and power |
| 0x40 | Bit 6 | FRAME_INCOMPLETE | Frame ended with fewer lines than expected | Warning | Check ROIC panel connection |
| 0x80 | Bit 7 | SYSTEM_ERROR | Internal FPGA error (should not occur normally) | Critical | Power cycle the detector |

### 4.2 Error Handling in Code

```csharp
client.ErrorOccurred += (sender, e) =>
{
    Console.WriteLine($"Error 0x{(int)e.Error:X4}: {e.Message}");

    switch (e.Error)
    {
        case DetectorError.READOUT_TIMEOUT:
        case DetectorError.OVEREXPOSURE:
        case DetectorError.FRAME_INCOMPLETE:
            // Warning-level: log and continue
            Console.WriteLine("Warning-level error. Scan can continue.");
            break;

        case DetectorError.BUFFER_OVERFLOW:
        case DetectorError.CRC_ERROR:
        case DetectorError.DPHY_LINK_FAIL:
            // Error-level: stop scan, reconfigure
            Console.WriteLine("Error-level issue. Stopping scan for diagnosis.");
            break;

        case DetectorError.SYSTEM_ERROR:
            // Critical: power cycle required
            Console.WriteLine("Critical error. Power cycle the detector.");
            break;
    }
};
```

### 4.3 Clearing Errors

Errors are cleared by writing to the error clear register via SPI. The SDK handles this automatically when you call `StartScanAsync()` after an error. If errors persist after clearing, the underlying hardware issue must be resolved.

---

## 5. Configuration Issues

### 5.1 Invalid Configuration

**Symptom**: `DetectorConfigException` when calling `SetConfigAsync`.

**Allowed Values**:

| Parameter | Valid Values |
|-----------|-------------|
| Width | 1024, 2048, 3072 |
| Height | 1024, 2048, 3072 |
| BitDepth | 14, 16 |
| TargetFps | 10, 15, 30 |

**Common Mistakes**:

- Setting Width and Height to different values that are not square
- Setting BitDepth to values other than 14 or 16
- Requesting 30 fps with Maximum resolution on 1 GbE network

### 5.2 Configuration Not Taking Effect

**Symptom**: Config change succeeds but frames still use old settings.

**Solution**: Some configuration changes require a scan restart. The SDK handles this automatically, but if you observe stale settings:

1. Stop any active scan: `await client.StopScanAsync()`
2. Apply new configuration: `await client.SetConfigAsync(config)`
3. Start a new scan: `await client.StartScanAsync(mode)`

---

## 6. Performance Issues

### 6.1 Low Frame Rate

**Symptom**: Actual FPS is lower than configured `TargetFps`.

| Cause | Diagnostic | Solution |
|-------|-----------|----------|
| Network bottleneck | FPS drops with resolution increase | Upgrade to 10 GbE |
| Host processing too slow | Queue depth grows | Use async processing pipeline |
| FPGA readout bottleneck | FPS limited regardless of network | Check panel timing configuration |

### 6.2 High CPU Usage

**Symptom**: Host PC CPU usage is abnormally high during capture.

| Cause | Solution |
|-------|----------|
| Synchronous frame processing | Use `StreamFramesAsync` with async pipeline |
| WPF rendering on UI thread | Process frames on background thread |
| Excessive event handlers | Minimize work in event handlers |

### 6.3 High Memory Usage

**Symptom**: Host PC memory grows during long capture sessions.

| Cause | Solution |
|-------|----------|
| Frames not disposed | Call `frame.Dispose()` after processing |
| Accumulating frames in memory | Process and save frames incrementally |
| Large receive buffer | Reduce `ReceiveBufferSize` if drops are acceptable |

---

## 7. Storage Issues

### 7.1 Save Fails

**Symptom**: `DetectorStorageException` when calling `SaveFrameAsync`.

| Cause | Solution |
|-------|----------|
| Insufficient disk space | Free disk space or change save directory |
| Permission denied | Run application with write permissions |
| Invalid path | Verify output directory exists |
| File locked | Close other applications accessing the file |

### 7.2 Large File Sizes

**Tip**: RAW format is the most compact (pixel data only). TIFF adds minimal overhead. DICOM includes metadata headers.

| Format | Overhead vs RAW |
|--------|----------------|
| RAW | Baseline (pixel data + JSON sidecar) |
| TIFF | ~1-2% (TIFF header and tags) |
| DICOM | ~5-10% (DICOM metadata) |

---

## 8. Hardware Diagnostics

### 8.1 SoC Controller Not Responding

1. Check power LED on SoC board
2. Verify SoC has finished booting (wait 30-60 seconds)
3. Connect via serial console to check SoC status
4. Check SoC system logs: `journalctl -u detector-service`

### 8.2 FPGA Not Communicating

1. Check FPGA board power and status LEDs
2. Verify SPI connection between SoC and FPGA
3. SoC can test SPI with: `spi-test -D /dev/spidev0.0 -s 1000000`
4. Check FPGA bitstream is loaded (DONE LED should be on)

### 8.3 CSI-2 Link Issues

1. Verify D-PHY cable is properly seated
2. Check cable length (max recommended: 30 cm for high-speed)
3. Verify CSI-2 lane configuration matches hardware
4. Use logic analyzer with MIPI decode for detailed diagnosis

### 8.4 Panel/ROIC Issues

1. Check panel power supply voltage
2. Verify ROIC LVDS connections to FPGA
3. Use test pattern generator to isolate panel vs data path issues:
   - If test patterns are correct, panel/ROIC connection is the issue
   - If test patterns are corrupted, data path (FPGA onward) is the issue

---

## 9. Development Environment Issues

### 9.1 Build Errors

| Error | Solution |
|-------|----------|
| SDK package not found | Run `dotnet restore` |
| .NET version mismatch | Install .NET 8.0 SDK |
| Missing project reference | Verify `.csproj` references |

### 9.2 Simulator Issues

The software simulator (`IntegrationRunner`) can help diagnose issues without hardware:

```bash
# Run integration test scenario
dotnet run --project IntegrationRunner -- --scenario IT-01

# Verbose output for debugging
dotnet run --project IntegrationRunner -- --scenario IT-01 --verbose
```

---

## 10. Log Collection

When reporting issues, collect the following information:

1. **Host SDK version**: Check NuGet package version
2. **SoC firmware version**: `client.DeviceInfo.FirmwareVersion`
3. **Error codes**: From `ErrorOccurred` event handler
4. **Network configuration**: NIC speed, MTU, buffer sizes
5. **Scan configuration**: Resolution, bit depth, FPS, scan mode
6. **Client options**: Buffer sizes, timeout settings
7. **OS and .NET version**: `dotnet --version`

### 10.1 Diagnostic Report Template

```
=== Detector Diagnostic Report ===
Date: [YYYY-MM-DD HH:MM]
SDK Version: [x.y.z]
FW Version: [x.y.z]

Environment:
  OS: [Windows 11 / Ubuntu 22.04]
  .NET: [8.0.x]
  NIC: [Intel X550 10GbE / Realtek 1GbE]
  MTU: [1500 / 9000]

Configuration:
  Resolution: [2048x2048]
  BitDepth: [16]
  FPS: [30]
  ScanMode: [Continuous]

Issue:
  Description: [What happened]
  Error Code: [0x????]
  Frequency: [Always / Intermittent]
  Steps to Reproduce: [1. 2. 3.]

Scan Status:
  FrameCount: [xxx]
  DroppedFrames: [xxx]
  CurrentFps: [xx.x]
```

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (analyst) | Initial troubleshooting guide |

---
