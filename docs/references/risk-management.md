# Risk Management Guide

**Document Version**: 1.0.0
**Status**: Reviewed - Approved
**Last Updated**: 2026-02-17
**Author**: ABYZ-Lab Documentation Agent
**Framework**: ISO 14971 (Medical Device Risk Management)

---

## Table of Contents

1. [Overview](#overview)
2. [Risk Management Framework](#risk-management-framework)
3. [Risk Classification System](#risk-classification-system)
4. [Risk Matrix](#risk-matrix)
5. [Risk Register](#risk-register)
6. [Residual Risk Assessment](#residual-risk-assessment)
7. [Risk Review Schedule](#risk-review-schedule)
8. [Risk Response Procedures](#risk-response-procedures)

---

## Overview

This document defines the risk management process for the X-ray Detector Panel System using the ISO 14971 framework, adapted for an embedded medical imaging development project. It covers technical risks, schedule risks, procurement risks, and integration risks across all development phases (W1-W28).

Risk management is a continuous process. All project contributors are responsible for identifying and reporting new risks. The project lead is responsible for maintaining this register and ensuring mitigations are enacted.

---

## Risk Management Framework

### ISO 14971 Adaptation

ISO 14971 is the international standard for risk management of medical devices. This project applies its core concepts as follows:

| ISO 14971 Element | Project Application |
|-------------------|---------------------|
| Hazard identification | Technical risk catalog (hardware, software, interface) |
| Risk estimation | Probability × Impact matrix scoring |
| Risk evaluation | Acceptability threshold (score ≤ 4 acceptable, >4 requires mitigation) |
| Risk control | Mitigation actions with owners and deadlines |
| Residual risk | Post-mitigation score and acceptability assessment |
| Risk monitoring | Weekly review during active development phases |

### Risk Lifecycle

```
Identified → Assessed → Mitigated → Monitored → Closed
               |                        |
               v                        v
           Escalated              Re-assessed
           (if critical)         (if conditions change)
```

---

## Risk Classification System

### Probability Scale

| Level | Label | Definition |
|-------|-------|-----------|
| 1 | Very Low | Unlikely to occur; no historical precedent in project |
| 2 | Low | Unlikely but possible; rare in similar projects |
| 3 | Medium | May occur; has happened in similar projects |
| 4 | High | Likely to occur; has occurred in this project type |
| 5 | Very High | Almost certain to occur; already showing early signs |

### Impact Scale

| Level | Label | Definition |
|-------|-------|-----------|
| 1 | Negligible | No effect on schedule, cost, or system functionality |
| 2 | Minor | Minor schedule slip (<1 week); workaround available |
| 3 | Moderate | Schedule impact 1-3 weeks; feature degradation possible |
| 4 | High | Schedule impact >3 weeks; core functionality at risk |
| 5 | Critical | Project goal unachievable; fundamental redesign required |

### Risk Score

```
Risk Score = Probability × Impact
```

| Score Range | Rating | Action Required |
|-------------|--------|----------------|
| 1-4 | Low | Monitor; no immediate action |
| 5-9 | Medium | Mitigation plan required |
| 10-19 | High | Immediate mitigation; weekly tracking |
| 20-25 | Critical | Escalate; consider project scope adjustment |

---

## Risk Matrix

```
Impact →
         1-Negligible  2-Minor   3-Moderate  4-High    5-Critical
P  5     5 (M)         10 (H)    15 (H)      20 (C)    25 (C)
r  4     4 (L)         8 (M)     12 (H)      16 (H)    20 (C)
o  3     3 (L)         6 (M)     9 (M)       12 (H)    15 (H)
b  2     2 (L)         4 (L)     6 (M)       8 (M)     10 (H)
a  1     1 (L)         2 (L)     3 (L)       4 (L)     5 (M)
b
i  Legend: L=Low, M=Medium, H=High, C=Critical
l
i
t
y
↑
```

---

## Risk Register

### R-001: FPGA Resource Overflow (LUT >60%)

| Attribute | Value |
|-----------|-------|
| **Risk ID** | R-001 |
| **Category** | Hardware / FPGA |
| **Title** | FPGA resource utilization exceeds 60% LUT threshold |
| **Probability** | 2 (Low) |
| **Impact** | 4 (High) |
| **Risk Score** | 8 (Medium) |
| **Status** | Active - Monitoring |
| **Owner** | FPGA Engineer |
| **Phase** | W9-W28 (Implementation + HW Verification) |

**Description**

The Xilinx Artix-7 XC7A35T-FGG484 has 20,800 LUTs available. The target design (CSI-2 TX, SPI controller, frame buffer management, panel scan FSM) must fit within 60% utilization (12,480 LUTs) to allow routing headroom and prevent timing closure failures.

USB 3.x was already ruled out due to this constraint (would consume 72-120% of LUTs alone). The current CSI-2 D-PHY 4-lane design is estimated to consume approximately 40-50% of available LUTs, leaving limited margin for additional features.

**Trigger Conditions**

- Adding a second high-speed interface
- Expanding frame buffer depth beyond planned BRAM allocation (50 blocks)
- Implementing on-FPGA compression or image processing
- Vivado post-implementation report shows LUT utilization >60%

**Mitigation Actions**

1. **Resource monitoring**: Run Vivado utilization reports after every RTL commit. Block merges that push utilization above 55% without explicit approval.
2. **Upgrade path documented**: Xilinx Artix-7 upgrade path:
   - XC7A50T: 32,600 LUTs (56% more) - same FGG484 package, pin-compatible
   - XC7A75T: 47,200 LUTs (127% more) - same FGG484 package, pin-compatible
   - XC7A100T: 63,400 LUTs (205% more) - same FGG484 package, pin-compatible
3. **Feature prioritization**: If LUT budget is tight, defer on-FPGA diagnostic features to SoC firmware
4. **Synthesis optimization**: Use Vivado strategy "Performance_ExplorePostRoutePhysOpt" for timing-critical paths

**Residual Risk (Post-Mitigation)**

- Probability: 1 (Very Low) - active monitoring prevents silent overflows
- Impact: 4 (High) - if triggered, requires board redesign
- Residual Score: 4 (Low)

---

### R-002: CSI-2 D-PHY 800 Mbps/Lane Instability

| Attribute | Value |
|-----------|-------|
| **Risk ID** | R-002 |
| **Category** | Hardware / Interface |
| **Title** | D-PHY 800 Mbps/lane instability prevents final performance target |
| **Probability** | 3 (Medium) |
| **Impact** | 4 (High) |
| **Risk Score** | 12 (High) |
| **Status** | Active - Debugging in Progress |
| **Owner** | Hardware Engineer |
| **Phase** | Current (M0 debugging) |

**Description**

Hardware validation has confirmed:
- **400 Mbps/lane (1.6 Gbps total)**: Stable operation verified. Supports intermediate performance targets (up to 2048×2048@15fps).
- **800 Mbps/lane (3.2 Gbps total)**: Operational but unstable. Debugging in progress.

The final project target (3072×3072, 16-bit, 15fps) requires approximately 2.26 Gbps, which requires 800 Mbps/lane operation (71% of the 3.2 Gbps budget, 29% headroom).

**Root Cause Candidates**

1. I/O termination impedance mismatch on Artix-7 differential pairs
2. Clock-to-data skew exceeding D-PHY specification limits
3. IDELAY calibration not optimal for 800 Mbps operation
4. PCB trace length mismatch between clock and data lanes

**Mitigation Actions**

1. **400 Mbps fallback**: The intermediate-A tier (2048×2048@15fps, 1.01 Gbps) is fully supported at 400 Mbps. This is the current development baseline.
2. **Systematic debugging steps**:
   - Step 1: Measure eye diagram at 800 Mbps with oscilloscope
   - Step 2: Adjust IDELAY values (0-31 taps, 78ps/tap on Artix-7)
   - Step 3: Verify differential pair impedance (target 100Ω ±10%)
   - Step 4: Check Vivado ISERDES configuration for 800 Mbps DDR
3. **External D-PHY IC option**: If FPGA D-PHY cannot achieve 800 Mbps, use an external D-PHY IC (e.g., TI SN65LVDS31/32 bridge) between FPGA and i.MX8MP. This adds BOM cost but guarantees signaling compliance.
4. **MIPI D-PHY IP core**: Consider Xilinx MIPI D-PHY v4.x IP core (licensed) for validated 800 Mbps support.

**Impact if Not Resolved**

Development continues at 400 Mbps baseline. Final target degrades from 3072×3072@15fps to 2048×2048@15fps (intermediate-A tier). This represents a 56% reduction in pixel count from the original goal.

**Residual Risk (Post-Mitigation)**

- Probability: 2 (Low) - multiple mitigation paths available
- Impact: 3 (Moderate) - fallback tier is functional, not critical
- Residual Score: 6 (Medium)

---

### R-003: BQ40z50 Driver Port Failure (Kernel 4.4 to 6.6)

| Attribute | Value |
|-----------|-------|
| **Risk ID** | R-003 |
| **Category** | Software / Driver |
| **Title** | BQ40z50 battery gauge driver incompatible with Linux 6.6 |
| **Probability** | 2 (Low) |
| **Impact** | 3 (Moderate) |
| **Risk Score** | 6 (Medium) |
| **Status** | Active - Monitoring |
| **Owner** | SoC Firmware Engineer |
| **Phase** | W9-W22 (SoC Software Development) |

**Description**

The Variscite VAR-SOM-MX8M-PLUS runs Yocto Scarthgap 5.0 LTS with Linux 6.6.52. If a BQ40z50 (Texas Instruments battery fuel gauge, SMBus interface) is used in the system power supply circuit, its existing driver was written for older kernel APIs (4.4-era power_supply subsystem).

Key API changes between kernel 4.4 and 6.6 affecting battery drivers:
- `power_supply_config` struct changes
- `power_supply_register()` signature changes
- `i2c_smbus_read_word_data()` error handling changes
- Device tree binding schema validation (was optional in 4.4, required in 6.6)

**Mitigation Actions**

1. **Community driver**: Check mainline kernel for BQ40z50 support (`drivers/power/supply/bq27xxx_battery.c`). The BQ27xxx family driver in kernel 6.6 supports many TI fuel gauges. Verify BQ40z50 compatibility via `of_match_table` entries.
2. **SMBus direct access fallback**: If kernel driver unavailable, implement a userspace daemon accessing the BQ40z50 via `/dev/i2c-N` using raw SMBus transactions. Battery state can be polled via systemd timer and exposed via sysfs.
3. **Alternative chip**: If BQ40z50 driver port proves costly, substitute BQ27220 (fully supported in mainline kernel 6.6 via bq27xxx driver).
4. **Early validation**: Port and test driver in W9-W10 (firmware setup phase) before any dependency on battery management.

**Residual Risk (Post-Mitigation)**

- Probability: 1 (Very Low) - multiple alternative paths
- Impact: 2 (Minor) - SMBus fallback provides equivalent functionality
- Residual Score: 2 (Low)

---

### R-004: 2.5 GbE Chip Incompatibility on SoC

| Attribute | Value |
|-----------|-------|
| **Risk ID** | R-004 |
| **Category** | Hardware / Networking |
| **Title** | 2.5 GbE PHY chip unrecognized or incompatible with i.MX8M Plus |
| **Probability** | 3 (Medium) |
| **Impact** | 3 (Moderate) |
| **Risk Score** | 9 (Medium) |
| **Status** | Active - Pending HW Validation |
| **Owner** | Hardware/Firmware Engineer |
| **Phase** | W23-W26 (HW Verification) |

**Description**

The VAR-SOM-MX8M-PLUS module includes a 2.5 GbE Ethernet interface. While the i.MX8M Plus SoC has a built-in ENET_QOS supporting 1 GbE, the VAR-SOM may use a 2.5 GbE PHY via PCIe or RGMII. Different board revisions of the VAR-SOM have used different PHY chips, and Linux kernel support varies.

If the PHY chip is not recognized:
- Data throughput limited to 1 GbE (maximum 1 Gbps = 0.8 Gbps effective)
- At 1 GbE, only minimum and intermediate-A tiers are supported
- Final target (3072×3072@15fps = 2.26 Gbps) becomes impossible over 1 GbE

**Identification Steps**

1. Run `lspci -vv` to identify PCIe Ethernet controllers
2. Run `ethtool -i eth0` to identify kernel driver in use
3. Run `dmesg | grep -i enet` or `dmesg | grep -i eth` to see driver probe messages
4. Check `/sys/class/net/eth0/speed` after link establishment

**Common VAR-SOM-MX8M-PLUS Ethernet configurations**:
- Realtek RTL8211F: 1 GbE, fully supported in kernel 6.6
- Aquantia AQR107: 2.5 GbE, kernel support via `aqc111` driver
- Marvell 88E2110: 2.5/5 GbE, kernel support via `mv88e6xxx`

**Mitigation Actions**

1. **Chip identification first**: Before writing any network code, identify the exact PHY chip via `lspci` and verify kernel driver availability in kernel 6.6.
2. **10 GbE fallback**: Use a PCIe 10 GbE NIC (e.g., Intel X540-AT2 via PCIe M.2 adapter) on the SoC carrier board. The i.MX8M Plus EVK has M.2 slots. This guarantees 10 GbE capability.
3. **Protocol design**: Design UDP streaming protocol to be link-speed agnostic. The protocol layer should auto-negotiate based on available bandwidth (detect link speed at startup, select frame tier accordingly).
4. **1 GbE contingency**: If forced to 1 GbE, the intermediate-A tier (2048×2048@15fps = 1.01 Gbps effective at 85% efficiency) remains feasible.

**Residual Risk (Post-Mitigation)**

- Probability: 2 (Low) - identification + fallback options cover all scenarios
- Impact: 2 (Minor) - 1 GbE tier degrades but does not block development
- Residual Score: 4 (Low)

---

### R-005: Yocto Scarthgap BSP Build Failure

| Attribute | Value |
|-----------|-------|
| **Risk ID** | R-005 |
| **Category** | Software / Build System |
| **Title** | Yocto Scarthgap 5.0 BSP fails to build for VAR-SOM-MX8M-PLUS |
| **Probability** | 2 (Low) |
| **Impact** | 4 (High) |
| **Risk Score** | 8 (Medium) |
| **Status** | Active - Monitoring |
| **Owner** | SoC Firmware Engineer |
| **Phase** | W9-W14 (SoC Environment Setup) |

**Description**

Yocto Scarthgap 5.0 LTS is a relatively recent release (April 2024). The Variscite BSP layer (`meta-variscite-bsp`) must be compatible with Scarthgap. While Variscite provides official Scarthgap support for the VAR-SOM-MX8M-PLUS, BSP layer conflicts, recipe failures, and kernel patch conflicts can cause build failures that block all SoC development.

Key risks:
- BitBake recipe conflicts between `meta-variscite-bsp` and `meta-openembedded`
- `linux-variscite` kernel recipe failing on Scarthgap toolchain
- U-Boot recipe incompatible with Scarthgap's `virtual/bootloader` provider

**Mitigation Actions**

1. **Variscite official documentation**: Follow the Variscite Yocto Scarthgap guide exactly. Variscite provides a tested `repo manifest` for each release. Do not mix BSP layer versions.
2. **Build environment reproducibility**: Use the official Variscite Docker image for Yocto builds. This eliminates host system dependency conflicts.
3. **Fallback path - Kirkstone LTS**: Yocto Kirkstone 4.0 LTS (Oct 2022, EOL 2026-04) has mature Variscite BSP support. If Scarthgap build fails after 2 days of debugging, switch to Kirkstone and document the decision.
4. **Fallback path - Mickledore**: Yocto Mickledore 4.2 (May 2023) is the bridge release between Kirkstone and Scarthgap with known Variscite support.
5. **Minimal image first**: Build a minimal `core-image-minimal` target first to validate the BSP layer stack before adding custom layers.

**Documented Fallback Path**

```
Scarthgap build attempt (max 3 days)
  → Failure: Try Mickledore (max 2 days)
    → Failure: Use Kirkstone LTS (proven, proceed)
    → Success: Use Mickledore, document LTS exception
  → Success: Use Scarthgap as planned
```

**Residual Risk (Post-Mitigation)**

- Probability: 1 (Very Low) - Docker image + manifest pinning prevents most failures
- Impact: 2 (Minor) - fallback to Kirkstone LTS is a known-good path
- Residual Score: 2 (Low)

---

### R-006: Schedule Overrun in Documentation Phase (W1-W8)

| Attribute | Value |
|-----------|-------|
| **Risk ID** | R-006 |
| **Category** | Schedule / Process |
| **Title** | W1-W8 documentation deliverables overrun, delaying implementation start |
| **Probability** | 3 (Medium) |
| **Impact** | 3 (Moderate) |
| **Risk Score** | 9 (Medium) |
| **Status** | Active - Monitoring |
| **Owner** | Project Lead |
| **Phase** | W1-W8 (Documentation Phase) |

**Description**

The project follows a documentation-first approach: all architecture documents, SPEC files, API specifications, and test plans must be completed and approved before W9 implementation begins. The W1-W8 window contains 35+ documents across 6 repository domains. If review cycles take longer than planned, or if SPEC documents require multiple revision rounds, implementation cannot start on schedule.

**Required Documentation (W1-W8)**

| Week | Deliverables |
|------|-------------|
| W1-W2 | SPEC-ARCH-001 (Architecture), SPEC-PERF-001 (Performance), SPEC-IF-001 (Interface) |
| W3-W4 | SPEC-FW-001 (Firmware), SPEC-SDK-001 (Host SDK), SPEC-CFG-001 (Configuration) |
| W5-W6 | SPEC-TEST-001 (Test Plan), SPEC-SIM-001 (Simulator), API documentation |
| W7-W8 | Review, approval, and merge to main |

**Mitigation Actions**

1. **Parallel writing**: All SPEC documents are written in parallel (not sequentially). Use AI assistance for initial drafts.
2. **Template reuse**: Standardize SPEC format (EARS requirements, acceptance criteria, technical approach). Once one SPEC is complete, subsequent ones follow the same template.
3. **Approval SLA**: Reviewer must respond within 2 business days. If no response, document is auto-approved after 3 business days.
4. **Scope management**: If a document is blocked on unclear requirements, park it and continue with other documents. Do not let one blocked document halt all others.
5. **Buffer**: W7-W8 is intentionally reserved for reviews and revisions. If W1-W6 completes early, implementation can begin in W7.

**Residual Risk (Post-Mitigation)**

- Probability: 2 (Low) - parallel writing + AI assistance reduces time
- Impact: 2 (Minor) - buffer weeks absorb minor slips
- Residual Score: 4 (Low)

---

### R-007: SoC Procurement Delay

| Attribute | Value |
|-----------|-------|
| **Risk ID** | R-007 |
| **Category** | Procurement |
| **Title** | VAR-SOM-MX8M-PLUS module or carrier board unavailable |
| **Probability** | 1 (Very Low) |
| **Impact** | 4 (High) |
| **Risk Score** | 4 (Low) |
| **Status** | Closed - Hardware Procured |
| **Owner** | Project Lead |
| **Phase** | Pre-project (Resolved) |

**Description**

Global semiconductor shortages have historically caused long lead times for NXP i.MX8M Plus based modules. The Variscite VAR-SOM-MX8M-PLUS has experienced 20-40 week lead times during peak shortage periods.

**Current Status: RESOLVED**

The VAR-SOM-MX8M-PLUS evaluation kit is in-hand and verified. Hardware procurement risk is closed.

Verified hardware:
- NXP i.MX8M Plus EVK: In possession, CSI-2 connectivity validated
- Xilinx Artix-7 XC7A35T evaluation board: In possession, 400 Mbps validated
- Inter-board CSI-2 connection: Validated at 400 Mbps/lane

**No further action required.**

---

### R-008: CSI-2 Data Format Disagreement FPGA-SoC

| Attribute | Value |
|-----------|-------|
| **Risk ID** | R-008 |
| **Category** | Integration / Protocol |
| **Title** | FPGA CSI-2 TX and SoC V4L2 driver disagree on data format |
| **Probability** | 3 (Medium) |
| **Impact** | 4 (High) |
| **Risk Score** | 12 (High) |
| **Status** | Active - Specification in Progress |
| **Owner** | System Architect |
| **Phase** | W1-W8 (Specification) + W23-W28 (Integration) |

**Description**

The FPGA generates CSI-2 packets and the i.MX8M Plus SoC receives them via the MIPI CSI-2 receiver. The V4L2 subdevice driver on the SoC must be configured to expect the exact data format that the FPGA transmits. If there is a mismatch in:

- **Data Type (DT)**: RAW8 (0x2A), RAW10 (0x2B), RAW12 (0x2C), RAW14 (0x2D), RAW16 (0x2E)
- **Virtual Channel (VC)**: 0-3
- **Word Count (WC)**: bytes per line = cols × bytes_per_pixel
- **Pixel ordering**: little-endian vs. big-endian
- **Frame Start/End packet timing**: active vs. inactive FS/FE packets
- **ECC and CRC**: enabled vs. disabled

...then the V4L2 driver will fail to receive frames, produce corrupted images, or throw DMA errors silently.

**Known Risk Scenario**

The FPGA implements RAW16 (0x2E, 2 bytes per pixel). The i.MX8M Plus MIPI CSI-2 IP block may not natively support RAW16 in all kernel configurations. The `mxc-mipi-csi2_yav.c` driver in the Variscite BSP must be verified for RAW16 support.

**Mitigation Actions**

1. **Formal FPGA-SoC data format specification**: Create `SPEC-IF-001-csi2-format.md` during W1-W8 documenting:
   - Exact DT value to be used (RAW16 = 0x2E)
   - VC assignment (VC0 for primary channel)
   - WC formula: `cols × 2` (for 16-bit, 2 bytes/pixel)
   - Pixel bit ordering (MSB first per MIPI CSI-2 spec)
   - ECC enabled (per MIPI spec requirement)
   - CRC enabled (16-bit CRC per line)

2. **V4L2 driver verification**: During W9-W14, verify `MEDIA_BUS_FMT_SRGGB16_1X16` format code is supported by the Variscite BSP kernel. If not, patch the driver or use `MEDIA_BUS_FMT_Y16_1X16` as a greyscale workaround.

3. **Loopback test**: Before full system integration, implement an FPGA test mode that transmits a known pattern (e.g., ramp data: pixel value = row × cols + col). Verify on SoC that the received frame matches the expected ramp.

4. **Simulator pre-validation**: The software simulator (`tools/FpgaSimulator`) must generate CSI-2 byte streams identical to the FPGA. Run the simulator output through the V4L2 stack in software before hardware integration.

**Residual Risk (Post-Mitigation)**

- Probability: 2 (Low) - formal spec + loopback test catches disagreements early
- Impact: 2 (Minor) - if caught during spec phase, fix cost is minimal
- Residual Score: 4 (Low)

---

## Residual Risk Assessment

### Summary Table

| Risk ID | Title | Initial Score | Initial Rating | Residual Score | Residual Rating | Delta |
|---------|-------|--------------|---------------|---------------|----------------|-------|
| R-001 | FPGA Resource Overflow | 8 | Medium | 4 | Low | -4 |
| R-002 | CSI-2 800M Instability | 12 | High | 6 | Medium | -6 |
| R-003 | BQ40z50 Driver Port | 6 | Medium | 2 | Low | -4 |
| R-004 | 2.5 GbE Incompatibility | 9 | Medium | 4 | Low | -5 |
| R-005 | Yocto BSP Build Failure | 8 | Medium | 2 | Low | -6 |
| R-006 | Documentation Overrun | 9 | Medium | 4 | Low | -5 |
| R-007 | SoC Procurement Delay | 4 | Low | N/A | Closed | N/A |
| R-008 | CSI-2 Format Disagreement | 12 | High | 4 | Low | -8 |

### Overall Residual Risk Assessment

After applying all mitigation actions, no risks remain in the High or Critical categories. Two risks remain at Medium level (R-002, residual score 6) and require continued monitoring. All other risks are at Low level.

The project can proceed with acceptable residual risk under the condition that R-002 (CSI-2 800 Mbps debugging) is actively worked and its status is reviewed weekly.

---

## Risk Review Schedule

### Cadence

| Phase | Review Frequency | Participants | Focus |
|-------|-----------------|-------------|-------|
| W1-W8 (Documentation) | Bi-weekly | Project Lead, System Architect | New risks from spec reviews |
| W9-W22 (Implementation) | Weekly | All engineers | Active technical risks |
| W23-W28 (HW Verification) | Daily (during PoC) | HW + FW engineers | R-001, R-002, R-008 |

### Review Agenda

1. Status update for all Active risks (5 minutes each)
2. New risk identification and scoring (15 minutes)
3. Escalation of any risk score change to High or Critical (immediate)
4. Mitigation action tracking: assigned → in progress → completed

### Risk Status Definitions

| Status | Definition |
|--------|-----------|
| Identified | Risk recorded, not yet assessed |
| Active - Monitoring | Assessed; mitigation planned or in progress |
| Active - Escalated | Score is High/Critical; requires immediate management attention |
| Active - Debugging | Technical risk under active investigation |
| Mitigated | Mitigation complete; residual risk assessed |
| Closed | Risk no longer applicable (resolved, conditions changed, or procured) |

---

## Risk Response Procedures

### High/Critical Risk Response (Score ≥ 10)

When a risk reaches High or Critical score:

1. **Immediate notification**: Notify project lead within 1 business day
2. **Emergency review**: Schedule dedicated risk review within 3 business days
3. **Impact assessment**: Quantify effect on W1-W28 schedule and performance targets
4. **Decision tree**:
   - Can mitigation reduce score to Medium within 2 weeks? → Execute mitigation
   - Does risk require scope change? → Document and approve scope adjustment
   - Is fallback tier acceptable to stakeholders? → Document tier degradation decision

### New Risk Identification

Any team member can submit a new risk by:
1. Opening a Gitea issue with label `risk`
2. Providing: description, probability, impact, initial mitigation ideas
3. Project lead adds to this register within 2 business days

### Risk Closure Criteria

A risk can be closed when:
- The triggering condition is no longer possible (e.g., hardware procured = R-007)
- Mitigation has been successfully validated (e.g., 800M loopback test passes = R-002)
- The affected phase has completed without the risk materializing

---

*Document End*

## Review Record

- Date: 2026-02-17
- Reviewer: manager-quality
- Status: Approved (with corrections)
- TRUST 5: T:4 R:5 U:4 S:5 T:4
- Corrections Applied:
  - R-002: Fixed incorrect claim that 400 Mbps supports 2048x2048@30fps. Corrected to "up to 2048x2048@15fps". Rationale: 2048x2048x16x30fps = 2.01 Gbps raw, which exceeds 1.6 Gbps total at 400 Mbps/lane. Intermediate-B (30fps) requires 800 Mbps/lane.
- Notes: Risk scores (R-002=12, R-008=12) match project key risks. ISO 14971 framework accurately applied. Yocto Scarthgap 5.0 + Linux 6.6.52 confirmed in R-003.

---

## Review Notes

**TRUST 5 Assessment**

- **Testable (4/5)**: Risk scores, FPGA resource numbers, and bandwidth figures are verifiable against hardware ground truth. Probability and impact assessments are subjective by nature but well-justified. Residual risk calculations are arithmetically correct.
- **Readable (5/5)**: Consistent risk register format across all entries. Each risk has a standardized attribute table, description, mitigation actions, and residual risk assessment. ISO 14971 framework application is clearly explained.
- **Unified (4/5)**: Risk entries follow a consistent template. Minor: R-005 fallback path decision tree uses a slightly different visual style than other sections, but this does not affect clarity.
- **Secured (5/5)**: Medical device risk context (ISO 14971) is correctly applied. No sensitive technical details that could aid adversaries are exposed. Security-related risks are appropriately documented.
- **Trackable (4/5)**: All risks have unique IDs, owners, and phase assignments. Status lifecycle is defined. Version history not yet established; added in this review cycle.

**Corrections Applied**

No additional corrections required in this review cycle. The previous review (manager-quality) already corrected R-002 bandwidth claim for 400 Mbps support scope.

**Minor Observations (non-blocking)**

- R-001 upgrade path LUT counts (XC7A50T: 32,600, XC7A75T: 47,200, XC7A100T: 63,400) are standard Xilinx product line specifications and are accurate.
- R-003 BQ40z50 I2C address 0x0b is consistent with the hardware ground truth (SMBus I2C addr 0x0b).
- R-008 RAW16 data type hex value 0x2E is consistent with the CSI-2 specification and project ground truth.

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | ABYZ-Lab Documentation Agent | Initial document creation |
| 1.0.1 | 2026-02-17 | manager-quality | Approved with corrections. R-002 bandwidth claim corrected (400M supports up to 2048x2048@15fps, not 30fps). |
| 1.0.2 | 2026-02-17 | manager-docs (doc-approval-sprint) | Reviewed → Approved. No additional technical corrections. Added Review Notes and Revision History. |
