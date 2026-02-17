# SoC Firmware Build Guide

**Project**: X-ray Detector Panel System
**Target Platform**: NXP i.MX8M Plus (Quad Cortex-A53, Linux 5.15+)
**Version**: 1.0.0
**Last Updated**: 2026-02-17

---

## 1. Overview

This guide covers cross-compilation, testing, and deployment of the SoC Controller firmware for the X-ray Detector Panel System. The firmware runs as a Linux user-space daemon (`detector_daemon`) on the NXP i.MX8M Plus SoC.

### 1.1 Firmware Responsibilities

| Module | Function | Source |
|--------|----------|--------|
| CSI-2 RX Driver | V4L2 frame capture from FPGA | `hal/csi2_rx.c` |
| SPI Master | FPGA register read/write | `hal/spi_master.c` |
| Ethernet TX | UDP frame streaming to Host PC | `hal/eth_tx.c` |
| Sequence Engine | Frame scan control FSM | `sequence_engine.c` |
| Frame Manager | DDR4 buffer lifecycle | `frame_manager.c` |
| Command Protocol | Host command handling | `protocol/command_protocol.c` |

### 1.2 Build Options

| Build Type | Compiler | Target | Purpose |
|-----------|---------|--------|---------|
| Cross-compile | `aarch64-poky-linux-gcc` | i.MX8M Plus (ARM64) | Production deployment |
| Host build | `gcc` / `clang` | x86-64 (development PC) | Unit testing |
| Docker build | Docker container | ARM64 | Reproducible CI builds |

---

## 2. Prerequisites

### 2.1 Required Software

| Software | Version | Purpose |
|----------|---------|---------|
| CMake | 3.20+ | Build system |
| GCC / Clang | 11+ | Host compilation for tests |
| Yocto SDK | Kirkstone (5.15) | Cross-compilation toolchain |
| CMocka or Unity | Latest | Unit test framework |
| gcov / lcov | Latest | Code coverage |

### 2.2 Install Build Tools

**Ubuntu 22.04+**:
```bash
sudo apt-get update
sudo apt-get install -y \
    build-essential \
    cmake \
    git \
    pkg-config \
    libyaml-dev \
    libcmocka-dev \
    lcov \
    sshpass
```

### 2.3 Install Cross-Compilation Toolchain

**Option A: Yocto SDK (Recommended)**

Obtain the i.MX8M Plus Yocto SDK from NXP or build it from the Yocto BSP:

```bash
# Install SDK
chmod +x fsl-imx-xwayland-glibc-x86_64-meta-toolchain-cortexa53-crypto-toolchain-5.15-kirkstone.sh
sudo ./fsl-imx-xwayland-glibc-x86_64-meta-toolchain-cortexa53-crypto-toolchain-5.15-kirkstone.sh

# Default install location: /opt/fsl-imx-xwayland/5.15-kirkstone/

# Source the environment (required before every build session)
source /opt/fsl-imx-xwayland/5.15-kirkstone/environment-setup-cortexa53-crypto-poky-linux

# Verify cross-compiler
$CC --version
echo $CROSS_COMPILE
aarch64-poky-linux-gcc --version
```

**Option B: Linaro Toolchain**

```bash
# Download and extract
wget https://releases.linaro.org/components/toolchain/binaries/latest-7/aarch64-linux-gnu/gcc-linaro-7.5.0-2019.12-x86_64_aarch64-linux-gnu.tar.xz
tar xf gcc-linaro-7.5.0-2019.12-x86_64_aarch64-linux-gnu.tar.xz

# Add to PATH
export PATH=$PWD/gcc-linaro-7.5.0-2019.12-x86_64_aarch64-linux-gnu/bin:$PATH
export CROSS_COMPILE=aarch64-linux-gnu-

# Verify
aarch64-linux-gnu-gcc --version
```

---

## 3. Source Code Structure

```
fw/
  CMakeLists.txt                    # Top-level CMake build
  src/
    main.c                          # Entry point, daemon initialization
    sequence_engine.c/.h            # Scan sequence control FSM
    frame_manager.c/.h              # Frame buffer lifecycle
    hal/
      csi2_rx.c/.h                  # V4L2 CSI-2 RX wrapper
      spi_master.c/.h               # SPI register read/write via spidev
      eth_tx.c/.h                   # 10 GbE UDP TX
    config/
      config_loader.c/.h            # YAML configuration parser
    protocol/
      frame_header.c/.h             # Frame header encode/decode
      command_protocol.c/.h         # Host command handling
    util/
      crc16.c/.h                    # CRC-16/CCITT implementation
      log.c/.h                      # Structured logging
  tests/
    test_main.c                     # Test runner entry point
    test_sequence_engine.c          # Sequence engine unit tests
    test_frame_manager.c            # Frame manager unit tests
    test_spi_protocol.c             # SPI protocol unit tests
    test_frame_header.c             # Frame header encode/decode tests
    test_crc16.c                    # CRC-16 reference vector tests
    test_config_loader.c            # Config parsing tests
  toolchain/
    imx8mp-toolchain.cmake          # Cross-compilation toolchain file
  config/
    detector.service                # systemd service file
    detector_config.yaml            # Runtime configuration
  Dockerfile                        # Docker build environment
```

---

## 4. Cross-Compilation (ARM64 Target)

### 4.1 CMake Toolchain File

The cross-compilation toolchain file (`toolchain/imx8mp-toolchain.cmake`):

```cmake
# Cross-compilation toolchain for NXP i.MX8M Plus
set(CMAKE_SYSTEM_NAME Linux)
set(CMAKE_SYSTEM_PROCESSOR aarch64)

# Toolchain prefix
set(CROSS_COMPILE aarch64-poky-linux-)

# Set compilers (Yocto SDK environment variables take precedence)
if(DEFINED ENV{CC})
    set(CMAKE_C_COMPILER $ENV{CC})
else()
    set(CMAKE_C_COMPILER ${CROSS_COMPILE}gcc)
endif()

# Sysroot (from Yocto SDK)
if(DEFINED ENV{SDKTARGETSYSROOT})
    set(CMAKE_SYSROOT $ENV{SDKTARGETSYSROOT})
endif()

# Search settings
set(CMAKE_FIND_ROOT_PATH_MODE_PROGRAM NEVER)
set(CMAKE_FIND_ROOT_PATH_MODE_LIBRARY ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_INCLUDE ONLY)
```

### 4.2 Build for Target

```bash
cd system-emul-sim/fw

# Source cross-compiler environment
source /opt/fsl-imx-xwayland/5.15-kirkstone/environment-setup-cortexa53-crypto-poky-linux

# Create build directory
mkdir -p build-arm64 && cd build-arm64

# Configure with cross-compilation
cmake -DCMAKE_TOOLCHAIN_FILE=../toolchain/imx8mp-toolchain.cmake \
      -DCMAKE_BUILD_TYPE=Release \
      ..

# Build
make -j$(nproc)

# Output binaries
ls -la detector_daemon detector_cli
file detector_daemon
# Expected: ELF 64-bit LSB executable, ARM aarch64
```

### 4.3 Build Configuration Options

| CMake Option | Default | Description |
|-------------|---------|-------------|
| `CMAKE_BUILD_TYPE` | Release | Debug, Release, RelWithDebInfo |
| `BUILD_TESTS` | OFF | Build unit tests (host only) |
| `ENABLE_SANITIZERS` | OFF | Enable AddressSanitizer (debug) |
| `ENABLE_COVERAGE` | OFF | Enable gcov coverage (host only) |
| `LOG_LEVEL` | INFO | DEBUG, INFO, WARN, ERROR |

---

## 5. Host Build (Unit Testing)

### 5.1 Build for Host

Unit tests run on the development PC (x86-64), not on the SoC:

```bash
cd system-emul-sim/fw

# Create host build directory
mkdir -p build-host && cd build-host

# Configure for host (no cross-compilation)
cmake -DCMAKE_BUILD_TYPE=Debug \
      -DBUILD_TESTS=ON \
      -DENABLE_COVERAGE=ON \
      ..

# Build
make -j$(nproc)
```

### 5.2 Run Unit Tests

```bash
cd fw/build-host

# Run all tests
ctest --output-on-failure

# Run specific test
./test_crc16
./test_frame_header
./test_sequence_engine

# Run tests with verbose output
ctest -V
```

### 5.3 Test Coverage

```bash
cd fw/build-host

# Build with coverage
cmake -DCMAKE_BUILD_TYPE=Debug -DBUILD_TESTS=ON -DENABLE_COVERAGE=ON ..
make -j$(nproc)

# Run tests (generates .gcda files)
ctest

# Generate coverage report
lcov --capture --directory . --output-file coverage.info
lcov --remove coverage.info '/usr/*' --output-file coverage.info
lcov --remove coverage.info '*/tests/*' --output-file coverage.info

# Generate HTML report
genhtml coverage.info --output-directory coverage-report

# View report
open coverage-report/index.html
```

**Coverage Target**: 80%+ per module

### 5.4 Unit Test Structure

Tests use CMocka framework with mock HAL interfaces:

```c
// test_sequence_engine.c
#include <setjmp.h>
#include <cmocka.h>
#include "sequence_engine.h"

// Mock SPI write function
int __wrap_fpga_reg_write(int fd, uint8_t addr, uint16_t data) {
    check_expected(addr);
    check_expected(data);
    return (int)mock();
}

static void test_start_scan_writes_control_register(void **state) {
    // Expect SPI write to CONTROL register (0x00) with start_scan bit
    expect_value(__wrap_fpga_reg_write, addr, 0x00);
    expect_value(__wrap_fpga_reg_write, data, 0x0001);
    will_return(__wrap_fpga_reg_write, 0);

    int result = sequence_engine_start(SCAN_MODE_SINGLE);
    assert_int_equal(result, 0);
}

int main(void) {
    const struct CMUnitTest tests[] = {
        cmocka_unit_test(test_start_scan_writes_control_register),
        // ... more tests
    };
    return cmocka_run_group_tests(tests, NULL, NULL);
}
```

---

## 6. Docker Build (Reproducible CI)

### 6.1 Dockerfile

```dockerfile
FROM ubuntu:22.04

RUN apt-get update && apt-get install -y \
    build-essential \
    cmake \
    git \
    pkg-config \
    libyaml-dev \
    libcmocka-dev \
    lcov \
    wget

# Install cross-compiler (Linaro toolchain)
RUN wget -q https://releases.linaro.org/components/toolchain/binaries/latest-7/aarch64-linux-gnu/gcc-linaro-7.5.0-2019.12-x86_64_aarch64-linux-gnu.tar.xz && \
    tar xf gcc-linaro-*.tar.xz -C /opt/ && \
    rm gcc-linaro-*.tar.xz

ENV PATH="/opt/gcc-linaro-7.5.0-2019.12-x86_64_aarch64-linux-gnu/bin:${PATH}"
ENV CROSS_COMPILE=aarch64-linux-gnu-

WORKDIR /workspace
```

### 6.2 Build with Docker

```bash
cd system-emul-sim

# Build Docker image
docker build -t xray-fw-build -f fw/Dockerfile .

# Cross-compile firmware
docker run --rm -v $(pwd)/fw:/workspace xray-fw-build \
    bash -c "mkdir -p build && cd build && \
    cmake -DCMAKE_TOOLCHAIN_FILE=../toolchain/imx8mp-toolchain.cmake .. && \
    make -j$(nproc)"

# Run host tests
docker run --rm -v $(pwd)/fw:/workspace xray-fw-build \
    bash -c "mkdir -p build-test && cd build-test && \
    cmake -DBUILD_TESTS=ON .. && make -j$(nproc) && ctest -V"
```

---

## 7. Deployment to SoC

### 7.1 Network Setup

```bash
# Set SoC IP (default from detector_config.yaml)
export SOC_IP=192.168.1.100
export SOC_USER=root

# Verify connectivity
ping -c 3 ${SOC_IP}
ssh ${SOC_USER}@${SOC_IP} "uname -a"
```

### 7.2 Deploy Binaries

```bash
# Deploy firmware binaries
scp fw/build-arm64/detector_daemon ${SOC_USER}@${SOC_IP}:/usr/bin/
scp fw/build-arm64/detector_cli ${SOC_USER}@${SOC_IP}:/usr/bin/

# Deploy configuration
ssh ${SOC_USER}@${SOC_IP} "mkdir -p /etc/detector"
scp config/detector_config.yaml ${SOC_USER}@${SOC_IP}:/etc/detector/

# Deploy systemd service
scp fw/config/detector.service ${SOC_USER}@${SOC_IP}:/etc/systemd/system/

# Set permissions
ssh ${SOC_USER}@${SOC_IP} "chmod +x /usr/bin/detector_daemon /usr/bin/detector_cli"
```

### 7.3 Systemd Service Management

The firmware runs as a systemd service:

```ini
# /etc/systemd/system/detector.service
[Unit]
Description=X-ray Detector Daemon
After=network.target

[Service]
Type=simple
ExecStart=/usr/bin/detector_daemon --config /etc/detector/detector_config.yaml
Restart=on-failure
RestartSec=5
User=root

[Install]
WantedBy=multi-user.target
```

```bash
# Enable and start the service
ssh ${SOC_USER}@${SOC_IP} "systemctl daemon-reload"
ssh ${SOC_USER}@${SOC_IP} "systemctl enable detector"
ssh ${SOC_USER}@${SOC_IP} "systemctl start detector"

# Check status
ssh ${SOC_USER}@${SOC_IP} "systemctl status detector"

# View logs
ssh ${SOC_USER}@${SOC_IP} "journalctl -u detector -f"

# Restart after update
ssh ${SOC_USER}@${SOC_IP} "systemctl restart detector"
```

### 7.4 Deployment Script

Automate deployment with a shell script:

```bash
#!/bin/bash
# deploy.sh - Deploy firmware to SoC
set -e

SOC_IP=${1:-192.168.1.100}
SOC_USER=${2:-root}
BUILD_DIR="fw/build-arm64"

echo "Deploying to ${SOC_USER}@${SOC_IP}..."

# Stop service
ssh ${SOC_USER}@${SOC_IP} "systemctl stop detector 2>/dev/null || true"

# Copy binaries
scp ${BUILD_DIR}/detector_daemon ${SOC_USER}@${SOC_IP}:/usr/bin/
scp ${BUILD_DIR}/detector_cli ${SOC_USER}@${SOC_IP}:/usr/bin/

# Copy config (only if changed)
scp config/detector_config.yaml ${SOC_USER}@${SOC_IP}:/etc/detector/

# Restart service
ssh ${SOC_USER}@${SOC_IP} "systemctl start detector"

# Verify
sleep 2
ssh ${SOC_USER}@${SOC_IP} "systemctl status detector --no-pager"

echo "Deployment complete!"
```

---

## 8. Runtime Verification

### 8.1 Basic Health Checks

```bash
# Check daemon is running
ssh root@${SOC_IP} "systemctl is-active detector"
# Expected: active

# Check FPGA communication (read DEVICE_ID register)
ssh root@${SOC_IP} "detector_cli read-reg 0xF0"
# Expected: 0xA735

# Check FPGA status
ssh root@${SOC_IP} "detector_cli status"
# Expected: IDLE state, no errors

# Check network interface
ssh root@${SOC_IP} "ip link show eth1"  # 10 GbE interface
```

### 8.2 SPI Communication Test

```bash
# Write and read-back timing register
ssh root@${SOC_IP} "detector_cli write-reg 0x20 1000"  # gate_on_us = 1000
ssh root@${SOC_IP} "detector_cli read-reg 0x20"
# Expected: 1000

# Read all status registers
ssh root@${SOC_IP} "detector_cli dump-regs"
```

### 8.3 CSI-2 RX Verification

```bash
# Check V4L2 device
ssh root@${SOC_IP} "v4l2-ctl --list-devices"
# Expected: /dev/video0

# Query format
ssh root@${SOC_IP} "v4l2-ctl -d /dev/video0 --get-fmt-video"
# Expected: Width=2048, Height=2048, Pixel Format=Y16

# Capture test frame
ssh root@${SOC_IP} "detector_cli capture-frame --output /tmp/test_frame.raw"
```

### 8.4 Network Streaming Test

```bash
# On Host PC: Start UDP listener
nc -u -l 8000 > /dev/null &

# On SoC: Send test frame
ssh root@${SOC_IP} "detector_cli test-stream --host 192.168.1.1 --frames 10"

# Verify packet reception
# Check Host PC received data
```

---

## 9. Troubleshooting

### 9.1 Build Issues

| Issue | Cause | Solution |
|-------|-------|---------|
| "Cannot find cross-compiler" | Yocto SDK not sourced | Run `source /opt/fsl-imx-xwayland/.../environment-setup-*` |
| "CMake Error: Could not find toolchain file" | Wrong path | Verify `toolchain/imx8mp-toolchain.cmake` exists |
| "undefined reference to `yaml_parser_*`" | libyaml not installed | Install in sysroot: `apt install libyaml-dev` |
| Link error: "incompatible architecture" | Host build mixed with cross-build | Clean build: `rm -rf build && mkdir build` |

### 9.2 Deployment Issues

| Issue | Cause | Solution |
|-------|-------|---------|
| SSH connection refused | SoC not booted or wrong IP | Verify SoC power, check IP with `arp -a` |
| "Permission denied" on `/dev/spidev0.0` | Missing permissions | Run as root or add udev rule |
| "No such device: /dev/video0" | CSI-2 driver not loaded | Check device tree, verify `imx8-mipi-csi2` module |
| "Network unreachable" for 10 GbE | NIC not configured | Check `ip addr`, configure static IP |
| Service fails to start | Missing config file | Verify `/etc/detector/detector_config.yaml` exists |

### 9.3 Runtime Issues

| Issue | Cause | Solution |
|-------|-------|---------|
| Frame drops > 0.01% | Buffer overrun, slow TX | Increase buffer count, check network bandwidth |
| SPI timeout | FPGA not responding | Check SPI wiring, verify FPGA is programmed |
| CSI-2 capture errors | D-PHY instability | Check cable, reduce lane speed, verify FPGA CSI-2 TX |
| High CPU usage | SPI polling too fast | Verify 100 us polling interval, check thread priorities |

---

## 10. CMakeLists.txt Reference

```cmake
cmake_minimum_required(VERSION 3.20)
project(detector_firmware VERSION 1.0.0 LANGUAGES C)

set(CMAKE_C_STANDARD 11)
set(CMAKE_C_STANDARD_REQUIRED ON)

# Compiler flags
add_compile_options(-Wall -Wextra -Wpedantic)

if(CMAKE_BUILD_TYPE STREQUAL "Debug")
    add_compile_options(-g -O0)
    if(ENABLE_SANITIZERS)
        add_compile_options(-fsanitize=address -fno-omit-frame-pointer)
        add_link_options(-fsanitize=address)
    endif()
    if(ENABLE_COVERAGE)
        add_compile_options(--coverage -fprofile-arcs -ftest-coverage)
        add_link_options(--coverage)
    endif()
else()
    add_compile_options(-O2)
endif()

# Source files
set(FW_SOURCES
    src/main.c
    src/sequence_engine.c
    src/frame_manager.c
    src/hal/csi2_rx.c
    src/hal/spi_master.c
    src/hal/eth_tx.c
    src/config/config_loader.c
    src/protocol/frame_header.c
    src/protocol/command_protocol.c
    src/util/crc16.c
    src/util/log.c
)

# Main daemon executable
add_executable(detector_daemon ${FW_SOURCES})
target_include_directories(detector_daemon PRIVATE src)
target_link_libraries(detector_daemon PRIVATE pthread yaml)

# CLI tool
add_executable(detector_cli src/cli/detector_cli.c src/hal/spi_master.c src/util/crc16.c)
target_include_directories(detector_cli PRIVATE src)

# Unit tests (host build only)
if(BUILD_TESTS AND NOT CMAKE_CROSSCOMPILING)
    enable_testing()

    set(TEST_SOURCES
        tests/test_main.c
        tests/test_crc16.c
        tests/test_frame_header.c
        tests/test_sequence_engine.c
        tests/test_frame_manager.c
        tests/test_spi_protocol.c
        tests/test_config_loader.c
    )

    # Library for testable code (excludes main.c and HAL)
    add_library(fw_testable STATIC
        src/sequence_engine.c
        src/frame_manager.c
        src/protocol/frame_header.c
        src/protocol/command_protocol.c
        src/config/config_loader.c
        src/util/crc16.c
        src/util/log.c
    )
    target_include_directories(fw_testable PUBLIC src)

    add_executable(fw_tests ${TEST_SOURCES})
    target_link_libraries(fw_tests PRIVATE fw_testable cmocka yaml)

    add_test(NAME firmware_tests COMMAND fw_tests)
endif()
```

---

## 11. Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (architect) | Initial firmware build guide |

---
