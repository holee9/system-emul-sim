# Production Deployment Guide

**Project**: X-ray Detector Panel System
**Version**: 1.0.0
**Last Updated**: 2026-02-17

---

## 1. Overview

This guide covers production deployment procedures for the X-ray Detector Panel System. It describes how to prepare, deploy, verify, and maintain the system in a production environment.

### 1.1 Deployment Scope

| Component | Deployment Target | Artifact |
|-----------|------------------|----------|
| FPGA Bitstream | Artix-7 XC7A35T (on-board flash) | `.bit` / `.mcs` file |
| SoC Firmware | NXP i.MX8M Plus (eMMC/SD) | `detector_daemon` binary |
| Host SDK | Host PC (.NET runtime) | Published .NET application |
| Configuration | All layers | `detector_config.yaml` |

### 1.2 Deployment Environments

| Environment | Purpose | Configuration |
|-------------|---------|---------------|
| **Development** | Active development and testing | Debug builds, ILA probes enabled |
| **Staging** | Pre-production validation | Release builds, production config |
| **Production** | Clinical deployment | Release builds, hardened, no debug |

---

## 2. Pre-Deployment Checklist

Before deploying to any environment, verify all items:

### 2.1 Quality Gates

- [ ] All unit tests pass (`dotnet test` returns 0)
- [ ] Code coverage >= 85% (per quality.yaml target)
- [ ] Integration tests IT-01 through IT-10 all pass
- [ ] FPGA LUT utilization < 60%
- [ ] FPGA timing closure: WNS >= 1 ns
- [ ] CDC report: zero violations
- [ ] No critical warnings in synthesis or implementation
- [ ] TRUST 5 framework compliance verified

### 2.2 Build Artifacts

- [ ] FPGA bitstream built in Release mode (no ILA probes for production)
- [ ] Firmware cross-compiled with `-O2` optimization
- [ ] Host SDK built in Release configuration
- [ ] Configuration file validated by ConfigConverter
- [ ] All artifacts version-tagged in Git

### 2.3 Hardware Verification

- [ ] FPGA board power supply verified
- [ ] CSI-2 FPC cable integrity tested
- [ ] SPI wiring verified (4 signals + ground)
- [ ] 10 GbE cable tested (Cat6a rated)
- [ ] All connectors secured

---

## 3. Build Release Artifacts

### 3.1 FPGA Release Build

```bash
cd system-emul-sim/fpga

# Build without debug probes (production)
vivado -mode batch -source scripts/build_release.tcl

# Output: fpga/output/csi2_detector_release.bit
# Output: fpga/output/csi2_detector_release.mcs (for flash)
```

**Production Build Differences**:

| Setting | Development | Production |
|---------|------------|-----------|
| ILA Probes | Enabled | Removed |
| VIO | Enabled | Removed |
| Bitstream Compression | Optional | Enabled |
| Security | None | Optional encryption |
| Configuration Speed | 33 MHz | 33 MHz |

### 3.2 Firmware Release Build

```bash
cd system-emul-sim/fw

# Source cross-compiler
source /opt/fsl-imx-xwayland/5.15-kirkstone/environment-setup-cortexa53-crypto-poky-linux

# Release build
mkdir -p build-release && cd build-release
cmake -DCMAKE_TOOLCHAIN_FILE=../toolchain/imx8mp-toolchain.cmake \
      -DCMAKE_BUILD_TYPE=Release \
      -DLOG_LEVEL=WARN \
      ..
make -j$(nproc)

# Strip debug symbols
aarch64-poky-linux-strip detector_daemon
aarch64-poky-linux-strip detector_cli

# Verify
file detector_daemon
# Expected: ELF 64-bit LSB executable, ARM aarch64, stripped
ls -la detector_daemon
```

### 3.3 Host SDK Release Build

```bash
cd system-emul-sim

# Publish self-contained application (Windows)
dotnet publish tools/GUI.Application/ \
    -c Release \
    -r win-x64 \
    --self-contained true \
    -o publish/win-x64/

# Publish self-contained application (Linux)
dotnet publish tools/GUI.Application/ \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -o publish/linux-x64/

# Publish SDK library as NuGet package
dotnet pack sdk/XrayDetector.Sdk/ \
    -c Release \
    -o publish/nuget/
```

### 3.4 Create Release Package

```bash
# Create release directory
VERSION=1.0.0
RELEASE_DIR=release/v${VERSION}
mkdir -p ${RELEASE_DIR}/{fpga,firmware,host,config,docs}

# Copy artifacts
cp fpga/output/csi2_detector_release.bit ${RELEASE_DIR}/fpga/
cp fpga/output/csi2_detector_release.mcs ${RELEASE_DIR}/fpga/
cp fw/build-release/detector_daemon ${RELEASE_DIR}/firmware/
cp fw/build-release/detector_cli ${RELEASE_DIR}/firmware/
cp fw/config/detector.service ${RELEASE_DIR}/firmware/
cp -r publish/win-x64/ ${RELEASE_DIR}/host/windows/
cp -r publish/linux-x64/ ${RELEASE_DIR}/host/linux/
cp config/detector_config.yaml ${RELEASE_DIR}/config/
cp docs/guides/installation-guide.md ${RELEASE_DIR}/docs/

# Create archive
tar czf release/xray-detector-v${VERSION}.tar.gz -C release v${VERSION}
```

---

## 4. FPGA Deployment

### 4.1 Volatile Programming (JTAG)

For development and testing -- bitstream is lost on power cycle:

```bash
vivado -mode batch -source fpga/scripts/program.tcl \
    -tclargs fpga/output/csi2_detector_release.bit
```

### 4.2 Non-Volatile Programming (Flash)

For production -- bitstream persists across power cycles:

```bash
# Generate MCS file (if not already done)
vivado -mode batch -source fpga/scripts/create_mcs.tcl

# Program flash via JTAG
vivado -mode batch -source fpga/scripts/program_flash.tcl \
    -tclargs fpga/output/csi2_detector_release.mcs
```

**Example program_flash.tcl**:

```tcl
open_hw_manager
connect_hw_server -allow_non_jtag
open_hw_target

set device [get_hw_devices xc7a35t_0]
current_hw_device $device

# Create memory device
create_hw_cfgmem -hw_device $device -mem_dev [lindex [get_cfgmem_parts {s25fl128sxxxxxx0-spi-x1_x2_x4}] 0]

set cfgmem [get_property PROGRAM.HW_CFGMEM $device]
set_property PROGRAM.BLANK_CHECK 0 $cfgmem
set_property PROGRAM.ERASE 1 $cfgmem
set_property PROGRAM.CFG_PROGRAM 1 $cfgmem
set_property PROGRAM.VERIFY 1 $cfgmem
set_property PROGRAM.FILES [list "[lindex $argv 0]"] $cfgmem

# Program flash
program_hw_cfgmem -hw_cfgmem $cfgmem

puts "Flash programming complete!"
close_hw_manager
```

### 4.3 Verify FPGA After Programming

```bash
# Power cycle the board (for flash programming)
# Wait 5 seconds for configuration

# Check heartbeat LED
# Check DEVICE_ID via SPI from SoC:
ssh root@192.168.1.100 "detector_cli read-reg 0xF0"
# Expected: 0xA735
```

---

## 5. SoC Firmware Deployment

### 5.1 Deploy Firmware

```bash
SOC_IP=192.168.1.100
SOC_USER=root

# Stop running service
ssh ${SOC_USER}@${SOC_IP} "systemctl stop detector 2>/dev/null || true"

# Backup previous version
ssh ${SOC_USER}@${SOC_IP} "cp /usr/bin/detector_daemon /usr/bin/detector_daemon.bak 2>/dev/null || true"

# Deploy new binaries
scp fw/build-release/detector_daemon ${SOC_USER}@${SOC_IP}:/usr/bin/
scp fw/build-release/detector_cli ${SOC_USER}@${SOC_IP}:/usr/bin/
ssh ${SOC_USER}@${SOC_IP} "chmod +x /usr/bin/detector_daemon /usr/bin/detector_cli"

# Deploy configuration
scp config/detector_config.yaml ${SOC_USER}@${SOC_IP}:/etc/detector/

# Deploy systemd service
scp fw/config/detector.service ${SOC_USER}@${SOC_IP}:/etc/systemd/system/
ssh ${SOC_USER}@${SOC_IP} "systemctl daemon-reload"

# Start service
ssh ${SOC_USER}@${SOC_IP} "systemctl enable detector"
ssh ${SOC_USER}@${SOC_IP} "systemctl start detector"

# Verify
ssh ${SOC_USER}@${SOC_IP} "systemctl status detector --no-pager"
```

### 5.2 Rollback Firmware

If the new version has issues:

```bash
# Stop current version
ssh ${SOC_USER}@${SOC_IP} "systemctl stop detector"

# Restore backup
ssh ${SOC_USER}@${SOC_IP} "cp /usr/bin/detector_daemon.bak /usr/bin/detector_daemon"

# Restart
ssh ${SOC_USER}@${SOC_IP} "systemctl start detector"
```

### 5.3 Firmware Configuration

Production configuration adjustments:

```yaml
# /etc/detector/detector_config.yaml (production settings)
panel:
  rows: 2048            # Match actual panel
  cols: 2048
  bit_depth: 16

fpga:
  timing:
    gate_on_us: 1000     # Match ROIC datasheet
    gate_off_us: 200
    roic_settle_us: 50
    adc_conv_us: 10

controller:
  ethernet:
    speed: 10gbe
    port: 8000
    control_port: 8001

# Logging (production: reduced verbosity)
logging:
  level: WARN           # DEBUG, INFO, WARN, ERROR
  output: syslog        # syslog or file
```

---

## 6. Host PC Deployment

### 6.1 Deploy SDK Application

**Windows**:

```powershell
# Extract release package
Expand-Archive xray-detector-v1.0.0.zip -DestinationPath C:\XrayDetector\

# Or copy published application
Copy-Item -Recurse publish\win-x64\ C:\XrayDetector\

# Create desktop shortcut (optional)
$WshShell = New-Object -comObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("$Home\Desktop\X-ray Detector.lnk")
$Shortcut.TargetPath = "C:\XrayDetector\GUI.Application.exe"
$Shortcut.Save()
```

**Linux**:

```bash
# Extract release package
tar xzf xray-detector-v1.0.0.tar.gz -C /opt/
chmod +x /opt/v1.0.0/host/linux/GUI.Application

# Create symlink
sudo ln -sf /opt/v1.0.0/host/linux/GUI.Application /usr/local/bin/xray-detector

# Create systemd service (optional, for headless operation)
sudo cat > /etc/systemd/system/xray-detector.service << 'EOF'
[Unit]
Description=X-ray Detector Host Application
After=network.target

[Service]
ExecStart=/opt/v1.0.0/host/linux/GUI.Application --headless
Restart=on-failure

[Install]
WantedBy=multi-user.target
EOF
```

### 6.2 Network Configuration

See Section 4 of the Installation Guide for Host PC network setup.

### 6.3 Verify Host Application

```bash
# Run the application
cd publish/win-x64/
./GUI.Application.exe

# Or on Linux (CLI mode)
./GUI.Application --headless --config /etc/detector/detector_config.yaml

# Run verification tests
dotnet run --project tools/IntegrationRunner -- --all
```

---

## 7. Configuration Management

### 7.1 Configuration Versioning

All configuration changes must be version-controlled:

```bash
# Tag configuration with release version
cd system-emul-sim
git tag -a v1.0.0-config -m "Production config for v1.0.0"
git push origin v1.0.0-config
```

### 7.2 Configuration Validation

Before deploying configuration changes:

```bash
# 1. Validate schema
dotnet run --project tools/ConfigConverter -- \
    --input config/detector_config.yaml --validate-only

# 2. Run integration tests with new config
dotnet run --project tools/IntegrationRunner -- --all \
    --config config/detector_config.yaml

# 3. Deploy to SoC
scp config/detector_config.yaml root@192.168.1.100:/etc/detector/
ssh root@192.168.1.100 "systemctl restart detector"
```

### 7.3 Configuration Sync Across Layers

All three layers must use consistent configuration:

```bash
# Generate all target configs from single source
dotnet run --project tools/ConfigConverter -- \
    --input config/detector_config.yaml \
    --output generated/ \
    --target all

# Deploy to each layer:
# 1. FPGA: Apply generated .xdc constraints (requires rebuild)
# 2. SoC: Copy detector_config.yaml
# 3. Host: Copy sdk-config.json
```

---

## 8. Monitoring and Health Checks

### 8.1 SoC Health Monitoring

```bash
# Check daemon health
ssh root@192.168.1.100 "detector_cli status"

# Expected output:
# System Status: IDLE
# FPGA Device ID: 0xA735
# Frame Counter: 0
# Error Flags: 0x00
# Uptime: 2h 34m 12s
# Temperature: 45.2 C
```

### 8.2 FPGA Status Monitoring

```bash
# Read all FPGA registers
ssh root@192.168.1.100 "detector_cli dump-regs"

# Key registers to monitor:
# STATUS (0x04): Should be IDLE when not scanning
# ERROR_FLAGS (0x10): Should be 0x00 (no errors)
# FRAME_COUNTER (0x08): Increments during scan
```

### 8.3 Network Performance Monitoring

```bash
# Monitor network throughput (Linux Host)
iftop -i eth1 -f "port 8000"

# Check UDP statistics
ss -s | grep UDP

# Monitor frame statistics via SDK
dotnet run --project tools/IntegrationRunner -- --scenario IT-10 --verbose
```

### 8.4 Automated Health Check Script

```bash
#!/bin/bash
# health_check.sh - Automated system health check
set -e

SOC_IP=192.168.1.100

echo "=== System Health Check ==="

# 1. Check SoC connectivity
echo -n "SoC connectivity: "
if ping -c 1 -W 2 ${SOC_IP} > /dev/null 2>&1; then
    echo "OK"
else
    echo "FAIL - Cannot reach SoC"
    exit 1
fi

# 2. Check firmware daemon
echo -n "Firmware daemon: "
STATUS=$(ssh root@${SOC_IP} "systemctl is-active detector" 2>/dev/null)
if [ "$STATUS" = "active" ]; then
    echo "OK (active)"
else
    echo "FAIL ($STATUS)"
    exit 1
fi

# 3. Check FPGA communication
echo -n "FPGA DEVICE_ID: "
DEVICE_ID=$(ssh root@${SOC_IP} "detector_cli read-reg 0xF0" 2>/dev/null)
if [ "$DEVICE_ID" = "0xA735" ]; then
    echo "OK (0xA735)"
else
    echo "FAIL ($DEVICE_ID)"
    exit 1
fi

# 4. Check error flags
echo -n "Error flags: "
ERROR=$(ssh root@${SOC_IP} "detector_cli read-reg 0x10" 2>/dev/null)
if [ "$ERROR" = "0x0000" ]; then
    echo "OK (no errors)"
else
    echo "WARNING (errors detected: $ERROR)"
fi

echo "=== Health Check Complete ==="
```

---

## 9. Upgrade Procedures

### 9.1 FPGA Upgrade

1. Build new bitstream (see Section 3.1)
2. Verify in staging environment
3. Program production FPGA via JTAG or flash
4. Verify DEVICE_ID and heartbeat
5. Run integration tests

### 9.2 Firmware Upgrade

1. Cross-compile new firmware (see Section 3.2)
2. Deploy to staging SoC
3. Run verification tests
4. Deploy to production SoC (see Section 5.1)
5. Monitor for 24 hours

### 9.3 Host SDK Upgrade

1. Build release package (see Section 3.3)
2. Deploy to staging Host PC
3. Run all integration tests
4. Deploy to production Host PC
5. Verify frame capture and storage

### 9.4 Configuration-Only Update

```bash
# 1. Validate new config
dotnet run --project tools/ConfigConverter -- \
    --input config/detector_config_new.yaml --validate-only

# 2. Deploy to SoC
scp config/detector_config_new.yaml root@192.168.1.100:/etc/detector/detector_config.yaml
ssh root@192.168.1.100 "systemctl restart detector"

# 3. Verify
ssh root@192.168.1.100 "detector_cli status"
```

---

## 10. Backup and Recovery

### 10.1 What to Back Up

| Component | Location | Frequency |
|-----------|----------|-----------|
| FPGA bitstream | `fpga/output/*.bit` | Each release |
| Firmware binary | `fw/build-release/*` | Each release |
| Configuration | `config/detector_config.yaml` | Each change |
| Frame data | `frames/` on Host PC | Per policy |
| SoC system image | Full eMMC/SD backup | Before upgrade |

### 10.2 SoC Full Backup

```bash
# Backup SoC filesystem
ssh root@192.168.1.100 "tar czf /tmp/soc_backup.tar.gz /etc/detector/ /usr/bin/detector_*"
scp root@192.168.1.100:/tmp/soc_backup.tar.gz backups/soc_backup_$(date +%Y%m%d).tar.gz
```

### 10.3 Recovery Procedure

If the system enters an unrecoverable state:

1. **FPGA**: Reprogram via JTAG using known-good bitstream
2. **SoC**: Restore from backup:
   ```bash
   scp backups/soc_backup_YYYYMMDD.tar.gz root@192.168.1.100:/tmp/
   ssh root@192.168.1.100 "cd / && tar xzf /tmp/soc_backup_YYYYMMDD.tar.gz"
   ssh root@192.168.1.100 "systemctl restart detector"
   ```
3. **Host**: Reinstall from release package

---

## 11. Security Considerations

### 11.1 Production Hardening

| Measure | Description |
|---------|-------------|
| SSH Key Authentication | Disable password login on SoC |
| Firewall | Only allow ports 8000, 8001, 22 |
| Read-only Root FS | Mount SoC root filesystem as read-only |
| Signed Bitstream | Enable Vivado bitstream encryption (optional) |
| TLS for Control | Encrypt control channel (future enhancement) |

### 11.2 Access Control

```bash
# SoC: Disable root password login
ssh root@192.168.1.100 "sed -i 's/PermitRootLogin yes/PermitRootLogin prohibit-password/' /etc/ssh/sshd_config"
ssh root@192.168.1.100 "systemctl restart sshd"

# Use SSH key authentication instead
ssh-copy-id root@192.168.1.100
```

---

## 12. Troubleshooting Deployment

| Issue | Cause | Solution |
|-------|-------|---------|
| Flash programming fails | Wrong MCS format | Verify SPI flash part number in TCL script |
| Firmware won't start after upgrade | Config format changed | Validate config with ConfigConverter |
| Host can't connect after upgrade | Port changed in config | Verify port numbers match across all layers |
| Frame data corrupted | Config mismatch between layers | Re-sync configuration from `detector_config.yaml` |
| Performance regression | Debug symbols not stripped | Rebuild with Release config, strip binaries |

---

## 13. Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (architect) | Initial production deployment guide |

---
