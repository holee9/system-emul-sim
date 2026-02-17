# SPEC-FPGA-001: Implementation Plan

## Overview

This implementation plan outlines the phased approach to FPGA RTL development for the X-ray Detector Panel System. The plan covers five RTL modules targeting Xilinx Artix-7 XC7A35T-FGG484, following DDD (ANALYZE-PRESERVE-IMPROVE) methodology per quality.yaml hybrid settings.

---

## Implementation Phases

### Phase 1: RTL Foundation and Infrastructure (Primary Goal)

**Objective**: Establish clock/reset infrastructure, SPI register map, and verification framework.

**Tasks**:

1. **Clock and Reset Infrastructure**
   - Configure MMCM from 100 MHz input to generate 4 clock domains
   - Implement reset synchronizers for each clock domain (async assert, sync deassert)
   - Verify clock frequencies and reset behavior in simulation
   - Deliverable: `clock_reset_top.sv`, MMCM IP configuration

2. **SPI Slave Module**
   - Implement SPI Mode 0 (CPOL=0, CPHA=0) protocol engine
   - Implement 8-bit address + 8-bit R/W + 16-bit data transaction format
   - Create complete register map per fpga-design.md Section 6.3
   - Verify CS_N abort handling, unmapped address behavior
   - Deliverable: `spi_slave.sv`, SPI testbench

3. **Verification Framework Setup**
   - Configure Vivado xsim or Questa for SystemVerilog simulation
   - Set up coverage collection (line, branch, FSM)
   - Create common testbench utilities (clock generators, reset drivers, SPI BFM)
   - Deliverable: `tb/common/` testbench library

**Dependencies**:
- `docs/architecture/fpga-design.md` Section 6.3 (register map definition)
- AMD Vivado license (synthesis and simulation)

---

### Phase 2: Core Data Path (Secondary Goal)

**Objective**: Implement Panel Scan FSM and Line Buffer forming the pixel acquisition data path.

**Tasks**:

1. **Panel Scan FSM**
   - Implement 6-state FSM: IDLE, INTEGRATE, READOUT, LINE_DONE, FRAME_DONE, ERROR
   - Implement gate timing (gate_on_us configurable via SPI register 0x20)
   - Implement ROIC settling time delay (roic_settle_us)
   - Implement 3 operating modes: Single Scan, Continuous, Calibration
   - Implement stop_scan (graceful stop within 1 line time)
   - Implement 32-bit frame counter (wraps at 2^32)
   - Verify: 100% FSM state and transition coverage
   - Deliverable: `panel_scan_fsm.sv`, FSM testbench

2. **Line Buffer (Ping-Pong)**
   - Implement dual-bank architecture using True Dual-Port BRAMs
   - Size: 3072 pixels x 16-bit per bank (4 BRAMs, 2 cascaded per bank)
   - Implement write side (clk_roic domain) and read side (clk_csi2_byte domain)
   - Implement 2-stage FF synchronizer for bank select CDC
   - Implement overflow detection (write catches read in same bank)
   - Implement bank toggle on line_done signal
   - Verify: 10,000 bank switches with zero data corruption
   - Deliverable: `line_buffer.sv`, line buffer testbench

3. **FSM-Buffer Integration**
   - Connect Panel Scan FSM outputs to Line Buffer control inputs
   - Verify end-to-end pixel data flow from ROIC input to buffer output
   - Verify mode switching and frame boundary handling
   - Deliverable: Integration testbench

**Dependencies**:
- Phase 1 complete (clock/reset infrastructure, SPI register access for configuration)
- `docs/architecture/fpga-design.md` Sections 3.1-3.2 (FSM states, transitions)

---

### Phase 3: CSI-2 Transmit Path (Final Goal)

**Objective**: Implement CSI-2 TX wrapper and complete the FPGA-to-SoC data path.

**Tasks**:

1. **CSI-2 TX Wrapper**
   - Instantiate AMD MIPI CSI-2 TX Subsystem IP v3.1 via Vivado IP Catalog
   - Implement AXI4-Stream interface adaptation (line buffer read to tdata/tvalid/tlast)
   - Implement RAW16 (0x2E) data type on Virtual Channel 0
   - Implement frame structure: Frame Start -> Line Data x N -> Frame End
   - Implement backpressure handling (pause on tready deasserted)
   - Implement CRC-16 computation per MIPI CSI-2 specification
   - Implement configurable lane speed (400-1250 Mbps/lane via register 0x88)
   - Deliverable: `csi2_tx_wrapper.sv`, CSI-2 packet testbench

2. **D-PHY Output Configuration**
   - Configure OSERDES2 primitives for 10:1 DDR serialization
   - Configure LVDS_25 I/O standard for 4 data lanes + 1 clock lane
   - Create XDC constraints for D-PHY pin locations and timing
   - Deliverable: `constraints/dphy_pins.xdc`, timing constraints

3. **Full Pipeline Integration**
   - Connect: Panel Scan FSM -> Line Buffer -> CSI-2 TX -> D-PHY Output
   - Verify Intermediate-A throughput (2048x2048, 16-bit, 15 fps, >= 1.01 Gbps)
   - Run 100-frame sustained throughput test
   - Deliverable: `fpga_top.sv`, full pipeline testbench

**Dependencies**:
- Phase 2 complete (FSM + Line Buffer operational)
- AMD MIPI CSI-2 TX Subsystem IP license
- `docs/architecture/fpga-design.md` Sections 4-5 (CSI-2 TX, D-PHY)

---

### Phase 4: Protection and Safety (Final Goal - continued)

**Objective**: Implement protection logic, complete error handling, and achieve safety compliance.

**Tasks**:

1. **Protection Logic**
   - Implement 8 error condition detectors per fpga-design.md Section 8.1
   - Implement ERROR_FLAGS register (bit-mapped, write-1-to-clear)
   - Implement safe state transition within 10 clock cycles of fatal error
   - Implement watchdog timer (default 100 ms, configurable)
   - Verify all 8 error conditions with dedicated testbench
   - Deliverable: `protection_logic.sv`, error injection testbench

2. **Safe State Integration**
   - Connect protection logic to FSM (ERROR state trigger)
   - Verify gate outputs held LOW in safe state
   - Verify CSI-2 TX enters LP mode in safe state
   - Verify line buffer write disabled in safe state
   - Verify SPI remains active in safe state
   - Verify error_clear restores IDLE state
   - Deliverable: Safe state integration testbench

3. **System-Level Verification**
   - Run all module testbenches with coverage enabled
   - Achieve: Line >= 95%, Branch >= 90%, FSM 100%
   - Generate Vivado CDC report (target: zero violations)
   - Run golden reference comparison with FpgaSimulator (SPEC-SIM-001)
   - Deliverable: Coverage reports, CDC report, golden reference comparison log

**Dependencies**:
- Phase 3 complete (full pipeline operational)
- SPEC-SIM-001 FpgaSimulator available for golden reference comparison
- `docs/architecture/fpga-design.md` Section 8 (Protection Logic)

---

### Phase 5: Optimization and HIL Preparation (Optional Goal)

**Objective**: Optimize design, add debug features, and prepare for hardware-in-the-loop testing.

**Tasks**:

1. **Resource Optimization**
   - Analyze post-implementation utilization report
   - Optimize LUT usage if approaching 60% threshold
   - Optimize BRAM usage and timing paths
   - Deliverable: Optimized synthesis results

2. **Debug Instrumentation (Optional)**
   - Add ILA debug probes on key signals (REQ-FPGA-080)
   - Signals: pixel_data, line_valid, frame_valid, D-PHY lanes, SPI, error flags
   - Budget: 500-1000 additional LUTs
   - Deliverable: ILA configuration, debug bitstream

3. **HIL Preparation**
   - Generate production bitstream for Artix-7 XC7A35T evaluation board
   - Create hardware test scripts for SoC-FPGA integration
   - Document pin mapping and board connections
   - Deliverable: Production bitstream, HIL test procedures

**Dependencies**:
- Phase 4 complete (all coverage targets met)
- Artix-7 XC7A35T evaluation board available
- NXP i.MX8M Plus EVK available (for HIL integration)

---

## Task Decomposition

### Priority-Based Milestones

**Primary Goal**: Establish RTL foundation and verification infrastructure
- Phase 1: Clock/reset, SPI slave, testbench framework
- Success criteria: SPI register access working, verification framework operational

**Secondary Goal**: Implement core pixel data acquisition path
- Phase 2: Panel Scan FSM, Line Buffer, FSM-Buffer integration
- Success criteria: Pixel data flows from simulated ROIC input through buffer

**Final Goal**: Complete data path and safety features
- Phase 3: CSI-2 TX wrapper, D-PHY output, full pipeline
- Phase 4: Protection logic, safe state, system verification
- Success criteria: Intermediate-A throughput achieved, all safety features verified, coverage met

**Optional Goal**: Optimization and hardware preparation
- Phase 5: Resource optimization, ILA debug, HIL preparation
- Success criteria: Bitstream generated and ready for hardware testing

---

## Technology Stack Specifications

### FPGA Development Stack

**Target Device**: Xilinx Artix-7 XC7A35T-FGG484

| Resource | Available | Budget (60%) | Estimated Usage |
|----------|-----------|-------------|----------------|
| LUTs | 20,800 | 12,480 | 4,400-7,700 (21-37%) |
| Flip-Flops | 41,600 | N/A | ~3,000-5,000 |
| Block RAM (36Kb) | 50 | 30 | 7-10 (14-20%) |
| DSP Slices | 90 | N/A | 0 (no DSP operations) |
| I/O Pins | ~250 | ~60 used | D-PHY + SPI + ROIC + debug |

**Development Tools**:
- AMD Vivado Design Suite: 2023.2 or later (HL Design Edition for MIPI IP)
- Simulation: Vivado xsim (primary) or Questa/ModelSim (secondary)
- Language: SystemVerilog (IEEE 1800-2017)
- Version Control: Git on Gitea (`fpga/` repository)

**IP Cores**:

| IP Core | Version | Purpose | License |
|---------|---------|---------|---------|
| AMD MIPI CSI-2 TX Subsystem | v3.1+ | CSI-2 packet generation, D-PHY TX | HL Design Edition |
| Clocking Wizard (MMCM) | Vivado IP | 4-clock generation from 100 MHz | Free (included) |
| AXI4-Stream Interconnect | Vivado IP | Data path connection | Free (included) |

**Clock Domains**:

| Clock | Frequency | Source | Purpose |
|-------|-----------|--------|---------|
| clk_sys | 100 MHz | MMCM output / input | System logic, SPI, FSM |
| clk_pixel | 125.83 MHz | MMCM output | Pixel processing |
| clk_csi2_byte | 125 MHz | MMCM output | CSI-2 byte clock |
| clk_dphy_hs | 500 MHz | MMCM output | D-PHY high-speed serialization |

---

### Verification Stack

**Testbench Architecture**:
- Per-module unit testbenches (5 modules)
- Integration testbenches (FSM+Buffer, full pipeline)
- Coverage collection enabled for all simulations

**Coverage Targets**:

| Coverage Type | Target | Tool |
|---------------|--------|------|
| Line Coverage | >= 95% | Vivado xsim / Questa |
| Branch Coverage | >= 90% | Vivado xsim / Questa |
| FSM State Coverage | 100% | Vivado xsim / Questa |
| FSM Transition Coverage | 100% | Vivado xsim / Questa |
| Toggle Coverage (I/O) | >= 80% | Questa (optional) |

**Testbench Components**:

| Component | Purpose | Files |
|-----------|---------|-------|
| Clock Generator | Generate 4 clock domains | `tb/common/clk_gen.sv` |
| Reset Driver | Power-on and software reset | `tb/common/rst_driver.sv` |
| SPI BFM | SPI master bus functional model | `tb/common/spi_bfm.sv` |
| Pixel Generator | Generate known pixel patterns | `tb/common/pixel_gen.sv` |
| CSI-2 Monitor | Capture and parse CSI-2 packets | `tb/common/csi2_monitor.sv` |
| Coverage Collector | Aggregate coverage data | `tb/common/coverage.sv` |

---

## Risk Analysis

### Risk 1: CSI-2 TX IP Resource Overrun

**Risk Description**: AMD MIPI CSI-2 TX Subsystem IP consumes more LUTs than estimated (>5,500), pushing total above 60%.

**Probability**: Low (IP documented at 3,000-5,000 LUTs for Artix-7)
**Impact**: High (budget overrun requires optimization or FPGA upgrade)

**Mitigation**:
- Evaluate IP resource usage immediately when IP is first instantiated (Phase 3)
- Monitor cumulative utilization after each module integration
- Optimize wrapper logic to minimize overhead

**Contingency**:
- Reduce ILA debug probes (save 500-1000 LUTs)
- Simplify register file (reduce unused register groups)
- Upgrade to Artix-7 XC7A50T as last resort (52,160 LUTs)

---

### Risk 2: Timing Closure at 500 MHz D-PHY

**Risk Description**: D-PHY high-speed clock domain (500 MHz) may have timing closure difficulty on Artix-7.

**Probability**: Medium (Artix-7 speed grade dependent)
**Impact**: Medium (may need to reduce lane speed, still sufficient for Intermediate-A)

**Mitigation**:
- Use pipelining for OSERDES data path
- Apply proper timing constraints from AMD MIPI IP documentation
- Start with conservative 400 Mbps/lane (confirmed stable)

**Contingency**:
- Reduce to 400 Mbps/lane and limit to Intermediate-A tier
- Apply additional pipeline stages in critical paths
- Consider industrial speed grade device (-2I or -3)

---

### Risk 3: CDC Metastability in Line Buffer

**Risk Description**: Clock domain crossing between clk_roic and clk_csi2_byte may cause data corruption.

**Probability**: Low (BRAM dual-port provides inherent isolation)
**Impact**: High (data corruption in medical imaging is unacceptable)

**Mitigation**:
- Use BRAM dual-port for data bus CDC (inherently safe)
- Use 2-stage FF synchronizer for single-bit control signals (bank select)
- Use Gray coding for any multi-bit counters crossing domains
- Run Vivado CDC analysis to verify zero violations

**Contingency**:
- Add additional synchronizer stages (3-stage) if metastability margin insufficient
- Reduce clock frequency ratio between domains
- Add data integrity checking (read-back verification) in testbench

---

### Risk 4: FpgaSimulator Golden Reference Mismatch

**Risk Description**: FpgaSimulator (C#, SPEC-SIM-001) and FPGA RTL may produce different outputs due to implementation differences.

**Probability**: Medium (C# float vs RTL integer arithmetic, timing model differences)
**Impact**: Medium (requires debugging to identify source of mismatch)

**Mitigation**:
- Use integer-only arithmetic in both simulator and RTL (no floating point)
- Document exact CRC-16 polynomial and algorithm for both implementations
- Use identical test vectors (deterministic pixel patterns)
- Compare at packet level (not cycle-accurate timing)

**Contingency**:
- Define acceptable mismatch tolerance (e.g., timing differences OK, data must be bit-exact)
- Create automated comparison scripts
- Fix discrepancies in simulator if RTL is correct (RTL is reference for hardware)

---

## Dependencies

### External Dependencies

**Hardware**:
- Artix-7 XC7A35T evaluation board (for HIL testing, Phase 5)
- NXP i.MX8M Plus EVK (for CSI-2 receiver testing, Phase 5)

**Software and IP**:
- AMD Vivado HL Design Edition license (MIPI CSI-2 TX IP access)
- AMD MIPI CSI-2 TX Subsystem IP v3.1+ documentation
- Vivado xsim or Questa/ModelSim license (simulation)

**Documentation**:
- `docs/architecture/fpga-design.md` v1.0.0 (complete architecture reference)
- MIPI CSI-2 specification v1.3 (packet format, CRC-16 definition)

---

### Internal Dependencies

**SPEC Dependencies**:

| SPEC | Dependency Type | What It Provides |
|------|----------------|------------------|
| SPEC-ARCH-001 | Upstream | P0 decisions (CSI-2, FPGA device, resource budget) |
| SPEC-SIM-001 | Parallel | FpgaSimulator as golden reference model |
| SPEC-FW-001 | Downstream | SoC CSI-2 RX driver (consumes FPGA output) |
| SPEC-POC-001 | Downstream | CSI-2 PoC validation plan |

**Document Dependencies**:

| Document | Section | Content Required |
|----------|---------|-----------------|
| fpga-design.md | Section 3.1-3.2 | FSM states, transitions, encoding |
| fpga-design.md | Section 6.3 | Register map definition |
| fpga-design.md | Section 8.1 | Error conditions and codes |
| fpga-design.md | Section 9 | MMCM configuration |
| detector_config.yaml | FPGA section | Timing parameters, resolution, bit depth |

---

### Phase Dependencies

```
Phase 1 (Foundation) ─────────┐
                               ├──► Phase 2 (Data Path) ──► Phase 3 (CSI-2 TX) ──► Phase 4 (Protection) ──► Phase 5 (HIL)
SPEC-SIM-001 (Simulator) ─────┘                                                          │
                                                                                          ▼
                                                                                    Golden Reference Match
```

---

## Milestone Alignment

| Project Milestone | Phase | Key Deliverables | Gate Criteria |
|------------------|-------|------------------|---------------|
| M1-Doc (W8) | Pre-Phase 1 | fpga-design.md complete, SPEC-FPGA-001 approved | Document review passed |
| M2-Impl (W14) | Phase 1-4 | All RTL modules verified, coverage targets met | M2 gate checklist passed |
| M0.5-PoC (W26) | Phase 5 | Bitstream on HW, CSI-2 PoC with SoC | Data integrity confirmed |
| M6-Final (W28) | Phase 5 | HIL testing complete, system validated | All acceptance criteria met |

---

## Next Steps

### Immediate Actions (Post-SPEC Approval)

1. **Verify fpga-design.md completeness**
   - Confirm register map (Section 6.3) is finalized
   - Confirm FSM state diagram (Section 3.1) is finalized
   - Confirm error conditions (Section 8.1) are complete

2. **Set up FPGA verification environment**
   - Install Vivado and configure for Artix-7 XC7A35T
   - Create `fpga/` directory structure: `rtl/`, `tb/`, `constraints/`, `ip/`, `sim/`
   - Create initial testbench framework with clock and reset generators

3. **Begin Phase 1 implementation**
   - Start with clock/reset infrastructure (MMCM configuration)
   - Implement SPI slave module and register map
   - Write characterization tests (DDD methodology: ANALYZE-PRESERVE-IMPROVE)

### Transition to Phase 2

**Trigger**: Phase 1 SPI register access verified, verification framework operational

**Preparation**:
- Review FSM state diagram in fpga-design.md
- Prepare FSM testbench stimulus patterns (all states and transitions)
- Prepare line buffer test patterns (3072 pixels, 16-bit counter pattern)

---

## Traceability

This implementation plan aligns with:

- **SPEC-FPGA-001 spec.md**: All 33 requirements mapped to implementation phases
- **SPEC-ARCH-001**: P0 architecture decisions (CSI-2, FPGA device, resource budget)
- **docs/architecture/fpga-design.md**: Block diagram, register map, FSM, resource estimates
- **docs/testing/unit-test-plan.md**: FV-01 to FV-11 (RTL verification tests)
- **detector_config.yaml**: FPGA-specific configuration parameters

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | ABYZ-Lab Agent (spec-fpga) | Initial implementation plan for SPEC-FPGA-001 |

---

**END OF PLAN**
