# SPEC-ARCH-001: P0 Architecture Decisions and Technology Stack Finalization

---
id: SPEC-ARCH-001
version: 1.1.0
status: approved
created: 2026-02-17
updated: 2026-02-17
author: ABYZ-Lab Agent (manager-spec)
priority: critical
---

## HISTORY

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | ABYZ-Lab Agent (manager-spec) | Initial SPEC creation for M0 milestone P0 decisions |

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

**IMPACT**: Mid-B tier (2048×2048, 16-bit, 30fps, ~2.01 Gbps) requires 800 Mbps/lane (debugging in progress). Target (Final Goal) tier (3072×3072, 16-bit, 15fps, ~2.26 Gbps) also requires 800 Mbps/lane. Maximum tier (3072×3072, 16-bit, 30fps, ~4.53 Gbps) exceeds hardware limits and is not a supported tier.

---

### 3. State-Driven Requirements (Performance Tier Modes)

**REQ-ARCH-007**: **IF** Mid-B tier (2048×2048, 16-bit, 30fps) or Target (Final Goal) tier (3072×3072, 16-bit, 15fps) is selected **THEN** CSI-2 D-PHY shall operate at 800 Mbps/lane with 4 lanes (3.2 Gbps aggregate).

**WHY**: Mid-B tier requires ~2.01 Gbps and Target (Final Goal) tier requires ~2.26 Gbps aggregate bandwidth. Both exceed the 400 Mbps/lane verified capacity (1.6 Gbps) and require 800 Mbps/lane operation. The 800 Mbps/lane mode is operational but undergoing stability debugging as of W1.

**IMPACT**: MIPI CSI-2 TX Subsystem IP configuration must be validated at 800 Mbps/lane before Mid-B or Target tier development begins. Development baseline is Mid-A tier (400 Mbps/lane, verified stable).

---

**REQ-ARCH-008**: **IF** 10 Gigabit Ethernet is selected as Host link **THEN** the system shall support all four supported performance tiers (Minimum, Mid-A, Mid-B, Target (Final Goal)).

**WHY**: 10 GbE provides 10 Gbps bandwidth, sufficient for the Target (Final Goal) tier (2.26 Gbps) and Mid-B tier (2.01 Gbps) with ample headroom. Maximum tier (3072×3072@30fps, ~4.53 Gbps) is not a supported development tier as it exceeds hardware limits.

**IMPACT**: 10 GbE selection enables all four supported tiers. Firmware and Host SDK must implement tier-specific configuration and validation.

---

**REQ-ARCH-009**: **IF** 1 Gigabit Ethernet is selected as Host link **THEN** the system shall support Minimum tier and Mid-A tier only.

**WHY**: 1 GbE provides approximately 0.94 Gbps effective throughput after protocol overhead. This is sufficient for Minimum tier (0.21 Gbps) and Mid-A tier (1.01 Gbps) but insufficient for Mid-B tier (2.01 Gbps) or higher.

**Note: Mid-B tier and above require 10 GbE network connection. 1 GbE supports only Minimum and Mid-A tiers.**

**IMPACT**: 1 GbE limits system to Minimum and Mid-A tiers. Mid-B tier (2048x2048@30fps), Target tier (3072x3072@15fps), and Maximum tier (3072x3072@30fps) all require 10 GbE. Not recommended for production deployment in clinical environments requiring higher performance.

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

| Tier | Resolution | Bit Depth | FPS | Data Rate (Gbps) | CSI-2 Feasible | Host Link Required |
|------|-----------|-----------|-----|------------------|----------------|--------------------|
| Minimum | 1024×1024 | 14-bit | 15 | 0.21 | ✅ Yes | 1 GbE or 10 GbE |
| Mid-A | 2048×2048 | 16-bit | 15 | 1.01 | ✅ Yes (400 Mbps/lane) | 1 GbE or 10 GbE |
| Mid-B | 2048×2048 | 16-bit | 30 | 2.01 | ⚠️ 800 Mbps/lane | **10 GbE required** |
| Target (Final) | 3072×3072 | 16-bit | 15 | 2.26 | ⚠️ 800 Mbps/lane | **10 GbE required** |
| Maximum | 3072×3072 | 16-bit | 30 | 4.53 | ⚠️ Validation Required | **10 GbE required** |

**Note: Mid-B tier and above require 10 GbE network connection. 1 GbE supports only Minimum and Mid-A tiers.**

1 GbE effective throughput is approximately 0.94 Gbps after protocol overhead, which is insufficient for Mid-B tier (2.01 Gbps) and higher. Using 1 GbE with Mid-B or above will result in severe frame drops and buffer overflow.

**10 GbE Host Link Bandwidth**: 10 Gbps (supports all tiers including Mid-B and above)

**1 GbE Host Link Bandwidth**: ~0.94 Gbps effective (Minimum and Mid-A tiers only)

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

### Inter-Layer Command Protocol

The three-layer architecture (FPGA / SoC Firmware / Host SDK) uses the following command protocol for the control channel. This protocol is implemented in SoC firmware (see SPEC-FW-001) and Host SDK (see SPEC-SDK-001), and defines the logical interface between layers.

**Transport**: UDP, port 8001 (control channel). Frame data uses UDP port 8000 (data channel).

**Magic Values**:

| Direction | Magic Value | Description |
|-----------|-------------|-------------|
| Host → SoC Firmware | `0xBEEFCAFE` | Identifies an outbound host command frame |
| SoC Firmware → Host | `0xCAFEBEEF` | Identifies an inbound firmware response frame |

**Command Frame Structure** (little-endian byte order):

| Offset (bytes) | Size (bytes) | Field | Description |
|----------------|-------------|-------|-------------|
| 0 | 4 | magic | `0xBEEFCAFE` for commands, `0xCAFEBEEF` for responses |
| 4 | 4 | sequence | Monotonic sequence number for replay protection |
| 8 | 2 | command_id | Opcode (START_SCAN=0x01, STOP_SCAN=0x02, GET_STATUS=0x10, SET_CONFIG=0x20) |
| 10 | 2 | payload_len | Length of the command-specific payload in bytes |
| 12 | 32 | hmac | HMAC-SHA256 authentication tag (per SPEC-FW-001 REQ-FW-100) |
| 44 | variable | payload | Command-specific data |

**Protocol Rules**:
- Any received frame whose first 4 bytes do not match `0xBEEFCAFE` (on firmware side) or `0xCAFEBEEF` (on SDK side) **shall** be discarded.
- Sequence numbers are monotonically increasing; frames with sequence <= last accepted sequence are treated as replays and discarded.
- The SoC firmware echoes the received sequence number in the response frame.

**Rationale**: Documenting the command protocol at the architecture level ensures that FPGA simulator (SPEC-SIM-001), SoC firmware (SPEC-FW-001), and Host SDK (SPEC-SDK-001) implementations are consistent and independently verifiable.

---

## Acceptance Criteria

### Success Criteria for M0 Completion

**AC-001**: Performance tier selection documented with bandwidth validation
- **GIVEN**: Four supported performance tiers (Minimum, Mid-A, Mid-B, Target (Final Goal)) and one infeasible reference tier (Maximum)
- **WHEN**: Bandwidth calculations are performed for each tier
- **THEN**: Target (Final Goal) tier (3072×3072, 16-bit, 15fps, ~2.26 Gbps) shall be the final development target, with Mid-A (2048×2048, 16-bit, 15fps) as the verified development baseline
- **AND**: Bandwidth validation confirms Target (Final Goal) tier feasibility at 800 Mbps/lane (3.2 Gbps aggregate, 71% utilization)
- **AND**: Maximum tier (3072×3072@30fps, ~4.53 Gbps) shall be documented as exceeding hardware limits and excluded from development scope

---

**AC-002**: Host link technology selection documented with tier support mapping
- **GIVEN**: 10 GbE and 1 GbE options
- **WHEN**: Bandwidth requirements for each tier are compared against link capacity
- **THEN**: 10 GbE shall be recommended for production deployment
- **AND**: 1 GbE shall be noted as Minimum and Mid-A tiers only (insufficient for Mid-B and above) with limited applicability

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

## P0-006: SoC Firmware Build System Selection

### Overview

**Decision**: Yocto Project Scarthgap (5.0 LTS) shall be used as the exclusive build system for i.MX8M Plus firmware.

**Rationale**:
- Scarthgap is a Long-Term Support (LTS) release maintained until April 2028
- Provides Linux kernel 6.6 (LTS until December 2026)
- Medical device certification requires LTS operating system with security patch support
- Variscite provides official BSP support (imx-6.6.52-2.2.0-v1.3)
- Existing Mickledore (4.2) environment reached EOL in November 2024

**Migration Timeline**: W1-W2 (8 days total)

---

### Requirements

**REQ-ARCH-020**: The SoC firmware **shall** be built using Yocto Project Scarthgap (5.0 LTS) based on Variscite BSP.

**WHY**: LTS release ensures long-term security support, kernel stability, and vendor BSP compatibility.

**IMPACT**: All firmware development must use Yocto Scarthgap toolchain. No alternative build systems (Buildroot, Ubuntu Core) permitted.

---

**REQ-ARCH-021**: The SoC firmware **shall** use Linux kernel 6.6.52 from Variscite BSP.

**WHY**: Kernel 6.6 is LTS (supported until December 2026) and includes required drivers for peripherals.

**IMPACT**: Kernel module development must target kernel 6.6 API. Legacy drivers require recompilation or porting.

---

**REQ-ARCH-022**: The SoC firmware **shall** integrate the following hardware components with verified drivers:

| Component | Model | Interface | Driver | Kernel 6.6 Status |
|-----------|-------|-----------|--------|-------------------|
| WiFi/BT | Ezurio Sterling 60 (QCA6174A) | M.2 PCIe + USB | ath10k_pci | ✅ Included |
| Battery | TI BQ40z50 | SMBus (I2C addr 0x0b) | bq27xxx_battery | ⚠️ Port from 4.4 needed |
| IMU | Bosch BMI160 | I2C7 (addr 0x68) | bmi160_i2c (IIO) | ✅ Included |
| GPIO | NXP PCA9534 | I2C | gpio-pca953x | ✅ Included |
| 2.5GbE | TBD (on-board chip) | PCIe/RGMII | TBD | ⚠️ Verify chip model |

**WHY**: Hardware platform validation ensures all peripherals are supported before integration.

**IMPACT**: BQ40z50 battery driver requires porting from kernel 4.4 to 6.6. 2.5GbE chip identification required via `lspci -nn`.

---

**REQ-ARCH-023**: The SoC firmware **shall** implement new CSI-2 receiver driver for FPGA data acquisition.

**WHY**: Existing dscam6.ko driver is deprecated. FPGA→i.MX8MP data path requires custom V4L2 driver.

**IMPACT**: New driver development required:
1. MIPI CSI-2 4-lane D-PHY receiver configuration
2. V4L2 video device node (/dev/videoX)
3. FPGA-SoC data format definition (MIPI CSI-2 RAW16 or custom)
4. Buffer management and DMA integration

---

**REQ-ARCH-024**: The SoC firmware **shall** define FPGA-SoC data format specification.

**WHY**: FPGA CSI-2 TX and SoC CSI-2 RX must agree on pixel format, frame structure, and metadata encoding.

**IMPACT**: Data format specification must be documented in W1-W8 documentation phase. Possible formats:
- MIPI CSI-2 RAW16 (16-bit grayscale)
- Custom frame format with header metadata
- Multi-frame buffering strategy

---

### New Development Scope

**Deprecated Legacy Drivers** (NOT migrated):
- ❌ dscam6.ko - Legacy CSI-2 camera driver (replaced by new FPGA RX driver)
- ❌ ax_usb_nic.ko - Failed AX88279 USB Ethernet (replaced by 2.5GbE on-board)
- ❌ imx8-media-dev.ko - Generic media device (V4L2 framework used instead)

**New Development Required**:
1. **FPGA → i.MX8MP CSI-2 RX Driver** (V4L2 subsystem)
   - Target: Linux kernel 6.6
   - Framework: Video4Linux2 (V4L2)
   - Interface: MIPI CSI-2 4-lane D-PHY
   - Development phase: W9-W14 (after documentation)

2. **FPGA-SoC Data Format Definition** (W1-W8 documentation)
   - Pixel format: 16-bit grayscale or custom RAW
   - Frame header: Metadata, timestamps, sequence numbers
   - Error handling: CRC, frame loss detection

3. **2.5GbE Network Driver Validation** (W15-W18)
   - Chip identification: `lspci -nn | grep -i ethernet`
   - Driver verification: Confirm kernel 6.6 support
   - Performance testing: Validate sustained throughput

---

### Migration Impact Assessment

**Reduced Complexity** (vs initial estimate):
- **Original estimate**: 13 days (legacy driver recompilation)
- **Revised estimate**: 8 days (legacy drivers deprecated)
- **Cost reduction**: 38% (5 days saved)

**Migration Scope** (W1-W2):
- Day 1-2: Document revision (7 essential documents)
- Day 3-4: Yocto Scarthgap BSP build and image creation
- Day 5-6: Hardware validation (WiFi, Battery, IMU, GPIO)
- Day 7: 2.5GbE chip identification and driver check
- Day 8: Migration completion report and M0 approval

**Risk Assessment**:
- **Low Risk**: WiFi (ath10k stable), IMU (IIO framework stable), GPIO (PCA9534 stable)
- **Medium Risk**: Battery (driver port needed, SMBus protocol validation)
- **High Risk**: 2.5GbE (chip model unknown, driver compatibility TBD)

---

### Acceptance Criteria

**AC-006**: Scarthgap BSP build completes successfully
- **GIVEN**: Variscite imx-6.6.52-2.2.0-v1.3 BSP
- **WHEN**: Yocto build is executed with MACHINE=imx8mp-var-dart
- **THEN**: core-image-minimal-imx8mp-var-dart.wic image shall be generated
- **AND**: Image shall boot on VAR-SOM-MX8M-PLUS hardware

---

**AC-007**: All hardware peripherals verified on Scarthgap
- **GIVEN**: Confirmed hardware list (WiFi, Battery, IMU, GPIO, 2.5GbE)
- **WHEN**: Scarthgap image boots on target hardware
- **THEN**: All devices shall appear in kernel dmesg and lspci/i2cdetect output
- **AND**: Basic driver functionality shall be verified (WiFi scan, I2C read, network link)

---

**AC-008**: 2.5GbE chip identified and driver validated
- **GIVEN**: On-board 2.5GbE module
- **WHEN**: `lspci -nn | grep -i ethernet` is executed
- **THEN**: Chip vendor and model shall be identified
- **AND**: Kernel 6.6 driver availability shall be confirmed

---

**AC-009**: Migration documentation complete
- **GIVEN**: 7 essential documents (SPEC-ARCH-001, SPEC-POC-001, README, etc.)
- **WHEN**: All documents are revised with Scarthgap details
- **THEN**: Documents shall include Scarthgap BSP version, hardware table, migration timeline
- **AND**: Git commit with conventional commit message shall be created

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
| 1.0.0 | 2026-02-17 | ABYZ-Lab Agent (manager-spec) | Initial SPEC creation for M0 milestone P0 decisions |
| 1.1.0 | 2026-02-17 | ABYZ-Lab Agent | MAJOR-003: Added Inter-Layer Command Protocol section (magic 0xBEEFCAFE/0xCAFEBEEF, frame format, protocol rules). MAJOR-004: Fixed performance tier naming conflict — renamed "Target" (2048×2048@30fps) to "Mid-B", established "Target (Final Goal)" as 3072×3072@15fps, documented Maximum tier as infeasible. Updated REQ-ARCH-006/007/008 and AC-001/002 accordingly. |
| 1.2.0 | 2026-02-17 | ABYZ-Lab Agent | MAJOR-007: Added Host Link Required column to bandwidth table showing 10 GbE is required for Mid-B tier and above. Updated REQ-ARCH-009 to explicitly state 1 GbE supports Minimum and Mid-A tiers only. Added note that 1 GbE effective throughput (~0.94 Gbps) is insufficient for Mid-B (2.01 Gbps) and higher. |

---

**END OF SPEC**
