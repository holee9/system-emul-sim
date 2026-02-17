# SPEC-ARCH-001: Implementation Plan

## Overview

This implementation plan outlines the three-phase approach to completing M0 milestone P0 architecture decisions. The plan focuses on documentation, validation, and procurement preparation with a 5-day timeline.

---

## Implementation Phases

### Phase 1: Architecture Decision Documentation (Days 1-2)

**Objective**: Document all P0 architecture decisions with technical rationale and constraints.

**Tasks**:

1. **Performance Tier Analysis** (4 hours)
   - Calculate bandwidth requirements for Minimum, Target, and Maximum tiers
   - Validate CSI-2 D-PHY aggregate bandwidth constraints (4-5 Gbps)
   - Document tier selection rationale (Target tier recommended, Minimum as baseline)
   - Create performance tier comparison table with feasibility assessment

2. **Host Link Technology Selection** (3 hours)
   - Compare 10 GbE vs 1 GbE bandwidth capacity against tier requirements
   - Document tier support mapping (10 GbE → all tiers, 1 GbE → Minimum only)
   - Recommend 10 GbE for production deployment
   - Identify 1 GbE limitations and development-only use case

3. **SoC Platform Evaluation** (5 hours)
   - Evaluate NXP i.MX8M Plus features (CSI-2 receiver, Cortex-A53, GPU, GbE)
   - Assess alternative platforms (Raspberry Pi CM4, NVIDIA Jetson Nano)
   - Validate CSI-2 receiver compatibility with MIPI D-PHY 4-lane configuration
   - Document SoC selection rationale and fallback options

4. **FPGA Interface Confirmation** (3 hours)
   - Reconfirm CSI-2 MIPI D-PHY as exclusive high-speed interface
   - Document USB 3.x rejection rationale (LUT 72-120% vs 20,800 available)
   - Validate FPGA resource budget (<60% LUT utilization target)
   - Estimate CSI-2 TX IP resource consumption from AMD IP documentation

**Deliverables**:
- Architecture decision document (this SPEC)
- Performance tier bandwidth validation spreadsheet
- SoC platform comparison matrix
- FPGA resource budget estimate

**Dependencies**:
- Access to AMD MIPI CSI-2 TX Subsystem IP documentation
- NXP i.MX8M Plus datasheet and technical reference manual
- Xilinx Artix-7 XC7A35T resource specifications

---

### Phase 2: Technology Stack Validation (Days 3-4)

**Objective**: Validate development tool versions, IP licenses, and library compatibility.

**Tasks**:

1. **FPGA Development Tools** (4 hours)
   - Confirm AMD Vivado 2023.2+ availability and license
   - Verify Artix-7 XC7A35T device support in Vivado
   - Document MIPI CSI-2 TX Subsystem IP version (v3.1 or later)
   - Validate IP license availability and procurement requirements

2. **SoC Firmware Stack** (4 hours)
   - Confirm GCC ARM cross-compiler version for Cortex-A53
   - Validate lwIP network stack version and compatibility
   - Document SoC BSP (Board Support Package) availability for i.MX8M Plus
   - Identify firmware development dependencies and tools

3. **Host SDK and Tools** (5 hours)
   - Confirm .NET 8.0 LTS availability for C# SDK and simulator
   - Validate Visual Studio or VS Code configuration
   - Document C++ SDK dependencies (if applicable)
   - Verify GUI framework compatibility (WPF, WinForms, or Avalonia)

4. **Configuration Management** (2 hours)
   - Finalize `detector_config.yaml` schema for performance tier definitions
   - Document configuration parameter structure (resolution, bit depth, FPS)
   - Validate YAML parsing libraries for C# and C++
   - Create example configuration files for each performance tier

**Deliverables**:
- Technology stack version matrix (Vivado, GCC, .NET, IP versions)
- Tool installation and setup checklist
- Configuration schema documentation
- Development environment validation report

**Dependencies**:
- AMD Vivado license availability
- .NET 8.0 SDK installation
- Access to NXP i.MX8M Plus BSP documentation

---

### Phase 3: Procurement Planning (Day 5)

**Objective**: Prepare Bill of Materials (BOM) and procurement requests for hardware and IP licenses.

**Tasks**:

1. **FPGA Development Board Selection** (2 hours)
   - Identify Artix-7 XC7A35T evaluation boards (e.g., Digilent Arty A7-35T)
   - Validate board features (FPGA device, I/O headers, power supply)
   - Document board cost and availability
   - Create procurement request with vendor links

2. **SoC Evaluation Board Procurement** (3 hours)
   - Confirm NXP i.MX8M Plus EVK availability and pricing
   - Identify alternative boards if i.MX8M Plus unavailable
   - Validate board features (CSI-2 receiver, Ethernet, GPIO)
   - Document procurement lead time and delivery schedule

3. **Network Equipment** (2 hours)
   - Specify 10 Gigabit Ethernet switch or direct connection adapter
   - Identify compatible network cards for Host PC
   - Document network cable requirements (Cat6A or fiber)
   - Create network equipment BOM

4. **IP License and Software Procurement** (3 hours)
   - Confirm AMD MIPI CSI-2 TX Subsystem IP license requirements
   - Validate Vivado license availability (node-locked or floating)
   - Document software license costs and renewal schedule
   - Create IP and software procurement request

**Deliverables**:
- Complete Bill of Materials (BOM) with part numbers and costs
- Procurement request documents with vendor information
- Hardware lead time and delivery schedule
- Budget estimate for M0-M1 hardware and software

**Dependencies**:
- Vendor availability and pricing information
- Budget approval authority
- Procurement process and timeline

---

## Task Decomposition

### Priority-Based Milestones

**Primary Goal**: Complete P0 architecture decisions documentation
- Phase 1 architecture decision documentation (Days 1-2)
- All five P0 decisions documented with rationale
- Success criteria: SPEC-ARCH-001 spec.md complete and reviewed

**Secondary Goal**: Validate technology stack versions and compatibility
- Phase 2 technology stack validation (Days 3-4)
- All tool versions confirmed and documented
- Success criteria: Technology stack version matrix approved

**Final Goal**: Prepare procurement BOM and requests
- Phase 3 procurement planning (Day 5)
- BOM complete with part numbers and costs
- Success criteria: Procurement requests submitted for approval

**Optional Goal**: Begin Git repository structure setup
- Create 6 Git repositories (fpga, fw, sdk, tools, config, docs)
- Initialize repository README files and directory structure
- Success criteria: Repository skeleton ready for M0.5 PoC development

---

## Technology Stack Specifications

### FPGA Development Stack

**FPGA Device**: Xilinx Artix-7 XC7A35T-FGG484
- Logic Cells: 33,280
- LUTs: 20,800
- Block RAM: 50 (1.8 Mbit)
- DSP Slices: 90
- Package: FGG484 (484-pin BGA)

**Development Tools**:
- AMD Vivado Design Suite: 2023.2 or later
- License Type: Vivado HL WebPACK or HL Design Edition
- OS: Windows 10/11 or Linux (Ubuntu 20.04 LTS or later)

**IP Cores**:
- AMD MIPI CSI-2 TX Subsystem: v3.1 or later
- AXI Interconnect: Vivado IP Catalog
- AXI Stream FIFO: Vivado IP Catalog
- Clocking Wizard: Vivado IP Catalog

**Language**: SystemVerilog (IEEE 1800-2017)
- Testbench framework: UVM or custom testbench
- Coverage target: Line ≥95%, Branch ≥90%, FSM 100%

---

### SoC Platform Stack

**SoC Platform**: NXP i.MX8M Plus
- CPU: Quad-core ARM Cortex-A53 @ 1.8 GHz
- GPU: Vivante GC7000UL (OpenGL ES 3.1, Vulkan 1.1)
- VPU: H.265/H.264 encoder/decoder
- Memory: 2GB LPDDR4 (expandable to 4GB)
- Network: Gigabit Ethernet (10/100/1000 Mbps)
- CSI-2: MIPI CSI-2 receiver (4-lane D-PHY)

**Firmware Stack**:
- Build System: Yocto Project Scarthgap (5.0 LTS)
  - BSP Version: Variscite imx-6.6.52-2.2.0-v1.3
  - Linux Kernel: 6.6.52 (LTS support until December 2026, Yocto LTS until April 2028)
- Toolchain: GCC ARM 13.x (from Yocto SDK, arm-linux-gnueabihf-gcc)
- Network Stack: Linux glibc networking (TCP/UDP via standard sockets)
- CSI-2 Driver: Custom V4L2 driver (new development for FPGA data acquisition)

**Confirmed Hardware Peripherals** (as of 2026-02-17):
- WiFi/BT: Ezurio Sterling 60 (M.2, QCA6174A chip) | ath10k_pci driver | ✅ Kernel 6.6
- Battery: TI BQ40z50 (SMBus, 7-bit addr 0x0b) | bq27xxx_battery driver | ⚠️ Port from 4.4 needed
- IMU: Bosch BMI160 (I2C7, addr 0x68) | bmi160_i2c driver (IIO) | ✅ Kernel 6.6
- GPIO: NXP PCA9534 (8-bit I/O expander) | gpio-pca953x driver | ✅ Kernel 6.6
- 2.5GbE: On-board chip (model TBD) | TBD driver | ⚠️ Identify via lspci

**Alternative SoC Platforms** (Fallback):
- Raspberry Pi Compute Module 4 (BCM2711, Cortex-A72, 2-lane CSI-2)
- NVIDIA Jetson Nano (Tegra X1, Cortex-A57, 4-lane CSI-2)

---

### Host SDK Stack

**Primary Language**: C# (.NET 8.0 LTS)
- Target Framework: .NET 8.0 or later
- Language Version: C# 12
- Platform: Windows (x64), Linux optional

**Development Tools**:
- IDE: Visual Studio 2022 or VS Code with C# extensions
- Build System: MSBuild or dotnet CLI
- Testing Framework: xUnit or NUnit

**SDK Components**:
- Network Communication: System.Net.Sockets (TCP/UDP)
- Configuration Parsing: YamlDotNet or System.Text.Json
- GUI Framework: WPF (Windows) or Avalonia (cross-platform)
- Logging: Serilog or NLog

**Secondary Language** (Optional): C++ for performance-critical modules
- Compiler: MSVC 19.x or GCC 11.x
- Build System: CMake 3.20+
- Libraries: Boost.Asio (network), yaml-cpp (configuration)

---

### Configuration Management

**Single Source of Truth**: `detector_config.yaml`
- Schema Version: 1.0.0
- Location: `config/` repository
- Format: YAML (YAML 1.2 specification)

**Generated Artifacts** (from `detector_config.yaml`):
- FPGA RTL parameters (Verilog headers, SystemVerilog packages)
- Firmware C headers (enums, structs, constants)
- SDK configuration classes (C# models, C++ structs)
- Documentation tables (Markdown, HTML)

**Configuration Parameters**:
- Performance tier: Minimum, Target, Maximum
- Resolution: Width × Height (pixels)
- Bit depth: 14-bit or 16-bit
- Frame rate: 15 fps, 30 fps, or configurable
- Network settings: IP address, port, protocol

**Tools**:
- Parameter Extractor: C# .NET 8.0 (YAML → JSON schema)
- Code Generator: C# .NET 8.0 (templates → RTL/C/C# code)
- Validator: C# .NET 8.0 (schema validation, constraint checking)

---

## Risk Analysis

### Risk 1: D-PHY Bandwidth Saturation (Maximum Tier)

**Risk Description**: Maximum tier (3072×3072, 16-bit, 30fps, 4.53 Gbps) may exceed CSI-2 D-PHY practical bandwidth (4-5 Gbps with protocol overhead).

**Probability**: Medium (40%)

**Impact**: High (Maximum tier may be development reference only, not production-capable)

**Mitigation**:
- Target tier (2.01 Gbps) provides sufficient bandwidth margin
- M0.5 CSI-2 PoC validates Maximum tier bandwidth with real hardware
- Frame rate scaling (e.g., 20 fps instead of 30 fps) reduces bandwidth to 3.02 Gbps
- Resolution reduction (e.g., 2560×2560) reduces bandwidth to 3.14 Gbps

**Contingency**:
- If Maximum tier fails validation, document as future enhancement requiring upgraded SoC
- Proceed with Target tier as production goal
- Maintain Maximum tier as simulator reference for testing

---

### Risk 2: SoC CSI-2 Receiver Compatibility

**Risk Description**: NXP i.MX8M Plus CSI-2 receiver may have compatibility issues with AMD MIPI CSI-2 TX IP (lane ordering, HS/LP transitions, timing).

**Probability**: Medium (30%)

**Impact**: High (M0.5 PoC failure, potential SoC platform change)

**Mitigation**:
- Early CSI-2 PoC (M0.5, W6) validates compatibility before full development
- AMD MIPI IP documentation provides D-PHY timing parameters for verification
- NXP i.MX8M Plus reference manual documents CSI-2 receiver requirements
- Fallback SoC platforms identified (Raspberry Pi CM4, Jetson Nano)

**Contingency**:
- If i.MX8M Plus CSI-2 fails validation, evaluate Jetson Nano (known MIPI compatibility)
- Allocate 2-week buffer (W7-W8) for SoC platform migration if needed
- Update firmware BSP and development environment for alternative platform

---

### Risk 3: FPGA Resource Budget Overrun

**Risk Description**: MIPI CSI-2 TX IP + frame buffer + control logic may exceed 60% LUT target (12,480 LUTs).

**Probability**: Low (20%)

**Impact**: Medium (feature reduction or RTL optimization required)

**Mitigation**:
- Early resource utilization estimate from AMD MIPI IP documentation
- Incremental RTL development with resource monitoring after each module
- Frame buffer optimization (BRAM usage vs distributed RAM trade-off)
- Control logic simplification (SPI state machine, register file)

**Contingency**:
- If LUT usage exceeds 60%, reduce frame buffer depth (e.g., 2 frames → 1 frame)
- Optimize pixel packing logic (14-bit → 16-bit alignment)
- Defer optional features (test pattern generator, diagnostic counters)

---

### Risk 4: Procurement Delays

**Risk Description**: FPGA dev board, SoC eval board, or network equipment may have long lead times or availability issues.

**Probability**: Medium (35%)

**Impact**: Medium (M0.5 PoC schedule delay, development environment unavailable)

**Mitigation**:
- Submit procurement requests immediately after M0 approval (W1)
- Identify multiple vendor sources (Digikey, Mouser, Arrow, direct from vendor)
- Allocate 2-4 week lead time buffer for international shipping
- Prioritize critical items (SoC board for PoC) over non-critical (10 GbE switch)

**Contingency**:
- If i.MX8M Plus EVK unavailable, use Raspberry Pi CM4 for initial PoC
- If 10 GbE equipment unavailable, develop with 1 GbE (Minimum tier only)
- Use FPGA simulator for RTL development until hardware arrives

---

## Dependencies

### External Dependencies

**Hardware Procurement**:
- FPGA Development Board: Artix-7 XC7A35T evaluation board (e.g., Digilent Arty A7-35T)
- SoC Evaluation Board: NXP i.MX8M Plus EVK or alternative
- Network Equipment: 10 GbE switch or direct connection adapter
- Network Cables: Cat6A or fiber optic cables

**Software and IP Licenses**:
- AMD Vivado Design Suite 2023.2+ license
- AMD MIPI CSI-2 TX Subsystem IP license (may require purchase)
- .NET 8.0 SDK (free download)
- Visual Studio Community Edition or VS Code (free)

**Documentation and Vendor Support**:
- AMD MIPI CSI-2 TX Subsystem IP documentation
- NXP i.MX8M Plus datasheet and reference manual
- NXP BSP and Linux kernel drivers
- Xilinx Artix-7 FPGA datasheet and user guide

---

### Internal Dependencies

**Project Documentation**:
- `detector_config.yaml` schema definition (config repository)
- `CHEATSHEET.md` quick reference (root directory)
- `QUICKSTART.md` development setup guide (root directory)
- `X-ray_Detector_Optimal_Project_Plan.md` detailed schedule (root directory)

**Repository Structure** (6 Git repositories on Gitea):
1. `fpga/`: RTL, testbench, constraints, IP configurations
2. `fw/`: SoC firmware, drivers, network stack
3. `sdk/`: Host SDK, network client, configuration API
4. `tools/`: Simulator, GUI, parameter extractor, code generator
5. `config/`: `detector_config.yaml`, schemas, validation scripts
6. `docs/`: Architecture documents, API documentation, user guides

**Development Environment**:
- Git version control system with Gitea server
- CI/CD integration (n8n webhooks + Gitea)
- Network file share or cloud storage for large files (bitstreams, binaries)

---

### Milestone Dependencies

**M0 → M0.5**: CSI-2 PoC development requires:
- SoC platform procurement and availability
- AMD MIPI CSI-2 TX IP integration into FPGA project
- Basic CSI-2 receiver firmware (i.MX8M Plus BSP)
- FPGA development board with CSI-2 TX output

**M0.5 → M1**: Full FPGA development requires:
- CSI-2 PoC successful validation (data integrity, bandwidth, timing)
- FPGA resource budget confirmed within 60% LUT target
- SoC CSI-2 receiver compatibility validated

**M1 → M2**: Firmware development requires:
- FPGA CSI-2 TX operational and stable
- SoC platform firmware toolchain setup (GCC, BSP, RTOS)
- Network stack integration (lwIP or Linux networking)

**M2 → M3**: Host SDK integration requires:
- Firmware network protocol implementation (TCP or UDP)
- 10 GbE infrastructure setup and validated
- Configuration API defined and documented

---

## Next Steps

### Immediate Actions (Post-M0 Approval)

1. **Document Finalization** (Day 1-2)
   - Complete SPEC-ARCH-001 spec.md, plan.md, acceptance.md
   - Commit to Git with message: `feat(spec): Add SPEC-ARCH-001 P0 architecture decisions`
   - Create Pull Request for technical review

2. **Technology Stack Validation** (Day 3-4)
   - Install AMD Vivado 2023.2 and verify Artix-7 device support
   - Install .NET 8.0 SDK and verify C# project compilation
   - Download AMD MIPI CSI-2 TX Subsystem IP documentation
   - Download NXP i.MX8M Plus datasheet and reference manual

3. **Procurement Preparation** (Day 5)
   - Create Bill of Materials (BOM) spreadsheet
   - Identify vendor sources and obtain pricing quotes
   - Submit procurement requests for approval
   - Track procurement status and delivery schedule

4. **Git Repository Setup** (Optional, Day 5)
   - Create 6 Git repositories on Gitea server
   - Initialize README.md files and directory structure
   - Create `.gitignore` files for each repository
   - Document repository access and clone URLs

---

### Transition to M0.5 (CSI-2 PoC)

**Trigger**: M0 milestone approval and SoC platform procurement complete

**Objective**: Validate CSI-2 MIPI D-PHY data transmission from FPGA to SoC

**Key Activities**:
- Integrate AMD MIPI CSI-2 TX Subsystem IP into FPGA project
- Develop minimal RTL for test pattern generation and CSI-2 transmission
- Develop minimal SoC firmware for CSI-2 receiver and data validation
- Measure bandwidth, verify data integrity, validate timing margins

**Success Criteria**:
- CSI-2 data transmission successful at Target tier bandwidth (2.01 Gbps)
- Data integrity verified (zero errors over 1000 frames)
- Maximum tier bandwidth validation (4.53 Gbps feasibility assessment)

**Timeline**: W6 (6 weeks after project start)

---

## Traceability

This implementation plan aligns with:

- **SPEC-ARCH-001 spec.md**: All requirements and acceptance criteria mapped to implementation tasks
- **X-ray_Detector_Optimal_Project_Plan.md**: M0 milestone (W1) and M0.5 milestone (W6)
- **CHEATSHEET.md**: FPGA constraints, interface decisions, naming conventions
- **detector_config.yaml**: Performance tier definitions, configuration schema

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | ABYZ-Lab Agent (manager-spec) | Initial implementation plan for SPEC-ARCH-001 |

---

**END OF PLAN**
