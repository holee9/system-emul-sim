# Integration Test Plan

**Project**: X-ray Detector Panel System
**Document Version**: 1.2.0
**Last Updated**: 2026-02-17
**Status**: Reviewed - Approved

---

## Overview

This document defines IT-01 through IT-10 integration test scenarios for the X-ray Detector Panel System. Tests validate inter-component communication across three interfaces: FPGA-SoC (CSI-2 + SPI), SoC-Host (UDP/TCP), and end-to-end pipeline. Tests are executed using the IntegrationRunner tool against the simulator stack (FpgaSimulator + McuSimulator + HostSimulator).

**Reference**: SPEC-TOOLS-001 REQ-TOOLS-030, SPEC-SIM-001, SPEC-FPGA-001, SPEC-FW-001, SPEC-SDK-001

---

## Test Environment

- **Simulator Stack**: FpgaSimulator + McuSimulator + HostSimulator
- **Integration Runner**: `dotnet run --project tools/IntegrationRunner -- --scenario IT-XX`
- **Log Output**: `logs/integration/IT-XX-YYYYMMDD-HHMMSS.log`
- **Configuration**: `detector_config.yaml` (single source of truth)
- **Development Methodology**: Hybrid per quality.yaml (TDD for new tests, DDD for existing)

---

## Interface Coverage Matrix

| Interface | Protocol | IT Scenarios | Direction |
|-----------|----------|-------------|-----------|
| FPGA -> SoC (data) | CSI-2 MIPI 4-lane D-PHY, RAW16 (0x2E) | IT-01, IT-02, IT-09, IT-10 | Unidirectional |
| SoC -> FPGA (control) | SPI Mode 0, 50 MHz, 16-bit word | IT-01, IT-04, IT-05, IT-06 | Bidirectional |
| SoC -> Host (data) | UDP, port 8000 | IT-02, IT-03, IT-07, IT-09 | Unidirectional |
| Host -> SoC (control) | TCP, port 8001 | IT-04, IT-05, IT-06, IT-08 | Bidirectional |
| End-to-end pipeline | FPGA -> SoC -> Host | IT-01, IT-02, IT-09, IT-10 | Full chain |

---

## Test Scenario Summary

| ID | Title | Tier | Interface Focus | Priority |
|----|-------|------|----------------|----------|
| IT-01 | Single Frame, Minimum Tier | Minimum | End-to-end | Critical |
| IT-02 | 1000-Frame Continuous, Intermediate-A | Intermediate-A | End-to-end, CSI-2 | Critical |
| IT-03 | Out-of-Order UDP Packet Handling | Intermediate-A | SoC-Host | High |
| IT-04 | Error Injection and Recovery | Intermediate-A | FPGA-SoC, SoC-Host | Critical |
| IT-05 | Runtime Configuration Change | Min -> Int-A | SoC-FPGA (SPI) | High |
| IT-06 | Mode Transition (Continuous/Single-Shot/Calibration) | Intermediate-A | SoC-FPGA (SPI) | High |
| IT-07 | Packet Loss and Network Resilience | Intermediate-A | SoC-Host | High |
| IT-08 | Simultaneous Connection Requests | Any | Host-SoC | Medium |
| IT-09 | 10,000-Frame Long-Duration Stability | Intermediate-A | End-to-end | Critical |
| IT-10 | Bandwidth Limit Testing | All tiers | CSI-2, End-to-end | Critical |

---

## Test Scenarios

### IT-01: Single Frame, Minimum Tier

**Purpose**: Verify basic end-to-end pipeline with minimum configuration
**Tier**: Minimum (1024x1024, 14-bit, 15fps)
**Interfaces Tested**: FPGA-SoC (CSI-2 + SPI), SoC-Host (UDP + TCP)

**Preconditions**:
- FpgaSimulator, McuSimulator, HostSimulator all started
- detector_config.yaml set to Minimum tier

**Procedure**:
1. Start FpgaSimulator with Minimum tier config
2. McuSimulator reads SPI DEVICE_ID register (0xF0) -- verify 0xA735
3. McuSimulator reads SPI STATUS register (0x04) -- verify IDLE state
4. Start HostSimulator, connect to TCP port 8001 (control), bind UDP port 8000 (data)
5. Send single-shot scan command via TCP port 8001
6. McuSimulator writes SPI CONTROL register (0x00) bit[0] = 1 (start_scan)
7. Verify CSI-2 frame transmitted: Frame Start -> 1024 line packets -> Frame End
8. Verify frame received at Host within 5 seconds
9. McuSimulator reads SPI STATUS register -- verify return to IDLE

**Pass Criteria**:
- SPI DEVICE_ID read correctly: 0xA735 (per SPEC-FPGA-001 REQ-FPGA-042)
- Frame received with correct dimensions: 1024x1024
- Pixel bit depth: 14-bit (zero-padded to 16-bit in TIFF)
- FrameHeader magic: 0xD7E01234
- CSI-2 data type: 0x2E (RAW16, per MIPI CSI-2 v1.3 Table 10)
- CRC-16 valid for all line packets (per SPEC-FPGA-001 REQ-FPGA-036)
- No error flags set after acquisition (ERROR_FLAGS register 0xA0 = 0x00)
- FSM returns to IDLE after frame completion

**Traces To**: SPEC-FPGA-001 (REQ-FPGA-010, 031, 042), SPEC-FW-001, SPEC-SDK-001

---

### IT-02: 1000-Frame Continuous, Intermediate-A Tier

**Purpose**: Verify sustained continuous acquisition at development baseline
**Tier**: Intermediate-A (2048x2048, 16-bit, 15fps)
**Interfaces Tested**: CSI-2 (sustained throughput), SoC-Host (UDP streaming)

**Preconditions**:
- detector_config.yaml set to Intermediate-A tier
- All simulators configured for 400 Mbps/lane D-PHY speed

**Procedure**:
1. Configure for Intermediate-A tier via SPI timing registers (0x20-0x3F)
2. Set continuous scan mode via SPI CONTROL register bits[3:2] = 2'b01
3. Start continuous scan (CONTROL bit[0] = 1)
4. Capture 1000 frames, monitoring frame counter, drop counter, error flags
5. After 1000 frames, send stop_scan (CONTROL bit[1] = 1)
6. Verify graceful stop within 1 line time (per SPEC-FPGA-001 REQ-FPGA-016)

**Pass Criteria**:
- 1000 frames received at Host
- Frame drop rate < 0.01% (max 0.1 drops per 1000 frames)
- Frame counter increments monotonically (0 to 999, no gaps)
- 32-bit frame counter read via SPI DATA_STATUS register (0x90) matches Host count
- ERROR_FLAGS register (0xA0) = 0x00 throughout acquisition
- Measured throughput >= 1.01 Gbps (Intermediate-A requirement)
- Average frame interval: 66.7 ms +/- 3.3 ms (15 fps +/- 5%)
- Graceful stop completes: FSM returns to IDLE, no partial frame

**Traces To**: SPEC-FPGA-001 (REQ-FPGA-015, 016), SPEC-FW-001, SPEC-SDK-001

---

### IT-03: Out-of-Order UDP Packet Handling

**Purpose**: Verify Host SDK frame reassembly with packet reordering
**Tier**: Intermediate-A (2048x2048, 16-bit, 15fps)
**Interfaces Tested**: SoC-Host (UDP)

**Preconditions**:
- Network simulator configured with 5% packet reordering

**Procedure**:
1. Configure HostSimulator to simulate 5% packet reordering on UDP data channel
2. Acquire 100 frames in continuous mode
3. Verify reassembly correctness by comparing pixel data against expected values
4. Measure frame delivery latency with and without reordering

**Pass Criteria**:
- All 100 frames correctly reassembled (bit-accurate pixel data)
- Reordering tolerance: up to 8 packets within a single frame
- Frame delivery latency increase < 2x compared to IT-02 baseline
- No frames delivered with corrupt data (prefer discard over corruption)
- Host SDK logs reordering events without crash or hang

**Traces To**: SPEC-SDK-001

---

### IT-04: Error Injection and Recovery

**Purpose**: Verify error detection, safe state, and recovery pipeline across all interfaces
**Interfaces Tested**: FPGA-SoC (SPI error flags), SoC-Host (error reporting)

**Sub-test A -- TIMEOUT Error (Fatal)**:

**Procedure**:
1. Start continuous acquisition at Intermediate-A tier
2. After 10 frames, inject TIMEOUT error in FpgaSimulator
3. Verify FpgaSimulator enters ERROR state within 10 clock cycles
4. Verify McuSimulator detects error via SPI STATUS register (0x04) polling
5. Verify McuSimulator reads ERROR_FLAGS (0xA0) -- bit[0] = 1 (TIMEOUT, code 0x01)
6. Verify McuSimulator attempts error_clear (write CONTROL bit error_clear)
7. After 3 failed retries, verify escalation to Host

**Pass Criteria**:
- FpgaSimulator safe state: gate_on = 0, CSI-2 TX in LP mode (per SPEC-FPGA-001 REQ-FPGA-051/052)
- Error detected within 200ms of injection
- Exactly 3 retry attempts before escalation (per SPEC-FW-001 REQ-FW-032)
- Host receives structured error: `{type: "TIMEOUT", code: 0x01}`
- After escalation, system in IDLE state awaiting new StartScan command

**Sub-test B -- OVERFLOW Error (Fatal)**:

**Procedure**:
1. Inject OVERFLOW error (line buffer write catches read in same bank)
2. Verify ERROR_FLAGS bit[1] = 1 (code 0x02)
3. McuSimulator sends error_clear command
4. Verify scan resumes after recovery

**Pass Criteria**:
- OVERFLOW flag (bit[1] of ERROR_FLAGS 0xA0) detected and cleared
- FpgaSimulator enters safe state (gate_on = 0, write disabled)
- After error_clear, FSM returns to IDLE (per SPEC-FPGA-001 REQ-FPGA-053)
- Scan resumes within 500ms of error_clear
- Subsequent frames unaffected (data integrity maintained)

**Sub-test C -- CRC Error (Non-Fatal)**:

**Procedure**:
1. Inject CRC mismatch in FpgaSimulator CSI-2 packet
2. Verify ERROR_FLAGS bit[2] = 1 (code 0x04)
3. Verify FSM does NOT enter ERROR state (CRC is non-fatal)
4. Verify Host SDK detects and reports CRC error for affected frame

**Pass Criteria**:
- CRC error flag set in ERROR_FLAGS register
- FSM continues operation (no safe state for non-fatal errors)
- Host SDK marks affected frame as corrupted
- Subsequent frames unaffected

**Sub-test D -- Watchdog Timeout**:

**Procedure**:
1. Start continuous acquisition
2. Pause McuSimulator SPI polling for > 100 ms (simulating SoC hang)
3. Verify FpgaSimulator watchdog triggers

**Pass Criteria**:
- Watchdog error (ERROR_FLAGS bit[7], code 0x80) asserts after 100 ms SPI silence
- FpgaSimulator enters safe state (gate_on = 0, per SPEC-FPGA-001 REQ-FPGA-054)
- After McuSimulator resumes and sends error_clear, system recovers

**Sub-test E -- Single-Retry Recovery Success**:

**Procedure**:
1. Inject TIMEOUT error in FpgaSimulator
2. FpgaSimulator auto-recovers after first error_clear attempt

**Pass Criteria**:
- McuSimulator detects error, sends error_clear command
- FpgaSimulator recovers, scan resumes from IDLE
- Host receives 1 temporary error event, then normal frames
- Total recovery time < 500ms

**Sub-test F -- Three-Retry Exhaustion**:

**Procedure**:
1. Inject persistent OVERFLOW error that clears then immediately re-triggers
2. McuSimulator attempts error_clear 3 times

**Pass Criteria**:
- Exactly 3 retry attempts observed (no more, no less)
- After 3rd failure, error reported to Host with code 0x02
- FPGA enters safe state (gate_on = 0)
- System enters IDLE state awaiting new StartScan command

**Sub-test G -- Post-Recovery Normal Operation**:

**Procedure**:
1. Trigger and recover from Sub-test E (TIMEOUT recovery)
2. After recovery, issue new StartAcquisitionAsync
3. Acquire 100 frames normally

**Pass Criteria**:
- New acquisition starts successfully after recovery
- 100 frames received with no errors
- Frame counter continues from previous value (no reset)
- ERROR_FLAGS = 0x00 throughout post-recovery acquisition

**Traces To**: SPEC-FPGA-001 (REQ-FPGA-050 through 054), SPEC-FW-001 (REQ-FW-032)

---

### IT-05: Runtime Configuration Change

**Purpose**: Verify tier change via SPI register update takes effect without system restart
**Interfaces Tested**: Host-SoC (TCP command), SoC-FPGA (SPI register writes)

**Preconditions**:
- System running at Minimum tier with active acquisition

**Procedure**:
1. Start with Minimum tier (1024x1024, 14-bit, 15fps)
2. Acquire 10 frames (verify baseline dimensions and bit depth)
3. Stop acquisition (CONTROL bit[1] = 1, stop_scan)
4. Host sends ConfigureAsync command with Intermediate-A parameters via TCP
5. McuSimulator writes SPI timing registers (0x20-0x3F) and panel config registers (0x40-0x5F)
6. Verify SPI readback matches written values
7. Start new acquisition (CONTROL bit[0] = 1, start_scan)
8. Acquire 10 more frames

**Pass Criteria**:
- Configuration accepted without error (SPI write-read verification)
- Frame dimensions change to 2048x2048 after reconfiguration
- Bit depth changes to 16-bit
- No frames lost during transition (clean stop -> reconfigure -> clean start)
- All SPI timing parameters (gate_on_us, roic_settle_us) update correctly
- CSI-2 line packet word count updates (2048 pixels -> 4096 bytes per line)

**Traces To**: SPEC-FPGA-001 (REQ-FPGA-042, 012, 014), SPEC-FW-001

---

### IT-06: Mode Transition (Continuous / Single-Shot / Calibration)

**Purpose**: Verify all three operating modes and transitions between them
**Interfaces Tested**: SoC-FPGA (SPI CONTROL register bits[3:2])

**Sub-test A -- Continuous to Single-Shot Transition**:

**Procedure**:
1. Start continuous scan mode (CONTROL bits[3:2] = 2'b01, 15fps)
2. After 50 frames, send stop_scan (CONTROL bit[1] = 1)
3. Switch to single-shot mode (CONTROL bits[3:2] = 2'b00)
4. Trigger 10 individual single-shot captures (CONTROL bit[0] = 1 for each)

**Pass Criteria**:
- Continuous scan stops cleanly within 1 line time after stop_scan
- Mode switch via SPI accepted (CONTROL readback confirms new mode)
- Single-shot mode responds to individual trigger commands
- Each single-shot frame received within 200ms of trigger
- No spurious frames between single-shot triggers
- Frame counter increments correctly across mode switch

**Sub-test B -- Calibration Mode (Dark Frame Acquisition)**:

**Procedure**:
1. Switch to calibration mode (CONTROL bits[3:2] = 2'b10)
2. Trigger single calibration frame
3. Verify gate_on is NOT asserted during INTEGRATE state (dark frame)
4. Verify frame data represents dark noise (no X-ray exposure)

**Pass Criteria**:
- Calibration mode accepted via SPI (per SPEC-FPGA-001 REQ-FPGA-014)
- gate_on output remains LOW during entire INTEGRATE phase
- Frame received at Host with correct dimensions
- Pixel values represent sensor dark current (near-zero for simulated data)
- FSM transitions: IDLE -> INTEGRATE (gate OFF) -> READOUT -> LINE_DONE -> ... -> FRAME_DONE -> IDLE

**Sub-test C -- Mode Transition Sequence**:

**Procedure**:
1. Single-shot -> Continuous -> Calibration -> Single-shot
2. Acquire 5 frames in each mode
3. Verify correct behavior at each transition

**Pass Criteria**:
- All mode transitions succeed without error
- Each mode produces correct frame behavior
- No error flags set during any transition
- SPI CONTROL register readback correct at each step

**Traces To**: SPEC-FPGA-001 (REQ-FPGA-014, 015, 016)

---

### IT-07: Packet Loss and Network Resilience

**Purpose**: Verify Host SDK handles network impairments gracefully
**Interfaces Tested**: SoC-Host (UDP data channel)

**Sub-test A -- 5% Random Packet Loss**:

**Procedure**:
1. Configure network simulation with 5% random UDP packet loss
2. Acquire 1000 frames in continuous mode at Intermediate-A tier
3. Monitor incomplete frame count and Host SDK error reporting

**Pass Criteria**:
- Frames with > 20% packet loss are discarded (not delivered corrupt)
- Frame counter gap detection works correctly
- Host SDK reports frame loss via callback (FrameLost event) without crash
- Recovery: next frame acquired normally after loss event
- No memory leak from incomplete frame buffers

**Sub-test B -- Network Latency Spike**:

**Procedure**:
1. Inject 200ms latency spike on UDP channel after 500 frames
2. Continue acquisition for 500 more frames

**Pass Criteria**:
- Frames during latency spike may be delayed but not lost
- Host SDK receive buffer accommodates delay (no buffer overflow)
- Post-spike frames arrive at normal latency
- Frame ordering preserved (sequence numbers monotonic)

**Traces To**: SPEC-SDK-001

---

### IT-08: Simultaneous Connection Requests

**Purpose**: Verify single-connection enforcement on control channel
**Interfaces Tested**: Host-SoC (TCP control channel)

**Procedure**:
1. Connect Host A to detector TCP control port 8001
2. Verify Host A can issue commands (e.g., GetStatus)
3. While Host A connected, attempt to connect Host B to TCP port 8001
4. Verify Host B receives rejection
5. Disconnect Host A
6. Verify Host B can now connect successfully

**Pass Criteria**:
- Host B receives connection rejected error (ECONNREFUSED or equivalent)
- Host A connection and ongoing acquisition unaffected by Host B attempt
- After Host A disconnects, Host B connects successfully within 1 second
- No data corruption or state inconsistency from rejected connection attempt
- If acquisition was in progress during Host A disconnect, system stops cleanly (no orphan scan)

**Traces To**: SPEC-SDK-001, SPEC-FW-001

---

### IT-09: 10,000-Frame Long-Duration Stability Test

**Purpose**: Verify system stability over extended operation (simulated)
**Tier**: Intermediate-A (2048x2048, 16-bit, 15fps)
**Duration**: ~667 seconds (11 minutes simulated)

**Preconditions**:
- All simulators configured for Intermediate-A tier
- System monitoring enabled (memory, CPU, error flags)

**Procedure**:
1. Start continuous acquisition for 10,000 frames
2. Monitor every 1000 frames:
   - Frame drop rate
   - Process RSS memory usage (all 3 simulators)
   - ERROR_FLAGS register via SPI
   - CPU usage on McuSimulator
3. After 10,000 frames, stop acquisition and verify clean shutdown
4. Validate final frame counter matches expected count

**Pass Criteria**:
- 10,000 frames received at Host (frame drop rate < 0.01%, max 1 drop)
- Frame counter: 0 to 9999, monotonic, no gaps
- Memory: process RSS growth < 50MB over entire test duration (no memory leak)
- ERROR_FLAGS: register 0xA0 = 0x00 for >= 99.9% of duration
- CPU usage: < 80% average on McuSimulator
- CSI-2 CRC errors: 0 across all 10,000 frames
- Clean shutdown: FSM returns to IDLE, all buffers flushed, Host receives all queued frames
- SPI watchdog: no spurious watchdog triggers during normal operation

**Traces To**: SPEC-FPGA-001 (REQ-FPGA-015, 054), SPEC-FW-001, SPEC-SDK-001

---

### IT-10: Bandwidth Limit Testing

**Purpose**: Verify performance at each tier boundary, validating CSI-2 throughput
**Interfaces Tested**: FPGA-SoC (CSI-2 throughput), End-to-end pipeline

**Sub-test IT-10A: Minimum Tier**:

**Configuration**: 1024x1024, 14-bit, 15fps, 400 Mbps/lane
**Expected Throughput**: ~0.22 Gbps (raw pixel data)

**Procedure**:
1. Configure Minimum tier via SPI registers
2. Acquire 100 frames in continuous mode
3. Measure actual throughput and frame timing

**Pass Criteria**:
- Sustained throughput >= 95% of theoretical (>= 0.21 Gbps)
- CSI-2 aggregate bandwidth well within 400M limit (1.6 Gbps)
- Zero frames dropped due to bandwidth
- FPS measured: 15 fps +/- 5% (frame interval 66.7 ms +/- 3.3 ms)

**Sub-test IT-10B: Intermediate-A Tier (Development Baseline)**:

**Configuration**: 2048x2048, 16-bit, 15fps, 400 Mbps/lane
**Expected Throughput**: ~1.01 Gbps (raw pixel data)

**Procedure**:
1. Configure Intermediate-A tier via SPI registers
2. Acquire 100 frames in continuous mode
3. Measure actual throughput and frame timing
4. Verify CSI-2 utilization ratio (pixel data / aggregate bandwidth)

**Pass Criteria**:
- Sustained throughput >= 95% of theoretical (>= 0.96 Gbps)
- CSI-2 aggregate bandwidth: 1.6 Gbps (400 Mbps x 4 lanes), utilization ~63%
- Zero frames dropped due to bandwidth
- FPS measured: 15 fps +/- 5%
- This sub-test MUST pass (development baseline)

**Sub-test IT-10C: Target Tier (Final Goal, Conditional)**:

**Configuration**: 3072x3072, 16-bit, 15fps, 800 Mbps/lane
**Expected Throughput**: ~2.26 Gbps (raw pixel data)

**Procedure**:
1. Configure Target tier via SPI registers
2. Configure CSI-2 lane speed to 800 Mbps/lane (register 0x88)
3. Acquire 100 frames in continuous mode
4. Measure actual throughput and frame timing

**Pass Criteria**:
- Sustained throughput >= 95% of theoretical (>= 2.15 Gbps)
- CSI-2 aggregate bandwidth: 3.2 Gbps (800 Mbps x 4 lanes), utilization ~71%
- Zero frames dropped due to bandwidth
- FPS measured: 15 fps +/- 5%
- 800M bandwidth margin: 29% (2.26 / 3.2 = 71% utilization)

**Conditional Execution**:
- IF 800 Mbps/lane debugging is NOT complete: SKIP IT-10C, mark as CONDITIONAL PASS
- IT-10B (Intermediate-A at 400M) MUST pass regardless
- IT-10C result does not block M3 gate if IT-10A and IT-10B pass

**Traces To**: SPEC-FPGA-001 (REQ-FPGA-034), SPEC-ARCH-001 (REQ-ARCH-006, 007)

---

## Bit-Exact Data Integrity Verification

All integration tests that verify pixel data correctness use the following bit-exact verification procedure. This ensures zero data corruption across the full simulator pipeline: PanelSim -> FpgaSim -> McuSim -> HostSim.

### Verification Algorithm

```
Input:  Expected pixel array (generated from known test pattern)
        Received pixel array (captured at Host after full pipeline traversal)

Procedure:
  1. Generate expected frame using counter pattern:
     expected[row][col] = (row * width + col) % 65536

  2. Capture frame at Host endpoint after pipeline traversal:
     PanelSim generates pixels -> FpgaSim packages as CSI-2 ->
     McuSim receives via CSI-2 RX, fragments to UDP ->
     HostSim receives UDP, reassembles frame

  3. Compare dimensions:
     ASSERT received.Width  == expected.Width
     ASSERT received.Height == expected.Height

  4. Compare every pixel value (bit-exact):
     for row in 0..(height-1):
       for col in 0..(width-1):
         ASSERT received[row][col] == expected[row][col]
         If mismatch: record (row, col, expected_value, received_value)

  5. Report:
     - Total pixels compared: width * height
     - Mismatched pixels: count
     - First mismatch location: (row, col)
     - Pass criterion: zero mismatches (0 bit errors)
```

### CRC Verification Chain

Data integrity is validated at multiple points in the pipeline:

| Checkpoint | Layer | Method | Reference |
|-----------|-------|--------|-----------|
| CSI-2 line CRC | FPGA -> SoC | CRC-16/CCITT per line payload | SPEC-FPGA-001 REQ-FPGA-036 |
| UDP header CRC | SoC -> Host | CRC-16/CCITT per packet header | SPEC-FW-001 REQ-FW-042 |
| End-to-end pixel compare | Full pipeline | Bit-exact pixel comparison | This section |

### IntegrationRunner Verification Commands

```bash
# Run bit-exact verification with counter pattern
dotnet run --project tools/IntegrationRunner -- --scenario IT-01 --verify-pixels

# Run with checksum dump (prints CRC at each pipeline stage)
dotnet run --project tools/IntegrationRunner -- --scenario IT-02 --verify-pixels --dump-crc

# Compare captured frame against golden reference file
dotnet run --project tools/IntegrationRunner -- --verify-golden \
  --captured output/frame_0000.raw \
  --golden golden/counter_1024x1024_14bit.raw

# Generate golden reference for a given tier
dotnet run --project tools/IntegrationRunner -- --generate-golden \
  --tier minimum --output golden/counter_1024x1024_14bit.raw
```

### Test Pattern Definitions

| Pattern | Formula | Verification Use |
|---------|---------|-----------------|
| Counter | `pixel[r][c] = (r * width + c) % 65536` | Full pixel ordering and value verification |
| Checkerboard | `pixel[r][c] = ((r+c) % 2 == 0) ? 0xFFFF : 0x0000` | Bit-flip detection (alternating all-ones/all-zeros) |
| Constant | `pixel[r][c] = 0x8000` (mid-scale) | Stuck-bit detection |

### Which Tests Use Bit-Exact Verification

| IT Scenario | Bit-Exact Verify | Pattern | Notes |
|-------------|-----------------|---------|-------|
| IT-01 | Yes | Counter | Single frame, full pixel compare |
| IT-02 | First + last frame | Counter | Spot-check at frame 0 and frame 999 |
| IT-03 | Yes | Counter | Verifies reassembly correctness after reordering |
| IT-04 | Sub-test G only | Counter | Post-recovery frames verified |
| IT-05 | Yes (both tiers) | Counter | Before and after reconfiguration |
| IT-06 | Yes | Counter | Each mode produces correct frame data |
| IT-07 | Non-dropped frames | Counter | Frames that arrive must be correct |
| IT-08 | No | N/A | Connection management only |
| IT-09 | Spot-check every 1000th | Counter | Frames 0, 999, 1999, ..., 9999 |
| IT-10 | First 10 frames per tier | Counter | Bandwidth test focuses on throughput, but verifies correctness |

---

## Test Execution

### IntegrationRunner CLI Reference

```bash
# Run single test
dotnet run --project tools/IntegrationRunner -- --scenario IT-01

# Run all tests (sequential, recommended execution order)
dotnet run --project tools/IntegrationRunner -- --scenario ALL

# Run M3 gate tests only (IT-01 through IT-06)
dotnet run --project tools/IntegrationRunner -- --scenario IT-01 IT-02 IT-03 IT-04 IT-05 IT-06

# Run M4 gate tests only (IT-07 through IT-10)
dotnet run --project tools/IntegrationRunner -- --scenario IT-07 IT-08 IT-09 IT-10

# Run with custom config
dotnet run --project tools/IntegrationRunner -- --scenario IT-02 --config custom.yaml

# Run with verbose output (includes per-packet logging)
dotnet run --project tools/IntegrationRunner -- --scenario IT-04 --verbose

# Run bandwidth tests only
dotnet run --project tools/IntegrationRunner -- --scenario IT-10A IT-10B IT-10C

# Run error injection suite
dotnet run --project tools/IntegrationRunner -- --scenario IT-04 IT-07 --verbose

# Run with JUnit XML output (for CI/CD integration)
dotnet run --project tools/IntegrationRunner -- --scenario ALL --output-format junit --output-file results/integration.xml

# Run with timeout override (useful for IT-09 long-duration test)
dotnet run --project tools/IntegrationRunner -- --scenario IT-09 --timeout 900
```

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `DETECTOR_CONFIG` | `detector_config.yaml` | Path to configuration file |
| `INTEGRATION_LOG_DIR` | `logs/integration/` | Log output directory |
| `INTEGRATION_VERBOSE` | `false` | Enable verbose packet-level logging |
| `INTEGRATION_TIMEOUT_SEC` | `300` | Global test timeout in seconds |

---

## Test Decision Tree

```
New hardware setup?
  -> Run IT-01 first (single frame baseline)
  -> IT-01 fails? -> Check: SPI connectivity, CSI-2 IP config, detector_config.yaml
  -> IT-01 passes? -> Run IT-02 (1000 frames, verifies continuous operation)

Performance validation?
  -> Run IT-10A, IT-10B (mandatory), IT-10C (conditional on 800M)
  -> IT-10B fails? -> Check: CSI-2 lane speed, frame timing, buffer sizes

Network issues suspected?
  -> Run IT-03 (packet reordering) and IT-07 (packet loss)
  -> Check: UDP buffers (sysctl net.core.rmem_max), MTU (ip link show eth1)

Error recovery validation?
  -> Run IT-04 (all sub-tests A through G)
  -> Check: SPI polling interval, error_clear logic, retry count

Mode switching issues?
  -> Run IT-06 (mode transitions including calibration)
  -> Check: CONTROL register bits[3:2], FSM state after mode change

Long-term stability?
  -> Run IT-09 (10,000 frames, ~11 minutes)
  -> Check: Memory growth, frame drops, CPU usage
```

---

## Test Dependencies and Execution Order

```
IT-01 (Single Frame) ──────────────┐
                                    ├──> IT-02 (1000 Frames) ──> IT-09 (10,000 Frames)
IT-05 (Config Change) ─────────────┘                              │
                                                                   ▼
IT-04 (Error Injection) ─────────> IT-06 (Mode Transitions)    IT-10 (Bandwidth)
                                                                   │
IT-03 (UDP Reorder) ──> IT-07 (Packet Loss)                       ▼
                                                               IT-10C (Target, conditional)
IT-08 (Simultaneous Connections) -- independent, run anytime
```

**Recommended Execution Order**: IT-01 -> IT-02 -> IT-05 -> IT-06 -> IT-04 -> IT-03 -> IT-07 -> IT-08 -> IT-09 -> IT-10

---

## Traceability Matrix

| IT Scenario | SPEC-FPGA-001 | SPEC-FW-001 | SPEC-SDK-001 | SPEC-ARCH-001 |
|-------------|--------------|-------------|-------------|---------------|
| IT-01 | REQ-FPGA-010, 031, 036, 042 | REQ-FW-010 | REQ-SDK-010 | REQ-ARCH-001 |
| IT-02 | REQ-FPGA-015, 016, 034 | REQ-FW-020 | REQ-SDK-020 | - |
| IT-03 | - | - | REQ-SDK-030 | - |
| IT-04 | REQ-FPGA-050-054 | REQ-FW-032 | REQ-SDK-040 | - |
| IT-05 | REQ-FPGA-012, 014, 042 | REQ-FW-025 | REQ-SDK-025 | - |
| IT-06 | REQ-FPGA-014, 015, 016 | REQ-FW-026 | REQ-SDK-026 | - |
| IT-07 | - | - | REQ-SDK-030 | - |
| IT-08 | - | REQ-FW-035 | REQ-SDK-035 | - |
| IT-09 | REQ-FPGA-015, 054 | REQ-FW-020 | REQ-SDK-020 | - |
| IT-10 | REQ-FPGA-034 | REQ-FW-020 | REQ-SDK-020 | REQ-ARCH-006, 007 |

---

## Pass/Fail Summary Template

| ID | Sub-test | Result | Notes |
|----|----------|--------|-------|
| IT-01 | - | PENDING | |
| IT-02 | - | PENDING | |
| IT-03 | - | PENDING | |
| IT-04 | A (TIMEOUT) | PENDING | |
| IT-04 | B (OVERFLOW) | PENDING | |
| IT-04 | C (CRC) | PENDING | |
| IT-04 | D (Watchdog) | PENDING | |
| IT-04 | E (Single Retry) | PENDING | |
| IT-04 | F (3-Retry Exhaust) | PENDING | |
| IT-04 | G (Post-Recovery) | PENDING | |
| IT-05 | - | PENDING | |
| IT-06 | A (Cont->Single) | PENDING | |
| IT-06 | B (Calibration) | PENDING | |
| IT-06 | C (Mode Sequence) | PENDING | |
| IT-07 | A (Packet Loss) | PENDING | |
| IT-07 | B (Latency Spike) | PENDING | |
| IT-08 | - | PENDING | |
| IT-09 | - | PENDING | |
| IT-10 | A (Minimum) | PENDING | |
| IT-10 | B (Int-A) | PENDING | Must pass |
| IT-10 | C (Target) | PENDING | Conditional on 800M |

**Gate Criteria**:

**M3 Gate (W14)**: Basic pipeline and data integrity
- IT-01 through IT-06: ALL PASS required
- Validates: end-to-end pipeline, continuous operation, error handling, configuration, mode transitions

**M4 Gate (W18)**: Advanced resilience, performance, stability
- IT-07 through IT-10: ALL PASS required (except IT-10C conditional)
- IT-10A and IT-10B: PASS required
- IT-10C: CONDITIONAL PASS acceptable (depends on 800M debugging status)

---

## Version

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | ABYZ-Lab | Initial integration test plan |
| 1.1.0 | 2026-02-17 | spec-fpga (doc-approval-sprint) | Added interface coverage matrix, test summary table, traceability matrix, pass/fail template, execution order. Enhanced IT-01 with SPI register details. Enhanced IT-04 with sub-tests C (CRC non-fatal), D (Watchdog), restructured E/F/G from IT-07. Enhanced IT-06 with calibration mode (sub-test B) and mode sequence (sub-test C). Enhanced IT-07 with latency spike sub-test. Enhanced IT-08 with disconnect-during-acquisition. Enhanced IT-09 with CRC monitoring and clean shutdown. Enhanced IT-10 with per-sub-test detail and conditional execution rules. Added test decision tree improvements and dependency graph. |
| 1.2.0 | 2026-02-17 | manager-spec (doc-completion) | Added Bit-Exact Data Integrity Verification section with algorithm, CRC verification chain, test pattern definitions, per-scenario verification matrix. Enhanced IntegrationRunner CLI reference with M3/M4 gate commands, JUnit XML output, environment variables, golden reference generation. |
