# Yocto Build Guide for meta-detector

Complete guide for building X-ray Detector Panel firmware with Yocto Project Scarthgap 5.0 LTS.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Quick Start](#quick-start)
3. [Detailed Build Instructions](#detailed-build-instructions)
4. [Cross-Compilation](#cross-compilation)
5. [Flashing and Deployment](#flashing-and-deployment)
6. [Troubleshooting](#troubleshooting)

---

## Prerequisites

### Build Host Requirements

- **OS**: Ubuntu 22.04 LTS or similar Linux distribution
- **Disk Space**: At least 100 GB free space
- **RAM**: 16 GB recommended (8 GB minimum)
- **CPU**: 4+ cores recommended

### Required Packages

```bash
sudo apt-get update
sudo apt-get install -y \
    gawk wget git-core diffstat unzip texinfo gcc-multilib \
    build-essential chrpath socat cpio python3 python3-pip \
    python3-pexpect xz-utils debianutils iputils-ping \
    python3-git python3-jinja2 python3-subunit zstd \
    python3-setuptools libssl-dev localeutils \
    file libsdl1.2-dev xterm
```

### Yocto Components

- **Poky**: Scarthgap 5.0 LTS
- **Variscite BSP**: meta-variscite-bsp (imx-6.6.52-2.2.0-v1.3)
- **meta-detector**: Custom layer (included in fw/meta-detector/)

---

## Quick Start

### 1. Clone Yocto Poky

```bash
cd fw/
git clone -b scarthgap git://git.yoctoproject.org/poky.git
```

### 2. Initialize Build Environment

```bash
source poky/oe-init-build-env build-yocto
```

### 3. Add Layers

```bash
# Add Variscite BSP layer (follow Variscite documentation)
bitbake-layers add-layer ../meta-variscite-bsp

# Add meta-detector layer
bitbake-layers add-layer ../meta-detector
```

### 4. Configure local.conf

Add to `conf/local.conf`:

```bitbake
# Machine configuration
MACHINE = "imx8mp-var-dart"

# Parallel build
BB_NUMBER_THREADS = "8"
PARALLEL_MAKE = "-j8"

# Package type
PACKAGE_CLASSES = "package_ipk"

# Systemd
DISTRO_FEATURES:append = " systemd"
VIRTUAL-RUNTIME_init_manager = "systemd"
```

### 5. Build Image

```bash
# Build complete image (first build: 1-2 hours)
bitbake detector-image

# Or build only detector-daemon (faster)
bitbake detector-daemon
```

---

## Detailed Build Instructions

### Step 1: Download Variscite BSP

Follow Variscite's official guide for setting up the i.MX8M Plus BSP:

https://variwiki.com/index.php?title=Yocto_Build_Release

Summary:
```bash
git clone https://github.com/varigit/meta-variscite-bsp.git
cd meta-variscite-bsp
git checkout kirkstone  # or scarthgap when available
./mk-variscite-bsp.sh -y imx8mp-var-dart
```

### Step 2: Integrate meta-detector Layer

After BSP setup:

```bash
cd meta-variscite-bsp/sources
git clone <path-to-fw>/meta-detector.git meta-detector

cd ../
./mk-variscite-bsp.sh -y setup
```

Or manually add to `conf/bblayers.conf`:
```bitbake
BBLAYERS += " ${OE_ROOT}/layers/meta-detector "
```

### Step 3: Build detector-daemon

From the build directory:
```bash
cd build-yocto
source conf/envsetup.sh  # if not already sourced

# Clean build (first time)
bitbake detector-daemon -c cleansstate

# Build
bitbake detector-daemon
```

### Step 4: Build Complete Image

```bash
bitbake detector-image
```

Output location:
- `tmp/deploy/images/imx8mp-var-dart/`
- Key files:
  - `detector-image-imx8mp-var-dart.wic.gz` (SD card image)
  - `detector-image-imx8mp-var-dart.manifest` (installed packages)
  - `Image` (kernel image)
  - `fsl-imx8mp-var-dart.dtb` (device tree)

---

## Cross-Compilation

### Using Yocto SDK

For development outside of BitBake:

1. **Install SDK**:
   ```bash
   bitbake detector-image -c populate_sdk
   ```

2. **Install SDK on host**:
   ```bash
   tmp/deploy/sdk/oecore-x86_64-toolchain-nosysroot.sh
   ```

3. **Source SDK environment**:
   ```bash
   source /opt/oecore-x86_64/environment-setup-aarch64-poky-linux
   ```

4. **Build with CMake**:
   ```bash
   cd fw/
   mkdir build && cd build
   cmake \
       -DCMAKE_TOOLCHAIN_FILE=../meta-detector/cmake/yocto-toolchain.cmake \
       -DCMAKE_BUILD_TYPE=Release \
       -DBUILD_TESTS=OFF \
       ..
   make
   ```

### Verification

Verify cross-compiled binary:
```bash
$ file detector_daemon
detector_daemon: ELF 64-bit LSB executable, ARM aarch64, version 1 (SYSV)...
```

---

## Flashing and Deployment

### SD Card Image

1. **Write to SD card**:
   ```bash
   sudo dd if=detector-image-imx8mp-var-dart.wic.gz of=/dev/sdX bs=4M status=progress
   sudo sync
   ```

2. **Boot from SD card**:
   - Insert SD card into VAR-SOM-MX8M-PLUS
   - Set boot mode to SD card
   - Power on

### Network Boot (optional)

Configure U-Boot for TFTP boot:
```
setenv serverip <tftp-server-ip>
setenv ipaddr <board-ip>
setenv bootcmd 'tftpboot ${loadaddr} detector-image; bootm'
saveenv
```

---

## Troubleshooting

### Build Issues

**Problem**: BitBake can't find dependencies

**Solution**:
```bash
bitbake detector-daemon -c cleansstate
bitbake detector-daemon
```

**Problem**: License hash mismatch

**Solution**: Add to local.conf:
```bitbake
LICENSE_ACCEPTED = "Proprietary"
# Or add specific hash:
LIC_FILES_CHKSUM = "file://LICENSE;md5=<actual-md5>"
```

### Runtime Issues

**Problem**: detector_daemon can't access /dev/spidev0.0

**Solution**: Check permissions:
```bash
ls -l /dev/spidev0.0
# Should be: crw-rw---- 1 root detector 153, 0 ...
```

If not, re-run pkg_postinst script or manually set:
```bash
chown root:detector /dev/spidev0.0
chmod 660 /dev/spidev0.0
```

**Problem**: Service fails to start

**Solution**: Check logs:
```bash
journalctl -u detector.service -n 50
```

### Verification Commands

```bash
# Verify binary architecture
file /usr/bin/detector_daemon

# Check systemd service
systemctl status detector.service

# Verify device access
ls -l /dev/spidev0.0 /dev/video0 /dev/i2c-1

# Test configuration
/usr/bin/detector_daemon --check-config
```

---

## Acceptance Criteria (AC-FW-007)

### Verify Yocto Cross-Build

```bash
# Build completes without errors
bitbake detector-daemon
# Expected: exit code 0, binary generated

# Binary targets aarch64
file tmp/deploy/packages/aarch64/detector-daemon_1.0.0_r0_aarch64.ipk
# Expected: ELF 64-bit LSB executable, ARM aarch64

# Verify in deployed image
cat tmp/work/aarch64-poky-linux/detector-daemon/1.0.0-r0/packages-split/detector-daemon/usr/bin/detector_daemon | file -
# Expected: ELF 64-bit LSB executable, ARM aarch64
```

---

## References

- Yocto Project Documentation: https://docs.yoctoproject.org/
- Variscite i.MX8M Plus BSP: https://variwiki.com/
- SPEC-FW-001: `.moai/specs/SPEC-FW-001/spec.md`
- Architecture Design: `docs/architecture/soc-firmware-design.md`
