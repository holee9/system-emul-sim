# SPEC-FPGA-001: Acceptance Criteria and Test Scenarios

## Overview

This document defines the acceptance criteria, test scenarios, and quality gates for SPEC-FPGA-001 (FPGA RTL Requirements Specification). All scenarios use Given/When/Then format for clarity and traceability. Requirements trace to `docs/architecture/fpga-design.md` v1.0.0 and parent SPEC-ARCH-001.

---

## Test Scenarios

### Scenario 1: Resource Utilization Compliance

**Objective**: Verify FPGA design meets resource budget constraints on Artix-7 XC7A35T-FGG484.

```gherkin
Given the complete FPGA design is synthesized and implemented in Vivado
When the utilization report is generated post place-and-route
Then the following resource constraints shall be met:
  | Resource | Available | Budget (60%) | Threshold |
  | LUTs | 20,800 | 12,480 | < 60% utilization |
  | Block RAM (36Kb) | 50 | 30 | < 60% utilization |
  | DSP Slices | 90 | N/A | No hard constraint |
  | I/O Pins | ~250 | ~60 used | Sufficient margin |
And WNS (Worst Negative Slack) >= 1 ns for all clock domains:
  | Clock Domain | Frequency | WNS Target |
  | clk_sys | 100 MHz | >= 1 ns |
  | clk_pixel | 125.83 MHz | >= 1 ns |
  | clk_csi2_byte | 125 MHz | >= 1 ns |
  | clk_dphy_hs | 500 MHz | >= 1 ns |
And all RTL source files use .sv extension (SystemVerilog)
```

**Success Criteria**:
- Vivado utilization report shows LUT < 12,480 and BRAM < 30
- Timing summary shows WNS >= 1 ns for all constrained clocks
- No combinational loops reported in synthesis warnings

**Verification Method**: Vivado synthesis and implementation reports

**Traces To**: REQ-FPGA-001, REQ-FPGA-002, REQ-FPGA-003, REQ-FPGA-004

---

### Scenario 2: Panel Scan FSM State Machine Verification

**Objective**: Verify 6-state FSM covers complete scan lifecycle with all transitions.

```gherkin
Given the Panel Scan FSM testbench with all stimulus patterns
When the FSM simulation completes with coverage enabled
Then 100% state coverage shall be achieved:
  | State | Encoding | Verified Behavior |
  | IDLE | 3'b000 | Waits for start_scan, outputs inactive |
  | INTEGRATE | 3'b001 | gate_on asserted for gate_on_us microseconds |
  | READOUT | 3'b010 | Waits roic_settle_us, then asserts data valid |
  | LINE_DONE | 3'b011 | Toggles line buffer bank, increments line counter |
  | FRAME_DONE | 3'b100 | Increments 32-bit frame counter |
  | ERROR | 3'b101 | Safe state, gate OFF, waits for error_clear |
And 100% transition coverage shall be achieved:
  | From | To | Trigger |
  | IDLE | INTEGRATE | start_scan = 1 (within 1 clk_sys cycle) |
  | INTEGRATE | READOUT | gate timer expires |
  | READOUT | LINE_DONE | ADC conversion + readout complete |
  | LINE_DONE | INTEGRATE | line_counter < panel_rows |
  | LINE_DONE | FRAME_DONE | line_counter == panel_rows |
  | FRAME_DONE | IDLE | single scan mode |
  | FRAME_DONE | INTEGRATE | continuous scan mode |
  | ANY_ACTIVE | ERROR | fatal error detected |
  | ERROR | IDLE | error_clear via SPI |
And three operating modes shall be verified:
  | Mode | CONTROL[3:2] | Behavior |
  | Single Scan | 2'b00 | FRAME_DONE -> IDLE |
  | Continuous | 2'b01 | FRAME_DONE -> INTEGRATE |
  | Calibration | 2'b10 | gate OFF during INTEGRATE (dark frame) |
```

**Success Criteria**:
- FSM state coverage: 100% (6/6 states visited)
- FSM transition coverage: 100% (all valid transitions exercised)
- Start-scan latency: <= 1 clk_sys cycle from SPI write
- Gate timing accuracy: +/- 1 clk_sys cycle (10 ns at 100 MHz)
- Stop-scan completes within 1 line time

**Verification Method**: SystemVerilog testbench simulation, coverage analysis (Vivado xsim or Questa)

**Traces To**: REQ-FPGA-010 through REQ-FPGA-016

---

### Scenario 3: Line Buffer Data Integrity and CDC

**Objective**: Verify Ping-Pong line buffer operates correctly across clock domains with zero data corruption.

```gherkin
Given the line buffer configured for 3072 pixels x 16-bit depth
And clk_roic (write side) at 80 MHz and clk_csi2_byte (read side) at 125 MHz
When a counter pattern (0x0000 to 0x0BFF) is written to Bank A
And bank toggle occurs on line_done signal
And the same data is read back from Bank A via the read port
Then all 3072 pixel values shall match (zero bit errors)
And read data shall be available within 1 clock cycle of read enable
And bank select signal shall cross domains via 2-stage FF synchronizer
```

```gherkin
Given the line buffer operating in continuous mode
When 10,000 consecutive bank switches occur during simulation
Then zero data corruption shall be detected across all switches
And overflow flag shall propagate safely across clock domain boundary
And no metastability violations shall appear in Vivado CDC report
```

**Success Criteria**:
- 100% data integrity across 10,000 bank switches
- BRAM address width = 12 bits (ceil(log2(3072)))
- Overflow flag asserts when write catches read in same bank
- Vivado CDC report: zero violations

**Verification Method**: SystemVerilog testbench with data comparison, Vivado CDC analysis

**Traces To**: REQ-FPGA-020 through REQ-FPGA-024

---

### Scenario 4: CSI-2 TX Packet Correctness and Throughput

**Objective**: Verify CSI-2 TX generates valid MIPI packet stream with correct data format.

```gherkin
Given one frame of known pixel data (e.g., incrementing 16-bit values)
When the frame is transmitted through CSI-2 TX on Virtual Channel 0
Then the output packet stream shall contain:
  | Packet | Data Type | Content |
  | Frame Start | 0x00 | Sync pattern, VC=0 |
  | Line Data x N | 0x2E (RAW16) | Pixel payload + CRC-16 |
  | Frame End | 0x01 | Sync pattern, VC=0 |
And total packet count = 2 + N_rows (FS + N lines + FE)
And CRC-16 for each line packet shall match MIPI CSI-2 specification polynomial
And pixel values in Line Data packets shall match source data (bit-exact)
```

```gherkin
Given CSI-2 TX configured for 4-lane D-PHY at 400 Mbps/lane
When 100 consecutive frames of 2048x2048 16-bit data are transmitted
Then measured output throughput >= 1.01 Gbps
And zero frame drops shall occur
And frame interval shall be within 5% of target (66.7 ms for 15 fps)
And AXI4-Stream backpressure (tready deasserted) shall pause TX without data loss
```

**Success Criteria**:
- Packet structure: FS + N line packets + FE per frame
- RAW16 data type (0x2E) and VC0 in all packets
- CRC-16 verification passes for all line packets
- Throughput >= 1.01 Gbps at Intermediate-A tier
- Zero data loss during backpressure events
- Lane speed configurable from 400 to 1250 Mbps/lane via register 0x88

**Verification Method**: CSI-2 packet capture and parsing in testbench, throughput measurement

**Traces To**: REQ-FPGA-030 through REQ-FPGA-036

---

### Scenario 5: SPI Slave Register Access

**Objective**: Verify SPI register map is complete and SPI protocol is correct.

```gherkin
Given SPI Mode 0 (CPOL=0, CPHA=0) at 50 MHz clock
And transaction format: 8-bit address + 8-bit R/W flag + 16-bit data (32 bits total)
When write-read sequence completes for each register group
Then the following register groups shall respond correctly:
  | Group | Address Range | Type | Test |
  | Control | 0x00-0x0F | R/W | Write, read back, verify match |
  | Timing | 0x20-0x3F | R/W | Write timing values, verify readback |
  | Panel Config | 0x40-0x5F | R/W | Write config, verify readback |
  | CSI-2 Config | 0x80-0x8F | R/W | Write lane speed, verify readback |
  | Data Status | 0x90-0x9F | RO | Read, verify runtime values |
  | Error Flags | 0xA0-0xAF | R/W1C | Read flags, write-1-to-clear |
  | Identification | 0x00-0x01 | RO | DEVICE_ID = 0xD7E0_0001 (0x00: 0xD7E0, 0x01: 0x0001) |
And unmapped addresses shall return 0x0000 on read and ignore writes
And all transactions shall complete within 32 SCLK cycles
```

```gherkin
Given SPI transaction in progress (CS_N asserted)
When CS_N is deasserted mid-transaction (after 16 of 32 bits)
Then no register shall be modified
And the SPI state machine shall return to idle
And the next transaction shall proceed normally
```

**Success Criteria**:
- All defined registers accessible per specification
- DEVICE_ID returns 0xD7E0_0001: address 0x00 returns 0xD7E0 (upper 16 bits), address 0x01 returns 0x0001 (lower 16 bits)
- Read-only registers ignore write attempts
- CS_N abort does not corrupt register state
- 32-bit transaction completes within 32 SCLK cycles

**Verification Method**: SPI protocol testbench, register map verification

**Traces To**: REQ-FPGA-040 through REQ-FPGA-044

---

### Scenario 6: Protection Logic and Safe State

**Objective**: Verify all 8 error conditions are detected and safe state is entered correctly.

```gherkin
Given error injection testbench capable of triggering all 8 error conditions
When each error condition is triggered individually
Then the corresponding ERROR_FLAGS bit shall be set within 10 clock cycles:
  | Error | Code | Bit | Fatal | Trigger Method |
  | Timeout | 0x01 | [0] | Yes | Starve FSM timer |
  | Overflow | 0x02 | [1] | Yes | Write faster than read in buffer |
  | CRC | 0x04 | [2] | No | Inject CRC mismatch |
  | Overexposure | 0x08 | [3] | No | Exceed max gate_on_us |
  | ROIC Fault | 0x10 | [4] | Yes | Assert ROIC error input |
  | D-PHY Error | 0x20 | [5] | Yes | Force D-PHY LP error |
  | Config Error | 0x40 | [6] | No | Write invalid config value |
  | Watchdog | 0x80 | [7] | Yes | Stop SPI for > 100 ms |
And for fatal errors (timeout, overflow, ROIC fault, D-PHY error, watchdog):
  | Safe State Action | Verification |
  | FSM -> ERROR state | FSM state register reads ERROR encoding |
  | Gate outputs LOW | gate_on = 0 (no X-ray exposure) |
  | CSI-2 TX disabled | D-PHY enters LP mode |
  | Line buffer write disabled | Write enable deasserted |
  | SPI remains active | Register reads still functional |
And safe state transition completes within 10 clock cycles of error detection
```

```gherkin
Given the system is in safe state (ERROR_FLAGS non-zero, FSM in ERROR)
When error_clear bit is written via SPI CONTROL register
Then all ERROR_FLAGS shall be cleared to 0x00
And FSM shall transition from ERROR to IDLE
And gate outputs shall remain LOW until next scan command
```

**Success Criteria**:
- All 8 error conditions detected and flagged
- Fatal errors trigger FSM ERROR state within 10 clocks
- Gate outputs held LOW in safe state (patient safety)
- Error clearing restores IDLE state
- Watchdog default timeout: 100 ms (configurable)

**Verification Method**: Error injection testbench, safe state verification, watchdog timer test

**Traces To**: REQ-FPGA-050 through REQ-FPGA-054

---

### Scenario 7: Clock Domain and Reset Verification

**Objective**: Verify MMCM clock generation, CDC integrity, and reset behavior.

```gherkin
Given the FPGA MMCM configured from 100 MHz input
When the MMCM lock indicator is asserted
Then the following clock domains shall be active:
  | Clock | Frequency | Tolerance | Purpose |
  | clk_sys | 100 MHz | +/- 50 ppm | System logic, SPI, FSM |
  | clk_pixel | 125.83 MHz | +/- 50 ppm | Pixel processing |
  | clk_csi2_byte | 125 MHz | +/- 50 ppm | CSI-2 byte clock |
  | clk_dphy_hs | 500 MHz | +/- 50 ppm | D-PHY serialization |
And all CDC crossings shall use appropriate synchronization:
  | Signal Type | Synchronization Method |
  | Single-bit control | 2-stage FF synchronizer |
  | Multi-bit counter | Gray code encoding |
  | Data bus | BRAM dual-port |
```

```gherkin
Given the system is in normal operation
When a software reset is issued via SPI CONTROL register bit[2]
Then all clock domains shall receive synchronized reset
And FSM shall return to IDLE state
And all outputs shall be deasserted (gate OFF, CSI-2 LP mode)
And SPI register map shall be re-initialized to default values
And the reset deassertion shall be synchronous to each clock domain
```

**Success Criteria**:
- MMCM generates all 4 clocks with correct frequencies
- Vivado CDC report shows zero violations
- Power-on reset: asynchronous assert, synchronous deassert
- Software reset: full re-initialization within 100 clock cycles
- Reset synchronizer in each clock domain

**Verification Method**: Clock frequency measurement in simulation, CDC analysis, reset sequence verification

**Traces To**: REQ-FPGA-060 through REQ-FPGA-062

---

### Scenario 8: Unwanted Behavior Rejection

**Objective**: Verify prohibited design patterns are not present in the RTL.

```gherkin
Given the complete FPGA design RTL source
When design rule checks are performed
Then the following prohibited patterns shall be absent:
  | Prohibited Pattern | Check Method | Expected Result |
  | USB 3.x IP | Source code search | Zero USB references |
  | Combinational loops | Vivado synthesis warnings | Zero loop warnings |
  | Async resets on data path | RTL audit | Only sync resets (except safety outputs) |
And the design shall use only approved primitives:
  | Category | Allowed | Prohibited |
  | Flip-flops | always_ff @(posedge clk) | always @(*) for registers |
  | Reset | Synchronous (data path) | Asynchronous (except gate_on) |
  | Serializer | OSERDES2 (LVDS_25) | External PHY |
```

**Success Criteria**:
- Zero USB 3.x references in source
- Zero combinational loop warnings in synthesis
- Asynchronous reset only on gate control outputs (safety)
- All flip-flops use `always_ff` with positive edge clock

**Verification Method**: RTL source audit, synthesis warning analysis

**Traces To**: REQ-FPGA-070 through REQ-FPGA-072

---

### Scenario 9: Golden Reference Match (Simulator vs RTL)

**Objective**: Verify FPGA RTL matches FpgaSimulator (C#) output bit-for-bit.

```gherkin
Given the FpgaSimulator (C#, SPEC-SIM-001) and FPGA RTL simulation
And both configured with identical input parameters:
  | Parameter | Value |
  | Resolution | 2048 x 2048 |
  | Bit Depth | 16-bit |
  | Frame Rate | 15 fps |
  | Lane Speed | 400 Mbps/lane |
When 10 frames of identical pixel data are processed by both
Then CSI-2 packet output shall match bit-for-bit:
  | Comparison | Tolerance |
  | Pixel data values | Exact match (0 bit errors) |
  | CRC-16 values | Exact match |
  | Packet structure | Exact match (FS/Line/FE sequence) |
And register values shall match for all SPI read operations
And FSM state sequences shall match cycle-by-cycle
```

**Success Criteria**:
- Bit-exact match between simulator and RTL for 10 frames
- Register map values identical for all read addresses
- FSM state transitions occur at same relative time
- CRC-16 values match for all line packets

**Verification Method**: Co-simulation with data comparison scripts

**Traces To**: AC-FPGA-009 in spec.md

---

### Scenario 10: Code Coverage Compliance

**Objective**: Verify RTL verification meets coverage targets.

```gherkin
Given all RTL testbenches have been executed
When coverage reports are generated by Vivado xsim or Questa
Then the following coverage targets shall be met:
  | Coverage Type | Target | Module Scope |
  | Line Coverage | >= 95% | All RTL modules |
  | Branch Coverage | >= 90% | All RTL modules |
  | FSM State Coverage | 100% | panel_scan_fsm.sv |
  | FSM Transition Coverage | 100% | panel_scan_fsm.sv |
  | Toggle Coverage | >= 80% | I/O ports only |
And per-module coverage shall be reported:
  | Module | Line | Branch | Notes |
  | panel_scan_fsm.sv | >= 95% | >= 90% | + FSM 100% |
  | line_buffer.sv | >= 95% | >= 90% | CDC paths included |
  | csi2_tx_wrapper.sv | >= 95% | >= 90% | IP wrapper only |
  | spi_slave.sv | >= 95% | >= 90% | All register groups |
  | protection_logic.sv | >= 95% | >= 90% | All 8 error types |
```

**Success Criteria**:
- Overall: Line >= 95%, Branch >= 90%
- FSM: 100% state and transition coverage
- Coverage report generated and reviewed at M2 gate
- No unreachable code in RTL (dead code eliminated)

**Verification Method**: Coverage analysis tools (Vivado xsim or Questa)

**Traces To**: REQ-FPGA-005

---

## Edge Case Testing

### Edge Case 1: Maximum Resolution Operation (3072x3072)

**Scenario**:
```gherkin
Given FPGA configured for maximum tier (3072x3072, 16-bit, 15 fps)
And CSI-2 D-PHY at 800 Mbps/lane (4-lane, 3.2 Gbps aggregate)
When continuous scan mode runs for 100 frames
Then data throughput >= 2.26 Gbps shall be sustained
And zero overflow events in line buffer
And frame interval within 5% of 66.7 ms target
And if 800 Mbps/lane is unstable:
  | Fallback | Action | Impact |
  | Reduce to 400 Mbps/lane | Limit to Intermediate-A tier | 2048x2048@15fps only |
  | Debug D-PHY timing | OSERDES/MMCM tuning | May require board rework |
```

**Expected Outcome**:
- Maximum tier operates if 800 Mbps D-PHY debugging succeeds (29% bandwidth margin)
- Fallback to 400 Mbps (Intermediate-A) if debugging incomplete

**Verification Method**: Extended simulation, throughput measurement, D-PHY timing analysis

---

### Edge Case 2: Line Buffer Overflow Recovery

**Scenario**:
```gherkin
Given line buffer operating with fast ROIC write and slow CSI-2 read
When write address catches read address in the same bank
Then overflow flag (ERROR_FLAGS bit[1]) shall assert
And line buffer write shall halt (no data corruption)
And protection logic shall trigger FSM ERROR state
And after error_clear, the system shall resume from IDLE with empty buffers
```

**Expected Outcome**:
- Overflow detected before data corruption occurs
- Clean recovery path through error clear mechanism
- No stale data in buffers after recovery

**Verification Method**: Deliberate overflow injection, recovery sequence verification

---

### Edge Case 3: SPI Watchdog Timeout

**Scenario**:
```gherkin
Given watchdog timer configured to 100 ms default timeout
When no SPI transaction occurs for > 100 ms
Then watchdog error (ERROR_FLAGS bit[7]) shall assert
And FSM shall transition to ERROR state (safe state)
And gate outputs shall be held LOW
And a single valid SPI transaction shall reset the watchdog timer
And error_clear shall be required to exit ERROR state
```

**Expected Outcome**:
- SoC hang or communication loss detected within 100 ms
- Safe state entered automatically
- Watchdog timeout configurable via protection register

**Verification Method**: Watchdog timer testbench with configurable delays

---

### Edge Case 4: Simultaneous Multiple Errors

**Scenario**:
```gherkin
Given the system is operating normally
When two or more error conditions occur simultaneously (e.g., overflow + ROIC fault)
Then all triggered error bits shall be set in ERROR_FLAGS
And FSM shall transition to ERROR state once (not multiple times)
And safe state shall be entered within 10 clock cycles
And error_clear shall clear all error bits simultaneously
```

**Expected Outcome**:
- Multiple concurrent errors correctly captured
- Single FSM transition to ERROR state
- All error flags independently readable

**Verification Method**: Multi-error injection testbench

---

### Edge Case 5: D-PHY Lane Speed Reconfiguration

**Scenario**:
```gherkin
Given CSI-2 TX operating at 400 Mbps/lane
When CSI2_LANE_SPEED register (0x88) is updated to 800 Mbps/lane
And MMCM dynamic reconfiguration is triggered
Then D-PHY shall complete reconfiguration within 10 ms
And no data corruption shall occur during reconfiguration window
And the new lane speed shall be verified via throughput measurement
```

**Expected Outcome**:
- Lane speed change without full bitstream reload (optional feature)
- Data path quiesced during reconfiguration
- New speed validated before resuming data flow

**Verification Method**: MMCM DRP reconfiguration testbench

---

## Performance Criteria

### Throughput Validation

**Criterion**: System sustains required throughput at each performance tier.

**Metrics**:

| Tier | Resolution | FPS | Required Throughput | D-PHY Speed | Margin |
|------|-----------|-----|--------------------|----|--------|
| Minimum | 1024x1024 | 15 | 0.21 Gbps | 400 Mbps/lane | 87% |
| Intermediate-A | 2048x2048 | 15 | 1.01 Gbps | 400 Mbps/lane | 37% |
| Intermediate-B | 2048x2048 | 30 | 2.01 Gbps | 800 Mbps/lane | 37% |
| Target (final) | 3072x3072 | 15 | 2.26 Gbps | 800 Mbps/lane | 29% |

**Acceptance Threshold**: Measured throughput >= required throughput with zero frame drops over 100 frames

**Verification Method**: Simulation throughput measurement, cycle-accurate timing analysis

---

### Latency Validation

**Criterion**: Key operations meet latency targets.

**Metrics**:

| Operation | Target Latency | Measurement Point |
|-----------|---------------|-------------------|
| Start scan (SPI write to gate_on) | <= 1 clk_sys + SPI latency | CONTROL[0] write to gate_on assert |
| Stop scan | <= 1 line time | CONTROL[1] write to IDLE |
| Error detection to safe state | <= 10 clk_sys cycles | Error event to gate_off |
| Bank toggle | 1 clk_sys cycle | line_done to bank_sel toggle |
| SPI transaction | <= 32 SCLK cycles | CS_N assert to CS_N deassert |

**Acceptance Threshold**: All operations meet specified latency targets

**Verification Method**: Simulation timing measurement

---

### Resource Utilization

**Criterion**: FPGA resource usage within budget for all modules.

**Metrics (estimates)**:

| Module | LUT Estimate | BRAM Estimate | Notes |
|--------|-------------|---------------|-------|
| Panel Scan FSM | 200-400 | 0 | Simple state machine + timers |
| Line Buffer | 100-200 | 4 | 2 banks x 2 cascaded BRAMs |
| CSI-2 TX Wrapper | 3,000-5,000 | 2-4 | AMD IP core (main consumer) |
| SPI Slave | 300-500 | 0 | Register file, protocol engine |
| Protection Logic | 200-400 | 0 | Error detection, watchdog |
| Clock/Reset | 100-200 | 0 | MMCM, reset synchronizers |
| ILA Debug (opt.) | 500-1,000 | 1-2 | Optional debug probes |
| **Total** | **4,400-7,700** | **7-10** | **21-37% LUT, 14-20% BRAM** |

**Acceptance Threshold**: Total LUT < 12,480 (60%), Total BRAM < 30 (60%)

**Verification Method**: Vivado synthesis and implementation reports

---

## Quality Gates

### TRUST 5 Framework Compliance

**Tested (T)**:
- All 5 RTL modules have dedicated testbenches
- Line coverage >= 95%, Branch coverage >= 90%, FSM 100%
- Characterization tests for existing RTL patterns (DDD methodology)
- Golden reference match with FpgaSimulator (SPEC-SIM-001)
- 100 frames sustained throughput test at Intermediate-A tier

**Readable (R)**:
- SystemVerilog coding style: `always_ff`, `always_comb`, `logic` types
- English code comments per language.yaml (code_comments: en)
- Module interfaces documented with port descriptions
- FSM state names match specification (IDLE, INTEGRATE, READOUT, etc.)

**Unified (U)**:
- Consistent naming: `snake_case` for all RTL files and signals
- File naming: `panel_scan_fsm.sv`, `line_buffer.sv`, etc.
- Clock naming: `clk_sys`, `clk_pixel`, `clk_csi2_byte`, `clk_dphy_hs`
- Reset naming: `rst_n` (active low), `sw_reset` (software reset)

**Secured (S)**:
- Gate outputs default to OFF (fail-safe)
- Protection logic covers 8 error conditions
- Safe state entered within 10 clocks of fatal error
- Watchdog timer prevents uncontrolled operation
- No patient safety risk from any error condition

**Trackable (T)**:
- All RTL files in `fpga/` Git repository
- Conventional commits: `feat(fpga):`, `fix(fpga):`, `test(fpga):`
- Requirements traced: REQ-FPGA-001 through REQ-FPGA-081
- Coverage reports archived at M2 gate review

---

### Technical Review Approval

**Review Criteria**:
- All 33 requirements implemented with testbench coverage
- Resource utilization confirmed within 60% LUT budget
- Timing closure achieved (WNS >= 1 ns for all clocks)
- CDC report shows zero violations
- Protection logic response verified for all 8 error types

**Reviewers**:
- FPGA Engineer: RTL design review, resource analysis
- System Architect: Interface compliance (CSI-2, SPI)
- Safety Engineer: Protection logic and safe state verification
- Test Engineer: Coverage report review, test adequacy

**Approval Criteria**:
- Zero unresolved critical findings
- All acceptance criteria met
- Coverage targets achieved (Line >= 95%, Branch >= 90%, FSM 100%)
- Golden reference match with FpgaSimulator confirmed

---

### Milestone Gate (M2-M3, W14)

**M2 Gate Checklist**:
- [ ] All 5 RTL modules synthesize without errors
- [ ] Resource utilization report: LUT < 60%, BRAM < 60%
- [ ] Timing closure: WNS >= 1 ns for all clocks
- [ ] CDC analysis: zero violations
- [ ] FSM coverage: 100% state and transition
- [ ] Line coverage >= 95%, Branch coverage >= 90%
- [ ] SPI register map fully verified
- [ ] Protection logic verified for all 8 error conditions
- [ ] Golden reference match with FpgaSimulator (10+ frames)

**M3 Gate Checklist** (post HIL):
- [ ] FPGA bitstream loaded on Artix-7 XC7A35T evaluation board
- [ ] CSI-2 TX verified with SoC receiver (i.MX8MP)
- [ ] SPI register access verified from SoC
- [ ] Intermediate-A throughput achieved (1.01 Gbps)
- [ ] Protection logic verified with hardware error injection

---

## Traceability Matrix

| Requirement ID | Acceptance Scenario | Test Method | Quality Gate |
|---------------|-------------------|-------------|--------------|
| REQ-FPGA-001 | Scenario 1 | Vivado synthesis report | Technical Review |
| REQ-FPGA-002 | Scenario 1 | Vivado timing report | Technical Review |
| REQ-FPGA-003 | Scenario 8 | Source code audit | Technical Review |
| REQ-FPGA-004 | Scenario 8 | RTL design audit | Technical Review |
| REQ-FPGA-005 | Scenario 10 | Coverage analysis | M2 Gate |
| REQ-FPGA-010 | Scenario 2 | FSM testbench | Technical Review |
| REQ-FPGA-011 | Scenario 2 | SPI+FSM testbench | Technical Review |
| REQ-FPGA-012 | Scenario 2 | Gate timing testbench | Technical Review |
| REQ-FPGA-013 | Scenario 2 | Readout sequence testbench | Technical Review |
| REQ-FPGA-014 | Scenario 2 | Mode switching testbench | Technical Review |
| REQ-FPGA-015 | Scenario 2 | Frame completion testbench | Technical Review |
| REQ-FPGA-016 | Scenario 2 | Stop-scan testbench | Technical Review |
| REQ-FPGA-020 | Scenario 3 | Line buffer testbench | Technical Review |
| REQ-FPGA-021 | Scenario 3 | Max resolution testbench | Technical Review |
| REQ-FPGA-022 | Scenario 3 | CDC verification | Technical Review |
| REQ-FPGA-023 | Scenario 3, EC 2 | Overflow injection | Technical Review |
| REQ-FPGA-024 | Scenario 3 | Bank toggle testbench | Technical Review |
| REQ-FPGA-030 | Scenario 4 | CSI-2 packet testbench | Technical Review |
| REQ-FPGA-031 | Scenario 4 | RAW16 format verification | Technical Review |
| REQ-FPGA-032 | Scenario 4 | Packet framing testbench | Technical Review |
| REQ-FPGA-033 | Scenario 4 | Backpressure testbench | Technical Review |
| REQ-FPGA-034 | Scenario 4, EC 5 | Lane speed config test | Technical Review |
| REQ-FPGA-035 | Scenario 4 | OSERDES verification | M3 Gate (HIL) |
| REQ-FPGA-036 | Scenario 4, 9 | CRC-16 comparison | Technical Review |
| REQ-FPGA-040 | Scenario 5 | SPI protocol testbench | Technical Review |
| REQ-FPGA-041 | Scenario 5 | Transaction format test | Technical Review |
| REQ-FPGA-042 | Scenario 5 | Register map testbench | Technical Review |
| REQ-FPGA-043 | Scenario 5 | CS_N abort testbench | Technical Review |
| REQ-FPGA-044 | Scenario 5 | Unmapped address test | Technical Review |
| REQ-FPGA-050 | Scenario 6 | Error injection testbench | Technical Review |
| REQ-FPGA-051 | Scenario 6 | Fatal error response test | Technical Review |
| REQ-FPGA-052 | Scenario 6 | Safe state verification | Safety Review |
| REQ-FPGA-053 | Scenario 6 | Error clear testbench | Technical Review |
| REQ-FPGA-054 | Scenario 6, EC 3 | Watchdog timer testbench | Technical Review |
| REQ-FPGA-060 | Scenario 7 | Clock measurement | Technical Review |
| REQ-FPGA-061 | Scenario 3, 7 | CDC report analysis | Technical Review |
| REQ-FPGA-062 | Scenario 7 | Reset sequence testbench | Technical Review |
| REQ-FPGA-070 | Scenario 8 | Source code audit | Technical Review |
| REQ-FPGA-071 | Scenario 8 | Synthesis warning check | Technical Review |
| REQ-FPGA-072 | Scenario 8 | RTL audit | Technical Review |
| REQ-FPGA-080 | N/A (optional) | ILA deployment test | Optional |
| REQ-FPGA-081 | EC 5 (optional) | DRP reconfiguration test | Optional |

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | ABYZ-Lab Agent (spec-fpga) | Initial acceptance criteria for SPEC-FPGA-001 |

---

**END OF ACCEPTANCE CRITERIA**
