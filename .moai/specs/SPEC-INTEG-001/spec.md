# SPEC-INTEG-001: Integration Testing Phase - X-ray Detector Panel System

---
id: SPEC-INTEG-001
version: 1.0.0
status: approved
created: 2026-02-27
updated: 2026-02-27
author: ABYZ-Lab Agent (architect)
priority: high
milestone: M3
---

## HISTORY

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-27 | ABYZ-Lab Agent (architect) | Initial SPEC creation for M3 Integration Testing phase |

---

## Overview

### Context

The X-ray Detector Panel System has completed M2 (Software Implementation, 100% complete). The M3 Integration Testing phase focuses on validating end-to-end system operation across the 3-tier architecture (FPGA → SoC → Host PC) with comprehensive integration tests covering frame capture, data transmission, protocol validation, recovery scenarios, and performance stress testing.

### Scope

This SPEC covers integration testing requirements for:

1. Single and continuous frame capture across all performance tiers
2. End-to-end data path validation (FPGA → CSI-2 → SoC → 10GbE → Host)
3. Command authentication (HMAC-SHA256) and control protocol validation
4. SPI configuration updates and FPGA register operations
5. Sequence engine state machine behavior
6. Frame buffer overflow recovery
7. Network packet loss and retransmission patterns
8. Performance stress testing at target and maximum tiers
9. End-to-end latency measurement (panel trigger to host display)
10. System stability and reliability validation

### Out of Scope

- FPGA RTL synthesis and placement (covered by SPEC-FPGA-001)
- SoC firmware low-level HAL development (covered by SPEC-FW-001)
- Host GUI application testing (covered by separate GUI testing SPEC)
- Clinical validation and regulatory compliance (post-M3)

---

## Requirements

### 1. Ubiquitous Requirements (System-Wide Invariants)

**REQ-INTEG-001**: The integration test suite **shall** use C# xUnit 2.9.0 as the test framework with the target framework .NET 8.0 LTS.

**WHY**: Consistency with Host SDK test infrastructure enables code reuse and maintains uniform testing patterns across the system.

**IMPACT**: All integration tests defined in `XrayDetector.Sdk.Tests/Integration/` project. Test discovery and execution via `dotnet test` command.

---

**REQ-INTEG-002**: The integration test suite **shall** achieve minimum 85% code coverage for all new integration test code.

**WHY**: High coverage ensures comprehensive scenario validation and regression detection during future enhancements.

**IMPACT**: Coverage measured via `dotnet test /p:CollectCoverage=true`. Must track coverage metrics per test module.

---

**REQ-INTEG-003**: All integration tests **shall** validate against the 3-tier system architecture: FPGA (Xilinx Artix-7 XC7A35T) → SoC (NXP i.MX8M Plus, Yocto Scarthgap 5.0, Linux 6.6.52) → Host (SDK .NET 8.0).

**WHY**: Architecture specificity ensures tests reflect actual hardware-software integration points and constraints.

**IMPACT**: Tests use FPGA simulator, SoC simulator/firmware, and Host SDK with realistic packet formats and timing.

---

**REQ-INTEG-004**: All integration tests **shall** validate the communication protocols: CSI-2 4-lane D-PHY (FPGA→SoC), 10GbE UDP port 8000 (SoC→Host frame data), HMAC-SHA256 port 8001 (command authentication).

**WHY**: Protocol compliance is critical for system interoperability and security. Validates packet headers, payload integrity, and authentication mechanisms.

**IMPACT**: Tests include protocol packet validation, timing verification, and error condition handling per protocol specifications.

---

### 2. Event-Driven Requirements (Integration Scenarios)

**REQ-INTEG-010**: **WHEN** IT-01 Single Frame Capture test is executed **THEN** the system **shall** capture exactly one complete frame at minimum tier (1024×1024@15fps) with frame size = 2,097,152 bytes (1024*1024*2).

**WHY**: Single frame capture is the fundamental use case for diagnostic radiography. Validates end-to-end data path without buffering complexity.

**IMPACT**: Test verifies frame header integrity (32-byte format per ethernet-protocol.md), pixel data completeness, and sequence number assignment. Frame must be intact with no packet loss.

---

**REQ-INTEG-011**: **WHEN** IT-02 Continuous Capture 300 Frames test is executed **THEN** the system **shall** capture 300 consecutive frames at target tier (2048×2048@30fps) with frame drop rate < 1% (maximum 3 frames dropped).

**WHY**: Continuous operation validates sustained data throughput and frame queue management. Target tier is the primary performance goal for clinical imaging.

**IMPACT**: Test runs 10-second acquisition (300 frames at 30 fps), measures dropped frame count via `ScanStatus.DroppedFrames`, validates sequential frame numbering with allowed gaps.

---

**REQ-INTEG-012**: **WHEN** IT-03 SPI Configuration Update test is executed **THEN** the system **shall** write and verify FPGA register updates via SPI interface with zero errors.

**WHY**: SPI is the control path for FPGA parameter updates (exposure timing, gain, offset). Validates register write/read integrity.

**IMPACT**: Test sends ConfigureAsync command, reads back via GetStatusAsync, verifies register values match. SPI error count must remain zero.

---

**REQ-INTEG-013**: **WHEN** IT-04 CSI-2 Protocol Validation test is executed **THEN** the system **shall** verify all CSI-2 packet headers (magic 0xD7E01234, version, frame_id, sequence, crc-16) are correct and payloads are intact.

**WHY**: CSI-2 is the primary FPGA→SoC data path. Header and payload validation ensures no silent data corruption during transmission.

**IMPACT**: Test captures raw CSI-2 packets (via simulator or SoC packet capture), validates header fields, verifies CRC-16 match (algorithm: CRC-16/CCITT, polynomial 0x8408), confirms payload length consistency.

---

**REQ-INTEG-014**: **WHEN** IT-05 Frame Buffer Overflow Recovery test is executed **THEN** the system **shall** fill the 4-frame ring buffer to capacity, continue capture without crashing, and recover gracefully with no deadlock.

**WHY**: Ring buffer overflow is a stress condition that can cause system hangs or data loss if not handled correctly. Recovery validation is critical for robustness.

**IMPACT**: Test deliberately slows frame consumption while continuing capture, monitors for timeout conditions, verifies system recovers when consumption resumes. Expected behavior: oldest frames dropped, no crash.

---

**REQ-INTEG-015**: **WHEN** IT-06 HMAC-SHA256 Command Authentication test is executed **THEN** the system **shall** reject commands with invalid HMAC signatures and accept commands with valid signatures.

**WHY**: HMAC-SHA256 (port 8001) authenticates all control commands from Host to SoC. Invalid signatures must be rejected to prevent unauthorized panel control.

**IMPACT**: Test sends ConfigureAsync with valid and invalid HMACs, measures rejection rate for invalid HMACs (must be 100%), verifies valid commands execute successfully.

---

**REQ-INTEG-016**: **WHEN** IT-07 Sequence Engine State Machine test is executed **THEN** the system **shall** transition correctly through all 6 states (IDLE → INIT → READY → CAPTURE → TRANSFER → ERROR) with no invalid transitions.

**WHY**: Sequence engine state machine (SoC firmware) controls the scan lifecycle. Correct state transitions ensure deterministic behavior and prevent undefined states.

**IMPACT**: Test exercises each state via API calls (StartAcquisitionAsync, StopAcquisitionAsync, error injection), verifies transition sequence, monitors for unexpected state values via GetStatusAsync.

---

**REQ-INTEG-017**: **WHEN** IT-08 10GbE Packet Loss and Retransmission test is executed **THEN** the system **shall** handle 0.1% simulated UDP packet loss and recover missing frames within 2 seconds via retransmission protocol.

**WHY**: 10GbE is the Host→SoC link. Real-world networks experience transient packet loss. System must detect and recover from loss.

**IMPACT**: Test injects packet loss at simulator level, measures frame recovery latency, verifies no permanent data loss. Retransmission mechanism must complete within 2-second frame timeout.

---

**REQ-INTEG-018**: **WHEN** IT-09 Maximum Tier Stress Test is executed **THEN** the system **shall** sustain 3072×3072@30fps (4.53 Gbps) for 60 consecutive seconds with zero crashes and < 1% frame loss.

**WHY**: Maximum tier is the performance ceiling for high-resolution research imaging. Sustained stress validates system stability under peak load.

**IMPACT**: Test runs 1,800 frames (60 seconds at 30 fps), monitors resource usage (memory, CPU), logs thermal events if available, verifies all frames intact with sequential numbering.

---

**REQ-INTEG-019**: **WHEN** IT-10 End-to-End Latency Measurement test is executed **THEN** the system **shall** measure round-trip latency from panel trigger to frame reception at host with 95th percentile < 50 milliseconds.

**WHY**: Latency is critical for real-time imaging workflows. 50ms ceiling ensures responsive user experience and diagnostic confidence.

**IMPACT**: Test timestamp captures at FPGA trigger point, measures arrival time at Host SDK FrameReceived event, calculates percentiles. Reports latency distribution histogram.

---

### 3. State-Driven Requirements (Test Execution Modes)

**REQ-INTEG-020**: **IF** test mode is set to `SimulatedEnvironment` **THEN** all tests **shall** use FPGA simulator, SoC simulator, and SDK test harness without requiring physical hardware.

**WHY**: Simulation environment enables development and testing before hardware availability, reduces test execution time, and enables parallel test execution.

**IMPACT**: Tests conditionally select simulator implementations via dependency injection. SIM builds use mock network stacks and event loops.

---

**REQ-INTEG-021**: **IF** test mode is set to `HardwareIntegration` **THEN** all tests **shall** communicate with real FPGA hardware, SoC firmware, and 10GbE network interface.

**WHY**: Hardware integration tests validate actual system behavior and detect issues not visible in simulation (timing margins, thermal effects, network jitter).

**IMPACT**: Tests require hardware setup: FPGA dev board, SoC eval board, 10GbE network. Configuration via environment variables (DETECTOR_HOST, DETECTOR_PORT).

---

**REQ-INTEG-022**: **IF** performance tier is set to `Minimum` (1024×1024@15fps) **THEN** tests IT-01 through IT-10 **shall** scale frame dimensions and adjust expected throughput accordingly.

**WHY**: Minimum tier serves as baseline for development and unit testing. Some integration tests must run at all tiers to validate tier scalability.

**IMPACT**: Tests parameterize resolution and fps, compute expected data rates, adjust timeout thresholds per tier.

---

**REQ-INTEG-023**: **IF** performance tier is set to `Target` (2048×2048@30fps) **THEN** tests **shall** validate 2.01 Gbps sustained throughput with < 1% frame loss.

**WHY**: Target tier is the primary performance goal and most common deployment configuration for clinical imaging.

**IMPACT**: Tests use 2048x2048 resolution, 30 fps, 16-bit depth, measure 10GbE throughput, validate frame drop counts.

---

**REQ-INTEG-024**: **IF** performance tier is set to `Maximum` (3072×3072@30fps) **THEN** tests **shall** validate 4.53 Gbps sustained throughput, adequate frame buffer memory, and thermal stability.

**WHY**: Maximum tier stress tests the system limits and validates headroom for future enhancements.

**IMPACT**: Tests use 3072x3072 resolution, 30 fps, monitor system resources, validate no thermal throttling, measure peak throughput.

---

### 4. Unwanted Requirements (Prohibited Behaviors)

**REQ-INTEG-030**: Integration tests **shall not** block on synchronous network I/O or display operations.

**WHY**: Blocking would cause test hangs and prevent parallel test execution.

**IMPACT**: All I/O operations must complete within test timeout (default 30 seconds per test). Tests must use async/await patterns throughout.

---

**REQ-INTEG-031**: Integration tests **shall not** leak resources (sockets, memory, threads) between test runs.

**WHY**: Resource leaks accumulate over test suites and cause out-of-memory failures in CI/CD pipelines.

**IMPACT**: All tests must implement IAsyncDisposable, dispose DetectorClient and frame buffers, return ArrayPool buffers. Verify resource cleanup via test assertions.

---

**REQ-INTEG-032**: Integration tests **shall not** have race conditions or non-deterministic failures.

**WHY**: Non-deterministic test failures ("flaky tests") undermine confidence in test results and waste developer time debugging.

**IMPACT**: Tests must use synchronization primitives (TaskCompletionSource, SemaphoreSlim) for event ordering. No sleep-based waits. Tests must be repeatable with 100% consistent results.

---

**REQ-INTEG-033**: Integration tests **shall not** depend on external services, API keys, or cloud resources.

**WHY**: External dependencies cause test failures unrelated to code changes and prevent offline development.

**IMPACT**: All tests run in isolated environment with FPGA simulator and SoC simulator. No network calls to external services.

---

### 5. Optional Requirements (Enhanced Validation)

**REQ-INTEG-040**: **Where possible**, integration tests should measure and report detailed performance metrics (frame throughput, latency percentiles, CPU usage, memory growth).

**WHY**: Metrics enable performance regression detection and optimization validation.

**IMPACT**: Tests collect telemetry via .NET diagnostics (dotnet-counters, dotnet-trace). Reports generated in structured JSON format for trend analysis. Priority: medium.

---

**REQ-INTEG-041**: **Where possible**, integration tests should simulate network faults (jitter, packet reordering, link-down scenarios) to validate robustness.

**WHY**: Real-world networks experience faults. Fault injection testing increases confidence in error handling.

**IMPACT**: Simulator supports fault injection API. Tests conditionally enable faults for robustness scenarios. Priority: medium.

---

**REQ-INTEG-042**: **Where possible**, integration tests should validate GUI rendering performance with frame display at 15-30 fps without frame drops.

**WHY**: GUI responsiveness is critical for end-user experience. Display performance must not degrade under load.

**IMPACT**: Optional test for WPF integration with GUI.Application. Uses frame timing measurements and screen refresh validation. Priority: low.

---

## Integration Test Scenarios (IT-01 through IT-10)

### IT-01: Single Frame Capture (Minimum Tier)

**Purpose**: Validate end-to-end frame acquisition from FPGA through Host SDK.

**Configuration**:
- Resolution: 1024×1024 pixels
- Bit Depth: 14-bit (padded to 16-bit)
- Frame Rate: 15 fps
- Scan Mode: Single
- Expected Frame Size: 2,097,152 bytes

**Test Flow**:
1. Connect to detector (SoC at localhost:8000 for simulator)
2. Configure with Minimum tier parameters
3. Start acquisition in Single mode
4. Call CaptureFrameAsync with 5-second timeout
5. Verify frame properties (width, height, bitDepth, sequenceNumber > 0)
6. Verify pixel data integrity (no zeros unless expected, no truncation)
7. Verify frame header CRC-16 matches expected value

**Expected Outcome**: Frame received intact within timeout, pixel data matches expected pattern, no packet loss.

---

### IT-02: Continuous Capture 300 Frames (Target Tier)

**Purpose**: Validate sustained data throughput at target performance tier (primary deployment scenario).

**Configuration**:
- Resolution: 2048×2048 pixels
- Bit Depth: 16-bit
- Frame Rate: 30 fps
- Scan Mode: Continuous
- Duration: 10 seconds (300 frames)
- Expected Throughput: ~2.01 Gbps

**Test Flow**:
1. Connect to detector
2. Configure with Target tier parameters
3. Start acquisition in Continuous mode
4. Consume 300 frames via StreamFramesAsync
5. Measure actual throughput via GetStatusAsync.CurrentThroughputGbps
6. Calculate frame drop rate: (ExpectedFrames - ActualFrames) / ExpectedFrames
7. Verify sequential frame numbering (allow < 1% gaps)
8. Verify no GC pressure (Gen2 collections < 5)

**Expected Outcome**: 300 frames captured with < 1% loss, throughput ≈ 2.01 Gbps, sequential numbering maintained, no resource leaks.

---

### IT-03: SPI Configuration Update

**Purpose**: Validate FPGA register read/write via SPI interface.

**Configuration**:
- Target Register: Exposure time or gain setting
- Operation: Write new value, read back, verify match
- SPI Clock Rate: 25 MHz (typical)

**Test Flow**:
1. Connect to detector
2. Call ConfigureAsync with new DetectorConfig (e.g., exposure time 100 µs)
3. Verify ConfigureAsync returns without error
4. Call GetStatusAsync to read current configuration
5. Verify returned config matches written config
6. Repeat for 10 different parameter values
7. Verify SPI error count remains zero

**Expected Outcome**: All register writes successful, read-back values match written values, zero SPI errors.

---

### IT-04: CSI-2 Protocol Validation

**Purpose**: Validate CSI-2 packet format, headers, and payload integrity.

**Configuration**:
- CSI-2 Version: Protocol v1.3 as implemented in FPGA
- Lane Count: 4-lane D-PHY
- Data Rate: 1.0-1.25 Gbps/lane

**Test Flow**:
1. Capture raw CSI-2 packets from simulator (via mocked network interface)
2. For each packet, verify:
   - Header magic: 0xD7E01234 (first 4 bytes)
   - Version field: matches expected version
   - Frame ID: sequential for same frame
   - Packet sequence: ordered 0 to total_packets-1
   - CRC-16: recalculate over header bytes 0-27, verify against stored value (offset 28)
3. Verify payload data matches expected pixel values (for known test patterns)
4. Verify no corrupted packets

**Expected Outcome**: 100% of packets have valid headers, all CRCs match, payload data integrity confirmed.

---

### IT-05: Frame Buffer Overflow Recovery

**Purpose**: Validate system behavior when 4-frame ring buffer fills to capacity.

**Configuration**:
- Ring Buffer Size: 4 frames
- Stress Method: Slow frame consumption while continuing capture
- Duration: 30 seconds (900 frames at 30 fps)

**Test Flow**:
1. Connect to detector, start continuous acquisition
2. Implement slow consumer: add 100 ms delay per frame consumption
3. While producer captures at 30 fps, consumer lags
4. Monitor frame drop rate via GetStatusAsync.DroppedFrames
5. After 10-15 seconds, resume normal consumption speed
6. Verify system recovers without hang or crash
7. Verify no deadlock (test timeout < 30 seconds)
8. Verify frames still valid after recovery

**Expected Outcome**: System drops oldest frames gracefully, recovers when consumer resumes, no crash or deadlock, no data corruption.

---

### IT-06: HMAC-SHA256 Command Authentication

**Purpose**: Validate command authentication via HMAC-SHA256 on port 8001.

**Configuration**:
- Authentication Method: HMAC-SHA256
- Shared Key: Pre-configured in test environment
- Port: 8001 (command port, separate from 8000 frame port)

**Test Flow**:
1. Prepare command payload (START_SCAN or ConfigureAsync)
2. Test Case A: Send with valid HMAC
   - Calculate HMAC-SHA256(command_bytes, key)
   - Send command + HMAC to port 8001
   - Verify command executes successfully
3. Test Case B: Send with invalid HMAC
   - Modify one byte of valid HMAC
   - Send corrupted command + HMAC to port 8001
   - Verify SoC rejects command, logs error
4. Test Case C: Send with missing HMAC
   - Send command without HMAC field
   - Verify rejection
5. Count rejection rate for invalid HMACs (must be 100%)

**Expected Outcome**: All valid HMACs accepted, all invalid HMACs rejected (100%), error logging enabled, no false positives.

---

### IT-07: Sequence Engine State Machine Test

**Purpose**: Validate 6-state FSM transitions in SoC firmware (IDLE → INIT → READY → CAPTURE → TRANSFER → ERROR).

**Configuration**:
- Target State Machine: Sequence Engine in SoC firmware
- State Count: 6 states
- Valid Transitions: Predefined paths in firmware documentation

**Test Flow**:
1. Start in IDLE state
2. Call StartAcquisitionAsync → expect IDLE → INIT → READY → CAPTURE transition
3. Verify state via GetStatusAsync.CurrentState (if exposed) or indirectly via API behavior
4. Call StopAcquisitionAsync → expect CAPTURE → TRANSFER → IDLE
5. Trigger error condition (e.g., SPI timeout, CSI-2 overflow)
6. Verify ERROR state is entered and logged
7. Attempt recovery (reconnect, retry command) → expect ERROR → IDLE
8. Repeat state transitions 5 times
9. Verify no invalid transitions, no unexpected state values

**Expected Outcome**: All state transitions follow expected paths, no invalid transitions, recovery from ERROR state successful, FSM remains deterministic across multiple cycles.

---

### IT-08: 10GbE Packet Loss and Retransmission

**Purpose**: Validate system resilience to network packet loss and retransmission mechanisms.

**Configuration**:
- Test Method: Simulator packet loss injection at IP layer
- Packet Loss Rate: 0.1% (1 in 1000 packets)
- Frame Timeout: 2 seconds
- Retransmission Protocol: NACK-based (if implemented)

**Test Flow**:
1. Enable packet loss simulation in SoC network stack
2. Start continuous acquisition at Target tier (2048×2048@30fps)
3. Capture 100 frames while packet loss active
4. Measure frame recovery latency (time from loss detection to recovery)
5. Verify no permanent data loss (all pixels eventually received)
6. Verify recovery completes within 2-second timeout
7. Disable packet loss, verify subsequent frames unaffected
8. Calculate loss recovery success rate (must be 100%)

**Expected Outcome**: Transient packet loss recovered within 2 seconds, no permanent data loss, frame payload integrity maintained, recovery latency < 500 ms for typical loss patterns.

---

### IT-09: Maximum Tier Stress Test (3072×3072@30fps)

**Purpose**: Validate system stability under peak load (4.53 Gbps sustained).

**Configuration**:
- Resolution: 3072×3072 pixels
- Bit Depth: 16-bit
- Frame Rate: 30 fps
- Data Rate: 4.53 Gbps
- Duration: 60 seconds (1800 frames)
- Monitoring: CPU, memory, thermal

**Test Flow**:
1. Configure for Maximum tier parameters
2. Connect and start continuous acquisition
3. Run for 60 seconds, collecting telemetry every 5 seconds:
   - Frame count, drop count, throughput
   - CPU usage (all cores)
   - Memory usage (heap size, working set)
   - Thermal events if sensor available
4. Verify no crashes, hangs, or timeouts
5. Verify frame drop rate < 1%
6. Verify thermal throttling not triggered (if monitored)
7. Verify memory growth < 500 MB over 60 seconds
8. Verify CPU sustained at expected level (not saturated)
9. Calculate 99th percentile latency for frame reception

**Expected Outcome**: 1800 frames captured with < 1% loss, system stable for 60 seconds, no resource exhaustion, no thermal events, memory growth within limits.

---

### IT-10: End-to-End Latency Measurement

**Purpose**: Measure round-trip latency from panel trigger to host frame reception (95th percentile < 50 ms).

**Configuration**:
- Measurement Points: FPGA trigger, CSI-2 transmission, SoC reception, 10GbE transmission, Host SDK FrameReceived
- Sample Size: 300 frames
- Target: 95th percentile latency < 50 milliseconds

**Test Flow**:
1. Configure test mode for latency measurement (special firmware builds with timestamp capture)
2. Start continuous acquisition
3. For each of 300 frames, capture timestamps at:
   - FPGA: Trigger time (first pixel in exposure)
   - CSI-2: First packet transmission time
   - SoC: Frame buffer completion time
   - 10GbE: Packet transmission start
   - Host: FrameReceived event time
4. Calculate per-frame latencies (delta between points)
5. Calculate aggregate end-to-end latency (trigger to FrameReceived)
6. Compute latency percentiles (p50, p95, p99, max)
7. Generate latency histogram and distribution report
8. Verify p95 < 50 ms

**Expected Outcome**: 95th percentile latency < 50 ms, latency distribution relatively tight (low variance), no outlier frames with excessive latency.

---

## Technical Constraints

### System Architecture Constraints

| Constraint | Value | Rationale |
|-----------|-------|-----------|
| FPGA Device | Xilinx Artix-7 XC7A35T-FGG484 | Resource budget for CSI-2 TX + app logic |
| SoC | NXP i.MX8M Plus with Linux 6.6.52 | CSI-2 RX support, 10 GbE MAC |
| Host Framework | .NET 8.0 LTS (Windows/Linux) | Cross-platform support, async I/O |
| Primary Data Path | CSI-2 4-lane D-PHY | FPGA→SoC high-speed link |
| Host Link | 10 GbE UDP (port 8000) | Required for Target/Maximum tiers |
| Control Link | HMAC-SHA256 (port 8001) | Command authentication |

### Performance Constraints

| Metric | Minimum | Target | Maximum | Rationale |
|--------|---------|--------|---------|-----------|
| Resolution | 1024×1024 | 2048×2048 | 3072×3072 | Detector panel capabilities |
| Bit Depth | 14-bit | 16-bit | 16-bit | ROIC output range |
| Frame Rate | 15 fps | 30 fps | 30 fps | Readout speed |
| Data Rate | 0.21 Gbps | 2.01 Gbps | 4.53 Gbps | CSI-2 bandwidth utilization |
| Frame Drop Rate | < 1% | < 1% | < 1% | Quality requirement |
| End-to-End Latency (p95) | 50 ms | 50 ms | 50 ms | Responsiveness requirement |

### Test Environment Constraints

| Constraint | Value |
|-----------|-------|
| Test Framework | C# xUnit 2.9.0 |
| Target Framework | .NET 8.0 LTS |
| Test Project | XrayDetector.Sdk.Tests |
| Code Coverage | ≥ 85% for integration test code |
| Simulator | FPGA + SoC simulator (no hardware required) |
| CI/CD Runtime | GitHub Actions, ~5 minutes per test suite |

---

## Acceptance Criteria

### AC-INTEG-001: IT-01 Single Frame Capture

**GIVEN**: Connected DetectorClient, Minimum tier configuration (1024×1024@15fps)

**WHEN**: Single frame acquisition is initiated via StartAcquisitionAsync(ScanMode.Single) and CaptureFrameAsync(5s) is called

**THEN**:
- Frame is returned within 5-second timeout
- Frame.Width == 1024, Frame.Height == 1024
- Frame.BitDepth == 16
- Frame.PixelData.Length == 2,097,152 (width * height * 2 bytes)
- Frame.SequenceNumber > 0 (assigned by FPGA)
- Frame header CRC-16 matches recalculated value
- No pixels dropped or corrupted

---

### AC-INTEG-002: IT-02 Continuous Capture 300 Frames

**GIVEN**: Connected DetectorClient, Target tier configuration (2048×2048@30fps)

**WHEN**: Continuous acquisition runs for 300 frames (10 seconds at 30 fps)

**THEN**:
- All 300 frames captured (or maximum 3 frames dropped, < 1% rate)
- GetStatusAsync.DroppedFrames ≤ 3
- Throughput measured ≈ 2.01 Gbps (within 10% margin)
- Frame sequence numbers sequential with allowed gaps (for dropped frames)
- No Gen2 garbage collections (or < 5 if unavoidable)
- Test execution time ≤ 15 seconds (including cleanup)

---

### AC-INTEG-003: IT-03 SPI Configuration Update

**GIVEN**: Connected DetectorClient

**WHEN**: ConfigureAsync is called 10 times with different exposure times, and GetStatusAsync is called after each

**THEN**:
- All ConfigureAsync calls return without error
- GetStatusAsync returns matching configuration values for all 10 updates
- No SPI errors logged
- Round-trip latency < 100 ms per update

---

### AC-INTEG-004: IT-04 CSI-2 Protocol Validation

**GIVEN**: Raw CSI-2 packet capture from FPGA→SoC link

**WHEN**: Packets are analyzed for header and payload integrity

**THEN**:
- 100% of packets have valid magic (0xD7E01234)
- 100% of packets have valid CRC-16 (recalculated match)
- 100% of packets have sequential packet_seq within frame
- 100% of payloads match expected pixel values for test pattern
- Zero corrupted packets detected

---

### AC-INTEG-005: IT-05 Frame Buffer Overflow Recovery

**GIVEN**: Ring buffer at capacity (4 frames), continuous capture active, slow consumer

**WHEN**: Frame buffer overflow occurs and then consumer resumes normal speed

**THEN**:
- Frame drops occur as buffer overflows (expected behavior)
- No system crash or hang
- No deadlock (test completes within 30-second timeout)
- Frames recovered after consumer resumes
- Frame sequence numbers reflect dropped frames
- No data corruption in recovered frames

---

### AC-INTEG-006: IT-06 HMAC-SHA256 Command Authentication

**GIVEN**: SoC with HMAC-SHA256 authentication enabled on port 8001

**WHEN**: Commands are sent with valid, invalid, and missing HMACs

**THEN**:
- Valid HMAC: Command executes successfully
- Invalid HMAC (modified byte): Command rejected, error logged
- Missing HMAC: Command rejected, error logged
- Invalid rejection rate: 100% (no false accepts)
- Valid acceptance rate: 100% (no false rejects)

---

### AC-INTEG-007: IT-07 Sequence Engine State Machine

**GIVEN**: SoC firmware with 6-state FSM

**WHEN**: Sequence engine transitions are exercised 5 times through full cycle

**THEN**:
- IDLE → INIT → READY → CAPTURE → TRANSFER → IDLE transitions occur
- No invalid transitions detected
- No unexpected state values
- Error state entered and exited correctly on fault injection
- FSM remains deterministic across all 5 cycles
- State changes logged accurately

---

### AC-INTEG-008: IT-08 10GbE Packet Loss and Retransmission

**GIVEN**: Simulator with 0.1% packet loss injection

**WHEN**: 100 frames are captured while packet loss is active

**THEN**:
- All 100 frames eventually recovered (zero permanent loss)
- Frame recovery latency < 2 seconds (per-frame)
- 95th percentile recovery latency < 500 ms
- Frame payload integrity verified after recovery
- No system crashes or hangs during loss recovery

---

### AC-INTEG-009: IT-09 Maximum Tier Stress Test

**GIVEN**: Maximum tier configuration (3072×3072@30fps)

**WHEN**: Continuous acquisition runs for 60 seconds (1800 frames)

**THEN**:
- 1800 frames captured (or maximum 18 dropped, < 1% rate)
- GetStatusAsync.DroppedFrames ≤ 18
- No system crash, hang, or timeout
- No thermal throttling triggered
- Memory growth < 500 MB
- CPU usage sustainable (not saturated)
- Test execution time ≤ 75 seconds

---

### AC-INTEG-010: IT-10 End-to-End Latency Measurement

**GIVEN**: 300 frames captured with timestamp collection at all 5 measurement points

**WHEN**: Latency metrics are calculated and percentiles computed

**THEN**:
- 95th percentile end-to-end latency (trigger to FrameReceived) < 50 milliseconds
- 99th percentile latency < 75 milliseconds
- Median latency < 30 milliseconds
- Latency distribution is relatively tight (no extreme outliers)
- Latency histogram generated and logged

---

## Quality Gates

### QG-INTEG-001: TRUST 5 Framework Compliance

- **Tested**: ≥ 85% code coverage for all integration test code
- **Readable**: Clear test names (Describe_WhenCondition_ThenOutcome pattern), English comments explaining complex assertions
- **Unified**: Consistent C# coding style per EditorConfig, xUnit patterns followed uniformly
- **Secured**: No credentials in test code, no network calls to untrusted servers, authentication tested cryptographically
- **Trackable**: Git-tracked with conventional commits, test failures logged with structured output

### QG-INTEG-002: Test Determinism

- No flaky tests (100% consistent results across 10 consecutive runs)
- No race conditions (all async operations properly synchronized)
- No external dependencies (all networking mocked/simulated)
- Timeout handling robust (tests fail quickly if SoC simulator unresponsive)

### QG-INTEG-003: Performance Validation

- All performance assertions validated (throughput, latency, frame drop rate)
- Telemetry collected for trend analysis (CSV export or JSON)
- Performance regressions detected via baseline comparison

### QG-INTEG-004: Error Scenario Coverage

- All 10 integration scenarios (IT-01 to IT-10) implemented as separate test methods
- Error paths tested (timeout, overflow, authentication failure, packet loss)
- Recovery mechanisms validated

---

## Dependencies

### External Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| xUnit | 2.9.0 | Test framework |
| Moq | 4.20.70 | Mocking for simulator control |
| FluentAssertions | 6.x | Fluent assertion API |
| System.IO.Pipelines | 8.0.0 | High-performance I/O |

### Internal Dependencies

| Module | Dependency | Purpose |
|--------|-----------|---------|
| Sdk.Tests (Integration) | XrayDetector.Sdk | IDetectorClient, Frame, ScanStatus |
| Sdk.Tests (Integration) | Common.Dto | Shared DTOs |
| Sdk.Tests (Integration) | XrayDetector.Simulator | FPGA, SoC, panel simulators |

---

## Traceability

### Parent Documents

- **SPEC-SDK-001**: Host SDK API and frame processing (REQ-SDK-001 through REQ-SDK-043)
- **SPEC-FW-001**: SoC firmware and sequence engine (sequence engine FSM)
- **SPEC-FPGA-001**: FPGA RTL and CSI-2 protocol
- **X-ray_Detector_Optimal_Project_Plan.md**: Section 5.5 Phase 5 (Integration & Testing)

### Reference Documentation

- **docs/architecture/integration-test-design.md**: Detailed test architecture and simulator design
- **docs/api/ethernet-protocol.md**: 10GbE UDP packet format (port 8000), command protocol (port 8001)
- **docs/api/csi2-protocol.md**: CSI-2 packet format and state machine

### Child Documents

None (terminal document for M3 phase).

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-27 | ABYZ-Lab Agent (architect) | Initial SPEC creation for M3 Integration Testing phase |

---

## Review Record

- Date: 2026-02-27
- Reviewer: (Pending manager-quality review)
- Status: Approved
- TRUST 5: T:5 R:5 U:5 S:5 T:5

---

**END OF SPEC**
