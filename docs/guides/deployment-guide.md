# Deployment Guide

**Document Version**: 1.0.0
**Status**: Reviewed - Approved
**Last Updated**: 2026-02-17

## Table of Contents

1. [Overview](#1-overview)
2. [Prerequisites and Quality Gates](#2-prerequisites-and-quality-gates)
3. [Deployment Artifacts](#3-deployment-artifacts)
4. [Build Release Artifacts](#4-build-release-artifacts)
5. [FPGA Deployment](#5-fpga-deployment)
6. [SoC Firmware Deployment](#6-soc-firmware-deployment)
7. [Host PC Deployment](#7-host-pc-deployment)
8. [Environment Management](#8-environment-management)
9. [Post-Deployment Validation](#9-post-deployment-validation)
10. [Rollback Procedures](#10-rollback-procedures)
11. [Monitoring and Health Checks](#11-monitoring-and-health-checks)
12. [Upgrade Procedures](#12-upgrade-procedures)
13. [Backup and Recovery](#13-backup-and-recovery)
14. [Security Hardening](#14-security-hardening)
15. [Revision History](#15-revision-history)

---

## 1. Overview

This guide covers production deployment procedures for the X-ray Detector Panel System. All three system layers (FPGA, SoC firmware, Host PC) must be deployed together to maintain configuration consistency.

### 1.1 Deployment Scope

| Layer | Artifact | Target |
|-------|----------|--------|
| FPGA | `csi2_detector_v{version}.bit` / `.mcs` | Artix-7 XC7A35T SPI flash |
| SoC firmware | `detector_daemon` binary + `detector.service` | NXP i.MX8M Plus eMMC |
| Host SDK | `XrayDetector.SDK.{version}.nupkg` | Host PC .NET runtime |
| Host GUI | `XrayDetector.GUI_{version}_win-x64.zip` | Windows Host PC |
| Configuration | `detector_config_{env}.yaml` | All layers |

### 1.2 Deployment Environments

| Environment | Purpose | CSI-2 Speed | Notes |
|-------------|---------|------------|-------|
| `dev` | Active development and simulation | N/A (simulator) | Debug builds, fault injection enabled |
| `integration` | HIL testbed, IT-01 through IT-10 | 400 Mbps/lane | Release builds |
| `staging` | Full hardware pre-production validation | 400 Mbps/lane | Production config, no debug symbols |
| `production` | Clinical imaging | 800 Mbps/lane | Strict mode, syslog logging |

---

## 2. Prerequisites and Quality Gates

All gates must pass before deploying to `staging` or `production`. For `dev` and `integration` environments, gates that cannot yet pass must be documented with an approved exception.

### 2.1 Software Quality Gates

- [ ] All unit tests pass: `dotnet test` returns exit code 0
- [ ] Code coverage >= 85% (configured in `.moai/config/sections/quality.yaml`)
- [ ] Integration tests IT-01 through IT-06 pass (IT-07 to IT-10 required for production)
- [ ] No LSP errors or type errors (zero tolerance per quality gate configuration)
- [ ] TRUST 5 framework compliance verified

### 2.2 FPGA Quality Gates

- [ ] Synthesis and implementation complete with zero critical warnings
- [ ] LUT utilization < 60% (target for XC7A35T: < 12,480 of 20,800 LUTs)
- [ ] BRAM utilization < 50% (< 25 of 50 BRAMs)
- [ ] Timing closure: Worst Negative Slack (WNS) >= 1 ns
- [ ] Clock Domain Crossing (CDC) report: zero violations
- [ ] No ILA or VIO debug probes in production bitstream

### 2.3 Hardware Verification

- [ ] FPGA board power supply verified (11.8-12.2 V)
- [ ] CSI-2 FPC cable integrity confirmed (visual inspection, no kinks)
- [ ] SPI wiring verified (read DEVICE_ID = `0xD7E00001`)
- [ ] 10 GbE link confirmed at full speed (`ethtool eth1` shows `Speed: 10000Mb/s`)
- [ ] M0.5 PoC complete (required for production deployment)

---

## 3. Deployment Artifacts

### 3.1 Artifact Naming Convention

```
csi2_detector_v{major}.{minor}.{patch}.bit         # FPGA bitstream (JTAG)
csi2_detector_v{major}.{minor}.{patch}.mcs         # FPGA bitstream (SPI flash)
detector_daemon_{version}_aarch64                   # SoC firmware binary
xray-detector-fw_{version}_aarch64.deb             # SoC Debian package (optional)
XrayDetector.SDK.{version}.nupkg                   # Host SDK NuGet package
XrayDetector.GUI_{version}_win-x64.zip             # Host GUI Windows package
detector_config_{env}.yaml                          # Environment configuration
```

### 3.2 Version Tagging

All artifacts must be tagged in Git before deployment:

```bash
git tag -a v1.0.0 -m "Release v1.0.0: Intermediate tier validated at 400 Mbps"
git push origin v1.0.0
```

---

## 4. Build Release Artifacts

### 4.1 FPGA Release Build

Production bitstreams must be built without debug probes (ILA/VIO):

```bash
cd D:/workspace-github/system-emul-sim/fpga

# Release build: no ILA probes, bitstream compression enabled
vivado -mode batch -source scripts/build_release.tcl

# Outputs:
#   fpga/output/csi2_detector_release.bit  (JTAG programming)
#   fpga/output/csi2_detector_release.mcs  (SPI flash programming)
```

Build settings comparison:

| Setting | Development | Production |
|---------|------------|-----------|
| ILA debug probes | Enabled | Removed |
| VIO virtual I/O | Enabled | Removed |
| Bitstream compression | Optional | Enabled |
| Bitstream encryption | None | Optional |

### 4.2 SoC Firmware Release Build

```bash
cd D:/workspace-github/system-emul-sim/fw

# Source the Yocto Scarthgap cross-compiler toolchain
source /opt/fsl-imx-xwayland/5.0-scarthgap/environment-setup-cortexa53-crypto-poky-linux

# Release build with optimization
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
# Expected: ELF 64-bit LSB executable, ARM aarch64, version 1 (SYSV), stripped
```

### 4.3 Host SDK Release Build

```bash
cd D:/workspace-github/system-emul-sim

# Publish Windows GUI (self-contained)
dotnet publish tools/GUI.Application/ \
    -c Release -r win-x64 \
    --self-contained true \
    -o publish/win-x64/

# Publish Linux CLI (self-contained)
dotnet publish tools/XrayDetector.CLI/ \
    -c Release -r linux-x64 \
    --self-contained true \
    -o publish/linux-x64/

# Pack SDK as NuGet
dotnet pack sdk/XrayDetector.Sdk/ \
    -c Release \
    -o publish/nuget/
```

### 4.4 Create Release Package

```bash
VERSION=1.0.0
RELEASE_DIR=release/v${VERSION}
mkdir -p ${RELEASE_DIR}/{fpga,firmware,host,config,docs}

cp fpga/output/csi2_detector_release.bit  ${RELEASE_DIR}/fpga/
cp fpga/output/csi2_detector_release.mcs  ${RELEASE_DIR}/fpga/
cp fw/build-release/detector_daemon       ${RELEASE_DIR}/firmware/
cp fw/build-release/detector_cli          ${RELEASE_DIR}/firmware/
cp fw/config/xray-detector.service        ${RELEASE_DIR}/firmware/
cp -r publish/win-x64/                    ${RELEASE_DIR}/host/windows/
cp -r publish/linux-x64/                  ${RELEASE_DIR}/host/linux/
cp config/detector_config_production.yaml ${RELEASE_DIR}/config/
cp docs/guides/installation-guide.md      ${RELEASE_DIR}/docs/

tar czf release/xray-detector-v${VERSION}.tar.gz -C release v${VERSION}/
sha256sum release/xray-detector-v${VERSION}.tar.gz > release/xray-detector-v${VERSION}.tar.gz.sha256
```

---

## 5. FPGA Deployment

### 5.1 Volatile Programming via JTAG (Development / Staging)

The bitstream is lost on power cycle. Use for development and staging environments where frequent updates occur.

```bash
vivado -mode batch -source fpga/scripts/program.tcl \
    -tclargs fpga/output/csi2_detector_release.bit
```

### 5.2 Non-Volatile Programming via SPI Flash (Production)

The bitstream persists through power cycles. Required for production.

```bash
# Program SPI flash via JTAG
vivado -mode batch -source fpga/scripts/program_flash.tcl \
    -tclargs fpga/output/csi2_detector_release.mcs

# Power cycle the board after flash programming
# Wait 5 seconds for configuration
```

The `program_flash.tcl` script:
- Erases the flash
- Programs the MCS file
- Verifies readback
- Reports "Flash programming complete!" on success

### 5.3 Verify FPGA After Deployment

```bash
# From SoC, read DEVICE_ID register via SPI
ssh root@192.168.1.100 "detector_cli read-reg 0x00"
# Expected: 0xD7E00001

# Verify heartbeat LED is blinking
# Verify DONE LED is solid on
```

---

## 6. SoC Firmware Deployment

### 6.1 Deploy Firmware

```bash
SOC_IP=192.168.1.100
SOC_USER=root

# Stop running service
ssh ${SOC_USER}@${SOC_IP} "systemctl stop xray-detector.service 2>/dev/null || true"

# Backup previous version
ssh ${SOC_USER}@${SOC_IP} \
    "cp /usr/bin/detector_daemon /usr/bin/detector_daemon.bak 2>/dev/null || true"

# Deploy new binaries
scp fw/build-release/detector_daemon ${SOC_USER}@${SOC_IP}:/usr/bin/
scp fw/build-release/detector_cli    ${SOC_USER}@${SOC_IP}:/usr/bin/
ssh ${SOC_USER}@${SOC_IP} "chmod +x /usr/bin/detector_daemon /usr/bin/detector_cli"

# Deploy configuration (environment-specific)
scp config/detector_config_production.yaml \
    ${SOC_USER}@${SOC_IP}:/etc/detector/detector_config.yaml

# Deploy systemd service unit
scp fw/config/xray-detector.service \
    ${SOC_USER}@${SOC_IP}:/etc/systemd/system/
ssh ${SOC_USER}@${SOC_IP} "systemctl daemon-reload"
ssh ${SOC_USER}@${SOC_IP} "systemctl enable xray-detector.service"
ssh ${SOC_USER}@${SOC_IP} "systemctl start xray-detector.service"

# Verify
ssh ${SOC_USER}@${SOC_IP} "systemctl status xray-detector.service --no-pager"
# Expected: Active: active (running)
```

### 6.2 Environment-Specific Configuration

Production configuration disables debug features and sets logging to syslog:

```yaml
# /etc/detector/detector_config.yaml (production)
panel:
  rows: 2048
  cols: 2048
  bit_depth: 16
  pixel_pitch_um: 150

fpga:
  timing:
    gate_on_us: 1000
    gate_off_us: 200
    roic_settle_us: 50
    adc_conv_us: 10
  csi2:
    lane_speed_mbps: 400  # 800 when Final tier is ready

controller:
  ethernet:
    data_port: 8000
    control_port: 8001

logging:
  level: WARN           # production: WARN; dev: DEBUG
  output: syslog        # production: syslog; dev: file

fault_injection:
  enabled: false        # production: false; dev: true
```

---

## 7. Host PC Deployment

### 7.1 Deploy Windows GUI

```powershell
# Extract release package
Expand-Archive xray-detector-v1.0.0-host-win.zip -DestinationPath C:\XrayDetector\

# Create desktop shortcut
$WshShell = New-Object -comObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("$Home\Desktop\X-ray Detector.lnk")
$Shortcut.TargetPath = "C:\XrayDetector\GUI.Application.exe"
$Shortcut.Save()
```

### 7.2 Deploy Linux CLI (Headless)

```bash
# Extract to /opt
tar xzf xray-detector-v1.0.0-host-linux.tar.gz -C /opt/
chmod +x /opt/xray-detector-v1.0.0/XrayDetector.CLI

# Create versioned symlink
sudo ln -sf /opt/xray-detector-v1.0.0/XrayDetector.CLI \
    /usr/local/bin/xray-detector

# Create systemd service for headless continuous acquisition
cat > /etc/systemd/system/xray-acquisition.service << 'EOF'
[Unit]
Description=X-ray Detector Continuous Acquisition
After=network.target

[Service]
ExecStart=/usr/local/bin/xray-detector \
    --host 192.168.1.100 \
    --rows 2048 --cols 2048 --fps 15 \
    --frames 0 \
    --output /data/frames --format tiff
Restart=on-failure
RestartSec=5

[Install]
WantedBy=multi-user.target
EOF

systemctl enable xray-acquisition.service
```

### 7.3 Deploy Host SDK as NuGet Package

For consumer applications:

```bash
# Push to local NuGet feed
dotnet nuget push publish/nuget/XrayDetector.SDK.1.0.0.nupkg \
    --source http://your-nuget-server/v3/index.json \
    --api-key ${NUGET_API_KEY}

# Install in consumer project
dotnet add package XrayDetector.SDK --version 1.0.0
```

---

## 8. Environment Management

### 8.1 Configuration per Environment

| Setting | `dev` | `integration` | `staging` | `production` |
|---------|-------|--------------|---------|------------|
| `logging.level` | DEBUG | INFO | WARN | WARN |
| `logging.output` | file | file | syslog | syslog |
| `fault_injection.enabled` | true | true | false | false |
| `csi2.lane_speed_mbps` | N/A | 400 | 400 | 400 (800 when ready) |
| `panel.rows` | 1024 (fast) | 2048 | 2048 | 2048-3072 |

### 8.2 Environment Promotion

Artifacts must be promoted through environments in order:

```
dev -> integration -> staging -> production
```

Never promote directly from `dev` to `production`. Each environment must pass its validation before promotion.

---

## 9. Post-Deployment Validation

Run immediately after deploying to any environment:

```bash
# 1. Verify SoC daemon is running
ssh root@192.168.1.100 "systemctl is-active xray-detector.service"
# Expected: active

# 2. Verify FPGA DEVICE_ID
ssh root@192.168.1.100 "detector_cli read-reg 0x00"
# Expected: 0xD7E00001

# 3. Run integration smoke test
dotnet run --project tools/IntegrationRunner -- --scenario IT-01
# Expected: PASS

# 4. Run full integration suite for production
dotnet run --project tools/IntegrationRunner -- --all \
    --report reports/post-deploy-$(date +%Y%m%d).json
```

Integration test requirements by environment:

| Environment | Required Passing Tests |
|-------------|----------------------|
| `dev` | IT-01, IT-03 |
| `integration` | IT-01 through IT-06 |
| `staging` | IT-01 through IT-10 |
| `production` | IT-01 through IT-10 |

---

## 10. Rollback Procedures

### 10.1 Rollback SoC Firmware

```bash
SOC_IP=192.168.1.100

# Stop current service
ssh root@${SOC_IP} "systemctl stop xray-detector.service"

# Restore backup binary
ssh root@${SOC_IP} "cp /usr/bin/detector_daemon.bak /usr/bin/detector_daemon"

# Restore previous configuration (replace date with backup date)
scp backups/detector_config_YYYYMMDD.yaml \
    root@${SOC_IP}:/etc/detector/detector_config.yaml

# Restart service
ssh root@${SOC_IP} "systemctl start xray-detector.service"

# Verify
ssh root@${SOC_IP} "systemctl status xray-detector.service"
```

### 10.2 Rollback FPGA Bitstream

Keep the previous bitstream MCS file accessible. To rollback:

```bash
# Program previous known-good bitstream
vivado -mode batch -source fpga/scripts/program_flash.tcl \
    -tclargs fpga/output/csi2_detector_v{previous_version}.mcs
```

For immediate recovery without reprogramming flash, use JTAG volatile programming with the known-good bitstream. This allows recovery in minutes rather than waiting for the full flash programming cycle.

### 10.3 Rollback Host Application

On Windows:

```powershell
# Replace current installation with previous version
Remove-Item -Recurse C:\XrayDetector\
Expand-Archive xray-detector-v{previous_version}-host-win.zip \
    -DestinationPath C:\XrayDetector\
```

On Linux:

```bash
# Update symlink to previous version
sudo ln -sf /opt/xray-detector-v{previous_version}/XrayDetector.CLI \
    /usr/local/bin/xray-detector
```

---

## 11. Monitoring and Health Checks

### 11.1 Automated Health Check Script

Save as `scripts/health_check.sh` and run after deployment or on a schedule:

```bash
#!/bin/bash
# health_check.sh - Automated system health check
set -euo pipefail

SOC_IP=192.168.1.100
PASS=0
FAIL=0

check() {
    local name="$1"
    local result="$2"
    local expected="$3"
    if [ "$result" = "$expected" ]; then
        echo "[PASS] ${name}: ${result}"
        PASS=$((PASS+1))
    else
        echo "[FAIL] ${name}: expected '${expected}', got '${result}'"
        FAIL=$((FAIL+1))
    fi
}

echo "=== X-ray Detector Health Check - $(date) ==="

# 1. SoC connectivity
PING_RESULT=$(ping -c 1 -W 2 ${SOC_IP} > /dev/null 2>&1 && echo "ok" || echo "fail")
check "SoC ping" "$PING_RESULT" "ok"

# 2. Firmware daemon
DAEMON_STATUS=$(ssh root@${SOC_IP} "systemctl is-active xray-detector.service" 2>/dev/null || echo "inactive")
check "Firmware daemon" "$DAEMON_STATUS" "active"

# 3. FPGA DEVICE_ID
DEVICE_ID=$(ssh root@${SOC_IP} "detector_cli read-reg 0x00" 2>/dev/null || echo "error")
check "FPGA DEVICE_ID" "$DEVICE_ID" "0xD7E00001"

# 4. FPGA error flags (REG_ERROR_FLAGS = 0x80, see docs/api/spi-register-map.md)
ERROR_FLAGS=$(ssh root@${SOC_IP} "detector_cli read-reg 0x80" 2>/dev/null || echo "error")
check "FPGA error flags" "$ERROR_FLAGS" "0x0000"

echo "=== Results: ${PASS} passed, ${FAIL} failed ==="
[ "$FAIL" -eq 0 ]  # Exit code 0 if all pass, 1 if any fail
```

### 11.2 Key Registers to Monitor

| Register | Address | Normal Value | Description |
|----------|---------|-------------|-------------|
| DEVICE_ID | 0x00 | 0xD7E0 (upper) | Fixed identifier upper 16-bit |
| DEVICE_ID_LO | 0x01 | 0x0001 (lower) | Fixed identifier lower 16-bit |
| STATUS | 0x20 | 0x0001 (idle) | FSM state: bit[0]=idle, bit[1]=scanning |
| ERROR_FLAGS | 0x80 | 0x0000 | Should be zero (write-1-clear) |
| FRAME_COUNT_LO | 0x30 | Increasing | Frames scanned lower 16-bit |

---

## 12. Upgrade Procedures

### 12.1 FPGA Upgrade

1. Build new release bitstream (Section 4.1)
2. Deploy to `integration` environment and run IT-01 through IT-10
3. Deploy to `staging` and verify
4. Schedule `production` maintenance window
5. Program production FPGA flash (Section 5.2)
6. Power cycle and verify DEVICE_ID
7. Run post-deployment validation (Section 9)
8. Monitor for 24 hours

### 12.2 Firmware Upgrade

1. Cross-compile new firmware release (Section 4.2)
2. Deploy to `integration` and run integration tests
3. Deploy to `staging` and run IT-01 through IT-10
4. Deploy to `production` (Section 6.1)
5. Verify daemon status and DEVICE_ID
6. Monitor logs for 24 hours: `journalctl -u xray-detector.service -f`

### 12.3 Configuration-Only Upgrade

When only `detector_config.yaml` changes (no binary changes):

```bash
# 1. Validate new configuration
dotnet run --project tools/ConfigConverter -- \
    --input config/detector_config_new.yaml --validate-only

# 2. Run integration tests with new config
dotnet run --project tools/IntegrationRunner -- \
    --all --config config/detector_config_new.yaml

# 3. Deploy to SoC
scp config/detector_config_new.yaml \
    root@192.168.1.100:/etc/detector/detector_config.yaml
ssh root@192.168.1.100 "systemctl restart xray-detector.service"

# 4. Verify
ssh root@192.168.1.100 "systemctl status xray-detector.service"
```

---

## 13. Backup and Recovery

### 13.1 What to Back Up

| Artifact | Location | Backup Frequency | Retention |
|----------|----------|-----------------|-----------|
| FPGA bitstreams | `fpga/output/*.bit`, `*.mcs` | Each release | All versions |
| Firmware binaries | `fw/build-release/` | Each release | Last 3 versions |
| Configuration files | `config/detector_config_*.yaml` | Each change | All versions (Git) |
| SoC system state | Full eMMC/SD backup | Before major upgrade | Last 2 snapshots |
| Frame data | `/data/frames/` on Host PC | Per policy | Per retention policy |

### 13.2 SoC Application Backup

```bash
# Create application backup (configuration and binaries)
ssh root@192.168.1.100 \
    "tar czf /tmp/soc_app_backup.tar.gz \
     /etc/detector/ /usr/bin/detector_daemon /usr/bin/detector_cli"

scp root@192.168.1.100:/tmp/soc_app_backup.tar.gz \
    backups/soc_app_$(date +%Y%m%d).tar.gz
```

### 13.3 Recovery from Unrecoverable State

1. **FPGA**: Reprogram via JTAG with the last known-good bitstream from `fpga/output/`.
2. **SoC firmware**: Restore application backup:
   ```bash
   scp backups/soc_app_YYYYMMDD.tar.gz root@192.168.1.100:/tmp/
   ssh root@192.168.1.100 "cd / && tar xzf /tmp/soc_app_YYYYMMDD.tar.gz"
   ssh root@192.168.1.100 "systemctl restart xray-detector.service"
   ```
3. **SoC OS**: Re-flash Yocto image (Section 3.1 of the Installation Guide), then redeploy firmware.
4. **Host application**: Reinstall from the release package in `release/`.

---

## 14. Security Hardening

Apply these measures before production deployment:

## Security Configuration

### Non-Root Service Account Setup

Before deploying detector_daemon, create a dedicated service account:

```bash
# Create service account (no login shell)
sudo useradd -r -s /bin/false -d /var/lib/detector -m detector
sudo mkdir -p /var/log/detector
sudo chown detector:detector /var/lib/detector /var/log/detector

# Set correct permissions on config files
sudo chmod 640 /etc/detector/detector_config.yaml
sudo chown root:detector /etc/detector/detector_config.yaml
```

### TCP Command Channel Security

The detector uses HMAC-SHA256 authentication on the command port (TCP 8001).
Configure the shared key during deployment:

```bash
# Generate a random 256-bit key
openssl rand -hex 32 > /etc/detector/command_auth_key
sudo chmod 600 /etc/detector/command_auth_key
sudo chown root:detector /etc/detector/command_auth_key
```

**IMPORTANT**: Never use default or empty keys in production. Rotate keys periodically.

### Firewall Configuration

```bash
# Allow only necessary ports
sudo ufw allow from <host_ip>/32 to any port 8000 proto udp  # Data stream
sudo ufw allow from <host_ip>/32 to any port 8001 proto tcp  # Command channel (TCP)
sudo ufw allow from 224.0.0.0/4 to any port 8002 proto udp  # Discovery (multicast)
sudo ufw deny in on eth1  # Block other inbound on data interface
sudo ufw enable
```

### 14.1 SoC Access Control

```bash
# Disable root password login (use SSH key authentication)
ssh root@192.168.1.100 \
    "sed -i 's/#PermitRootLogin.*/PermitRootLogin prohibit-password/' \
     /etc/ssh/sshd_config && systemctl restart sshd"

# Copy your SSH public key for key-based authentication
ssh-copy-id root@192.168.1.100
```

### 14.2 Network Firewall on SoC

```bash
ssh root@192.168.1.100 << 'EOF'
# Allow only necessary ports
iptables -P INPUT DROP
iptables -A INPUT -i lo -j ACCEPT
iptables -A INPUT -m state --state ESTABLISHED,RELATED -j ACCEPT
iptables -A INPUT -p tcp --dport 22 -j ACCEPT    # SSH
iptables -A INPUT -p tcp --dport 8001 -j ACCEPT  # Control (from Host PC, TCP)
# Frame data (8000) is outbound only
iptables-save > /etc/iptables.rules
EOF
```

### 14.3 Production Security Checklist

| Measure | Applied | Notes |
|---------|---------|-------|
| SSH key authentication | Required | Disable password login |
| Firewall: only ports 22, 8001 inbound | Required | Block all other inbound |
| Debug features disabled | Required | `fault_injection.enabled: false` |
| Logging to syslog | Required | Enables centralized log collection |
| Signed/encrypted FPGA bitstream | Optional | Enable in Vivado if IP protection required |

---

## 15. Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Docs Agent | Complete deployment guide with environment management, rollback, and security hardening |
| 1.0.1 | 2026-02-17 | manager-quality | Fix health_check.sh: ERROR_FLAGS address corrected from 0x04 to 0x80. Update Key Registers table: STATUS=0x20 (not 0x02), ERROR_FLAGS=0x80 (not 0x04), FRAME_COUNT_LO=0x30 (not 0x10). |
| 1.0.2 | 2026-02-17 | manager-docs (doc-approval-sprint) | Reviewed → Approved. Fix control port protocol: 8001 is TCP not UDP (section heading, ufw rule, iptables rule). |

---

## Review Record

- Date: 2026-02-17
- Reviewer: manager-quality
- Status: Approved (with corrections applied)
- TRUST 5: T:5 R:5 U:4 S:5 T:4

---

## Review Notes

**TRUST 5 Assessment**

- **Testable (5/5)**: All deployment steps include explicit verification commands with expected outputs. Health check script, DEVICE_ID reads, and integration test invocations are all concrete and runnable.
- **Readable (5/5)**: Document is well-structured with numbered sections, clear tables, and inline comments in code blocks. Deployment environments and port references are clearly documented.
- **Unified (4/5)**: Consistent use of environment names, register addresses, and IP scheme throughout. Minor inconsistency in Section 14 heading (previously referenced "UDP" for TCP-based control port, now corrected).
- **Secured (5/5)**: Comprehensive security hardening section with non-root service account, HMAC-SHA256 authentication, SSH key-only access, firewall rules, and production checklist.
- **Trackable (4/5)**: Full revision history with version entries. The deployment environments table and rollback procedures provide clear audit trail. Minor: no explicit reference to change request or issue ID.

**Corrections Applied**

1. Section "UDP Command Channel Security" heading renamed to "TCP Command Channel Security" - control port 8001 is TCP per ground truth, not UDP.
2. Section 14 ufw firewall rule: `port 8001 proto udp` corrected to `proto tcp`.
3. Section 14.2 iptables rule: `-p udp --dport 8001` corrected to `-p tcp --dport 8001`.

**Minor Observations (non-blocking)**

- The discovery multicast rule (`port 8002 proto udp`) references a port not listed in the ground truth port reference. This may be a placeholder for a future feature; no correction applied as it does not conflict with ground truth.
- Section 14.3 checklist refers to "Firewall: only ports 22, 8001 inbound" without specifying protocols; the iptables rules in 14.2 now correctly reflect TCP for both.
