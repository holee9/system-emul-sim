# SPEC-SDK-001: Acceptance Criteria and Test Scenarios

## Overview

This document defines the acceptance criteria, test scenarios, and quality gates for SPEC-SDK-001 (Host SDK Requirements - DetectorClient API and Frame Processing). All scenarios use Given/When/Then format for clarity and traceability.

---

## Test Scenarios

### Scenario 1: Connection Management

**Objective**: Verify that DetectorClient establishes and manages UDP connections with the SoC Controller correctly.

```gherkin
Given a DetectorClient instance
And a reachable SoC at 192.168.1.100:8000
When ConnectAsync("192.168.1.100", 8000) is called
Then IsConnected shall return true
And DeviceInfo shall be populated with SoC identification
And ConnectionChanged event shall report Connected state
And connection shall complete within 10 seconds
```

**Success Criteria**:
- PING command (0x0007) sent to SoC and response received
- ConnectionChanged event raised with Connected state
- DeviceInfo populated from SoC response payload
- Connection completes within 10 seconds (REQ-SDK-010)

**Verification Method**: Unit test with mock UDP socket, integration test with SoC simulator

---

### Scenario 2: Connection Timeout

**Objective**: Verify that DetectorClient handles unreachable SoC gracefully.

```gherkin
Given a DetectorClient instance
And no SoC reachable at 192.168.1.200:8000
When ConnectAsync("192.168.1.200", 8000) is called
Then DetectorConnectionException shall be thrown
And the exception message shall indicate timeout
And IsConnected shall remain false
And elapsed time shall be approximately 10 seconds
```

**Success Criteria**:
- DetectorConnectionException thrown after 10-second timeout
- No resource leaks (UDP socket released)
- IsConnected remains false after failure
- ErrorOccurred event raised with CONNECTION_TIMEOUT code

**Verification Method**: Unit test with non-responsive mock socket

---

### Scenario 3: Single Frame Capture

**Objective**: Verify that single-frame capture workflow returns a complete, valid frame.

```gherkin
Given a connected DetectorClient
When StartAcquisitionAsync(ScanMode.Single) is called
And CaptureFrameAsync(TimeSpan.FromSeconds(5)) is called
Then a Frame object shall be returned
And Frame.Width and Frame.Height shall match configured resolution
And Frame.BitDepth shall be 16
And Frame.PixelData shall contain Width * Height ushort values
And Frame.SequenceNumber shall be greater than 0
And the SDK shall automatically stop the scan after one frame
And IsScanning shall return false after capture completes
```

**Success Criteria**:
- Frame pixel data is complete (Width * Height ushort values)
- Frame metadata (Width, Height, BitDepth, SequenceNumber, Timestamp) is populated
- Scan automatically stops after single frame (REQ-SDK-020)
- No additional frames queued after single capture

**Verification Method**: Integration test with FPGA simulator, unit test with mock packet stream

---

### Scenario 4: Continuous Streaming

**Objective**: Verify that continuous streaming delivers sequential frames with minimal drops.

```gherkin
Given a connected DetectorClient
When StartAcquisitionAsync(ScanMode.Continuous) is called
And 100 frames are consumed via StreamFramesAsync
Then all 100 frames shall have sequential SequenceNumbers (no gaps)
And frame drop rate shall be < 1% (< 1 drop per 100 frames in test conditions)
And each frame shall have valid pixel data
And FrameReceived event shall be raised for each frame
And StopAcquisitionAsync shall gracefully end the stream
```

**Success Criteria**:
- 100 frames received with sequential SequenceNumbers
- Frame drop rate < 1% in test conditions
- FrameReceived event raised for each complete frame (REQ-SDK-015)
- StreamFramesAsync yields frames as IAsyncEnumerable (REQ-SDK-014)
- StopAcquisitionAsync completes within 1 frame period (REQ-SDK-012)

**Verification Method**: Integration test with FPGA simulator at Intermediate-A tier

---

### Scenario 5: Out-of-Order Packet Reassembly

**Objective**: Verify that FrameReassembler correctly handles shuffled packets.

```gherkin
Given a FrameReassembler instance
And a complete frame split into N packets
When all N packets are delivered in random order
Then the reassembled frame shall match the expected pixel data bit-for-bit
And reassembly shall complete within 10 ms of last packet arrival
And no pixel data corruption shall occur
```

**Success Criteria**:
- Bit-for-bit match between reassembled frame and original frame data
- Reassembly latency < 10 ms from last packet to Frame event
- Works with 2048x2048 frame (approximately 1,024 packets at 8192-byte payload)
- Works with 3072x3072 frame (approximately 2,304 packets at 8192-byte payload)

**Verification Method**: Unit test with randomized packet ordering, property-based test

---

### Scenario 6: Missing Packet Handling

**Objective**: Verify that FrameReassembler handles missing packets with zero-fill and error reporting.

```gherkin
Given a FrameReassembler instance
And a complete frame with 5% of packets deliberately missing
When frame timeout (2 seconds) expires
Then missing pixel regions shall be zero-filled (0x0000)
And the frame shall be marked with IncompleteFrame flag
And ErrorOccurred event shall be raised with INCOMPLETE_FRAME code
And the error shall report which packet indices were missing
```

**Success Criteria**:
- Zero-fill applied to missing packet regions (REQ-SDK-005 implied by AC-005)
- Frame marked with appropriate IncompleteFrame flag
- ErrorOccurred event raised with INCOMPLETE_FRAME code (REQ-SDK-016)
- Frame timeout triggers after 2 seconds of incomplete data

**Verification Method**: Unit test with controlled packet loss, fault injection test

---

### Scenario 7: CRC-16 Validation

**Objective**: Verify that CRC-16/CCITT validation correctly identifies valid and corrupt packets.

```gherkin
Given UDP packets with FrameHeader including CRC-16 field
When CRC-16/CCITT is calculated over header bytes 0-27 (polynomial 0x8408)
Then calculated CRC shall match header.crc16 for valid packets
And packets with invalid CRC shall be discarded
And a warning log shall be emitted for each CRC failure
And CRC failure count shall be tracked in ScanStatus
```

**Success Criteria**:
- CRC-16/CCITT implementation matches polynomial 0x8408 specification
- Valid packets pass CRC check and are processed
- Corrupt packets are discarded (not reassembled)
- Warning log emitted per CRC failure
- ScanStatus tracks cumulative CRC failure count

**Verification Method**: Unit test with known CRC test vectors, fuzz testing with random corruption

---

### Scenario 8: TIFF Storage

**Objective**: Verify that TIFF image storage preserves 16-bit pixel data with correct metadata.

```gherkin
Given a captured Frame with 2048x2048, 16-bit pixel data
When SaveFrameAsync(frame, "test.tiff", ImageFormat.Tiff) is called
Then a TIFF file shall be created at "test.tiff"
And the TIFF shall contain correct tags:
  | Tag | Value |
  | IMAGEWIDTH | 2048 |
  | IMAGELENGTH | 2048 |
  | BITSPERSAMPLE | 16 |
  | SAMPLESPERPIXEL | 1 |
  | PHOTOMETRIC | MINISBLACK |
And re-reading the file shall produce identical pixel data (round-trip fidelity)
And file size shall be approximately 8 MB (2048*2048*2 + TIFF overhead)
```

**Success Criteria**:
- TIFF file created with correct header and metadata tags (REQ-SDK-023)
- Round-trip fidelity: read back produces identical pixel data
- File size approximately 8 MB for 2048x2048 16-bit image
- LibTiff.NET used for TIFF generation

**Verification Method**: Unit test with known pixel data, round-trip comparison

---

### Scenario 9: RAW Storage

**Objective**: Verify that RAW binary storage preserves pixel data with JSON metadata sidecar.

```gherkin
Given a captured Frame with 2048x2048, 16-bit pixel data
When SaveFrameAsync(frame, "test.raw", ImageFormat.Raw) is called
Then a binary file "test.raw" shall contain exactly 8,388,608 bytes (2048*2048*2)
And pixel data shall be little-endian uint16
And a JSON sidecar "test.json" shall be created containing:
  | Field | Value |
  | width | 2048 |
  | height | 2048 |
  | bitDepth | 16 |
  | timestamp | ISO 8601 string |
  | sequenceNumber | integer > 0 |
And re-reading raw + sidecar shall produce an identical Frame
```

**Success Criteria**:
- Binary file size exactly Width * Height * 2 bytes (REQ-SDK-024)
- Little-endian byte order for uint16 pixel values
- JSON sidecar contains all required metadata fields
- Round-trip fidelity: read back produces identical Frame

**Verification Method**: Unit test with known pixel data, round-trip comparison, byte-level verification

---

### Scenario 10: Connection Loss Recovery

**Objective**: Verify that auto-reconnect handles transient network failures.

```gherkin
Given an active continuous scan
When network connection is interrupted for 15 seconds
Then ConnectionChanged event shall report Disconnected state
And ConnectionChanged event shall report Reconnecting state
And auto-reconnect shall attempt every 5 seconds (REQ-SDK-025)
And after network recovery, ConnectionChanged shall report Connected
And scan shall resume automatically within 10 seconds of network recovery
And frames captured after reconnection shall have correct SequenceNumbers
```

**Success Criteria**:
- ConnectionChanged event sequence: Connected -> Disconnected -> Reconnecting -> Connected
- Auto-reconnect interval is 5 seconds (REQ-SDK-025)
- Scan resumes automatically in Continuous mode after reconnection
- No duplicate frames after reconnection
- DroppedFrames counter updated for frames lost during disconnection

**Verification Method**: Integration test with network fault injection (firewall rule toggle)

---

### Scenario 11: Frame Drop Rate Under Load

**Objective**: Verify that frame drop rate stays below 0.01% during sustained capture.

```gherkin
Given a connected DetectorClient at Intermediate-A tier (2048x2048@15fps)
When 10,000 frames are captured continuously
Then DroppedFrames shall be < 1 (0.01% rate)
And all non-dropped frames shall have correct pixel data
And ScanStatus.CurrentThroughputGbps shall be approximately 1.01 Gbps
And ScanStatus.FramesReceived shall equal 10,000 minus DroppedFrames
```

**Success Criteria**:
- Frame drop rate < 0.01% over 10,000 frames (REQ-SDK-032)
- All non-dropped frames pass CRC validation
- Throughput measurement approximately matches expected data rate
- FramesReceived + DroppedFrames = total frames transmitted by SoC

**Verification Method**: Stress test with FPGA simulator at Intermediate-A tier, performance profiling

---

### Scenario 12: GC Pressure Stress Test

**Objective**: Verify that ArrayPool memory management prevents GC pressure during sustained capture.

```gherkin
Given a connected DetectorClient at Intermediate-A tier (2048x2048@15fps)
When continuous 10,000-frame capture runs with dotnet-trace GC event monitoring
Then Gen2 GC occurrences shall be < 5 (over 10,000 frames)
And ArrayPool<ushort> return rate shall be >= 99% (no buffer leaks)
And 99th percentile frame processing latency shall be < 50ms (including GC pauses)
And maximum heap growth shall be < 100MB (after initial 10 frames stabilization)
```

**Success Criteria**:
- Gen2 GC < 5 occurrences over 10,000 frames (REQ-SDK-031)
- ArrayPool return rate >= 99% (Frame.Dispose returns buffers)
- P99 latency < 50ms (no GC-induced spikes)
- Heap growth < 100MB after stabilization

**Verification Method**: Performance test with dotnet-trace, GC event analysis, memory profiling

---

### Scenario 13: Disconnect and Resource Cleanup

**Objective**: Verify that DisconnectAsync properly releases all resources.

```gherkin
Given a connected DetectorClient with an active scan
When DisconnectAsync() is called
Then DISCONNECT command (0x0008) shall be sent to SoC
And any in-progress scan shall complete or timeout within 5 seconds
And UDP sockets shall be released
And frame buffers shall be returned to ArrayPool
And IsConnected shall return false
And IsScanning shall return false
And ConnectionChanged event shall report Disconnected
```

**Success Criteria**:
- DISCONNECT command sent to SoC (REQ-SDK-018)
- All network resources released (no socket leaks)
- All frame buffers returned to ArrayPool (no memory leaks)
- IAsyncDisposable pattern correctly implemented
- No exceptions thrown during cleanup

**Verification Method**: Unit test with mock socket, resource leak detection test

---

### Scenario 14: Status Polling

**Objective**: Verify that GetStatusAsync returns accurate scan health metrics.

```gherkin
Given a connected DetectorClient during active continuous scan
When GetStatusAsync() is called repeatedly (at 1 Hz)
Then ScanStatus shall contain:
  | Field | Type | Constraint |
  | IsConnected | bool | true during active scan |
  | IsScanning | bool | true during active scan |
  | DroppedFrames | long | cumulative since ConnectAsync |
  | FramesReceived | long | cumulative since ConnectAsync |
  | CurrentThroughputGbps | double | approximately matches tier data rate |
And status values shall be updated at 1 Hz minimum
And all counters shall be cumulative since ConnectAsync (REQ-SDK-019)
```

**Success Criteria**:
- All ScanStatus fields populated correctly
- Status update frequency >= 1 Hz
- Throughput measurement within 10% of expected data rate
- Counters reset on new ConnectAsync call

**Verification Method**: Integration test with simulator, time-series status verification

---

### Scenario 15: Calibration Mode

**Objective**: Verify that calibration scan mode captures dark frames correctly.

```gherkin
Given a connected DetectorClient
When StartAcquisitionAsync(ScanMode.Calibration) is called
And CaptureFrameAsync(TimeSpan.FromSeconds(5)) is called
Then the returned frame shall have flags.calibration = 1
And pixel data shall represent dark frame values (gate OFF during exposure)
And frame metadata shall indicate calibration mode
```

**Success Criteria**:
- Calibration flag set in frame header (REQ-SDK-022)
- START_SCAN command sent with calibration mode parameter
- Frame data represents dark frame (no X-ray exposure)

**Verification Method**: Integration test with simulator in calibration mode

---

## Edge Case Testing

### Edge Case 1: Concurrent API Calls

**Scenario**:
```gherkin
Given a connected DetectorClient with an active continuous scan
When CaptureFrameAsync and StreamFramesAsync are called simultaneously
Then both shall operate independently without deadlock
And both shall receive frames from the internal queue
And no thread-safety violations shall occur
```

**Expected Outcome**:
- No deadlocks or race conditions
- Internal bounded queue (16 frames) distributes correctly
- Thread-safe event invocation

**Verification Method**: Concurrent stress test with multiple consumers, thread sanitizer analysis

---

### Edge Case 2: Bounded Queue Overflow

**Scenario**:
```gherkin
Given a connected DetectorClient in ScanMode.Continuous
And the consumer is not reading frames (paused consumer)
When internal frame queue reaches 16 frames capacity (REQ-SDK-021)
Then the oldest frame shall be dropped from the queue
And the newest frame shall be enqueued
And DroppedFrames counter shall increment
And no memory growth shall occur (dropped frames return buffers to pool)
```

**Expected Outcome**:
- Bounded queue prevents unbounded memory growth
- Oldest frames dropped when queue full (REQ-SDK-021)
- Frame buffers returned to ArrayPool on drop
- DroppedFrames counter accurately reflects overflow drops

**Verification Method**: Unit test with paused consumer, memory monitoring

---

### Edge Case 3: Maximum Frame Size (Target Tier)

**Scenario**:
```gherkin
Given DetectorClient configured for Final target tier (3072x3072, 16-bit, 15fps)
When a single frame is captured
Then frame pixel data shall contain 9,437,184 ushort values (3072*3072)
And frame memory allocation shall use ArrayPool (no per-frame heap allocation)
And total frame memory shall be approximately 18 MB (3072*3072*2 bytes)
And frame reassembly shall complete within 10 ms of last packet
```

**Expected Outcome**:
- Large frame (18 MB) handled without allocation pressure
- ArrayPool provides pre-allocated buffers for large frames
- Reassembly latency within 10 ms specification

**Verification Method**: Performance test at target tier resolution

---

### Edge Case 4: Rapid Connect/Disconnect Cycles

**Scenario**:
```gherkin
Given a DetectorClient instance
When ConnectAsync and DisconnectAsync are called 100 times in rapid succession
Then no resource leaks shall occur (sockets, threads, buffers)
And each cycle shall complete successfully
And final state shall be disconnected
And memory usage shall return to baseline after all cycles
```

**Expected Outcome**:
- No socket leaks (all UDP sockets released)
- No thread leaks (all background tasks cancelled)
- No memory leaks (ArrayPool buffers returned)
- Stable operation after 100 cycles

**Verification Method**: Stress test with resource monitoring, finalizer analysis

---

### Edge Case 5: Invalid API Usage

**Scenario**:
```gherkin
Given a DetectorClient instance that is NOT connected
When StartAcquisitionAsync(ScanMode.Single) is called
Then InvalidOperationException shall be thrown
And the exception message shall indicate "Not connected"

Given a connected DetectorClient with an active scan
When StartAcquisitionAsync(ScanMode.Continuous) is called again
Then InvalidOperationException shall be thrown
And the exception message shall indicate "Scan already active"
```

**Expected Outcome**:
- Clear exception messages for invalid state transitions (REQ-SDK-011)
- No state corruption after invalid API calls
- SDK remains usable after exception recovery

**Verification Method**: Unit test for all invalid state transitions

---

### Edge Case 6: Frame Timeout During Single Capture

**Scenario**:
```gherkin
Given a connected DetectorClient
And SoC is not transmitting any frame data
When CaptureFrameAsync(TimeSpan.FromSeconds(2)) is called
Then TimeoutException shall be thrown after 2 seconds
And no partial frames shall be leaked
And the SDK shall remain in a valid state for subsequent operations
```

**Expected Outcome**:
- TimeoutException thrown at specified timeout (REQ-SDK-013)
- Incomplete frame slots cleaned up
- SDK state machine returns to idle

**Verification Method**: Unit test with non-responsive mock SoC

---

## Performance Criteria

### Frame Reassembly Latency

**Criterion**: Frame reassembly shall complete within 10 ms of last packet arrival.

**Metrics**:
| Tier | Resolution | Packets/Frame | Reassembly Target |
|------|-----------|---------------|-------------------|
| Intermediate-A | 2048x2048 | ~1,024 | < 10 ms |
| Final Target | 3072x3072 | ~2,304 | < 10 ms |

**Acceptance Threshold**: 99th percentile reassembly latency < 10 ms

**Verification Method**: Latency measurement with high-resolution timer, percentile analysis

---

### Frame Drop Rate

**Criterion**: Frame drop rate shall be below 0.01% during continuous capture at the active performance tier.

**Metrics**:
| Tier | Data Rate | Frames Tested | Max Drops |
|------|-----------|---------------|-----------|
| Intermediate-A | 1.01 Gbps | 10,000 | < 1 |
| Final Target | 2.26 Gbps | 10,000 | < 1 |

**Acceptance Threshold**: DroppedFrames < 1 per 10,000 frames (REQ-SDK-032)

**Verification Method**: Long-duration stress test, drop counter analysis

---

### GC Pressure

**Criterion**: Gen2 GC occurrences shall be minimal during sustained capture.

**Metrics**:
| Metric | Target | Measurement Tool |
|--------|--------|-----------------|
| Gen2 GC count | < 5 per 10,000 frames | dotnet-trace |
| ArrayPool return rate | >= 99% | Custom counter |
| P99 frame latency | < 50 ms | High-resolution timer |
| Heap growth | < 100 MB after stabilization | dotnet-counters |

**Acceptance Threshold**: All metrics within targets (REQ-SDK-031)

**Verification Method**: dotnet-trace GC event monitoring, memory profiling

---

### Connection Establishment

**Criterion**: Connection to SoC shall complete within 10 seconds.

**Metrics**:
| Scenario | Target | Timeout |
|----------|--------|---------|
| Normal connection (LAN) | < 1 second | 10 seconds |
| Connection timeout (unreachable) | 10 seconds | 10 seconds |
| Auto-reconnect attempt | 5 second interval | N/A |

**Acceptance Threshold**: ConnectAsync completes within 10 seconds (REQ-SDK-010)

**Verification Method**: Timer measurement, network latency simulation

---

### Storage Performance

**Criterion**: Frame storage shall not block the frame processing pipeline.

**Metrics**:
| Format | Frame Size | Write Target | Disk I/O |
|--------|-----------|-------------|----------|
| TIFF | ~8 MB (2048x2048) | < 100 ms | Sequential write |
| TIFF | ~18 MB (3072x3072) | < 200 ms | Sequential write |
| RAW | ~8 MB (2048x2048) | < 50 ms | Sequential write |
| RAW | ~18 MB (3072x3072) | < 100 ms | Sequential write |

**Acceptance Threshold**: SaveFrameAsync completes without blocking frame reception

**Verification Method**: Concurrent capture + save test, I/O performance measurement

---

## Quality Gates

### TRUST 5 Framework Compliance

**Tested (T)**:
- 85%+ code coverage for all SDK modules (TDD for new code)
- Unit tests for all public API methods (IDetectorClient)
- Integration tests with FPGA simulator
- Performance tests for frame drop rate, GC pressure, latency
- Edge case tests for error handling and recovery

**Readable (R)**:
- English XML documentation comments on all public types and methods
- Clear API naming following .NET conventions (async suffix, event patterns)
- Thread safety documented for each public member
- Code comments in English (per language.yaml)

**Unified (U)**:
- Consistent C# coding style enforced by EditorConfig
- Naming conventions: PascalCase for public members, camelCase for locals
- Consistent error handling pattern (exceptions for errors, events for notifications)
- Common.Dto shared types prevent duplication across modules

**Secured (S)**:
- No secret exposure in SDK code or configuration
- Input validation on all network data (CRC-16, magic number, payload size)
- Buffer overflow protection (bounded queue, ArrayPool size limits)
- No unsafe code blocks in public API surface

**Trackable (T)**:
- Git-tracked with conventional commits (feat, fix, test, refactor)
- SPEC-SDK-001 traceability tags on all implementation commits
- Version history maintained in SPEC revision table
- API changes tracked through interface versioning

---

### API Design Review

**Review Criteria**:
- All public types documented with XML comments
- No breaking changes without version bump
- Thread safety documented for each public member
- Disposal pattern correctly implemented (IAsyncDisposable)
- CancellationToken support on all async methods
- Event handler patterns follow .NET conventions

**Reviewers**:
- SDK Lead: API design and usability review
- Performance Engineer: Memory allocation and GC impact review
- Integration Engineer: Simulator compatibility and protocol compliance
- Quality Engineer: Test coverage and edge case verification

**Approval Criteria**:
- Zero unresolved API design concerns
- All reviewers sign off on IDetectorClient interface
- Performance benchmarks pass all criteria
- Test coverage >= 85%

---

### Integration Readiness

**Review Criteria**:
- SDK integrates with FPGA simulator (IT-01 through IT-10)
- GUI.Application can use IDetectorClient for frame display
- Network protocol matches docs/api/ethernet-protocol.md specification
- Configuration loading matches detector_config.yaml schema

**Approval Criteria**:
- Integration test suite (IT-01 through IT-10) passes
- GUI preview displays frames at 15 fps
- Frame storage round-trip verified (TIFF and RAW)
- ScanStatus metrics validated against simulator output

---

## Traceability Matrix

| Requirement ID | Acceptance Criterion | Test Scenario | Quality Gate |
|---------------|---------------------|---------------|--------------|
| REQ-SDK-001 | .NET 8.0 LTS framework | Scenario 1 (build target) | Unified |
| REQ-SDK-002 | IDetectorClient API | Scenarios 1-15 | API Design Review |
| REQ-SDK-003 | Common.Dto dependency | Build verification | Unified |
| REQ-SDK-004 | 16-bit pixel data | Scenarios 3, 5, 8, 9 | Tested |
| REQ-SDK-010 | ConnectAsync within 10s | Scenarios 1, 2 | Tested |
| REQ-SDK-011 | StartAcquisitionAsync | Scenario 3, Edge Case 5 | Tested |
| REQ-SDK-012 | StopAcquisitionAsync | Scenario 4 | Tested |
| REQ-SDK-013 | CaptureFrameAsync timeout | Scenario 3, Edge Case 6 | Tested |
| REQ-SDK-014 | StreamFramesAsync | Scenario 4 | Tested |
| REQ-SDK-015 | FrameReceived event | Scenario 4 | Tested |
| REQ-SDK-016 | ErrorOccurred event | Scenarios 6, 10 | Tested |
| REQ-SDK-017 | SaveFrameAsync | Scenarios 8, 9 | Tested |
| REQ-SDK-018 | DisconnectAsync | Scenario 13 | Tested |
| REQ-SDK-019 | GetStatusAsync | Scenario 14 | Tested |
| REQ-SDK-020 | ScanMode.Single | Scenario 3 | Tested |
| REQ-SDK-021 | ScanMode.Continuous (bounded queue) | Scenario 4, Edge Case 2 | Tested |
| REQ-SDK-022 | ScanMode.Calibration | Scenario 15 | Tested |
| REQ-SDK-023 | TIFF storage | Scenario 8 | Tested |
| REQ-SDK-024 | RAW storage | Scenario 9 | Tested |
| REQ-SDK-025 | Auto-reconnect | Scenario 10 | Tested |
| REQ-SDK-030 | Non-blocking I/O | All scenarios (async) | API Design Review |
| REQ-SDK-031 | No per-frame heap allocation | Scenario 12 | Performance |
| REQ-SDK-032 | Frame drop rate < 0.01% | Scenario 11 | Performance |
| REQ-SDK-033 | No exposed threading | API Design Review | API Design Review |
| REQ-SDK-040 | Device discovery (optional) | Deferred | N/A |
| REQ-SDK-041 | DICOM format (optional) | Deferred | N/A |
| REQ-SDK-042 | Window/Level utility (optional) | Deferred to GUI SPEC | N/A |
| REQ-SDK-043 | Frame statistics (optional) | Deferred | N/A |

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (spec-sdk) | Initial acceptance criteria for SPEC-SDK-001 |

---

**END OF ACCEPTANCE CRITERIA**
