# SPEC-ARCH-001: P0 Architecture Decisions and Technology Stack Finalization

---
id: SPEC-ARCH-001
version: 1.0.0
status: approved
created: 2026-02-17
updated: 2026-02-17
author: MoAI Agent (manager-spec)
priority: critical
---

## HISTORY

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (manager-spec) | Initial SPEC creation for M0 milestone P0 decisions |

---

## Overview

### Project Context

The X-ray Detector Panel System is a 28-week development project (W1-W28) for medical imaging equipment. This system collects, transmits, and processes X-ray detector panel data through a hierarchical architecture consisting of FPGA (data acquisition), SoC (preprocessing and network transmission), and Host PC (final processing and storage).

### M0 Milestone Objective

Milestone M0 (W1) establishes the foundational architecture decisions (P0 decisions) that define the project's technical direction. These decisions are critical and irreversible due to hardware procurement, FPGA resource constraints, and development timeline dependencies.

### P0 Decisions Scope

This SPEC documents the following P0 architecture decisions:

1. **Performance Tier Selection**: Determine target performance level (Minimum, Target, or Maximum)
2. **Host Link Technology**: Select network interface (1 GbE vs 10 GbE)
3. **SoC Platform Selection**: Choose System-on-Chip platform (NXP i.MX8M Plus recommended)
4. **FPGA Interface Confirmation**: Validate CSI-2 MIPI D-PHY as data path
5. **Technology Stack Validation**: Confirm development tools and library versions

---

## Requirements

### 1. Ubiquitous Requirements (System-Wide Invariants)

**REQ-ARCH-001**: The system **shall** use CSI-2 MIPI 4-lane D-PHY as the exclusive high-speed data interface between FPGA and SoC.

**WHY**: Xilinx Artix-7 XC7A35T FPGA resource constraints prohibit USB 3.x implementation (LUT usage 72-120% vs available 20,800 LUTs). CSI-2 is the only viable high-speed interface within resource budget.

**IMPACT**: All data acquisition, buffering, and transmission logic must conform to CSI-2 specification and D-PHY electrical standards.

---

**REQ-ARCH-002**: The FPGA **shall** maintain LUT utilization below 60% (12,480 LUTs) for all target features.

**WHY**: Resource budget preservation enables future features, debugging logic, and timing closure margin.

**IMPACT**: All RTL modules must undergo resource utilization analysis during implementation. Features exceeding budget require decomposition or alternative implementation.

---

**REQ-ARCH-003**: The system **shall** support the Minimum performance tier (1024×1024, 14-bit, 15fps, ~0.21 Gbps) as baseline development target.

**WHY**: Minimum tier provides achievable development baseline within CSI-2 D-PHY bandwidth constraints and simplifies initial testing.

**IMPACT**: All development phases (FPGA, SoC firmware, Host SDK) must demonstrate Minimum tier functionality before advancing to higher tiers.

---

### 2. Event-Driven Requirements (Milestone Gates and PoC Triggers)

**REQ-ARCH-004**: **WHEN** M0 milestone is completed **THEN** technology stack decisions shall be documented and approved.

**WHY**: M0 completion triggers procurement activities and detailed design phase (M1). Technology changes after M0 introduce significant schedule risk.

**IMPACT**: All P0 decisions documented in this SPEC become project constraints for the entire 28-week development cycle.

---

**REQ-ARCH-005**: **WHEN** CSI-2 PoC validation (M0.5, W6) completes successfully **THEN** the system shall advance to full FPGA development (M1-M3).

**WHY**: CSI-2 PoC validates critical path assumptions (D-PHY bandwidth, MIPI IP compatibility, SoC CSI-2 receiver).

**IMPACT**: PoC failure triggers architecture review and potential SoC platform change. Success enables parallel development of FPGA RTL, firmware, and Host SDK.

---

**REQ-ARCH-006**: **WHEN** performance tier selection is finalized **THEN** bandwidth calculations shall be validated against CSI-2 D-PHY limits.

**WHY**: Performance tier determines pixel data rate, which must not exceed CSI-2 4-lane aggregate bandwidth (~4-5 Gbps with Artix-7 OSERDES constraints).

**IMPACT**: Target tier (2048×2048, 16-bit, 30fps, ~2.01 Gbps) is achievable. Maximum tier (3072×3072, 16-bit, 30fps, ~4.53 Gbps) requires validation and may be development reference only.

---

### 3. State-Driven Requirements (Performance Tier Modes)

**REQ-ARCH-007**: **IF** Target tier is selected (2048×2048, 16-bit, 30fps) **THEN** CSI-2 D-PHY shall operate at 1.0-1.25 Gbps/lane with 4 lanes.

**WHY**: Target tier requires ~2.01 Gbps aggregate bandwidth, achievable within CSI-2 D-PHY operational range.

**IMPACT**: MIPI CSI-2 TX Subsystem IP configuration must support 1.0-1.25 Gbps/lane. SoC CSI-2 receiver must support equivalent or higher data rates.

---

**REQ-ARCH-008**: **IF** 10 Gigabit Ethernet is selected as Host link **THEN** the system shall support all three performance tiers (Minimum, Target, Maximum).

**WHY**: 10 GbE provides 10 Gbps bandwidth, sufficient for Maximum tier (4.53 Gbps) with headroom for protocol overhead and future expansion.

**IMPACT**: 10 GbE selection enables full performance tier range. Firmware and Host SDK must implement tier-specific configuration and validation.

---

**REQ-ARCH-009**: **IF** 1 Gigabit Ethernet is selected as Host link **THEN** the system shall support Minimum tier only.

**WHY**: 1 GbE provides ~1 Gbps bandwidth, insufficient for Target tier (2.01 Gbps) or Maximum tier (4.53 Gbps).

**IMPACT**: 1 GbE limits system to Minimum tier, reducing project scope and clinical applicability. Not recommended for production deployment.

---

### 4. Unwanted Requirements (Prohibited Technologies and Actions)

**REQ-ARCH-010**: The system **shall not** use USB 3.x as the FPGA-to-SoC data interface.

**WHY**: USB 3.x IP cores require 72-120% of available FPGA LUTs (14,976-24,960 LUTs vs 20,800 available), exceeding resource budget.

**IMPACT**: Any proposal for USB 3.x interface must be rejected. CSI-2 MIPI is the only viable high-speed interface.

---

**REQ-ARCH-011**: The system **shall not** violate CSI-2 D-PHY bandwidth constraints (4-5 Gbps aggregate for 4-lane Artix-7 OSERDES).

**WHY**: Exceeding D-PHY bandwidth causes data corruption, frame drops, and system instability.

**IMPACT**: Performance tier selection and pixel format configurations must undergo bandwidth validation. Maximum tier may require frame rate reduction or resolution compromise if D-PHY cannot sustain 4.53 Gbps.

---

**REQ-ARCH-012**: The system **shall not** introduce architectural changes after M0 completion without formal review and approval.

**WHY**: Post-M0 architecture changes invalidate procurement decisions, delay schedule, and introduce integration risk.

**IMPACT**: All P0 decisions are locked after M0. Changes require formal change control process with stakeholder approval.

---

### 5. Optional Requirements (Procurement and Development Enhancements)

**REQ-ARCH-013**: **Where possible**, the system should procure NXP i.MX8M Plus evaluation board for SoC platform validation.

**WHY**: i.MX8M Plus provides CSI-2 receiver, Cortex-A53 quad-core processor, GPU, and Gigabit Ethernet, suitable for firmware development and CSI-2 PoC.

**IMPACT**: Availability and cost determine procurement feasibility. Alternative SoC platforms (e.g., Raspberry Pi CM4, NVIDIA Jetson Nano) may be considered if i.MX8M Plus is unavailable.

---

**REQ-ARCH-014**: **Where possible**, the system should implement frame rate scaling to maximize CSI-2 D-PHY bandwidth utilization.

**WHY**: Frame rate adjustment enables testing at various bandwidth levels and provides fallback if Maximum tier exceeds D-PHY limits.

**IMPACT**: FPGA logic includes configurable frame timing generator. Firmware provides frame rate control API.

---

**REQ-ARCH-015**: **Where possible**, Host SDK should support network discovery and automatic device configuration.

**WHY**: Automatic discovery simplifies deployment and reduces configuration errors.

**IMPACT**: SDK implements UDP broadcast discovery, device enumeration, and automatic IP configuration. Implementation priority is medium.

---

## Technical Constraints

### FPGA Hardware Constraints

**FPGA Device**: Xilinx Artix-7 XC7A35T-FGG484

**Resource Limits**:
- Logic Cells: 33,280
- LUTs: 20,800 (target utilization <60% = 12,480 LUTs)
- Flip-Flops: 41,600
- Block RAM: 50 (1.8 Mbit total)
- DSP Slices: 90

**Interface Constraints**:
- High-Speed I/O: CSI-2 MIPI D-PHY via OSERDES (max ~1.0-1.25 Gbps/lane)
- Control Interface: SPI (max 50 MHz)
- No USB 3.x support due to LUT constraints

---

### Bandwidth Calculations

**CSI-2 D-PHY Aggregate Bandwidth** (4-lane):
- Per-lane: 1.0-1.25 Gbps
- Aggregate: 4.0-5.0 Gbps
- Practical limit (protocol overhead): ~4-5 Gbps

**Performance Tier Bandwidth Requirements**:

| Tier | Resolution | Bit Depth | FPS | Data Rate (Gbps) | CSI-2 Feasible |
|------|-----------|-----------|-----|------------------|----------------|
| Minimum | 1024×1024 | 14-bit | 15 | 0.21 | ✅ Yes |
| Target | 2048×2048 | 16-bit | 30 | 2.01 | ✅ Yes |
| Maximum | 3072×3072 | 16-bit | 30 | 4.53 | ⚠️ Validation Required |

**10 GbE Host Link Bandwidth**: 10 Gbps (supports all tiers)

**1 GbE Host Link Bandwidth**: 1 Gbps (Minimum tier only)

---

### Technology Stack Constraints

**Development Tools**:
- FPGA: AMD Vivado 2023.2 or later (Artix-7 support)
- Firmware: GCC ARM cross-compiler (for Cortex-A53 SoC)
- Host SDK: .NET 8.0 LTS or later (C# SDK and GUI)
- Simulator: .NET 8.0 LTS (C# FPGA emulator)

**Key IP and Libraries**:
- MIPI CSI-2 TX Subsystem: AMD MIPI CSI-2 TX Subsystem IP v3.1 or later
- Network Stack: lwIP or similar (for SoC firmware)
- Configuration: `detector_config.yaml` as single source of truth

---

## Acceptance Criteria

### Success Criteria for M0 Completion

**AC-001**: Performance tier selection documented with bandwidth validation
- **GIVEN**: Three performance tiers (Minimum, Target, Maximum)
- **WHEN**: Bandwidth calculations are performed for each tier
- **THEN**: Target tier shall be selected, with Minimum as baseline and Maximum as reference
- **AND**: Bandwidth validation confirms Target tier feasibility within CSI-2 D-PHY limits

---

**AC-002**: Host link technology selection documented with tier support mapping
- **GIVEN**: 10 GbE and 1 GbE options
- **WHEN**: Bandwidth requirements for each tier are compared against link capacity
- **THEN**: 10 GbE shall be recommended for production deployment
- **AND**: 1 GbE shall be noted as Minimum tier only with limited applicability

---

**AC-003**: SoC platform selection documented with CSI-2 receiver validation
- **GIVEN**: NXP i.MX8M Plus and alternative platforms
- **WHEN**: CSI-2 receiver compatibility and processing capability are evaluated
- **THEN**: i.MX8M Plus shall be recommended for procurement
- **AND**: Alternative platforms (Raspberry Pi CM4, Jetson Nano) documented as fallback options

---

**AC-004**: FPGA interface confirmation with resource budget validation
- **GIVEN**: CSI-2 MIPI D-PHY as sole high-speed interface
- **WHEN**: FPGA LUT utilization is estimated for CSI-2 TX IP
- **THEN**: CSI-2 implementation shall fit within 60% LUT budget (<12,480 LUTs)
- **AND**: USB 3.x shall be explicitly rejected due to resource constraints

---

**AC-005**: Technology stack versions documented with procurement BOM
- **GIVEN**: Development tools, MIPI IP, and libraries
- **WHEN**: Version numbers and compatibility are verified
- **THEN**: All versions shall be documented in SPEC-ARCH-001 plan.md
- **AND**: Bill of Materials (BOM) shall include FPGA dev board, SoC eval board, and network equipment

---

### Quality Gates

**QG-001**: TRUST 5 Framework Compliance
- **Tested**: Bandwidth calculations validated with CSI-2 D-PHY specification
- **Readable**: Architecture decisions documented with clear rationale
- **Unified**: Consistent terminology across SPEC documents
- **Secured**: No security vulnerabilities introduced by technology choices
- **Trackable**: SPEC-ARCH-001 tracked in Git with version history

---

**QG-002**: Technical Review Approval
- All P0 decisions reviewed by technical lead
- FPGA resource budget validated against MIPI IP documentation
- Bandwidth calculations peer-reviewed
- Risk assessment completed for Maximum tier D-PHY saturation

---

**QG-003**: Stakeholder Sign-Off
- Project manager approves performance tier selection
- Procurement team confirms hardware availability and budget
- Development team confirms tool version compatibility
- Schedule validated with M0.5 CSI-2 PoC milestone

---

## Dependencies

### External Dependencies

**Procurement Dependencies**:
- FPGA Development Board: Artix-7 XC7A35T evaluation board
- SoC Evaluation Board: NXP i.MX8M Plus EVK or equivalent
- Network Equipment: 10 GbE switch or direct connection adapter

**Vendor Dependencies**:
- AMD MIPI CSI-2 TX Subsystem IP license and documentation
- NXP i.MX8M Plus technical documentation and BSP
- Vivado 2023.2+ license and installation

---

### Internal Dependencies

**Project Documentation**:
- `detector_config.yaml`: Configuration schema definition
- `CHEATSHEET.md`: Quick reference for project constraints
- `QUICKSTART.md`: Development environment setup guide
- `X-ray_Detector_Optimal_Project_Plan.md`: Detailed project schedule

**Repository Structure** (Gitea):
- `fpga/`: RTL, testbench, constraints
- `fw/`: SoC firmware (C/C++)
- `sdk/`: Host SDK (C++/C#)
- `tools/`: Simulator, GUI, code generators
- `config/`: `detector_config.yaml`, schemas, converters
- `docs/`: Architecture documents, API documentation

---

### Milestone Dependencies

**M0 → M0.5**: CSI-2 PoC validation depends on SoC platform procurement and MIPI IP integration
**M0.5 → M1**: Full FPGA development blocked until PoC confirms CSI-2 feasibility
**M1 → M2**: Firmware development requires SoC platform availability and FPGA CSI-2 TX operational
**M2 → M3**: Host SDK integration requires firmware network stack and 10 GbE infrastructure

---

## Traceability

This SPEC aligns with the following project documents:

- **Project Plan**: `X-ray_Detector_Optimal_Project_Plan.md` (M0 milestone, W1)
- **Architecture Design**: `README.md` (System architecture overview)
- **Configuration**: `detector_config.yaml` (Performance tier definitions)
- **Constraints**: `CHEATSHEET.md` (FPGA resource limits, interface decisions)
- **Risk Management**: `docs/references/risk-management.md` (D-PHY bandwidth risk, SoC compatibility risk)

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (manager-spec) | Initial SPEC creation for M0 milestone P0 decisions |

---

**END OF SPEC**
