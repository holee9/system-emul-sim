# SPEC-ARCH-001: Acceptance Criteria and Test Scenarios

## Overview

This document defines the acceptance criteria, test scenarios, and quality gates for SPEC-ARCH-001 (P0 Architecture Decisions and Technology Stack Finalization). All scenarios use Given/When/Then format for clarity and traceability.

---

## Test Scenarios

### Scenario 1: P0 Architecture Decisions Documented

**Objective**: Verify that all five P0 architecture decisions are documented with technical rationale and validation.

```gherkin
Given the M0 milestone is reached
When all P0 architecture decisions are documented in SPEC-ARCH-001
Then the SPEC shall contain the following confirmed decisions:
  | Decision Area | Selected Option | Rationale |
  | Performance Tier | Target (2048×2048, 16-bit, 30fps) | Bandwidth 2.01 Gbps within CSI-2 D-PHY limits |
  | Host Link | 10 Gigabit Ethernet | Supports all three performance tiers |
  | SoC Platform | NXP i.MX8M Plus | CSI-2 receiver, Cortex-A53, GPU, GbE |
  | FPGA Interface | CSI-2 MIPI 4-lane D-PHY | Only viable interface within FPGA LUT budget |
  | Technology Stack | Vivado 2023.2+, .NET 8.0, MIPI IP v3.1+ | Version compatibility validated |
And each decision shall include bandwidth calculations, resource estimates, or version validation
And the SPEC shall explicitly reject USB 3.x due to FPGA LUT constraints
```

**Success Criteria**:
- SPEC-ARCH-001 spec.md contains all five decision areas with rationale
- Bandwidth calculations provided for performance tiers (Minimum 0.21 Gbps, Target 2.01 Gbps, Maximum 4.53 Gbps)
- FPGA LUT utilization target documented (<60%, <12,480 LUTs)
- Technology stack version numbers confirmed (Vivado 2023.2+, .NET 8.0 LTS, MIPI CSI-2 TX v3.1+)

**Verification Method**: Document review and technical validation

---

### Scenario 2: Technology Stack Validated

**Objective**: Verify that technology stack versions and compatibility are validated against project requirements.

```gherkin
Given SPEC-ARCH-001 is approved
When technology stack validation is performed
Then FPGA LUT utilization shall be <60% (<12,480 LUTs)
And CSI-2 MIPI D-PHY bandwidth shall support Target tier (2.01 Gbps)
And 10 Gigabit Ethernet bandwidth shall support Maximum tier (4.53 Gbps)
And the following tool versions shall be confirmed:
  | Tool | Version | Validation |
  | AMD Vivado | 2023.2 or later | Artix-7 XC7A35T device support |
  | MIPI CSI-2 TX IP | v3.1 or later | 4-lane D-PHY, 1.0-1.25 Gbps/lane |
  | .NET SDK | 8.0 LTS or later | C# 12, cross-platform support |
  | GCC ARM | 11.x or later | Cortex-A53 cross-compilation |
```

**Success Criteria**:
- All tool versions documented in plan.md Technology Stack Specifications section
- FPGA resource estimate confirms <60% LUT utilization for CSI-2 TX IP
- Bandwidth calculations confirm Target tier feasibility (2.01 Gbps < 4-5 Gbps D-PHY limit)
- Tool installation validated on development workstation

**Verification Method**: Tool installation, version check, bandwidth calculation review

---

### Scenario 3: Procurement BOM Prepared

**Objective**: Verify that Bill of Materials (BOM) is complete and procurement requests are ready for submission.

```gherkin
Given SPEC-ARCH-001 is approved
And technology stack versions are validated
When procurement BOM is prepared
Then the BOM shall include the following hardware items:
  | Item | Description | Quantity | Purpose |
  | FPGA Dev Board | Artix-7 XC7A35T evaluation board | 1 | RTL development, CSI-2 PoC |
  | SoC Eval Board | NXP i.MX8M Plus EVK | 1 | Firmware development, CSI-2 receiver |
  | 10 GbE Switch | 10 Gigabit Ethernet switch | 1 | Host link testing |
  | Network Card | 10 GbE NIC for Host PC | 1 | Host connectivity |
  | Network Cables | Cat6A or fiber cables | 3 | FPGA-SoC-Host connections |
And the BOM shall include the following software/IP licenses:
  | Item | Description | License Type | Cost Estimate |
  | AMD Vivado | FPGA development suite | WebPACK or HL Design | Free or paid |
  | MIPI CSI-2 TX IP | MIPI transmitter IP core | IP license | TBD (AMD pricing) |
  | .NET SDK | C# development SDK | Free | $0 |
  | Visual Studio | IDE for C# development | Community Edition | Free |
And procurement requests shall be submitted with vendor information and lead time estimates
```

**Success Criteria**:
- BOM spreadsheet created with part numbers, vendors, costs, and lead times
- Hardware procurement requests submitted for approval
- IP license requirements identified and procurement initiated
- Total cost estimate within project budget

**Verification Method**: BOM review, procurement request submission confirmation

---

### Scenario 4: FPGA Interface Confirmed (CSI-2 MIPI D-PHY)

**Objective**: Verify that CSI-2 MIPI D-PHY is confirmed as the exclusive FPGA-to-SoC interface and USB 3.x is explicitly rejected.

```gherkin
Given FPGA resource constraints are analyzed
When FPGA interface selection is evaluated
Then CSI-2 MIPI 4-lane D-PHY shall be selected as the high-speed data interface
And USB 3.x shall be explicitly rejected with the following rationale:
  | Interface | LUT Usage | Available LUTs | Feasibility |
  | USB 3.x | 14,976-24,960 (72-120%) | 20,800 | ❌ Exceeds budget |
  | CSI-2 MIPI | <12,480 (<60%) | 20,800 | ✅ Within budget |
And CSI-2 D-PHY bandwidth shall be validated:
  | Configuration | Lanes | Gbps/Lane | Aggregate Gbps | Target Tier Supported |
  | D-PHY 4-lane | 4 | 1.0-1.25 | 4.0-5.0 | ✅ Yes (2.01 Gbps) |
And SPI shall be selected as the control interface (max 50 MHz)
```

**Success Criteria**:
- CSI-2 MIPI D-PHY documented as exclusive high-speed interface in spec.md
- USB 3.x rejection documented with LUT resource analysis
- D-PHY bandwidth calculation confirms Target tier support (2.01 Gbps < 4-5 Gbps)
- SPI control interface documented with timing specifications

**Verification Method**: Resource analysis review, bandwidth calculation validation, interface specification review

---

### Scenario 5: Performance Tier Selection Validated

**Objective**: Verify that performance tier selection is validated with bandwidth calculations and CSI-2 D-PHY feasibility.

```gherkin
Given performance tier requirements are defined
When bandwidth calculations are performed
Then the following performance tiers shall be validated:
  | Tier | Resolution | Bit Depth | FPS | Data Rate (Gbps) | CSI-2 Feasible | Host Link |
  | Minimum | 1024×1024 | 14-bit | 15 | 0.21 | ✅ Yes | 1 GbE or 10 GbE |
  | Target | 2048×2048 | 16-bit | 30 | 2.01 | ✅ Yes | 10 GbE |
  | Maximum | 3072×3072 | 16-bit | 30 | 4.53 | ⚠️ Validation Req | 10 GbE |
And Target tier shall be selected as the primary production goal
And Minimum tier shall be selected as the development baseline
And Maximum tier shall be selected as the reference tier (M0.5 PoC validation required)
And the bandwidth calculation formula shall be documented:
  Data Rate (Gbps) = (Width × Height × Bit Depth × FPS) / 1,000,000,000
```

**Success Criteria**:
- Bandwidth calculations provided for all three tiers with results matching table
- Target tier confirmed as production goal with CSI-2 D-PHY feasibility (2.01 Gbps < 4-5 Gbps)
- Minimum tier confirmed as development baseline
- Maximum tier flagged for M0.5 PoC validation due to D-PHY saturation risk
- 10 GbE recommended for production deployment

**Verification Method**: Bandwidth calculation review, tier selection rationale validation, Host link capacity comparison

---

## Edge Case Testing

### Edge Case 1: Maximum Tier D-PHY Bandwidth Saturation

**Scenario**:
```gherkin
Given Maximum tier is configured (3072×3072, 16-bit, 30fps, 4.53 Gbps)
When CSI-2 D-PHY aggregate bandwidth is measured
Then the data rate shall be compared against practical D-PHY limit (4-5 Gbps)
And if data rate exceeds 4.5 Gbps, the following mitigations shall be applied:
  | Mitigation | Modified Parameter | New Data Rate |
  | Frame rate reduction | 30 fps → 20 fps | 3.02 Gbps |
  | Resolution reduction | 3072×3072 → 2560×2560 | 3.14 Gbps |
  | Bit depth reduction | 16-bit → 14-bit | 3.97 Gbps |
And M0.5 CSI-2 PoC shall validate Maximum tier feasibility with real hardware
And if Maximum tier fails validation, Target tier shall remain as production goal
```

**Expected Outcome**:
- Maximum tier bandwidth risk identified and documented
- Mitigation strategies defined (frame rate scaling, resolution reduction)
- M0.5 PoC milestone includes Maximum tier bandwidth validation
- Project proceeds with Target tier even if Maximum tier fails

**Verification Method**: Bandwidth calculation, PoC validation plan, risk mitigation documentation

---

### Edge Case 2: SoC CSI-2 Receiver Incompatibility

**Scenario**:
```gherkin
Given NXP i.MX8M Plus is selected as SoC platform
When CSI-2 receiver compatibility is tested during M0.5 PoC
And if CSI-2 data integrity errors occur (frame corruption, sync loss, CRC errors)
Then the following troubleshooting steps shall be performed:
  | Step | Action | Expected Result |
  | 1 | Verify D-PHY lane ordering | FPGA TX lane mapping matches SoC RX |
  | 2 | Validate HS/LP transition timing | Timing margins within spec |
  | 3 | Check CSI-2 packet structure | Header, payload, footer correct |
  | 4 | Test at reduced bandwidth | Lower FPS or resolution to isolate issue |
And if incompatibility persists, the following fallback SoC platforms shall be evaluated:
  | Platform | CSI-2 Support | Compatibility Track Record | Lead Time |
  | NVIDIA Jetson Nano | 4-lane D-PHY | Known MIPI compatibility | 2-4 weeks |
  | Raspberry Pi CM4 | 2-lane D-PHY | Limited bandwidth | 1-2 weeks |
And SoC platform migration shall be completed within W7-W8 buffer period
```

**Expected Outcome**:
- CSI-2 compatibility validated during M0.5 PoC (W6)
- Troubleshooting procedure defined for compatibility issues
- Fallback SoC platforms identified with procurement lead times
- Project schedule includes 2-week buffer (W7-W8) for platform migration if needed

**Verification Method**: M0.5 PoC validation, compatibility troubleshooting, fallback platform evaluation

---

### Edge Case 3: FPGA Resource Budget Overrun

**Scenario**:
```gherkin
Given FPGA LUT utilization target is <60% (<12,480 LUTs)
When CSI-2 TX IP, frame buffer, and control logic are integrated
And if LUT utilization exceeds 60% threshold
Then the following optimization steps shall be performed:
  | Optimization | Target Module | Expected Savings |
  | Reduce frame buffer depth | Frame buffer FIFO | ~1,000 LUTs |
  | Optimize pixel packing logic | 14-bit to 16-bit alignment | ~500 LUTs |
  | Simplify control state machine | SPI controller, register file | ~300 LUTs |
  | Defer optional features | Test pattern generator, diagnostics | ~800 LUTs |
And if LUT utilization still exceeds 70% after optimization
Then the project shall consider upgrading to Artix-7 XC7A50T (larger FPGA, 52,160 LUTs)
And procurement request shall be submitted for upgraded FPGA development board
```

**Expected Outcome**:
- FPGA resource monitoring integrated into RTL development workflow
- Optimization steps defined with estimated LUT savings
- FPGA upgrade option identified as last resort (Artix-7 XC7A50T)
- Project proceeds with confidence in resource budget management

**Verification Method**: RTL synthesis reports, LUT utilization analysis, optimization validation

---

### Edge Case 4: Procurement Delays

**Scenario**:
```gherkin
Given hardware procurement requests are submitted after M0 approval
When vendor lead times exceed expected delivery schedule
And if SoC evaluation board delivery is delayed beyond W4
Then the following contingency plans shall be activated:
  | Item Delayed | Contingency Plan | Impact |
  | SoC eval board | Use Raspberry Pi CM4 for initial PoC | Limited to 2-lane CSI-2 |
  | FPGA dev board | Develop RTL using FPGA simulator | M0.5 PoC delayed to W8 |
  | 10 GbE equipment | Develop with 1 GbE (Minimum tier only) | Limited tier testing |
And if delivery delays exceed 2 weeks, project schedule shall be adjusted
And M0.5 PoC milestone shall be rescheduled to W8 (if SoC board delayed)
```

**Expected Outcome**:
- Procurement lead times monitored closely after M0 approval
- Contingency plans defined for critical item delays
- Alternative platforms identified for interim development (Pi CM4, simulator)
- Project schedule flexibility allows 2-week delay buffer

**Verification Method**: Procurement tracking, delivery confirmation, contingency plan activation

---

## Performance Criteria

### Bandwidth Validation

**Criterion**: All performance tier bandwidth calculations shall be validated against CSI-2 D-PHY aggregate bandwidth limits.

**Metrics**:
- Minimum tier: 0.21 Gbps < 4.0 Gbps (✅ Pass)
- Target tier: 2.01 Gbps < 4.0 Gbps (✅ Pass)
- Maximum tier: 4.53 Gbps < 5.0 Gbps (⚠️ Requires M0.5 PoC validation)

**Acceptance Threshold**: Target tier bandwidth ≤ 50% of D-PHY aggregate bandwidth (safety margin)

**Verification Method**: Bandwidth calculation review, PoC measurement validation

---

### FPGA Resource Utilization

**Criterion**: FPGA LUT utilization shall remain below 60% for all target features.

**Metrics**:
- CSI-2 TX IP: ~5,000-8,000 LUTs (estimate from AMD IP documentation)
- Frame buffer: ~2,000-3,000 LUTs (2-frame depth, 2048×2048×16-bit)
- Control logic: ~1,000-1,500 LUTs (SPI controller, register file)
- Total estimate: ~8,000-12,500 LUTs (~38-60% of 20,800 LUTs)

**Acceptance Threshold**: Total LUT utilization ≤ 12,480 LUTs (60%)

**Verification Method**: Vivado synthesis report, resource utilization analysis

---

### Technology Stack Compatibility

**Criterion**: All development tool versions shall be validated for compatibility with target platforms.

**Metrics**:
| Tool | Version | Target Platform | Validation |
|------|---------|----------------|-----------|
| AMD Vivado | 2023.2+ | Artix-7 XC7A35T | Device support confirmed |
| MIPI CSI-2 TX IP | v3.1+ | 4-lane D-PHY | 1.0-1.25 Gbps/lane |
| .NET SDK | 8.0 LTS | Windows/Linux | C# 12, cross-platform |
| GCC ARM | 11.x+ | Cortex-A53 | Cross-compilation support |

**Acceptance Threshold**: 100% of critical tools installed and validated

**Verification Method**: Tool installation, version check, test project compilation

---

### Procurement Completeness

**Criterion**: Bill of Materials (BOM) shall be complete with vendor information and cost estimates.

**Metrics**:
- Hardware items: 5 (FPGA board, SoC board, 10 GbE switch, network card, cables)
- Software/IP licenses: 4 (Vivado, MIPI IP, .NET SDK, Visual Studio)
- Total items documented: 9
- Items with vendor information: 9 (100%)
- Items with cost estimates: 9 (100%)

**Acceptance Threshold**: 100% BOM completeness with procurement requests submitted

**Verification Method**: BOM spreadsheet review, procurement submission confirmation

---

## Quality Gates

### TRUST 5 Framework Compliance

**Tested (T)**:
- Bandwidth calculations validated with CSI-2 D-PHY specification
- FPGA resource estimates validated with AMD MIPI IP documentation
- Tool version compatibility validated with installation and test projects
- Performance tier calculations verified with independent review

**Readable (R)**:
- SPEC-ARCH-001 written in clear, unambiguous language
- Architecture decisions documented with technical rationale
- Bandwidth calculation formula provided with step-by-step examples
- Technology stack specifications organized in structured tables

**Unified (U)**:
- Consistent terminology across SPEC documents (performance tier, D-PHY, LUT)
- Naming conventions aligned with project standards (FPGA, SoC, Host)
- Document structure follows ABYZ-Lab-ADK SPEC template (spec.md, plan.md, acceptance.md)
- Cross-references to project documents (CHEATSHEET.md, detector_config.yaml)

**Secured (S)**:
- No security vulnerabilities introduced by technology choices
- Network security considerations documented (10 GbE, firewall, encryption)
- FPGA configuration security (bitstream encryption, secure boot) deferred to M2
- SoC firmware security (secure boot, trusted execution) deferred to M3

**Trackable (T)**:
- SPEC-ARCH-001 tracked in Git with version history
- All P0 decisions documented with creation date and author
- Architecture changes tracked through formal change control process
- M0 milestone completion tracked in project plan (W1)

---

### Technical Review Approval

**Review Criteria**:
- All P0 decisions documented with technical rationale
- Bandwidth calculations reviewed by FPGA expert
- FPGA resource budget validated against AMD IP documentation
- SoC platform selection reviewed by firmware engineer
- Technology stack versions confirmed by development team

**Reviewers**:
- Technical Lead: Architecture decision review and approval
- FPGA Engineer: Resource budget and CSI-2 interface validation
- Firmware Engineer: SoC platform and firmware stack review
- Software Engineer: Host SDK and .NET stack validation

**Approval Criteria**:
- Zero unresolved technical concerns
- All reviewers sign off on SPEC-ARCH-001
- Risk analysis reviewed and mitigations accepted
- Project manager approves M0 milestone completion

---

### Stakeholder Sign-Off

**Stakeholder Approval Requirements**:

**Project Manager**:
- Performance tier selection aligns with project goals
- M0 milestone timeline met (W1)
- Procurement BOM within budget
- Schedule validated with M0.5 PoC milestone (W6)

**Procurement Team**:
- BOM complete with vendor information and costs
- Hardware items available with acceptable lead times
- IP licenses available with acceptable licensing terms
- Total cost estimate within approved budget

**Development Team**:
- Technology stack versions compatible with existing environment
- Development tools available and validated
- FPGA and SoC platforms suitable for development workflow
- No blocking technical issues identified

**Quality Assurance**:
- TRUST 5 framework compliance validated
- Acceptance criteria clear and measurable
- Test scenarios cover all critical paths
- Risk analysis complete with mitigations defined

**Sign-Off Criteria**:
- All stakeholders approve SPEC-ARCH-001
- M0 milestone declared complete
- Procurement requests submitted and approved
- M0.5 CSI-2 PoC planning initiated

---

## Traceability Matrix

| Requirement ID | Acceptance Criterion | Test Scenario | Quality Gate |
|---------------|---------------------|---------------|--------------|
| REQ-ARCH-001 | CSI-2 MIPI selected | Scenario 4 | Technical Review |
| REQ-ARCH-002 | FPGA LUT <60% | Scenario 2 | Technical Review |
| REQ-ARCH-003 | Minimum tier baseline | Scenario 5 | Stakeholder Sign-Off |
| REQ-ARCH-004 | M0 documentation complete | Scenario 1 | Stakeholder Sign-Off |
| REQ-ARCH-005 | M0.5 PoC planned | Scenario 2 | Technical Review |
| REQ-ARCH-006 | Bandwidth validated | Scenario 5 | Technical Review |
| REQ-ARCH-007 | Target tier D-PHY | Scenario 5 | Technical Review |
| REQ-ARCH-008 | 10 GbE supports all tiers | Scenario 2 | Technical Review |
| REQ-ARCH-009 | 1 GbE Minimum only | Scenario 2 | Technical Review |
| REQ-ARCH-010 | USB 3.x rejected | Scenario 4 | Technical Review |
| REQ-ARCH-011 | D-PHY bandwidth limit | Edge Case 1 | Technical Review |
| REQ-ARCH-012 | No post-M0 changes | Stakeholder Sign-Off | Stakeholder Sign-Off |
| REQ-ARCH-013 | i.MX8M Plus procurement | Scenario 3 | Procurement Team |
| REQ-ARCH-014 | Frame rate scaling | Edge Case 1 | Technical Review |
| REQ-ARCH-015 | Network discovery | Deferred to M3 | N/A |

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | ABYZ-Lab Agent (manager-spec) | Initial acceptance criteria for SPEC-ARCH-001 |

---

**END OF ACCEPTANCE CRITERIA**
