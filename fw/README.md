# X-ray Detector Panel - SoC Firmware

## Overview

The SoC Controller firmware runs on NXP i.MX8M Plus, bridging FPGA and Host PC for real-time X-ray detector panel data acquisition and streaming.

### System Role

The firmware is the critical middle layer in the three-tier architecture:

```
[FPGA] --CSI-2 MIPI--> [i.MX8M Plus SoC] --UDP--> [Host PC]
                            |
                            +--SPI--> [FPGA Control]
```

**Responsibilities**:
- Receive RAW16 pixel frames via CSI-2 MIPI 4-lane D-PHY (V4L2)
- Control FPGA via SPI Master (50 MHz, 32-bit transaction format)
- Stream frames to Host via 10 GbE UDP (port 8000)
- Execute Host commands via UDP control channel (port 8001)
- Monitor battery (TI BQ40z50), IMU (Bosch BMI160), GPIO (NXP PCA9534)

### Technology Stack

| Component | Technology | Version |
|-----------|-----------|---------|
| SoC | NXP i.MX8M Plus | Variscite VAR-SOM-MX8M-PLUS (DART) |
| OS | Yocto Project | Scarthgap 5.0 LTS |
| Kernel | Linux | 6.6.52 (LTS until 2026.12) |
| BSP | Variscite | imx-6.6.52-2.2.0-v1.3 |
| Language | C | C11 standard |
| Build | CMake | Cross-compilation toolchain |

## Features

### Core Features

- **CSI-2 Frame Reception**: V4L2-based RAW16 pixel capture with zero-copy DMA buffers
- **FPGA SPI Control**: 32-bit register read/write with write verification
- **High-Speed Streaming**: 10 GbE UDP frame fragmentation and transmission
- **Command Protocol**: HMAC-SHA256 authenticated Host commands on port 8001
- **Security Architecture**: HMAC-SHA256 message authentication + privilege drop defense-in-depth
- **Sequence Engine**: State machine (IDLE -> CONFIGURE -> ARM -> SCANNING -> STREAMING -> COMPLETE)
- **Frame Management**: 4-buffer DDR4 ring (ping-pong + double-buffering)
- **Error Recovery**: Automatic V4L2 pipeline restart and FPGA error recovery

### Hardware Integration

| Peripheral | Interface | Driver/Kernel | Status |
|------------|-----------|---------------|--------|
| CSI-2 RX | MIPI 4-lane D-PHY | V4L2 (`/dev/video0`) | Included in BSP |
| SPI Master | SPI Mode 0, 50 MHz | spidev (`/dev/spidev0.0`) | Included in BSP |
| 10 GbE TX | PCIe NIC | ixgbe/mlx5 kernel driver | To be verified |
| Battery | SMBus (I2C addr 0x0b) | BQ40z50 (ported from 4.4) | Port required |
| IMU | I2C7 (addr 0x68) | bmi160_i2c IIO | Included in kernel 6.6 |
| GPIO Expander | I2C | gpio-pca953x | Included in kernel 6.6 |

## Building

### Prerequisites

- Yocto Scarthgap 5.0 LTS build environment
- Variscite BSP layer: `meta-variscite-bsp` (imx-6.6.52-2.2.0-v1.3)
- Cross-compiler: `aarch64-poky-linux-gcc`
- CMake 3.20+
- libyaml-dev

### Build Commands

```bash
# From Yocto environment
source /path/to/yocto/sdk/environment-setup-aarch64-poky-linux

# Create build directory
mkdir -p build && cd build

# Configure with CMake toolchain file
cmake -DCMAKE_TOOLCHAIN_FILE=../cmake/aarch64-yocto-toolchain.cmake ..

# Build
make -j$(nproc)

# Run tests
ctest --output-on-failure
```

### Yocto Recipe

See `deploy/detector-daemon_1.0.bb` for BitBake recipe.

```bash
# From Yocto build directory
bitbake detector-daemon
bitbake detector-image  # Build complete target image
```

## Testing

### Unit Tests

```bash
# Build with coverage
cmake -DCMAKE_BUILD_TYPE=Debug -DENABLE_COVERAGE=ON ..
make && make test

# Generate coverage report
make coverage
lcov --capture --directory . --output-file coverage.info
genhtml coverage.info --output-directory coverage_html
```

### Integration Tests

Integration tests are defined in the test plan (see `docs/testing/test-plan.md`):

- IT-01: SPI register communication
- IT-02: CSI-2 frame capture
- IT-03: UDP frame streaming
- IT-04: Sequence engine full cycle
- IT-05: Error recovery
- IT-06: Frame drop rate
- IT-07: Yocto cross-build
- IT-08: BQ40z50 battery driver
- IT-09: BMI160 IMU HAL (Phase 3)
- IT-10: PCA9534 GPIO HAL (Phase 2)

### Manual Testing

```bash
# Run daemon on target hardware
ssh detector@imx8mp-evk
detector_daemon --config /etc/detector/detector_config.yaml

# Send commands from Host SDK
./detector_cli start_scan --mode continuous
./detector_cli get_status
./detector_cli stop_scan
```

## Deployment

### Target Image

Yocto recipe `detector-image.bb` generates a complete target image including:

- Root filesystem with detector_daemon
- Runtime dependencies: `v4l-utils`, `spidev`, `iproute2`, `ethtool`, `libyaml`
- Systemd service unit: `detector.service`
- Configuration schema: `/etc/detector/detector_config.yaml`

### Systemd Service

```bash
# Enable and start service
systemctl enable detector.service
systemctl start detector.service

# Check status
systemctl status detector.service

# View logs
journalctl -u detector.service -f
```

### Configuration

The firmware loads configuration from `detector_config.yaml` (see `config/detector_config.yaml`).

Hot-swappable parameters (no scan restart required):
- Frame rate
- Network destination
- Log level

Cold parameters (require scan stop):
- Resolution
- Bit depth
- CSI-2 lane speed

## Architecture

### Module Overview

| Module | File | Function |
|--------|------|----------|
| CSI-2 RX Driver | `hal/csi2_rx.c` | V4L2 interface, DMA setup, frame capture |
| SPI Master | `hal/spi_master.c` | FPGA register read/write, polling |
| Ethernet TX | `hal/eth_tx.c` | UDP packet transmission, frame fragmentation |
| Sequence Engine | `sequence_engine.c` | Frame scan control FSM |
| Frame Manager | `frame_manager.c` | DDR4 4-buffer ring management |
| Command Protocol | `protocol/command_protocol.c` | Host command handling (HMAC-SHA256) |
| Battery Driver | `hal/bq40z50_driver.c` | TI BQ40z50 battery gauge (ported from 4.4) |
| IMU HAL | `hal/imu_hal.c` | Bosch BMI160 via IIO subsystem |
| GPIO HAL | `hal/gpio_hal.c` | NXP PCA9534 via sysfs GPIO |
| Health Monitor | `health_monitor.c` | Watchdog, error tracking |
| Main Daemon | `main.c` | Initialization, thread management |

### Security Architecture

The firmware implements defense-in-depth security with multiple layers:

**Message Authentication (HMAC-SHA256)**:
- Pre-shared key authentication for Host commands
- HMAC-SHA256 over command header + payload
- Silent discard on authentication failure
- Replay protection via monotonic sequence numbers

**Privilege Separation**:
- Non-root service account (`detector`)
- Minimal Linux capabilities (CAP_NET_BIND_SERVICE, CAP_SYS_NICE)
- Systemd hardening (NoNewPrivileges, ProtectSystem, ProtectHome)

**Secure Key Storage**:
- HMAC key in `/etc/detector/hmac_key` (root:detector 0400)
- udev rules for device permission management

For detailed security architecture, see [SECURITY_IMPROVEMENTS.md](SECURITY_IMPROVEMENTS.md).

For detailed architecture, see [ARCHITECTURE.md](ARCHITECTURE.md).

### Data Flow

```
1. CSI-2 RX receives frame -> Frame Manager (buffer acquisition)
2. Frame Manager delivers buffer -> Ethernet TX
3. Ethernet TX fragments and sends UDP packets to Host
4. SPI Control polls FPGA STATUS -> Sequence Engine
5. Command Protocol receives Host commands -> Sequence Engine
6. Sequence Engine coordinates all modules via state machine
```

## Development Methodology

Per `.moai/config/sections/quality.yaml`:

- **New firmware code**: TDD (RED-GREEN-REFACTOR)
- **HAL integration with BSP**: DDD (ANALYZE-PRESERVE-IMPROVE)
- **Battery driver port**: DDD (ANALYZE kernel 4.4 driver, PRESERVE behavior, IMPROVE for 6.6 API)
- **IMU HAL**: TDD (kernel driver included, HAL is new userspace code)
- **GPIO HAL**: TDD (kernel driver included, HAL is new userspace code)

Coverage target: 85%+ per module.

## Documentation

### Specification

- [SPEC-FW-001](../.moai/specs/SPEC-FW-001/spec.md): SoC Firmware Requirements Specification

### Design Documents

- [../docs/architecture/soc-firmware-design.md](../docs/architecture/soc-firmware-design.md): Detailed architecture design
- [SECURITY_IMPROVEMENTS.md](SECURITY_IMPROVEMENTS.md): Security architecture and implementation details

### Test Documentation

- [../docs/testing/test-plan.md](../docs/testing/test-plan.md): Integration test scenarios
- [../docs/testing/unit-test-plan.md](../docs/testing/unit-test-plan.md): Unit test coverage requirements

## Troubleshooting

### Common Issues

**Issue**: CSI-2 capture errors (EAGAIN, EIO)
- **Solution**: V4L2 pipeline restart mechanism (REQ-FW-061) automatically recovers
- **Logs**: `journalctl -u detector.service | grep CSI2`

**Issue**: High frame drop rate (> 0.01%)
- **Check**: 10 GbE link speed: `ethtool eth0`
- **Check**: CSI-2 lane speed (400 Mbps stable, 800 Mbps debugging)
- **Check**: Buffer allocation: `cat /proc/meminfo | grep Huge`

**Issue**: SPI timeout errors
- **Check**: FPGA STATUS register read latency (target: < 10 ms)
- **Check**: SPI speed: `cat /sys/kernel/debug/spi/spi0/spi0.0`
- **Solution**: Reduce SPI speed to 25 MHz if unstable

**Issue**: BQ40z50 battery driver fails to load
- **Check**: I2C bus scan: `i2cdetect -y 1`
- **Check**: SMBus address: should be 0x0b (default BQ40z50)
- **Solution**: Port kernel 4.4 driver to 6.6 API (see REQ-FW-090)

## License

See [../LICENSE.md](../LICENSE.md).

## Contributing

See [../CONTRIBUTING.md](../CONTRIBUTING.md).

---

**Version**: 1.0.0-alpha
**Last Updated**: 2026-02-18
**Status**: In Development (SPEC-FW-001 approved, implementation pending)
