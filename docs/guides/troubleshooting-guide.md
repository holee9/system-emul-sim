# Troubleshooting Guide

**Document Version**: 1.0.0
**Status**: Draft
**Last Updated**: 2026-02-17

## Table of Contents

1. [Quick Diagnostic Reference](#1-quick-diagnostic-reference)
2. [SoC System Health](#2-soc-system-health)
3. [Hardware Connectivity](#3-hardware-connectivity)
4. [CSI-2 Link Issues](#4-csi-2-link-issues)
   - [800M D-PHY Debugging Procedures](#800m-d-phy-debugging-procedures)
5. [SPI Communication Failures](#5-spi-communication-failures)
6. [Frame Acquisition Issues](#6-frame-acquisition-issues)
7. [Network and Throughput Issues](#7-network-and-throughput-issues)
8. [Battery and Peripheral Devices](#8-battery-and-peripheral-devices)
9. [WiFi (Sterling 60 / QCA6174A)](#9-wifi-sterling-60--qca6174a)
10. [FPGA Error Codes Reference](#10-fpga-error-codes-reference)
    - [Error Recovery State Machine](#error-recovery-state-machine)
11. [Log Collection](#11-log-collection)
    - [Log Analysis Guide](#log-analysis-guide)
12. [Revision History](#12-revision-history)

---

## 1. Quick Diagnostic Reference

Run these commands first to characterize the problem. Results narrow down which section to consult.

### 1.1 SoC System State

```bash
# Kernel version and uptime
uname -r && uptime

# Memory availability
free -h

# Disk usage
df -h

# Firmware daemon status
systemctl status xray-detector.service

# Recent daemon logs (last hour)
journalctl -u xray-detector.service --since "1 hour ago" --no-pager
```

Expected output:

```
6.6.52-lts-imx8mp
 12:34:56 up 2:15,  1 user,  load average: 0.08, 0.12, 0.10
               total        used        free
Mem:           7.6Gi       1.2Gi       6.4Gi

xray-detector.service - X-ray Detector Daemon
     Active: active (running) since ...
```

### 1.2 Hardware Device Check

```bash
# I2C bus 0: BQ40z50 battery fuel gauge at 0x0b
i2cdetect -y 0

# I2C bus 7: BMI160 IMU at 0x68
i2cdetect -y 7

# PCIe devices: Sterling 60 WiFi and 2.5 GbE
lspci -nn

# CSI-2 RX: should list /dev/video0
v4l2-ctl --list-devices
```

### 1.3 Network State

```bash
# Check all interfaces and link state
ip link show

# Check 10 GbE link speed (should show Speed: 10000Mb/s)
ethtool eth1

# Ping Host PC from SoC
ping -c 4 192.168.1.1

# Bandwidth test (run iperf3 server on Host PC first)
iperf3 -c 192.168.1.1 -t 10 -u -b 3G
```

### 1.4 FPGA Communication

```bash
# Read DEVICE_ID via SPI (use spidev_test from linux-tools)
spidev_test -D /dev/spidev0.0 -v -s 1000000 -p "\x00\x00\x00\x00"
# Expected response bytes: D7 E0 00 01

# Or use the firmware CLI (cleaner output)
detector_cli read-reg 0x00
# Expected: 0xD7E00001
```

---

## 2. SoC System Health

### 2.1 No SoC Boot (UART shows no output)

**Symptoms**: UART console is silent after power on, or shows only boot loader output.

**Diagnostics**:

```bash
# Verify UART baud rate: 115200 baud, 8N1, no flow control
# On Linux
screen /dev/ttyUSB0 115200
# On Windows: PuTTY -> Serial -> COMx -> 115200
```

**Resolution steps**:

1. Check power supply: SoC board requires 5 V / 3 A minimum. Measure at the power connector. Low voltage causes random resets.
2. Check boot media: Verify the Yocto image was flashed successfully. If using SD card, try a different card.
3. Re-flash the Yocto image:
   ```bash
   # On Linux, using DD to SD card (replace /dev/sdX)
   sudo dd if=core-image-minimal-imx8mp-var-dart.wic \
       of=/dev/sdX bs=1M status=progress conv=fsync
   sync
   ```
4. Verify SD card integrity:
   ```bash
   # On Linux
   sudo fsck /dev/sdX1
   ```
5. Check boot mode jumpers: Confirm the VAR-SOM-MX8M-PLUS carrier board jumpers are set for SD card or eMMC boot (see carrier board manual).

### 2.2 SoC Firmware Daemon Not Starting

**Symptoms**: `systemctl status xray-detector.service` shows `failed` or `inactive`.

```bash
# Check detailed failure reason
journalctl -u xray-detector.service -n 50 --no-pager

# Common causes in the log:
# "Failed to open /dev/spidev0.0" -> SPI device missing
# "config file not found" -> /etc/detector/detector_config.yaml missing
# "device ID mismatch" -> FPGA not programmed

# Check configuration file exists
ls -la /etc/detector/detector_config.yaml

# Check SPI device exists
ls /dev/spidev0.0

# Check CSI-2 video device exists
ls /dev/video0
```

---

## 3. Hardware Connectivity

### 3.1 Checking FPGA Configuration (DONE LED Off)

The DONE LED on the FPGA board should be solid on after configuration. If it is off:

1. Verify the bitstream file exists: `fpga/output/csi2_detector_top.bit`
2. Reprogram via JTAG:
   ```bash
   vivado -mode batch -source fpga/scripts/program_fpga.tcl \
       -tclargs fpga/output/csi2_detector_top.bit
   ```
3. Check Vivado output for errors. Common errors:
   - "Cannot find device": JTAG cable not connected or driver missing
   - "Wrong part": Bitstream was built for a different Artix-7 variant
   - "CRC error during programming": Power supply noise during programming (add capacitors or use shorter JTAG cable)

### 3.2 Verifying FPC Cable Integrity

CSI-2 FPC cable failures are a common source of link problems.

1. Visual inspection: Look for kinks, cuts, or bent pins at the connectors.
2. Connector seating: Unlock both connectors, reinsert the cable, and lock. The cable should lie flat.
3. Swap test: If a second cable is available, swap it to isolate the cable from the connector.
4. Resistance check: With the connectors unlocked and cable removed, measure continuity between the FPGA-end and SoC-end of each lane pair (CLK, D0, D1, D2, D3).

---

## 4. CSI-2 Link Issues

### 4.1 v4l2 No Device (/dev/video0 Missing)

**Symptoms**: `v4l2-ctl --list-devices` shows no MIPI CSI-2 device.

```bash
# Check if the CSI-2 subsystem driver loaded
dmesg | grep -i "mipi"
dmesg | grep -i "csi"

# Expected output includes:
# nxp-mipi-csi2 32e40000.mipi_csi: enabled
# video0: MIPI CSI-2 ...
```

**Resolution steps**:

1. Check FPC cable connection (Section 3.2).
2. Verify the FPGA CSI-2 TX is operational:
   ```bash
   # CSI2_LANE_SPEED register = 0x60 (bit[0]: 0=400Mbps, 1=800Mbps)
   detector_cli read-reg 0x60
   # Expected: 0x0000 (400 Mbps/lane) or 0x0001 (800 Mbps/lane)
   # Check CSI2_STATUS register = 0x70 (bit[0]: phy_ready)
   detector_cli read-reg 0x70
   # Expected: bit[0]=1 (phy_ready) when D-PHY is initialized
   ```
3. Apply the device tree overlay if it was not applied at boot:
   ```bash
   # Verify the overlay is in uEnv.txt
   grep "fdt_overlay" /boot/uEnv.txt
   # If missing:
   echo "fdt_overlay=/boot/detector-overlay.dtbo" >> /boot/uEnv.txt
   reboot
   ```
4. Check kernel messages for D-PHY errors:
   ```bash
   dmesg | grep -i "dphy"
   # Look for: "DPHY calibration failed" or "lane alignment timeout"
   ```

### 4.2 CSI-2 Link Established But Frames Are Corrupted

**Symptoms**: `/dev/video0` exists, frames are received, but pixel data contains stripes, random values, or partially filled images.

```bash
# Check for FPGA CRC or D-PHY errors
detector_cli read-reg 0x04
# Non-zero value indicates error flags are set

# Read the specific error flag bits
# Bit 3 (0x08): CRC_ERROR - CSI-2 CRC-16 mismatch
# Bit 5 (0x20): DPHY_LINK_FAIL - lane sync lost
```

Resolution:

1. Reduce CSI-2 lane speed to 400 Mbps (if currently at 800 Mbps):
   ```yaml
   # In detector_config.yaml
   fpga:
     csi2:
       lane_speed_mbps: 400
   ```
2. Check FPC cable length and quality. At 800 Mbps, the maximum reliable cable length is approximately 15 cm. For 400 Mbps, 30 cm is acceptable.
3. Check for electromagnetic interference sources near the FPC cable.

### 800M D-PHY Debugging Procedures

**Note**: 800M operation is currently under validation. Use 400M for production.

When experiencing instability at 800M lane speed (CSI2_LANE_SPEED=0x61, value=1):

**Step 1: Verify Lane Speed Configuration**
```bash
detector_cli read-reg 0x61  # CSI2_LANE_SPEED: 0=400M, 1=800M
```

**Step 2: Check CSI-2 Link Status**
```bash
detector_cli read-reg 0x70  # CSI2_STATUS
# Bit 0: phy_ready (1=ready)
# Bit 1: tx_active (1=transmitting)
# Bit 4: fifo_overflow (1=error)
```

**Step 3: Read ILA Capture Registers**
```bash
detector_cli read-reg 0x10  # ILA_CAPTURE_0: lane_sync status
detector_cli read-reg 0x11  # ILA_CAPTURE_1: cdc_error flags
detector_cli read-reg 0x12  # ILA_CAPTURE_2: timing margins
detector_cli read-reg 0x13  # ILA_CAPTURE_3: error counters
```

**Step 4: Signal Integrity Check**
- CRC errors at 800M may indicate signal integrity issues
- FPC cable length: maximum 15cm for 800M (30cm for 400M)
- Check FPC connector for bent pins, loose contact
- If errors persist, downgrade to 400M: `detector_cli write-reg 0x61 0`
- Then power cycle FPGA board

**Thermal Considerations at 800M**
- FPGA power consumption increases at 800M
- Monitor FPGA board temperature (should not exceed ambient +15°C)
- If spontaneous CRC errors occur during sustained 800M operation, suspect thermal marginality
- Mitigation: Apply heatsink to FPGA BGA package

---

## 5. SPI Communication Failures

### 5.1 Register Read Returns 0x00000000

**Cause**: SPI MISO line is disconnected or grounded. The FPGA is not responding.

```bash
# Test with spidev_test at a slow speed
spidev_test -D /dev/spidev0.0 -v -s 100000 -p "\x00\x00\x00\x00"
# Look at the received bytes
```

**Resolution steps**:

1. Verify MISO wire is connected: FPGA `GPIO_SPI_MISO` to SoC `SPI1_MISO`.
2. Verify common ground: FPGA GND to SoC GND.
3. Verify CS_N is asserted: Check with a multimeter or oscilloscope that CS_N goes low during the SPI transaction.
4. Verify FPGA is programmed (DONE LED lit).

### 5.2 Register Read Returns 0xFFFFFFFF

**Cause**: FPGA is not programmed or has entered an error state. When the FPGA SPI slave is not active, the MISO pin floats high.

```bash
# Confirm FPGA is programmed by checking the status register
detector_cli read-reg 0x02
# Expected: 0x0000 (IDLE) if FPGA is running
# If 0xFFFF, the FPGA is not responding
```

**Resolution**: Reprogram the FPGA bitstream via JTAG.

### 5.3 Intermittent SPI Errors

**Cause**: SPI clock speed too high, or long/unshielded wires causing signal integrity issues.

```bash
# Check the configured SPI clock speed
cat /etc/detector/detector_config.yaml | grep spi_clock
# Verify it is <= 50000000 (50 MHz)
```

**Resolution**: Reduce SPI clock speed in `detector_config.yaml`. For wire lengths greater than 20 cm, use 10 MHz or less.

---

## 6. Frame Acquisition Issues

### 6.1 No Frames Received (Timeout)

**Symptoms**: `GetFrameAsync` throws `TimeoutException`, or the IT-01 integration test fails with "frame timeout".

```bash
# Check the STATUS register to confirm scan state
# REG_STATUS = 0x20 (see docs/api/spi-register-map.md)
# bit[0]=idle, bit[1]=scan_active, bit[2]=error
detector_cli read-reg 0x20
# 0x0001: IDLE (not scanning)
# 0x0002: SCANNING active (scan_active bit set)
# 0x0004: ERROR state
```

**Diagnostics**:

```bash
# Check the frame counter - should increment when scanning
# REG_FRAME_COUNT_LO = 0x30
detector_cli read-reg 0x30
sleep 1
detector_cli read-reg 0x30
# If both readings are the same, the FPGA is not generating frames

# Check the STATUS register
detector_cli read-reg 0x20
# bit[0]=idle: 1=IDLE state
# bit[1]=scan_active: 1=SCANNING active
# bit[3]=frame_done: 1=frame completed (self-clearing on read)
```

**Resolution**:

1. Ensure `StartScanAsync()` was called before `GetFrameAsync()`.
2. Check the FPGA error flags register (0x04). Non-zero indicates a hardware error.
3. Verify panel timing in `detector_config.yaml` matches the ROIC datasheet values.

### 6.2 Checking Panel Timing

Verify the panel timing registers contain sensible values:

```bash
# GATE_ON timing (REG_TIMING_GATE_ON = 0x50)
detector_cli read-reg 0x50
# Should match detector_config.yaml fpga.timing.gate_on_us * 100
# e.g., gate_on_us=1000 -> register value = 100000 (0x186A)

# GATE_OFF timing (REG_TIMING_GATE_OFF = 0x51)
detector_cli read-reg 0x51
# e.g., gate_off_us=200 -> register value = 20000 (0x4E20)
```

If registers show unexpected values, re-synchronize configuration:

```bash
# Regenerate timing from config and re-deploy
dotnet run --project tools/ConfigConverter -- \
    --input config/detector_config.yaml --format c-header

# Rebuild and redeploy firmware
# (See deployment guide Section 6.1)
```

---

## 7. Network and Throughput Issues

### 7.1 High Frame Drop Rate (> 0.01%)

**Diagnostics**:

```bash
# On Host PC: Check link speed
ethtool eth1 | grep Speed
# Expected: Speed: 10000Mb/s

# Check current UDP receive buffer size
sysctl net.core.rmem_max
# Should be >= 26214400 (25 MB) for Intermediate tier
# Should be >= 67108864 (64 MB) for Final tier

# Run a bandwidth test to verify physical link
iperf3 -s -p 5201  # on Host PC
iperf3 -c 192.168.1.1 -p 5201 -t 10  # on SoC
# Expected: >= 2.5 Gbps for Final tier
```

**Resolution**:

```bash
# Increase UDP socket buffer on Host PC
sudo sysctl -w net.core.rmem_max=26214400
sudo sysctl -w net.core.rmem_default=26214400

# Make permanent
echo "net.core.rmem_max=26214400" | sudo tee -a /etc/sysctl.conf

# Enable jumbo frames on both sides (if switch supports it)
sudo ip link set eth1 mtu 9000          # on SoC
sudo ip link set eth1 mtu 9000          # on Host PC
```

For persistent jumbo frames on the SoC:

```bash
cat >> /etc/network/interfaces.d/eth1 << 'EOF'
    mtu 9000
EOF
systemctl restart networking
```

### 7.2 Testing UDP Frame Reception

```bash
# Run a quick bandwidth verification with iperf3 UDP mode
# On Host PC (receiver):
iperf3 -s -p 5201

# On SoC (sender):
iperf3 -c 192.168.1.1 -p 5201 -t 10 -u -b 2.5G -l 8192
# Expected: Actual bandwidth near 2.5 Gbps, loss < 0.01%
```

### 7.3 Link Negotiating at 1 GbE Instead of 10 GbE

```bash
# Check auto-negotiation result on SoC
ethtool eth1
# Look for: Speed: 10000Mb/s
# If Speed: 1000Mb/s:

# Force 10 GbE speed (if supported by the NIC)
ethtool -s eth1 speed 10000 duplex full autoneg off

# Check cable: Cat6a is required for 10 GbE at standard cable lengths
# Cat5e may link at 1 GbE only
```

---

## 8. Battery and Peripheral Devices

### 8.1 BQ40z50 Battery Fuel Gauge Not Detected

**Symptoms**: `i2cdetect -y 0` shows no device at `0x0b`.

```bash
# Check I2C bus 0 devices
i2cdetect -y 0
# Expected: -- at most addresses, and 0b at address 0x0b

# Verify the bq27xxx driver is loaded
lsmod | grep bq27
# Expected: bq27xxx_battery ...

# If driver is missing, load it manually
modprobe bq27xxx_battery

# Manual SMBus read to verify device responds
i2cget -y 0 0x0b 0x0d w
# Expected: non-zero value (state of charge as a percentage * 256)

# Full register dump
i2cdump -y 0 0x0b
```

**Resolution if device still not found**:

1. Check the battery is connected and charged sufficiently to respond on the bus.
2. Check `SMBUS_SCL` and `SMBUS_SDA` lines from the battery connector to the SoC I2C0 bus.
3. Verify the battery's SMBUS address is configured to `0x0b` (SMBus default for BQ40z50).

### 8.2 BMI160 IMU Not Detected

**Symptoms**: `i2cdetect -y 7` shows no device at `0x68`.

```bash
# Check I2C bus 7
i2cdetect -y 7
# Expected: device at address 0x68

# Verify BMI160 chip ID
i2cget -y 7 0x68 0x00
# Expected: 0xd1 (BMI160 chip ID)

# Check IMU power supply
# BMI160 requires 1.8 V or 3.3 V VDD
```

The BMI160 is not critical for X-ray acquisition. If it is absent, the firmware daemon logs a warning but continues normal operation.

---

## 9. WiFi (Sterling 60 / QCA6174A)

### 9.1 WiFi Interface Not Available

The Ezurio Sterling 60 module uses the QCA6174A chip connected via PCIe.

```bash
# Verify PCIe detection
lspci | grep -i "QCA"
# Expected: Qualcomm Atheros QCA6174 802.11ac Wireless Network Adapter

# Check ath10k firmware files
ls /lib/firmware/ath10k/QCA6174/hw3.0/
# Expected: firmware-6.bin, board.bin

# If firmware files are missing, obtain from linux-firmware package
apt-get install linux-firmware
# or copy from the Yocto build

# Load the driver
modprobe ath10k_pci

# Verify interface appears
ip link show wlan0
```

### 9.2 WiFi Not Connecting

```bash
# Scan for available networks
nmcli dev wifi list

# Connect to a network
nmcli dev wifi connect "SSID" password "password"

# Check connection status
nmcli con show

# If NetworkManager is not running
systemctl start NetworkManager
```

**Note**: WiFi is available for management access but should not carry X-ray frame data. Frame data must flow over the 10 GbE link (eth1) for performance reasons.

---

## 10. FPGA Error Codes Reference

The FPGA ERROR_FLAGS register (address 0x80) uses individual bits to report hardware conditions. Multiple errors can be set simultaneously. All flags are sticky (write-1-clear).

| Bit | Hex Mask | Name | Description | Immediate Action |
|-----|----------|------|-------------|-----------------|
| 0 | 0x0001 | TIMEOUT | Readout exceeded TIMING_LINE_PERIOD x 2 | Check panel connection; retry scan |
| 1 | 0x0002 | OVERFLOW | Line buffer bank collision; data lost | Reduce FPS or resolution |
| 2 | 0x0004 | CRC_ERROR | CSI-2 self-check CRC mismatch | Reseat FPC cable; reduce CSI-2 speed |
| 3 | 0x0008 | OVEREXPOSURE | One or more pixels reached saturation threshold | Reduce X-ray exposure time |
| 4 | 0x0010 | ROIC_FAULT | No valid ROIC data within TIMING_LINE_PERIOD | Check ROIC LVDS connections |
| 5 | 0x0020 | DPHY_ERROR | D-PHY initialization failed or link loss | Reseat FPC cable; check power to FPGA |
| 6 | 0x0040 | CONFIG_ERROR | Invalid config: rows=0, cols=0, or invalid bit_depth | Verify detector_config.yaml |
| 7 | 0x0080 | WATCHDOG | System heartbeat timer expired (100 ms threshold) | Check SPI wires; restart daemon |

**Reading and clearing error flags**:

```bash
# Read current error flags (REG_ERROR_FLAGS = 0x80)
detector_cli read-reg 0x80

# Clear all error flags using write-1-clear semantics
detector_cli write-reg 0x80 0x00FF  # Write 1 to all flag bits to clear them
```

**Severity guide**:

- Bits 0, 3, 7 (TIMEOUT, OVEREXPOSURE, WATCHDOG): Warning severity. Log the occurrence. Scan may continue.
- Bits 1, 2, 4, 5 (OVERFLOW, CRC_ERROR, ROIC_FAULT, DPHY_ERROR): Error severity. Stop the scan. Investigate before restarting.
- Bit 6 (CONFIG_ERROR): Configuration error. Fix detector_config.yaml and redeploy.
- Any flag causes STATUS bit[2] (error) to assert and FSM to enter ERROR state.

### Error Recovery State Machine

When errors occur, follow this escalation sequence:

**Tier 1 - Transient Error Recovery** (CRC_ERROR, TIMEOUT)
1. Read ERROR_FLAGS (0x80) to identify error type
2. Write error_clear to register 0x80 (value 0x80 to clear all)
3. Wait 100ms
4. Retry scan — if succeeds, continue normally

**Tier 2 - Persistent Error Recovery** (after 3 failed Tier 1 retries)
1. Issue soft reset: Write CONTROL register (0x21), bit 2 = 1
2. Wait 500ms for FPGA to reinitialize
3. Verify DEVICE_ID (0x00 = upper bytes, 0x01 = lower bytes, total 0xD7E00001)
4. Restart scan from beginning

**Tier 3 - Unrecoverable State** (DEVICE_ID unreadable, all registers return 0xFF)
1. Power cycle FPGA board (hardware power off/on)
2. If DEVICE_ID still unreadable after power cycle → FPGA bitstream corrupted
3. Re-program FPGA via Vivado/JTAG
4. If SoC daemon won't restart → Re-flash SoC Yocto image

**Error Priority when Multiple Flags Set**
Priority order (high→low): WATCHDOG > OVERFLOW > CRC_ERROR > TIMEOUT
Handle in priority order, clearing each flag after recovery.

**WATCHDOG Error (bit 7) Specific Recovery**
WATCHDOG fires when SoC loses SPI communication for >100ms:
1. Verify SPI cable connections (all 4 signals: SCLK, MOSI, MISO, CS_N)
2. Check SoC kernel: `dmesg | grep spi` for driver errors
3. If SPI driver reports errors: reboot SoC (`sudo reboot`)
4. If WATCHDOG repeats after 2 reboot cycles: power cycle FPGA

---

## 11. Log Collection

When reporting issues or escalating to the development team, collect the following:

### Log Analysis Guide

**Log Collection**
```bash
# SoC daemon logs (primary)
journalctl -u detector-daemon -n 100 --no-pager

# Kernel messages (driver issues)
dmesg | grep -E "mipi|csi|spi|v4l2"

# All recent errors
journalctl -p err -n 50 --no-pager
```

**Normal Startup Log Pattern**
```
detector[PID]: INFO: Loading config from /etc/detector/detector_config.yaml
detector[PID]: INFO: SPI device opened: /dev/spidev0.0 (16-bit, 50MHz)
detector[PID]: INFO: FPGA DEVICE_ID: 0xD7E00001 - OK
detector[PID]: INFO: CSI-2 V4L2 device: /dev/video0
detector[PID]: INFO: UDP data socket bound to port 8000
detector[PID]: INFO: UDP command socket bound to port 8001
detector[PID]: INFO: Daemon ready
```

**Error Pattern Search**
```bash
# Find all FPGA errors
journalctl -u detector-daemon | grep "FPGA_ERR"

# Find frame drops
journalctl -u detector-daemon | grep "drop\|DROP"

# Find CSI-2 issues
dmesg | grep -i "csi\|mipi\|v4l2" | grep -i "err\|warn\|fail"
```

**Timestamp Note**: All journalctl timestamps are in local timezone unless overridden.
For UTC: `journalctl --utc -u detector-daemon`

---

### 11.1 SoC Logs

```bash
# Firmware daemon log (last 200 lines)
journalctl -u xray-detector.service -n 200 --no-pager > /tmp/daemon.log

# Kernel messages related to detector hardware
dmesg | grep -E "mipi|csi|spi|i2c|ath10k|bq27" > /tmp/kernel.log

# System status snapshot
{
  echo "=== uname ==="; uname -a
  echo "=== uptime ==="; uptime
  echo "=== free ==="; free -h
  echo "=== df ==="; df -h
  echo "=== ip link ==="; ip link show
  echo "=== ethtool eth1 ==="; ethtool eth1
  echo "=== detector status ==="; detector_cli status 2>/dev/null || echo "daemon not running"
  echo "=== i2cdetect bus 0 ==="; i2cdetect -y 0
  echo "=== i2cdetect bus 7 ==="; i2cdetect -y 7
  echo "=== v4l2 devices ==="; v4l2-ctl --list-devices
  echo "=== lspci ==="; lspci -nn
} > /tmp/soc_status.txt

# Collect all logs into one archive
tar czf /tmp/detector_logs_$(date +%Y%m%d_%H%M%S).tar.gz \
    /tmp/daemon.log /tmp/kernel.log /tmp/soc_status.txt
```

### 11.2 Host PC Diagnostics

```bash
# Collect integration test results
dotnet run --project tools/IntegrationRunner -- \
    --all --verbose \
    --report /tmp/integration_$(date +%Y%m%d).json

# Network statistics
{
  echo "=== ip link ==="; ip link show
  echo "=== ethtool ==="; ethtool eth1 2>/dev/null
  echo "=== sysctl UDP ==="; sysctl net.core.rmem_max net.core.rmem_default
  echo "=== ss UDP stats ==="; ss -s | grep UDP
} > /tmp/network_diag.txt
```

### 11.3 Diagnostic Report Template

Include this information when submitting a bug report or support request:

```
=== X-ray Detector Diagnostic Report ===
Date: [YYYY-MM-DD HH:MM UTC]
SDK Version: [output of detector_cli --version]
Firmware Version: [from detector_cli status]
FPGA Bitstream: [version from detector_config.yaml or build artifact name]

Hardware:
  FPGA Board: Artix-7 XC7A35T evaluation board
  SoC Board: VAR-SOM-MX8M-PLUS
  CSI-2 Cable: [length, part number if known]
  10 GbE: [NIC make/model, cable category]

Software:
  OS (SoC): [uname -r output]
  OS (Host): [Windows 11 / Ubuntu 24.04]
  .NET Version: [dotnet --version]

Configuration:
  Tier: [Minimum / Intermediate / Final]
  Panel rows x cols: [2048 x 2048]
  Bit depth: [16]
  FPS: [15]
  CSI-2 speed: [400 Mbps/lane]

Issue:
  Description: [What happened]
  FPGA ERROR_FLAGS: [0x????]
  First occurrence: [when did this start]
  Frequency: [Always / Intermittent / Once]
  Steps to reproduce: [numbered list]

Integration Test Results:
  IT-01: [PASS/FAIL]
  IT-03: [PASS/FAIL]
  IT-05: [PASS/FAIL]
  ...

Attachments:
  [ ] daemon.log
  [ ] kernel.log
  [ ] soc_status.txt
  [ ] integration_results.json
```

---

## 12. Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Docs Agent | Complete troubleshooting guide with diagnostic commands, issue categories, and log collection procedures |
| 1.0.1 | 2026-02-17 | manager-quality | Fix register addresses throughout: STATUS=0x20, CONTROL=0x21, FRAME_COUNT_LO=0x30, TIMING_GATE_ON=0x50, TIMING_GATE_OFF=0x51, CSI2_LANE_SPEED=0x60, CSI2_STATUS=0x70, ERROR_FLAGS=0x80. Corrected ERROR_FLAGS bit definitions to match spi-register-map.md. |
| 1.1.0 | 2026-02-17 | manager-docs | Add 800M D-PHY Debugging Procedures section, Error Recovery State Machine, and Log Analysis Guide. |

---

## Review Record

- Date: 2026-02-17
- Reviewer: manager-quality
- Status: Approved (with corrections applied)
- TRUST 5: T:5 R:5 U:4 S:4 T:4
