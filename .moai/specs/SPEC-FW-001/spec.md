# SPEC-FW-001: SoC Firmware Requirements Specification

---
id: SPEC-FW-001
version: 1.0.0
status: draft
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

---

## Overview

### Project Context

The SoC Controller (NXP i.MX8M Plus) bridges the FPGA (real-time data acquisition) and the Host PC (frame processing). It receives pixel data via CSI-2, controls the FPGA via SPI, and streams frames to the Host via 10 GbE UDP.

### Scope

| Module | File | Function |
|--------|------|----------|
| CSI-2 RX Driver | `hal/csi2_rx.c` | V4L2 interface, DMA setup, frame capture |
| SPI Master | `hal/spi_master.c` | FPGA register read/write |
| Ethernet TX | `hal/eth_tx.c` | UDP packet transmission, frame fragmentation |
| Sequence Engine | `sequence_engine.c` | Frame scan control FSM |
| Frame Manager | `frame_manager.c` | DDR4 buffer lifecycle |
| Command Protocol | `protocol/command_protocol.c` | Host command handling |

### Development Methodology

- **New firmware code**: TDD (RED-GREEN-REFACTOR) per quality.yaml hybrid settings
- **HAL integration with BSP**: DDD (ANALYZE-PRESERVE-IMPROVE)

---

## Requirements

### 1. Ubiquitous Requirements

**REQ-FW-001**: The firmware **shall** run as a Linux user-space daemon (`detector_daemon`) on NXP i.MX8M Plus with Linux 5.15+.

**WHY**: Linux provides V4L2, spidev, and network stack. User-space development simplifies debugging and deployment.

**IMPACT**: HAL layer wraps kernel interfaces. Root privileges required for device access.

---

**REQ-FW-002**: The firmware **shall** be written in C (C11 standard) and built with CMake cross-compilation toolchain.

**WHY**: C provides minimal runtime overhead, maximum BSP compatibility, and direct hardware control.

**IMPACT**: Yocto SDK cross-compiler for Cortex-A53. CMake toolchain file for cross-build.

---

**REQ-FW-003**: The firmware **shall** load all configuration from `detector_config.yaml` at startup.

**WHY**: Single source of truth for system configuration. Firmware behavior must match FPGA and SDK expectations.

**IMPACT**: YAML parser (libyaml or custom) loads config at boot. Validated against expected ranges.

---

**REQ-FW-004**: Firmware unit test coverage **shall** achieve 80%+ per module.

**WHY**: Quality KPI for safety-critical embedded software.

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

### 8. Unwanted Requirements

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

### AC-FW-002: CSI-2 Frame Capture

**GIVEN**: FPGA transmitting counter pattern at 1024x1024, 15 fps
**WHEN**: Firmware captures 100 frames via V4L2
**THEN**: All 100 frames received (zero drops)
**AND**: Frame data matches expected counter pattern

---

### AC-FW-003: UDP Frame Streaming

**GIVEN**: Firmware receives one frame from CSI-2 RX
**WHEN**: Frame is fragmented and sent via UDP port 8000
**THEN**: All packets have correct frame header (magic=0xDEADBEEF)
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

### AC-FW-006: Frame Drop Rate

**GIVEN**: Continuous scanning at Intermediate-A tier (2048x2048, 15 fps)
**WHEN**: 10,000 frames are captured and streamed
**THEN**: Frame drop rate < 0.01% (max 1 drop)
**AND**: Drop counter accurately reflects actual drops

---

---

## Dependencies

- `docs/architecture/soc-firmware-design.md`: Full architecture reference
- `docs/architecture/fpga-design.md`: FPGA register map for SPI communication
- `SPEC-FPGA-001`: FPGA SPI slave specification
- NXP i.MX8M Plus BSP (Linux 5.15+, V4L2, spidev)
- 10 GbE PCIe NIC and driver (ixgbe or mlx5)

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

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (analyst) | Initial SPEC creation for SoC firmware |

---

**END OF SPEC**
