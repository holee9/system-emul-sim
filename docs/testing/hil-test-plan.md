# Hardware-in-the-Loop (HIL) Test Plan

**Project**: X-ray Detector Panel System
**Document Version**: 2.1.0
**Last Updated**: 2026-02-17
**Status**: Reviewed - Approved

---

## Overview

This document defines HIL test procedures using actual FPGA and SoC hardware boards. HIL testing validates the complete hardware stack that software simulation cannot fully replicate. HIL tests are executed during Phase 3 (W23-W28) after simulator-based integration tests (IT-01 through IT-10) have passed.

**Hardware Required**:
- Xilinx Artix-7 XC7A35T-FGG484 evaluation board
- NXP i.MX8M Plus EVK (Variscite VAR-SOM-MX8M-PLUS DART)
- FPC cable: 10cm, 15-pin, 4-lane D-PHY, 100 ohm differential impedance
- 10 GbE network connection (Host PC to SoC)
- USB-to-UART debug cable (for SoC console, 115200 8N1)
- Bench power supply: 5V/3A (FPGA board), 12V/2A (SoC board)
- Ambient temperature monitoring (digital thermometer)

**Reference SPECs**: SPEC-FPGA-001, SPEC-FW-001, SPEC-SDK-001, SPEC-SIM-001

**Development Methodology**: DDD (ANALYZE-PRESERVE-IMPROVE) for hardware characterization tests

---

## Hardware Setup

### Physical Connections

```
+-------------------+     FPC Cable (CSI-2)     +-------------------+
|  Artix-7 XC7A35T  |<========================>|  i.MX8M Plus EVK  |
|  (FPGA Board)     |  4-lane D-PHY, 100 ohm   |  (SoC Board)      |
|                   |                           |                   |
|  SPI: MOSI/MISO/  |<--- SPI (50 MHz) ------->|  spidev0.0        |
|       SCLK/CS_N   |                           |                   |
+-------------------+                           +-------------------+
      |  5V/3A                                        |  12V/2A
      |  JTAG (Vivado)                               |  UART (115200)
      |                                               |  10 GbE
      v                                               v
  [Bench PSU]                                    [Host PC]
  [Vivado HW Manager]                           [detector_cli]
                                                 [Host SDK]
```

### Connection Checklist

| Step | Connection | Verification |
|------|-----------|--------------|
| 1 | FPGA board power (5V/3A) | Power LED on, DONE LED off (awaiting config) |
| 2 | JTAG cable (FPGA to Host PC) | Vivado HW Manager detects XC7A35T |
| 3 | FPC cable (FPGA CSI-2 TX to SoC CSI-2 RX) | Firm seating, latch closed, no bend stress |
| 4 | SPI wires (MOSI, MISO, SCLK, CS_N, GND) | Continuity check with multimeter |
| 5 | SoC board power (12V/2A) | Power LED on, boot LED sequence |
| 6 | UART cable (SoC to Host PC) | Terminal shows boot messages |
| 7 | 10 GbE cable (SoC to Host PC or switch) | Link LED on both ends |
| 8 | Ambient temperature sensor | Baseline reading recorded |

### Software Prerequisites

**FPGA Board**:
- Bitstream programmed via Vivado HW Manager
- Expected bitstream: `xray_detector_top.bit` (matching SPEC-FPGA-001 RTL)

**SoC Board**:
- Yocto Scarthgap image booted: `core-image-detector-imx8mp-var-dart.wic`
- Kernel: Linux 6.6.52 (Variscite BSP imx-6.6.52-2.2.0-v1.3)
- `detector_daemon` installed and configured
- `detector_cli` available in PATH
- V4L2 tools installed (`v4l2-ctl`, `media-ctl`)

**Host PC**:
- Host SDK installed (.NET 8.0 runtime)
- `detector_host` application configured with SoC IP address
- 10 GbE NIC configured (MTU 9000 for jumbo frames recommended)
- Test results directory created: `results/hil/`

---

## Pre-Test Checklist

Before executing any HIL test pattern, verify the following:

| # | Check | Command / Action | Expected Result |
|---|-------|-----------------|----------------|
| 1 | FPGA bitstream loaded | Vivado: `program_device` | DONE LED on |
| 2 | SoC booted | UART terminal | Login prompt, `uname -r` = 6.6.52 |
| 3 | SPI communication | `detector_cli read-reg 0x00` | Returns 0xA735 (DEVICE_ID, Artix-7 35T) |
| 4 | CSI-2 PHY ready | `detector_cli read-reg 0x90` | Bit 0 = 1 (csi2_link_up) |
| 5 | V4L2 device present | `v4l2-ctl --list-devices` | `/dev/video0` listed |
| 6 | Network link up | `ip link show eth1` | State: UP, speed 10000Mb/s |
| 7 | Host SDK connected | `detector_host --status` | Connected to SoC IP |
| 8 | No prior errors | `detector_cli read-reg 0x80` | ERROR_FLAGS = 0x0000 |
| 9 | Ambient temperature | Digital thermometer | Record baseline (typical: 20-25C) |
| 10 | Disk space | `df -h /data` on Host PC | >= 50 GB free for frame storage |

**If any check fails**: Do not proceed. Resolve the issue using the Troubleshooting Guide (Section at end of document) before continuing.

---

## HIL Test Patterns

### Pattern A: Basic Connectivity (HIL-A)

**Purpose**: Verify physical hardware connections before functional testing.
**Prerequisites**: Hardware setup complete, all connections verified.
**Schedule**: W23-24 (first hardware bring-up)

---

#### HIL-A-01: Power and FPGA Configuration

**Procedure**:
1. Power on FPGA board (5V/3A bench supply)
2. Verify DONE LED illuminates within 2 seconds (bitstream loaded from flash or JTAG)
3. Connect SoC UART (115200 baud, 8N1)
4. Power on SoC board (12V/2A)
5. Verify Linux boot messages appear within 30 seconds
6. Login and verify kernel: `uname -r` (expected: 6.6.52)
7. Verify Yocto version: `cat /etc/os-release` (expected: Scarthgap 5.0)

**Pass Criteria**:
- DONE LED: illuminates within 2s of power-on
- Linux boot: completes within 30s
- Kernel version: 6.6.52
- OS: Yocto Scarthgap 5.0

**Fail Action**: Check JTAG cable, reprogram bitstream. Check SoC SD card image.

---

#### HIL-A-02: SPI Communication Verification

**Procedure**:
1. Run detector_daemon in test mode: `detector_daemon --test-mode`
2. Read DEVICE_ID registers via detector_cli
3. Write and read-back a test value to a writable register

```bash
# Step 2: Read DEVICE_ID (single 16-bit register)
detector_cli read-reg 0x00  # Expected: 0xA735 (Artix-7 35T identifier)

# Step 3: Write-read-back test on Panel Configuration registers
detector_cli write-reg 0x50 0x0400  # PANEL_ROWS = 1024
detector_cli read-reg 0x50          # Expected: 0x0400
detector_cli write-reg 0x50 0x0800  # PANEL_ROWS = 2048
detector_cli read-reg 0x50          # Expected: 0x0800

# Step 4: Verify STATUS register readability
detector_cli read-reg 0x20          # STATUS: bit 0 = 1 (idle)
```

**Pass Criteria**:
- Register 0x00 = 0xA735 (DEVICE_ID, Artix-7 35T)
- Write-read-back: values match for all tested registers (0x50, 0x51)
- STATUS register (0x20) readable, bit 0 = 1 (idle state)
- Read latency < 1ms per register

**Fail Action**: Check SPI wiring (MOSI/MISO not swapped), verify CPOL=0/CPHA=0, check CS_N signal.

---

#### HIL-A-03: CSI-2 Link Establishment

**Procedure**:
1. Configure CSI-2 for 400 Mbps/lane, 4-lane mode
2. Enable V4L2 streaming pipeline
3. Verify link status via register and V4L2 tools

```bash
# Step 1: Configure 400 Mbps/lane mode
detector_cli write-reg 0x60 0x000E  # CSI2_LANE_COUNT: 4-lane (10), tx_enable (1), continuous_clk (1)
detector_cli write-reg 0x61 0x0064  # CSI2_LANE_SPEED = 0x64 (1.0 Gbps/lane = 400 Mbps effective)

# Step 2: Enable V4L2
media-ctl -d /dev/media0 --set-v4l2 '"imx8mp-mipi-csi2":0[fmt:SRGGB16_1X16/2048x2048]'
v4l2-ctl -d /dev/video0 --set-fmt-video=width=2048,height=2048,pixelformat=RG16

# Step 3: Verify
detector_cli read-reg 0x90  # DATA_IF_STATUS: bit 0 (csi2_link_up) = 1
v4l2-ctl --list-devices     # Should show /dev/video0
dmesg | grep -i csi          # No errors
```

**Pass Criteria**:
- DATA_IF_STATUS register 0x90 bit 0 = 1 (csi2_link_up)
- `/dev/video0` device present and accessible
- No kernel errors in dmesg related to CSI-2 or MIPI
- D-PHY lane lock achieved within 1 second

**Fail Action**: Check FPC cable seating, verify lane ordering, check D-PHY voltage levels.

---

#### HIL-A-04: Peripheral Verification - WiFi/BT (Ezurio Sterling 60)

**Procedure**:
1. Verify WiFi module detection
2. Scan for available networks
3. Verify Bluetooth detection

```bash
# WiFi verification
lspci | grep -i qca          # QCA6174A on M.2 PCIe
ip link show wlan0            # Interface present
iw dev wlan0 scan | head -20  # Scan for networks

# Bluetooth verification
hciconfig hci0 up             # Enable BT adapter
hciconfig hci0                # Show BT status
```

**Pass Criteria**:
- QCA6174A detected on PCIe bus (ath10k_pci driver loaded)
- wlan0 interface present and scannable
- Bluetooth adapter (hci0) responds to hciconfig

**Fail Action**: Check M.2 module seating, verify ath10k firmware files present in `/lib/firmware/ath10k/`.

---

#### HIL-A-05: Peripheral Verification - IMU (Bosch BMI160)

**Procedure**:
1. Verify I2C device detection at address 0x68
2. Read chip ID register
3. Read accelerometer data

```bash
# I2C detection
i2cdetect -y 6               # Scan I2C bus 7 (0-indexed = 6)
# Expected: address 0x68 shows "68"

# Read chip ID (register 0x00)
i2cget -y 6 0x68 0x00        # Expected: 0xD1 (BMI160 chip ID)

# Read accelerometer (IIO framework)
cat /sys/bus/iio/devices/iio:device0/in_accel_x_raw
cat /sys/bus/iio/devices/iio:device0/in_accel_y_raw
cat /sys/bus/iio/devices/iio:device0/in_accel_z_raw
```

**Pass Criteria**:
- I2C address 0x68 responds on bus 7
- Chip ID = 0xD1 (BMI160)
- Accelerometer raw values change when board is tilted
- bmi160_i2c driver loaded (IIO subsystem)

**Fail Action**: Check I2C pull-ups, verify device tree overlay for I2C7.

---

#### HIL-A-06: Peripheral Verification - GPIO Expander (NXP PCA9534)

**Procedure**:
1. Verify I2C device detection
2. Read and write GPIO pins

```bash
# I2C detection
i2cdetect -y 2               # Scan I2C bus for PCA9534
# Expected: PCA9534 address responds

# GPIO framework verification
gpiodetect                    # List GPIO controllers
gpioinfo gpiochip_pca9534     # Show PCA9534 pin states
gpioset gpiochip_pca9534 0=1  # Set pin 0 high
gpioget gpiochip_pca9534 0    # Read back: expected 1
```

**Pass Criteria**:
- PCA9534 detected on I2C bus (gpio-pca953x driver loaded)
- GPIO pins readable and writable
- Pin state changes reflected in hardware (verify with oscilloscope or LED)

**Fail Action**: Check I2C address configuration (A0-A2 pins), verify device tree.

---

#### HIL-A-07: Peripheral Verification - 2.5GbE Network

**Procedure**:
1. Identify 2.5GbE chip model
2. Verify driver loaded and link up
3. Test basic connectivity

```bash
# Step 1: Identify chip
lspci -nn | grep -i ethernet  # Record vendor:device ID

# Step 2: Verify link
ip link show eth1              # 2.5GbE interface (eth0 = 1GbE on-SoM)
ethtool eth1                   # Speed: 2500Mb/s, Link detected: yes

# Step 3: Connectivity
ping -c 5 -I eth1 <host_ip>   # Ping Host PC
iperf3 -c <host_ip> -B <soc_eth1_ip> -t 10  # Throughput test
```

**Pass Criteria**:
- 2.5GbE chip vendor:device ID identified and documented
- Kernel driver loaded (check dmesg for driver name)
- Link speed: 2500 Mb/s (or 10000 Mb/s if 10GbE)
- iperf3 throughput: >= 2.0 Gbps sustained
- Ping latency: < 1 ms on local network

**Fail Action**: If chip unidentified, record lspci output for driver investigation. Check cable category (Cat6A minimum).

---

#### HIL-A-08: Peripheral Verification - Battery Gauge (TI BQ40z50)

**Procedure**:
1. Verify SMBus device detection at address 0x0b
2. Read battery status registers

```bash
# SMBus detection
i2cdetect -y 1               # Scan SMBus
# Expected: address 0x0b shows "0b"

# Read battery voltage (if battery connected)
cat /sys/class/power_supply/bq27xxx-0/voltage_now  # Microvolts

# Read battery status
cat /sys/class/power_supply/bq27xxx-0/status        # Charging/Discharging/Full
```

**Pass Criteria**:
- BQ40z50 responds at SMBus address 0x0b
- bq27xxx_battery driver loaded (ported from kernel 4.4 to 6.6)
- Battery voltage readable (if battery connected)
- No kernel oops or driver errors in dmesg

**Fail Action**: Verify BQ40z50 driver port complete. Check SMBus pull-ups and address configuration.

**Note**: This test requires the BQ40z50 driver port (REQ-ARCH-022). If port not yet complete, mark as SKIP with justification.

---

### Pattern B: Performance Validation (HIL-B)

**Purpose**: Validate data acquisition performance at each tier with real hardware.
**Prerequisites**: All HIL-A tests passed.
**Schedule**: W25-W27

---

#### HIL-B-01: Single Frame Acquisition

**Procedure**:
1. Load counter test pattern into FPGA via SPI
2. Configure for Minimum tier (1024x1024, 14-bit)
3. Trigger single-shot scan from Host SDK
4. Capture frame and verify pixel data integrity

```bash
# Step 1: Configure test pattern
detector_cli write-reg 0x40 0x0001  # TEST_PATTERN = counter mode

# Step 2: Configure Minimum tier (Panel Configuration registers 0x50-0x52)
detector_cli write-reg 0x50 0x0400  # PANEL_ROWS = 1024
detector_cli write-reg 0x51 0x0400  # PANEL_COLS = 1024
detector_cli write-reg 0x52 0x000E  # BIT_DEPTH = 14

# Step 3: Trigger single-shot from Host
detector_host --single-shot --output results/hil/HIL-B-01-frame.raw

# Step 4: Verify
detector_host --verify-pattern counter --file results/hil/HIL-B-01-frame.raw
```

**Pass Criteria**:
- Frame received within 200ms of trigger
- Pixel values match counter pattern: pixel[r][c] = (r * 1024 + c) % 2^14
- FrameHeader magic: 0xD7E01234
- No CRC errors in ERROR_FLAGS (register 0x80 = 0x0000)
- Frame dimensions: exactly 1024x1024

**Fail Action**: Compare with FpgaSimulator golden reference output. Check CSI-2 packet captures.

---

#### HIL-B-02: 400M Continuous Acquisition (Intermediate-A)

**Procedure**:
1. Configure: 2048x2048, 16-bit, 15fps, 400M lane speed
2. Acquire 1000 frames continuously
3. Verify throughput, integrity, and stability

```bash
# Configure
detector_cli write-reg 0x61 0x0064  # CSI2_LANE_SPEED = 0x64 (1.0 Gbps/lane = 400M effective)
detector_host --continuous --frames 1000 --tier intermediate-a \
  --output results/hil/HIL-B-02/ \
  --verify-checksum \
  --report results/hil/HIL-B-02-report.json
```

**Pass Criteria**:
- Actual FPS: 15fps +/- 5% (14.25-15.75 fps)
- Frame drop rate < 0.01% (max 0.1 drops per 1000 frames)
- CSI-2 bandwidth utilization: ~63% of 1.6 Gbps (1.01 / 1.6)
- All frames pixel-accurate (CRC checksum verification)
- ERROR_FLAGS = 0x0000 throughout acquisition
- SoC CPU usage < 60% during acquisition

**Fail Action**: Check D-PHY signal integrity. Reduce to Minimum tier to isolate issue.

---

#### HIL-B-03: 800M Link Initialization

**Procedure**:
1. Switch to 800M lane speed via SPI
2. Power cycle FPGA to apply new lane speed
3. Verify 800M D-PHY link establishment
4. Verify link stability for 60 seconds

```bash
# Step 1: Configure 800M
detector_cli write-reg 0x61 0x007D  # CSI2_LANE_SPEED = 0x7D (1.25 Gbps/lane = 800M effective)

# Step 2: Soft reset via CONTROL register (0x21) bit[2]
detector_cli write-reg 0x21 0x0004  # CONTROL: soft_reset bit[2]

# Step 3: Wait for link and verify
sleep 5
detector_cli read-reg 0x61  # Should read back 0x7D (800M)
detector_cli read-reg 0x90  # DATA_IF_STATUS: bit 0 = 1 (csi2_link_up)

# Step 4: Stability check (60 seconds)
for i in $(seq 1 12); do
  sleep 5
  detector_cli read-reg 0x90  # DATA_IF_STATUS: csi2_link_up must remain 1
  detector_cli read-reg 0x80  # ERROR_FLAGS must remain 0x0000
done
```

**Pass Criteria**:
- CSI2_LANE_SPEED (0x61) reads back 0x7D (800M = 1.25 Gbps/lane)
- DATA_IF_STATUS (0x90) bit 0 = 1 (csi2_link_up)
- Link established within 5 seconds of reset
- Link remains stable for 60 seconds (no drops, no errors)
- No D-PHY errors (ERROR_FLAGS bit 5 = 0)

**Note**: If 800M debugging is not yet complete, this test may fail. Mark as CONDITIONAL and record failure details for debugging team.

**Fail Action**: Record D-PHY error counters. Try reduced lane count (2-lane) at 800M. Check signal integrity with oscilloscope.

---

#### HIL-B-04: 800M Continuous Acquisition (Final Target Tier)

*Prerequisite: HIL-B-03 passed*

**Procedure**:
1. Configure: 3072x3072, 16-bit, 15fps, 800M lane speed
2. Acquire 100 frames continuously
3. Verify throughput and data integrity

```bash
detector_host --continuous --frames 100 --tier target \
  --output results/hil/HIL-B-04/ \
  --verify-checksum \
  --report results/hil/HIL-B-04-report.json
```

**Pass Criteria**:
- Actual FPS: 15fps +/- 5% (14.25-15.75 fps)
- Frame drop rate < 0.1% (validation threshold; < 0.01% is production target)
- CSI-2 bandwidth: ~71% of 3.2 Gbps (2.26 / 3.2 Gbps)
- CRC error rate: < 0.001% of packets
- All received frames bit-accurate (counter pattern match)

**Note**: 800M is under active debugging. Production target is < 0.01% drop rate. During validation, < 0.1% is acceptable.

**Fail Action**: Reduce to 2048x2048@15fps at 800M to isolate bandwidth vs stability issue. Compare error patterns with 400M results.

---

#### HIL-B-05: 1-Hour Continuous Acquisition (Stability Test)

*Prerequisite: HIL-B-02 passed*

**Procedure**:
1. Configure: 2048x2048, 16-bit, 15fps (400M, stable tier)
2. Run continuous acquisition for 3,600 seconds (1 hour)
3. Monitor system metrics every 5 minutes throughout

**Monitoring Script**:
```bash
# Run in background, log every 5 minutes
while true; do
  echo "$(date -Iseconds)"
  detector_cli read-reg 0x30  # FRAME_COUNT_HI
  detector_cli read-reg 0x31  # FRAME_COUNT_LO
  detector_cli read-reg 0x80  # ERROR_FLAGS
  free -m                      # SoC memory
  cat /sys/class/thermal/thermal_zone0/temp  # SoC temperature
  sleep 300
done > results/hil/HIL-B-05-monitor.log &

# Start acquisition
detector_host --continuous --duration 3600 --tier intermediate-a \
  --output results/hil/HIL-B-05/ \
  --report results/hil/HIL-B-05-report.json
```

**Pass Criteria**:
- Total frames: 54,000 +/- 270 (15fps x 3600s +/- 0.5%)
- Drop rate: < 0.01% (< 5.4 frames)
- ERROR_FLAGS: 0x0000 for >= 99.9% of duration
- SoC memory: RSS growth < 100MB over 1 hour (no memory leak)
- FPGA board temperature: < ambient + 15 degrees C
- SoC CPU usage: < 60% average over 1 hour
- No kernel oops, no daemon crashes

**Fail Action**: Analyze monitor log for correlation between temperature/memory and errors. Check for memory leaks with valgrind on shorter run.

---

#### HIL-B-06: Golden Reference Comparison (Simulator vs Hardware)

*Prerequisite: HIL-B-01 passed, FpgaSimulator golden reference files available*

**Purpose**: Validate that real FPGA hardware output matches FpgaSimulator golden reference (REQ-SIM-026).

**Procedure**:
1. Configure FPGA for counter pattern at Minimum tier (1024x1024, 14-bit)
2. Capture 10 frames from real hardware, save as RAW files
3. Generate 10 frames from FpgaSimulator with identical configuration
4. Run bit-exact comparison using rtl_vs_sim_checker

```bash
# Step 1-2: Capture from hardware
detector_host --single-shot --frames 10 --pattern counter \
  --tier minimum --output results/hil/HIL-B-06-hw/

# Step 3: Generate simulator reference
dotnet run --project tools/IntegrationRunner -- \
  --scenario golden-ref --frames 10 --pattern counter \
  --tier minimum --output results/hil/HIL-B-06-sim/

# Step 4: Compare
dotnet run --project tools/RtlVsSimChecker -- \
  --hw-dir results/hil/HIL-B-06-hw/ \
  --sim-dir results/hil/HIL-B-06-sim/ \
  --report results/hil/HIL-B-06-comparison.json
```

**Pass Criteria**:
- CSI-2 packet headers (Data Type, VC, Word Count) match bit-accurately
- All pixel payload bytes match bit-accurately (tolerance = 0)
- CRC-16 values match bit-accurately
- FSM state transition sequences identical (log comparison)
- On mismatch: report first mismatch location, byte offset, and values

**Fail Action**: If mismatch found, investigate both FpgaSimulator and RTL. Check for endianness issues, bit-packing differences, or CRC polynomial mismatch.

---

#### HIL-B-07: 10 GbE Network Throughput Validation

*Prerequisite: HIL-A-07 passed*

**Purpose**: Validate that 10 GbE (or 2.5 GbE) link sustains required throughput for each tier.

**Procedure**:
1. Run iperf3 baseline throughput test (raw network capacity)
2. Acquire frames at each tier and measure effective throughput
3. Verify no packet loss at sustained throughput

```bash
# Step 1: Baseline (iperf3)
iperf3 -c <host_ip> -t 30 -P 4 --json > results/hil/HIL-B-07-baseline.json

# Step 2: Tier throughput
detector_host --continuous --frames 500 --tier minimum \
  --measure-throughput --report results/hil/HIL-B-07-min.json

detector_host --continuous --frames 500 --tier intermediate-a \
  --measure-throughput --report results/hil/HIL-B-07-intA.json
```

**Pass Criteria**:

| Tier | Data Rate | Required Net BW | Packet Loss |
|------|-----------|-----------------|-------------|
| Minimum (1024x1024@15fps) | 0.21 Gbps | < 1 Gbps | 0% |
| Intermediate-A (2048x2048@15fps) | 1.01 Gbps | < 2.5 Gbps | 0% |
| Final Target (3072x3072@15fps) | 2.26 Gbps | < 10 Gbps | 0% |

- iperf3 baseline: >= 2.0 Gbps (2.5GbE) or >= 9.0 Gbps (10GbE)
- Frame delivery: zero packet loss at Minimum and Intermediate-A tiers
- UDP receive buffer: no overflows reported (`netstat -su`)

**Fail Action**: Increase UDP receive buffer (`sysctl net.core.rmem_max=26214400`). Enable jumbo frames (MTU 9000). Check NIC offload settings.

---

### Pattern C: Error Recovery (HIL-C)

**Purpose**: Validate error detection, reporting, and recovery with real hardware fault injection.
**Prerequisites**: All HIL-A and HIL-B-01/B-02 tests passed.
**Schedule**: W28

---

#### HIL-C-01: SPI Communication Error Recovery

**Procedure**:
1. Start continuous scan at Intermediate-A tier
2. During active scan, physically disconnect SPI MISO wire for 3 seconds
3. Verify WATCHDOG error detected
4. Reconnect MISO wire
5. Issue soft reset and verify system recovers
6. Resume scan and verify normal operation

**Pass Criteria**:
- WATCHDOG error (ERROR_FLAGS bit 7) detected within 200ms of MISO disconnect
- Error correctly reported to Host via error event
- System recovers after MISO reconnection and soft reset
- Subsequent scans complete normally with no data corruption
- Frame counter resumes from correct value

**Fail Action**: Check watchdog timeout configuration. Verify error reporting path (FPGA -> SoC -> Host).

---

#### HIL-C-02: CSI-2 Cable Disconnect/Reconnect

**Procedure**:
1. Start continuous scan at Intermediate-A tier
2. During active scan, carefully disconnect CSI-2 FPC cable
3. Verify error detection on both FPGA and SoC sides
4. Reconnect cable firmly (latch closed)
5. Verify V4L2 pipeline restarts and acquisition resumes

**Pass Criteria**:
- D-PHY error detected within 500ms on SoC (dmesg error, ERROR_FLAGS bit 5)
- V4L2 pipeline restarts within 5 seconds of reconnection
- Frame acquisition resumes automatically (or after manual restart command)
- No permanent state corruption in FPGA or SoC
- No kernel panic or daemon crash

**Fail Action**: If V4L2 pipeline does not auto-restart, verify CSI-2 RX driver recovery logic. May require manual `media-ctl` reconfiguration.

---

#### HIL-C-03: FPGA Soft Reset Recovery

**Procedure**:
1. Start continuous scan at Intermediate-A tier
2. After 100 frames, issue FPGA soft reset via SPI
3. Verify FPGA re-initializes correctly
4. Restart acquisition and verify normal operation

```bash
# Step 2: Soft reset via CONTROL register (0x21) bit[2]
detector_cli write-reg 0x21 0x0004  # CONTROL: soft_reset bit[2]

# Step 3: Verify re-initialization
sleep 2
detector_cli read-reg 0x00  # DEVICE_ID: 0xA735 (Artix-7 35T, confirms SPI still works)
detector_cli read-reg 0x20  # STATUS: bit[0]=1 (IDLE state)
detector_cli read-reg 0x30  # FRAME_COUNT_HI: 0x0000 (reset)
detector_cli read-reg 0x31  # FRAME_COUNT_LO: 0x0000 (reset)

# Step 4: Resume
detector_host --continuous --frames 100 --tier intermediate-a
```

**Pass Criteria**:
- FPGA completes soft reset within 2 seconds
- All registers return to default values after reset
- FRAME_COUNTER resets to 0
- FSM returns to IDLE state
- Post-reset acquisition produces correct data (no corruption)

**Fail Action**: Check reset sequence in RTL. Verify all registers have proper reset values.

---

#### HIL-C-04: Power Cycle Recovery

**Procedure**:
1. Start continuous scan at Intermediate-A tier
2. After 50 frames, power cycle FPGA board (remove/restore 5V)
3. Wait for FPGA reconfiguration (DONE LED)
4. Verify SoC detects FPGA loss and recovery
5. Re-establish full pipeline and verify normal operation

**Pass Criteria**:
- SoC detects FPGA power loss within 1 second (SPI timeout or CSI-2 loss)
- SoC daemon logs error and enters recovery state
- After FPGA power restore and DONE LED, SoC re-establishes SPI communication
- Full pipeline resumes within 10 seconds of FPGA DONE
- No SoC crash or daemon restart required

**Fail Action**: Verify SoC daemon handles FPGA disappearance gracefully. Check error escalation path.

---

#### HIL-C-05: Network Interruption Recovery

**Procedure**:
1. Start continuous scan streaming to Host PC
2. After 100 frames, disconnect 10 GbE cable for 5 seconds
3. Reconnect cable
4. Verify Host SDK detects gap and recovers

**Pass Criteria**:
- Host SDK detects frame gap (missing frame_seq values)
- SoC continues acquiring frames into ring buffer during network outage
- After reconnection, Host SDK receives frames from current buffer position
- No SoC crash or buffer corruption during outage
- Host SDK reports network interruption event with duration

**Fail Action**: Check SoC ring buffer management during network loss. Verify buffer does not overflow during 5-second outage.

---

## Go/No-Go 800M Decision Criteria

### Purpose

The 800 Mbps/lane D-PHY configuration is required for the Final Target tier (3072x3072@16-bit@15fps, 2.26 Gbps). This section defines the criteria for deciding whether to proceed with 800M for production or fall back to 400M (Intermediate-A tier as maximum).

### Decision Gate: W26 PoC Review

| Criterion | Required Result | Measurement | Gate |
|-----------|----------------|-------------|------|
| HIL-B-03: 800M Link Init | PASS | D-PHY link established within 5s | Must pass |
| HIL-B-03: 60s Stability | PASS | No link drops or D-PHY errors for 60s | Must pass |
| HIL-B-04: 100 Frame Capture | Drop rate < 0.1% | Frame drop count / 100 | Must pass |
| HIL-B-04: Data Integrity | CRC error rate < 0.001% | CRC error count / total packets | Must pass |
| HIL-B-04: Throughput | >= 2.15 Gbps sustained | Measured CSI-2 throughput | Must pass |
| FPGA Temperature | < ambient + 20 C | Thermal sensor during HIL-B-04 | Must pass |

### Decision Matrix

| HIL-B-03 | HIL-B-04 | Decision | Action |
|----------|----------|----------|--------|
| PASS | PASS (all criteria) | **GO** for 800M | Proceed to Target tier validation |
| PASS | PARTIAL (drop rate 0.1-1%) | **CONDITIONAL GO** | Continue debugging, extend timeline |
| PASS | FAIL (drop rate > 1%) | **NO-GO** | Fall back to 400M, Intermediate-A as max tier |
| FAIL | N/A | **NO-GO** | Fall back to 400M, 800M debugging continues offline |

### Fallback Plan (400M Only)

If 800M is NO-GO at W26:

1. **Maximum tier**: Intermediate-A (2048x2048@16-bit@15fps) at 400M
2. **Throughput**: 1.01 Gbps (63% of 1.6 Gbps aggregate)
3. **Impact**: Final Target tier (3072x3072) deferred to future hardware revision
4. **Acceptance**: All IT tests (IT-01 through IT-09) must pass at 400M. IT-10C marked SKIP.
5. **Documentation**: Update project plan, SPEC-ARCH-001, and customer communication

### Signal Integrity Debugging Checklist (if 800M fails)

| Step | Check | Tool | Pass Criterion |
|------|-------|------|---------------|
| 1 | FPC cable impedance | TDR / VNA | 100 ohm +/- 10% differential |
| 2 | Lane-to-lane skew | Oscilloscope | < 50 ps at 800M |
| 3 | Eye diagram at 800M | Oscilloscope with compliance mask | Eye opening > 80 mV, > 0.3 UI |
| 4 | T_HS-PREP timing | ILA capture of D-PHY state | 40-85 ns (per D-PHY v1.2) |
| 5 | T_HS-ZERO timing | ILA capture | >= 105 ns at 800M |
| 6 | MMCM lock status | Vivado ILA or register read | Lock stable for 60s |
| 7 | Reduce to 2-lane 800M | SPI register 0x60 | Eliminates crosstalk hypothesis |
| 8 | Try 700M intermediate | SPI register 0x61 = 0x78 | Isolates frequency sensitivity |

---

## HIL Test Execution Schedule

| Phase | HIL Pattern | When | Hardware Required | Dependency |
|-------|------------|------|------------------|------------|
| W23-24 | HIL-A (all: A-01 to A-08) | First hardware bring-up | FPGA + SoC boards + peripherals | Bitstream + Yocto image ready |
| W25 | HIL-B-01, B-02, B-06, B-07 | After basic connectivity confirmed | + FPC cable + 10 GbE | HIL-A passed |
| W26 (PoC) | HIL-B-03, B-04 | 800M validation | Full setup | HIL-B-02 passed |
| W27 | HIL-B-05 | Stability validation (1 hour) | Full setup + monitoring | HIL-B-02 passed |
| W28 | HIL-C-01 to C-05 | Error recovery validation | Full setup + cable access | HIL-B-01 passed |

---

## Test Report Template

Each HIL test execution produces a report in the following format:

```
=== HIL Test Report ===
Test ID:        HIL-X-YY
Date:           YYYY-MM-DD HH:MM:SS
Operator:       [name]
Environment:
  FPGA Board:   [serial number]
  SoC Board:    [serial number]
  Bitstream:    [version/hash]
  Yocto Image:  [version]
  Ambient Temp: [degrees C]

Result:         PASS / FAIL / SKIP / CONDITIONAL
Duration:       [seconds]

Measurements:
  [metric 1]:   [value] [unit]   [PASS/FAIL vs criteria]
  [metric 2]:   [value] [unit]   [PASS/FAIL vs criteria]

Notes:
  [any observations, anomalies, or conditions]

Attachments:
  - [log file path]
  - [captured frame path]
  - [monitoring data path]
===
```

---

## Troubleshooting Guide

### Common HIL Failures

| Symptom | Likely Cause | Resolution |
|---------|-------------|------------|
| DONE LED not on | Bitstream not loaded | Reprogram via JTAG: `vivado -mode batch -source program.tcl` |
| SPI read returns 0xFFFF | SPI wires disconnected or swapped | Check MOSI/MISO/SCLK/CS_N with oscilloscope |
| CSI2_STATUS phy_ready = 0 | FPC cable not seated | Reseat FPC cable, ensure latch is closed |
| V4L2 device not found | CSI-2 RX driver not loaded | `modprobe imx8mp_csi2` or check device tree |
| Frame data all zeros | Test pattern not configured | Set TEST_PATTERN register (0x40) before scan |
| Frame drops > 0.1% | D-PHY signal integrity | Check FPC cable length, reduce lane speed |
| SoC memory growth | Memory leak in daemon | Profile with valgrind, check frame buffer free |
| 800M link unstable | D-PHY debugging in progress | Document failure, fall back to 400M |
| iperf3 < 2 Gbps | MTU too small or CPU limit | Set MTU 9000, enable NIC hardware offload |
| Kernel oops | Driver bug | Capture full dmesg, report to firmware team |

### Signal Integrity Checklist (for D-PHY issues)

1. FPC cable length: <= 10cm recommended
2. Differential impedance: 100 ohm +/- 10%
3. Lane-to-lane skew: < 100ps
4. Common-mode voltage: 200mV (D-PHY spec)
5. Eye diagram: Verify with oscilloscope at 400M and 800M

---

## Traceability Matrix

| HIL Test | SPEC Reference | Requirement | Integration Test Analog |
|----------|---------------|-------------|------------------------|
| HIL-A-01 | SPEC-ARCH-001 | REQ-ARCH-020 (Scarthgap) | - |
| HIL-A-02 | SPEC-FPGA-001 | REQ-FPGA-040, 042 (SPI slave, register map) | IT-01 (SPI verify) |
| HIL-A-03 | SPEC-FPGA-001 | REQ-FPGA-030, 034 (CSI-2 TX, lane speed) | IT-01 (CSI-2 link) |
| HIL-A-04 | SPEC-ARCH-001 | REQ-ARCH-022 (WiFi QCA6174A) | - |
| HIL-A-05 | SPEC-ARCH-001 | REQ-ARCH-022 (IMU BMI160) | - |
| HIL-A-06 | SPEC-ARCH-001 | REQ-ARCH-022 (GPIO PCA9534) | - |
| HIL-A-07 | SPEC-ARCH-001 | REQ-ARCH-022 (2.5GbE) | IT-10 (bandwidth), IT-07 (network) |
| HIL-A-08 | SPEC-ARCH-001 | REQ-ARCH-022 (Battery BQ40z50) | - |
| HIL-B-01 | SPEC-FPGA-001 | REQ-FPGA-010, 031, 042 (scan FSM, CSI-2, SPI) | IT-01 (single frame) |
| HIL-B-02 | SPEC-FPGA-001, SPEC-FW-001 | REQ-FPGA-015, 034, REQ-FW-010, 040 | IT-02 (1000 frames), IT-10B (400M BW) |
| HIL-B-03 | SPEC-ARCH-001, SPEC-FPGA-001 | REQ-FPGA-034 (lane speed), D-PHY 800M | IT-10C (800M bandwidth) |
| HIL-B-04 | SPEC-ARCH-001, SPEC-FPGA-001 | Final target tier (3072x3072@15fps) | IT-10C (target tier) |
| HIL-B-05 | SPEC-FW-001, SPEC-SDK-001 | REQ-FW-052 (drop rate), REQ-SDK-032 | IT-09 (10,000 frames) |
| HIL-B-06 | SPEC-SIM-001 | REQ-SIM-026 (golden reference) | AC-SIM-009a |
| HIL-B-07 | SPEC-SDK-001, SPEC-ARCH-001 | REQ-ARCH-008 (10 GbE), REQ-SDK-030 | IT-10 (bandwidth), IT-07 (network) |
| HIL-C-01 | SPEC-FPGA-001, SPEC-FW-001 | REQ-FPGA-054 (watchdog), REQ-FW-032 (recovery) | IT-04D (watchdog), IT-04E/F (retry) |
| HIL-C-02 | SPEC-FW-001 | REQ-FW-061 (CSI-2 RX recovery) | IT-04 (error inject) |
| HIL-C-03 | SPEC-FPGA-001 | REQ-FPGA-062 (reset), REQ-FPGA-053 (error clear) | IT-04G (post-recovery), IT-05 (config change) |
| HIL-C-04 | SPEC-FW-001 | REQ-FW-060 (health monitor), REQ-FW-120 (systemd) | IT-08 (connection mgmt) |
| HIL-C-05 | SPEC-SDK-001, SPEC-FW-001 | REQ-SDK-025 (auto-reconnect), REQ-FW-051 (drop) | IT-07 (packet loss), IT-03 (reordering) |

---

## Quality Gates

### TRUST 5 Compliance

**Tested (T)**:
- All HIL patterns executed with documented results
- Pass/fail criteria are quantitative and measurable
- Golden reference comparison validates simulator accuracy

**Readable (R)**:
- Each test has clear procedure, commands, and expected results
- Troubleshooting guide for common failures
- Report template standardizes result documentation

**Unified (U)**:
- Register addresses consistent with fpga-design.md
- CLI commands consistent with SPEC-FW-001 detector_cli specification
- Tier names consistent across all documents

**Secured (S)**:
- No credentials or keys in test procedures
- Hardware access requires physical presence (no remote destructive tests)
- Error recovery tests verify safe state (gate_on = 0)

**Trackable (T)**:
- Each test has unique ID (HIL-X-YY)
- Traceability matrix links to SPEC requirements
- Version history tracks document changes

---

## Version

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | ABYZ-Lab | Initial HIL test plan |
| 2.0.0 | 2026-02-17 | ABYZ-Lab Agent (spec-sim) | Complete rewrite: added hardware setup, pre-test checklist, peripheral tests (A-04 to A-08), golden reference test (B-06), network test (B-07), error recovery tests (C-03 to C-05), troubleshooting guide, traceability matrix, quality gates, report template |
| 2.1.0 | 2026-02-17 | spec-fpga (P1 register fix) | Fixed 6 register address errors per fpga-design.md Section 6.3: HIL-B-01 Panel Config 0x60/0x61/0x62 -> 0x50/0x51/0x52, HIL-B-02/B-03 CSI2_LANE_SPEED values 0/1 -> 0x64/0x7D, HIL-B-03/C-03 soft reset 0x00 -> CONTROL 0x21, HIL-B-03 CSI2_STATUS 0x70 -> DATA_IF_STATUS 0x90, HIL-C-03 STATUS 0x04 -> 0x20 and FRAME_COUNTER 0x08 -> 0x30/0x31, HIL-C-03 DEVICE_ID value 0xD7E0 -> 0xA735. Added Go/No-Go 800M Decision Criteria section. Enhanced traceability matrix with IT-03/IT-05/IT-06/IT-07/IT-08/IT-10 cross-references. |
