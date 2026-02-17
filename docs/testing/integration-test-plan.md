# Integration Test Plan

**Project**: X-ray Detector Panel System
**Document Version**: 1.0.0
**Last Updated**: 2026-02-17
**Milestone**: M3 (W14) - All IT scenarios pass

---

## Overview

Integration tests validate end-to-end data flow across module boundaries. All scenarios use the software simulator pipeline before hardware-in-the-loop (HIL) testing.

---

## Test Environment

**Software Stack**:
- .NET 8.0+ runtime
- IntegrationRunner CLI tool
- All 4 simulators: PanelSimulator, FpgaSimulator, McuSimulator, HostSimulator

**Configuration**: `detector_config.yaml` (loaded by IntegrationRunner)

**Execution**: `dotnet run --project IntegrationRunner -- --scenario IT-XX`

---

## Integration Scenarios (IT-01 to IT-10)

### IT-01: Single Frame Capture (Minimum Tier)

**Objective**: Validate complete data path for one frame at minimum resolution

| Parameter | Value |
|-----------|-------|
| Resolution | 1024 x 1024 |
| Bit Depth | 14-bit |
| FPS | 15 |
| Frames | 1 |

**Steps**:
1. PanelSimulator generates 1 frame (counter pattern, no noise)
2. FpgaSimulator processes frame through line buffer and CSI-2 TX
3. McuSimulator receives CSI-2 packets, generates UDP packets
4. HostSimulator receives UDP packets, reassembles frame

**Pass Criteria**:
- Output frame matches input frame (bit-exact comparison)
- Zero pixel errors
- Total latency < 100 ms (simulated)
- All CSI-2 CRC checks pass

---

### IT-02: Multi-Frame Streaming (Minimum Tier)

**Objective**: Validate sustained streaming for 100 frames

| Parameter | Value |
|-----------|-------|
| Resolution | 1024 x 1024 |
| Bit Depth | 14-bit |
| FPS | 15 |
| Frames | 100 |

**Steps**:
1. PanelSimulator generates 100 sequential frames
2. FpgaSimulator streams through CSI-2 TX continuously
3. McuSimulator streams via simulated Ethernet
4. HostSimulator reassembles all 100 frames

**Pass Criteria**:
- All 100 frames bit-exact match
- Frame sequence numbers sequential (0-99)
- Zero dropped frames
- Frame interval within 5% of target (66.7 ms at 15 fps)

---

### IT-03: Target Tier Full Pipeline

**Objective**: Validate target resolution data path

| Parameter | Value |
|-----------|-------|
| Resolution | 2048 x 2048 |
| Bit Depth | 16-bit |
| FPS | 15 |
| Frames | 10 |

**Steps**:
1. PanelSimulator generates 10 frames at target resolution
2. Full pipeline processes frames
3. HostSimulator saves frames to TIFF

**Pass Criteria**:
- All 10 frames bit-exact match
- TIFF files valid and readable
- Frame size = 8,388,608 bytes each
- CSI-2 bandwidth utilization logged

---

### IT-04: Maximum Tier Bandwidth Stress

**Objective**: Validate maximum resolution data path (stress test)

| Parameter | Value |
|-----------|-------|
| Resolution | 3072 x 3072 |
| Bit Depth | 16-bit |
| FPS | 15 |
| Frames | 5 |

**Steps**:
1. PanelSimulator generates 5 frames at maximum resolution
2. FpgaSimulator processes (verify CSI-2 bandwidth capacity)
3. McuSimulator streams via 10 GbE
4. HostSimulator reassembles and validates

**Pass Criteria**:
- All 5 frames bit-exact match
- CSI-2 bandwidth < 4.5 Gbps (within D-PHY limit)
- 10 GbE bandwidth < 9.5 Gbps
- No buffer overflows reported

---

### IT-05: Noise and Defect Simulation

**Objective**: Validate realistic panel data with noise and defects

| Parameter | Value |
|-----------|-------|
| Resolution | 2048 x 2048 |
| Noise Model | Gaussian (stddev=100) |
| Defect Rate | 0.1% |
| Frames | 10 |

**Steps**:
1. PanelSimulator generates noisy frames with dead/hot pixels
2. Full pipeline processes frames
3. HostSimulator receives and stores

**Pass Criteria**:
- Output frames match PanelSimulator output (data integrity through pipeline)
- Noise statistics preserved (mean, stddev within 1% tolerance)
- Defect pixel positions preserved
- No data corruption from noise values

---

### IT-06: SPI Control Channel

**Objective**: Validate bidirectional SPI control between McuSimulator and FpgaSimulator

**Steps**:
1. McuSimulator writes START_SCAN to FPGA CONTROL register (0x00)
2. FpgaSimulator transitions to scanning state
3. McuSimulator reads STATUS register (0x04) - verify BUSY
4. FpgaSimulator completes one frame
5. McuSimulator reads FRAME_COUNTER register (0x08) - verify = 1
6. McuSimulator writes STOP_SCAN to CONTROL register
7. McuSimulator reads STATUS - verify IDLE

**Pass Criteria**:
- All SPI register values correct
- State transitions match expected sequence
- SPI latency < 10 ms (simulated)
- Frame counter increments correctly

---

### IT-07: Error Detection and Recovery

**Objective**: Validate error handling across module boundaries

**Sub-scenarios**:

| ID | Error Type | Trigger | Expected Recovery |
|----|-----------|---------|-------------------|
| IT-07a | SPI timeout | McuSimulator stops SPI for 200 ms | FpgaSimulator enters ERROR state |
| IT-07b | Buffer overflow | FpgaSimulator receives data faster than CSI-2 TX | Overflow flag set, scan stops |
| IT-07c | UDP packet loss | HostSimulator drops 1% of packets | Frame marked incomplete |
| IT-07d | Frame drop | McuSimulator drops 1 frame | HostSimulator detects gap in sequence |

**Pass Criteria**:
- Error flags set correctly in FPGA registers
- McuSimulator detects error via SPI polling
- HostSimulator reports incomplete/dropped frames
- System recoverable after error clearing

---

### IT-08: Configuration Changes at Runtime

**Objective**: Validate dynamic configuration changes

**Steps**:
1. Start pipeline at minimum tier (1024x1024, 14-bit, 15fps)
2. Capture 5 frames (verify correct operation)
3. Stop scan
4. Reconfigure to target tier (2048x2048, 16-bit, 15fps)
5. Start scan, capture 5 frames
6. Verify both sets of frames correct

**Pass Criteria**:
- Both resolution modes produce correct output
- No data corruption during reconfiguration
- SPI register updates propagate correctly
- Frame size changes reflected in all layers

---

### IT-09: Long Duration Stability

**Objective**: Validate system stability over extended operation

| Parameter | Value |
|-----------|-------|
| Resolution | 2048 x 2048 |
| Bit Depth | 16-bit |
| FPS | 15 |
| Frames | 1000 |
| Duration | ~67 seconds (simulated) |

**Steps**:
1. Run continuous pipeline for 1000 frames
2. Monitor memory usage, buffer states, counters
3. Validate every 100th frame (spot check)
4. Full validation on first and last frames

**Pass Criteria**:
- Zero bit errors across all validated frames
- Frame counter = 1000 at end
- No memory leaks (stable memory footprint)
- No buffer overflows or underflows
- Frame drop rate < 0.01% (max 0 drops in 1000 frames)

---

### IT-10: Multi-Format Storage

**Objective**: Validate all storage formats

**Steps**:
1. Capture 1 frame at target tier
2. Save as TIFF (uncompressed)
3. Save as TIFF (LZW compressed)
4. Save as RAW
5. Reload each file and compare to original

**Pass Criteria**:
- TIFF (uncompressed): bit-exact match, valid TIFF header
- TIFF (LZW): bit-exact match after decompression
- RAW: bit-exact match, file size = rows * cols * 2 bytes
- All formats readable by standard tools (ImageJ, Python PIL)

---

## Test Execution Matrix

| Scenario | Min Tier | Target Tier | Max Tier | Priority |
|----------|---------|-------------|---------|----------|
| IT-01 | Primary | - | - | P0 |
| IT-02 | Primary | - | - | P0 |
| IT-03 | - | Primary | - | P0 |
| IT-04 | - | - | Primary | P1 |
| IT-05 | - | Primary | - | P0 |
| IT-06 | N/A | N/A | N/A | P0 |
| IT-07 | Primary | - | - | P1 |
| IT-08 | Both | Both | - | P1 |
| IT-09 | - | Primary | - | P0 |
| IT-10 | - | Primary | - | P1 |

**P0**: Must pass for M3 milestone
**P1**: Should pass, acceptable deferral to M4

---

## Dependencies

- All unit tests (FV-01 to FV-11, SW-01 to SW-08) must pass before integration testing
- `detector_config.yaml` schema validation must pass
- IntegrationRunner CLI tool must be functional
- All simulators must implement `ISimulator` interface

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (analyst) | Initial integration test plan |

---
