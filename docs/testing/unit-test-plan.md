# Unit Test Plan

**Project**: X-ray Detector Panel System
**Document Version**: 1.0.0
**Last Updated**: 2026-02-17

---

## Overview

This document defines unit test scenarios for all system modules. Tests are organized by verification layer (RTL and SW) following the Hybrid development methodology (TDD for new code, DDD for existing code).

---

## 1. FPGA RTL Verification (FV-01 to FV-11)

**Tool**: AMD Vivado Simulator / ModelSim / Questa
**Language**: SystemVerilog (testbenches)
**Coverage Targets**: Line >= 95%, Branch >= 90%, FSM 100%
**Methodology**: DDD (ANALYZE-PRESERVE-IMPROVE) with characterization tests

### FV-01: Panel Scan FSM

**Module Under Test**: `panel_scan_fsm.sv`

| Test ID | Description | Stimulus | Expected Result |
|---------|-------------|----------|----------------|
| FV-01-001 | Reset state | Assert `rst_n` low for 10 cycles | FSM in IDLE state, all outputs deasserted |
| FV-01-002 | Start scan | Set `start_scan` high for 1 cycle | Transition IDLE -> GATE_ON, gate output asserted |
| FV-01-003 | Gate timing | Wait for `gate_on_us` duration | Transition GATE_ON -> ROIC_SETTLE |
| FV-01-004 | ROIC settle | Wait for `roic_settle_us` duration | Transition ROIC_SETTLE -> ADC_CONVERT |
| FV-01-005 | ADC conversion | Wait for `adc_conv_us` duration | Transition ADC_CONVERT -> LINE_READOUT |
| FV-01-006 | Line completion | All pixels read from line buffer | Transition LINE_READOUT -> GATE_ON (next line) |
| FV-01-007 | Frame completion | All rows scanned | Transition to FRAME_DONE, frame_done pulse |
| FV-01-008 | Stop scan | Set `stop_scan` during active scan | Transition to IDLE within 1 line time |
| FV-01-009 | Error timeout | No SPI activity for `timeout_ms` | Transition to ERROR state, error flag set |
| FV-01-010 | Error recovery | Assert reset after ERROR state | Transition ERROR -> IDLE |
| FV-01-011 | Back-to-back frames | Continuous scanning without stop | Frame counter increments, no state corruption |

**Coverage Requirement**: 100% FSM state coverage, 100% transition coverage

---

### FV-02: Line Buffer (Ping-Pong BRAM)

**Module Under Test**: `line_buffer.sv`

| Test ID | Description | Stimulus | Expected Result |
|---------|-------------|----------|----------------|
| FV-02-001 | Single line write | Write 2048 pixels to buffer A | All pixels stored correctly |
| FV-02-002 | Single line read | Read buffer A after write complete | Pixel data matches written values |
| FV-02-003 | Ping-Pong swap | Write buffer A, then switch to buffer B | Buffer A available for read while B is written |
| FV-02-004 | Full line 3072 pixels | Write 3072 pixels (max tier) | All pixels stored, no overflow |
| FV-02-005 | Overflow protection | Write more than max pixels per line | Write stops, overflow flag set |
| FV-02-006 | Concurrent R/W | Read buffer A while writing buffer B | No data corruption, both operations complete |
| FV-02-007 | Reset behavior | Assert reset during mid-write | All pointers reset, no partial data |
| FV-02-008 | 14-bit data | Write 14-bit pixel values | Upper 2 bits zero-padded in 16-bit BRAM |

**Coverage Requirement**: Line >= 95%, Branch >= 90%

---

### FV-03: CSI-2 TX

**Module Under Test**: `csi2_tx_wrapper.sv` (wrapper around AMD CSI-2 TX IP)

| Test ID | Description | Stimulus | Expected Result |
|---------|-------------|----------|----------------|
| FV-03-001 | Frame Start packet | Begin new frame transmission | FS packet with correct VC and data type |
| FV-03-002 | Line data packet | Transmit one line of RAW16 data | Packet with correct word count and CRC-16 |
| FV-03-003 | Frame End packet | Complete frame transmission | FE packet after last line |
| FV-03-004 | CRC-16 validation | Known test vector | CRC matches precomputed value |
| FV-03-005 | RAW16 data type | Configure for 16-bit pixels | Data type = 0x2C in packet header |
| FV-03-006 | RAW14 data type | Configure for 14-bit pixels | Data type = 0x2D in packet header |
| FV-03-007 | Multi-frame sequence | Transmit 10 consecutive frames | Sequential frame numbers, no gaps |
| FV-03-008 | AXI4-Stream backpressure | Deassert `tready` mid-line | TX pauses, resumes when ready |
| FV-03-009 | Line blanking | Inter-line gap timing | Blanking >= configured clocks |
| FV-03-010 | Frame blanking | Inter-frame gap timing | Blanking >= configured lines |

**Coverage Requirement**: Line >= 95%, Branch >= 90%

---

### FV-04: SPI Slave

**Module Under Test**: `spi_slave.sv`

| Test ID | Description | Stimulus | Expected Result |
|---------|-------------|----------|----------------|
| FV-04-001 | Register write | SPI write to address 0x00 | CONTROL register updated |
| FV-04-002 | Register read | SPI read from address 0x04 | STATUS register value returned |
| FV-04-003 | Frame counter read | SPI read address 0x08 | 32-bit frame counter value |
| FV-04-004 | Error flags read | SPI read address 0x10 | Current error flag state |
| FV-04-005 | Invalid address | SPI access to unmapped address | Returns 0x00000000, no side effects |
| FV-04-006 | SPI Mode 0 timing | CPOL=0, CPHA=0 waveform | Data sampled on rising edge |
| FV-04-007 | Maximum clock rate | SPI at 50 MHz | No setup/hold violations |
| FV-04-008 | CS deassertion | CS_N goes high mid-transaction | Transaction aborted, no register corruption |

**Coverage Requirement**: Line >= 95%, Branch >= 90%

---

### FV-05: Protection Logic

**Module Under Test**: `protection_logic.sv`

| Test ID | Description | Stimulus | Expected Result |
|---------|-------------|----------|----------------|
| FV-05-001 | Watchdog timeout | No SPI activity for `timeout_ms` | Timeout flag set, scan stopped |
| FV-05-002 | Overexposure detect | Pixel value > `overexposure_threshold` | Overexposure flag set |
| FV-05-003 | Buffer overflow | Line buffer overflow signal | Overflow flag set, configured action taken |
| FV-05-004 | CRC error | CSI-2 CRC mismatch detected | CRC error flag set |
| FV-05-005 | Error clearing | Write to error clear register | All error flags cleared |
| FV-05-006 | Multiple simultaneous errors | Timeout + overflow at same cycle | Both flags set independently |

**Coverage Requirement**: Line >= 95%, Branch >= 90%

---

### FV-06 to FV-11: Additional RTL Modules

| Test ID | Module | Key Tests |
|---------|--------|-----------|
| FV-06 | Clock Manager | PLL lock, frequency accuracy, reset recovery |
| FV-07 | Reset Controller | Power-on reset, watchdog reset, soft reset sequencing |
| FV-08 | D-PHY Serializer | OSERDES configuration, lane alignment, LP/HS transition |
| FV-09 | Frame Timing Generator | Configurable resolution/fps, blanking intervals |
| FV-10 | Test Pattern Generator | Counter, checkerboard, PRBS patterns |
| FV-11 | Top-Level Integration | Full datapath: panel -> buffer -> CSI-2 -> output |

---

## 2. Software Unit Tests (xUnit / pytest)

**Framework**: xUnit (C# .NET 8.0+)
**Coverage Target**: 80-90% per module
**Methodology**: TDD (RED-GREEN-REFACTOR) for new code

### SW-01: PanelSimulator

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|----------------|
| SW-01-001 | Default initialization | Default config | 2048x2048 pixel matrix, all zeros |
| SW-01-002 | Custom resolution | rows=1024, cols=1024 | 1024x1024 pixel matrix |
| SW-01-003 | Gaussian noise | noiseStdDev=100 | Pixel values follow N(0, 100) distribution |
| SW-01-004 | Dead pixel injection | defectRate=0.001 | ~0.1% pixels marked as defective |
| SW-01-005 | Hot pixel injection | defectRate=0.001 | ~0.1% pixels at max value |
| SW-01-006 | Frame generation | Generate 1 frame | 2D array with noise + defects |
| SW-01-007 | Bit depth clipping | 14-bit mode, value > 16383 | Clipped to 16383 |
| SW-01-008 | ISimulator interface | Call via ISimulator | All interface methods functional |

---

### SW-02: FpgaSimulator

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|----------------|
| SW-02-001 | SPI register write/read | Write 0xFF to CONTROL | Read returns 0xFF |
| SW-02-002 | FSM state transitions | Start scan command | State: IDLE -> SCANNING |
| SW-02-003 | Line buffer simulation | Input pixel line | Buffered line matches input |
| SW-02-004 | CSI-2 packet generation | One line of pixel data | Valid CSI-2 packet with CRC |
| SW-02-005 | Frame counter | Complete 5 frames | Frame counter = 5 |
| SW-02-006 | Golden reference output | Known input frame | Bit-exact expected output |
| SW-02-007 | Config from YAML | Load detector_config.yaml | All parameters match |
| SW-02-008 | Protection timeout | No SPI for timeout_ms | Error state triggered |

---

### SW-03: McuSimulator

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|----------------|
| SW-03-001 | CSI-2 RX receive | CSI-2 packet stream | Frame buffer populated |
| SW-03-002 | SPI master write | Write CONTROL register | FPGA SPI slave receives data |
| SW-03-003 | Ethernet TX packet | One frame | UDP packets with frame header |
| SW-03-004 | Frame header format | Generate header | Magic=0xDEADBEEF, correct seq/size |
| SW-03-005 | Sequence engine | Start/stop scan commands | SPI transactions to FPGA |
| SW-03-006 | Frame buffer management | 4-buffer ping-pong | No buffer overwrite during TX |
| SW-03-007 | HAL abstraction | Call HAL methods | Correct driver layer invocation |

---

### SW-04: HostSimulator

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|----------------|
| SW-04-001 | UDP packet receive | Single UDP packet | Packet stored in receive buffer |
| SW-04-002 | Frame reassembly | All packets for 1 frame | Complete 2D frame |
| SW-04-003 | Out-of-order packets | Packets in random order | Correctly reassembled frame |
| SW-04-004 | Missing packet handling | Missing packet #512 | Frame marked incomplete |
| SW-04-005 | Frame timeout | Missing packets after timeout | Incomplete frame reported |
| SW-04-006 | TIFF storage | Save frame to TIFF | Valid TIFF file, correct pixel data |
| SW-04-007 | RAW storage | Save frame to RAW | Binary file, correct size |
| SW-04-008 | Multi-frame sequence | Receive 10 frames | All frames reassembled in order |

---

### SW-05: ParameterExtractor

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|----------------|
| SW-05-001 | PDF parsing | Sample datasheet PDF | Extracted parameter table |
| SW-05-002 | Rule engine | Parameter with min/max range | Validation pass/fail |
| SW-05-003 | YAML generation | Extracted parameters | Valid detector_config.yaml |
| SW-05-004 | GUI data binding | Parameter list | WPF DataGrid populated |

---

### SW-06: CodeGenerator

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|----------------|
| SW-06-001 | RTL skeleton | detector_config.yaml | SystemVerilog module with parameters |
| SW-06-002 | MCU skeleton | detector_config.yaml | C header with register definitions |
| SW-06-003 | SDK skeleton | detector_config.yaml | C# class with API methods |
| SW-06-004 | Compile check | Generated RTL | Vivado synthesis pass (no errors) |

---

### SW-07: ConfigConverter

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|----------------|
| SW-07-001 | YAML to XDC | detector_config.yaml | Valid .xdc with timing constraints |
| SW-07-002 | YAML to DTS | detector_config.yaml | Valid device tree overlay |
| SW-07-003 | YAML to JSON | detector_config.yaml | Valid SDK config JSON |
| SW-07-004 | Schema validation | Valid YAML | Schema validation passes |
| SW-07-005 | Schema validation | Invalid YAML (missing field) | Schema validation fails with error |
| SW-07-006 | Bandwidth check | Config exceeding CSI-2 limit | Warning or error reported |
| SW-07-007 | Round-trip | YAML -> JSON -> YAML | Values preserved exactly |

---

### SW-08: IntegrationRunner

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|----------------|
| SW-08-001 | CLI argument parsing | `--scenario IT-01` | Correct scenario loaded |
| SW-08-002 | Scenario execution | IT-01 config | All simulators instantiated and connected |
| SW-08-003 | Result reporting | Completed scenario | Pass/fail summary with metrics |
| SW-08-004 | Error handling | Invalid scenario name | Clear error message |

---

## 3. Common.Dto Tests

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|----------------|
| SW-09-001 | ISimulator contract | Mock implementation | All interface methods callable |
| SW-09-002 | DTO serialization | Create FrameData DTO | JSON round-trip preserves values |
| SW-09-003 | DTO validation | Invalid FrameData (negative size) | Validation exception thrown |

---

## Test Execution Strategy

### Continuous Integration

```
git push → CI pipeline:
  1. dotnet build (all projects)
  2. dotnet test (all xUnit tests)
  3. Coverage report (target: 80-90% per module)
  4. Static analysis (ruff/lint)
```

### RTL Test Execution

```
vivado -mode batch -source run_tests.tcl:
  1. Compile testbenches
  2. Run simulations (FV-01 to FV-11)
  3. Generate coverage report
  4. Check: Line >= 95%, Branch >= 90%, FSM 100%
```

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (analyst) | Initial unit test plan |

---
