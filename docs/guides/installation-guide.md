# Installation Guide

**Document Version**: 1.0.0
**Status**: Reviewed
**Last Updated**: 2026-02-17

## Table of Contents

1. [Overview](#1-overview)
2. [Hardware Assembly](#2-hardware-assembly)
3. [SoC Software Installation](#3-soc-software-installation)
4. [FPGA Bitstream Installation](#4-fpga-bitstream-installation)
5. [Host PC Software Installation](#5-host-pc-software-installation)
6. [End-to-End Verification](#6-end-to-end-verification)
7. [Network Configuration Reference](#7-network-configuration-reference)
8. [Uninstallation](#8-uninstallation)
9. [Troubleshooting Installation](#9-troubleshooting-installation)
10. [Revision History](#10-revision-history)

---

## 1. Overview

This guide covers the complete installation procedure for the X-ray Detector Panel System. The system has three hardware layers that must be installed and verified in the order shown below.

### 1.1 System Architecture

```
+------------------+  CSI-2 4-lane   +-------------------+   10 GbE UDP   +------------------+
| FPGA Board       | --------------> | SoC Board         | ------------> | Host PC          |
| Artix-7 XC7A35T  |   D-PHY FPC     | VAR-SOM-MX8M-PLUS |               | x86-64, .NET 8.0 |
|                  |                 | Yocto Scarthgap   |               |                  |
| Panel Scan FSM   |  SPI (50 MHz)   | Linux 6.6.52      |   Control     | XrayDetector.SDK |
| CSI-2 TX         | <--- control -> | detector daemon   | <--- 8001 --> | GUI.Application  |
+------------------+                 +-------------------+               +------------------+
```

### 1.2 Hardware Bill of Materials

| Component | Specification | Qty |
|-----------|--------------|-----|
| FPGA Board | Xilinx Artix-7 XC7A35T-FGG484 evaluation board | 1 |
| SoC Board | Variscite VAR-SOM-MX8M-PLUS (NXP i.MX8M Plus) | 1 |
| Host PC | x86-64, 16 GB RAM, 10 GbE NIC, .NET 8.0 | 1 |
| CSI-2 FPC Cable | 10 cm, 15-pin, 4-lane D-PHY | 1 |
| SPI Wiring | 4-wire jumper set (SCLK, MOSI, MISO, CS_N) + GND | 1 set |
| Ethernet Cable | Cat6a or better (10 GbE rated) | 1 |
| JTAG Cable | Digilent HS2 or Platform Cable USB II | 1 |
| Power Supplies | Per board requirements (FPGA: 12 V, SoC: 5 V/3 A) | As needed |

### 1.3 Installation Order

Complete each phase in order. Do not skip ahead.

1. Hardware assembly (Section 2)
2. SoC software installation (Section 3)
3. FPGA bitstream installation (Section 4)
4. Host PC software installation (Section 5)
5. End-to-end verification (Section 6)

---

## 2. Hardware Assembly

### 2.1 FPGA Board Setup

**Power supply check:**
Connect the power supply before attaching any cables. The Artix-7 evaluation board requires 12 V DC. Measure the voltage at the board power header before powering on. Expected: 11.8-12.2 V.

**JTAG setup:**
1. Connect the Digilent HS2 or Platform Cable USB II to the JTAG header on the FPGA board. The connector is keyed; ensure pin 1 (marked with a triangle) is aligned with pin 1 on the header.
2. Connect the USB end to the development PC.
3. Install Vivado Lab Edition or Vivado Design Suite if not already present.
4. Open Vivado Hardware Manager and click Auto Connect to verify the JTAG connection detects `xc7a35t_0`.

**CSI-2 FPC cable connection:**
The 10 cm FPC cable connects the FPGA D-PHY transmitter output to the SoC CSI-2 receiver input.

- FPGA end: Locate the MIPI CSI-2 TX FPC connector on the evaluation board (usually labelled J5 or CSI_OUT).
- SoC end: Locate the MIPI CSI-2 RX connector on the VAR-SOM-MX8M-PLUS carrier board (usually labelled CSI1).
- Insert the FPC cable with the metal contacts facing down. Press the latch until it clicks. Do not force.

Lane mapping verification: The 15-pin FPC connector carries lanes in this order from pin 1: CLK_N, CLK_P, D0_N, D0_P, D1_N, D1_P, D2_N, D2_P, D3_N, D3_P, GND, GND, GND, VDD, VDD. Verify the cable is not reversed.

### 2.2 SPI Wiring

Connect four GPIO pins from the FPGA to the SoC SPI controller. The SoC uses `/dev/spidev0.0` (SPI1 bus, chip select 0):

| Signal | FPGA Pin | SoC Pin | Note |
|--------|----------|---------|------|
| SCLK | GPIO_SPI_SCLK | SPI1_SCLK | Clock driven by SoC |
| MOSI | GPIO_SPI_MOSI | SPI1_MOSI | Data SoC to FPGA |
| MISO | GPIO_SPI_MISO | SPI1_MISO | Data FPGA to SoC |
| CS_N | GPIO_SPI_CS_N | SPI1_CS0 | Active low, driven by SoC |
| GND | Any GND | Any GND | Common ground required |

Maximum SPI clock: 50 MHz. The FPGA SPI slave accepts up to 50 MHz; do not configure the SoC SPI controller above this rate.

### 2.3 10 GbE Connection

Connect a Cat6a Ethernet cable from the SoC 10 GbE port (eth1 on the i.MX8M Plus carrier board) directly to the Host PC 10 GbE NIC. A network switch is acceptable if it is 10 GbE capable. Avoid using USB Ethernet adapters; they cannot sustain the 2.26 Gbps data rate required for the Final imaging tier.

---

## 3. SoC Software Installation

### 3.1 Flash Yocto Scarthgap Image

The SoC runs Yocto Scarthgap 5.0 LTS with Linux 6.6.52. Obtain the pre-built image `core-image-minimal-imx8mp-var-dart.wic` from the project release assets.

**Flash to eMMC via USB:**

1. Set the SoC board to USB download mode (consult the VAR-SOM-MX8M-PLUS carrier board manual for the boot mode jumper setting).
2. Connect a USB cable from the SoC USB OTG port to the development PC.
3. Flash the image using `uuu` (Freescale Universal Update Utility):
   ```bash
   uuu -b emmc_all imx-boot-imx8mp.bin core-image-minimal-imx8mp-var-dart.wic
   ```

**Flash to SD card (development use):**

```bash
# On Linux (replace /dev/sdX with your SD card device)
sudo dd if=core-image-minimal-imx8mp-var-dart.wic \
    of=/dev/sdX bs=1M status=progress conv=fsync

# Eject safely
sync && sudo eject /dev/sdX
```

### 3.2 First Boot and Console Access

Connect a USB-to-UART adapter (3.3 V logic level) to the SoC UART console header:

| SoC Pin | Signal |
|---------|--------|
| UART_TXD | TX (SoC sends) |
| UART_RXD | RX (SoC receives) |
| GND | GND |

Open a serial terminal at **115200 baud, 8N1, no flow control**:

```bash
# Linux
screen /dev/ttyUSB0 115200

# Windows: use PuTTY or TeraTerm, COM port at 115200 baud
```

Power on the SoC board. Boot messages should appear within 5 seconds. The system reaches a login prompt in approximately 30 seconds. Log in as `root` (no password by default on development images).

### 3.3 Network Configuration on SoC

Configure a static IP address on the 10 GbE interface (eth1):

```bash
# Check current interfaces
ip link show

# Configure static IP for eth1 (10 GbE, data interface)
cat > /etc/network/interfaces.d/eth1 << 'EOF'
auto eth1
iface eth1 inet static
    address 192.168.1.100
    netmask 255.255.255.0
EOF

# Apply
systemctl restart networking

# Verify
ip addr show eth1
# Expected: inet 192.168.1.100/24
```

### 3.4 Verify Hardware Devices

Confirm that kernel drivers for CSI-2 and SPI are loaded:

```bash
# CSI-2 receiver should appear as a video device
ls /dev/video*
# Expected: /dev/video0

# SPI device for FPGA communication
ls /dev/spidev*
# Expected: /dev/spidev0.0

# I2C bus 0 for BQ40z50 battery fuel gauge
i2cdetect -y 0
# Expected: device at address 0x0b

# I2C bus 7 for BMI160 IMU
i2cdetect -y 7
# Expected: device at address 0x68
```

If `/dev/video0` is missing, apply the device tree overlay generated by ConfigConverter:

```bash
# Copy the generated overlay to the SoC
scp generated/detector-overlay.dts root@192.168.1.100:/boot/

# Compile
dtc -O dtb -o /boot/detector-overlay.dtbo /boot/detector-overlay.dts

# Add to boot configuration
echo "fdt_overlay=/boot/detector-overlay.dtbo" >> /boot/uEnv.txt

# Reboot
reboot
```

### 3.5 Install Firmware Service

```bash
# On the development PC, copy binaries to the SoC
scp fw/build-release/detector_daemon root@192.168.1.100:/usr/bin/
scp fw/build-release/detector_cli root@192.168.1.100:/usr/bin/

# Create configuration directory
ssh root@192.168.1.100 "mkdir -p /etc/detector"

# Copy configuration
scp config/detector_config.yaml root@192.168.1.100:/etc/detector/

# Set execute permissions
ssh root@192.168.1.100 "chmod +x /usr/bin/detector_daemon /usr/bin/detector_cli"

# Install and enable the systemd service
scp fw/config/xray-detector.service root@192.168.1.100:/etc/systemd/system/
ssh root@192.168.1.100 "systemctl daemon-reload"
ssh root@192.168.1.100 "systemctl enable xray-detector.service"
ssh root@192.168.1.100 "systemctl start xray-detector.service"

# Verify
ssh root@192.168.1.100 "systemctl status xray-detector.service"
# Expected: Active: active (running)
```

---

## 4. FPGA Bitstream Installation

### 4.1 Program via JTAG (Development)

For development and testing, program the FPGA directly via JTAG. The bitstream is lost on power cycle.

```bash
# Program using Vivado batch mode
vivado -mode batch -source fpga/scripts/program_fpga.tcl \
    -tclargs fpga/output/csi2_detector_top.bit
```

If Vivado is not in your PATH, specify the full path:

```bash
/tools/Xilinx/Vivado/2024.1/bin/vivado -mode batch \
    -source fpga/scripts/program_fpga.tcl \
    -tclargs fpga/output/csi2_detector_top.bit
```

### 4.2 Program via SPI Flash (Production)

For production, program the SPI flash so the bitstream persists across power cycles.

```bash
# Generate MCS format for flash programming
vivado -mode batch -source fpga/scripts/create_mcs.tcl

# Program flash via JTAG
vivado -mode batch -source fpga/scripts/program_flash.tcl \
    -tclargs fpga/output/csi2_detector_top.mcs
```

After flash programming, power cycle the board and wait 5 seconds for configuration to load. The DONE LED should illuminate.

### 4.3 Verify FPGA

After programming, confirm the FPGA is operational:

1. **Heartbeat LED**: The LED connected to the heartbeat signal should blink at approximately 1 Hz.
2. **DONE LED**: Should be solid on after configuration.
3. **SPI register read**: From the SoC, read the DEVICE_ID register:

```bash
ssh root@192.168.1.100 "detector_cli read-reg 0x00"
# Expected: 0xD7E00001
```

The DEVICE_ID value `0xD7E00001` confirms the bitstream is loaded and SPI communication is working.

---

## 5. Host PC Software Installation

### 5.1 Install .NET 8.0 SDK

**Windows:**

```powershell
winget install Microsoft.DotNet.SDK.8
# Verify
dotnet --version
# Expected: 8.0.x
```

**Linux (Ubuntu/Debian):**

```bash
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0
dotnet --version
```

### 5.2 Install Host SDK via NuGet

For use in your own application:

```bash
dotnet add package XrayDetector.SDK --version 0.1.0
```

For development from source (this repository):

```bash
cd D:/workspace-github/system-emul-sim
dotnet build sdk/XrayDetector.Sdk/
```

### 5.3 Network Configuration on Host PC

Configure a static IP address on the 10 GbE NIC:

**Linux:**

```bash
# Replace enp5s0 with your 10 GbE interface name (check with: ip link show)
sudo nmcli con mod "Wired connection 1" \
    ipv4.method manual \
    ipv4.addresses 192.168.1.1/24

sudo nmcli con up "Wired connection 1"

# Verify
ping -c 3 192.168.1.100
```

**Windows:**

1. Open Settings > Network & Internet > Ethernet.
2. Click the 10 GbE adapter > Edit IP settings.
3. Change to Manual, enable IPv4.
4. Set IP address: `192.168.1.1`, Subnet prefix length: `24`.
5. Leave Gateway and DNS blank. Click Save.

### 5.4 Optimize UDP Receive Buffer

For the Final imaging tier (2.26 Gbps), increase the OS UDP receive buffer:

**Linux (persistent):**

```bash
sudo sysctl -w net.core.rmem_max=67108864
sudo sysctl -w net.core.rmem_default=67108864
echo "net.core.rmem_max=67108864" | sudo tee -a /etc/sysctl.conf
echo "net.core.rmem_default=67108864" | sudo tee -a /etc/sysctl.conf
```

**Windows:**

Open Registry Editor and navigate to `HKLM\SYSTEM\CurrentControlSet\Services\AFD\Parameters`. Create or set the DWORD value `DefaultReceiveWindow` = `67108864` (decimal). Reboot for the change to take effect.

### 5.5 Open Firewall Ports

**Linux (ufw):**

```bash
sudo ufw allow 8000/udp comment "Detector frame data"
sudo ufw allow 8001/udp comment "Detector control"
```

**Windows (PowerShell, run as Administrator):**

```powershell
netsh advfirewall firewall add rule name="XrayDetector Data" `
    dir=in action=allow protocol=UDP localport=8000
netsh advfirewall firewall add rule name="XrayDetector Control" `
    dir=in action=allow protocol=UDP localport=8001
```

---

## 6. End-to-End Verification

Perform these verification steps in order. Each step depends on the previous one passing.

### 6.1 Step 1 - Power On and UART Console

Power on the FPGA board first, then the SoC board. Connect to the SoC UART console (115200 baud). Confirm boot messages appear and a login prompt is reached within 60 seconds.

Expected UART output (final lines):

```
Starting xray-detector.service...
[  OK  ] Started xray-detector.service - X-ray Detector Daemon.
imx8mp-var-dart login:
```

### 6.2 Step 2 - SPI Register Read

Read the FPGA DEVICE_ID register via SPI to confirm FPGA programming and SPI wiring are correct:

```bash
ssh root@192.168.1.100 "detector_cli read-reg 0x00"
# Expected: 0xD7E00001
```

If the result is `0x00000000` or `0xFFFFFFFF`, the SPI communication has failed. See Section 9 for diagnostics.

### 6.3 Step 3 - CSI-2 Link

Verify the MIPI CSI-2 receiver is enumerated by the kernel:

```bash
ssh root@192.168.1.100 "v4l2-ctl --list-devices"
# Expected output includes:
# imx8mq-mipi-csi2 (platform:32e40000.mipi_csi):
#         /dev/video0
```

### 6.4 Step 4 - Network Connectivity

From the Host PC, verify connectivity to the SoC:

```bash
ping -c 4 192.168.1.100
# Expected: 0% packet loss, < 1 ms RTT
```

Test UDP bandwidth with iperf3 (optional but recommended for the Final tier):

```bash
# On SoC
iperf3 -s -p 5201

# On Host PC
iperf3 -c 192.168.1.100 -p 5201 -t 10 -u -b 3G
# Expected: > 2.5 Gbps actual throughput
```

### 6.5 Step 5 - Single Frame Capture

Run the IT-01 integration test to verify the end-to-end pipeline:

```bash
cd D:/workspace-github/system-emul-sim
dotnet run --project tools/IntegrationRunner -- --scenario IT-01 --verbose
# Expected: PASS - Zero bit errors
```

### 6.6 Full Verification Checklist

| Step | Command / Observation | Expected |
|------|----------------------|---------|
| 1 | FPGA heartbeat LED | Blinking ~1 Hz |
| 2 | FPGA DONE LED | Solid on |
| 3 | `detector_cli read-reg 0x00` | `0xD7E00001` |
| 4 | `systemctl status xray-detector` | `active (running)` |
| 5 | `ping 192.168.1.100` | < 1 ms, 0% loss |
| 6 | `v4l2-ctl --list-devices` | `/dev/video0` listed |
| 7 | IT-01 integration test | PASS, zero bit errors |
| 8 | IT-03 integration test (100 frames) | PASS, zero drops |

---

## 7. Network Configuration Reference

### 7.1 Default IP Scheme

| Device | Interface | IP Address | Purpose |
|--------|-----------|-----------|---------|
| Host PC | 10 GbE NIC | 192.168.1.1 | Receive frame data and send control |
| SoC (data) | eth1 (10 GbE) | 192.168.1.100 | Frame data port 8000, control port 8001 |
| SoC (mgmt) | eth0 (1 GbE) | DHCP or 10.0.0.100 | SSH access |

### 7.2 Port Reference

| Port | Protocol | Direction | Purpose |
|------|----------|-----------|---------|
| 8000 | UDP | SoC → Host | Frame pixel data |
| 8001 | UDP | Host → SoC | Scan control commands |
| 22 | TCP | Host → SoC | SSH management |

---

## 8. Uninstallation

### 8.1 Remove SoC Firmware

```bash
ssh root@192.168.1.100 << 'EOF'
systemctl stop xray-detector.service
systemctl disable xray-detector.service
rm /etc/systemd/system/xray-detector.service
rm /usr/bin/detector_daemon /usr/bin/detector_cli
rm -rf /etc/detector/
systemctl daemon-reload
EOF
```

### 8.2 Remove Host Software

```bash
cd D:/workspace-github/system-emul-sim
dotnet clean
```

To remove the NuGet package from a consumer project:

```bash
dotnet remove package XrayDetector.SDK
```

---

## 9. Troubleshooting Installation

### 9.1 FPGA Issues

| Symptom | Likely Cause | Solution |
|---------|-------------|---------|
| No heartbeat LED after programming | Power supply too low | Measure supply voltage, ensure >= 11.8 V |
| JTAG not detected in Vivado | USB cable issue or driver missing | Try different USB port, reinstall Digilent USB driver |
| Programming fails with "device not found" | Wrong board in TCL script | Confirm `xc7a35t_0` in `get_hw_devices` output |
| DONE LED does not light after power cycle | Flash programming incomplete | Re-run `program_flash.tcl` and verify erase step completes |

### 9.2 SoC Issues

| Symptom | Likely Cause | Solution |
|---------|-------------|---------|
| No UART output | Wrong baud rate or cable polarity | Try 115200, check TX/RX are not swapped |
| `/dev/video0` missing | CSI-2 device tree not applied | Apply `detector-overlay.dtbo` (Section 3.4) |
| `/dev/spidev0.0` missing | SPI not enabled in device tree | Apply device tree overlay with SPI enabled |
| `read-reg 0x00` returns `0x00000000` | SPI MISO or CS_N disconnected | Check all 5 SPI wires including GND |
| `read-reg 0x00` returns `0xFFFFFFFF` | FPGA not programmed | Reprogram via JTAG (Section 4.1) |

### 9.3 Network Issues

| Symptom | Likely Cause | Solution |
|---------|-------------|---------|
| `ping 192.168.1.100` fails | Wrong subnet or cable | Confirm both sides are on 192.168.1.0/24 subnet |
| iperf3 shows < 1 Gbps | Link is 1 GbE not 10 GbE | Run `ethtool eth1` on both sides, confirm `10000baseT` |
| Frame drops during streaming | UDP buffer too small | Apply `net.core.rmem_max` settings (Section 5.4) |

---

## 10. Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Docs Agent | Complete installation guide with hardware assembly and verification sequence |

---

## Review Record

- Date: 2026-02-17
- Reviewer: manager-quality
- Status: Approved
- TRUST 5: T:5 R:5 U:5 S:4 T:4
