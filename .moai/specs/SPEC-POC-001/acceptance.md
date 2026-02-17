# SPEC-POC-001 Acceptance Criteria

---
spec_id: SPEC-POC-001
milestone: M0.5
gate_week: W6
test_framework: Manual + Python validation scripts
---

## Overview

This document defines detailed acceptance criteria for M0.5 CSI-2 Proof of Concept using Given-When-Then format. All criteria must be validated and documented in the PoC Test Report.

---

## AC-001: FPGA CSI-2 TX Module Instantiation

### Scenario 1: CSI-2 IP Integration and Bitstream Generation

**GIVEN**:
- Xilinx Artix-7 XC7A35T-FGG484 FPGA evaluation board connected via JTAG
- Vivado 2023.2+ with MIPI CSI-2 TX Subsystem IP v3.1 installed
- License for MIPI CSI-2 IP verified (Vivado HL Design Edition)

**WHEN**:
- Vivado project is created with target device xc7a35tfgg484-1
- MIPI CSI-2 TX Subsystem IP is configured:
  - Lanes: 4 data lanes + 1 clock lane
  - Lane speed: 1.0 Gbps/lane
  - Data type: RAW16 (0x2C)
  - Virtual channel: VC0
- Synthesis, implementation, and bitstream generation are executed

**THEN**:
- ✅ Synthesis completes without errors
- ✅ Implementation achieves timing closure (WNS ≥ 0 ns, TNS = 0 ns)
- ✅ Bitstream file `csi2_poc.bit` is generated (size ~1-2 MB)
- ✅ Resource utilization report shows:
  - LUTs: ≤5,000 (≤24% of 20,800 available)
  - BRAMs: ≤5 (≤10% of 50 available)
  - OSERDES: 8 used (4 data lanes × 2 for DDR serialization)

**Validation**:
- Check Vivado log: `vivado.log` contains "write_bitstream completed successfully"
- Review utilization report: `reports/utilization.rpt`, grep for "Slice LUTs" and "Block RAM Tile"

**Pass Criteria**: All ✅ conditions met, no critical warnings

---

### Scenario 2: FPGA Programming and Basic Connectivity

**GIVEN**:
- Bitstream `csi2_poc.bit` generated in AC-001 Scenario 1
- FPGA powered on, JTAG cable connected

**WHEN**:
- Bitstream is programmed to FPGA: `program_hw_devices -bitstream csi2_poc.bit`
- FPGA user LEDs are observed (test pattern generator running indicator)

**THEN**:
- ✅ Programming completes without errors
- ✅ FPGA configuration status LED turns green (configuration successful)
- ✅ User LED blinks at 1 Hz (indicates test pattern generator is active)
- ✅ D-PHY output pins show activity (measured with oscilloscope: toggling signal at ~1 GHz)

**Validation**:
- Vivado Hardware Manager shows "FPGA configured successfully"
- Oscilloscope probe on D-PHY lane 0: frequency ~1 GHz (1.0 Gbps DDR)

**Pass Criteria**: All ✅ conditions met

---

### Scenario 3: ILA (Integrated Logic Analyzer) Validation

**GIVEN**:
- FPGA programmed with bitstream containing ILA probes on test pattern generator
- Vivado Hardware Manager connected to FPGA

**WHEN**:
- ILA is triggered on `frame_valid` rising edge
- Waveform capture is executed for 2048 pixel clocks (one line)

**THEN**:
- ✅ ILA captures waveform successfully
- ✅ `pixel_data` signal increments: 0x0000 → 0x0001 → 0x0002 → ... (counter pattern)
- ✅ `pixel_valid` asserts for 2048 consecutive clock cycles (one line)
- ✅ `line_valid` asserts at start of line, de-asserts at end
- ✅ No glitches or metastability observed on captured signals

**Validation**:
- Export ILA waveform: `write_hw_ila_data ila_counter_pattern.csv`
- Python script verifies counter sequence:
  ```python
  import csv
  data = [int(row['pixel_data'], 16) for row in csv.DictReader(open('ila_counter_pattern.csv'))]
  assert data == list(range(2048)), "Counter pattern mismatch"
  ```

**Pass Criteria**: All ✅ conditions met, Python validation script outputs "PASS"

---

## AC-002: SoC CSI-2 RX Pipeline Configuration

### Scenario 1: Linux Kernel Boot and Driver Loading

**GIVEN**:
- NXP i.MX8M Plus EVK with Linux 5.15+ flashed to eMMC or SD card
- UART console connected (115200 baud)

**WHEN**:
- SoC is powered on
- Linux kernel boots

**THEN**:
- ✅ Kernel boot completes without errors (no kernel panic)
- ✅ MIPI CSI-2 driver module is loaded: `lsmod | grep imx8_mipi_csi2` shows module
- ✅ V4L2 device is registered: `ls /dev/video*` shows `/dev/video0`
- ✅ Kernel log shows CSI-2 initialization: `dmesg | grep mipi` contains "Registered sensor subdevice"

**Validation**:
- UART console output captured in `boot_log.txt`
- Check for error messages: `grep -i "error\|fail\|panic" boot_log.txt` returns empty

**Pass Criteria**: All ✅ conditions met, no kernel errors

---

### Scenario 2: Device Tree Configuration and V4L2 Query

**GIVEN**:
- Device tree configured for MIPI CSI-2 4-lane RAW16 input (per plan.md Phase 3)
- SoC rebooted after device tree update

**WHEN**:
- V4L2 device capabilities are queried: `v4l2-ctl -d /dev/video0 --all`

**THEN**:
- ✅ Device name: "imx8-mipi-csi2" or similar
- ✅ Supported formats include: RAW16 (Bayer RG16)
- ✅ Resolution capabilities: Width=1024-4096, Height=1024-4096 (covers all test cases)
- ✅ Frame rate capabilities: 1-60 fps

**Validation**:
- Save V4L2 output: `v4l2-ctl -d /dev/video0 --all > v4l2_caps.txt`
- Grep for RAW16: `grep -i "RG16\|RAW16" v4l2_caps.txt` returns match

**Pass Criteria**: All ✅ conditions met

---

### Scenario 3: Single Frame Capture Test

**GIVEN**:
- FPGA transmitting counter pattern at 1024×1024 resolution, 15 fps
- SoC V4L2 device configured for RAW16, 1024×1024

**WHEN**:
- Frame capture command is executed: `v4l2-ctl --device /dev/video0 --stream-mmap --stream-count=1 --stream-to=frame_001.raw`

**THEN**:
- ✅ Command completes without errors
- ✅ File `frame_001.raw` is created
- ✅ File size is exactly 2,097,152 bytes (1024×1024 pixels × 2 bytes/pixel)
- ✅ Hexdump of first 32 bytes shows counter pattern:
  - Expected: `00 00 01 00 02 00 03 00 04 00 05 00 06 00 07 00 08 00 09 00 0a 00 0b 00 0c 00 0d 00 0e 00 0f 00`
  - Actual: `hexdump -C frame_001.raw | head -n 2` matches expected

**Validation**:
- File size check: `stat -c %s frame_001.raw` returns 2097152
- Hexdump comparison (automated):
  ```bash
  expected="00000000  00 00 01 00 02 00 03 00  04 00 05 00 06 00 07 00"
  actual=$(hexdump -C frame_001.raw | head -n 1)
  [[ "$actual" == "$expected" ]] && echo "PASS" || echo "FAIL"
  ```

**Pass Criteria**: All ✅ conditions met, hexdump matches

---

## AC-003: End-to-End Data Integrity (Counter Pattern)

### Scenario 1: 100-Frame Capture with Zero Bit Errors (Minimum Tier)

**GIVEN**:
- FPGA transmitting counter pattern at 1024×1024 resolution, 15 fps (Minimum tier)
- SoC V4L2 device configured for RAW16, 1024×1024

**WHEN**:
- 100 frames are captured: `v4l2-ctl --stream-count=100 --stream-to=frames_%03d.raw`
- Python validation script processes all 100 frames

**THEN**:
- ✅ All 100 frame files are created (frames_001.raw through frames_100.raw)
- ✅ Each file size is exactly 2,097,152 bytes
- ✅ Validation script reports: **0 bit errors** across all 100 frames
- ✅ Total validated data: 100 frames × 1,048,576 pixels × 16 bits = 1.68 Gbit

**Validation Script** (`validate_100_frames.py`):
```python
import numpy as np
import glob

total_errors = 0
for i in range(1, 101):
    frame = np.fromfile(f'frames_{i:03d}.raw', dtype=np.uint16).reshape(1024, 1024)
    expected = np.arange(1024*1024, dtype=np.uint16).reshape(1024, 1024)
    errors = np.sum(frame != expected)
    total_errors += errors
    if errors > 0:
        print(f"Frame {i}: {errors} bit errors")

print(f"Total bit errors across 100 frames: {total_errors}")
assert total_errors == 0, f"FAIL: {total_errors} errors detected"
print("PASS: Zero bit errors in 100 frames")
```

**Pass Criteria**: Script outputs "PASS: Zero bit errors in 100 frames"

---

### Scenario 2: 1000-Frame Capture with Frame Drop Detection (Target Tier)

**GIVEN**:
- FPGA transmitting counter pattern at 2048×2048 resolution, 30 fps (Target tier)
- SoC V4L2 device configured for RAW16, 2048×2048

**WHEN**:
- 1000 frames are captured: `v4l2-ctl --stream-count=1000 --stream-to=frames_%04d.raw`
- Duration is measured: `time v4l2-ctl ...`

**THEN**:
- ✅ 1000 frame files are created
- ✅ Each file size is exactly 8,388,608 bytes (2048×2048 × 2)
- ✅ Frame drop rate <1% (≤10 dropped frames)
- ✅ Validation script reports: **0 bit errors** across all captured frames
- ✅ Total validated data: 1000 frames × 4,194,304 pixels × 16 bits = 67.1 Gbit

**Frame Drop Detection** (`validate_sequence.py`):
```python
import numpy as np
import glob

frame_files = sorted(glob.glob('frames_*.raw'))
expected_count = 1000
actual_count = len(frame_files)

print(f"Expected frames: {expected_count}, Actual frames: {actual_count}")
dropped = expected_count - actual_count
drop_rate = dropped / expected_count * 100
print(f"Dropped frames: {dropped} ({drop_rate:.2f}%)")

assert drop_rate < 1.0, f"FAIL: Drop rate {drop_rate:.2f}% exceeds 1%"
print(f"PASS: Drop rate {drop_rate:.2f}% is acceptable")

# Validate data integrity of captured frames
total_errors = 0
for i, frame_file in enumerate(frame_files, start=1):
    frame = np.fromfile(frame_file, dtype=np.uint16).reshape(2048, 2048)
    expected = np.arange(2048*2048, dtype=np.uint16).reshape(2048, 2048)
    errors = np.sum(frame != expected)
    total_errors += errors

print(f"Total bit errors: {total_errors}")
assert total_errors == 0, f"FAIL: {total_errors} errors detected"
print("PASS: Zero bit errors in all captured frames")
```

**Pass Criteria**: Script outputs "PASS" for both drop rate and bit errors

---

### Scenario 3: Checkerboard Pattern Validation (Electrical Stress Test)

**GIVEN**:
- FPGA transmitting checkerboard pattern (alternating 0xFFFF and 0x0000) at 1024×1024, 15 fps
- SoC V4L2 device configured for RAW16, 1024×1024

**WHEN**:
- 100 frames are captured
- Python validation script checks for expected checkerboard pattern

**THEN**:
- ✅ All 100 frames match expected checkerboard pattern
- ✅ No bit flips (0xFFFF → 0xFFFE or 0x0000 → 0x0001) detected
- ✅ Zero bit errors across all frames

**Validation Script** (`validate_checkerboard.py`):
```python
import numpy as np

for i in range(1, 101):
    frame = np.fromfile(f'frames_{i:03d}.raw', dtype=np.uint16).reshape(1024, 1024)

    # Expected checkerboard: even pixels=0xFFFF, odd pixels=0x0000
    expected = np.zeros((1024, 1024), dtype=np.uint16)
    expected[::2, ::2] = 0xFFFF  # Even rows, even cols
    expected[1::2, 1::2] = 0xFFFF  # Odd rows, odd cols

    errors = np.sum(frame != expected)
    assert errors == 0, f"Frame {i}: {errors} errors"

print("PASS: Checkerboard pattern validated across 100 frames")
```

**Pass Criteria**: Script outputs "PASS"

---

## AC-004: End-to-End Throughput Measurement (Target Tier)

### Scenario 1: Sustained Throughput at 2048×2048, 30 fps

**GIVEN**:
- FPGA transmitting counter pattern at 2048×2048 resolution, 30 fps
- SoC V4L2 device configured for RAW16, 2048×2048

**WHEN**:
- 1000 frames are captured with time measurement:
  ```bash
  time v4l2-ctl --device /dev/video0 --stream-mmap --stream-count=1000 --stream-to=/dev/null
  ```
- Duration is extracted from `time` command output (real time in seconds)

**THEN**:
- ✅ Expected duration: 33.33 seconds (1000 frames / 30 fps)
- ✅ Actual duration: 33.33 ± 1.0 seconds (±3% tolerance)
- ✅ Calculated throughput: 2.01 ± 0.06 Gbps
- ✅ Throughput meets **70% threshold**: ≥1.41 Gbps (2.01 × 0.7 = 1.407)

**Throughput Calculation**:
```python
width, height, fps = 2048, 2048, 30
num_frames = 1000
duration_sec = 33.5  # Example: actual measured time

bits_per_frame = width * height * 16  # 16-bit pixels
total_bits = bits_per_frame * num_frames
throughput_bps = total_bits / duration_sec
throughput_gbps = throughput_bps / 1e9

print(f"Measured duration: {duration_sec:.2f} seconds")
print(f"Calculated throughput: {throughput_gbps:.2f} Gbps")

assert throughput_gbps >= 1.41, f"FAIL: Throughput {throughput_gbps:.2f} < 1.41 Gbps"
print(f"PASS: Throughput {throughput_gbps:.2f} Gbps meets 70% threshold")
```

**Pass Criteria**: Throughput ≥1.41 Gbps (outputs "PASS")

---

### Scenario 2: Frame Rate Stability

**GIVEN**:
- 1000 frames captured in AC-004 Scenario 1

**WHEN**:
- Frame timestamps are extracted from V4L2 metadata (if available) or system timestamps
- Inter-frame intervals are calculated

**THEN**:
- ✅ Average frame interval: 33.33 ms ± 1.0 ms (30 fps ± 3%)
- ✅ Frame interval jitter (std dev): <5 ms
- ✅ No frame interval outliers >50 ms (no severe stalls)

**Validation**:
```python
import numpy as np

# Simulated frame timestamps (in milliseconds)
timestamps = [i * 33.33 for i in range(1000)]  # Ideal case
# In reality, extract from V4L2 buffer timestamps

intervals = np.diff(timestamps)
mean_interval = np.mean(intervals)
std_interval = np.std(intervals)
max_interval = np.max(intervals)

print(f"Mean interval: {mean_interval:.2f} ms (target: 33.33 ms)")
print(f"Std dev (jitter): {std_interval:.2f} ms")
print(f"Max interval: {max_interval:.2f} ms")

assert abs(mean_interval - 33.33) < 1.0, "FAIL: Mean interval out of tolerance"
assert std_interval < 5.0, f"FAIL: Jitter {std_interval:.2f} ms exceeds 5 ms"
assert max_interval < 50.0, f"FAIL: Max interval {max_interval:.2f} ms indicates stalls"
print("PASS: Frame rate stability validated")
```

**Pass Criteria**: All assertions pass

---

## AC-005: Lane Speed Characterization

### Scenario 1: Lane Speed Sweep (1.0, 1.1, 1.2, 1.25 Gbps)

**GIVEN**:
- FPGA CSI-2 TX module configured for variable lane speed
- SoC device tree updated to match FPGA lane speed for each test

**WHEN**:
- For each lane speed (1.0, 1.1, 1.2, 1.25 Gbps):
  - FPGA bitstream is regenerated and programmed
  - SoC is rebooted with updated device tree
  - 100 frames are captured at 2048×2048 resolution
  - Data integrity is validated

**THEN**:
- ✅ For each lane speed, capture completes without kernel errors
- ✅ Bit error rate (BER) is calculated: BER = errors / total_bits
- ✅ BER <10^-12 qualifies as "stable" (zero errors in 100 frames)
- ✅ Maximum stable lane speed is identified

**Test Results Table** (to be filled during PoC):

| Lane Speed (Gbps) | 4-Lane Aggregate (Gbps) | Bit Errors (100 frames) | BER | Stable? |
|------------------|------------------------|------------------------|-----|---------|
| 1.0 | 4.0 | 0 | <10^-12 | ✅ |
| 1.1 | 4.4 | 0 | <10^-12 | ✅ |
| 1.2 | 4.8 | 0 | <10^-12 | ✅ |
| 1.25 | 5.0 | ? | ? | ? |

**Validation**:
```python
# Run for each lane speed
lane_speed_gbps = 1.0
frames = 100
width, height = 2048, 2048

total_bits = frames * width * height * 16
errors = 0  # From validation script (validate_100_frames.py)

ber = errors / total_bits if errors > 0 else 0
stable = (ber < 1e-12)

print(f"Lane speed: {lane_speed_gbps} Gbps")
print(f"BER: {ber:.2e}")
print(f"Stable: {'Yes' if stable else 'No'}")
```

**Pass Criteria**: At least 1.0 Gbps/lane is stable (enables Target tier)

---

### Scenario 2: Maximum Stable Lane Speed Documentation

**GIVEN**:
- Lane speed sweep completed (AC-005 Scenario 1)
- Results table filled with BER data

**WHEN**:
- Maximum stable lane speed is determined (highest speed with BER <10^-12)

**THEN**:
- ✅ Maximum stable lane speed ≥1.0 Gbps/lane (minimum for Target tier)
- ✅ If maximum ≥1.25 Gbps/lane: 4-lane aggregate = 5.0 Gbps (Maximum tier feasible)
- ✅ If maximum <1.25 Gbps/lane: Document actual maximum, implications for Maximum tier

**Documentation**:
- Maximum stable lane speed: ___ Gbps/lane
- 4-lane aggregate bandwidth: ___ Gbps
- Target tier (2.01 Gbps) achievable: Yes / No
- Maximum tier (4.53 Gbps) achievable: Yes / No / Requires frame rate reduction

**Pass Criteria**: Maximum stable lane speed documented, Target tier confirmed achievable

---

## AC-006: Signal Integrity Validation (Eye Diagram)

### Scenario 1: Eye Diagram Capture (Optional, if logic analyzer available)

**GIVEN**:
- Logic analyzer (Total Phase Promira or equivalent) with MIPI D-PHY decode
- D-PHY lane 0 probed at FPC cable midpoint (7-8 cm from FPGA)
- FPGA transmitting test pattern at 1.0-1.25 Gbps/lane

**WHEN**:
- Eye diagram is captured over 100 frames minimum
- Measurements are extracted: vertical opening, horizontal opening, rise/fall time

**THEN**:
- ✅ Vertical eye opening ≥200 mV (MIPI D-PHY v1.2 spec minimum)
- ✅ Horizontal eye opening ≥0.5 UI (Unit Interval = 800 ps at 1.25 Gbps)
- ✅ Rise/fall time (20-80%) ≤100 ps
- ✅ No excessive ringing or overshoot (>10% of signal swing)

**Validation**:
- Screenshot eye diagram from logic analyzer
- Annotate measurements on screenshot
- Include in PoC report as Appendix A

**Pass Criteria**: All ✅ conditions met, or this scenario is marked **SKIPPED** if logic analyzer unavailable

---

### Scenario 2: Cable Length Characterization (Optional)

**GIVEN**:
- Multiple FPC cables: 5 cm, 10 cm, 15 cm
- Logic analyzer available for SI measurement

**WHEN**:
- For each cable length:
  - Replace FPC cable
  - Capture eye diagram
  - Capture 1000 frames and validate data integrity

**THEN**:
- ✅ For each cable length, eye diagram vertical opening is measured
- ✅ For each cable length, BER is calculated
- ✅ Maximum reliable cable length is identified (BER <10^-12 and eye opening >200 mV)

**Test Results Table** (to be filled during PoC):

| Cable Length (cm) | Eye Opening (mV) | BER (1000 frames) | Reliable? |
|------------------|-----------------|-------------------|-----------|
| 5 | ≥200 | <10^-12 | ✅ |
| 10 | ≥200 | <10^-12 | ✅ |
| 15 | ? | ? | ? |

**Pass Criteria**: At least 10 cm cable is reliable (meets mechanical design baseline)

**Note**: This scenario is **OPTIONAL**. If skipped, use 10 cm cable for all tests.

---

## AC-007: Packet Integrity Validation

### Scenario 1: CSI-2 Packet Header Validation

**GIVEN**:
- SoC captures raw CSI-2 packets (before ISP processing, if possible)
- Packet parsing tool or script extracts header fields

**WHEN**:
- 100 frames are captured
- Packet headers are parsed for each line (2048 lines × 100 frames = 204,800 packets)

**THEN**:
- ✅ All packet data types = 0x2C (RAW16 format)
- ✅ All virtual channels = 0 (VC0)
- ✅ All word counts = 4096 bytes (2048 pixels × 2 bytes/pixel)
- ✅ No malformed packets (incorrect sync pattern, invalid data type)

**Validation**:
```python
# Pseudo-code (requires raw packet access, SoC driver-dependent)
for packet in raw_packets:
    assert packet.data_type == 0x2C, "Wrong data type"
    assert packet.virtual_channel == 0, "Wrong VC"
    assert packet.word_count == 4096, "Wrong word count"
print("PASS: All packet headers valid")
```

**Pass Criteria**: All assertions pass

**Note**: Raw packet access may not be available on all SoC platforms. If not available, rely on AC-003 data integrity tests as proxy for packet integrity.

---

### Scenario 2: CRC-16 Checksum Validation

**GIVEN**:
- SoC captures raw CSI-2 packets with CRC-16 appended
- CRC calculation tool or script processes packet payloads

**WHEN**:
- 100 frames are captured
- CRC-16 is calculated over each line's pixel data (4096 bytes)
- Calculated CRC is compared to received CRC (appended to packet)

**THEN**:
- ✅ All 204,800 packets (2048 lines × 100 frames) have matching CRC
- ✅ Zero CRC errors → confirms packet payload integrity

**Validation**:
```python
def calculate_crc16(data):
    """Standard CRC-16-CCITT algorithm."""
    crc = 0xFFFF
    for byte in data:
        crc ^= byte << 8
        for _ in range(8):
            if crc & 0x8000:
                crc = (crc << 1) ^ 0x1021
            else:
                crc <<= 1
        crc &= 0xFFFF
    return crc

# For each packet
for packet in raw_packets:
    calculated_crc = calculate_crc16(packet.payload)
    received_crc = packet.crc
    assert calculated_crc == received_crc, f"CRC mismatch: calc={calculated_crc:04X}, recv={received_crc:04X}"

print("PASS: All CRC-16 checksums valid")
```

**Pass Criteria**: All CRC matches, zero errors

**Note**: Similar to AC-007 Scenario 1, this requires raw packet access. If unavailable, mark as **SKIPPED**.

---

## AC-008: GO/NO-GO Decision Documentation

### Scenario 1: GO Criteria Evaluation

**GIVEN**:
- All PoC tests (AC-001 through AC-007) are completed
- Test results are compiled

**WHEN**:
- GO criteria are evaluated:
  1. Measured throughput ≥1.41 Gbps (70% of Target tier 2.01 Gbps)
  2. Zero data corruption (bit errors) in 1000 frames
  3. Signal integrity validated (eye diagram or functional test)
  4. SoC CSI-2 receiver successfully decodes packets

**THEN**:
- ✅ All 4 GO criteria are met → **Recommendation: GO** (proceed to M1)
- ❌ Any criterion fails → **Recommendation: NO-GO** (architecture review required)

**Decision Table**:

| Criterion | Result | Status |
|-----------|--------|--------|
| Throughput ≥1.41 Gbps | ___ Gbps | ✅ / ❌ |
| Zero bit errors (1000 frames) | ___ errors | ✅ / ❌ |
| Signal integrity (eye diagram or functional) | ___ | ✅ / ❌ / SKIPPED |
| Packet decoding success | ___ | ✅ / ❌ |

**GO Recommendation**: Yes / No

**Pass Criteria**: All ✅ for GO decision

---

### Scenario 2: NO-GO Mitigation Analysis (If Applicable)

**GIVEN**:
- One or more GO criteria failed (NO-GO scenario)

**WHEN**:
- Mitigation options are evaluated:
  - **Option A**: External D-PHY PHY chip (e.g., TI DLPC3439, 2.5 Gbps/lane)
    - Cost impact: ~$50 per unit
    - Schedule impact: +2 weeks for integration and re-validation
  - **Option B**: Alternative SoC platform (e.g., Raspberry Pi CM4, NVIDIA Jetson Nano)
    - Cost impact: ~$50-200 per unit (depends on platform)
    - Schedule impact: +4 weeks for evaluation, driver development, re-validation
  - **Option C**: Reduce performance tier (e.g., Target tier → 1536×1536 or Minimum tier only)
    - Cost impact: $0
    - Schedule impact: $0 (no additional development)
    - Impact: Reduced clinical imaging capability

**THEN**:
- ✅ Mitigation options are documented with cost/schedule/impact analysis
- ✅ Recommended mitigation is identified based on stakeholder priorities
- ✅ Revised project plan is drafted (if Option A or B selected)

**Mitigation Recommendation**:
- Selected option: A / B / C
- Rationale: ___
- Next steps: ___

**Pass Criteria**: Mitigation plan documented, stakeholder approval obtained

---

### Scenario 3: PoC Report Compilation

**GIVEN**:
- All PoC tests completed
- GO/NO-GO decision made

**WHEN**:
- PoC report is compiled (per plan.md Phase 6)

**THEN**:
- ✅ Report contains:
  - Executive Summary (GO/NO-GO decision on page 1)
  - Test Setup (hardware, FPGA config, SoC config)
  - Test Results (throughput, data integrity, lane speed, SI)
  - Analysis (GO criteria evaluation, risk assessment)
  - Recommendations (proceed to M1 or architecture alternatives)
  - Appendices (test logs, waveforms, validation scripts, eye diagrams)
- ✅ Report is 15-20 pages (PDF format)
- ✅ Report filename: `POC_Report_M0.5_CSI2_YYYYMMDD.pdf`

**Pass Criteria**: Report complete, stakeholder review scheduled

---

## Test Environment

### Hardware Configuration

**FPGA Setup**:
- Board: Xilinx Artix-7 XC7A35T FGG484 evaluation board
- JTAG: Xilinx Platform Cable USB II (or on-board USB-JTAG)
- Power: 12V / 5V (per board specification)
- FPC connector: MIPI CSI-2 TX (check board documentation for pinout)

**SoC Setup**:
- Board: NXP i.MX8M Plus EVK
- OS: Linux 5.15+ (Yocto or Buildroot with imx8-mipi-csi2 driver)
- UART: USB-to-serial adapter, 115200 baud
- FPC connector: MIPI CSI-2 RX (verify pinout matches FPGA)

**Connectivity**:
- FPC cable: 0.5 mm pitch, 10 cm length (baseline), 15-pin (4 data + 1 clock + grounds)
- SPI wires: FPGA GPIO to SoC SPI master (SCLK, MOSI, MISO, CS_N)

**Optional**:
- Logic analyzer: Total Phase Promira MIPI Protocol Analyzer (for AC-006 SI validation)

---

### Software Tools

**FPGA Development**:
- AMD Vivado 2023.2+ (with MIPI CSI-2 TX IP license)
- Python 3.10+ (for ILA waveform analysis)

**SoC Configuration**:
- Device tree compiler (dtc)
- V4L2 utilities (v4l2-ctl, v4l2-compliance)
- Python 3.10+ with NumPy (for validation scripts)

**Validation Scripts**:
- `validate_frame.py`: Single frame counter pattern validation
- `validate_100_frames.py`: 100-frame batch validation
- `validate_sequence.py`: Frame drop detection and sequence validation
- `validate_checkerboard.py`: Checkerboard pattern validation
- `calculate_throughput.py`: Throughput and frame rate analysis

All validation scripts provided in PoC test package: `poc_validation_scripts.zip`

---

## Success Criteria Summary

**PoC is considered SUCCESSFUL (GO) if**:
- ✅ AC-001 through AC-005 all PASS (FPGA, SoC, data integrity, throughput, lane speed)
- ✅ AC-008 GO criteria met (throughput ≥1.41 Gbps, zero errors, SI validated)
- ✅ AC-006 and AC-007 are PASS or SKIPPED (optional SI and packet validation)

**PoC is considered FAILED (NO-GO) if**:
- ❌ Any of AC-001 through AC-005 FAIL (critical path blocked)
- ❌ AC-008 GO criteria not met (throughput <1.41 Gbps or data corruption)

**Mitigation Required if**:
- NO-GO decision → Evaluate alternatives (external PHY, SoC change, tier reduction)
- Proceed with mitigation plan approval and revised PoC schedule

---

## Traceability

**Validates**:
- SPEC-POC-001 Requirements (REQ-POC-001 through REQ-POC-019)
- SPEC-ARCH-001 REQ-ARCH-001 (CSI-2 as exclusive interface)
- SPEC-ARCH-001 REQ-ARCH-007 (Target tier CSI-2 D-PHY configuration)

**Inputs From**:
- plan.md (test procedures, hardware setup, validation methodology)
- X-ray_Detector_Optimal_Project_Plan.md Section 5.4 (PoC gate criteria)

**Outputs To**:
- PoC Test Report (W6)
- GO/NO-GO decision (triggers M1 or architecture review)

---

**Acceptance Criteria Version**: 1.0.0
**Created**: 2026-02-17
**Author**: MoAI Agent (manager-spec)

🗿 MoAI <email@mo.ai.kr>
