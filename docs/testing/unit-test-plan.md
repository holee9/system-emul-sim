# Unit Test Plan

**Project**: X-ray Detector Panel System
**Document Version**: 2.0.0
**Last Updated**: 2026-02-17
**Status**: Reviewed - Approved

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

**FSM State Definitions** (per SPEC-FPGA-001 REQ-FPGA-010):
- IDLE: Waiting for start_scan command
- INTEGRATE: Gate signal active, sensor integrating
- READOUT: Pixel data being read from ROIC
- LINE_DONE: One line complete, preparing next
- FRAME_DONE: All lines complete
- ERROR: Fault condition detected

| Test ID | Description | Stimulus | Expected Result |
|---------|-------------|----------|----------------|
| FV-01-001 | Reset state | Assert `rst_n` low for 10 cycles | FSM in IDLE state, all outputs deasserted |
| FV-01-002 | Start scan | Set `start_scan` high for 1 cycle | Transition IDLE -> INTEGRATE, gate output asserted |
| FV-01-003 | Gate timing | Wait for `gate_on_us` duration | Transition INTEGRATE -> READOUT |
| FV-01-004 | ROIC settle | Wait for `roic_settle_us` duration | Transition READOUT -> LINE_DONE |
| FV-01-005 | ADC conversion | Wait for `adc_conv_us` duration | Transition LINE_DONE -> FRAME_DONE |
| FV-01-006 | Line completion | All pixels read from line buffer | Transition LINE_DONE -> INTEGRATE (next line) |
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
| FV-05-007 | ROIC Fault Error (0x10) | Assert `roic_fault` signal | ERROR_FLAGS bit[4] = 1, FSM -> ERROR state within 10 clock cycles; gate output goes LOW immediately |
| FV-05-008 | D-PHY Error (0x20) | Assert `dphy_error` signal | ERROR_FLAGS bit[5] = 1, FSM -> ERROR state within 10 clock cycles; CSI-2 transmission halted |
| FV-05-009 | Configuration Error (0x40) | Write invalid panel dimensions (rows=0) | ERROR_FLAGS bit[6] = 1, scan prevented from starting; control register SCAN_ENABLE bit cannot be set |
| FV-05-010 | Fatal Error Gate Safety | Trigger ROIC fault (0x10) during INTEGRATE state | gate_on output LOW within 1 clock cycle of fault detection; per REQ-FPGA-052, fatal errors force gate to safe state |

**Coverage Requirement**: Line >= 95%, Branch >= 90%

---

### FV-06: Clock Manager

**Module Under Test**: `clock_manager.sv` (MMCM wrapper)

| Test ID | Description | Stimulus | Expected Result |
|---------|-------------|----------|----------------|
| FV-06-001 | PLL lock after reset | Release reset, wait for lock | MMCM locked signal asserted within 10 ms |
| FV-06-002 | clk_sys frequency | Measure clk_sys period | 100 MHz +/- 0.1% (10 ns period) |
| FV-06-003 | clk_pixel frequency | Measure clk_pixel period | 125.83 MHz +/- 0.1% |
| FV-06-004 | clk_csi2_byte frequency | Measure clk_csi2_byte period | 125 MHz +/- 0.1% |
| FV-06-005 | clk_dphy_hs frequency | Measure clk_dphy_hs period | 500 MHz +/- 0.1% |
| FV-06-006 | Reset during locked | Assert reset while PLL locked | PLL re-locks within 10 ms after reset release |
| FV-06-007 | Input clock loss | Remove 100 MHz input | Lock lost signal asserted, all outputs disabled |
| FV-06-008 | Clock phase relationship | Measure clk_sys to clk_csi2_byte skew | Phase alignment within 500 ps |

**Coverage Requirement**: Line >= 95%, Branch >= 90%

---

### FV-07: Reset Controller

**Module Under Test**: `reset_controller.sv`

| Test ID | Description | Stimulus | Expected Result |
|---------|-------------|----------|----------------|
| FV-07-001 | Power-on reset (POR) | Apply power-on sequence | All modules held in reset until PLL lock |
| FV-07-002 | POR release sequence | PLL lock asserted | Reset released: clk_sys domain first, then clk_csi2_byte, then clk_dphy_hs |
| FV-07-003 | Software reset | SPI write CONTROL bit[2] = 1 | All modules reset, PLL remains locked |
| FV-07-004 | Software reset release | Software reset auto-clears | Reset sequence same as POR (sequential per domain) |
| FV-07-005 | Watchdog reset | Watchdog timeout trigger | Full system reset (equivalent to POR) |
| FV-07-006 | Reset synchronization | Reset deassertion | Synchronous deassert in each clock domain (2-FF sync) |
| FV-07-007 | Nested reset | Software reset during POR recovery | System stays in reset, single clean release |
| FV-07-008 | Reset during active scan | Assert software reset during SCANNING | FSM returns to IDLE, all outputs safe |

**Coverage Requirement**: Line >= 95%, Branch >= 90%

---

### FV-08: D-PHY Serializer

**Module Under Test**: `dphy_serializer.sv` (OSERDES2 wrapper)

| Test ID | Description | Stimulus | Expected Result |
|---------|-------------|----------|----------------|
| FV-08-001 | OSERDES2 configuration | Initialize at 400 Mbps/lane | 10:1 DDR serialization, LVDS_25 output |
| FV-08-002 | Lane alignment | Transmit known pattern on all 4 lanes | All lanes produce identical bit patterns with < 1 UI skew |
| FV-08-003 | LP to HS transition | Switch from LP mode to HS mode | LP-00 -> HS-0 -> payload, timing per D-PHY spec |
| FV-08-004 | HS to LP transition | End of packet transmission | HS -> LP-11, proper trail sequence |
| FV-08-005 | Clock lane continuous | Enable HS clock lane | Continuous clock output at configured frequency |
| FV-08-006 | Data lane idle | No active transmission | All data lanes in LP-11 state |
| FV-08-007 | 800 Mbps lane speed | Configure for 800 Mbps/lane | Serialization correct at higher rate (timing closure pending) |
| FV-08-008 | Differential output swing | Measure output swing | 200 mV typical, within D-PHY specification |

**Coverage Requirement**: Line >= 95%, Branch >= 90%

---

### FV-09: Frame Timing Generator

**Module Under Test**: `frame_timing_gen.sv`

| Test ID | Description | Stimulus | Expected Result |
|---------|-------------|----------|----------------|
| FV-09-001 | Configurable resolution | Set rows=2048, cols=2048 | Line count = 2048, pixel count per line = 2048 |
| FV-09-002 | Configurable FPS | Set target FPS = 15 | Frame interval = 66.67 ms +/- 1% |
| FV-09-003 | Line blanking | Configure inter-line gap | Blanking >= configured clock cycles between lines |
| FV-09-004 | Frame blanking | Configure inter-frame gap | Blanking >= configured lines between frames |
| FV-09-005 | Minimum resolution | Set rows=1024, cols=1024 | Valid frame timing generated |
| FV-09-006 | Maximum resolution | Set rows=3072, cols=3072 | Valid frame timing generated (within 800M bandwidth) |
| FV-09-007 | FPS change mid-stream | Update FPS register during continuous scan | New FPS applied at next frame boundary |
| FV-09-008 | Invalid configuration | Set rows=0 | Configuration error flag set, scan prevented |

**Coverage Requirement**: Line >= 95%, Branch >= 90%

---

### FV-10: Test Pattern Generator

**Module Under Test**: `test_pattern_gen.sv`

| Test ID | Description | Stimulus | Expected Result |
|---------|-------------|----------|----------------|
| FV-10-001 | Counter pattern | Select counter mode, 1024x1024 | pixel[r][c] = (r * cols + c) % 2^16 |
| FV-10-002 | Checkerboard pattern | Select checkerboard mode | Alternating 0x0000 / 0xFFFF, inverted each row |
| FV-10-003 | Constant pattern | Select constant mode, value=0x8000 | All pixels = 0x8000 |
| FV-10-004 | PRBS pattern | Select PRBS mode, seed=0x1234 | Output matches LFSR reference model |
| FV-10-005 | Pattern at 14-bit depth | Counter pattern with 14-bit mode | Values clamped to 0x3FFF maximum |
| FV-10-006 | Resolution change | Switch from 1024x1024 to 2048x2048 | Pattern adapts to new resolution |
| FV-10-007 | Multi-frame consistency | Generate 10 frames of counter pattern | Frame N pixel[0][0] = N * total_pixels % 2^16 |
| FV-10-008 | Bypass to panel data | Select bypass mode | External ROIC data passed through unmodified |

**Coverage Requirement**: Line >= 95%, Branch >= 90%

---

### FV-11: Top-Level Integration

**Module Under Test**: `fpga_top.sv`

| Test ID | Description | Stimulus | Expected Result |
|---------|-------------|----------|----------------|
| FV-11-001 | Full datapath | Counter pattern, 1024x1024 | CSI-2 output matches expected packets bit-exactly |
| FV-11-002 | SPI to CSI-2 flow | SPI start_scan -> CSI-2 output | Complete frame from command to last packet |
| FV-11-003 | Multi-frame continuous | 10 frames continuous mode | All 10 frames output correctly, sequential frame_seq |
| FV-11-004 | Error propagation | Inject timeout error | ERROR_FLAGS set, CSI-2 halted, gate LOW |
| FV-11-005 | Error recovery | Clear error, restart scan | Normal operation resumes |
| FV-11-006 | Resource utilization | Post-implementation analysis | LUT < 60%, BRAM < 50%, WNS >= 1 ns |
| FV-11-007 | Clock domain interaction | Full pipeline operating | Zero CDC violations in simulation |
| FV-11-008 | Intermediate-A throughput | 2048x2048, 16-bit, 15 fps | Measured CSI-2 output >= 1.01 Gbps, zero drops |

**Coverage Requirement**: Line >= 95%, Branch >= 90%, FSM 100% (aggregate)

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
| SW-03-004 | Frame header format | Generate header | Magic=0xD7E01234, correct seq/size |
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

## 4. SoC Firmware Unit Tests (CMocka / Unity)

**Framework**: CMocka or Unity (C)
**Coverage Target**: 85%+ per module
**Methodology**: TDD for new code, DDD for HAL integration and battery driver port
**Cross-Compiler**: aarch64-poky-linux-gcc (Yocto Scarthgap SDK)
**Reference**: SPEC-FW-001, docs/architecture/soc-firmware-design.md

### FW-UT-01: SPI Master HAL

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|----------------|
| FW-UT-01-001 | Register write | Write 0x1234 to addr 0x20 | spidev ioctl called with Word0=[0x20<<8\|0x01], Word1=[0x1234] |
| FW-UT-01-002 | Register read | Read from addr 0x20 | spidev ioctl called with Word0=[0x20<<8\|0x00], returns Word1 data |
| FW-UT-01-003 | Write-verify success | Write 0xABCD, read-back returns 0xABCD | Write succeeds on first attempt |
| FW-UT-01-004 | Write-verify retry | Read-back mismatch on first 2 attempts | Retry 3 times, succeed on 3rd |
| FW-UT-01-005 | Write-verify exhausted | Read-back mismatch on all 3 attempts | Return error code, log SPI error |
| FW-UT-01-006 | SPI configuration | Open /dev/spidev0.0 | Mode 0, 16-bit word, 50 MHz confirmed |
| FW-UT-01-007 | Transaction latency | Round-trip timing | < 10 ms per transaction |
| FW-UT-01-008 | Invalid address | Write to addr 0xFF (unmapped) | No crash, operation completes gracefully |

---

### FW-UT-02: Frame Header Encode/Decode

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|----------------|
| FW-UT-02-001 | Header encode | Frame seq=1, 2048x2048, 16-bit | 32-byte packed header with magic=0xD7E01234 |
| FW-UT-02-002 | Header decode | 32-byte packed header buffer | All fields parsed correctly |
| FW-UT-02-003 | Round-trip | Encode then decode same header | All fields match original values |
| FW-UT-02-004 | CRC-16 in header | Encode header | CRC-16 field matches independent CRC calculation |
| FW-UT-02-005 | Packet index range | packet_index=0 to 1023 | All indices encoded correctly in header |
| FW-UT-02-006 | Flags field | last_packet=1, error_frame=0 | Flags bits set correctly |
| FW-UT-02-007 | Endianness | Encode on little-endian host | All multi-byte fields in little-endian |
| FW-UT-02-008 | Invalid magic | Decode buffer with wrong magic | Decode returns error code |

---

### FW-UT-03: CRC-16 Calculation

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|----------------|
| FW-UT-03-001 | Empty input | Zero-length buffer | CRC = 0xFFFF (initial value) |
| FW-UT-03-002 | Single byte | 0x00 | CRC matches reference implementation |
| FW-UT-03-003 | Known test vector | "123456789" ASCII | CRC-16/CCITT = 0x29B1 |
| FW-UT-03-004 | Frame header CRC | 30-byte header (excluding CRC field) | CRC matches header.crc16 field |
| FW-UT-03-005 | Large buffer | 8192 bytes of 0xAA | CRC matches reference implementation |
| FW-UT-03-006 | Incremental update | CRC computed in 2 chunks vs 1 | Results are identical |

---

### FW-UT-04: Configuration YAML Parsing

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|----------------|
| FW-UT-04-001 | Valid config | Complete detector_config.yaml | All parameters loaded correctly |
| FW-UT-04-002 | Missing file | Non-existent path | Error code returned, error logged |
| FW-UT-04-003 | Invalid YAML | Malformed YAML syntax | Parser error, daemon exits with code 1 |
| FW-UT-04-004 | Out-of-range resolution | width=0 | Validation fails, error identifies parameter |
| FW-UT-04-005 | Out-of-range FPS | fps=120 | Validation fails (max 60) |
| FW-UT-04-006 | Out-of-range bit_depth | bit_depth=8 | Validation fails (must be 14 or 16) |
| FW-UT-04-007 | Out-of-range SPI speed | spi_speed=100000000 | Validation fails (max 50 MHz) |
| FW-UT-04-008 | Hot/cold classification | Frame rate parameter | Classified as hot-swappable |
| FW-UT-04-009 | Hot/cold classification | Resolution parameter | Classified as cold (requires restart) |
| FW-UT-04-010 | Default values | Config with missing optional fields | Defaults applied correctly |

---

### FW-UT-05: Sequence Engine FSM

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|----------------|
| FW-UT-05-001 | Initial state | After initialization | State = IDLE |
| FW-UT-05-002 | IDLE to CONFIGURE | StartScan command received | Transition to CONFIGURE, SPI writes begin |
| FW-UT-05-003 | CONFIGURE to ARM | All registers written and verified | Transition to ARM, start_scan written to FPGA |
| FW-UT-05-004 | ARM to SCANNING | STATUS.busy asserted within 10 ms | Transition to SCANNING |
| FW-UT-05-005 | ARM timeout | STATUS.busy not asserted in 10 ms | Transition to ERROR |
| FW-UT-05-006 | SCANNING to STREAMING | Frame received via CSI-2 | Transition to STREAMING |
| FW-UT-05-007 | STREAMING to COMPLETE | Frame TX complete | Transition to COMPLETE |
| FW-UT-05-008 | COMPLETE continuous | Continuous mode active | Transition to ARM (loop) |
| FW-UT-05-009 | COMPLETE single | Single mode active | Transition to IDLE |
| FW-UT-05-010 | Error detection | FPGA STATUS.error set | Transition to ERROR |
| FW-UT-05-011 | Error recovery | Error clear and retry | Retry up to 3 times, then report |
| FW-UT-05-012 | Stop command | StopScan during SCANNING | Write stop_scan to FPGA, transition to IDLE |
| FW-UT-05-013 | Calibration mode | StartScan with calibration flag | Gate OFF during INTEGRATE (dark frame) |

---

### FW-UT-06: Frame Buffer Manager

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|----------------|
| FW-UT-06-001 | Initialization | 4 buffer allocation | 4 buffers in FREE state |
| FW-UT-06-002 | FREE to FILLING | CSI-2 RX starts DMA | Buffer transitions to FILLING |
| FW-UT-06-003 | FILLING to READY | DMA complete (DQBUF) | Buffer transitions to READY |
| FW-UT-06-004 | READY to SENDING | TX thread picks up frame | Buffer transitions to SENDING |
| FW-UT-06-005 | SENDING to FREE | TX complete | Buffer transitions to FREE |
| FW-UT-06-006 | Oldest-drop policy | All 4 buffers in use, new frame arrives | Oldest unsent frame dropped, counter incremented |
| FW-UT-06-007 | Drop counter accuracy | Drop 3 frames | drop_counter = 3 |
| FW-UT-06-008 | No deadlock | Rapid produce/consume cycles (1000) | All buffers cycle correctly |
| FW-UT-06-009 | RX never blocked | All buffers SENDING | CSI-2 RX QBUF still accepted (oldest dropped) |

---

### FW-UT-07: Command Protocol (HMAC Auth)

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|----------------|
| FW-UT-07-001 | Valid START_SCAN | HMAC-valid command packet | Command accepted, scan initiated |
| FW-UT-07-002 | Valid STOP_SCAN | HMAC-valid command packet | Command accepted, scan stopped |
| FW-UT-07-003 | Valid GET_STATUS | HMAC-valid command packet | Status response sent within 50 ms |
| FW-UT-07-004 | Invalid HMAC | Corrupted HMAC field | Packet discarded, auth-failure counter++ |
| FW-UT-07-005 | Replay attack | Valid HMAC but old sequence number | Packet discarded, auth-failure counter++ |
| FW-UT-07-006 | Unknown command ID | Valid HMAC, command_id=0xFFFF | Error response sent |
| FW-UT-07-007 | Response format | GET_STATUS command | Response with magic=0xCAFEBEEF, status=OK |
| FW-UT-07-008 | SET_CONFIG cold param | Change resolution during scan | Response status=BUSY |

---

### FW-UT-08: Health Monitor

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|----------------|
| FW-UT-08-001 | Watchdog pet | Pet within 1s interval | No timeout triggered |
| FW-UT-08-002 | Watchdog timeout | No pet for 5 seconds | Timeout detected |
| FW-UT-08-003 | Runtime counters | Receive 10 frames, send 9 | frames_received=10, frames_sent=9, frames_dropped=1 |
| FW-UT-08-004 | Counter reset | Reset counters | All counters = 0 |
| FW-UT-08-005 | Status assembly | GET_STATUS query | Response includes state, counters, FPGA status |
| FW-UT-08-006 | Syslog output | Log ERROR event | Structured log with timestamp, module, severity |

---

## 5. Host SDK Unit Tests (xUnit / .NET 8.0)

**Framework**: xUnit + FluentAssertions
**Coverage Target**: 85%+ per module
**Methodology**: TDD (RED-GREEN-REFACTOR) for all new SDK code
**Reference**: SPEC-SDK-001, docs/architecture/host-sdk-design.md

### SDK-01: PacketReceiver

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|----------------|
| SDK-01-001 | UDP socket binding | Bind to port 8000 | Socket created and bound successfully |
| SDK-01-002 | Packet reception | 1 valid UDP packet | Packet queued for processing |
| SDK-01-003 | CRC-16 validation | Packet with valid CRC | Packet accepted |
| SDK-01-004 | CRC-16 rejection | Packet with invalid CRC | Packet discarded, warning logged |
| SDK-01-005 | High-rate reception | 1024 packets at line rate | All packets queued, zero drops in receiver |
| SDK-01-006 | Buffer overflow | Receive queue full | Oldest packet dropped, counter incremented |
| SDK-01-007 | Magic number check | Packet with wrong magic (not 0xD7E01234) | Packet discarded |
| SDK-01-008 | Socket close | DisconnectAsync called | Socket released, no resource leak |

---

### SDK-02: FrameReassembler

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|----------------|
| SDK-02-001 | In-order assembly | 1024 packets, sequential | Complete frame, bit-exact match |
| SDK-02-002 | Out-of-order assembly | 1024 packets, shuffled | Complete frame, bit-exact match |
| SDK-02-003 | Missing packets | 5% packets missing, timeout 2s | Frame marked incomplete, missing regions zero-filled |
| SDK-02-004 | Duplicate packets | Same packet_index repeated | Duplicate ignored, no data corruption |
| SDK-02-005 | Concurrent frames | 2 frames interleaved | Both frames reassembled independently |
| SDK-02-006 | Frame timeout | Incomplete frame after 2s | Frame expired, ErrorOccurred event raised |
| SDK-02-007 | Max reassembly slots | 8 concurrent incomplete frames | Oldest slot recycled on 9th frame |
| SDK-02-008 | Reassembly latency | All packets delivered | Frame available within 10 ms of last packet |

---

### SDK-03: DetectorClient API Contract

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|----------------|
| SDK-03-001 | IDetectorClient interface | Mock implementation | All 8 methods + 3 events are callable |
| SDK-03-002 | ConnectAsync success | Reachable SoC address | IsConnected = true within 10s |
| SDK-03-003 | ConnectAsync timeout | Unreachable address | DetectorConnectionException after 10s |
| SDK-03-004 | StartAcquisition not connected | Call before ConnectAsync | InvalidOperationException thrown |
| SDK-03-005 | StartAcquisition already scanning | Call twice | InvalidOperationException on second call |
| SDK-03-006 | CaptureFrameAsync | Single mode, valid frame | Frame returned with correct dimensions |
| SDK-03-007 | StreamFramesAsync | Continuous mode, 10 frames | IAsyncEnumerable yields 10 frames |
| SDK-03-008 | DisconnectAsync | Connected client | IsConnected = false, resources released |
| SDK-03-009 | GetStatusAsync | During scan | ScanStatus with counters populated |
| SDK-03-010 | Auto-reconnect | Network drop for 15s | Reconnect within 10s of network recovery |

---

### SDK-04: ImageEncoder

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|----------------|
| SDK-04-001 | TIFF encode | 2048x2048 16-bit frame | Valid TIFF file, correct tags |
| SDK-04-002 | TIFF round-trip | Save then load same frame | Pixel data bit-exact match |
| SDK-04-003 | RAW encode | 2048x2048 16-bit frame | 8,388,608 byte binary file + JSON sidecar |
| SDK-04-004 | RAW round-trip | Save then load same frame | Pixel data bit-exact match |
| SDK-04-005 | TIFF metadata tags | Frame with metadata | IMAGEWIDTH, IMAGELENGTH, BITSPERSAMPLE=16 present |
| SDK-04-006 | JSON sidecar content | Frame with metadata | width, height, bitDepth, timestamp, sequenceNumber |
| SDK-04-007 | Invalid path | Save to non-existent directory | IOException thrown |

---

### SDK-05: Frame Memory Management

| Test ID | Description | Input | Expected Output |
|---------|-------------|-------|----------------|
| SDK-05-001 | ArrayPool allocation | Create frame | Pixel buffer from ArrayPool<ushort> |
| SDK-05-002 | Frame Dispose | Dispose frame | Buffer returned to pool |
| SDK-05-003 | Double dispose | Dispose frame twice | No exception, idempotent |
| SDK-05-004 | GC pressure test | 1000 frame create/dispose cycle | Gen2 GC count < 5 |
| SDK-05-005 | Memory stability | 10,000 frames, 11 min simulated | Heap growth < 100 MB |

---

## Test Execution Strategy

### Continuous Integration (Simulators + SDK)

```
git push → CI pipeline:
  1. dotnet build (all projects: simulators, SDK, tools)
  2. dotnet test (SW-01 to SW-09, SDK-01 to SDK-05)
  3. Coverage report via coverlet (target: 85%+ per module)
  4. Static analysis (ruff/lint, EditorConfig)
```

### RTL Test Execution

```
vivado -mode batch -source run_tests.tcl:
  1. Compile testbenches (SystemVerilog)
  2. Run simulations (FV-01 to FV-11)
  3. Generate coverage report
  4. Check: Line >= 95%, Branch >= 90%, FSM 100%
```

### Firmware Test Execution

```
Yocto SDK cross-compile + host-side mock execution:
  1. source /opt/fsl-imx-xwayland/scarthgap/environment-setup-cortexa53-crypto-poky-linux
  2. cmake -DCMAKE_TOOLCHAIN_FILE=toolchain/imx8mp-toolchain.cmake -DBUILD_TESTS=ON ..
  3. make -j$(nproc)
  4. ctest --output-on-failure (FW-UT-01 to FW-UT-08)
  5. gcov + lcov coverage report (target: 85%+ per module)
  6. Host-side tests run with mock HAL (spidev mock, V4L2 mock, socket mock)
```

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (analyst) | Initial unit test plan |
| 2.0.0 | 2026-02-17 | spec-fw agent | Expanded FV-06 to FV-11 (48 detailed RTL tests). Added SoC Firmware tests FW-UT-01 to FW-UT-08 (65 tests). Added Host SDK tests SDK-01 to SDK-05 (37 tests). Fixed SW-03-004 magic number. Added firmware test execution strategy. |

---
