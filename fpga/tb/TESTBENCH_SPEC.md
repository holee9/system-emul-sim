# Testbench Development Specification

## TDD Approach for FPGA RTL Verification

This document defines the testbench-first (TDD) approach for SPEC-FPGA-001 RTL verification. Testbenches define expected behavior; RTL implementation must satisfy these tests.

## Module Testbench Specifications

### 1. Panel Scan FSM Testbench (`panel_scan_fsm_tb.sv`)

**Purpose**: Verify 6-state FSM behavior, timing accuracy, and mode control

**Coverage Requirements**:
- FSM State Coverage: 100% (all 6 states visited)
- FSM Transition Coverage: 100% (all valid transitions)
- Line Coverage: >= 95%
- Branch Coverage: >= 90%

**Test Scenarios**:

| ID | Scenario | Verification Points |
|----|----------|---------------------|
| T1.1 | IDLE state waiting | FSM stays in IDLE until start_scan |
| T1.2 | Start scan latency | start_scan → INTEGRATE within 1 clk_sys cycle |
| T1.3 | INTEGRATE timing | gate_on asserted for exactly gate_on_us |
| T1.4 | READOUT timing | roic_settle_us wait before data valid |
| T1.5 | LINE_DONE handling | Bank toggle, line counter increment |
| T1.6 | FRAME_DONE (single) | Frame counter inc, return to IDLE |
| T1.7 | FRAME_DONE (continuous) | Return to INTEGRATE for next frame |
| T1.8 | Calibration mode | gate OFF during INTEGRATE |
| T1.9 | Stop scan response | Return to IDLE within 1 line time |
| T1.10 | Error state entry | Fatal error → ERROR state within 10 cycles |
| T1.11 | Error recovery | error_clear → IDLE, gate OFF |

**Key Assertions**:
```systemverilog
// FSM state encoding
property fsm_state_valid;
  @(posedge clk) state inside {IDLE, INTEGRATE, READOUT, LINE_DONE, FRAME_DONE, ERROR};
endproperty

// Start scan latency
property start_scan_latency;
  @(posedge clk) start_scan |=> ##[0:1] state == INTEGRATE;
endproperty

// Gate timing accuracy
property gate_on_duration;
  $rose(gate_on) |=> ##[gate_on_us*100-1:gate_on_us*100+1] $fell(gate_on);
endproperty
```

**Stimulus Requirements**:
- Configurable: panel_rows, gate_on_us, roic_settle_us
- Randomized: start_scan timing, stop_scan timing
- Corner cases: Minimum/maximum timing values

---

### 2. Line Buffer Testbench (`line_buffer_tb.sv`)

**Purpose**: Verify ping-pong BRAM operation, CDC integrity, overflow detection

**Coverage Requirements**:
- Line Coverage: >= 95%
- Branch Coverage: >= 90%
- CDC Coverage: Zero metastability violations

**Test Scenarios**:

| ID | Scenario | Verification Points |
|----|----------|---------------------|
| T2.1 | Single bank write/read | Data integrity: written == read |
| T2.2 | Ping-pong toggle | Bank switch on line_done |
| T2.3 | CDC stress test | 10,000 bank switches, zero errors |
| T2.4 | Overflow detection | Write catches read → overflow flag |
| T2.5 | Full line width | 3072 pixels x 16-bit |
| T2.6 | Back-to-back lines | No gaps between lines |
| T2.7 | CDC synchronization | 2-FF sync for bank_sel |
| T2.8 | Clock ratio tolerance | clk_roic ≠ clk_csi2_byte operation |

**Key Assertions**:
```systemverilog
// Data integrity
property data_integrity;
  @(posedge clk_csi2_byte) read_en |-> ##1 read_data == expected_data;
endproperty

// Overflow detection
property overflow_asserted;
  @(posedge clk_roic) (write_addr == read_addr) && (bank_sel == bank_sel_rd) |=> overflow_flag;
endproperty

// CDC synchronization
property bank_sel_cdc;
  @(posedge clk_csi2_byte) bank_sel_rd |=> $past(bank_sel_wr, 2);
endproperty
```

**Stimulus Requirements**:
- Two clock domains: clk_roic (80 MHz), clk_csi2_byte (125 MHz)
- Pattern: Counter, alternating, walking ones/zeros
- Address range: 0 to 3071 (12-bit address)

---

### 3. CSI-2 TX Wrapper Testbench (`csi2_tx_wrapper_tb.sv`)

**Purpose**: Verify MIPI CSI-2 packet structure, CRC-16, backpressure handling

**Coverage Requirements**:
- Line Coverage: >= 95%
- Branch Coverage: >= 90%

**Test Scenarios**:

| ID | Scenario | Verification Points |
|----|----------|---------------------|
| T3.1 | Frame Start packet | Data type 0x00, VC=0 |
| T3.2 | Line Data packet | Data type 0x2E (RAW16), pixel data + CRC |
| T3.3 | Frame End packet | Data type 0x01 |
| T3.4 | CRC-16 calculation | Polynomial x^16 + x^12 + x^5 + 1 |
| T3.5 | Backpressure pause | tready=0 → no data loss |
| T3.6 | Throughput measurement | >= 1.01 Gbps (2048x2048@15fps) |
| T3.7 | Lane speed config | 400/800 Mbps/lane switching |
| T3.8 | Multi-lane scaling | 1/2/4 lane operation |

**Key Assertions**:
```systemverilog
// Packet structure
property frame_start_present;
  @(posedge clk_csi2_byte) frame_start |-> dphy_data_type == 8'h00;
endproperty

// RAW16 format
property raw16_format;
  @(posedge clk_csi2_byte) line_data_valid |-> csi2_data_type == 8'h2E;
endproperty

// CRC correctness
property crc_valid;
  @(posedge clk_csi2_byte) line_end |-> crc_calc == crc_transmitted;
endproperty
```

**Stimulus Requirements**:
- Frame: 2048 rows x 2048 cols x 16-bit
- Pixel patterns: Increment, checkerboard, gradient
- Backpressure injection: Random tready deassertion

---

### 4. SPI Slave Testbench (`spi_slave_tb.sv`)

**Purpose**: Verify SPI protocol, register map access, error handling

**Coverage Requirements**:
- Line Coverage: >= 95%
- Branch Coverage: >= 90%

**Test Scenarios**:

| ID | Scenario | Verification Points |
|----|----------|---------------------|
| T4.1 | Device ID read | 0xD7E0_0001 (0x00=0xD7E0, 0x01=0x0001) |
| T4.2 | Control register write | Bits[4:0] writable, reserved ignored |
| T4.3 | Timing registers | All 6 timing regs writeable |
| T4.4 | Status register read | Read-only, returns runtime values |
| T4.5 | Unmapped address | Returns 0x0000, no side effects |
| T4.6 | CS_N abort mid-transaction | No register modification |
| T4.7 | Read transaction | Returns correct register value |
| T4.8 | Write-1-to-clear | ERROR_FLAGS bits cleared on write-1 |
| T4.9 | Full register map | All registers accessible |
| T4.10 | Transaction timing | <= 32 SCLK cycles |

**Key Assertions**:
```systemverilog
// Device ID
property device_id_hi;
  @(posedge sclk) (addr == 8'h00) && (rw == 0) |=> miso_data == 16'hD7E0;
endproperty

// CS_N abort recovery
property cs_n_abort_no_modify;
  @(negedge cs_n) !transaction_complete |-> stable(register_values);
endproperty

// Read-only protection
property read_only_write_ignored;
  @(posedge sclk) (addr inside {[8'h00:8'h01]}) && (rw == 1) |=> stable(register_values);
endproperty
```

**Stimulus Requirements**:
- SPI Mode 0: CPOL=0, CPHA=0
- Clock: 50 MHz, 25 MHz, 10 MHz (variable)
- Address sweep: 0x00 to 0xFF
- Data patterns: 0x0000, 0xFFFF, 0xAAAA, 0x5555, random

---

### 5. Protection Logic Testbench (`protection_logic_tb.sv`)

**Purpose**: Verify error detection, safe state entry, watchdog timer

**Coverage Requirements**:
- Line Coverage: >= 95%
- Branch Coverage: >= 90%

**Test Scenarios**:

| ID | Scenario | Verification Points |
|----|----------|---------------------|
| T5.1 | Timeout error | ERROR_FLAGS[0] set, FSM→ERROR |
| T5.2 | Overflow error | ERROR_FLAGS[1] set, FSM→ERROR |
| T5.3 | CRC error | ERROR_FLAGS[2] set (non-fatal) |
| T5.4 | Overexposure error | ERROR_FLAGS[3] set (non-fatal) |
| T5.5 | ROIC fault | ERROR_FLAGS[4] set, FSM→ERROR |
| T5.6 | D-PHY error | ERROR_FLAGS[5] set, FSM→ERROR |
| T5.7 | Config error | ERROR_FLAGS[6] set (non-fatal) |
| T5.8 | Watchdog timeout | ERROR_FLAGS[7] set, FSM→ERROR |
| T5.9 | Safe state entry | gate=0, CSI-2 disabled, buffer write disabled |
| T5.10 | Error clearing | error_clear → IDLE, flags cleared |
| T5.11 | Multiple errors | All flags set correctly |
| T5.12 | Watchdog reset | SPI transaction resets watchdog |

**Key Assertions**:
```systemverilog
// Error detection latency
property error_detection_latency;
  @(posedge clk) error_trigger |-> ##[0:10] error_flag;
endproperty

// Safe state gate output
property safe_state_gate_off;
  @(posedge clk) (state == ERROR) |-> (gate_on == 0);
endproperty

// Watchdog reset
property watchdog_reset_on_spi;
  @(posedge clk) spi_transaction_active |=> watchdog_timer == 0;
endproperty
```

**Stimulus Requirements**:
- Error injection: All 8 error types individually
- Concurrent errors: 2+ simultaneous errors
- Watchdog timing: Configurable timeout (default 100 ms)

---

## Simulation Scripts

### Compile Script (`fpga/sim/compile.sh`)

```bash
#!/bin/bash
# FPGA RTL and Testbench Compilation Script

# Paths
RTL_DIR="../rtl"
TB_DIR="../tb"
COMMON_DIR="$TB_DIR/common"

# Source files
RTL_SV=(
  "panel_scan_fsm.sv"
  "line_buffer.sv"
  "csi2_tx_wrapper.sv"
  "spi_slave.sv"
  "protection_logic.sv"
)

TB_SV=(
  "panel_scan_fsm_tb.sv"
  "line_buffer_tb.sv"
  "csi2_tx_wrapper_tb.sv"
  "spi_slave_tb.sv"
  "protection_logic_tb.sv"
)

# Compile RTL
for file in "${RTL_SV[@]}"; do
  xvlog -sv "$RTL_DIR/$file" -work rtl_work
done

# Compile testbenches
for file in "${TB_SV[@]}"; do
  xvlog -sv "$TB_DIR/$file" -work tb_work
done
```

### Run Script (`fpga/sim/run.sh`)

```bash
#!/bin/bash
# Run simulation with coverage

TESTBENCH=$1  # e.g., panel_scan_fsm_tb

xelab -debug typical ${TESTBENCH} -s ${TESTBENCH}_sim \
  -L rtl_work -L tb_work

xsim ${TESTBENCH}_sim -runall \
  -testplusarg "UCMD" \
  -covaxueshdr \
  -covoverrideoverwrite \
  -log sim_${TESTBENCH}.log
```

---

## Coverage Verification

### Coverage Collection

Enable coverage in simulation:
```tcl
# Vivado xsim
covarrayrefsfileregister
covconfigure -testbench ${TESTBENCH} -code {all}
```

### Coverage Merge and Report

```bash
# Merge coverage from all testbenches
covmerge -outfile merged.cov *.cax

# Generate HTML report
covreport -html -htmlfile report.html merged.cov
```

### Coverage Acceptance Criteria

| Module | Line | Branch | FSM | Notes |
|--------|------|--------|-----|-------|
| panel_scan_fsm | >= 95% | >= 90% | 100% | All 6 states, all transitions |
| line_buffer | >= 95% | >= 90% | N/A | CDC paths included |
| csi2_tx_wrapper | >= 95% | >= 90% | N/A | IP wrapper only |
| spi_slave | >= 95% | >= 90% | N/A | All register groups |
| protection_logic | >= 95% | >= 90% | N/A | All 8 error types |

---

## Testbench Development Sequence

Following TDD methodology, testbenches are developed before RTL implementation:

1. **TDD RED Phase**: Write testbench with failing tests (no RTL or incomplete RTL)
2. **TDD GREEN Phase**: Implement RTL to pass tests
3. **TDD REFACTOR Phase**: Optimize RTL while maintaining test coverage

### Sequence for SPEC-FPGA-001

| Week | Testbench | RTL Implementation |
|------|-----------|-------------------|
| W9 | panel_scan_fsm_tb.sv | panel_scan_fsm.sv |
| W10 | line_buffer_tb.sv | line_buffer.sv |
| W11 | spi_slave_tb.sv | spi_slave.sv |
| W12 | protection_logic_tb.sv | protection_logic.sv |
| W13 | csi2_tx_wrapper_tb.sv | csi2_tx_wrapper.sv |
| W14 | Integration testbench, coverage merge, bug fixes |

---

## References

- SPEC-FPGA-001: `.moai/specs/SPEC-FPGA-001/spec.md`
- Acceptance Criteria: `.moai/specs/SPEC-FPGA-001/acceptance.md`
- Architecture: `docs/architecture/fpga-design.md`
- Common Utilities: `fpga/tb/common/utils.sv`
