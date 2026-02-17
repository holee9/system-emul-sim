# SPEC-POC-001: M0.5 CSI-2 Proof of Concept (PoC) - End-to-End Validation

---
id: SPEC-POC-001
version: 1.0.0
status: planned
created: 2026-02-17
updated: 2026-02-17
author: MoAI Agent (manager-spec)
priority: critical
milestone: M0.5
gate_week: W6
---

## HISTORY

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (manager-spec) | Initial SPEC creation for M0.5 CSI-2 PoC milestone |

---

## Overview

### Milestone Context

**M0.5 (Week 6)** is a critical GO/NO-GO gate for the X-ray Detector Panel System project. This milestone validates the feasibility of CSI-2 MIPI 4-lane D-PHY as the sole high-speed data interface between FPGA (Xilinx Artix-7 XC7A35T) and SoC (NXP i.MX8M Plus).

### PoC Objectives

The CSI-2 Proof of Concept validates five critical assumptions:

1. **FPGA D-PHY TX Capability**: Artix-7 OSERDES can achieve 1.0-1.25 Gbps/lane serialization
2. **SoC CSI-2 RX Compatibility**: i.MX8M Plus CSI-2 receiver can capture FPGA D-PHY output
3. **End-to-End Throughput**: Measured aggregate bandwidth reaches ≥70% of Target tier requirement (2.01 Gbps × 0.7 = 1.41 Gbps minimum)
4. **Signal Integrity**: D-PHY electrical characteristics meet MIPI D-PHY v1.2 specification
5. **Packet Integrity**: CSI-2 packet framing, CRC, and data payload are validated

### GO/NO-GO Decision

**GO Criteria**:
- Measured throughput ≥1.41 Gbps (70% of Target tier)
- Signal integrity validated (eye diagram meets D-PHY spec)
- Zero data corruption over 1000+ frames
- SoC CSI-2 receiver successfully decodes FPGA packets

**NO-GO Implications**:
- Architecture review required (potential SoC platform change or external D-PHY PHY chip)
- Schedule impact: +2-4 weeks for alternative evaluation
- Budget impact: Additional hardware procurement

---

## Requirements

### 1. Ubiquitous Requirements (System-Wide Invariants)

**REQ-POC-001**: The PoC **shall** use Xilinx Artix-7 XC7A35T-FGG484 FPGA evaluation board.

**WHY**: Project constraint confirmed at M0. All development and production hardware use this device.

**IMPACT**: PoC results directly transfer to production FPGA design. No device-specific re-validation needed.

---

**REQ-POC-002**: The PoC **shall** use NXP i.MX8M Plus evaluation board as SoC platform.

**WHY**: i.MX8M Plus selected at M0 for CSI-2 receiver, processing capability, and 10 GbE expansion path.

**IMPACT**: SoC firmware and CSI-2 RX driver development uses validated hardware. Alternative SoC only if PoC fails.

---

**REQ-POC-003**: The PoC **shall** transmit CSI-2 MIPI packets via 4-lane D-PHY interface.

**WHY**: 4-lane configuration balances bandwidth (4-5 Gbps aggregate) and FPGA I/O pin count (20 pins total including grounds).

**IMPACT**: PoC validates full production configuration. Single-lane and 2-lane tests may be performed incrementally for debug.

---

**REQ-POC-004**: The PoC **shall** generate test patterns in FPGA and validate data integrity at SoC memory.

**WHY**: End-to-end validation confirms CSI-2 TX, D-PHY electrical layer, SoC RX pipeline, and memory DMA.

**IMPACT**: Data corruption detection enables immediate root cause analysis (FPGA timing, cable SI, SoC driver).

---

### 2. Event-Driven Requirements (PoC Test Triggers)

**REQ-POC-005**: **WHEN** FPGA CSI-2 TX module is instantiated **THEN** OSERDES lane speed shall be configurable between 1.0-1.25 Gbps/lane.

**WHY**: Lane speed configurability enables bandwidth sweep testing and D-PHY limit characterization.

**IMPACT**: PoC measures achievable bandwidth across speed range, identifies optimal operating point for Target tier.

---

**REQ-POC-006**: **WHEN** test pattern transmission begins **THEN** SoC shall capture frames at configured resolution and frame rate.

**WHY**: Frame capture validates CSI-2 packet parsing, line synchronization, and frame assembly.

**IMPACT**: Successful capture confirms SoC CSI-2 receiver pipeline is functional. Failure isolates SoC driver or configuration issue.

---

**REQ-POC-007**: **WHEN** 1000 frames are transmitted **THEN** data integrity check shall report zero bit errors.

**WHY**: Extended test duration validates sustained throughput stability and eliminates intermittent SI issues.

**IMPACT**: Zero errors confirms production-ready reliability. Non-zero errors trigger SI analysis (eye diagram, cable length, termination).

---

**REQ-POC-008**: **WHEN** lane speed is swept from 1.0 to 1.25 Gbps/lane **THEN** maximum stable throughput shall be documented.

**WHY**: OSERDES speed limit characterization determines Target tier achievability and Maximum tier feasibility.

**IMPACT**: Measured maximum throughput defines performance tier roadmap. If <1.41 Gbps, Target tier requires frame rate reduction or resolution compromise.

---

### 3. State-Driven Requirements (Configuration Modes)

**REQ-POC-009**: **IF** test pattern mode is "counter" **THEN** FPGA shall transmit incrementing pixel values (0x0000, 0x0001, 0x0002, ...).

**WHY**: Counter pattern enables simple bit error detection (expected value = pixel index).

**IMPACT**: Counter pattern simplifies SoC validation logic. Errors are immediately detectable as value mismatches.

---

**REQ-POC-010**: **IF** test pattern mode is "checkerboard" **THEN** FPGA shall transmit alternating 0xFFFF and 0x0000 pixels.

**WHY**: Checkerboard pattern exercises maximum toggle rate for D-PHY electrical stress testing.

**IMPACT**: Checkerboard validates signal integrity under worst-case switching conditions. Passing confirms robust SI design.

---

**REQ-POC-011**: **IF** resolution is 1024×1024 (Minimum tier) **THEN** CSI-2 packets shall use RAW16 data type (0x2C) with 1024-pixel line length.

**WHY**: Minimum tier provides baseline performance validation with minimal bandwidth stress.

**IMPACT**: Minimum tier success confirms basic CSI-2 functionality. Enables incremental scaling to higher resolutions.

---

**REQ-POC-012**: **IF** resolution is 2048×2048 (Target tier) **THEN** sustained throughput shall be ≥2.01 Gbps.

**WHY**: Target tier is project baseline performance requirement. PoC must validate achievability.

**IMPACT**: Target tier validation at PoC stage de-risks M1-M3 FPGA development. Failure triggers architecture review.

---

### 4. Unwanted Requirements (Prohibited Actions and Errors)

**REQ-POC-013**: The PoC **shall not** use external D-PHY PHY chip or re-timer IC.

**WHY**: PoC validates native Artix-7 OSERDES capability. External PHY defers validation to later phase.

**IMPACT**: PoC results directly apply to production FPGA design. External PHY introduction only if native OSERDES fails.

---

**REQ-POC-014**: The PoC **shall not** introduce data compression or bandwidth reduction techniques.

**WHY**: PoC validates raw pixel data throughput. Compression masks bandwidth limitations.

**IMPACT**: Raw data validation confirms worst-case throughput. Compression may be introduced later as optimization.

---

**REQ-POC-015**: The PoC **shall not** accept data corruption or intermittent errors as acceptable results.

**WHY**: Production system requires zero-error data integrity for medical imaging quality.

**IMPACT**: Any detected error triggers root cause analysis and mitigation before PoC completion.

---

**REQ-POC-016**: The PoC **shall not** proceed to M1 (full FPGA development) if GO criteria are not met.

**WHY**: M1-M3 phases assume validated CSI-2 interface. Proceeding without validation introduces unrecoverable architecture risk.

**IMPACT**: NO-GO decision triggers 2-4 week delay for alternative evaluation (external PHY, SoC change, interface redesign).

---

### 5. Optional Requirements (Enhanced Validation)

**REQ-POC-017**: **Where possible**, the PoC should capture D-PHY electrical waveforms using logic analyzer or oscilloscope.

**WHY**: Eye diagram validation provides signal integrity margin analysis beyond pass/fail binary result.

**IMPACT**: Eye diagram data enables PCB layout optimization and FPC cable selection for production design.

---

**REQ-POC-018**: **Where possible**, the PoC should test multiple FPC cable lengths (5 cm, 10 cm, 15 cm).

**WHY**: Cable length affects signal integrity. Testing multiple lengths identifies maximum reliable length.

**IMPACT**: Cable length characterization informs mechanical design constraints (FPGA-to-SoC board spacing).

---

**REQ-POC-019**: **Where possible**, the PoC should measure power consumption of FPGA CSI-2 TX module.

**WHY**: Power budget validation ensures thermal design adequacy for production enclosure.

**IMPACT**: Power measurement enables cooling solution selection (passive heatsink vs active fan).

---

## Technical Constraints

### FPGA Hardware Constraints

**Device**: Xilinx Artix-7 XC7A35T-FGG484 (confirmed at M0)

**OSERDES D-PHY Limitation**:
- Artix-7 OSERDES2 maximum serialization: 10:1 at DDR 1.25 Gbps (per Xilinx DS181)
- Practical D-PHY lane speed: 1.0-1.25 Gbps/lane (not 2.5 Gbps D-PHY spec maximum)
- 4-lane aggregate: 4.0-5.0 Gbps raw bandwidth

**PoC FPGA IP**:
- AMD MIPI CSI-2 TX Subsystem IP v3.1 or later (license required)
- OSERDES primitive configuration: 10:1 serialization, DDR mode, 625 MHz byte clock
- LVDS I/O buffers: IOSTANDARD="LVDS_25"

---

### SoC Platform Constraints

**Device**: NXP i.MX8M Plus (recommended at M0)

**CSI-2 Receiver Specification**:
- 2× 4-lane MIPI CSI-2 RX (per i.MX8M Plus datasheet)
- Lane speed support: 80 Mbps to 2.5 Gbps/lane (specification)
- Validated range: 1.0-1.5 Gbps/lane (typical operation per NXP documentation)

**ISP Pipeline**:
- Image Signal Processor (ISP) supports RAW8, RAW10, RAW12, RAW16 formats
- Frame buffer: DDR4 memory via DMA (up to 4 GB capacity)
- Memory bandwidth: 14.9 GB/s (sufficient for 4.53 Gbps pixel data + OS overhead)

---

### Test Pattern Specifications

**Counter Pattern**:
- Pixel format: RAW16 (16-bit unsigned integer)
- Sequence: 0x0000, 0x0001, 0x0002, ..., 0xFFFF, 0x0000 (wrap-around)
- Purpose: Bit error detection via expected value = (row × width + col) % 65536

**Checkerboard Pattern**:
- Pixel format: RAW16
- Sequence: Alternating 0xFFFF (white) and 0x0000 (black) per pixel
- Purpose: Maximum toggle rate for electrical stress testing

**PRBS Pattern** (optional):
- Pseudo-Random Binary Sequence (PRBS-15 or PRBS-23)
- Purpose: Scrambled data reduces EMI and tests receiver equalization

---

### Signal Integrity Constraints

**D-PHY Electrical Specifications** (MIPI Alliance D-PHY v1.2):
- Differential voltage swing: 200 mV typical (160-270 mV range)
- Common-mode voltage: 200 mV ± 25 mV
- Rise/fall time: <100 ps (80-120 ps typical at 1.25 Gbps)
- Eye opening: >200 mV vertical, >0.5 UI horizontal (Unit Interval)

**FPC Cable Requirements**:
- Impedance: 100 Ω differential (±10%)
- Length: ≤15 cm (SI validation required for longer cables)
- Connector: 0.5 mm pitch, gold-plated contacts

---

### Bandwidth Calculations

**Performance Tier Requirements** (from M0 decision):

| Tier | Resolution | Bit Depth | FPS | Raw Data Rate | PoC Validation |
|------|-----------|-----------|-----|---------------|----------------|
| Minimum | 1024×1024 | 14-bit | 15 | 0.21 Gbps | ✅ Baseline (10% CSI-2 utilization) |
| Target | 2048×2048 | 16-bit | 30 | 2.01 Gbps | ✅ Primary goal (50-60% utilization) |
| Maximum | 3072×3072 | 16-bit | 30 | 4.53 Gbps | ⚠️ Stretch goal (90-100% utilization) |

**PoC Success Threshold**: ≥1.41 Gbps (70% of Target tier)

**CSI-2 Protocol Overhead**: ~20-30% (packet headers, line blanking, frame blanking)
- Usable bandwidth: ~3.2-3.5 Gbps (from 4-5 Gbps raw)
- Target tier net: 2.01 Gbps × 1.25 (overhead) = 2.51 Gbps required → ✅ Fits
- Maximum tier net: 4.53 Gbps × 1.25 = 5.66 Gbps required → ⚠️ Exceeds capacity

---

## Acceptance Criteria

### AC-001: FPGA CSI-2 TX Module Instantiation

**GIVEN**: Artix-7 XC7A35T FPGA with AMD MIPI CSI-2 TX Subsystem IP

**WHEN**: CSI-2 TX module is configured for 4-lane D-PHY at 1.0 Gbps/lane

**THEN**:
- Bitstream generation completes successfully
- LUT utilization for CSI-2 TX module is ≤5,000 LUTs (≤24% of device)
- Static timing analysis reports WNS (Worst Negative Slack) ≥0 ns
- D-PHY clock and data lanes are assigned to FPGA I/O pins

**AND**: CSI-2 TX module passes basic loopback test (internal FPGA logic analyzer captures transmitted packets)

---

### AC-002: SoC CSI-2 RX Pipeline Configuration

**GIVEN**: NXP i.MX8M Plus SoC with CSI-2 receiver enabled

**WHEN**: Linux kernel driver (imx8-mipi-csi2) is configured for 4-lane RAW16 input

**THEN**:
- Kernel driver loads without errors (`dmesg | grep mipi` shows no errors)
- CSI-2 receiver reports "streaming" state (`v4l2-ctl --list-devices` shows /dev/video0)
- Frame capture command succeeds: `v4l2-ctl --device /dev/video0 --stream-mmap --stream-count=1`

**AND**: Captured frame buffer is written to DDR4 memory (verified via `/proc/meminfo` allocated pages increase)

---

### AC-003: End-to-End Data Integrity (Counter Pattern)

**GIVEN**: FPGA transmits counter pattern (0x0000, 0x0001, ...) at 1024×1024 resolution, 15 fps

**WHEN**: SoC captures 100 consecutive frames

**THEN**:
- All 100 frames are received without drops (frame counter increments sequentially)
- Pixel value verification script reports 0 bit errors
- Expected pixel value at position (row, col) = (row × 1024 + col) % 65536
- Actual pixel value matches expected value for all 1024×1024 pixels in all frames

**AND**: Total data integrity: 100 frames × 1024×1024 pixels × 16 bits = 1.68 Gbits verified

---

### AC-004: End-to-End Throughput Measurement (Target Tier)

**GIVEN**: FPGA transmits counter pattern at 2048×2048 resolution, 30 fps (Target tier)

**WHEN**: SoC captures 1000 consecutive frames over 33.33 seconds

**THEN**:
- Measured throughput ≥1.41 Gbps (70% of 2.01 Gbps requirement)
- Frame drop rate <1% (≤10 dropped frames out of 1000)
- Average frame interval = 33.33 ms ± 1 ms (30 fps ± 3%)
- Jitter (frame interval std dev) <5 ms

**AND**: Sustained throughput confirms Target tier achievability

---

### AC-005: Lane Speed Characterization

**GIVEN**: FPGA CSI-2 TX module with configurable lane speed (1.0, 1.1, 1.2, 1.25 Gbps)

**WHEN**: Lane speed is swept across range at 2048×2048, 30 fps

**THEN**:
- Each lane speed configuration is tested for 100 frames minimum
- Maximum stable lane speed is identified (zero errors for 100 frames)
- Bit error rate (BER) is measured at each speed: BER = errors / (100 frames × 2048×2048 pixels × 16 bits)
- BER <10^-12 (zero errors in 100 frames = 6.7×10^11 bits) qualifies as "stable"

**AND**: Maximum stable lane speed ≥1.0 Gbps/lane (minimum for Target tier)

---

### AC-006: Signal Integrity Validation (Eye Diagram)

**GIVEN**: Logic analyzer or oscilloscope with MIPI D-PHY decode capability

**WHEN**: D-PHY data lane 0 is probed at midpoint of FPC cable (7-8 cm from FPGA)

**THEN**:
- Eye diagram vertical opening ≥200 mV (MIPI D-PHY spec minimum)
- Eye diagram horizontal opening ≥0.5 UI (Unit Interval = 800 ps at 1.25 Gbps)
- Rise/fall time ≤100 ps (20-80% threshold)
- No ringing or overshoot exceeding 10% of signal swing

**AND**: Eye diagram screenshot is included in PoC report

**NOTE**: This criterion is **optional** if logic analyzer is unavailable. Data integrity tests (AC-003, AC-004) provide functional validation.

---

### AC-007: Packet Integrity Validation

**GIVEN**: SoC captures CSI-2 packets in raw format (before ISP processing)

**WHEN**: Packet header and CRC are extracted from captured frame

**THEN**:
- Packet data type = 0x2C (RAW16 format)
- Virtual channel = 0 (VC0)
- Word count = 2048 × 2 bytes = 4096 bytes (for 2048-pixel line)
- CRC-16 checksum matches calculated CRC over packet payload

**AND**: Zero CRC errors in 1000 frames confirms packet integrity

---

### AC-008: GO/NO-GO Decision Documentation

**GIVEN**: All PoC tests (AC-001 through AC-007) are completed

**WHEN**: Test results are compiled into PoC report

**THEN**:
- GO criteria met:
  - Measured throughput ≥1.41 Gbps (70% of Target tier)
  - Zero data corruption (bit errors) in 1000 frames
  - Signal integrity validated (eye diagram or functional test)
  - SoC CSI-2 receiver successfully decodes packets

**OR**:
- NO-GO criteria met:
  - Measured throughput <1.41 Gbps
  - Data corruption detected (>0 bit errors)
  - Signal integrity failure (eye diagram not meeting spec)
  - SoC CSI-2 receiver unable to decode packets

**AND**: Recommendation documented:
- **GO**: Proceed to M1 (Architecture Review) and M2 (Simulator Development) in parallel
- **NO-GO**: Initiate architecture review, evaluate alternatives (external D-PHY PHY chip, alternative SoC platform, interface redesign)

---

## Risks and Mitigation

### R-POC-001: FPGA OSERDES Lane Speed Insufficient

**Risk**: Artix-7 OSERDES fails to achieve 1.0 Gbps/lane stable operation

**Probability**: Low (OSERDES spec supports 1.25 Gbps)

**Impact**: High (Target tier unachievable, architecture redesign required)

**Mitigation**:
- Pre-validation: Review Xilinx UG471 (SelectIO Resources) for OSERDES timing constraints
- Incremental testing: Start at 800 Mbps/lane, incrementally increase to 1.0-1.25 Gbps
- Fallback: External D-PHY PHY chip (e.g., TI DLPC3439) for 2.5 Gbps/lane capability

**Trigger**: Lane speed characterization (AC-005) reveals maximum stable speed <1.0 Gbps/lane

---

### R-POC-002: Signal Integrity Degradation Over Cable Length

**Risk**: FPC cable length >10 cm causes signal integrity failure (eye diagram closure, bit errors)

**Probability**: Medium (SI depends on cable quality and routing)

**Impact**: Medium (mechanical design constraint, may require FPGA-SoC board proximity)

**Mitigation**:
- Pre-procurement: Select high-quality FPC cable (impedance-controlled, 100 Ω differential)
- Incremental testing: Test 5 cm, 10 cm, 15 cm cable lengths (AC-POC-018 optional requirement)
- Fallback: Limit cable length to validated maximum, adjust mechanical design accordingly

**Trigger**: Data corruption increases with cable length, or eye diagram fails at >10 cm

---

### R-POC-003: SoC CSI-2 Receiver Driver Instability

**Risk**: i.MX8M Plus Linux kernel driver (imx8-mipi-csi2) fails to capture frames reliably

**Probability**: Medium (driver maturity depends on kernel version and BSP)

**Impact**: Medium (driver debug delays PoC, but does not invalidate interface choice)

**Mitigation**:
- Pre-validation: Verify kernel version compatibility (Linux 5.15+ recommended for i.MX8M Plus)
- Incremental testing: Test single-lane, then 2-lane, then 4-lane configuration
- Fallback: Use alternative SoC platform (e.g., Raspberry Pi CM4 with known-good libcamera driver)

**Trigger**: SoC fails to capture frames (AC-002 fails), or kernel logs show CSI-2 RX errors

---

### R-POC-004: Test Pattern Validation Logic Errors

**Risk**: Software validation script reports false positives/negatives due to bugs

**Probability**: Low (validation logic is straightforward)

**Impact**: Low (delays PoC result analysis, but fixable within W6 timeline)

**Mitigation**:
- Pre-validation: Test validation script against known-good synthetic data (offline test)
- Peer review: Second engineer reviews validation logic before PoC execution
- Fallback: Manual spot-check of captured frame data (visual inspection of counter sequence)

**Trigger**: Validation script reports unexpected errors that cannot be reproduced

---

### R-POC-005: Hardware Procurement Delays

**Risk**: FPGA or SoC evaluation board not available by W3 (PoC start)

**Probability**: Low (boards are commercially available, 2-4 week lead time)

**Impact**: High (PoC delay directly delays M1-M3 phases)

**Mitigation**:
- Pre-procurement: Order FPGA and SoC boards at W1 (M0 milestone) with expedited shipping
- Fallback: Use alternative evaluation boards with similar FPGA (Artix-7 35T) and SoC (i.MX8M Plus)
- Schedule buffer: PoC timeline is W3-W6 (3 weeks), allows 1 week slip without M1 impact

**Trigger**: Board not received by W3

---

## Dependencies

### External Dependencies

**Hardware Procurement** (Week 1-3):
- Xilinx Artix-7 XC7A35T FGG484 evaluation board
- NXP i.MX8M Plus evaluation kit
- MIPI CSI-2 FPC cable (0.5 mm pitch, 15 cm max)
- Optional: Logic analyzer with MIPI D-PHY decode (Total Phase Promira or equivalent)

**Software/IP Licensing** (Week 1):
- AMD Vivado HL Design Edition (includes MIPI CSI-2 TX Subsystem IP)
- NXP i.MX8M Plus Linux BSP (Yocto or Buildroot, includes imx8-mipi-csi2 driver)

**Documentation**:
- AMD PG232 (MIPI CSI-2 TX Subsystem Product Guide)
- NXP i.MX8M Plus Reference Manual (CSI-2 receiver chapter)
- MIPI Alliance D-PHY v1.2 specification

---

### Internal Dependencies

**From M0 (Completed)**:
- Performance tier selection (Target tier = 2048×2048, 16-bit, 30 fps, 2.01 Gbps)
- SoC platform selection (i.MX8M Plus recommended)
- FPGA device locked (Artix-7 XC7A35T-FGG484)

**Required for M1 (Blocked Until PoC GO)**:
- Architecture Design Document cannot be finalized without PoC validation
- FPGA RTL development (M2-M3) assumes CSI-2 interface feasibility
- SoC firmware development assumes i.MX8M Plus CSI-2 receiver functionality

---

## Traceability

### Parent Documents

- **SPEC-ARCH-001**: P0 Architecture Decisions (M0 milestone)
  - REQ-ARCH-001: CSI-2 as exclusive high-speed interface
  - REQ-ARCH-003: Minimum tier baseline
  - REQ-ARCH-007: Target tier CSI-2 D-PHY configuration

- **X-ray_Detector_Optimal_Project_Plan.md**:
  - Section 5.2: Phase PoC (W3-W6)
  - Section 5.4.5: PoC gate criteria (≥70% throughput)
  - Section 6.1: Risk R-04 (CSI-2 D-PHY throughput insufficient)
  - Section 6.1: Risk R-10 (D-PHY signal integrity)

- **README.md**:
  - System architecture (FPGA → SoC → Host)
  - Performance tier definitions
  - Interface selection rationale

---

### Child Documents (To Be Created After PoC)

- **PoC Test Report**: Detailed test results, measurements, GO/NO-GO decision
- **Architecture Design Document** (M1): Updated with validated CSI-2 parameters
- **FPGA RTL Specification**: CSI-2 TX module detailed design (informed by PoC)

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (manager-spec) | Initial SPEC creation for M0.5 CSI-2 PoC milestone |

---

**END OF SPEC**
