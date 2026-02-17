# X-ray Detector Panel System - Product Overview

**Status**: 📋 Pre-implementation Baseline (M0 Preparation - Week 1)
**Generated**: 2026-02-17
**Source**: X-ray_Detector_Optimal_Project_Plan.md, README.md
**Last Updated**: 2026-02-17

⚠️ **Important**: This documentation is generated from the project plan BEFORE implementation. The 6 Gitea repositories (fpga/, fw/, sdk/, tools/, config/, docs/) are not yet cloned into this workspace.

**Update Triggers**:
- When repositories are cloned into workspace
- When actual code structure emerges
- At M0 milestone completion (Week 1)
- When technology choices are finalized
- Run `/moai project --refresh` to regenerate from code

---

## Project Identity

**Name**: X-ray Detector Panel System
**Tagline**: Medical Imaging Grade Data Acquisition and Processing Platform
**Mission**: Deliver a production-grade, layered system for real-time X-ray detector panel control, data acquisition, and image processing for medical imaging equipment OEMs

**Project Type**: Research & Development System (Not a commercial product; platform for medical imaging equipment development)

**Development Timeline**: 28 weeks (7 months) spanning W1-W28
**Current Phase**: M0 Preparation (Week 1) - Architecture finalization and procurement planning

---

## Core Purpose

The X-ray Detector Panel System is a comprehensive hardware and software platform designed to:

1. **Real-time Control**: Interface with X-ray detector panels via ROIC (Readout Integrated Circuit) for synchronized image capture
2. **High-Speed Data Acquisition**: Capture pixel data at rates up to 4.53 Gbps (Maximum tier) with deterministic latency
3. **Efficient Data Transport**: Stream image frames from FPGA → SoC → Host PC with minimal overhead
4. **Flexible Configuration**: Support multiple detector resolutions (1024×1024 to 3072×3072), bit depths (14-16 bit), and frame rates (15-30 fps)
5. **Development Acceleration**: Provide simulation environment and code generation tools to accelerate medical imaging device development

**Primary Use Cases**:
- Medical X-ray imaging systems (radiography, fluoroscopy, mammography)
- Detector panel characterization and testing
- Image processing algorithm development
- System integration for medical equipment OEMs

---

## System Architecture

### High-Level Data Flow

```
[X-ray Detector Panel] ──(Analog)──> [ROIC] ──(Parallel Digital)──> [FPGA]
                                                                        │
                                                                        │ CSI-2 MIPI
                                                                        │ 4-lane D-PHY
                                                                        ↓
                                                                    [SoC] ──(10 GbE)──> [Host PC]
                                                                        ↑
                                                                        │ SPI (control)
                                                                    [FPGA]
```

### Component Roles

**FPGA (Xilinx Artix-7 XC7A35T-FGG484)**:
- Panel scan sequencing and timing generation
- Pixel data acquisition from ROIC interface
- Line buffering and frame synchronization
- CSI-2 MIPI D-PHY transmitter (4-lane)
- SPI slave for Host control
- Protection logic (thermal, timing violations)

**SoC (NXP i.MX8M Plus, recommended)**:
- CSI-2 receiver and frame buffer management
- Image preprocessing (optional: bad pixel correction, gain/offset)
- 10 Gigabit Ethernet MAC/PHY controller
- Host communication protocol stack
- Firmware runtime and diagnostics

**Host PC**:
- Image acquisition and visualization
- Advanced image processing (reconstruction, enhancement)
- System configuration and calibration
- Data storage and archival
- User interface (GUI for parameter tuning)

### Key Architectural Decisions

1. **CSI-2 as Primary Data Path**: MIPI CSI-2 4-lane D-PHY chosen as the ONLY high-speed interface between FPGA and SoC due to FPGA resource constraints
2. **USB 3.x Exclusion**: USB 3.x IP cores require 72-120% of Artix-7 35T LUT resources (14,980-25,008 LUTs) - IMPOSSIBLE to implement
3. **10 GbE for Host Link**: 10 Gigabit Ethernet selected for SoC→Host to support Target and Maximum performance tiers (1 GbE insufficient for >1 Gbps sustained data rates)
4. **Single Configuration Source**: `detector_config.yaml` serves as single source of truth, with converters generating FPGA, SoC, and Host configuration files

---

## Performance Envelope

The system supports three performance tiers to balance requirements, costs, and technical constraints:

### Tier Comparison Matrix

| Performance Tier | Resolution | Bit Depth | Frame Rate | Data Rate | Target Use Case | FPGA Resource | Host Link |
|-----------------|------------|-----------|------------|-----------|----------------|---------------|-----------|
| **Minimum** (Baseline) | 1024×1024 | 14-bit | 15 fps | ~0.21 Gbps | Development, Unit Tests | ~40% LUTs | 1 GbE OK |
| **Target** (Primary Goal) | 2048×2048 | 16-bit | 30 fps | ~2.01 Gbps | Standard Clinical Imaging | ~55% LUTs | 10 GbE Required |
| **Maximum** (Stretch) | 3072×3072 | 16-bit | 30 fps | ~4.53 Gbps | High-Res Research Imaging | ~60% LUTs | 10 GbE Required |

### Data Rate Calculations

**Formula**: `Data Rate (Gbps) = Width × Height × Bit Depth × FPS / 1e9`

**Examples**:
- Minimum: `1024 × 1024 × 14 × 15 / 1e9 = 0.221 Gbps`
- Target: `2048 × 2048 × 16 × 30 / 1e9 = 2.013 Gbps`
- Maximum: `3072 × 3072 × 16 × 30 / 1e9 = 4.529 Gbps`

### CSI-2 Bandwidth Constraints

**FPGA D-PHY Lane Speed**: ~1.0-1.25 Gbps/lane (Artix-7 OSERDES hardware limit, not D-PHY specification limit)
**4-Lane Aggregate**: ~4-5 Gbps raw bandwidth (before protocol overhead)

**CSI-2 Protocol Overhead**: ~20-30% (packet headers, line start/end, frame start/end, blanking intervals)
**Usable Bandwidth**: ~3.2-3.5 Gbps effective payload

**Implications**:
- **Minimum Tier**: 0.21 Gbps ✅ Well within CSI-2 capacity (15% utilization)
- **Target Tier**: 2.01 Gbps ✅ Fits comfortably within CSI-2 capacity (57-63% utilization)
- **Maximum Tier**: 4.53 Gbps ⚠️ Borderline, exceeds usable bandwidth, requires aggressive frame buffer optimization and compression

### M0 Decision Point

At Week 1 (M0 milestone), the following decisions must be finalized:
1. **Primary Performance Goal**: Confirm "Target" tier (2048×2048@30fps) as development goal
2. **Host Link**: Confirm 10 GbE requirement (1 GbE insufficient for Target/Maximum tiers)
3. **SoC Platform**: Confirm i.MX8M Plus or alternative with CSI-2 RX + 10 GbE MAC
4. **Development Board Procurement**: Order Artix-7 35T FGG484 dev board (critical for PoC at M0.5)

---

## Key Features

### 1. Layered Architecture
- **Hardware Abstraction**: FPGA RTL abstracts ROIC timing details, SoC firmware abstracts CSI-2 and Ethernet protocols
- **Clean Interfaces**: Well-defined API boundaries between FPGA/SoC/Host layers
- **Testability**: Each layer independently testable via simulators

### 2. Real-Time Panel Control
- **Deterministic Timing**: FPGA generates pixel-accurate scan sequences with <10 ns jitter
- **Synchronization**: Frame trigger, exposure control, and readout timing coordinated across panel and detector
- **Protection Logic**: Thermal monitoring, timing violation detection, emergency shutdown pathways

### 3. High-Speed Data Path
- **CSI-2 Streaming**: 4-lane MIPI D-PHY interface with hardware-accelerated packet encoding
- **Zero-Copy Design**: SoC firmware uses DMA to minimize CPU overhead during frame transfers
- **Ethernet Offload**: 10 GbE NIC handles Host communication with hardware checksum and scatter-gather DMA

### 4. Comprehensive Simulation Environment
- **PanelSimulator**: Models X-ray panel analog output with configurable noise, gain, offset
- **FpgaSimulator**: Cycle-accurate behavioral model of FPGA logic in C# (.NET)
- **McuSimulator (SoC)**: SoC firmware emulation with CSI-2 and Ethernet endpoints
- **HostSimulator**: Host SDK test harness for integration scenarios
- **Benefits**: HIL testing before hardware availability, regression testing, algorithm validation

### 5. Single Configuration Source
- **detector_config.yaml**: YAML file defining panel geometry, timing parameters, interface settings
- **Code Generation**: Automated converters generate FPGA RTL parameters, SoC header files, Host API wrappers
- **Version Control**: Configuration changes tracked in Git, ensures consistency across all layers
- **Validation**: JSON schema validation prevents invalid configurations

### 6. Developer Tooling
- **ParameterExtractor**: GUI tool (C# WPF) to parse detector vendor PDFs and extract timing/electrical parameters
- **ConfigConverter**: Translates detector_config.yaml to target-specific formats (Verilog, C header, C# class)
- **CodeGenerator**: Template-based code generation for repetitive RTL blocks and boilerplate firmware
- **IntegrationRunner**: Automated test orchestration for multi-layer HIL scenarios

---

## Core Constraints

### FPGA Resource Budget (ABSOLUTE)

**Device**: Xilinx Artix-7 XC7A35T-FGG484 (smallest Artix-7 FGG484 package)
**Resources**:
- Logic Cells: 33,280
- LUTs: 20,800 (6-input LUTs)
- Flip-Flops: 41,600
- BRAMs: 50 (36 Kbit each = 1.8 Mbit total)
- DSP Slices: 90

**Target Utilization**: <60% LUTs (<12,480 LUTs) to maintain 40% margin for timing closure and future features

**Why USB 3.x is IMPOSSIBLE**:
- USB 3.0 SuperSpeed IP: 14,980-17,400 LUTs (72-84% of device)
- USB 3.1 Gen2 IP: 20,000-25,008 LUTs (96-120% of device, EXCEEDS capacity)
- Remaining resources after USB IP: Insufficient for panel control logic, line buffers, protection logic

**CSI-2 Resource Estimate**:
- MIPI CSI-2 TX Subsystem: ~2,500-3,500 LUTs (12-17% of device)
- D-PHY via OSERDES: ~500-800 LUTs (2-4% of device)
- **Total CSI-2**: ~3,000-4,300 LUTs (14-21% of device) ✅ Leaves 60-80% for application logic

### D-PHY Bandwidth Ceiling

**Artix-7 OSERDES Speed**: Maximum serialization ratio 10:1 at DDR 1.25 Gbps (per Xilinx DS181 datasheet)
**Lane Speed**: ~1.0-1.25 Gbps/lane (practical, with timing margin)
**4-Lane Aggregate**: ~4-5 Gbps raw bandwidth

**NOT a D-PHY Specification Limit**: D-PHY v2.5 supports up to 2.5 Gbps/lane, but Artix-7 OSERDES is the bottleneck
**Implication**: Cannot achieve full D-PHY v2.5 speed; limited to ~1.0-1.25 Gbps/lane by FPGA hardware

### Host Link Bandwidth

**1 Gigabit Ethernet**: ~125 MB/s (1 Gbps / 8) effective throughput
- **Minimum Tier**: 0.21 Gbps → 26.25 MB/s ✅ OK (21% utilization)
- **Target Tier**: 2.01 Gbps → 251.25 MB/s ❌ EXCEEDS 1 GbE capacity
- **Maximum Tier**: 4.53 Gbps → 566.25 MB/s ❌ FAR EXCEEDS 1 GbE capacity

**10 Gigabit Ethernet**: ~1.25 GB/s (10 Gbps / 8) effective throughput
- **Minimum Tier**: 0.21 Gbps → 26.25 MB/s ✅ OK (2% utilization)
- **Target Tier**: 2.01 Gbps → 251.25 MB/s ✅ OK (20% utilization)
- **Maximum Tier**: 4.53 Gbps → 566.25 MB/s ✅ OK (45% utilization)

**Recommendation**: 10 GbE required for Target and Maximum tiers

---

## Target Users

### Primary Audience
1. **Medical Equipment OEMs**: Companies developing X-ray imaging systems (radiography, fluoroscopy, mammography)
2. **Detector Manufacturers**: Vendors integrating custom detector panels into imaging equipment
3. **Research Institutions**: Universities and labs conducting medical imaging algorithm research

### Secondary Audience
4. **FPGA Engineers**: Hardware designers working on medical device data acquisition systems
5. **System Integrators**: Engineers integrating detector panels into complete imaging systems
6. **Algorithm Developers**: Software engineers developing image reconstruction, enhancement, or AI-based analysis

### User Roles
- **System Architect**: Defines system requirements, selects components, approves design
- **FPGA Developer**: Implements RTL, synthesizes, validates timing and resource utilization
- **Firmware Developer**: Writes SoC firmware (C/C++), integrates CSI-2 and Ethernet drivers
- **Software Developer**: Creates Host SDK (C++/C#), GUI tools, integration tests
- **Test Engineer**: Develops HIL test scenarios, runs characterization, validates performance

---

## Development Timeline

### Phase Overview (28 weeks total)

| Phase | Weeks | Milestone | Focus | Deliverables |
|-------|-------|-----------|-------|--------------|
| P0 | W1 | M0 | Requirements & Architecture | Finalized architecture, BOM, procurement plan |
| P1 | W2-W6 | M0.5 | Foundation & PoC | RTL skeleton, CSI-2 PoC, simulation framework |
| P2 | W7-W10 | M1 | Core Implementation | FPGA logic complete, SoC firmware alpha, Host SDK alpha |
| P3 | W11-W14 | M2 | Integration & Testing | End-to-end HIL tests, Minimum tier validated |
| P4 | W15-W18 | M3 | Optimization | Target tier performance achieved, power optimized |
| P5 | W19-W21 | M4 | Tooling & Automation | ParameterExtractor GUI, CodeGenerator, ConfigConverter |
| P6 | W22-W24 | M5 | Validation & Documentation | Full test suite passing, API docs, user guides |
| P7 | W25-W27 | M6 | Pilot Deployment | Customer pilot, feedback integration |
| P8 | W28 | M6+ | Handoff & Transition | Final release, training materials, support transition |

### Key Milestones
- **M0 (W1)**: Architecture finalized, performance tier confirmed, procurement initiated
- **M0.5 (W6)**: CSI-2 PoC operational on dev board, simulation environment functional
- **M1 (W10)**: Core FPGA and SoC firmware alpha release, integration begins
- **M2 (W14)**: Minimum tier (1024×1024@15fps) validated end-to-end
- **M3 (W18)**: Target tier (2048×2048@30fps) performance achieved
- **M4 (W21)**: Developer tooling complete and validated
- **M5 (W24)**: Full TRUST 5 quality compliance, documentation complete
- **M6 (W27)**: Customer pilot deployment and feedback collection
- **M6+ (W28)**: Final release and project handoff

---

## Quality Strategy

### Development Methodology: Hybrid (TDD + DDD)

**Configured in**: `.moai/config/sections/quality.yaml` → `development_mode: "hybrid"`

**New Code (TDD - RED-GREEN-REFACTOR)**:
- Simulators (PanelSimulator, FpgaSimulator, McuSimulator, HostSimulator)
- Host SDK (C++/C# libraries)
- Developer tools (ParameterExtractor, CodeGenerator, ConfigConverter)
- Test projects (unit tests, integration tests)

**Existing Code (DDD - ANALYZE-PRESERVE-IMPROVE)**:
- FPGA RTL (characterization tests before modifications)
- SoC firmware HAL integration (behavior preservation tests)

### Coverage Targets

**RTL (FPGA)**:
- Line Coverage: ≥95%
- Branch Coverage: ≥90%
- FSM State Coverage: 100%
- Toggle Coverage: ≥80% (for critical signals)

**Software (C#/C++)**:
- Per-module Coverage: 80-90%
- Overall Coverage: ≥85%

**Integration Tests**:
- 10 scenarios (IT-01 through IT-10) covering end-to-end data paths
- HIL test patterns with hardware-in-the-loop validation

### TRUST 5 Framework

**Tested**: 85%+ coverage, characterization tests for existing code, mutation testing (experimental)
**Readable**: Clear naming, English comments, minimal cyclomatic complexity
**Unified**: Consistent style (ruff/black for Python, clang-format for C++, SystemVerilog style guide for RTL)
**Secured**: OWASP compliance, input validation, secrets management (never commit credentials)
**Trackable**: Conventional commits, issue references, structured logs

### LSP Quality Gates

**Plan Phase**: Capture LSP baseline at phase start
**Run Phase**: Zero errors, zero type errors, zero lint errors required
**Sync Phase**: Zero errors, max 10 warnings, clean LSP required

---

## Market Position

**Category**: Research & Development System (Not a commercial product; internal tooling and platform)

**Competitive Landscape**:
- **Commercial Solutions**: Varex Imaging, Teledyne DALSA (integrated detector+FPGA modules, closed ecosystems, high cost)
- **Custom In-House Solutions**: Many medical OEMs develop proprietary data acquisition systems (fragmented, non-reusable)
- **FPGA IP Vendors**: Xilinx, Lattice (provide CSI-2/MIPI IP but not complete application frameworks)

**Differentiation**:
1. **Open Architecture**: Modular design with well-defined APIs, extensible for custom detector panels
2. **Simulation-First**: Comprehensive simulation environment enables development before hardware availability
3. **Single Configuration Source**: `detector_config.yaml` eliminates configuration drift and manual synchronization
4. **Developer Tooling**: GUI tools for parameter extraction and code generation accelerate development
5. **Quality Rigor**: Hybrid TDD/DDD methodology with 85%+ coverage and TRUST 5 compliance

**Strategic Positioning**: Internal R&D platform for medical imaging equipment OEMs, not a standalone product for external sale

---

## Success Criteria

### Technical Success
1. **Minimum Tier Validated**: 1024×1024@15fps end-to-end operation with <1% frame loss
2. **Target Tier Achieved**: 2048×2048@30fps with deterministic latency and stable operation
3. **FPGA Resource Budget Met**: <60% LUT utilization with 40% margin for future enhancements
4. **Quality Gates Passed**: TRUST 5 compliance, 85%+ coverage, zero critical bugs

### Process Success
5. **Timeline Adherence**: M0-M6 milestones achieved within ±1 week tolerance
6. **Test Coverage**: ≥85% overall, RTL ≥95% line/≥90% branch/100% FSM
7. **Documentation Complete**: Architecture docs, API reference, user guides, SPEC documents

### Organizational Success
8. **Customer Pilot**: At least one customer pilot deployment with positive feedback
9. **Knowledge Transfer**: Development team trained, support documentation complete
10. **Reusability**: Framework proven reusable for 2+ detector panel variants

---

## Assumptions and Dependencies

### Assumptions
1. Xilinx Artix-7 35T FGG484 dev board available by W1 (M0 milestone)
2. i.MX8M Plus eval board (or equivalent SoC) available by W3
3. 10 GbE network infrastructure (NIC + switch) available by W8
4. Detector panel specifications (timing, electrical) available in vendor PDFs
5. MIPI CSI-2 TX IP license acquired (bundled with Vivado or separate procurement)

### Dependencies
1. **Hardware Procurement**: Dev boards, eval boards, cables, network equipment (procurement schedule in project plan)
2. **IP Licensing**: AMD/Xilinx MIPI CSI-2 TX Subsystem license
3. **Toolchain Availability**: Vivado 2023.x or later, .NET SDK 8.0+, C++ cross-compiler for SoC target
4. **Vendor Documentation**: Detector panel datasheets and timing diagrams from panel manufacturer

### Risks
1. **D-PHY Bandwidth Risk**: Maximum tier (4.53 Gbps) may require compression or frame buffer optimization if CSI-2 overhead exceeds estimates
2. **SoC Platform Risk**: i.MX8M Plus CSI-2 receiver compatibility requires early validation (PoC at M0.5)
3. **FPGA Resource Risk**: If LUT utilization exceeds 60%, scope reduction (e.g., reduce Maximum tier support) may be necessary
4. **Schedule Risk**: Hardware procurement delays could push M0.5 milestone by 1-2 weeks

---

## Future Roadmap (Post-W28)

### Potential Extensions
1. **Additional Detector Support**: Expand to support 2+ detector panel variants (different resolutions, bit depths, manufacturers)
2. **Real-Time Image Processing**: Implement on-the-fly preprocessing (bad pixel correction, gain/offset, histogram equalization) in SoC
3. **AI Integration**: Add inference engine for real-time image classification or anomaly detection
4. **Multi-Panel Arrays**: Support tiled detector arrays (2×2, 3×3) with synchronized readout
5. **Cloud Connectivity**: Optional cloud upload for remote diagnostics and AI training data collection

### Technology Upgrades
- **FPGA**: Migrate to Artix-7 100T or Kintex UltraScale+ for higher bandwidth and resource headroom
- **SoC**: Evaluate alternatives with native 10 GbE MAC and higher CSI-2 lane counts
- **Host Link**: Explore 25 GbE or 40 GbE for future ultra-high-resolution applications

---

## Appendix: Glossary

**CSI-2**: Camera Serial Interface version 2 (MIPI Alliance standard for camera data transmission)
**D-PHY**: MIPI physical layer specification for high-speed serial communication (used by CSI-2)
**FPGA**: Field-Programmable Gate Array (reconfigurable logic device)
**OSERDES**: Output Serializer/Deserializer (Xilinx primitive for high-speed serial output)
**ROIC**: Readout Integrated Circuit (converts analog X-ray detector signals to digital)
**SoC**: System-on-Chip (embedded processor with integrated peripherals)
**HIL**: Hardware-in-the-Loop (testing with real hardware components)
**TRUST 5**: Quality framework (Tested, Readable, Unified, Secured, Trackable)

---

**Document End**

*This is a pre-implementation baseline document. Run `/moai project --refresh` after code repositories are cloned to regenerate from actual implementation.*
