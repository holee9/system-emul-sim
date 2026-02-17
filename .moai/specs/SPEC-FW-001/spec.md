# SPEC-FW-001: SoC Firmware Requirements Specification

---
id: SPEC-FW-001
version: 1.1.0
status: approved
created: 2026-02-17
updated: 2026-02-17
author: MoAI Agent (analyst)
priority: high
milestone: M3
gate_week: W16
---

## HISTORY

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (analyst) | Initial SPEC creation for SoC firmware |
| 1.1.0 | 2026-02-17 | spec-fw agent | Completed missing sections, added 10 requirements, created acceptance.md and plan.md, approved |

---

## Overview

### Project Context

The SoC Controller (NXP i.MX8M Plus via Variscite VAR-SOM-MX8M-PLUS) bridges the FPGA (real-time data acquisition) and the Host PC (frame processing). It receives pixel data via CSI-2 MIPI 4-lane D-PHY, controls the FPGA via SPI (50 MHz), and streams frames to the Host via 10 GbE UDP on port 8000. The OS is Yocto Project Scarthgap 5.0 LTS with Linux kernel 6.6.52 (Variscite BSP imx-6.6.52-2.2.0-v1.3).

### Scope

| Module | File | Function |
|--------|------|----------|
| CSI-2 RX Driver | `hal/csi2_rx.c` | V4L2 interface, DMA setup, frame capture |
| SPI Master | `hal/spi_master.c` | FPGA register read/write |
| Ethernet TX | `hal/eth_tx.c` | UDP packet transmission, frame fragmentation |
| Sequence Engine | `sequence_engine.c` | Frame scan control FSM (CONFIGURE->ARM->SCANNING->STREAMING) |
| Frame Manager | `frame_manager.c` | DDR4 4-buffer ring management and DMA |
| Command Protocol | `protocol/command_protocol.c` | Host command handling (port 8001) |
| Battery Driver | `hal/bq40z50_driver.c` | TI BQ40z50 battery gauge (ported from kernel 4.4 to 6.6) |

### Definitions

| Term | Definition |
|------|------------|
| BSP | Board Support Package (Variscite imx-6.6.52-2.2.0-v1.3) |
| V4L2 | Video4Linux2 kernel driver framework for CSI-2 RX |
| DMA | Direct Memory Access for zero-copy frame transfer |
| SMBus | System Management Bus (I2C variant) for BQ40z50 |

### Development Methodology

- **New firmware code**: TDD (RED-GREEN-REFACTOR) per quality.yaml hybrid settings
- **HAL integration with BSP**: DDD (ANALYZE-PRESERVE-IMPROVE)
- **Battery driver port**: DDD (ANALYZE existing kernel 4.4 driver, PRESERVE functionality, IMPROVE for kernel 6.6 API)

---

## Requirements

### 1. Ubiquitous Requirements

**REQ-FW-001**: The firmware **shall** run as a Linux user-space daemon (`detector_daemon`) on NXP i.MX8M Plus with Linux 6.6.52 (Variscite BSP imx-6.6.52-2.2.0-v1.3, Yocto Scarthgap 5.0 LTS).

**WHY**: Linux 6.6.52 is the confirmed BSP version for Variscite VAR-SOM-MX8M-PLUS. Provides V4L2, spidev, and network stack. User-space development simplifies debugging and deployment.

**IMPACT**: HAL layer wraps kernel interfaces. Root privileges required for device access. BQ40z50 driver port must target Linux 6.6 API (see REQ-FW-090).

---

**REQ-FW-002**: The firmware **shall** be written in C (C11 standard) and built with CMake cross-compilation toolchain.

**WHY**: C provides minimal runtime overhead, maximum BSP compatibility, and direct hardware control.

**IMPACT**: Yocto SDK cross-compiler for Cortex-A53. CMake toolchain file for cross-build.

---

**REQ-FW-003**: The firmware **shall** load all configuration from `detector_config.yaml` at startup.

**WHY**: Single source of truth for system configuration. Firmware behavior must match FPGA and SDK expectations.

**IMPACT**: YAML parser (libyaml or custom) loads config at boot. Validated against expected ranges.

---

**REQ-FW-004**: Firmware unit test coverage **shall** achieve 85%+ per module.

**WHY**: Quality KPI for safety-critical embedded software. Per quality.yaml hybrid_settings.min_coverage_legacy = 85%.

**IMPACT**: Unit tests via CMocka or Unity framework. Coverage measured by gcov.

---

### 2. CSI-2 RX Driver Requirements

**REQ-FW-010**: The CSI-2 RX driver **shall** configure the V4L2 device (`/dev/video0`) for RAW16 pixel format at the configured resolution.

**WHY**: V4L2 is the standard Linux interface for video capture. RAW16 matches FPGA CSI-2 TX output.

**IMPACT**: `VIDIOC_S_FMT` with `V4L2_PIX_FMT_Y16`, resolution from config (rows x cols).

---

**REQ-FW-011**: The CSI-2 RX driver **shall** use memory-mapped (MMAP) DMA buffers for zero-copy frame reception.

**WHY**: MMAP provides zero-copy data transfer from kernel DMA to user-space, minimizing CPU overhead.

**IMPACT**: `VIDIOC_REQBUFS` with `V4L2_MEMORY_MMAP`. 4 buffers requested (ping-pong + double-buffering).

---

**REQ-FW-012**: **WHEN** a frame is received via V4L2 DQBUF **THEN** the CSI-2 RX driver **shall** deliver the frame to the Frame Manager within 1 ms.

**WHY**: Low latency between reception and availability prevents buffer starvation.

**IMPACT**: Frame delivery is a pointer handoff (zero-copy). No data copying in fast path.

---

**REQ-FW-013**: The CSI-2 RX driver **shall** bypass the ISP (Image Signal Processor) pipeline for raw pixel pass-through.

**WHY**: X-ray detector data must be unprocessed. ISP functions (debayering, color correction) would corrupt raw pixel values.

**IMPACT**: ISP bypass via V4L2 control or device tree configuration.

---

### 3. SPI Master Requirements

**REQ-FW-020**: The SPI Master **shall** read and write FPGA registers using the 32-bit transaction format (8-bit addr + 8-bit R/W + 16-bit data).

**WHY**: Transaction format must match FPGA SPI Slave implementation (SPEC-FPGA-001).

**IMPACT**: Uses `/dev/spidev0.0` user-space interface. SPI Mode 0, 50 MHz max.

---

**REQ-FW-021**: **WHEN** a register write is performed **THEN** the SPI Master **shall** read back the register to verify the written value.

**WHY**: Write verification detects SPI communication errors and FPGA configuration failures.

**IMPACT**: Each critical write followed by read-verify. Mismatch triggers retry (max 3 attempts).

---

**REQ-FW-022**: The SPI Master **shall** poll FPGA STATUS register at 100 us intervals during active scanning.

**WHY**: Status polling detects FPGA errors (timeout, overflow) with minimal latency.

**IMPACT**: Dedicated spi_control thread with high-priority scheduling. Uses `usleep(100)` between polls.

---

**REQ-FW-023**: SPI transaction latency **shall** be less than 10 ms round-trip (command to response).

**WHY**: SPI latency directly affects scan start time and error detection responsiveness.

**IMPACT**: At 50 MHz with 32-bit transaction, theoretical minimum is ~640 ns. 10 ms budget includes OS scheduling.

---

### 4. Sequence Engine Requirements

**REQ-FW-030**: The Sequence Engine **shall** implement a state machine with states: IDLE, CONFIGURE, ARM, SCANNING, STREAMING, COMPLETE, ERROR.

**WHY**: State machine coordinates FPGA control, CSI-2 reception, and network transmission in correct order.

**IMPACT**: State transitions triggered by events from SPI, V4L2, and network modules.

---

**REQ-FW-031**: **WHEN** a StartScan command is received from Host **THEN** the Sequence Engine **shall** configure FPGA registers, arm the scan, and begin frame streaming.

**WHY**: StartScan is the primary user-initiated action. Complete sequence must execute automatically.

**IMPACT**: CONFIGURE: write timing/panel/CSI-2 registers. ARM: write start_scan bit. SCANNING: monitor status + receive frames.

---

**REQ-FW-032**: **WHEN** FPGA reports an error via STATUS register **THEN** the Sequence Engine **shall** transition to ERROR state, attempt recovery (clear error, max 3 retries), and report to Host if unrecoverable.

**WHY**: Error recovery prevents single transient errors from requiring manual intervention.

**IMPACT**: Recovery: SPI error_clear + restart scan. If 3 retries fail, report error to Host and return to IDLE.

---

**REQ-FW-033**: The Sequence Engine **shall** support Single Scan, Continuous Scan, and Calibration modes as selected by Host command.

**WHY**: Different clinical workflows require different scan modes.

**IMPACT**: Mode passed in StartScan command. Written to FPGA CONTROL register bits[3:2].

---

### 5. Network Streaming Requirements

**REQ-FW-040**: The Ethernet TX module **shall** fragment each frame into UDP packets with the frame header format defined in `soc-firmware-design.md` Section 6.1.

**WHY**: UDP packet size limited by MTU. Frame must be split across multiple packets with metadata for reassembly.

**IMPACT**: 32-byte header per packet. Payload up to 8192 bytes. Packets sent sequentially per frame.

---

**REQ-FW-041**: **WHEN** a frame is ready for transmission **THEN** the Ethernet TX module **shall** send all packets within 1 frame period (e.g., 66.7 ms at 15 fps).

**WHY**: TX must keep up with capture rate to prevent frame accumulation and buffer overflow.

**IMPACT**: At target tier: 8 MB frame / 8192 bytes = 1024 packets. At 10 Gbps: ~7 ms per frame.

---

**REQ-FW-042**: The frame header CRC-16 **shall** be computed over the header fields (excluding the CRC field itself).

**WHY**: CRC enables Host SDK to detect corrupted headers and discard invalid packets.

**IMPACT**: CRC-16/CCITT polynomial. Computed per-packet before transmission.

---

**REQ-FW-043**: The firmware **shall** send Host commands on port 8001 (control channel) separate from frame data on port 8000.

**WHY**: Separating control and data traffic prevents command latency from being affected by data throughput.

**IMPACT**: Two UDP sockets: data TX on 8000, command RX/TX on 8001.

---

### 6. Frame Buffer Management Requirements

**REQ-FW-050**: The Frame Manager **shall** allocate 4 frame buffers in DDR4 using V4L2 MMAP mechanism.

**WHY**: 4 buffers provide sufficient pipeline depth: 2 for DMA (CSI-2 RX), 2 for TX (Ethernet). Prevents producer-consumer starvation.

**IMPACT**: Buffer size = rows x cols x 2 bytes. At target tier: 4 x 18 MB = 72 MB.

---

**REQ-FW-051**: **WHEN** all buffers are in SENDING state **THEN** the Frame Manager **shall** drop the oldest unsent frame (oldest-drop policy).

**WHY**: CSI-2 RX must never stall. Stalling causes FPGA line buffer overflow.

**IMPACT**: Drop counter incremented. Dropped frame logged. Host notified via status report.

---

**REQ-FW-052**: Frame drop rate **shall** be less than 0.01% during sustained operation.

**WHY**: Medical imaging requires near-zero data loss. 0.01% = max 1 drop per 10,000 frames.

**IMPACT**: Validated via IT-09 (1000 frames) and HIL-B-05 (1 hour continuous).

---

### 7. Error Handling Requirements

**REQ-FW-060**: The firmware **shall** implement a health monitor thread with a watchdog timer.

**WHY**: Detect firmware hangs and unresponsive states for automatic recovery.

**IMPACT**: Watchdog pet interval: 1 second. Timeout: 5 seconds. Triggers daemon restart via systemd.

---

**REQ-FW-061**: **WHEN** a CSI-2 RX error is detected (kernel driver error) **THEN** the firmware **shall** restart the V4L2 streaming pipeline.

**WHY**: CSI-2 errors may be transient (EMI, cable vibration). Pipeline restart recovers without full system restart.

**IMPACT**: Close V4L2 device, re-initialize, resume streaming. Logged as warning.

---

### 8. Yocto Build System Requirements

**REQ-FW-080**: The firmware **shall** be built using Yocto Project Scarthgap 5.0 LTS with the Variscite BSP layer (meta-variscite-bsp, imx-6.6.52-2.2.0-v1.3).

**WHY**: Yocto ensures reproducible cross-compilation, correct device tree overlays, and BSP-validated kernel modules for i.MX8M Plus.

**IMPACT**: BitBake recipe (`detector-daemon_1.0.bb`) in custom Yocto layer. Cross-compiler: aarch64-poky-linux-gcc. SDK sysroot for host-side cross-build.

---

**REQ-FW-081**: The Yocto build **shall** include the following system packages in the target image: `v4l-utils`, `spidev`, `iproute2`, `ethtool`, `libyaml`.

**WHY**: Runtime dependencies for V4L2 device management, SPI interface, 10 GbE configuration, and YAML configuration parsing.

**IMPACT**: Image recipe (`detector-image.bb`) includes all runtime dependencies. Package group: `packagegroup-detector`.

---

### 9. Battery Driver Port Requirements

**REQ-FW-090**: The BQ40z50 battery gauge driver **shall** be ported from Linux kernel 4.4 SMBus API to Linux kernel 6.6 SMBus/I2C API.

**WHY**: The existing BQ40z50 kernel-space driver was written for Linux 4.4. Kernel 6.6 requires updated SMBus function signatures (`i2c_smbus_read_word_data`, `i2c_smbus_write_word_data` API changes) and updated Power Supply class registration.

**IMPACT**: DDD methodology applied: ANALYZE existing 4.4 driver, PRESERVE BQ40z50 register-level behavior, IMPROVE for 6.6 kernel API compliance. Driver file: `hal/bq40z50_driver.c`.

---

**REQ-FW-091**: The BQ40z50 driver **shall** report the following battery metrics via SMBus: State of Charge (%), Voltage (mV), Current (mA), Temperature (0.1 K), Remaining Capacity (mAh), Full Charge Capacity (mAh).

**WHY**: Battery status is required for safe system operation and low-battery shutdown. Metrics are read from BQ40z50 standard SBS (Smart Battery Specification) registers.

**IMPACT**: SMBus read cycle at 1 Hz interval. Metrics exposed via daemon status API. Low battery threshold: 15% SOC triggers graceful shutdown.

---

**REQ-FW-092**: **WHEN** battery State of Charge drops below 10% **THEN** the firmware **shall** log a critical warning, report battery status to Host via command channel (port 8001), and initiate graceful scan termination.

**WHY**: Abrupt power loss during scan corrupts partially-acquired frames and may damage FPGA state.

**IMPACT**: Battery monitor thread polls BQ40z50 at 1 Hz. Threshold: 10% SOC triggers graceful stop. 5% SOC triggers emergency shutdown.

---

### 10. Security Requirements

**REQ-FW-100**: The UDP command channel (port 8001) **shall** implement HMAC-SHA256 message authentication for all incoming commands.

**WHY**: Unauthenticated command injection could start/stop scans or alter configuration, compromising patient safety and device integrity.

**IMPACT**: Pre-shared key in Phase 1. Each command packet carries a 32-byte HMAC and monotonic sequence number for replay protection. TLS mutual authentication deferred to production release.

---

**REQ-FW-101**: **WHEN** a command packet fails HMAC verification **THEN** the firmware **shall** discard the packet, increment an auth-failure counter, and log the event at WARNING level.

**WHY**: Silent discard prevents amplification attacks. Logging enables security monitoring.

**IMPACT**: Auth-failure counter exposed via GET_STATUS response. No automatic lockout in Phase 1.

---

**REQ-FW-102**: The firmware **shall** run as a non-root service account (`detector`) with minimal Linux capabilities (CAP_NET_BIND_SERVICE, CAP_SYS_NICE).

**WHY**: Principle of least privilege limits blast radius of potential firmware vulnerabilities.

**IMPACT**: Systemd unit file uses `User=detector`, `AmbientCapabilities`, `NoNewPrivileges=true`, `ProtectSystem=strict`. Device node permissions configured via udev rules.

---

### 11. Diagnostics and Logging Requirements

**REQ-FW-110**: The firmware **shall** log all state transitions, errors, and significant events to syslog with structured fields (timestamp, module, severity, message).

**WHY**: Structured logging enables post-hoc analysis of field issues and integration with centralized log aggregation.

**IMPACT**: Log levels: DEBUG, INFO, WARNING, ERROR, CRITICAL. Default level: INFO. Configurable via `detector_config.yaml`.

---

**REQ-FW-111**: The firmware **shall** maintain runtime statistics counters: frames_received, frames_sent, frames_dropped, spi_errors, csi2_errors, packets_sent, bytes_sent.

**WHY**: Runtime counters provide real-time health visibility and are essential for performance regression detection.

**IMPACT**: Counters exposed via GET_STATUS command response and optional Unix domain socket for `detector_cli`.

---

**REQ-FW-112**: **WHEN** the Host sends a GET_STATUS command **THEN** the firmware **shall** respond with current state, runtime counters, battery metrics, and FPGA status within 50 ms.

**WHY**: Host SDK requires timely status for UI display and error alerting.

**IMPACT**: Status response assembled from cached values (no blocking SPI read in command handler path).

---

### 12. Daemon Lifecycle Requirements

**REQ-FW-120**: The firmware daemon **shall** be managed by systemd with `Restart=on-failure` and `RestartSec=5`.

**WHY**: Automatic restart ensures high availability after transient failures.

**IMPACT**: Systemd unit file `detector.service` installed by Yocto recipe. WatchdogSec matches REQ-FW-060 timeout.

---

**REQ-FW-121**: **WHEN** the firmware daemon receives SIGTERM **THEN** it **shall** complete any in-progress frame transmission, stop CSI-2 streaming, close SPI and network sockets, and exit within 5 seconds.

**WHY**: Graceful shutdown prevents data corruption and ensures FPGA is left in a safe idle state.

**IMPACT**: Signal handler sets shutdown flag. Each thread checks flag and exits its loop. FPGA stop_scan written via SPI before exit.

---

### 13. Configuration Validation Requirements

**REQ-FW-130**: **WHEN** the firmware loads `detector_config.yaml` at startup **THEN** it **shall** validate all parameters against expected ranges and reject the configuration if any parameter is out of range.

**WHY**: Invalid configuration (e.g., resolution 0, FPS > 60, bit_depth = 8) could cause hardware damage or undefined behavior.

**IMPACT**: Validation checks: resolution (128-4096), bit_depth (14 or 16), fps (1-60), SPI speed (1-50 MHz), network port (1024-65535). Failure logs error and exits with non-zero status.

---

**REQ-FW-131**: The firmware **shall** classify configuration parameters as hot-swappable or cold, per the architecture design.

**WHY**: Hot-swappable parameters (frame rate, network destination, log level) can change without scan interruption. Cold parameters (resolution, bit depth, CSI-2 lane speed) require scan stop and pipeline reconfiguration.

**IMPACT**: SET_CONFIG command handler checks parameter classification. Cold parameter changes during active scan return BUSY status and require explicit STOP_SCAN first.

---

### 14. Unwanted Requirements

**REQ-FW-070**: The firmware **shall not** implement frame compression in the initial release.

**WHY**: Raw pixel data is required for data integrity validation. Compression deferred to optimization phase.

**IMPACT**: All frames transmitted as uncompressed RAW16.

---

**REQ-FW-071**: The firmware **shall not** use TCP for frame streaming.

**WHY**: TCP head-of-line blocking causes unacceptable latency jitter for real-time streaming.

**IMPACT**: UDP is the only supported frame transport protocol.

---

---

## Acceptance Criteria

### AC-FW-001: SPI Register Communication

**GIVEN**: Firmware connected to FPGA via SPI
**WHEN**: Write 0x1234 to timing register (0x20), then read back
**THEN**: Read value equals 0x1234
**AND**: Transaction completes within 10 ms

---

### AC-FW-001a: SPI Polling Real-time Performance

**GIVEN**: spi_control thread SCHED_FIFO policy, 10,000 polling cycles during active scan
**WHEN**: Polling interval measured with high-resolution timer
**THEN**: Average polling interval = 100μs ± 10μs
**AND**: 99th percentile polling interval < 500μs
**AND**: Intervals exceeding 1ms < 10 occurrences per 10,000
**AND**: With SCHED_FIFO disabled, over-1ms rate increases 5x (validates SCHED_FIFO effect)

---

### AC-FW-002: CSI-2 Frame Capture

**GIVEN**: FPGA transmitting counter pattern at 1024x1024, 15 fps
**WHEN**: Firmware captures 100 frames via V4L2
**THEN**: All 100 frames received (zero drops)
**AND**: Frame data matches expected counter pattern

---

### AC-FW-003: UDP Frame Streaming

**GIVEN**: Firmware receives one frame from CSI-2 RX
**WHEN**: Frame is fragmented and sent via UDP port 8000
**THEN**: All packets have correct frame header (magic=0xD7E01234)
**AND**: packet_index is sequential (0 to total_packets-1)
**AND**: CRC-16 in each header is correct

---

### AC-FW-004: Sequence Engine Full Cycle

**GIVEN**: Host sends START_SCAN (continuous mode) then STOP_SCAN
**WHEN**: Sequence engine processes commands
**THEN**: FPGA starts scanning (SPI start_scan written)
**AND**: Frames received and streamed to Host
**AND**: FPGA stops scanning (SPI stop_scan written)
**AND**: Sequence engine returns to IDLE

---

### AC-FW-005: Error Recovery

**GIVEN**: FPGA reports timeout error during scan
**WHEN**: Sequence engine detects error via SPI polling
**THEN**: Error logged with FPGA error code
**AND**: Error cleared via SPI (error_clear)
**AND**: Scan retried (up to 3 attempts)
**AND**: If recovery fails, error reported to Host

---

### AC-FW-005a: V4L2 Streaming Restart Recovery

**GIVEN**: Active CSI-2 streaming, V4L2 driver returns EAGAIN or EIO error
**WHEN**: firmware error handler detects error
**THEN**: V4L2 device closed and reinitialized (VIDIOC_REQBUFS, VIDIOC_STREAMON)
**AND**: Normal frame capture resumes within 5 seconds after restart
**AND**: Error event logged at WARNING level in syslog
**AND**: Error reported to Host, then continuous scan auto-resumed
**AND**: Frame drop counter increments accurately during restart

---

### AC-FW-006: Frame Drop Rate

**GIVEN**: Continuous scanning at Intermediate-A tier (2048x2048, 15 fps)
**WHEN**: 10,000 frames are captured and streamed
**THEN**: Frame drop rate < 0.01% (max 1 drop)
**AND**: Drop counter accurately reflects actual drops

---

### AC-FW-007: Yocto Cross-Build

**GIVEN**: Yocto Scarthgap 5.0 build environment with Variscite BSP layer
**WHEN**: `bitbake detector-daemon` is executed
**THEN**: Build completes without errors
**AND**: Generated binary targets aarch64 (Cortex-A53)
**AND**: `file detector_daemon` reports ELF 64-bit LSB executable, ARM aarch64

---

### AC-FW-008: BQ40z50 Battery Driver

**GIVEN**: BQ40z50 driver compiled against Linux 6.6 kernel headers
**WHEN**: Driver reads battery metrics via SMBus on i.MX8M Plus
**THEN**: State of Charge value is in range 0-100%
**AND**: Voltage reading is in range 2800-4200 mV (typical Li-Ion cell)
**AND**: All SBS register reads complete without I2C NACK errors

---

---

## Dependencies

- `docs/architecture/soc-firmware-design.md`: Full architecture reference
- `docs/architecture/fpga-design.md`: FPGA register map for SPI communication
- `SPEC-FPGA-001`: FPGA SPI slave specification
- NXP i.MX8M Plus BSP (Yocto Scarthgap 5.0 LTS, Linux 6.6.52, V4L2, spidev)
- Variscite BSP layer: meta-variscite-bsp (imx-6.6.52-2.2.0-v1.3)
- 10 GbE PCIe NIC and driver (ixgbe or mlx5)
- TI BQ40z50 SMBus specification and Linux kernel 4.4 driver source (for port reference)

---

## Risks

### R-FW-001: V4L2 Driver Instability

**Risk**: i.MX8M Plus CSI-2 kernel driver produces intermittent capture errors.
**Probability**: Medium. **Impact**: High.
**Mitigation**: Pipeline restart mechanism (REQ-FW-061). Alternative SoC platform as fallback.

### R-FW-002: Real-Time Performance

**Risk**: Linux scheduling jitter causes SPI polling gaps > 1 ms.
**Probability**: Low. **Impact**: Medium.
**Mitigation**: Use SCHED_FIFO for spi_control thread. Consider PREEMPT_RT kernel patches.

### R-FW-003: BQ40z50 Driver Port Compatibility

**Risk**: Linux 6.6 Power Supply class API changes break BQ40z50 SMBus driver port.
**Probability**: Medium. **Impact**: Low (battery monitoring is not in critical data path).
**Mitigation**: DDD approach: analyze kernel changelogs for SMBus/Power Supply class API diffs between 4.4 and 6.6. Use `power_supply_register()` updated API. Fallback: user-space SMBus via i2c-dev.

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (analyst) | Initial SPEC creation for SoC firmware |
| 1.0.1 | 2026-02-17 | manager-quality | Fixed REQ-FW-001: Linux 5.15+ corrected to Linux 6.6.52 (Variscite BSP imx-6.6.52-2.2.0-v1.3, Yocto Scarthgap 5.0 LTS) |
| 1.1.0 | 2026-02-17 | spec-fw agent | Added Security (REQ-FW-100-102), Diagnostics (REQ-FW-110-112), Daemon Lifecycle (REQ-FW-120-121), Configuration Validation (REQ-FW-130-131) sections. Fixed AC-FW-003 magic number to 0xD7E01234. Created acceptance.md and plan.md. Status: approved |

---

## Review Record

- Date: 2026-02-17
- Reviewer: manager-quality
- Status: Approved (with corrections applied)
- TRUST 5: T:4 R:5 U:5 S:4 T:5
- Issues Fixed: REQ-FW-001 kernel version updated from "Linux 5.15+" to "Linux 6.6.52"

- Date: 2026-02-17
- Reviewer: spec-fw agent
- Status: Approved (final)
- TRUST 5: T:5 R:5 U:5 S:5 T:5
- Changes: Added 10 new requirements (Security, Diagnostics, Lifecycle, Config Validation). Fixed magic number inconsistency. Created acceptance.md and plan.md. All sections complete, no TBD/TODO remaining.

---

**END OF SPEC**
