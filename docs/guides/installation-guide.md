# System Installation Guide

**Project**: X-ray Detector Panel System
**Version**: 1.0.0
**Last Updated**: 2026-02-17

---

## 1. Overview

This guide covers the complete installation procedure for the X-ray Detector Panel System, including hardware setup, software installation on all three layers (FPGA, SoC, Host PC), and verification testing.

### 1.1 System Architecture

```
+----------------+     CSI-2 4-lane     +------------------+     10 GbE      +------------------+
|   FPGA Board   | ------------------> |   SoC Board      | -------------> |   Host PC        |
|  (Artix-7 35T) |     D-PHY           | (i.MX8M Plus)    |    UDP         | (.NET 8.0 SDK)   |
|                |                      |                  |                |                  |
|  Panel Scan    |     SPI (50 MHz)     |  detector_daemon |    Control     |  DetectorClient  |
|  FSM + CSI-2   | <----- control ----> |  (Linux 5.15+)   | <--- 8001 --> |  GUI.Application |
+----------------+                      +------------------+                +------------------+
```

### 1.2 Hardware Bill of Materials

| Component | Specification | Quantity |
|-----------|--------------|----------|
| FPGA Board | Artix-7 XC7A35T-FGG484 evaluation board | 1 |
| SoC Board | NXP i.MX8M Plus EVK or custom board | 1 |
| Host PC | x86-64, 16+ GB RAM, 10 GbE NIC | 1 |
| CSI-2 FPC Cable | 10 cm, 4-lane D-PHY | 1 |
| SPI Connection | 4-wire (SCLK, MOSI, MISO, CS_N) | 1 |
| Ethernet Cable | Cat6a (for 10 GbE) | 1 |
| JTAG Cable | Platform Cable USB II or Digilent HS2 | 1 |
| Power Supply | Per board requirements | As needed |

---

## 2. FPGA Board Installation

### 2.1 Hardware Setup

1. **Unbox** the Artix-7 evaluation board
2. **Connect power supply** per board documentation
3. **Connect JTAG cable** (Platform Cable USB II to board JTAG header)
4. **Connect CSI-2 FPC cable** from FPGA D-PHY output to SoC CSI-2 RX connector
5. **Connect SPI wires**:
   - FPGA SPI_SCLK -> SoC SPI1_SCLK
   - FPGA SPI_MOSI -> SoC SPI1_MOSI
   - FPGA SPI_MISO -> SoC SPI1_MISO
   - FPGA SPI_CS_N -> SoC SPI1_CS0
   - FPGA GND -> SoC GND (common ground required)

### 2.2 Program FPGA

```bash
# Connect JTAG cable and power on the board

# Program via Vivado Hardware Manager
vivado -mode batch -source fpga/scripts/program.tcl

# Or use the GUI:
# 1. Open Vivado > Hardware Manager
# 2. Auto Connect
# 3. Program Device with fpga/output/csi2_detector_top.bit
```

### 2.3 Verify FPGA

After programming:

1. **Heartbeat LED** should be toggling at ~1 Hz
2. **Status LEDs** should show IDLE state pattern
3. Power consumption should be < 2 W

---

## 3. SoC Board Installation

### 3.1 Hardware Setup

1. **Install Linux image** on SoC board (NXP i.MX8M Plus BSP)
   - Write Yocto image to SD card or eMMC
   - Boot the board and verify Linux 5.15+ is running

2. **Connect Ethernet** cable from SoC 10 GbE port to Host PC
3. **Connect CSI-2 FPC cable** from FPGA to SoC CSI-2 RX connector
4. **Connect SPI wires** from FPGA (see Section 2.1)

### 3.2 Network Configuration

Configure static IP on the SoC:

```bash
# SSH to SoC (initial access via serial console or DHCP)
ssh root@<soc-ip>

# Configure static IP
cat > /etc/network/interfaces.d/eth1 << 'EOF'
auto eth1
iface eth1 inet static
    address 192.168.1.100
    netmask 255.255.255.0
    gateway 192.168.1.1
EOF

# Apply network configuration
systemctl restart networking

# Verify
ip addr show eth1
ping 192.168.1.1  # Ping Host PC
```

### 3.3 Device Tree Configuration

Ensure CSI-2 and SPI devices are enabled in the device tree:

```bash
# Check CSI-2 device
ls /dev/video*
# Expected: /dev/video0

# Check SPI device
ls /dev/spidev*
# Expected: /dev/spidev0.0

# If devices are missing, apply device tree overlay:
# Copy the generated overlay
scp generated/detector-overlay.dts root@192.168.1.100:/boot/

# Compile and install overlay
ssh root@192.168.1.100 "dtc -O dtb -o /boot/detector-overlay.dtbo /boot/detector-overlay.dts"

# Add to boot configuration
ssh root@192.168.1.100 'echo "fdt_overlay=/boot/detector-overlay.dtbo" >> /boot/uEnv.txt'

# Reboot to apply
ssh root@192.168.1.100 "reboot"
```

### 3.4 Install Firmware

```bash
# Create configuration directory
ssh root@192.168.1.100 "mkdir -p /etc/detector"

# Copy binaries and configuration
scp fw/build-arm64/detector_daemon root@192.168.1.100:/usr/bin/
scp fw/build-arm64/detector_cli root@192.168.1.100:/usr/bin/
scp config/detector_config.yaml root@192.168.1.100:/etc/detector/

# Set permissions
ssh root@192.168.1.100 "chmod +x /usr/bin/detector_daemon /usr/bin/detector_cli"

# Install systemd service
scp fw/config/detector.service root@192.168.1.100:/etc/systemd/system/
ssh root@192.168.1.100 "systemctl daemon-reload"
ssh root@192.168.1.100 "systemctl enable detector"
ssh root@192.168.1.100 "systemctl start detector"
```

### 3.5 Verify SoC

```bash
# Check daemon status
ssh root@192.168.1.100 "systemctl status detector"
# Expected: active (running)

# Read FPGA DEVICE_ID via SPI
ssh root@192.168.1.100 "detector_cli read-reg 0xF0"
# Expected: 0xA735

# Check V4L2 device
ssh root@192.168.1.100 "v4l2-ctl -d /dev/video0 --info"
# Expected: MIPI CSI-2 receiver device

# Check network
ssh root@192.168.1.100 "detector_cli status"
# Expected: IDLE state, no errors
```

---

## 4. Host PC Installation

### 4.1 System Requirements

| Component | Minimum | Recommended |
|-----------|---------|-------------|
| OS | Windows 10 / Ubuntu 22.04 | Windows 11 / Ubuntu 24.04 |
| CPU | 4-core x86-64 | 8-core x86-64 |
| RAM | 8 GB | 16 GB |
| Storage | 50 GB SSD | 250 GB NVMe SSD |
| Network | 1 GbE | 10 GbE |

### 4.2 Install .NET 8.0

**Windows**:
```powershell
winget install Microsoft.DotNet.SDK.8
dotnet --version
```

**Linux**:
```bash
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0
dotnet --version
```

### 4.3 Network Configuration

Configure the Host PC network interface for detector communication:

**Windows**:
1. Open Network Connections (Control Panel > Network and Sharing Center)
2. Right-click the 10 GbE adapter > Properties
3. Select IPv4 > Properties
4. Set static IP:
   - IP: 192.168.1.1
   - Subnet: 255.255.255.0
   - Gateway: (leave blank)

**Linux**:
```bash
# Configure static IP
sudo nmcli con mod "10GbE Connection" ipv4.method manual \
    ipv4.addresses 192.168.1.1/24

# Apply
sudo nmcli con up "10GbE Connection"

# Verify connectivity to SoC
ping 192.168.1.100
```

### 4.4 Optimize Network for High Throughput

For Target tier (2.26 Gbps), optimize network settings:

**Linux**:
```bash
# Increase UDP buffer sizes
sudo sysctl -w net.core.rmem_max=67108864
sudo sysctl -w net.core.rmem_default=67108864
sudo sysctl -w net.core.wmem_max=67108864

# Enable jumbo frames (if switch supports)
sudo ip link set eth1 mtu 9000

# Make persistent
echo "net.core.rmem_max=67108864" | sudo tee -a /etc/sysctl.conf
echo "net.core.rmem_default=67108864" | sudo tee -a /etc/sysctl.conf
```

**Windows**:
```powershell
# Increase UDP buffer via registry (run as Administrator)
# HKLM\SYSTEM\CurrentControlSet\Services\AFD\Parameters
# DefaultReceiveWindow = 67108864

# Enable Jumbo Frames in NIC properties
# Network Adapter > Properties > Configure > Advanced > Jumbo Packet > 9014 Bytes
```

### 4.5 Install Host SDK

```bash
cd system-emul-sim

# Build SDK
dotnet build sdk/XrayDetector.Sdk/

# Build GUI application (Windows only)
dotnet build tools/GUI.Application/
```

### 4.6 Create Output Directory

```bash
# Create frame storage directory
mkdir -p frames

# Verify write permissions
touch frames/test && rm frames/test
```

---

## 5. End-to-End Verification

### 5.1 Quick Connectivity Test

```bash
# 1. Verify Host can reach SoC
ping -c 3 192.168.1.100

# 2. Verify SoC can read FPGA registers
ssh root@192.168.1.100 "detector_cli read-reg 0xF0"
# Expected: 0xA735

# 3. Verify firmware daemon is running
ssh root@192.168.1.100 "systemctl is-active detector"
# Expected: active
```

### 5.2 Single Frame Test

```bash
# From Host PC, capture a single frame
dotnet run --project tools/IntegrationRunner -- --scenario IT-01

# Or use the SDK directly:
dotnet run --project sdk/XrayDetector.Sdk.Tests/ --filter "Category=Integration"
```

### 5.3 Continuous Streaming Test

```bash
# Capture 100 frames continuously
dotnet run --project tools/IntegrationRunner -- --scenario IT-03
```

### 5.4 Full Verification Checklist

| Step | Test | Expected Result |
|------|------|-----------------|
| 1 | FPGA heartbeat LED | Toggling ~1 Hz |
| 2 | SoC reads DEVICE_ID | 0xA735 |
| 3 | SoC daemon status | active |
| 4 | Host pings SoC | < 1 ms latency |
| 5 | Single frame capture | Zero bit errors |
| 6 | 100-frame continuous | Zero drops |
| 7 | TIFF file saved | Valid TIFF, correct dimensions |
| 8 | Error injection | Error detected and recovered |

---

## 6. Network Architecture

### 6.1 IP Address Scheme

| Device | Interface | IP Address | Port |
|--------|-----------|-----------|------|
| Host PC | 10 GbE NIC | 192.168.1.1 | - |
| SoC Controller | eth1 (10 GbE) | 192.168.1.100 | 8000 (data), 8001 (control) |
| SoC Controller | eth0 (management) | DHCP or 10.0.0.100 | 22 (SSH) |

### 6.2 Firewall Configuration

Ensure detector ports are open on the Host PC:

**Linux**:
```bash
sudo ufw allow 8000/udp  # Frame data
sudo ufw allow 8001/udp  # Control commands
```

**Windows**:
```powershell
# Open firewall for detector ports (run as Administrator)
netsh advfirewall firewall add rule name="Detector Data" dir=in action=allow protocol=UDP localport=8000
netsh advfirewall firewall add rule name="Detector Control" dir=in action=allow protocol=UDP localport=8001
```

---

## 7. Troubleshooting Installation

### 7.1 FPGA Issues

| Symptom | Possible Cause | Solution |
|---------|---------------|---------|
| No heartbeat LED | FPGA not programmed | Reprogram via JTAG |
| JTAG not detected | Cable disconnected or driver missing | Check USB connection, install drivers |
| Programming fails | Power issue or wrong bitstream | Verify power supply, check device part number |

### 7.2 SoC Issues

| Symptom | Possible Cause | Solution |
|---------|---------------|---------|
| `/dev/video0` missing | CSI-2 driver not loaded | Check device tree, verify FPC cable connection |
| `/dev/spidev0.0` missing | SPI not enabled | Apply device tree overlay with SPI enabled |
| DEVICE_ID reads 0x0000 | SPI wiring error | Check all 4 SPI signals + ground |
| Daemon won't start | Missing config file | Verify `/etc/detector/detector_config.yaml` exists |

### 7.3 Network Issues

| Symptom | Possible Cause | Solution |
|---------|---------------|---------|
| Host can't ping SoC | Wrong IP or subnet | Verify both devices on 192.168.1.0/24 |
| High packet loss | MTU mismatch | Set both ends to same MTU (1500 or 9000) |
| Low throughput | 1 GbE instead of 10 GbE | Verify 10 GbE NIC and cable, check `ethtool eth1` |
| Firewall blocking | Ports not open | Add firewall rules for UDP 8000/8001 |

### 7.4 Host PC Issues

| Symptom | Possible Cause | Solution |
|---------|---------------|---------|
| `dotnet` not found | .NET SDK not installed | Install .NET 8.0 SDK |
| Build fails | Missing NuGet packages | Run `dotnet restore` |
| Frame drops > 0.01% | UDP buffer too small | Increase `net.core.rmem_max` |
| GUI doesn't start | WPF on Linux | Use Windows for GUI, or use CLI tools on Linux |

---

## 8. Uninstallation

### 8.1 Remove SoC Software

```bash
ssh root@192.168.1.100 << 'EOF'
systemctl stop detector
systemctl disable detector
rm /etc/systemd/system/detector.service
rm /usr/bin/detector_daemon /usr/bin/detector_cli
rm -rf /etc/detector/
systemctl daemon-reload
EOF
```

### 8.2 Remove Host Software

```bash
# Remove build artifacts
cd system-emul-sim
dotnet clean
rm -rf frames/
```

---

## 9. Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (architect) | Initial installation guide |

---
