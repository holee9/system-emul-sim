# Hardware-in-the-Loop (HIL) Test Plan

## Overview

This document defines HIL test procedures using actual FPGA and SoC hardware boards. HIL testing validates the complete hardware stack that software simulation cannot fully replicate.

**Hardware Required**:
- Xilinx Artix-7 XC7A35T-FGG484 evaluation board
- NXP i.MX8M Plus EVK (Variscite VAR-SOM-MX8M-PLUS)
- FPC cable: 10cm, 15-pin, 4-lane D-PHY, 100 ohm differential impedance
- 10 GbE network connection (Host PC to SoC)

**Reference**: SPEC-FPGA-001, SPEC-FW-001

## HIL Test Patterns

### Pattern A: Basic Connectivity (HIL-A)

**Purpose**: Verify physical hardware connections before functional testing

**Procedures**:

**HIL-A-01: Power and FPGA Configuration**
1. Power on FPGA board
2. Verify DONE LED illuminates within 2 seconds
3. Connect SoC UART (115200 baud, 8N1)
4. Power on SoC board
5. Verify Linux boot messages appear within 30 seconds
6. Login and verify kernel: `uname -r` (expected: 6.6.52)

**Pass Criteria**:
- DONE LED: illuminates within 2s of power-on
- Linux boot: completes within 30s
- Kernel version: 6.6.52

**HIL-A-02: SPI Communication Verification**
1. Run detector_daemon in test mode
2. Read DEVICE_ID registers

```bash
detector_cli read-reg 0x00  # Upper 16-bit: expected 0xD7E0
detector_cli read-reg 0x01  # Lower 16-bit: expected 0x0001
```

**Pass Criteria**:
- Register 0x00 = 0xD7E0
- Register 0x01 = 0x0001
- Read latency < 1ms per register

**HIL-A-03: CSI-2 Link Establishment**
1. Configure CSI-2 for 400M, 4-lane
2. Enable V4L2 streaming
3. Verify link status

```bash
detector_cli read-reg 0x70  # CSI2_STATUS
# Expected: bit 0 (phy_ready) = 1
v4l2-ctl --list-devices      # Should show /dev/video0
```

**Pass Criteria**:
- CSI2_STATUS bit 0 = 1 (phy_ready)
- /dev/video0 device present
- No kernel errors in dmesg

---

### Pattern B: Performance Validation (HIL-B)

**HIL-B-01: Single Frame Acquisition**
1. Load test pattern into FPGA (gradient or checkerboard)
2. Trigger single-shot scan
3. Capture frame via Host SDK
4. Verify pixel data integrity

**Pass Criteria**:
- Frame received within 200ms of trigger
- Pixel values match expected test pattern (bit-accurate)
- FrameHeader magic: 0xD7E01234
- No CRC errors in ERROR_FLAGS (0x80 = 0x00)

**HIL-B-02: 400M Continuous Acquisition (Intermediate-A)**
1. Configure: 2048x2048, 16-bit, 15fps, 400M lane speed
2. Acquire 1000 frames continuously
3. Verify throughput and integrity

**Pass Criteria**:
- Actual FPS: 15fps +/- 5% (14.25-15.75 fps)
- Frame drop rate < 0.01%
- CSI-2 bandwidth utilization: ~63% of 1.6 Gbps
- All frames pixel-accurate (checksum verification)

**HIL-B-03: 800M Link Initialization**
1. Switch to 800M lane speed: `detector_cli write-reg 0x61 1`
2. Power cycle FPGA to apply
3. Verify 800M link establishment

```bash
detector_cli read-reg 0x61  # Should read back 1
detector_cli read-reg 0x70  # CSI2_STATUS: bit 0 = 1
```

**Pass Criteria**:
- CSI2_LANE_SPEED reads back 1 (800M)
- CSI2_STATUS bit 0 = 1 (phy_ready)
- Link established within 5 seconds of power cycle

**HIL-B-04: 800M Continuous Acquisition (Target Tier)**
*Prerequisite: HIL-B-03 passed*
1. Configure: 3072x3072, 16-bit, 15fps, 800M lane speed
2. Acquire 100 frames continuously
3. Verify throughput

**Pass Criteria**:
- Actual FPS: 15fps +/- 5%
- Frame drop rate < 0.1% (validation threshold; <0.01% is production target)
- CSI-2 bandwidth: ~83% of 3.2 Gbps (2.664 Gbps / 3.2 Gbps)
- CRC error rate: < 0.001% of packets

**HIL-B-05: 1-Hour Continuous Acquisition (Stability Test)**
*Prerequisite: HIL-B-02 passed*
1. Configure: 2048x2048, 16-bit, 15fps (400M, stable tier)
2. Run continuous acquisition for 3,600 seconds (1 hour)
3. Monitor system metrics throughout

**Monitored Metrics**:
```bash
# Every 5 minutes, record:
detector_cli read-reg 0x30  # FRAME_COUNT_HI
detector_cli read-reg 0x31  # FRAME_COUNT_LO
detector_cli read-reg 0x80  # ERROR_FLAGS
free -m                      # SoC memory
```

**Pass Criteria**:
- Total frames: 54,000 +/- 270 (15fps x 3600s +/- 0.5%)
- Drop rate: < 0.01% (< 5.4 frames)
- ERROR_FLAGS: 0x00 for >= 99.9% of duration
- SoC memory: RSS growth < 100MB over 1 hour
- FPGA board temperature: < ambient + 15 degrees C

---

### Pattern C: Error Recovery (HIL-C)

**HIL-C-01: SPI Error Recovery**
1. During active scan, physically disconnect SPI MISO wire
2. Verify WATCHDOG error fires (ERROR_FLAGS bit 7)
3. Reconnect MISO wire
4. Verify system recovers

**Pass Criteria**:
- WATCHDOG error detected within 200ms
- Error correctly reported to Host
- System recovers after MISO reconnection (soft reset succeeds)
- Subsequent scans complete normally

**HIL-C-02: CSI-2 Cable Disconnect/Reconnect**
1. During active scan, disconnect CSI-2 FPC cable
2. Verify error detection
3. Reconnect cable
4. Verify V4L2 pipeline restarts automatically

**Pass Criteria**:
- Error detected within 500ms
- V4L2 pipeline restarts within 5 seconds of reconnection
- Frame acquisition resumes automatically
- No permanent state corruption

## HIL Test Execution Schedule

| Phase | HIL Pattern | When | Hardware Required |
|-------|------------|------|------------------|
| W23-24 | HIL-A (all) | First hardware bring-up | FPGA + SoC boards |
| W25 | HIL-B-01, B-02 | After basic connectivity confirmed | + FPC cable |
| W26 (PoC) | HIL-B-03, B-04 | 800M validation | Full setup |
| W27 | HIL-B-05 | Stability validation | Full setup + monitoring |
| W28 | HIL-C-01, C-02 | Error recovery validation | Full setup |

## Version

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI | Initial HIL test plan |
