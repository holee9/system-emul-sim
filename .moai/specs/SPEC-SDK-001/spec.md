# SPEC-SDK-001: Host SDK Requirements - DetectorClient API and Frame Processing

---
id: SPEC-SDK-001
version: 1.1.0
status: implemented
created: 2026-02-17
updated: 2026-02-18
author: ABYZ-Lab Agent (architect)
priority: high
milestone: M2
---

## HISTORY

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | ABYZ-Lab Agent (architect) | Initial SPEC creation for Host SDK requirements |

---

## Overview

### Context

The Host SDK (DetectorClient) is the primary API for applications interacting with the X-ray Detector Panel System. It runs on the Host PC (.NET 8.0+, C#) and communicates with the SoC Controller via 10 GbE UDP to receive pixel frames, send control commands, and manage frame storage.

### Scope

This SPEC covers:
1. DetectorClient API design and behavior
2. Network packet reception and frame reassembly
3. Frame storage (TIFF, RAW, optional DICOM)
4. Real-time display support (15 fps preview)
5. Device discovery and connection management
6. Error handling and resilience

### Out of Scope

- GUI application (GUI.Application, covered by separate SPEC)
- Simulator integration (covered by SPEC-SIM-001)
- FPGA RTL development (covered by SPEC-FPGA-001)
- SoC firmware (covered by SPEC-FW-001)

---

## Requirements

### 1. Ubiquitous Requirements (System-Wide Invariants)

**REQ-SDK-001**: The SDK **shall** use .NET 8.0 LTS or later as the target framework.

**WHY**: .NET 8.0 provides cross-platform support (Windows/Linux), high-performance networking, and long-term support.

**IMPACT**: All SDK code must compile and run on both Windows and Linux. Platform-specific APIs (e.g., WPF) must be isolated behind interfaces.

---

**REQ-SDK-002**: The SDK **shall** implement `IDetectorClient` as the public API interface with the following 8 methods:

```csharp
public interface IDetectorClient : IAsyncDisposable
{
    // Connection Management
    Task ConnectAsync(string host, int port = 8000, CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);

    // Configuration
    Task ConfigureAsync(DetectorConfig config, CancellationToken ct = default);

    // Acquisition Control
    Task StartAcquisitionAsync(ScanMode mode, CancellationToken ct = default);
    Task StopAcquisitionAsync(CancellationToken ct = default);

    // Frame Acquisition
    Task<Frame> CaptureFrameAsync(TimeSpan timeout, CancellationToken ct = default);
    IAsyncEnumerable<Frame> StreamFramesAsync(CancellationToken ct = default);

    // Frame Storage
    Task SaveFrameAsync(Frame frame, string path, ImageFormat format, CancellationToken ct = default);

    // Status
    Task<ScanStatus> GetStatusAsync(CancellationToken ct = default);

    // Events
    event EventHandler<FrameEventArgs> FrameReceived;
    event EventHandler<ErrorEventArgs> ErrorOccurred;
    event EventHandler<ConnectionEventArgs> ConnectionChanged;
}
```

**WHY**: Interface-based design enables unit testing with mocks, dependency injection, and future alternative implementations.

**IMPACT**: All public API methods must be defined on `IDetectorClient`. Internal implementation details must not leak through the public API.

---

**REQ-SDK-003**: The SDK **shall** depend only on `Common.Dto` for shared types.

**WHY**: Dependency isolation prevents circular references and enables independent module development.

**IMPACT**: No direct dependencies between SDK modules (PacketReceiver, FrameReassembler, ImageEncoder). All shared types defined in Common.Dto.

---

**REQ-SDK-004**: The SDK **shall** handle all frame data as 16-bit unsigned integers (ushort).

**WHY**: X-ray detector panels produce 14-bit or 16-bit pixel data. 16-bit representation accommodates both.

**IMPACT**: Frame pixel data type is `ReadOnlyMemory<ushort>`. 14-bit data is zero-padded to 16 bits.

---

### 2. Event-Driven Requirements (API Triggers)

**REQ-SDK-010**: **WHEN** `ConnectAsync(host, port)` is called **THEN** the SDK shall establish UDP communication with the SoC at the specified address within 10 seconds.

**WHY**: Connection establishment validates network reachability and device availability.

**IMPACT**: ConnectAsync sends a PING command (0x0007) to the SoC and waits for a response. Timeout after 10 seconds raises `DetectorConnectionException`.

---

**REQ-SDK-011**: **WHEN** `StartAcquisitionAsync(mode)` is called **THEN** the SDK shall send a START_SCAN command (0x0001) to the SoC and begin receiving frame packets.

**WHY**: Scan initiation triggers the FPGA to begin pixel acquisition via the SoC sequence engine.

**IMPACT**: The SDK validates connection state before sending. If not connected, throws `InvalidOperationException`. If scan is already active, throws `InvalidOperationException`.

---

**REQ-SDK-012**: **WHEN** `StopAcquisitionAsync()` is called **THEN** the SDK shall send a STOP_SCAN command (0x0002) to the SoC and stop frame processing.

**WHY**: Graceful scan termination prevents orphan frames and resource leaks.

**IMPACT**: StopAcquisitionAsync waits for the current frame to complete (up to 1 frame period) before stopping the receive pipeline.

---

**REQ-SDK-013**: **WHEN** `CaptureFrameAsync(timeout)` is called **THEN** the SDK shall return the next complete frame within the specified timeout.

**WHY**: Synchronous frame access simplifies single-capture workflows.

**IMPACT**: Returns the next fully reassembled frame. Throws `TimeoutException` if no frame completes within timeout.

---

**REQ-SDK-014**: **WHEN** `StreamFramesAsync()` is called **THEN** the SDK shall yield frames as an `IAsyncEnumerable<Frame>`.

**WHY**: Async streaming enables efficient continuous capture with backpressure.

**IMPACT**: Frames are yielded as they become available. The caller controls consumption rate via `await foreach`.

---

**REQ-SDK-015**: **WHEN** a complete frame is received **THEN** the SDK shall raise the `FrameReceived` event.

**WHY**: Event-driven notification enables reactive programming patterns and GUI updates.

**IMPACT**: Events are raised on a thread pool thread. WPF applications must marshal to the UI thread via `Dispatcher.Invoke`.

---

**REQ-SDK-016**: **WHEN** a network error or FPGA error is detected **THEN** the SDK shall raise the `ErrorOccurred` event with error details.

**WHY**: Error visibility enables applications to implement custom recovery logic.

**IMPACT**: Errors include error code, message, and `IsRecoverable` flag. Auto-reconnect handles recoverable connection errors.

---

**REQ-SDK-017**: **WHEN** `SaveFrameAsync(frame, path, format)` is called **THEN** the SDK shall write the frame to disk in the specified format.

**WHY**: Frame persistence is required for clinical image archival and post-processing.

**IMPACT**: Supports TIFF (lossless 16-bit), RAW (binary with JSON sidecar), and optionally DICOM.

---

**REQ-SDK-018**: **WHEN** `DisconnectAsync()` is called **THEN** the SDK shall send a DISCONNECT command (0x0008) to the SoC and release all network resources.

**WHY**: Graceful disconnection allows the SoC to clean up state and prepare for the next connection.

**IMPACT**: DisconnectAsync waits for any in-progress scan to complete or times out after 5 seconds. Releases UDP sockets and frame buffers.

---

**REQ-SDK-019**: **WHEN** `GetStatusAsync()` is called **THEN** the SDK shall return a `ScanStatus` object containing: IsConnected, IsScanning, DroppedFrames, FramesReceived, CurrentThroughputGbps.

**WHY**: Status polling enables applications to monitor scan health and performance.

**IMPACT**: Status values updated at 1 Hz minimum. DroppedFrames and FramesReceived are cumulative since ConnectAsync.

---

### 3. State-Driven Requirements (Configuration Modes)

**REQ-SDK-020**: **IF** `ScanMode.Single` is selected **THEN** the SDK shall capture exactly one frame and return to idle state.

**WHY**: Single capture mode is the primary workflow for diagnostic radiography.

**IMPACT**: After receiving one complete frame, the SDK automatically stops the scan and transitions to idle.

---

**REQ-SDK-021**: **IF** `ScanMode.Continuous` is selected **THEN** the SDK shall capture frames continuously until `StopScanAsync()` is called.

**WHY**: Continuous mode is used for live preview, positioning, and fluoroscopy.

**IMPACT**: Frames are queued internally (bounded queue, 16 frames). Oldest frames are dropped if the consumer cannot keep up.

---

**REQ-SDK-022**: **IF** `ScanMode.Calibration` is selected **THEN** the SDK shall capture dark frames (gate OFF during exposure).

**WHY**: Dark frames are used for offset correction and defect detection.

**IMPACT**: Calibration frames are marked with `flags.calibration = 1` in the frame header.

---

**REQ-SDK-023**: **IF** `ImageFormat.Tiff` is selected for SaveFrame **THEN** the SDK shall write a 16-bit grayscale TIFF with appropriate metadata tags.

**WHY**: TIFF is the standard lossless format for medical imaging with metadata support.

**IMPACT**: TIFF tags include: IMAGEWIDTH, IMAGELENGTH, BITSPERSAMPLE=16, SAMPLESPERPIXEL=1, PHOTOMETRIC=MINISBLACK.

---

**REQ-SDK-024**: **IF** `ImageFormat.Raw` is selected for SaveFrame **THEN** the SDK shall write raw binary pixel data with a JSON metadata sidecar.

**WHY**: RAW format provides zero-overhead storage for post-processing pipelines.

**IMPACT**: Binary file contains width*height*2 bytes (little-endian uint16). JSON sidecar contains width, height, bitDepth, timestamp, sequenceNumber.

---

**REQ-SDK-025**: **IF** the network connection is lost during scanning **THEN** the SDK shall attempt auto-reconnect every 5 seconds.

**WHY**: Transient network failures should not require manual intervention.

**IMPACT**: ConnectionChanged event reports Reconnecting state. Scan automatically resumes after reconnection if Continuous mode was active.

---

### 4. Unwanted Requirements (Prohibited Actions)

**REQ-SDK-030**: The SDK **shall not** block the calling thread during frame reception.

**WHY**: Blocking would freeze GUI applications and prevent concurrent operations.

**IMPACT**: All I/O operations are async. The receive loop runs on a dedicated thread. Frame processing uses Task-based parallelism.

---

**REQ-SDK-031**: The SDK **shall not** allocate per-frame heap memory for pixel buffers in the hot path.

**WHY**: GC pressure from frequent allocations causes latency spikes and throughput degradation.

**IMPACT**: Frame pixel buffers use `ArrayPool<ushort>`. Frames implement `IDisposable` to return buffers to the pool.

---

**REQ-SDK-032**: The SDK **shall not** drop more than 0.01% of frames during continuous capture at the active performance tier.

**WHY**: Frame drops impact clinical image quality and diagnostic confidence.

**IMPACT**: At Target tier (3072x3072@15fps), maximum 1 drop per 10,000 frames. Measured via `ScanStatus.DroppedFrames`.

---

**REQ-SDK-033**: The SDK **shall not** expose internal threading or synchronization primitives through the public API.

**WHY**: Internal concurrency details are implementation concerns that complicate API usage.

**IMPACT**: Public API uses standard .NET async patterns (Task, IAsyncEnumerable, events). No ManualResetEvent, Semaphore, or Thread exposed.

---

### 5. Optional Requirements (Enhanced Features)

**REQ-SDK-040**: **Where possible**, the SDK should support automatic device discovery via UDP broadcast on port 8002.

**WHY**: Discovery simplifies deployment by eliminating manual IP configuration.

**IMPACT**: `DetectorDiscovery.DiscoverAsync()` broadcasts and collects responses. Priority: medium.

---

**REQ-SDK-041**: **Where possible**, the SDK should support DICOM format output.

**WHY**: DICOM is the standard for medical image exchange and PACS integration.

**IMPACT**: Requires fo-dicom NuGet package. DICOM tags include Patient, Study, Series, Image UIDs. Priority: low.

---

**REQ-SDK-042**: **Where possible**, the SDK should provide Window/Level mapping utility for 16-bit to 8-bit display conversion.

**WHY**: Medical image display requires adjustable brightness/contrast mapping.

**IMPACT**: `WindowLevel.Apply(pixels, center, width)` returns 8-bit grayscale. Useful for WPF display integration. Priority: medium.

---

**REQ-SDK-043**: **Where possible**, the SDK should support frame statistics (min, max, mean pixel values).

**WHY**: Statistics assist in exposure quality assessment and automatic Window/Level calculation.

**IMPACT**: `Frame.MinValue`, `Frame.MaxValue`, `Frame.MeanValue` computed lazily on first access. Priority: low.

---

## Technical Constraints

### Platform Constraints

| Constraint | Value | Rationale |
|-----------|-------|-----------|
| Target Framework | .NET 8.0 LTS | Long-term support, cross-platform |
| Language | C# 12 | Modern language features |
| OS Support | Windows 10+, Linux (Ubuntu 22.04+) | Cross-platform requirement |
| Memory (min) | 8 GB RAM | 16 GB for Target tier |
| Network (min) | 1 GbE | Minimum and Mid-A tiers only |
| Network (recommended) | 10 GbE | Required for Mid-B tier and above |

**Note: Mid-B tier and above require 10 GbE network connection. 1 GbE supports only Minimum and Mid-A tiers.**

1 GbE effective throughput is approximately 0.94 Gbps, which is insufficient for Mid-B tier (2.01 Gbps), Target tier (2.26 Gbps), and Maximum tier (4.53 Gbps). The SDK must validate network tier compatibility during `ConnectAsync` and raise a warning if the detected network interface cannot sustain the configured performance tier data rate.

### Protocol Constraints

| Constraint | Value | Reference |
|-----------|-------|-----------|
| Frame Header Size | 32 bytes | docs/api/ethernet-protocol.md Section 2.1 |
| Max Payload | 8192 bytes | UDP datagram size |
| Magic Number | 0xD7E01234 | Frame data packets |
| Command Magic | 0xBEEFCAFE | Control commands |
| Response Magic | 0xCAFEBEEF | Command responses |
| Frame Header CRC | CRC-16/CCITT at offset 28, covers bytes 0-27 | docs/api/ethernet-protocol.md Section 7.2 |
| CRC Algorithm | CRC-16/CCITT | Polynomial 0x8408 (reflected), init 0xFFFF |

### Performance Constraints

| Metric | Target | Measurement |
|--------|--------|-------------|
| Frame drop rate | < 0.01% | Continuous capture at active tier |
| Frame reassembly latency | < 10 ms | Packet arrival to Frame event |
| Connection timeout | < 10 s | ConnectAsync duration |
| Auto-reconnect interval | 5 s | Reconnect retry period |
| Max reassembly slots | 8 | Concurrent incomplete frames |
| Frame timeout | 2 s | Incomplete frame expiry |

---

## Acceptance Criteria

### AC-001: Connection Management

**GIVEN**: A DetectorClient instance and a reachable SoC at 192.168.1.100:8000
**WHEN**: `ConnectAsync("192.168.1.100")` is called
**THEN**: `IsConnected` returns true, `DeviceInfo` is populated
**AND**: Connection completes within 10 seconds

---

### AC-002: Single Frame Capture

**GIVEN**: Connected DetectorClient
**WHEN**: `StartAcquisitionAsync(ScanMode.Single)` is called, then `CaptureFrameAsync(5s)` is called
**THEN**: A Frame object is returned with correct Width, Height, BitDepth
**AND**: PixelData contains width*height ushort values
**AND**: SequenceNumber is greater than 0

---

### AC-003: Continuous Streaming

**GIVEN**: Connected DetectorClient
**WHEN**: `StartAcquisitionAsync(ScanMode.Continuous)` is called and 100 frames are consumed via `StreamFramesAsync`
**THEN**: All 100 frames have sequential SequenceNumbers (no gaps)
**AND**: Frame drop rate is < 1% (< 1 drop per 100 frames in test conditions)

---

### AC-004: Out-of-Order Packet Reassembly

**GIVEN**: FrameReassembler with shuffled input packets
**WHEN**: All packets of a frame are delivered in random order
**THEN**: The reassembled frame matches the expected pixel data bit-for-bit
**AND**: Reassembly completes within 10 ms of last packet arrival

---

### AC-005: Missing Packet Handling

**GIVEN**: FrameReassembler with 5% of packets missing
**WHEN**: Frame timeout (2s) expires
**THEN**: Missing regions are zero-filled (0x0000)
**AND**: Frame is marked with appropriate flag
**AND**: ErrorOccurred event is raised with INCOMPLETE_FRAME code

---

### AC-006: TIFF Storage

**GIVEN**: A captured Frame with 2048x2048, 16-bit pixel data
**WHEN**: `SaveFrameAsync(frame, "test.tiff", ImageFormat.Tiff)` is called
**THEN**: File is created with correct TIFF header and tags
**AND**: Re-reading the file produces identical pixel data (round-trip fidelity)
**AND**: File size is approximately 8 MB (2048*2048*2 bytes + TIFF overhead)

---

### AC-007: RAW Storage

**GIVEN**: A captured Frame with 2048x2048, 16-bit pixel data
**WHEN**: `SaveFrameAsync(frame, "test.raw", ImageFormat.Raw)` is called
**THEN**: Binary file contains exactly 8,388,608 bytes (2048*2048*2)
**AND**: JSON sidecar (test.json) contains width, height, bitDepth, timestamp
**AND**: Re-reading raw + sidecar produces identical Frame

---

### AC-008: CRC-16 Validation

**GIVEN**: UDP packets with FrameHeader including CRC-16
**WHEN**: CRC is calculated over header bytes 0-27 (fields: magic, version, reserved0, frame_id, packet_seq, total_packets, timestamp_ns, rows, cols)
**THEN**: Calculated CRC matches header.crc16 (offset 28, uint16) for valid packets
**AND**: Invalid CRC causes packet discard and warning log

**Reference**: CRC field layout and algorithm defined in `docs/api/ethernet-protocol.md` Section 2.2 (field crc16 at offset 28) and Section 7.2 (frame header CRC scope and pseudocode).

---

### AC-009: Connection Loss Recovery

**GIVEN**: Active continuous scan
**WHEN**: Network connection is interrupted for 15 seconds
**THEN**: ConnectionChanged event reports Disconnected, then Reconnecting
**AND**: After network recovery, ConnectionChanged reports Connected
**AND**: Scan resumes automatically within 10 seconds of network recovery

---

### AC-010: Frame Drop Rate Under Load

**GIVEN**: Connected DetectorClient at Intermediate-A tier (2048x2048@15fps)
**WHEN**: 10,000 frames are captured continuously
**THEN**: DroppedFrames < 1 (0.01% rate)
**AND**: All non-dropped frames have correct pixel data

---

### AC-SDK-010a: GC Pressure Stress Test

**GIVEN**: ConnectedDetectorClient, Intermediate-A tier (2048x2048@15fps)
**WHEN**: Continuous 10,000-frame capture with dotnet-trace GC event monitoring
**THEN**: Gen2 GC occurrences < 5 (over 10,000 frames)
**AND**: ArrayPool<ushort> return rate >= 99% (no leaks)
**AND**: 99th percentile frame processing latency < 50ms (including GC pauses)
**AND**: Maximum heap growth < 100MB (after initial 10 frames)

---

## Quality Gates

### QG-001: TRUST 5 Framework Compliance

- **Tested**: 85%+ code coverage (TDD for all new SDK code)
- **Readable**: English code comments, clear API naming
- **Unified**: Consistent C# coding style (EditorConfig)
- **Secured**: No secret exposure, input validation on network data
- **Trackable**: Git-tracked with conventional commits

### QG-002: API Design Review

- All public types documented with XML comments
- No breaking changes without version bump
- Thread safety documented for each public member
- Disposal pattern correctly implemented for Frame and DetectorClient

---

## Dependencies

### External Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| System.IO.Pipelines | 8.0.0 | High-performance I/O |
| LibTiff.NET | 2.4.6+ | TIFF file read/write |
| fo-dicom | 5.0+ | DICOM format (optional) |

### Internal Dependencies

| Module | Dependency | Interface |
|--------|-----------|-----------|
| XrayDetector.Sdk | Common.Dto | ISimulator, DTOs |
| XrayDetector.Sdk.Tests | XrayDetector.Sdk | Unit test target |
| GUI.Application | XrayDetector.Sdk | IDetectorClient |
| IntegrationRunner | XrayDetector.Sdk | IDetectorClient |

---

## Traceability

### Parent Documents

- **SPEC-ARCH-001**: P0 Architecture Decisions (REQ-ARCH-008, REQ-ARCH-015)
- **X-ray_Detector_Optimal_Project_Plan.md**: Section 5.4 Phase 5 (Host SDK Development)

### Architecture Reference

- **docs/architecture/host-sdk-design.md**: Detailed module design, class diagrams, threading model
- **docs/api/ethernet-protocol.md**: Network protocol specification
- **docs/api/host-sdk-api.md**: Public API reference

### Child Documents

- Integration test scenarios (IT-01 through IT-10) reference SDK API
- GUI.Application design depends on IDetectorClient interface

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | ABYZ-Lab Agent (architect) | Initial SPEC creation for Host SDK requirements |
| 1.0.1 | 2026-02-17 | manager-quality | Fixed CRIT-007: AC-008 expanded with field-by-field CRC scope and offset reference; Protocol Constraints updated with Frame Header CRC row and algorithm details |
| 1.1.0 | 2026-02-17 | ABYZ-Lab Agent | MAJOR-007: Expanded Platform Constraints table to explicitly list 10 GbE as required for Mid-B tier and above. Added note that 1 GbE (~0.94 Gbps effective) supports only Minimum and Mid-A tiers. Added requirement for SDK to warn when network interface cannot sustain configured tier data rate. |
| 1.1.1 | 2026-02-18 | manager-docs | Documentation sync v1.1.1: Status changed to "implemented". Added DicomEncoder.cs implementation note. Updated CHANGELOG.md. |
| 1.1.1 | 2026-02-18 | manager-docs | Documentation sync v1.1.1: Status changed to "implemented". Added DicomEncoder.cs implementation note. Updated CHANGELOG.md. |

---

## Review Record

- Date: 2026-02-17
- Reviewer: manager-quality
- Status: Approved
- TRUST 5: T:5 R:5 U:5 S:4 T:5

---

**END OF SPEC**
