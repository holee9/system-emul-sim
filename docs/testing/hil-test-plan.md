# Hardware-in-the-Loop (HIL) Test Plan

**Project**: X-ray Detector Panel System
**Document Version**: 1.0.0
**Last Updated**: 2026-02-17
**Milestone**: M4 (W18) - HIL Pattern A/B pass

---

## Overview

HIL tests validate hardware/software integration with actual FPGA and SoC hardware while using simulated panel input. Two test patterns (A and B) validate distinct system aspects.

---

## Hardware Configuration

### Required Equipment

| Equipment | Model | Purpose |
|-----------|-------|---------|
| FPGA Board | Artix-7 XC7A35T-FGG484 EVK | FPGA under test |
| SoC Board | NXP i.MX8M Plus EVK | SoC controller |
| FPC Cable | MIPI CSI-2, 10 cm, 100 ohm diff | FPGA-to-SoC data link |
| Host PC | x86-64, 10 GbE NIC | Frame capture and validation |
| Network | 10 GbE switch or direct cable | SoC-to-Host link |
| JTAG | Xilinx Platform Cable or USB-JTAG | FPGA programming |
| Serial | USB-to-UART adapter | SoC console access |

### Optional Equipment

| Equipment | Purpose |
|-----------|---------|
| Logic Analyzer (MIPI D-PHY capable) | Signal integrity validation |
| Oscilloscope (>2 GHz bandwidth) | Eye diagram measurement |
| Current Probe | Power consumption measurement |

### Connection Diagram

```
[Host PC]
    |
    | 10 GbE (RJ45 or SFP+)
    |
[10 GbE Switch or Direct Cable]
    |
    | 10 GbE
    |
[SoC: i.MX8M Plus EVK]
    |              |
    | CSI-2 FPC    | SPI (4 wires)
    | (10 cm)      |
    |              |
[FPGA: Artix-7 35T EVK]
    |
    | JTAG (programming)
    |
[Host PC or separate programmer]
```

---

## Pattern A: Data Integrity Validation

**Objective**: Validate bit-accurate data transfer from FPGA to Host PC through hardware

### HIL-A-01: Single Frame Data Integrity

| Parameter | Value |
|-----------|-------|
| Resolution | 1024 x 1024 |
| Bit Depth | 16-bit |
| Pattern | Counter (0x0000, 0x0001, ...) |
| Frames | 1 |

**Procedure**:
1. Program FPGA with test pattern generator bitstream
2. Boot SoC with CSI-2 RX driver
3. Configure Host SDK to receive frames
4. Trigger single frame capture via SPI
5. Compare received frame against expected counter pattern

**Pass Criteria**:
- Zero bit errors in 1024*1024 = 1,048,576 pixels
- Frame received within 100 ms of trigger
- CSI-2 CRC pass (no packet errors on SoC side)

---

### HIL-A-02: Sustained Streaming Data Integrity

| Parameter | Value |
|-----------|-------|
| Resolution | 2048 x 2048 |
| Bit Depth | 16-bit |
| Pattern | Counter |
| Frames | 1000 |
| Duration | ~67 seconds at 15 fps |

**Procedure**:
1. Start continuous scanning at target tier
2. Host SDK captures 1000 frames
3. Validate every frame against expected pattern
4. Report bit error rate and frame drop rate

**Pass Criteria**:
- Zero bit errors across all 1000 frames
- Frame drop rate < 0.01% (max 0 drops)
- Sustained throughput >= 1.01 Gbps (2048x2048x16x15)
- No SoC kernel errors (check `dmesg`)

---

### HIL-A-03: Checkerboard Pattern Stress Test

| Parameter | Value |
|-----------|-------|
| Resolution | 2048 x 2048 |
| Pattern | Checkerboard (0xFFFF, 0x0000 alternating) |
| Frames | 100 |

**Procedure**:
1. Configure FPGA for checkerboard pattern (maximum toggle rate)
2. Capture 100 frames through full pipeline
3. Validate each pixel against expected checkerboard

**Pass Criteria**:
- Zero bit errors (validates D-PHY signal integrity under worst-case switching)
- No intermittent errors (each frame independently correct)

---

### HIL-A-04: Maximum Tier Data Path (Conditional)

| Parameter | Value |
|-----------|-------|
| Resolution | 3072 x 3072 |
| Bit Depth | 16-bit |
| Pattern | Counter |
| Frames | 10 |
| Condition | Only if CSI-2 800M debugging complete |

**Procedure**:
1. Configure FPGA CSI-2 TX for 800 Mbps/lane or higher
2. Capture 10 frames at maximum resolution
3. Validate data integrity

**Pass Criteria**:
- Zero bit errors in all frames
- CSI-2 aggregate bandwidth >= 2.26 Gbps
- If FAIL: Document actual bandwidth limit, defer to 2048x2048

---

## Pattern B: System Behavior Validation

**Objective**: Validate system control, error handling, and operational behavior

### HIL-B-01: SPI Control Channel Latency

**Procedure**:
1. Host SDK sends StartScan command via network
2. SoC Sequence Engine sends SPI write to FPGA
3. Measure time from SDK command to first CSI-2 packet received
4. Repeat 100 times, compute statistics

**Pass Criteria**:
- Mean latency < 10 ms (command to first data)
- Maximum latency < 50 ms
- Standard deviation < 5 ms

---

### HIL-B-02: Start/Stop Scan Sequence

**Procedure**:
1. Host SDK: StartScan()
2. Wait for 10 frames
3. Host SDK: StopScan()
4. Verify scanning stops (no new frames)
5. Host SDK: StartScan() again
6. Verify scanning resumes (new frames arrive)
7. Repeat 10 cycles

**Pass Criteria**:
- Each start produces frames within 100 ms
- Each stop halts frames within 1 frame period
- Frame counter continues across start/stop cycles
- No data corruption during transitions

---

### HIL-B-03: Error Detection - SPI Timeout

**Procedure**:
1. Start normal scanning
2. SoC stops SPI polling (simulate SoC hang)
3. Wait for FPGA watchdog timeout (configured `timeout_ms`)
4. Resume SPI polling
5. Read FPGA ERROR_FLAGS register

**Pass Criteria**:
- FPGA enters ERROR state after timeout period
- Timeout error flag set in ERROR_FLAGS (0x10)
- Scan stops automatically
- FPGA recoverable via reset command

---

### HIL-B-04: Power Cycle Recovery

**Procedure**:
1. Start scanning at target tier
2. Power cycle FPGA board (simulate power glitch)
3. Wait for FPGA reboot (JTAG reprogram or flash boot)
4. SoC detects FPGA recovery via SPI status read
5. Resume scanning

**Pass Criteria**:
- SoC detects FPGA restart (STATUS register reads IDLE after power cycle)
- Scanning resumes successfully after recovery
- No persistent errors or state corruption

---

### HIL-B-05: Thermal Stability (Long Duration)

| Parameter | Value |
|-----------|-------|
| Resolution | 2048 x 2048 |
| Duration | 1 hour continuous |
| Frames | ~54,000 at 15 fps |

**Procedure**:
1. Start continuous scanning
2. Monitor FPGA temperature via XADC (on-chip temp sensor)
3. Monitor SoC temperature via `/sys/class/thermal/`
4. Sample temperature every 60 seconds
5. Validate frames every 1000th frame (spot check)

**Pass Criteria**:
- FPGA temperature < 85 C (Artix-7 commercial grade limit)
- SoC temperature < 90 C (i.MX8M Plus limit)
- Temperature stabilizes within 15 minutes
- Zero bit errors in validated frames
- No thermal throttling events

---

### HIL-B-06: Network Robustness

**Procedure**:
1. Start streaming at target tier
2. Introduce network impairment (via `tc` tool on SoC):
   - 0.1% packet loss
   - 1 ms jitter
   - 10 ms delay
3. Host SDK captures 100 frames
4. Report incomplete frames and retransmission statistics

**Pass Criteria**:
- Frame completion rate > 99% (max 1 incomplete frame in 100)
- Incomplete frames detected and reported by SDK
- No crash or hang on either SoC or Host side
- Graceful degradation under impairment

---

## Test Schedule

| Week | Pattern | Scenarios | Prerequisites |
|------|---------|-----------|---------------|
| W14-W15 | A | HIL-A-01, HIL-A-02 | FPGA bitstream, SoC driver ready |
| W15-W16 | A | HIL-A-03, HIL-A-04 | HIL-A-01 pass |
| W16-W17 | B | HIL-B-01, HIL-B-02, HIL-B-03 | All Pattern A pass |
| W17-W18 | B | HIL-B-04, HIL-B-05, HIL-B-06 | Basic Pattern B pass |

---

## Pass/Fail Criteria for M4 Gate

**Required (M4 Pass)**:
- HIL-A-01: Single frame data integrity at 1024x1024
- HIL-A-02: Sustained streaming at 2048x2048 (1000 frames)
- HIL-B-01: SPI control latency < 10 ms
- HIL-B-02: Start/Stop sequence works (10 cycles)

**Desirable (M4 Stretch)**:
- HIL-A-03: Checkerboard stress test
- HIL-A-04: Maximum tier (3072x3072) if 800M ready
- HIL-B-03 to HIL-B-06: Error handling and stability tests

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (analyst) | Initial HIL test plan |

---
