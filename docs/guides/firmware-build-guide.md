# SoC Firmware Cross-Compilation and Deployment Guide

**Document Version**: 1.0.0
**Status**: Draft
**Last Updated**: 2026-02-17

---

## Prerequisites

### Target Platform

| Parameter | Value |
|-----------|-------|
| Module | Variscite VAR-SOM-MX8M-PLUS |
| SoC | NXP i.MX8M Plus (Quad Cortex-A53) |
| OS | Yocto Scarthgap 5.0 LTS |
| BSP | imx-6.6.52-2.2.0-v1.3 |
| Linux Kernel | 6.6.52 |
| Architecture | AArch64 (arm64) |

### Cross-Compilation Toolchain

| Component | Version |
|-----------|---------|
| GCC ARM cross-compiler | 13.x (`aarch64-poky-linux-gcc` via Yocto SDK) |
| Yocto SDK | Scarthgap 5.0.2 |
| CMake | 3.20+ |
| GDB multiarch | Latest |

### Install CMake and Build Tools

```bash
# Ubuntu 22.04
sudo apt-get install -y cmake ninja-build gdb-multiarch
cmake --version
# Expected: cmake version 3.x (3.20 or higher)
```

---

## Setup

### Cross-Compiler Setup from Yocto SDK

The Yocto SDK provides the complete sysroot and cross-compilation toolchain for the VAR-SOM-MX8M-PLUS target.

Install the Yocto SDK (run the self-extracting installer):

```bash
./fsl-imx-xwayland-glibc-x86_64-meta-toolchain-armv8a-vfpv3-d16-toolchain-5.0.2.sh
# Accept the default installation path: /opt/poky/5.0.2
```

Source the SDK environment to activate the cross-compiler:

```bash
source /opt/poky/5.0.2/environment-setup-cortexa53-poky-linux
$CC --version
# Expected: arm-poky-linux-gnueabi-gcc (GCC) 13.x
```

Verify the sysroot is configured:

```bash
echo $SDKTARGETSYSROOT
# Expected: /opt/poky/5.0.2/sysroots/cortexa53-poky-linux
```

### CMake Toolchain File

Create `fw/cmake/arm-toolchain.cmake`:

```cmake
set(CMAKE_SYSTEM_NAME Linux)
set(CMAKE_SYSTEM_PROCESSOR aarch64)

# Use compiler from Yocto SDK environment (source environment-setup-* first)
if(DEFINED ENV{CC})
    set(CMAKE_C_COMPILER $ENV{CC})
else()
    # Fallback: Yocto Scarthgap SDK aarch64 cross-compiler
    set(CMAKE_C_COMPILER aarch64-poky-linux-gcc)
endif()

if(DEFINED ENV{CXX})
    set(CMAKE_CXX_COMPILER $ENV{CXX})
else()
    set(CMAKE_CXX_COMPILER aarch64-poky-linux-g++)
endif()

# Point to Yocto SDK sysroot
set(CMAKE_SYSROOT $ENV{SDKTARGETSYSROOT})
set(CMAKE_FIND_ROOT_PATH ${CMAKE_SYSROOT})

set(CMAKE_FIND_ROOT_PATH_MODE_PROGRAM NEVER)
set(CMAKE_FIND_ROOT_PATH_MODE_LIBRARY ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_INCLUDE ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_PACKAGE ONLY)
```

---

## Build

### Building Firmware with CMake

```bash
# Source Yocto SDK cross-compiler environment first
source /opt/poky/5.0.2/environment-setup-cortexa53-poky-linux

# Create ARM build directory
mkdir build-arm && cd build-arm

# Configure with cross-compilation toolchain
cmake .. -DCMAKE_TOOLCHAIN_FILE=../cmake/arm-toolchain.cmake \
         -DCMAKE_BUILD_TYPE=Release

# Build with 8 parallel jobs
make -j8
```

The output binaries:
- `build-arm/detector_daemon` — main background service
- `build-arm/detector_cli` — command-line diagnostic tool

### Yocto Recipe Creation

Create a BitBake recipe at `meta-xray-detector/recipes-detector/xray-detector-fw/xray-detector-fw_0.1.bb`:

```bitbake
SUMMARY = "X-ray Detector Firmware Daemon"
DESCRIPTION = "SoC firmware daemon for X-ray detector panel control"
LICENSE = "Proprietary"
LIC_FILES_CHKSUM = "file://LICENSE;md5=XXXX"

SRC_URI = "git://github.com/holee9/system-emul-sim.git;branch=main;protocol=https"
SRCREV = "${AUTOREV}"

S = "${WORKDIR}/git/fw"

inherit cmake systemd

DEPENDS = "libv4l2 libcamera"

SYSTEMD_SERVICE:${PN} = "xray-detector.service"
SYSTEMD_AUTO_ENABLE:${PN} = "enable"

do_install:append() {
    install -d ${D}${systemd_system_unitdir}
    install -m 0644 ${S}/systemd/xray-detector.service \
        ${D}${systemd_system_unitdir}/xray-detector.service
    install -d ${D}${sysconfdir}/detector
    install -m 0644 ${WORKDIR}/git/config/detector_config.yaml \
        ${D}${sysconfdir}/detector/detector_config.yaml
}
```

### Building Yocto Image

```bash
# Source the Yocto build environment
source setup-environment build-imx8mp

# Build the minimal image for VAR-SOM-MX8M-PLUS
MACHINE=imx8mp-var-dart bitbake core-image-minimal
```

Build output is located at `build-imx8mp/tmp/deploy/images/imx8mp-var-dart/`.

---

## Flashing to VAR-SOM-MX8M-PLUS

### Flash the Yocto Image to eMMC

Connect the VAR-SOM-MX8M-PLUS to the host PC via USB OTG. Boot into UBoot fastboot mode, then:

```bash
dd if=core-image-minimal-imx8mp-var-dart.wic \
   of=/dev/mmcblk0 \
   bs=1M \
   status=progress
```

For flashing via the Variscite recovery tool (recommended):

```bash
sudo uuu -b emmc_all imx-boot-imx8mp-var-dart.bin-flash_evk \
    core-image-minimal-imx8mp-var-dart.wic
```

### UART Console Access

Connect a USB-to-UART adapter to the VAR-SOM-MX8M-PLUS debug UART header. Settings: **115200 baud, 8 data bits, no parity, 1 stop bit (8N1)**.

```bash
# Linux
minicom -D /dev/ttyUSB0 -b 115200

# Windows (PuTTY)
# Port: COMx, Baud: 115200, Data bits: 8, Stop bits: 1, Parity: None
```

---

## Test

### Verifying Hardware After Boot

After the board boots to Linux, verify the system configuration:

```bash
# Verify kernel version
uname -r
# Expected: 6.6.52

# Check i2c devices
# BQ40z50 battery fuel gauge at 0x0b on bus 0
i2cdetect -y 0
# Expected: address 0x0b visible

# BMI160 IMU at 0x68 on bus 7
i2cdetect -y 7
# Expected: address 0x68 visible

# Verify Ethernet chip
lspci -nn | grep -i ethernet
# Expected: shows 2.5GbE Ethernet controller

# Check CSI-2 device availability
ls /dev/video*
# Expected: /dev/video0 (MIPI CSI-2 RX)

# Verify SPI device
ls /dev/spidev*
# Expected: /dev/spidev0.0 (FPGA SPI slave)
```

### Remote Debugging with GDB Server

On the target board, start GDB server:

```bash
gdbserver :2345 ./xray-detector-fw
# Output: Listening on port 2345
```

On the host development machine, connect GDB:

```bash
aarch64-poky-linux-gdb -x gdb/remote.gdb
```

Where `gdb/remote.gdb` contains:

```
file build-arm/detector_daemon
target remote 192.168.1.100:2345
set sysroot /opt/poky/5.0.2/sysroots/cortexa53-poky-linux
break main
continue
```

### systemd Service Management

```bash
# Enable firmware daemon to start on boot
systemctl enable xray-detector.service

# Start service immediately
systemctl start xray-detector.service

# Check service status
systemctl status xray-detector.service

# Follow live logs
journalctl -fu xray-detector.service

# Restart after firmware update
systemctl restart xray-detector.service
```

---

## Troubleshooting

### Cross-Compilation Issues

| Issue | Cause | Solution |
|-------|-------|---------|
| `arm-poky-linux-gnueabi-gcc: not found` | SDK not sourced | Run `source /opt/poky/5.0.2/environment-setup-*` |
| `cannot find -lv4l2` | Missing sysroot library | Verify `CMAKE_SYSROOT` points to SDK sysroot |
| CMake generates x86 binaries | Toolchain file not used | Pass `-DCMAKE_TOOLCHAIN_FILE=../cmake/arm-toolchain.cmake` |
| Linker error: undefined reference | Wrong sysroot | Ensure all `find_library()` calls resolve to ARM sysroot |

### Boot and Runtime Issues

| Issue | Cause | Solution |
|-------|-------|---------|
| Board does not boot | Corrupted image | Re-flash with `uuu` tool |
| `xray-detector.service` fails to start | Binary not found or permission denied | Check binary path and execute permissions with `chmod +x` |
| `/dev/spidev0.0` missing | SPI device tree overlay not enabled | Add SPI overlay to device tree, rebuild Yocto image |
| `i2cdetect -y 0` does not show 0x0b | I2C bus device tree issue | Check BSP imx8mp device tree for I2C bus configuration |

---

## Common Errors

| Error | Context | Meaning | Fix |
|-------|---------|---------|-----|
| `cannot execute binary file: Exec format error` | Running ARM binary on host | Binary is ARM64, not x86_64 | Cross-compile correctly; use QEMU for host testing |
| `error: unknown target triple 'aarch64'` | CMake | Wrong compiler selected | Source SDK environment before running CMake |
| `No such file: /dev/spidev0.0` | Runtime | SPI not enabled in device tree | Check and apply correct device tree overlay |
| `SIGILL` on target | Mismatched CPU flags | Binary compiled for wrong ARM variant | Ensure toolchain targets `cortexa53` |
| `GDB: Remote connection closed` | GDB server | Network interruption or binary crashed | Check SSH/network, restart gdbserver |

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent | Complete SoC firmware build and deployment guide |
