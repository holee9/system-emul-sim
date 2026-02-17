# SPEC-FW-001: Implementation Plan

## Overview

This implementation plan outlines the phased approach to developing the SoC Controller firmware for the X-ray Detector Panel System. The firmware runs on NXP i.MX8M Plus (Variscite VAR-SOM-MX8M-PLUS) under Yocto Scarthgap 5.0 LTS with Linux kernel 6.6.52.

Development follows the Hybrid methodology: TDD for new firmware modules, DDD for HAL integration and battery driver porting.

---

## Implementation Phases

### Phase 1: HAL Layer Development (Foundation)

**Objective**: Implement and unit-test all Hardware Abstraction Layer modules that interface with kernel drivers.

**Tasks**:

1. **SPI Master HAL** (`hal/spi_master.c`)
   - Implement `fpga_reg_write()` and `fpga_reg_read()` using spidev ioctl
   - 32-bit transaction format: Word0=[addr<<8|R/W], Word1=[16-bit data]
   - Write-verify with 3 retry logic (REQ-FW-020, REQ-FW-021)
   - SPI Mode 0, 50 MHz, 16-bit word (REQ-FW-020)
   - Unit tests: FW-UT-01 (register R/W round-trip, error injection)
   - **Methodology**: TDD (RED-GREEN-REFACTOR)

2. **CSI-2 RX HAL** (`hal/csi2_rx.c`)
   - V4L2 device open, format configuration (V4L2_PIX_FMT_Y16), MMAP buffer setup
   - ISP bypass configuration (REQ-FW-013)
   - Frame reception loop (DQBUF/QBUF) with < 1 ms delivery (REQ-FW-012)
   - Pipeline restart on error (REQ-FW-061)
   - Unit tests: Mock V4L2 ioctl for testability
   - **Methodology**: DDD (ANALYZE V4L2 API, PRESERVE kernel interface, IMPROVE error handling)

3. **Ethernet TX HAL** (`hal/eth_tx.c`)
   - UDP socket creation with 16 MB send buffer
   - Frame fragmentation: 32-byte header + 8192-byte payload per packet
   - Frame header encoding with CRC-16/CCITT (REQ-FW-040, REQ-FW-042)
   - Port separation: data on 8000, control on 8001 (REQ-FW-043)
   - Unit tests: FW-UT-02 (header encode/decode), FW-UT-03 (CRC-16)
   - **Methodology**: TDD

4. **CRC-16 Utility** (`util/crc16.c`)
   - CRC-16/CCITT polynomial implementation
   - Test against reference vectors
   - Unit tests: FW-UT-03
   - **Methodology**: TDD

**Deliverables**:
- `hal/spi_master.c/.h`, `hal/csi2_rx.c/.h`, `hal/eth_tx.c/.h`, `util/crc16.c/.h`
- Unit test files with 85%+ coverage per module
- HAL interface documentation (Doxygen headers)

**Dependencies**:
- Yocto Scarthgap SDK cross-compiler installed
- CMake toolchain file for i.MX8M Plus
- spidev, V4L2, and network headers from Yocto SDK sysroot

---

### Phase 2: Core Application Logic

**Objective**: Implement the Sequence Engine, Frame Manager, and Configuration Loader.

**Tasks**:

1. **Sequence Engine** (`sequence_engine.c`)
   - State machine: IDLE, CONFIGURE, ARM, SCANNING, STREAMING, COMPLETE, ERROR
   - CONFIGURE: write and verify FPGA registers via SPI HAL (REQ-FW-031)
   - ARM: write start_scan, wait for STATUS.busy (REQ-FW-031)
   - SCANNING: poll STATUS at 100 us with SCHED_FIFO thread (REQ-FW-022)
   - ERROR: recovery with 3 retry limit (REQ-FW-032)
   - Modes: Single, Continuous, Calibration (REQ-FW-033)
   - Unit tests: FW-UT-05 (all state transitions, all modes, error paths)
   - **Methodology**: TDD

2. **Frame Manager** (`frame_manager.c`)
   - 4-buffer ring with states: FREE, FILLING, READY, SENDING
   - Producer (CSI-2 RX) and consumer (Ethernet TX) coordination
   - Oldest-drop policy when all buffers busy (REQ-FW-051)
   - Drop counter and statistics (REQ-FW-052, REQ-FW-111)
   - Unit tests: FW-UT-06 (all buffer state transitions, overrun scenarios)
   - **Methodology**: TDD

3. **Configuration Loader** (`config/config_loader.c`)
   - YAML parsing via libyaml
   - Parameter validation with range checks (REQ-FW-130)
   - Hot/cold parameter classification (REQ-FW-131)
   - Unit tests: FW-UT-04 (valid configs, invalid configs, edge values)
   - **Methodology**: TDD

4. **Command Protocol** (`protocol/command_protocol.c`)
   - Host command handling on UDP port 8001
   - HMAC-SHA256 authentication and sequence number anti-replay (REQ-FW-100)
   - Command IDs: START_SCAN, STOP_SCAN, GET_STATUS, SET_CONFIG, RESET
   - Auth failure logging and counter (REQ-FW-101)
   - **Methodology**: TDD

**Deliverables**:
- `sequence_engine.c/.h`, `frame_manager.c/.h`, `config/config_loader.c/.h`
- `protocol/command_protocol.c/.h`, `protocol/frame_header.c/.h`
- Unit test files with 85%+ coverage per module

**Dependencies**:
- Phase 1 HAL modules completed
- libyaml library available in Yocto SDK
- FPGA register map finalized (SPEC-FPGA-001)

---

### Phase 3: Daemon Integration and System Services

**Objective**: Integrate all modules into the `detector_daemon` process with threading, health monitoring, and systemd management.

**Tasks**:

1. **Main Daemon** (`main.c`)
   - Thread creation: sequence_engine, frame_rx, frame_tx, spi_control, health_monitor
   - Signal handler for SIGTERM graceful shutdown (REQ-FW-121)
   - Configuration loading at startup (REQ-FW-003)
   - Root-to-detector user privilege drop (REQ-FW-102)
   - **Methodology**: TDD for signal handling; DDD for thread lifecycle

2. **Health Monitor** (`health_monitor.c`)
   - Watchdog timer: 1s pet interval, 5s timeout (REQ-FW-060)
   - Runtime statistics aggregation (REQ-FW-111)
   - Structured syslog logging (REQ-FW-110)
   - GET_STATUS response assembly < 50 ms (REQ-FW-112)
   - **Methodology**: TDD

3. **BQ40z50 Battery Driver** (`hal/bq40z50_driver.c`)
   - Port from Linux 4.4 SMBus API to 6.6 (REQ-FW-090)
   - Read 6 SBS metrics at 1 Hz (REQ-FW-091)
   - Low battery thresholds: 10% warning, 5% emergency (REQ-FW-092)
   - **Methodology**: DDD (ANALYZE 4.4 driver, PRESERVE register-level behavior, IMPROVE for 6.6 API)

4. **Systemd Service** (`detector.service`)
   - Unit file with Restart=on-failure, RestartSec=5 (REQ-FW-120)
   - Non-root execution with capability constraints (REQ-FW-102)
   - WatchdogSec=5 integration (REQ-FW-060)

5. **Yocto Recipe** (`detector-daemon_1.0.bb`)
   - BitBake recipe for cross-compilation (REQ-FW-080)
   - Runtime dependency packages (REQ-FW-081)
   - Package group: packagegroup-detector

**Deliverables**:
- `main.c`, `health_monitor.c/.h`, `hal/bq40z50_driver.c/.h`
- `detector.service` systemd unit file
- `detector-daemon_1.0.bb` Yocto recipe
- Integration test files

**Dependencies**:
- Phase 1 and Phase 2 completed
- BQ40z50 kernel 4.4 driver source available for reference
- Yocto Scarthgap build environment operational

---

### Phase 4: Integration Testing

**Objective**: Validate end-to-end firmware functionality with simulated and real hardware.

**Tasks**:

1. **Simulated Integration Tests** (host-side, no hardware)
   - FW-IT-03: Full scan sequence (single frame, all state transitions)
   - FW-IT-05: Error injection (SPI errors, CSI-2 errors, network errors)
   - Test with mock HAL layer (spidev mock, V4L2 mock, socket mock)

2. **Hardware Integration Tests** (target hardware required)
   - FW-IT-01: CSI-2 frame capture (100 frames, counter pattern validation)
   - FW-IT-02: SPI + CSI-2 concurrent operation (no interference)
   - FW-IT-04: Continuous 1000 frames (drop rate < 0.01%)

3. **Performance Benchmarks**
   - SPI polling jitter measurement (Scenario 2)
   - Frame throughput at Intermediate-A tier
   - End-to-end latency measurement (< 45 ms)
   - CPU utilization profiling

4. **Security Validation**
   - HMAC-SHA256 authentication test (valid/invalid/replay)
   - Non-root capability verification
   - Configuration validation boundary testing

**Deliverables**:
- Integration test results report
- Performance benchmark results
- Security validation report
- Known issues list

**Dependencies**:
- Phase 3 daemon integration completed
- FPGA hardware available for HIL tests (FW-IT-01, FW-IT-02)
- 10 GbE network infrastructure for throughput tests

---

## Task Decomposition

### Priority-Based Milestones

**Primary Goal**: Complete HAL layer with unit tests
- SPI Master: register R/W, write-verify, retry logic
- CSI-2 RX: V4L2 setup, MMAP buffers, frame reception
- Ethernet TX: UDP fragmentation, header encoding, CRC-16
- Success criteria: 85%+ coverage per HAL module, all FW-UT tests passing

**Secondary Goal**: Complete core application logic
- Sequence Engine: all 7 states, 3 modes, error recovery
- Frame Manager: 4-buffer ring, oldest-drop policy
- Config Loader: YAML parsing, validation, hot/cold classification
- Command Protocol: HMAC auth, all 5 command IDs
- Success criteria: 85%+ coverage, FW-UT-04/05/06 passing

**Final Goal**: Daemon integration and system testing
- Thread management, signal handling, watchdog
- BQ40z50 battery driver port (DDD)
- Systemd service and Yocto recipe
- Integration tests (FW-IT-01 through FW-IT-05)
- Success criteria: All acceptance scenarios passing, endurance test < 0.01% drops

**Optional Goal**: Performance optimization
- SPI polling jitter optimization (PREEMPT_RT patches if needed)
- UDP TX optimization (sendmmsg for batch transmission)
- Memory pool pre-allocation for zero-allocation fast path
- Success criteria: End-to-end latency < 45 ms, CPU < 80%

---

## Technology Stack Specifications

### Firmware Stack

**Language**: C (C11 standard, ISO/IEC 9899:2011)

**Build System**: CMake 3.20+
- Cross-compilation via Yocto SDK toolchain file
- `cmake -DCMAKE_TOOLCHAIN_FILE=toolchain/imx8mp-toolchain.cmake`

**Cross-Compiler**: aarch64-poky-linux-gcc (from Yocto Scarthgap SDK)
- Target: ARM Cortex-A53 (ARMv8-A)
- Flags: `-march=armv8-a -mtune=cortex-a53 -O2`

**Testing Framework**: CMocka or Unity
- Mocking: cmocka_mock for HAL layer isolation
- Coverage: gcov + lcov (target 85%+)

**Libraries**:
| Library | Version | Purpose |
|---------|---------|---------|
| libyaml | 0.2.5+ | YAML configuration parsing |
| OpenSSL | 3.0+ | HMAC-SHA256 for command auth |
| libv4l2 | (system) | V4L2 user-space helpers |
| pthread | (system) | Multi-threading |

### Target Platform

**SoC**: NXP i.MX8M Plus (Variscite VAR-SOM-MX8M-PLUS DART)
- CPU: Quad Cortex-A53 @ 1.8 GHz
- Memory: 4 GB LPDDR4
- Storage: eMMC 32 GB

**OS**: Yocto Scarthgap 5.0 LTS
- BSP: Variscite imx-6.6.52-2.2.0-v1.3
- Kernel: Linux 6.6.52 (LTS)

**Interfaces**:
| Interface | Device | Driver | Purpose |
|-----------|--------|--------|---------|
| CSI-2 RX | /dev/video0 | imx8-mipi-csi2 (V4L2) | Frame reception from FPGA |
| SPI | /dev/spidev0.0 | spi-imx (spidev) | FPGA register control |
| 10 GbE | eth1 | ixgbe or mlx5 | Frame streaming to Host |
| SMBus | I2C addr 0x0b | bq27xxx_battery | Battery monitoring |

### Source Tree

```
fw/
  CMakeLists.txt
  src/
    main.c
    sequence_engine.c/.h
    frame_manager.c/.h
    health_monitor.c/.h
    hal/
      csi2_rx.c/.h
      spi_master.c/.h
      eth_tx.c/.h
      bq40z50_driver.c/.h
    config/
      config_loader.c/.h
    protocol/
      frame_header.c/.h
      command_protocol.c/.h
    util/
      crc16.c/.h
      log.c/.h
  tests/
    test_spi_protocol.c
    test_frame_header.c
    test_crc16.c
    test_config_loader.c
    test_sequence_engine.c
    test_frame_manager.c
    test_command_protocol.c
    test_health_monitor.c
    mock/
      mock_spidev.c/.h
      mock_v4l2.c/.h
      mock_socket.c/.h
  toolchain/
    imx8mp-toolchain.cmake
  deploy/
    detector.service
    detector-daemon_1.0.bb
```

---

## Risk Analysis

### Risk 1: V4L2 Driver Instability (R-FW-001)

**Risk Description**: i.MX8M Plus CSI-2 kernel driver produces intermittent capture errors under sustained load.

**Probability**: Medium (40%)

**Impact**: High (frame drops, pipeline restarts, reduced reliability)

**Mitigation**:
- Pipeline restart mechanism (REQ-FW-061) recovers from transient errors
- Multiple restart cycles tested (10 consecutive restarts without leak)
- Kernel DMESG monitoring for pattern identification
- PREEMPT_RT kernel patches as escalation option

**Contingency**:
- If restart frequency exceeds 1/hour: investigate kernel driver, request Variscite BSP patch
- User-space DMA alternative (less efficient but more controllable)

---

### Risk 2: Real-Time Scheduling Jitter (R-FW-002)

**Risk Description**: Linux SCHED_FIFO scheduling provides insufficient determinism for 100 us SPI polling.

**Probability**: Low (20%)

**Impact**: Medium (missed FPGA errors, delayed error detection)

**Mitigation**:
- SCHED_FIFO with priority 99 for spi_control thread
- CPU affinity: pin spi_control to dedicated core
- Validate with jitter benchmark (Scenario 2)
- PREEMPT_RT kernel patches available as fallback

**Contingency**:
- If P99 jitter > 500 us: apply PREEMPT_RT patches to kernel 6.6
- If PREEMPT_RT insufficient: move SPI polling to kernel module (last resort)

---

### Risk 3: BQ40z50 Driver Port (R-FW-003)

**Risk Description**: Linux 6.6 Power Supply class API changes break BQ40z50 SMBus driver.

**Probability**: Medium (35%)

**Impact**: Low (battery monitoring not in critical data path)

**Mitigation**:
- DDD approach: analyze kernel changelogs for API diffs between 4.4 and 6.6
- `power_supply_register()` API migration documented
- Test on target hardware with known-good battery

**Contingency**:
- If kernel driver port fails: use user-space SMBus via i2c-dev (fallback)
- User-space driver reads BQ40z50 SBS registers directly via `/dev/i2c-X`
- Reduced integration but same functionality

---

### Risk 4: 10 GbE Throughput Under Load (R-FW-004)

**Risk Description**: 10 GbE UDP throughput drops below required rate due to kernel network stack overhead or PCIe bandwidth limits.

**Probability**: Low (15%)

**Impact**: High (frame drops at Target tier, throughput bottleneck)

**Mitigation**:
- Large send buffer (16 MB) reduces kernel overhead
- SO_PRIORITY for data socket to prioritize frame traffic
- Jumbo frames (9000 MTU) reduce per-packet overhead
- sendmmsg() batch transmission for multiple packets per syscall

**Contingency**:
- If UDP throughput < 270 MB/s: enable kernel bypass (DPDK or AF_XDP)
- If PCIe bottleneck: verify PCIe Gen3 x4 link status
- Reduce to Intermediate-A tier (120 MB/s) as interim solution

---

## Dependencies

### External Dependencies

**Hardware**:
- NXP i.MX8M Plus EVK (Variscite VAR-SOM-MX8M-PLUS) - available
- Artix-7 XC7A35T evaluation board - available
- 10 GbE NIC (PCIe) for i.MX8M Plus
- TI BQ40z50 battery unit for driver testing

**Software**:
- Yocto Scarthgap 5.0 LTS build system
- Variscite BSP (imx-6.6.52-2.2.0-v1.3)
- CMocka or Unity test framework (cross-compiled)
- OpenSSL 3.0+ (for HMAC-SHA256)

**Documentation**:
- FPGA register map (SPEC-FPGA-001) - for SPI communication
- BQ40z50 SBS register specification - for battery driver
- i.MX8M Plus CSI-2 receiver technical reference manual

### Internal Dependencies

**SPEC Dependencies**:
- SPEC-FPGA-001: FPGA register map defines SPI transaction addresses and formats
- SPEC-ARCH-001: Architecture decisions (CSI-2, 10 GbE, Yocto Scarthgap)
- SPEC-SDK-001: Host SDK command protocol alignment

**Document Dependencies**:
- `docs/architecture/soc-firmware-design.md`: Full architecture reference
- `docs/architecture/fpga-design.md`: FPGA register map
- `detector_config.yaml`: Configuration schema and defaults

---

## Milestone Mapping

| SPEC Phase | Project Milestone | Week | Gate Criteria |
|-----------|-------------------|------|---------------|
| Phase 1 (HAL) | M3 preparation | W9-W11 | HAL unit tests passing, 85% coverage |
| Phase 2 (Core) | M3 preparation | W11-W13 | Core unit tests passing, 85% coverage |
| Phase 3 (Integration) | M3 gate | W13-W16 | Daemon builds, systemd starts, basic scan works |
| Phase 4 (Testing) | M4 validation | W16-W18 | All FW-IT tests passing, < 0.01% drops |

---

## Next Steps

### Immediate Actions (Post-Approval)

1. **Yocto SDK Setup**
   - Install Scarthgap SDK: `source /opt/fsl-imx-xwayland/scarthgap/environment-setup-cortexa53-crypto-poky-linux`
   - Verify cross-compiler: `aarch64-poky-linux-gcc --version`
   - Create CMake toolchain file: `toolchain/imx8mp-toolchain.cmake`

2. **Project Skeleton**
   - Create `fw/` directory structure (see Source Tree)
   - Initialize CMakeLists.txt with cross-compilation support
   - Set up CMocka/Unity test framework
   - Create mock HAL headers for host-side unit testing

3. **Phase 1 Kickoff**
   - Start with SPI Master HAL (simplest, fewest dependencies)
   - TDD: write `test_spi_protocol.c` first (RED), then implement (GREEN)
   - Iterate through CSI-2 RX, Ethernet TX, CRC-16

### Transition to Phase 2

**Trigger**: Phase 1 HAL unit tests all passing with 85%+ coverage

**Actions**:
- Begin Sequence Engine implementation (TDD)
- Use mock HAL for state machine testing
- Parallel: Frame Manager and Config Loader

### Transition to Integration Testing

**Trigger**: Phase 2 and Phase 3 unit tests passing, daemon compiles and starts on target

**Actions**:
- Deploy to i.MX8M Plus hardware
- Run FW-IT-01 (CSI-2 capture with FPGA test pattern)
- Escalate to endurance tests (FW-IT-04)

---

## Traceability

This implementation plan aligns with:

- **SPEC-FW-001 spec.md**: All 37 requirements mapped to implementation tasks
- **SPEC-FW-001 acceptance.md**: All 14 test scenarios mapped to testing phases
- **soc-firmware-design.md**: Architecture, HAL interfaces, state machines, protocol
- **quality.yaml**: Hybrid methodology (TDD new, DDD legacy), 85% coverage target
- **SPEC-ARCH-001**: P0 decisions (CSI-2, 10 GbE, Yocto Scarthgap, i.MX8M Plus)

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | spec-fw agent | Initial implementation plan for SPEC-FW-001 |

---

**END OF PLAN**
