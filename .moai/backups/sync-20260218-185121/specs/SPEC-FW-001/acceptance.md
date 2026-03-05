# SPEC-FW-001: Acceptance Criteria and Test Scenarios

## Overview

This document defines the acceptance criteria, test scenarios, and quality gates for SPEC-FW-001 (SoC Firmware Requirements Specification). All scenarios use Given/When/Then (Gherkin) format for clarity and traceability.

---

## Test Scenarios

### Scenario 1: SPI Register Communication

**Objective**: Verify FPGA register read/write via SPI in 32-bit transaction format (8-bit addr + 8-bit R/W + 16-bit data).

```gherkin
Given the firmware is connected to FPGA via SPI (/dev/spidev0.0, Mode 0, 50 MHz, 16-bit word)
When a write of 0x1234 is performed to timing register (addr 0x20)
And a read-back is performed from the same register
Then the read value shall equal 0x1234
And the full round-trip transaction shall complete within 10 ms
And the SPI transaction format shall be: Word0=[addr<<8|R/W], Word1=[16-bit data]
```

**Success Criteria**:
- Write-verify succeeds for all FPGA register addresses (0x00-0xFF)
- Round-trip latency < 10 ms (REQ-FW-023)
- Read-back mismatch triggers retry (max 3 attempts per REQ-FW-021)

**Verification Method**: Unit test (FW-UT-01) + HIL test with FPGA hardware

---

### Scenario 2: SPI Polling Real-Time Performance

**Objective**: Verify SPI status polling meets 100 us interval requirement with SCHED_FIFO scheduling.

```gherkin
Given spi_control thread is configured with SCHED_FIFO policy
And 10,000 polling cycles are executed during active scanning
When polling interval is measured with high-resolution timer
Then average polling interval shall be 100 us +/- 10 us
And 99th percentile polling interval shall be < 500 us
And intervals exceeding 1 ms shall be < 10 occurrences per 10,000
And with SCHED_FIFO disabled, the over-1ms rate shall increase by at least 5x
```

**Success Criteria**:
- Average interval: 90-110 us
- P99 interval: < 500 us
- Jitter outliers (> 1 ms): < 0.1% of cycles
- SCHED_FIFO effectiveness validated by comparison test

**Verification Method**: Performance benchmark + HIL test (REQ-FW-022)

---

### Scenario 3: CSI-2 Frame Capture

**Objective**: Verify V4L2 CSI-2 RX driver captures frames with zero loss at Intermediate-A tier.

```gherkin
Given FPGA is transmitting counter pattern at 1024x1024, 16-bit, 15 fps
And V4L2 device /dev/video0 is configured with V4L2_PIX_FMT_Y16 and MMAP buffers
When firmware captures 100 consecutive frames via V4L2 DQBUF
Then all 100 frames shall be received (zero drops)
And each frame size shall equal 1024 * 1024 * 2 = 2,097,152 bytes
And frame pixel data shall match expected counter pattern (bitwise comparison)
And ISP bypass shall be confirmed (raw pixel values unmodified)
```

**Success Criteria**:
- 100/100 frames captured (REQ-FW-012)
- Data integrity: bitwise match with known pattern
- ISP bypass active (REQ-FW-013)
- Frame delivery latency < 1 ms from DQBUF to Frame Manager (REQ-FW-012)

**Verification Method**: Integration test (FW-IT-01) with FPGA test pattern generator

---

### Scenario 4: UDP Frame Streaming

**Objective**: Verify frame fragmentation, header correctness, and CRC-16 integrity for UDP frame streaming.

```gherkin
Given firmware receives one complete frame from CSI-2 RX (2048x2048, 16-bit)
When the frame is fragmented and sent via UDP port 8000
Then all packets shall have correct frame header:
  | Field | Expected Value |
  | magic | 0xD7E01234 |
  | width | 2048 |
  | height | 2048 |
  | bit_depth | 16 |
And packet_index shall be sequential (0 to total_packets-1)
And total_packets shall equal ceil(8,388,608 / 8192) = 1024
And CRC-16/CCITT in each header shall be correct (verified by independent CRC calculation)
And the last packet shall have flags.last_packet bit set
```

**Success Criteria**:
- All 1024 packets sent per frame (REQ-FW-040)
- Header fields match configured resolution (REQ-FW-041)
- CRC-16 valid on all packets (REQ-FW-042)
- Payload reassembly produces original frame data

**Verification Method**: Unit test (FW-UT-02, FW-UT-03) + integration test with packet capture

---

### Scenario 5: Sequence Engine Full Cycle

**Objective**: Verify complete scan sequence from StartScan to IDLE return.

```gherkin
Given Host sends START_SCAN command (continuous mode, Intermediate-A tier) via port 8001
When Sequence Engine processes the command
Then the following state transitions shall occur in order:
  | Step | State | Actions |
  | 1 | IDLE -> CONFIGURE | Write timing registers (0x20-0x34) via SPI, verify each |
  | 2 | CONFIGURE -> ARM | Write start_scan (CONTROL bit[0]=1) to FPGA |
  | 3 | ARM -> SCANNING | Wait for STATUS.busy assertion (< 10 ms timeout) |
  | 4 | SCANNING -> STREAMING | Receive frame via CSI-2, begin UDP TX |
  | 5 | STREAMING -> COMPLETE | Frame TX confirmed |
  | 6 | COMPLETE -> ARM | Continuous mode: loop back to ARM |
And when Host sends STOP_SCAN command
Then FPGA stop_scan shall be written via SPI
And Sequence Engine shall return to IDLE state
And frames_received and frames_sent counters shall be consistent
```

**Success Criteria**:
- All state transitions verified (REQ-FW-030, REQ-FW-031)
- FPGA registers written and verified via SPI (REQ-FW-020, REQ-FW-021)
- Continuous mode loops correctly (REQ-FW-033)
- Single mode returns to IDLE after one frame
- Calibration mode supported (REQ-FW-033)

**Verification Method**: Unit test (FW-UT-05) + integration test (FW-IT-03)

---

### Scenario 6: Error Recovery

**Objective**: Verify error detection, recovery, and reporting for FPGA errors.

```gherkin
Given FPGA reports timeout error via STATUS register during active scan
When Sequence Engine detects error via SPI polling
Then error shall be logged with FPGA error code at ERROR level
And error shall be cleared via SPI (write error_clear register)
And scan shall be retried (up to 3 attempts)
And if all 3 retries fail:
  | Action | Detail |
  | Report to Host | Error packet on port 8001 with error code |
  | Return to IDLE | Sequence Engine enters IDLE state |
  | Log CRITICAL | Unrecoverable error logged to syslog |
```

**Success Criteria**:
- Error detection within 100 us polling cycle (REQ-FW-022)
- Recovery succeeds on transient errors (REQ-FW-032)
- 3 retry limit enforced (REQ-FW-032)
- Host notification on unrecoverable error
- Error counters updated accurately (REQ-FW-111)

**Verification Method**: Unit test (FW-UT-05) + integration test (FW-IT-05) with error injection

---

### Scenario 7: V4L2 Streaming Restart Recovery

**Objective**: Verify CSI-2 pipeline restart after V4L2 driver error.

```gherkin
Given active CSI-2 streaming at Intermediate-A tier (2048x2048, 15 fps)
And V4L2 driver returns EAGAIN or EIO error
When firmware error handler detects the error
Then V4L2 device shall be closed and reinitialized:
  | Step | API Call |
  | 1 | VIDIOC_STREAMOFF |
  | 2 | close(/dev/video0) |
  | 3 | open(/dev/video0) |
  | 4 | VIDIOC_S_FMT (reconfigure) |
  | 5 | VIDIOC_REQBUFS (reallocate 4 buffers) |
  | 6 | VIDIOC_STREAMON |
And normal frame capture shall resume within 5 seconds after restart
And error event shall be logged at WARNING level in syslog
And frame drop counter shall increment accurately during restart period
```

**Success Criteria**:
- Pipeline restart completes within 5 seconds (REQ-FW-061)
- No memory leaks after multiple restart cycles (10 restarts in sequence)
- Frame capture resumes correctly after restart
- Drop counter reflects actual drops during restart window

**Verification Method**: Integration test with simulated V4L2 errors

---

### Scenario 8: Frame Drop Rate Under Sustained Load

**Objective**: Verify frame drop rate remains below 0.01% during continuous operation.

```gherkin
Given continuous scanning at Intermediate-A tier (2048x2048, 16-bit, 15 fps)
When 10,000 frames are captured and streamed over ~11 minutes
Then frame drop rate shall be < 0.01% (maximum 1 drop per 10,000 frames)
And frame_seq numbers on Host side shall be monotonically increasing
And drop counter shall accurately reflect actual drops
And buffer states (FREE/FILLING/READY/SENDING) shall cycle without deadlock
```

**Success Criteria**:
- Max 1 frame drop in 10,000 frames (REQ-FW-052)
- Zero deadlocks in buffer management (REQ-FW-050, REQ-FW-051)
- Memory usage stable (no growth over 11 minutes)
- CPU utilization < 80% during sustained operation

**Verification Method**: Integration test (FW-IT-04) + HIL endurance test (HIL-B-05)

---

### Scenario 9: Yocto Cross-Build

**Objective**: Verify firmware builds correctly under Yocto Scarthgap with Variscite BSP.

```gherkin
Given Yocto Scarthgap 5.0 build environment with Variscite BSP layer (imx-6.6.52-2.2.0-v1.3)
When `bitbake detector-daemon` is executed
Then build shall complete without errors
And generated binary shall target aarch64 (Cortex-A53):
  | Check | Expected |
  | file detector_daemon | ELF 64-bit LSB executable, ARM aarch64 |
  | ldd detector_daemon | Links to aarch64 system libraries |
And all runtime dependencies shall be included in the image:
  | Package | Purpose |
  | v4l-utils | V4L2 device management |
  | spidev | SPI user-space interface |
  | iproute2 | Network configuration |
  | ethtool | 10 GbE diagnostics |
  | libyaml | YAML configuration parsing |
```

**Success Criteria**:
- Build completes (REQ-FW-080)
- Binary is correct architecture (REQ-FW-080)
- All runtime dependencies present (REQ-FW-081)
- `detector_daemon` starts on target hardware without missing library errors

**Verification Method**: CI build verification + target boot test

---

### Scenario 10: BQ40z50 Battery Driver

**Objective**: Verify battery driver port from kernel 4.4 to 6.6 reads correct metrics via SMBus.

```gherkin
Given BQ40z50 driver compiled against Linux 6.6 kernel headers
And BQ40z50 is connected via SMBus (I2C addr 0x0b) on i.MX8M Plus
When driver reads battery metrics at 1 Hz polling interval
Then the following SBS register values shall be valid:
  | Metric | SBS Register | Valid Range |
  | State of Charge | 0x0D | 0-100 % |
  | Voltage | 0x09 | 2800-4200 mV |
  | Current | 0x0A | -5000 to 5000 mA |
  | Temperature | 0x08 | 2731-3431 (0.1 K, 0-70 C) |
  | Remaining Capacity | 0x0F | 0-65535 mAh |
  | Full Charge Capacity | 0x10 | 0-65535 mAh |
And all reads shall complete without I2C NACK errors
And metrics shall be exposed via daemon status API (GET_STATUS response)
```

**Success Criteria**:
- All 6 metrics read correctly (REQ-FW-091)
- No I2C NACK errors in 1000 consecutive reads (REQ-FW-090)
- Power Supply class registration succeeds on kernel 6.6 (REQ-FW-090)
- 1 Hz polling does not interfere with SPI or CSI-2 operations

**Verification Method**: Integration test on target hardware (AC-FW-008)

---

### Scenario 11: Low Battery Graceful Shutdown

**Objective**: Verify graceful scan termination when battery SOC drops below threshold.

```gherkin
Given active continuous scanning
And battery SOC is monitored at 1 Hz
When battery SOC drops below 10%
Then firmware shall:
  | Step | Action |
  | 1 | Log CRITICAL warning to syslog |
  | 2 | Send battery status to Host via port 8001 |
  | 3 | Complete current frame transmission |
  | 4 | Write stop_scan to FPGA via SPI |
  | 5 | Transition Sequence Engine to IDLE |
And when SOC drops below 5%
Then firmware shall initiate emergency shutdown sequence
And no data corruption shall occur during shutdown
```

**Success Criteria**:
- 10% SOC triggers graceful scan stop (REQ-FW-092)
- 5% SOC triggers emergency shutdown (REQ-FW-092)
- Host receives battery status notification
- FPGA left in safe idle state after shutdown

**Verification Method**: Integration test with simulated battery SOC values

---

### Scenario 12: Command Channel Authentication

**Objective**: Verify HMAC-SHA256 authentication on UDP command channel.

```gherkin
Given firmware is listening on UDP port 8001 for commands
When a command packet with valid HMAC-SHA256 and correct sequence number arrives
Then command shall be accepted and processed normally
And when a command packet with invalid HMAC arrives
Then packet shall be discarded silently
And auth-failure counter shall increment by 1
And WARNING level log entry shall be created
And when a command packet with replayed (old) sequence number arrives
Then packet shall be discarded (anti-replay protection)
```

**Success Criteria**:
- Valid commands accepted (REQ-FW-100)
- Invalid HMAC commands rejected (REQ-FW-101)
- Replay attacks detected and blocked (REQ-FW-100)
- Auth-failure counter accessible via GET_STATUS (REQ-FW-101)

**Verification Method**: Unit test + integration test with crafted packets

---

### Scenario 13: Configuration Validation at Startup

**Objective**: Verify firmware validates all configuration parameters at startup.

```gherkin
Given detector_config.yaml contains configuration parameters
When firmware loads configuration at startup
Then the following validation checks shall be performed:
  | Parameter | Valid Range | Invalid Example |
  | resolution.width | 128-4096 | 0, 8192 |
  | resolution.height | 128-4096 | -1, 5000 |
  | bit_depth | 14 or 16 | 8, 32 |
  | fps | 1-60 | 0, 120 |
  | spi_speed_hz | 1000000-50000000 | 0, 100000000 |
  | network.port | 1024-65535 | 80, 70000 |
And if any parameter is out of range:
  | Action |
  | Log ERROR with parameter name and invalid value |
  | Exit with non-zero status (exit code 1) |
  | Do NOT start scanning with invalid config |
```

**Success Criteria**:
- Valid config: daemon starts successfully (REQ-FW-003, REQ-FW-130)
- Invalid config: daemon exits with error (REQ-FW-130)
- All parameter ranges enforced
- Error message identifies the invalid parameter

**Verification Method**: Unit test (FW-UT-04) with valid and invalid YAML files

---

### Scenario 14: Graceful Daemon Shutdown

**Objective**: Verify firmware daemon shuts down cleanly on SIGTERM.

```gherkin
Given firmware daemon is running with active scanning
When SIGTERM signal is sent to the daemon process
Then the daemon shall:
  | Step | Action | Timeout |
  | 1 | Complete in-progress frame TX | 67 ms max (1 frame period) |
  | 2 | Stop CSI-2 streaming (VIDIOC_STREAMOFF) | 100 ms |
  | 3 | Write stop_scan to FPGA via SPI | 10 ms |
  | 4 | Close V4L2, SPI, network sockets | 100 ms |
  | 5 | Flush logs | 100 ms |
  | 6 | Exit with status 0 | - |
And total shutdown time shall be < 5 seconds
And no resource leaks (file descriptors, memory maps) after exit
```

**Success Criteria**:
- Clean shutdown within 5 seconds (REQ-FW-121)
- FPGA left in idle state (stop_scan written)
- All file descriptors closed
- No zombie threads or orphaned resources

**Verification Method**: Integration test with `kill -SIGTERM` and resource leak detection

---

## Edge Case Testing

### Edge Case 1: SPI Communication Failure During CONFIGURE

**Scenario**:
```gherkin
Given Sequence Engine is in CONFIGURE state writing FPGA registers
When SPI write-verify fails for a timing register (read-back mismatch)
Then SPI write shall be retried up to 3 times
And if all 3 retries fail:
  | Action |
  | Transition to ERROR state |
  | Log SPI communication error with register address |
  | Report to Host: FPGA configuration failure |
  | Return to IDLE |
```

**Expected Outcome**:
- Transient SPI errors recovered via retry
- Persistent SPI failure reported and logged
- Sequence Engine does not hang in CONFIGURE state

**Verification Method**: Unit test with SPI mock returning errors

---

### Edge Case 2: Buffer Overrun (TX Slower Than RX)

**Scenario**:
```gherkin
Given 4-buffer ring is active with CSI-2 RX producing at 15 fps
And network TX is temporarily stalled (e.g., 10 GbE link congestion)
When all 4 buffers enter SENDING or FILLING state (no FREE buffers)
Then oldest unsent frame shall be dropped (oldest-drop policy)
And drop counter shall increment
And CSI-2 RX shall NOT be blocked or stalled
And FPGA line buffer shall not overflow
And when network recovers, streaming shall resume automatically
```

**Expected Outcome**:
- Frame drops limited to congestion period only
- CSI-2 RX never blocked (REQ-FW-051)
- Recovery is automatic without manual intervention
- Drop counter accurately reflects drops

**Verification Method**: Integration test with artificial network delay injection

---

### Edge Case 3: Watchdog Timeout

**Scenario**:
```gherkin
Given health monitor watchdog is configured with 5-second timeout
When main loop or critical thread hangs (e.g., deadlock)
And watchdog is not pet for 5 seconds
Then systemd shall detect watchdog timeout
And daemon shall be restarted automatically (Restart=on-failure)
And restart shall occur within RestartSec=5 seconds
And FPGA shall be re-initialized after restart
```

**Expected Outcome**:
- Firmware hang detected within 5 seconds (REQ-FW-060)
- Automatic restart by systemd
- Full re-initialization on restart (config reload, V4L2 setup, SPI setup)
- No persistent state corruption

**Verification Method**: Integration test with artificial thread deadlock

---

### Edge Case 4: Hot Configuration Change During Scan

**Scenario**:
```gherkin
Given continuous scanning is active at 2048x2048, 15 fps
When Host sends SET_CONFIG to change frame rate to 10 fps (hot-swappable)
Then frame rate shall change without stopping the scan
And when Host sends SET_CONFIG to change resolution to 1024x1024 (cold parameter)
Then firmware shall respond with status=BUSY
And Host must send STOP_SCAN before resolution change is accepted
```

**Expected Outcome**:
- Hot parameters change immediately (REQ-FW-131)
- Cold parameters require scan stop first (REQ-FW-131)
- No data corruption during hot parameter change
- Clear error response for cold parameter change during active scan

**Verification Method**: Integration test with parameter change sequences

---

## Performance Criteria

### Frame Throughput

**Criterion**: Sustained frame throughput at Intermediate-A tier without drops.

**Metrics**:
| Tier | Resolution | FPS | Frame Size | Throughput | Drop Rate |
|------|-----------|-----|-----------|-----------|-----------|
| Minimum | 1024x1024 | 15 | 2 MB | 30 MB/s | < 0.01% |
| Intermediate-A | 2048x2048 | 15 | 8 MB | 120 MB/s | < 0.01% |
| Target (800M) | 3072x3072 | 15 | 18 MB | 270 MB/s | < 0.01% |

**Acceptance Threshold**: Drop rate < 0.01% over 10,000 frames (REQ-FW-052)

**Verification Method**: Endurance test (FW-IT-04), HIL test (HIL-B-05)

---

### Latency Budget

**Criterion**: End-to-end frame latency from CSI-2 RX to UDP TX completion.

**Metrics**:
| Operation | Budget | Measured |
|-----------|--------|---------|
| CSI-2 RX DMA (DQBUF) | < 1 ms | TBD (HIL) |
| Frame Manager handoff | < 1 ms | TBD (HIL) |
| UDP TX (8 MB frame at 10 Gbps) | < 10 ms | TBD (HIL) |
| Total end-to-end | < 45 ms | TBD (HIL) |

**Acceptance Threshold**: Total < 45 ms at Intermediate-A tier

**Verification Method**: Timestamp measurement at each stage, HIL validation

---

### SPI Polling Jitter

**Criterion**: SPI polling interval stability under load.

**Metrics**:
| Metric | Target | Measured |
|--------|--------|---------|
| Average interval | 100 us +/- 10 us | TBD |
| P99 interval | < 500 us | TBD |
| Max interval | < 1 ms (99.9%) | TBD |
| SCHED_FIFO effect | > 5x improvement | TBD |

**Acceptance Threshold**: P99 < 500 us with SCHED_FIFO (REQ-FW-022)

**Verification Method**: High-resolution timer benchmark on target hardware

---

## Quality Gates

### TRUST 5 Framework Compliance

**Tested (T)**:
- Unit test coverage >= 85% per module (REQ-FW-004)
- Integration tests cover all acceptance scenarios (FW-IT-01 through FW-IT-05)
- Performance benchmarks validate latency and throughput criteria
- Error injection tests validate all recovery paths

**Readable (R)**:
- Code follows C11 standard with consistent naming conventions
- HAL interfaces documented with Doxygen-compatible comments
- State machines have ASCII diagram documentation in source headers
- Configuration parameters documented with valid ranges

**Unified (U)**:
- SPI transaction format matches FPGA register map (SPEC-FPGA-001)
- Frame header matches architecture design (soc-firmware-design.md Section 6.1)
- Network ports (8000 data, 8001 control) consistent across all documents
- Magic numbers (0xD7E01234 frame, 0xBEEFCAFE command) consistent across firmware and SDK

**Secured (S)**:
- HMAC-SHA256 on command channel (REQ-FW-100)
- Non-root execution with minimal capabilities (REQ-FW-102)
- Input validation on all configuration parameters (REQ-FW-130)
- No buffer overflows in packet handling (bounded payload sizes)

**Trackable (T)**:
- All requirements traced to acceptance criteria (see Traceability Matrix)
- Runtime counters exposed via GET_STATUS (REQ-FW-111)
- Structured syslog logging with severity levels (REQ-FW-110)
- Version tracked in Git with conventional commits

---

### Technical Review Approval

**Review Criteria**:
- All 37 requirements have corresponding acceptance criteria
- EARS format used consistently for all requirements
- No TODO/TBD items remaining in spec.md
- Architecture alignment validated against soc-firmware-design.md
- Performance targets validated against hardware capabilities

**Reviewers**:
- Firmware Lead: SPI, CSI-2, and Sequence Engine implementation review
- System Architect: Architecture alignment and interface consistency
- Security Engineer: HMAC authentication and privilege model review
- Quality Engineer: Test coverage and acceptance criteria completeness

**Approval Criteria**:
- Zero unresolved technical concerns
- All reviewers sign off
- Risk mitigations accepted
- Test plan covers all critical paths

---

## Traceability Matrix

| Requirement ID | Acceptance Criterion | Test Scenario | Quality Gate |
|---------------|---------------------|---------------|--------------|
| REQ-FW-001 | Daemon runs on Linux 6.6.52 | Scenario 9 | Technical Review |
| REQ-FW-002 | C11, CMake cross-build | Scenario 9 | Technical Review |
| REQ-FW-003 | Config loaded from YAML | Scenario 13 | Technical Review |
| REQ-FW-004 | 85%+ coverage | All unit tests | TRUST 5 (T) |
| REQ-FW-010 | V4L2 RAW16 configured | Scenario 3 | Technical Review |
| REQ-FW-011 | MMAP DMA buffers | Scenario 3 | Technical Review |
| REQ-FW-012 | Frame delivery < 1 ms | Scenario 3 | Performance |
| REQ-FW-013 | ISP bypass | Scenario 3 | Technical Review |
| REQ-FW-020 | 32-bit SPI format | Scenario 1 | Technical Review |
| REQ-FW-021 | Write-verify read-back | Scenario 1, Edge Case 1 | Technical Review |
| REQ-FW-022 | 100 us polling | Scenario 2 | Performance |
| REQ-FW-023 | < 10 ms SPI latency | Scenario 1 | Performance |
| REQ-FW-030 | State machine (7 states) | Scenario 5 | Technical Review |
| REQ-FW-031 | StartScan sequence | Scenario 5 | Technical Review |
| REQ-FW-032 | Error recovery (3 retries) | Scenario 6 | Technical Review |
| REQ-FW-033 | Scan modes (Single/Cont/Cal) | Scenario 5 | Technical Review |
| REQ-FW-040 | UDP fragmentation | Scenario 4 | Technical Review |
| REQ-FW-041 | TX within 1 frame period | Scenario 4 | Performance |
| REQ-FW-042 | CRC-16/CCITT | Scenario 4 | Technical Review |
| REQ-FW-043 | Port separation (8000/8001) | Scenario 4, 12 | Technical Review |
| REQ-FW-050 | 4-buffer MMAP ring | Scenario 8 | Technical Review |
| REQ-FW-051 | Oldest-drop policy | Edge Case 2 | Technical Review |
| REQ-FW-052 | Drop rate < 0.01% | Scenario 8 | Performance |
| REQ-FW-060 | Watchdog (5s timeout) | Edge Case 3 | Technical Review |
| REQ-FW-061 | V4L2 pipeline restart | Scenario 7 | Technical Review |
| REQ-FW-070 | No compression | N/A (constraint) | Technical Review |
| REQ-FW-071 | No TCP | N/A (constraint) | Technical Review |
| REQ-FW-080 | Yocto Scarthgap build | Scenario 9 | Technical Review |
| REQ-FW-081 | System packages | Scenario 9 | Technical Review |
| REQ-FW-090 | BQ40z50 kernel 6.6 port | Scenario 10 | Technical Review |
| REQ-FW-091 | Battery metrics (6 values) | Scenario 10 | Technical Review |
| REQ-FW-092 | Low battery shutdown | Scenario 11 | Technical Review |
| REQ-FW-100 | HMAC-SHA256 auth | Scenario 12 | TRUST 5 (S) |
| REQ-FW-101 | Auth failure handling | Scenario 12 | TRUST 5 (S) |
| REQ-FW-102 | Non-root execution | Scenario 14 | TRUST 5 (S) |
| REQ-FW-110 | Structured syslog | Scenario 14 | TRUST 5 (T) |
| REQ-FW-111 | Runtime counters | Scenario 8 | TRUST 5 (T) |
| REQ-FW-112 | GET_STATUS < 50 ms | Scenario 12 | Performance |
| REQ-FW-120 | systemd management | Edge Case 3 | Technical Review |
| REQ-FW-121 | SIGTERM graceful shutdown | Scenario 14 | Technical Review |
| REQ-FW-130 | Config validation | Scenario 13 | Technical Review |
| REQ-FW-131 | Hot/cold parameters | Edge Case 4 | Technical Review |

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | spec-fw agent | Initial acceptance criteria for SPEC-FW-001 |

---

**END OF ACCEPTANCE CRITERIA**
