# Integration Test Plan

## Overview

This document defines IT-01 through IT-10 integration test scenarios for the X-ray Detector Panel System. Tests are executed using the IntegrationRunner tool.

**Reference**: SPEC-TOOLS-001 REQ-TOOLS-030, SPEC-SIM-001

## Test Environment

- **Simulator Stack**: FpgaSimulator + McuSimulator + HostSimulator
- **Integration Runner**: `dotnet run --project tools/IntegrationRunner -- --scenario IT-XX`
- **Log Output**: `logs/integration/IT-XX-YYYYMMDD-HHMMSS.log`

## Test Scenarios

### IT-01: Single Frame, Minimum Tier

**Purpose**: Verify basic end-to-end pipeline with minimum configuration
**Tier**: Minimum (1024x1024, 14-bit, 15fps)

**Procedure**:
1. Start FpgaSimulator with Minimum tier config
2. Start McuSimulator, verify SPI DEVICE_ID read (0xD7E00001)
3. Start HostSimulator, connect to UDP port 8000
4. Send single-shot scan command via port 8001
5. Verify frame received within 5 seconds

**Pass Criteria**:
- DEVICE_ID read correctly: 0xD7E00001
- Frame received with correct dimensions: 1024x1024
- Pixel bit depth: 14-bit (zero-padded to 16-bit in TIFF)
- FrameHeader magic: 0xD7E01234
- CSI-2 data type: 0x2E (RAW16)
- No error flags set after acquisition

---

### IT-02: 1000-Frame Continuous, Intermediate-A Tier

**Purpose**: Verify sustained continuous acquisition
**Tier**: Intermediate-A (2048x2048, 16-bit, 15fps)

**Procedure**:
1. Configure for Intermediate-A tier
2. Start continuous scan for 1000 frames
3. Monitor frame counter, drop counter, error flags

**Pass Criteria**:
- 1000 frames received
- Frame drop rate < 0.01% (max 0.1 drops per 1000 frames)
- Frame counter increments monotonically
- Error flag register 0x80 = 0x00 throughout

---

### IT-03: Out-of-Order UDP Packet Handling

**Purpose**: Verify Host SDK frame reassembly with packet reordering
**Tier**: Intermediate-A (2048x2048, 16-bit, 15fps)

**Procedure**:
1. Configure HostSimulator to simulate 5% packet reordering
2. Acquire 100 frames
3. Verify reassembly correctness

**Pass Criteria**:
- All 100 frames correctly reassembled (bit-accurate)
- Reordering tolerance up to 8 packets within a frame
- Frame delivery latency increase < 2x compared to IT-02

---

### IT-04: Error Injection and Recovery

**Purpose**: Verify error detection and recovery pipeline
**Error Types**: TIMEOUT (bit 0), CRC_ERROR (bit 2), OVERFLOW (bit 1)

**Sub-test A - TIMEOUT Error**:
1. Inject TIMEOUT error in FpgaSimulator
2. Verify McuSimulator detects via STATUS polling (0x20)
3. Verify Sequence Engine transitions to ERROR state
4. Verify 3 retry attempts
5. Verify Host receives error code 0x01

**Pass Criteria**:
- Error detected within 200ms of injection
- Exactly 3 retry attempts before escalation
- Host receives structured error: `{type: "TIMEOUT", code: 0x01}`
- FPGA safe state: gate_on = 0

**Sub-test B - OVERFLOW Error**:
1. Inject OVERFLOW error (line buffer collision)
2. Verify McuSimulator clears flag (write 0x80 to ERROR_FLAGS register 0x80)
3. Verify scan resumes after recovery

**Pass Criteria**:
- OVERFLOW flag (bit 1 of 0x80) detected and cleared
- Scan resumes within 500ms
- Subsequent frames unaffected

---

### IT-05: Runtime Configuration Change

**Purpose**: Verify detector_config.yaml changes take effect without restart
**Tier**: Change from Minimum to Intermediate-A during operation

**Procedure**:
1. Start with Minimum tier
2. Acquire 10 frames (verify baseline)
3. Send ConfigureAsync command with Intermediate-A parameters
4. Acquire 10 more frames

**Pass Criteria**:
- Configuration accepted without error
- Frame dimensions change to 2048x2048 after config
- No frames lost during transition
- All timing parameters update correctly

---

### IT-06: Continuous to Single-Shot Mode Transition

**Purpose**: Verify mode switching without system restart

**Procedure**:
1. Start continuous scan (15fps)
2. After 50 frames, switch to single-shot mode
3. Trigger 10 individual single-shot captures

**Pass Criteria**:
- Continuous scan stops cleanly after mode switch
- Single-shot mode responds to individual trigger commands
- Each single-shot frame received within 200ms of trigger
- No spurious frames during single-shot mode

---

### IT-07: Packet Loss Handling (5%)

**Purpose**: Verify Host SDK handles packet loss gracefully
**Simulated Loss**: 5% of UDP packets dropped randomly

**Procedure**:
1. Configure network simulation with 5% packet loss
2. Acquire 1000 frames in continuous mode
3. Monitor incomplete frame count

**Pass Criteria**:
- Frames with > 20% packet loss are discarded (not delivered corrupt)
- Frame counter gap detection works correctly
- Host SDK reports frame loss without crash
- Recovery: next frame acquired normally

#### IT-07e: Single-Retry Recovery Success

**Purpose**: Verify error recovery succeeds on first retry

**Procedure**:
1. Inject TIMEOUT error in FpgaSimulator
2. FpgaSimulator auto-recovers after first clear attempt

**Pass Criteria**:
- McuSimulator detects error, sends error_clear command
- FpgaSimulator recovers, scan resumes
- Host receives 1 temporary error event, then normal frames
- Total recovery time < 500ms

#### IT-07f: Three-Retry Exhaustion

**Purpose**: Verify escalation after 3 failed recovery attempts (REQ-FW-032)

**Procedure**:
1. Inject persistent OVERFLOW error that clears then immediately re-triggers
2. McuSimulator attempts error_clear 3 times

**Pass Criteria**:
- Exactly 3 retry attempts observed (no more, no less)
- After 3rd failure, error reported to Host with code 0x02
- FPGA enters safe state (gate_on = 0)
- System enters IDLE state awaiting new StartScan command

#### IT-07g: Post-Recovery Normal Operation

**Purpose**: Verify normal operation resumes after error recovery

**Procedure**:
1. Trigger and recover from IT-07a (TIMEOUT error)
2. After recovery, issue new StartAcquisitionAsync
3. Acquire 100 frames normally

**Pass Criteria**:
- New acquisition starts successfully after recovery
- 100 frames received with no errors
- Frame counter continues from where it left off (no reset)

---

### IT-08: Simultaneous Connection Requests

**Purpose**: Verify only one active connection at a time

**Procedure**:
1. Connect Host A to detector
2. While Host A connected, attempt to connect Host B

**Pass Criteria**:
- Host B receives connection rejected error (ECONNREFUSED or equivalent)
- Host A connection unaffected
- After Host A disconnects, Host B can connect successfully

---

### IT-09: 10,000-Frame Long-Duration Test

**Purpose**: Verify system stability over extended operation
**Tier**: Intermediate-A (2048x2048, 16-bit, 15fps)
**Duration**: ~667 seconds (11 minutes)

**Procedure**:
1. Start continuous acquisition for 10,000 frames
2. Monitor: frame drop rate, memory usage, error flags, CPU usage

**Pass Criteria**:
- Frame drop rate < 0.01% (max 1 drop per 10,000 frames)
- Memory: process RSS growth < 50MB over entire test duration
- Error flags: 0x80 register = 0x00 for >= 99.9% of duration
- CPU usage: < 80% average on SoC

---

### IT-10: Bandwidth Limit Testing

**Purpose**: Verify performance at each tier boundary

**Sub-tests**:
1. **IT-10A**: Minimum tier (1024x1024@15fps, ~0.22 Gbps) -- verify 400M operation
2. **IT-10B**: Intermediate-A (2048x2048@15fps, ~1.01 Gbps) -- verify 400M ceiling
3. **IT-10C**: Target tier (3072x3072@15fps, ~2.26 Gbps) -- verify 800M operation (if enabled)

**Pass Criteria per Sub-test**:
- Sustained throughput >= 95% of theoretical maximum
- CSI-2 aggregate bandwidth: 2.0–3.2 Gbps (within D-PHY 800M maximum: 4 × 800 Mbps = 3.2 Gbps)
- No frames dropped due to bandwidth saturation
- FPS measured: within 5% of target FPS
- If 800M debugging not yet complete: SKIP IT-10C, mark as CONDITIONAL PASS
- Alternative path: IT-10B (Intermediate-A at 400M) must pass

## Test Execution

```bash
# Run single test
dotnet run --project tools/IntegrationRunner -- --scenario IT-01

# Run all tests
dotnet run --project tools/IntegrationRunner -- --scenario ALL

# Run with custom config
dotnet run --project tools/IntegrationRunner -- --scenario IT-02 --config custom.yaml

# Run with verbose output
dotnet run --project tools/IntegrationRunner -- --scenario IT-04 --verbose
```

## Test Decision Tree

```
New hardware setup?
  -> Run IT-01 first
  -> IT-01 fails? -> Check Troubleshooting Guide Section 6 (Frame Acquisition)
  -> IT-01 passes? -> Run IT-03 (100 frames, verifies continuous operation)

Network issues suspected?
  -> Run IT-07 (packet loss) and IT-08 (connection)
  -> Check: UDP buffers (sysctl net.core.rmem_max), MTU (ip link show eth1)

Error recovery validation?
  -> Run IT-04 (error injection)

Long-term stability?
  -> Run IT-09 (10,000 frames)
```

## Version

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI | Initial integration test plan |
