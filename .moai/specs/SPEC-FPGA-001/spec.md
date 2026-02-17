# SPEC-FPGA-001: FPGA RTL Requirements Specification

---
id: SPEC-FPGA-001
version: 1.0.0
status: draft
created: 2026-02-17
updated: 2026-02-17
author: MoAI Agent (analyst)
priority: critical
milestone: M2-M3
gate_week: W14
---

## HISTORY

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (analyst) | Initial SPEC creation for FPGA RTL |

---

## Overview

### Project Context

The FPGA (Xilinx Artix-7 XC7A35T-FGG484) serves as the real-time data acquisition engine in the X-ray Detector Panel System. It controls panel scan timing, buffers pixel data, and streams frames to the SoC via CSI-2 MIPI D-PHY.

### Scope

This SPEC defines requirements for five RTL modules and their integration:

| Module | File | Function |
|--------|------|----------|
| Panel Scan FSM | `panel_scan_fsm.sv` | Scan timing, state machine, mode control |
| Line Buffer | `line_buffer.sv` | Ping-Pong BRAM, CDC |
| CSI-2 TX | `csi2_tx_wrapper.sv` | AMD CSI-2 TX IP wrapper, packet building |
| SPI Slave | `spi_slave.sv` | Register map, protocol engine |
| Protection Logic | `protection_logic.sv` | Error detection, safe shutdown |

### Development Methodology

FPGA RTL follows **DDD (ANALYZE-PRESERVE-IMPROVE)** per quality.yaml hybrid settings (existing code pattern). Characterization tests are written before RTL modifications.

### Reference Architecture

All requirements trace to `docs/architecture/fpga-design.md` v1.0.0. Register map, FSM states, clock domains, and resource estimates are defined there.

---

## Requirements

### 1. Ubiquitous Requirements (All RTL Modules)

**REQ-FPGA-001**: The FPGA design **shall** target Xilinx Artix-7 XC7A35T-FGG484 with LUT utilization below 60% (12,480 LUTs).

**WHY**: Resource budget preservation enables future features, debug logic, and timing closure margin.

**IMPACT**: All RTL modules undergo resource utilization analysis. If budget exceeded, module optimization or FPGA upgrade required.

---

**REQ-FPGA-002**: The FPGA design **shall** achieve timing closure with WNS (Worst Negative Slack) >= 1 ns for all clock domains.

**WHY**: Positive timing slack ensures reliable operation across temperature and voltage variations.

**IMPACT**: All paths must meet setup and hold timing. Critical paths identified and optimized during implementation.

---

**REQ-FPGA-003**: The FPGA design **shall** use SystemVerilog as the RTL description language.

**WHY**: SystemVerilog provides modern constructs (always_ff, always_comb, logic type) for safer RTL design and improved verification.

**IMPACT**: All source files use `.sv` extension. Synthesis tool: AMD Vivado (supports SystemVerilog-2012).

---

**REQ-FPGA-004**: All RTL modules **shall** follow synchronous design principles with explicit clock and reset signals.

**WHY**: Synchronous design ensures predictable timing behavior and simplifies static timing analysis.

**IMPACT**: No combinational loops. All flip-flops use `always_ff @(posedge clk)`. Active-low asynchronous reset for safety-critical paths.

---

**REQ-FPGA-005**: RTL code coverage **shall** achieve Line >= 95%, Branch >= 90%, FSM 100% (all states and transitions).

**WHY**: High coverage ensures verification completeness for safety-critical medical imaging hardware.

**IMPACT**: Coverage measured by Vivado xsim or Questa. Coverage report required at M2 gate.

---

### 2. Panel Scan FSM Requirements

**REQ-FPGA-010**: The Panel Scan FSM **shall** implement six states: IDLE, INTEGRATE, READOUT, LINE_DONE, FRAME_DONE, ERROR.

**WHY**: Six-state FSM covers the complete scan lifecycle including error handling. State encoding defined in fpga-design.md Section 3.2.

**IMPACT**: 3-bit state encoding. FSM transitions match state diagram in fpga-design.md Section 3.1.

---

**REQ-FPGA-011**: **WHEN** SPI CONTROL register bit[0] (start_scan) is written to 1 **THEN** the FSM **shall** transition from IDLE to INTEGRATE within 1 clock cycle of clk_sys.

**WHY**: Deterministic start response ensures predictable scan initiation timing.

**IMPACT**: SPI write latency is the dominant delay. FSM samples start_scan on clk_sys rising edge.

---

**REQ-FPGA-012**: **WHILE** in INTEGRATE state **THEN** the FSM **shall** assert gate_on output for exactly `gate_on_us` microseconds (configurable via SPI register 0x20).

**WHY**: Gate timing precision determines X-ray exposure accuracy. Sub-microsecond accuracy required.

**IMPACT**: Timer resolution: 1 clk_sys cycle (10 ns at 100 MHz). Timer value = gate_on_us * 100 (for 100 MHz clock).

---

**REQ-FPGA-013**: **WHEN** integration completes **THEN** the FSM **shall** transition to READOUT and wait for ROIC settling time (`roic_settle_us`) before asserting data valid.

**WHY**: ROIC requires settling time after gate transition before ADC data is valid.

**IMPACT**: Total line time = gate_on_us + roic_settle_us + adc_conv_us + readout_time.

---

**REQ-FPGA-014**: The FSM **shall** support three operating modes: Single Scan, Continuous Scan, and Calibration.

**WHY**: Single scan for diagnostic images, continuous for live preview, calibration for dark frame acquisition.

**IMPACT**: Mode selected via CONTROL register bits[3:2]. Calibration mode: gate OFF during INTEGRATE (dark frame).

---

**REQ-FPGA-015**: **WHEN** all rows are scanned (line_counter == panel_rows) **THEN** the FSM **shall** transition to FRAME_DONE, increment the 32-bit frame counter, and either return to IDLE (single mode) or INTEGRATE (continuous mode).

**WHY**: Frame completion triggers counter update and mode-dependent next action.

**IMPACT**: Frame counter wraps at 2^32. FRAME_DONE state duration: 1 clock cycle (minimal overhead).

---

**REQ-FPGA-016**: **WHEN** stop_scan is asserted (CONTROL register bit[1]) during any active state **THEN** the FSM **shall** return to IDLE within 1 line time.

**WHY**: Graceful stop allows current line to complete, preventing data corruption.

**IMPACT**: Stop takes effect at next LINE_DONE transition. Current line data preserved in buffer.

---

### 3. Line Buffer Requirements

**REQ-FPGA-020**: The line buffer **shall** implement Ping-Pong dual-bank architecture using True Dual-Port BRAMs.

**WHY**: Ping-Pong provides zero-copy data transfer with inherent clock domain isolation between ROIC write and CSI-2 read.

**IMPACT**: 2 banks, each sized for 3072 pixels x 16 bits. Total: 4 BRAMs (2 cascaded per bank).

---

**REQ-FPGA-021**: The line buffer **shall** support maximum line width of 3072 pixels at 16-bit depth.

**WHY**: Maximum tier resolution requires 3072-pixel lines. Buffer must be sized for worst case even when operating at lower tiers.

**IMPACT**: BRAM depth = 3072 words, data width = 16 bits. Address width = 12 bits (ceil(log2(3072))).

---

**REQ-FPGA-022**: The line buffer **shall** operate across two clock domains: clk_roic (write side) and clk_csi2_byte (read side).

**WHY**: ROIC data arrives at variable rate (ROIC-dependent), CSI-2 TX operates at fixed byte clock (125 MHz).

**IMPACT**: Bank select signal crosses domains via 2-stage FF synchronizer. BRAM dual-port provides inherent CDC isolation for data.

---

**REQ-FPGA-023**: **WHEN** write address catches read address in the same bank **THEN** the line buffer **shall** assert overflow flag and halt writes.

**WHY**: Overflow indicates that the read side (CSI-2 TX) cannot keep up with the write side (ROIC). Data corruption must be prevented.

**IMPACT**: Overflow flag reported via ERROR_FLAGS register (bit[1]). Protection logic may halt scan.

---

**REQ-FPGA-024**: Bank toggling **shall** occur on line_done signal, alternating between Bank A and Bank B.

**WHY**: Line-level granularity ensures one complete line is always available for CSI-2 TX while the next line is being written.

**IMPACT**: Bank select toggles on assertion of line_done from Panel Scan FSM.

---

### 4. CSI-2 TX Requirements

**REQ-FPGA-030**: The CSI-2 TX **shall** use AMD MIPI CSI-2 TX Subsystem IP v3.1 or later.

**WHY**: Validated IP reduces development risk and provides D-PHY compliance. License required (Vivado HL Design Edition).

**IMPACT**: IP instantiated via Vivado IP Catalog. Wrapper module (`csi2_tx_wrapper.sv`) provides AXI4-Stream interface adaptation.

---

**REQ-FPGA-031**: The CSI-2 TX **shall** transmit pixel data in RAW16 format (data type 0x2C) on Virtual Channel 0.

**WHY**: RAW16 matches 16-bit pixel depth. VC0 for single-sensor configuration.

**IMPACT**: Data type and VC configured in IP parameters. McuSimulator and SoC driver must expect RAW16/VC0.

---

**REQ-FPGA-032**: The CSI-2 TX **shall** generate valid frame structure: Frame Start -> [Line Data + CRC-16] x N rows -> Frame End.

**WHY**: MIPI CSI-2 specification requires proper packet framing for receiver synchronization.

**IMPACT**: Frame Start/End packets contain sync patterns. Each Line Data packet includes CRC-16 over pixel payload.

---

**REQ-FPGA-033**: **WHEN** the AXI4-Stream `tready` signal is deasserted **THEN** the CSI-2 TX **shall** pause transmission without data loss.

**WHY**: Backpressure from CSI-2 TX IP indicates D-PHY busy. Data source must wait.

**IMPACT**: Line buffer read pauses when tready is low. No data dropped during backpressure.

---

**REQ-FPGA-034**: The CSI-2 TX **shall** support configurable lane speed from 400 Mbps to 1250 Mbps per lane.

**WHY**: Lane speed configurability enables bandwidth sweep testing (PoC) and tier-specific optimization.

**IMPACT**: Lane speed configured via CSI2_LANE_SPEED register (0x88). MMCM/PLL reconfiguration may be required for speed changes.

---

**REQ-FPGA-035**: The D-PHY output **shall** use OSERDES2 primitives with LVDS_25 I/O standard on 4 data lanes + 1 clock lane.

**WHY**: Artix-7 native OSERDES provides serialization without external PHY. LVDS_25 meets D-PHY electrical specifications.

**IMPACT**: 10:1 DDR serialization. D-PHY differential swing: 200 mV typical. Rise/fall time < 100 ps.

---

**REQ-FPGA-036**: The CRC-16 engine **shall** compute CRC per MIPI CSI-2 specification over each line's pixel payload.

**WHY**: CRC-16 enables receiver-side data integrity verification. Medical imaging requires zero bit errors.

**IMPACT**: CRC polynomial per CSI-2 spec. CRC appended to each line packet. SoC validates CRC on receive.

---

### 5. SPI Slave Requirements

**REQ-FPGA-040**: The SPI Slave **shall** implement SPI Mode 0 (CPOL=0, CPHA=0) at up to 50 MHz clock frequency.

**WHY**: SPI Mode 0 compatible with i.MX8M Plus SPI master. 50 MHz provides sufficient bandwidth for register access.

**IMPACT**: Data sampled on SCLK rising edge. CS_N active low.

---

**REQ-FPGA-041**: The SPI transaction format **shall** be: 8-bit address + 8-bit R/W flag + 16-bit data (32 bits total).

**WHY**: Simple protocol sufficient for register map access. 8-bit address supports up to 256 registers.

**IMPACT**: Write: SoC sends addr + W + data. Read: SoC sends addr + R, FPGA returns data on MISO.

---

**REQ-FPGA-042**: The SPI Slave **shall** implement the complete register map defined in `fpga-design.md` Section 6.3.

**WHY**: Register map is the FPGA control and status interface. All registers must be accessible.

**IMPACT**: Control (0x00-0x0F), Timing (0x20-0x3F), Panel Config (0x40-0x5F), CSI-2 Config (0x80-0x8F), Data Status (0x90-0x9F), Error Flags (0xA0-0xAF), Identification (0xF0-0xFF).

---

**REQ-FPGA-043**: **WHEN** SPI CS_N is deasserted mid-transaction **THEN** the SPI Slave **shall** abort the transaction without modifying any register.

**WHY**: CS_N deassertion may indicate SoC reset or communication error. Partial writes must not corrupt state.

**IMPACT**: Write latched only on valid transaction completion (all 32 bits received with CS_N held low).

---

**REQ-FPGA-044**: **WHEN** an unmapped register address is accessed **THEN** the SPI Slave **shall** return 0x0000 (read) and ignore the write.

**WHY**: Graceful handling of invalid addresses prevents undefined behavior.

**IMPACT**: Address decoder returns zero for unmapped ranges. No side effects from invalid access.

---

### 6. Protection Logic Requirements

**REQ-FPGA-050**: The protection logic **shall** detect 8 error conditions as defined in fpga-design.md Section 8.1.

**WHY**: Comprehensive error detection ensures safe operation of X-ray equipment.

**IMPACT**: Error codes: timeout (0x01), overflow (0x02), CRC (0x04), overexposure (0x08), ROIC fault (0x10), D-PHY error (0x20), config error (0x40), watchdog (0x80).

---

**REQ-FPGA-051**: **WHEN** any fatal error is detected (timeout, overflow, ROIC fault, D-PHY error, watchdog) **THEN** the protection logic **shall** transition the FSM to ERROR state and enter safe state within 10 clock cycles.

**WHY**: Fast error response prevents unsafe X-ray exposure and data corruption.

**IMPACT**: Safe state: gate OFF, CSI-2 TX disabled (LP mode), line buffer write disabled, SPI remains active.

---

**REQ-FPGA-052**: **WHILE** in safe state **THEN** all gate control outputs **shall** be held LOW (no X-ray exposure).

**WHY**: Patient safety requires immediate exposure termination on error.

**IMPACT**: Gate outputs gated by error_active signal. Safe state is fail-safe (outputs default to OFF).

---

**REQ-FPGA-053**: **WHEN** error_clear bit is written via SPI CONTROL register **THEN** all ERROR_FLAGS **shall** be cleared and FSM **shall** return to IDLE.

**WHY**: SoC must be able to recover the system after error analysis and corrective action.

**IMPACT**: Error clearing is a deliberate SoC action. FSM only returns to IDLE after explicit clear.

---

**REQ-FPGA-054**: The watchdog timer **shall** trigger if no SPI transaction occurs within the configured timeout period (default: 100 ms).

**WHY**: SoC hang or communication loss must be detected to prevent uncontrolled operation.

**IMPACT**: Watchdog resets on any valid SPI transaction. Timeout configurable via protection register.

---

### 7. Clock and Reset Requirements

**REQ-FPGA-060**: The FPGA **shall** use MMCM to generate four clock domains from a 100 MHz input: clk_sys (100 MHz), clk_pixel (125.83 MHz), clk_csi2_byte (125 MHz), clk_dphy_hs (500 MHz).

**WHY**: Multiple clock domains required for system logic, pixel processing, CSI-2 byte clock, and D-PHY serialization.

**IMPACT**: MMCM configuration per fpga-design.md Section 9. PLL lock status monitored.

---

**REQ-FPGA-061**: All clock domain crossings **shall** use appropriate synchronization: 2-stage FF for single-bit signals, Gray coding for multi-bit counters, BRAM dual-port for data buses.

**WHY**: CDC violations cause metastability. Proper synchronization ensures reliable inter-domain communication.

**IMPACT**: CDC report from Vivado must show zero violations. Synchronizer primitives documented in design.

---

**REQ-FPGA-062**: The system **shall** support power-on reset (asynchronous assert, synchronous deassert) and software reset (via SPI CONTROL register bit[2]).

**WHY**: Both reset types needed: power-on for initial state, software for error recovery.

**IMPACT**: Reset synchronizer in each clock domain. Software reset triggers full re-initialization sequence.

---

### 8. Unwanted Requirements

**REQ-FPGA-070**: The FPGA design **shall not** use USB 3.x in any form.

**WHY**: Artix-7 XC7A35T resource constraints prohibit USB 3.x (LUT 72-120%). This is a permanent exclusion.

**IMPACT**: Any proposal for USB interface must be rejected.

---

**REQ-FPGA-071**: The FPGA design **shall not** use combinational feedback loops.

**WHY**: Combinational loops cause unpredictable timing and are rejected by Vivado synthesis with warnings/errors.

**IMPACT**: All feedback paths must include at least one register stage.

---

**REQ-FPGA-072**: The FPGA design **shall not** use asynchronous resets on data path flip-flops (except safety-critical outputs).

**WHY**: Asynchronous resets increase routing complexity and can cause timing issues with high fanout.

**IMPACT**: Synchronous reset preferred. Asynchronous reset limited to gate control outputs (safety requirement).

---

### 9. Optional Requirements

**REQ-FPGA-080**: **Where possible**, the FPGA should include ILA (Integrated Logic Analyzer) debug probes on key signals.

**WHY**: ILA enables real-time signal observation during hardware debugging without external equipment.

**IMPACT**: ILA probes on: pixel_data, line_valid, frame_valid, D-PHY lanes, SPI transaction, error flags. Resource cost: ~500-1000 LUTs.

---

**REQ-FPGA-081**: **Where possible**, the FPGA should support runtime D-PHY lane speed reconfiguration without full bitstream reload.

**WHY**: Lane speed changes during PoC testing reduce turnaround time.

**IMPACT**: Requires MMCM dynamic reconfiguration (DRP interface). Optional for production, useful for development.

---

---

## Technical Constraints

### Device Constraints

| Resource | Available | Budget (60%) | Notes |
|----------|-----------|-------------|-------|
| LUTs | 20,800 | 12,480 | Hard limit for all RTL + debug |
| Flip-Flops | 41,600 | N/A | Typically not limiting |
| Block RAM (36Kb) | 50 | 30 | Line buffer uses 4, debug uses 1-2 |
| I/O Pins | ~250 | ~60 used | D-PHY + SPI + ROIC + debug |

### Timing Constraints

- All clocks must have positive WNS (>= 1 ns target)
- SPI setup/hold: 5 ns max input delay, 5 ns max output delay
- D-PHY timing managed by AMD MIPI CSI-2 TX IP internals

### Environmental Constraints

- Operating temperature: 0 to 85 C (Commercial grade)
- FPGA junction temperature: < 85 C under continuous operation
- Power: < 2 W total (estimated 1.0-1.5 W)

---

## Acceptance Criteria

### AC-FPGA-001: Resource Utilization

**GIVEN**: Complete FPGA design synthesized and implemented in Vivado
**WHEN**: Utilization report is generated
**THEN**: LUT utilization < 60% (12,480 LUTs)
**AND**: BRAM utilization < 50% (25 BRAMs)
**AND**: WNS >= 1 ns for all clock domains

---

### AC-FPGA-002: FSM State Coverage

**GIVEN**: Panel Scan FSM testbench with all stimulus patterns
**WHEN**: Coverage report is generated
**THEN**: 100% state coverage (all 6 states visited)
**AND**: 100% transition coverage (all valid transitions exercised)
**AND**: FSM encoding matches specification (3-bit encoding)

---

### AC-FPGA-003: Line Buffer Data Integrity

**GIVEN**: Counter pattern written to line buffer (3072 pixels, 16-bit)
**WHEN**: Data is read back from opposite bank after ping-pong swap
**THEN**: All 3072 pixel values match written values (zero bit errors)
**AND**: Read data available within 1 clock cycle of read enable

---

### AC-FPGA-004: CSI-2 Packet Correctness

**GIVEN**: One frame of known data transmitted through CSI-2 TX
**WHEN**: Output packets are captured and parsed
**THEN**: Frame Start packet present with correct data type (0x2C) and VC (0)
**AND**: Line Data packets contain correct pixel values with valid CRC-16
**AND**: Frame End packet present after last line
**AND**: Total packet count = 2 + N_rows (FS + N lines + FE)

---

### AC-FPGA-005: SPI Register Access

**GIVEN**: SPI testbench writing and reading all defined registers
**WHEN**: Write-read sequence completes for each register
**THEN**: Read-only registers return correct values (DEVICE_ID = 0xA735)
**AND**: Read-write registers return last written value
**AND**: Unmapped addresses return 0x0000
**AND**: All transactions complete within 32 SCLK cycles

---

### AC-FPGA-006: Protection Logic Response

**GIVEN**: Error injection testbench for all 8 error conditions
**WHEN**: Each error condition is triggered
**THEN**: Corresponding ERROR_FLAGS bit is set within 10 clock cycles
**AND**: FSM transitions to ERROR state for fatal errors
**AND**: Gate outputs held LOW in safe state
**AND**: Error clearing restores IDLE state

---

### AC-FPGA-007: Throughput Validation

**GIVEN**: Full pipeline operating at Intermediate-A tier (2048x2048, 16-bit, 15 fps)
**WHEN**: 100 consecutive frames are transmitted
**THEN**: Measured CSI-2 output throughput >= 1.01 Gbps
**AND**: Zero frame drops
**AND**: Frame interval within 5% of target (66.7 ms)

---

### AC-FPGA-008: CDC Verification

**GIVEN**: Complete design with all clock domain crossings
**WHEN**: Vivado CDC report is generated
**THEN**: Zero CDC violations reported
**AND**: All synchronizer chains meet minimum path delay requirements

---

### AC-FPGA-009: Golden Reference Match

**GIVEN**: FpgaSimulator (C#) and FPGA RTL simulation with identical input
**WHEN**: Outputs are compared bit-by-bit
**THEN**: CSI-2 packet output matches between simulator and RTL (bit-exact)
**AND**: Register values match for all read operations
**AND**: FSM state sequences match

---

## Dependencies

### Internal Dependencies

- `docs/architecture/fpga-design.md`: Complete architecture reference (register map, FSM, blocks)
- `docs/architecture/system-architecture.md`: System-level interface specifications
- `SPEC-SIM-001`: FpgaSimulator as golden reference model
- `detector_config.yaml`: FPGA-specific configuration parameters

### External Dependencies

- AMD Vivado HL Design Edition license (CSI-2 TX IP access)
- AMD MIPI CSI-2 TX Subsystem IP v3.1 or later
- Artix-7 XC7A35T-FGG484 evaluation board (for HIL validation)

---

## Risks

### R-FPGA-001: CSI-2 TX IP Resource Consumption

**Risk**: AMD CSI-2 TX IP consumes more LUTs than estimated (>5,500 LUTs), pushing total utilization above 60%.

**Probability**: Low (IP documented at 3,000-5,000 LUTs for Artix-7)
**Impact**: High (may require FPGA upgrade or feature reduction)

**Mitigation**: Evaluate IP resource usage early in W4 (PoC phase). If >5,500 LUTs, optimize wrapper logic or upgrade to XC7A50T.

---

### R-FPGA-002: Timing Closure at Higher Lane Speeds

**Risk**: D-PHY timing closure fails at 1.25 Gbps/lane due to OSERDES path delay.

**Probability**: Medium (Artix-7 OSERDES at edge of specification)
**Impact**: Medium (fall back to 1.0 Gbps/lane, still sufficient for Intermediate-A tier)

**Mitigation**: Start PoC at conservative 1.0 Gbps/lane. Incrementally increase during lane speed sweep.

---

### R-FPGA-003: ROIC Interface Compatibility

**Risk**: Actual ROIC output format does not match assumed LVDS interface specification.

**Probability**: Medium (ROIC datasheet may have ambiguities)
**Impact**: Medium (requires ISERDES reconfiguration or additional logic)

**Mitigation**: Characterize ROIC output early. ROIC interface module is modular and replaceable.

---

## Traceability

### Parent Documents

- `docs/architecture/fpga-design.md`: Block diagram, register map, FSM, resource estimates
- `SPEC-ARCH-001`: P0 architecture decisions (CSI-2, FPGA device, resource budget)
- `SPEC-POC-001`: CSI-2 PoC validation (lane speed, throughput requirements)

### Child Documents

- `docs/testing/unit-test-plan.md`: FV-01 to FV-11 (RTL verification tests)
- `docs/testing/hil-test-plan.md`: HIL Pattern A (data integrity with hardware)

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (analyst) | Initial SPEC creation for FPGA RTL |

---

## Review Record

- Date: 2026-02-17
- Reviewer: manager-quality
- Status: Approved
- TRUST 5: T:5 R:5 U:5 S:5 T:5

---

**END OF SPEC**
