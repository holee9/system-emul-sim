# X-ray Detector Panel System - Optimal Project Plan

## Executive Summary

This project plan synthesizes the **Development Plan v2** (80KB, 1381 lines, 17 chapters) and the **Deep Research Report** (31KB, 326 lines, 7 sections) to produce an execution-optimized plan for the X-ray detector panel system.

### Core Objective

Build a layered system (FPGA - SoC Controller - Host PC) to drive an X-ray detector panel, along with supporting tools: SW-only simulator, HIL environment, parameter extraction GUI, and code generator.

### Key Design Philosophy

- FPGA handles **minimal hard real-time functions only** (panel scan timing, line buffering, high-speed TX)
- Complex sequence control, communication, and post-processing are delegated to **SoC Controller and Host PC**
- **"One Source of Truth"** configuration (detector_config.yaml) drives all targets

### FPGA Device Constraint

**Target Device: Xilinx Artix-7 XC7A35T-FGG484**

This is a small-class FPGA with limited logic resources, which fundamentally constrains the interface architecture:

| Resource | Available | Impact |
|----------|-----------|--------|
| Logic Cells | 33,280 | USB 3.x Device Controller IP (~15-25K LUTs) alone would consume 72-120% |
| CLB Slices / LUTs | 5,200 / 20,800 | CSI-2 TX + FSM + SPI feasible at ~34-58% utilization |
| Block RAM (36Kb) | 50 (225 KB total) | Line buffer ping-pong uses ~5% (3 BRAMs for 3072x16bit x2) |
| DSP48E1 | 90 | Sufficient for any signal processing needs |
| GTP Transceivers | 4 (6.6 Gbps each) | Available but unused by CSI-2 D-PHY (uses LVDS); reserved for future |
| I/O Pins | ~250 user I/O | Sufficient for ROIC LVDS + Gate IC + SPI + CSI-2 D-PHY (4-lane) |

**Critical Conclusion: USB 3.x is INFEASIBLE on XC7A35T.** CSI-2 is the sole viable high-speed data path.

### Critical Synthesis Decisions

| Topic | Dev Plan Position | Research Report Position | **Optimal Decision (with XC7A35T)** |
|-------|------------------|------------------------|-----------------------|
| High-speed IF | USB 3.x + CSI-2 dual | CSI-2 primary | **CSI-2 ONLY (USB 3.x infeasible on Artix-7 35T)** |
| Host link | "10 GbE or split" | "1 GbE structurally impossible" | **Force P0 decision in W1-W3** |
| Performance targets | "Example" (3072x3072@30fps) | Ambiguous | **Define min/target/max 3-tier envelope** |
| Controller naming | "MCU" | "SoC-grade required" | **Align to "SoC Controller"** |
| Schedule | Linear 8-phase | Add PoC gate W1-W6 | **Insert CSI-2 PoC gate at W3-W6** |
| D-PHY lane speed | 2.5 Gbps/lane (spec) | - | **~1.0-1.25 Gbps/lane (Artix-7 OSERDES limit)** |
| Verification scope | 14 test cases | PoC-dependent | **10 test cases (USB-specific FV-08/09/13 removed)** |

---

## 1. P0 Critical Decisions (W1-W3 Resolution Required)

These decisions must be locked before any implementation begins. All downstream design choices depend on them.

### Decision 1: Performance Envelope

| Tier | Resolution | Bit Depth | FPS | Raw Data Rate | Use Case |
|------|-----------|-----------|-----|---------------|----------|
| **Minimum** | 1024 x 1024 | 14-bit | 15 fps | ~0.21 Gbps | Development & debug baseline |
| **Target** | 2048 x 2048 | 16-bit | 30 fps | ~2.01 Gbps | Standard clinical imaging |
| **Maximum** | 3072 x 3072 | 16-bit | 30 fps | ~4.53 Gbps | High-resolution reference |

**Action**: Stakeholder agreement on which tier is the "must-achieve" vs "stretch goal."

### Decision 2: Host Link Selection

| Option | Bandwidth | Supports Max Tier? | Cost/Complexity | Recommendation |
|--------|-----------|-------------------|-----------------|----------------|
| 1 GbE | ~125 MB/s | No (max ~1024x1024@30fps) | Low | Only if Minimum tier is sufficient |
| 10 GbE | ~1.25 GB/s | Yes (with margin) | Medium (PCIe add-on on SoC) | **Recommended for Target/Max tiers** |
| Compression + 1 GbE | ~125 MB/s effective | Depends on ratio | Medium (SW complexity) | Fallback if 10 GbE unavailable |

### Decision 3: High-Speed Interface (RESOLVED by FPGA Selection)

**CSI-2 MIPI is the ONLY viable option on XC7A35T-FGG484.**

| Criterion | CSI-2 (Selected) | USB 3.x (Infeasible) |
|-----------|-------------------|----------------------|
| Protocol fit | Image streaming optimized | General-purpose bulk |
| SoC ecosystem | Native ISP pipeline on i.MX8/RK3588 | USB Host available |
| FPGA resource requirement | ~3,000-5,000 LUTs (15-24%) | ~15,000-25,000 LUTs (72-120%) |
| Artix-7 35T feasibility | **YES** | **NO - exceeds total LUT capacity** |
| PHY implementation | LVDS IO + OSERDES (no external PHY needed) | Requires GTP transceiver + complex IP |
| Lane speed (Artix-7) | ~1.0-1.25 Gbps/lane via OSERDES | N/A |
| 4-lane total throughput | ~4.0-5.0 Gbps effective | N/A |

**D-PHY Lane Speed Constraint on Artix-7:**
- D-PHY spec allows up to 2.5 Gbps/lane, but Artix-7 OSERDES limits practical output to ~1.0-1.25 Gbps/lane
- 4-lane total: ~4.0-5.0 Gbps effective (before protocol overhead)
- Sufficient for Target tier (2048x2048@30fps = 2.01 Gbps)
- Tight for Maximum tier (3072x3072@30fps = 4.53 Gbps + overhead)
- If Maximum tier is required at full rate, consider external D-PHY TX PHY chip for 2.5 Gbps/lane

**Decision**: CSI-2 4-lane D-PHY as sole high-speed interface. USB 3.x permanently removed from FPGA scope. GTP transceivers reserved for potential future use (debug, high-speed test port).

### Decision 4: Controller Platform

| Criterion | NXP i.MX8M Plus (Recommended) | Rockchip RK3588 | NVIDIA Jetson Orin Nano |
|-----------|-------------------------------|-----------------|------------------------|
| CSI-2 RX | Yes (ISP, camera pipeline) | Yes (multi-CSI) | Yes (CSI connector) |
| USB 3.x | USB 3.0 | USB 3.1 | USB 3.2 (10 Gbps) |
| Ethernet | 2x GbE | 2x GbE | 1x GbE |
| 10 GbE | PCIe add-on required | PCIe add-on required | PCIe add-on required |
| Ecosystem | Industrial-grade, long availability | Cost-effective, I/O rich | AI/ML capable |
| Dev maturity | Excellent (Yocto, Linux BSP) | Good (Buildroot/Debian) | Good (JetPack SDK) |

**Decision**: NXP i.MX8M Plus as primary for industrial reliability. RK3588 as backup for cost sensitivity.

---

## 2. System Architecture

### 2.1 Layered Structure

```
[X-ray Panel] -> [Gate IC + ROIC] -> [FPGA: XC7A35T] -> [SoC Controller] -> [Host PC + SDK]
                                           |                    |                    |
                                      Hard RT only         Sequence/Comm       Frame/Display
                                      (Timing FSM,        (SPI control,       (Reassembly,
                                       Line Buffer,        CSI-2 RX,           Storage,
                                       CSI-2 TX)           Ethernet TX)        Display)
```

### 2.2 Data Path & Bandwidth

```
ROIC -> FPGA:     Multi-ch LVDS (4.53 Gbps @ max tier)
FPGA -> SoC:      CSI-2 4-lane D-PHY (~4.0-5.0 Gbps effective on Artix-7) [SOLE PATH]
SoC -> Host:      10 GbE (1.25 GB/s) [RECOMMENDED]
                   1 GbE (125 MB/s) [MINIMUM tier only]
Control Channel:  SPI (up to 50 MHz, bidirectional)
```

### 2.3 FPGA Internal Architecture (XC7A35T-FGG484)

| Block | Function | Clock Domain | Est. LUTs |
|-------|----------|-------------|-----------|
| SPI Slave + Register File | Configuration R/W, FSM control | clk_spi | ~1,500-2,000 |
| Panel Scan Timing FSM | IDLE-INTEGRATE-READOUT-LINE_DONE-FRAME_DONE-ERROR | clk_sys | ~500-1,000 |
| ROIC Interface + Deserializer | Line data collection, LVDS RX | clk_roic | ~1,000-2,000 |
| Line Buffer (Ping-Pong BRAM) | Dual-port write/read isolation | clk_roic / clk_csi2 | ~500-1,000 (+ 3 BRAMs) |
| CSI-2 MIPI TX | Packet builder + D-PHY TX (1/2/4-lane) via OSERDES+LVDS | clk_csi2_byte | ~3,000-5,000 |
| Protection Logic | Timeout, overexposure, error reporting | clk_sys | ~500-1,000 |
| **Total Estimate** | | | **~7,000-12,000 (34-58%)** |

**Resource Budget (40% margin target = max 12,480 LUTs):**
- Estimated utilization: 34-58% -> within budget with margin
- BRAM usage: 3 of 50 blocks (~6%) -> ample headroom
- Clock domains: 4 (reduced from 6, no clk_usb/clk_csi2_esc needed)
- GTP transceivers: 4 available, reserved (not used by CSI-2 D-PHY)

**Register Map**: 7 address ranges (0x00-0x1000+) - USB_CONFIG (0x70-0x7F) removed. CSI2_CONFIG (0x80-0x8F), DATA_IF_STATUS (0x90-0x9F) retained.

**Removed from FPGA scope** (due to XC7A35T constraint):
- USB 3.x TX Controller (Device IP + DMA Engine)
- USB PHY Wrapper (PIPE Interface)
- Data IF MUX (no longer needed with single data path)
- clk_usb, clk_csi2_esc clock domains
- USB_CONFIG register range (0x70-0x7F)
- DATA_IF_SEL control bits
- Error codes 0x10 (USB_LINK_FAIL), 0x20 (USB_TX_TIMEOUT)

### 2.4 SW Module Architecture

```
Solution (10 projects + 8 test projects)
  Common.Dto          <- Shared interfaces (ISimulator, ICodeGenerator, DTOs)
  PanelSimulator      <- Pixel matrix, noise models, defect simulation
  FpgaSimulator       <- SPI registers, FSM, line buffer (golden reference)
  McuSimulator        <- HAL-abstracted firmware logic
  HostSimulator       <- Packet reassembly, frame completion
  ParameterExtractor  <- PDF parsing, rule engine, GUI (C# WPF)
  CodeGenerator       <- FPGA RTL / MCU / Host SDK skeleton generation
  ConfigConverter     <- YAML -> target-specific config transformation
  IntegrationRunner   <- CLI for IT-01~IT-10 scenario execution
  GUI.Application     <- Unified WPF GUI
```

**Dependency Rule**: All modules depend only on Common.Dto, never on each other's implementations.

---

## 3. Functional Requirements (Integrated)

### 3.1 Runtime Product Features (P0)

| ID | Requirement | EARS Specification | Acceptance Criteria |
|----|------------|-------------------|---------------------|
| FR-1 | Panel Scan Timing FSM | When START written to CONTROL, system shall begin scan per MODE (single/continuous/calibration) | FSM state transitions verified for all modes + ERROR recovery |
| FR-2 | Data Interface (SPI control + CSI-2 data) | When line data is ready in buffer, system shall transmit via CSI-2 TX with proper packet framing | CSI-2 packet integrity verified, CRC correct |
| FR-3 | Line Buffer (Ping-Pong BRAM) | When ROIC completes line ADC, store in active bank and set line_ready within 1 line_time | Zero data corruption across bank switches |
| FR-4 | Protection Logic | When readout exceeds timeout, transition to ERROR and set error code | All 8 error codes (0x01-0x80) trigger correctly |
| FR-5 | Frame Streaming (SoC->Host) | When SoC receives complete frame, stream via Ethernet within frame_time | Frame drop rate < 0.01% |
| FR-6 | Host Detector SDK | When all line packets received, reassemble into 2D image array | Missing frame/line detection + raw/TIFF storage |

### 3.2 Development Productivity Features (P0-P2)

| ID | Requirement | Priority | Acceptance Criteria |
|----|------------|----------|---------------------|
| FR-7 | SW-Only Integrated Simulator | P0 | IT-01~IT-06 auto PASS, bit-accurate vs golden model |
| FR-8 | HIL Environment | P1 | Pattern A/B each 1+ scenario, timing deviation <= 5% |
| FR-9 | Parameter Extraction GUI | P1 | Core parameters extract/edit/export from PDF |
| FR-10 | Code Generator | P2 | Generated RTL passes TB, generated C/C# builds+tests |
| FR-11 | Common Config (One Source of Truth) | P0 | detector_config.yaml drives all targets consistently |

---

## 4. Non-Functional Requirements & KPIs

### 4.1 Performance KPIs

| Metric | Target | Measurement Point |
|--------|--------|-------------------|
| Max resolution | >= 3072 x 3072 (16-bit) | M6 |
| Max FPS | >= 30 fps (at target resolution) | M6 |
| Frame drop rate | < 0.01% | M3, M4, M6 |
| Data integrity | Bit-accurate (0 error) | M3, M4, M6 |
| FPGA->SoC transfer latency | <= 1 line_time | M4 |
| CSI-2 effective throughput | >= 1 GB/s (4-lane on Artix-7 OSERDES) | M4 |
| End-to-end latency | <= 3 x frame_time | M6 |

### 4.2 Process KPIs

| Metric | Target | Frequency |
|--------|--------|-----------|
| RTL code coverage | Line >= 95%, Branch >= 90%, FSM 100% | Every build |
| SW unit test coverage | 80-90% per module | Every build |
| CI build success rate | >= 95% | Weekly |
| Code review completion | 100% before merge | Every PR |
| Milestone adherence | >= 80% (within +/- 1 week) | Per milestone |
| Critical issue resolution | <= 5 business days | Weekly |

### 4.3 Simulator Accuracy KPIs

| Metric | Target | When |
|--------|--------|------|
| FpgaSimulator vs RTL output | 100% bit-accurate | After M2 |
| PanelSimulator vs measured RMSE | <= 2 LSB | M6 |
| Timing deviation | <= 5% | M6 |

---

## 5. Development Schedule (Optimized 28-Week Plan)

### 5.1 Key Modification from Original Plan

**Inserted PoC Gate at W3-W6** (most impactful schedule change per research report):
- Validates CSI-2 end-to-end before full RTL commitment
- GO/NO-GO decision on USB 3.x dual support
- De-risks the single highest-impact technical uncertainty

### 5.2 Phase Timeline

```
W1  W2  W3  W4  W5  W6  W7  W8  W9  W10 W11 W12 W13 W14 W15 W16 W17 W18 W19 W20 W21 W22 W23 W24 W25 W26 W27 W28
|===P1: Architecture===|
         |===PoC Gate===|
    |============P2: Simulator Development============|
              |============P3: FPGA RTL Development================|
                   |========P4: MCU/SoC Firmware==============|
                        |========P5: Host SDK Development=========|
                                       |=====P6: Integration Testing=====|
                                            |=====P7: HIL Testing==========|
                                                           |=====P8: System V&V=====|
```

### 5.3 Milestones (Revised)

| Milestone | Week | Gate Criteria | Dependencies |
|-----------|------|--------------|--------------|
| **M0** | **W1** | **P0 decisions locked (performance envelope, host link, primary IF, SoC platform)** | Stakeholder alignment |
| **M0.5** | **W6** | **CSI-2 PoC: >= 70% target throughput measured end-to-end (FPGA TX -> SoC RX -> memory)** | FPGA dev board, SoC dev board, D-PHY adapter |
| M1 | W3 | Architecture review complete, common config schema finalized | M0 |
| M2 | W9 | All simulator modules unit test PASS, coverage targets met | - |
| M3 | W14 | IT-01~IT-06 integration scenarios all PASS | M2 |
| M4 | W18 | HIL Pattern A/B each 1+ scenario PASS, timing deviation <= 5% | M3, hardware boards |
| M5 | W23 | Code generator v1: generated RTL passes TB, generated code builds+tests | M3 |
| M6 | W28 | Real panel frame acquisition, simulator calibration RMSE <= 2 LSB | M4, panel sample |

### 5.4 Phase Details

**Phase 1: Architecture & P0 Decisions (W1-W4)**
- Lock performance envelope (min/target/max)
- Finalize host link selection (1 GbE vs 10 GbE)
- Confirm SoC platform (i.MX8M Plus recommended)
- Approve dual interface strategy (CSI-2 primary)
- Finalize register map, packet structure, common config schema
- Deliverable: Architecture Design Document

**Phase PoC: Interface Validation Gate (W3-W6) [NEW]**
- CSI-2 TX IP instantiation on FPGA eval board
- CSI-2 RX pipeline validation on SoC eval board
- End-to-end throughput measurement (target: >= 70% of full bandwidth)
- Signal integrity validation (D-PHY eye diagram)
- GO/NO-GO decision on USB 3.x dual support
- Deliverable: PoC Report with measured throughput, SI results, recommendation

**Phase 2: Simulator Development (W3-W9)**
- PanelSimulator: pixel model, noise, defects
- FpgaSimulator: SPI, FSM, line buffer (golden reference model)
- McuSimulator: HAL abstraction, sequence logic
- HostSimulator: packet reassembly, frame management
- Methodology: TDD (RED-GREEN-REFACTOR) for all new simulator code
- Deliverable: All simulators with unit tests, coverage >= 85%

**Phase 3: FPGA RTL Development (W5-W14)**
- SPI Slave + Register File
- Panel Scan Timing FSM
- ROIC Interface + Deserializer
- Line Buffer (Ping-Pong BRAM)
- CSI-2 MIPI TX (using vendor IP, informed by PoC results)
- USB 3.x TX Controller (only if PoC gate passes)
- Data IF MUX + Protection Logic
- Cross-verification: rtl_vs_sim_checker (RTL output vs FpgaSimulator golden model)
- Deliverable: Verified RTL with FV-01~FV-14 test coverage

**Phase 4: SoC Controller Firmware (W7-W16)**
- HAL Layer (SPI, USB, CSI-2, Ethernet drivers)
- Sequence Engine (frame sequence control)
- Data Reception Pipeline (CSI-2 primary, USB secondary)
- Network Streaming (Ethernet TX to Host)
- Methodology: TDD for new code, DDD for HAL integration with SoC BSP
- Deliverable: Firmware with unit + integration tests

**Phase 5: Host SDK Development (W8-W18)**
- DetectorClient (network connection management)
- FrameBuffer (frame reassembly from packets)
- PacketProtocol (packet parsing, CRC validation)
- Storage (raw, TIFF, optional DICOM)
- Display (real-time image viewer)
- Deliverable: SDK with unit tests, API documentation

**Phase 6: Integration Testing (W12-W18)**
- IT-01~IT-10 integration scenarios via IntegrationRunner
- SW-only full-chain validation
- Cross-interface scenarios (CSI-2/USB switching)
- Error injection and recovery testing
- Deliverable: All integration tests PASS

**Phase 7: HIL Testing (W14-W22)**
- Pattern A: Real SoC + Virtual FPGA/Panel
- Pattern B: Real FPGA + Virtual Panel
- Real-time timing validation (deviation <= 5%)
- Performance benchmarking at target resolution/FPS
- Deliverable: HIL validation report

**Phase 8: System V&V (W22-W28)**
- Real panel integration
- Simulator calibration (RMSE <= 2 LSB target)
- Full performance validation against KPIs
- Code generator validation
- Final acceptance testing
- Deliverable: System validation report, release package

---

## 6. Risk Management (Integrated)

### 6.1 Risk Register

| ID | Risk | Prob | Impact | Mitigation | Owner | Gate |
|----|------|------|--------|-----------|-------|------|
| R-01 | Panel/ROIC sample procurement delay | Med | High | SW simulator first; alternative panel specs | PM | M6 |
| R-02 | ROIC datasheet insufficient | Med | High | Early vendor FAE contact; emulator-based pre-validation | FPGA Lead | M1 |
| R-03 | FPGA resource (LUT/BRAM) insufficient on XC7A35T | Low | High | Est. 34-58% utilization with CSI-2 only; 40% margin target; Artix-7 75T/100T upgrade path | FPGA Lead | M0.5 |
| R-04 | CSI-2 D-PHY throughput insufficient on Artix-7 OSERDES | Med | High | OSERDES limit ~1.0-1.25 Gbps/lane; external D-PHY PHY chip if max tier needed; PoC measurement | Architect | M0.5 |
| R-05 | Simulator vs real HW mismatch | Med | Med | Phase 8 calibration; RMSE <= 2 LSB target | SW Lead | M6 |
| R-06 | Code generator output quality | Med | Med | Auto verification (build+test) in CI | SW Lead | M5 |
| R-07 | Key personnel departure | Low | High | Documentation; mandatory code review; knowledge sharing | PM | Ongoing |
| R-08 | Schedule delay accumulation | Med | Med | 2-week sprint reviews; milestone Go/No-Go gates | PM | All |
| ~~R-09~~ | ~~USB 3.x PHY/IP stability~~ | - | - | **ELIMINATED: USB 3.x removed from scope (XC7A35T constraint)** | - | - |
| R-10 | CSI-2 D-PHY Signal Integrity on PCB | Med | Med | SI simulation at PCB design; FPC connector spec early lock; OSERDES output drive characterization | HW Lead | M0.5 |
| ~~R-11~~ | ~~Dual IF causing FPGA timing failure~~ | - | - | **ELIMINATED: Single interface (CSI-2 only) simplifies timing** | - | - |
| **R-12** | **Host link bandwidth shortfall** | **Med** | **High** | **P0 decision: 10 GbE or reduce target tier** | **Architect** | **M0** |
| **R-13** | **Medical regulatory gap** | **Low** | **High** | **Establish risk register + secure SDLC framework early** | **PM** | **M1** |
| **R-14** | **Artix-7 OSERDES D-PHY lane speed limit** | **Med** | **Med** | **Max ~1.25 Gbps/lane; 4-lane = ~5 Gbps; sufficient for Target tier; external PHY for Max tier** | **FPGA Lead** | **M0.5** |

### 6.2 Risk-First Resolution Timeline

```
W1-W3:  R-12 (Host link), R-02 (ROIC datasheet) -> P0 decisions
W3-W6:  R-03 (FPGA resources), R-04 (bandwidth), R-09 (USB PHY),
        R-10 (CSI-2 SI), R-11 (dual IF timing) -> PoC Gate
W6-W14: R-01 (panel procurement), R-08 (schedule) -> Sprint reviews
W14+:   R-05 (sim vs HW), R-06 (codegen quality) -> Calibration
```

---

## 7. Resource & Budget Plan

### 7.1 Team Composition (5-7 FTE)

| Role | FTE | Responsibility | Phase Focus |
|------|-----|---------------|-------------|
| System Architect | 1 | Architecture decisions, integration oversight | P1-P8 |
| FPGA Engineer | 1-2 | RTL design, verification, PoC | P1, PoC, P3 |
| Embedded/SoC Engineer | 1 | SoC firmware, HAL, driver development | P4, P7 |
| Application SW Engineer | 1-2 | Simulators, GUI, SDK, code generator | P2, P5, P6 |
| QA/Verification | 0-1 | Test strategy, CI/CD, integration testing | P6-P8 |

### 7.2 Hardware Requirements

| Item | Quantity | When Needed | Purpose |
|------|----------|-------------|---------|
| **Xilinx Artix-7 35T dev board (FGG484)** | **1-2** | **W1** | **RTL development, CSI-2 PoC** |
| SoC dev board (i.MX8M Plus) | 1-2 | W3 | CSI-2 RX PoC, firmware development |
| MIPI D-PHY adapter/FPC | 1-2 | W3 | CSI-2 FPGA TX -> SoC RX connection |
| 10 GbE NIC + switch | 1-2 | W8 | Host link validation |
| X-ray panel sample | 1+ | W22 (best effort earlier) | System V&V |
| Logic analyzer (MIPI decode) | 1 | W3 | CSI-2 protocol debug |

### 7.3 Tool Stack

| Category | Tools |
|----------|-------|
| FPGA | **AMD Vivado** (synthesis + simulation, Artix-7 target) |
| RTL Simulation | ModelSim / Questa |
| SW Development | .NET 8.0+ C# (simulators, GUI, tools), C/C++ (SoC firmware) |
| Version Control | Gitea (6 repositories: fpga, fw, sdk, tools, config, docs) |
| CI/CD | n8n webhooks + Gitea integration |
| Project Management | Redmine |
| FPGA IP | **AMD/Xilinx MIPI CSI-2 TX Subsystem IP** (Artix-7 compatible, D-PHY via OSERDES+LVDS) |

### 7.4 Budget Estimate (from Research Report)

| Category | Estimate | Notes |
|----------|----------|-------|
| Personnel (28 weeks) | Primary cost driver | 5-7 FTE |
| FPGA IP Licensing | Variable | CSI-2 TX, potentially USB 3.x IP |
| Hardware (dev boards, adapters) | Medium | Multiple boards + analyzers |
| Tools (EDA licenses) | Medium-High | Vivado/Quartus + simulation |
| **Total estimate** | **55-80+ billion KRW range** | Research report estimate, varies significantly by scope |

---

## 8. Quality Strategy

### 8.1 Development Methodology (Hybrid)

Per project quality.yaml configuration:

| Code Type | Methodology | Cycle |
|-----------|------------|-------|
| New code (simulators, SDK, tools) | TDD | RED-GREEN-REFACTOR |
| Existing code modifications | DDD | ANALYZE-PRESERVE-IMPROVE |
| FPGA RTL | DDD approach | Characterization tests -> incremental RTL development |

### 8.2 Verification Pyramid

```
Layer 4: System V&V          Real panel integration (M6)
Layer 3: Integration Testing  IT-01~IT-10 via IntegrationRunner (M3)
Layer 2: Unit Testing         FV-01~FV-14 (RTL), xUnit/pytest (SW) (M2)
Layer 1: Static Analysis      RTL lint, CDC check, compile warnings (continuous)
```

### 8.3 Verification Test Cases (FPGA)

| ID | Test | Interface | Coverage |
|----|------|-----------|----------|
| FV-01 | SPI register R/W | SPI | Register access |
| FV-02 | Timing FSM single scan | - | FSM transitions |
| FV-03 | Timing FSM continuous scan | - | Multi-frame |
| FV-04 | Line buffer ping-pong | - | BRAM integrity |
| FV-05 | Error injection & recovery | - | Protection logic |
| FV-06 | SPI + data concurrent | SPI + IF | Dual channel |
| FV-07 | Full frame (multi-line) | - | End-to-end |
| FV-08 | CSI-2 single lane TX | CSI-2 | Basic D-PHY TX via OSERDES |
| FV-09 | CSI-2 multi-lane (2/4) | CSI-2 | Lane scaling, throughput |
| FV-10 | CSI-2 long packet + CRC | CSI-2 | Data integrity, packet framing |
| FV-11 | CSI-2 max throughput stress | CSI-2 | Sustained throughput at target resolution |

### 8.4 Integration Scenarios

| ID | Scenario | Components |
|----|----------|-----------|
| IT-01 | Single frame (CSI-2 path) | Panel -> FPGA -> SoC -> Host (CSI-2) |
| IT-02 | Continuous 100 frames | Full chain, frame drop monitoring |
| IT-03 | Error injection | Timeout, overexposure recovery |
| IT-04 | Calibration mode | Dark/gain correction sequence |
| IT-05 | CSI-2 lane change (1->2->4) | Dynamic lane configuration |
| IT-06 | Target resolution stress | 2048x2048@30fps sustained throughput |
| IT-07 | Max resolution stress | 3072x3072@30fps throughput (D-PHY limit test) |
| IT-08 | Long run stability | 1000+ frames continuous |
| IT-09 | Generated code validation | CodeGen output -> build -> test |
| IT-10 | Artix-7 resource margin | Post-implementation utilization < 60% target |

### 8.5 CI/CD Pipeline

```
Gitea Push -> n8n Webhook ->
  [RTL Pipeline]     RTL Lint -> Simulation (FV-01~14) -> Coverage Report
  [SW Pipeline]      Build -> Unit Test -> Coverage -> Integration Test
  [Config Pipeline]  Schema Validation -> Conversion Check
  -> Redmine Ticket Update -> Dashboard
```

---

## 9. Configuration Management

### 9.1 Git Repository Structure

| Repository | Content | Primary Language |
|-----------|---------|-----------------|
| fpga | RTL source, testbench, constraints | SystemVerilog |
| fw | SoC controller firmware | C/C++ |
| sdk | Host Detector SDK | C++/C# |
| tools | Simulators, GUI, code generator | C# (.NET 8.0+) |
| config | detector_config.yaml, schemas, converters | YAML/JSON |
| docs | Architecture docs, API docs, user guides | Markdown |

### 9.2 Common Configuration (One Source of Truth)

```yaml
# detector_config.yaml (simplified structure)
panel:
  rows: 3072
  cols: 3072
  pixel_pitch_um: 150
  bit_depth: 16

fpga:
  timing: { gate_on_us, gate_off_us, roic_settle_us, adc_conv_us }
  line_buffer: { depth_lines, bram_width_bits }
  data_interface:
    primary: csi2
    usb3: { endpoint, max_packet_size, gen }
    csi2: { lane_count, data_type, virtual_channel }
  spi: { clock_hz, mode }

controller:
  platform: imx8mp
  ethernet: { speed, protocol, port }

host:
  storage: { format, path }
  display: { fps, color_map }
```

---

## 10. Regulatory & Security Considerations

While medical certification (FDA/CE) is out-of-scope for this project, the research report recommends establishing foundational practices now to avoid cost explosion later:

| Practice | Action | When |
|----------|--------|------|
| Risk Register | Establish ISO 14971-compatible risk register framework | M1 |
| Secure SDLC | Apply basic NIST SSDF practices to development | M1 |
| Traceability | Requirements -> Design -> Test traceability matrix | M2+ |
| Data Integrity | Bit-accuracy verification at every interface boundary | Continuous |
| Audit Trail | Git-based change tracking with mandatory code review | Continuous |

---

## 11. Document Traceability

### Source Documents

| Document | Role in This Plan |
|----------|------------------|
| X-ray_Detector_Dev_Plan_Final_v2.md | Primary technical reference (architecture, registers, verification, schedule) |
| deep-research-report.md | Validation layer (bandwidth re-calculation, risk analysis, tech stack comparison, regulatory checklist) |

### Key Improvements Over Original Documents

1. **FPGA device locked: XC7A35T-FGG484** - constrains architecture to CSI-2 only, simplifies design
2. **USB 3.x permanently removed** - infeasible on Artix-7 35T (LUT capacity insufficient), eliminates R-09/R-11 risks
3. **FPGA resource budget added** - estimated 34-58% LUT utilization with upgrade path to Artix-7 75T/100T
4. **D-PHY lane speed constraint identified** - Artix-7 OSERDES limits to ~1.0-1.25 Gbps/lane (R-14 added)
5. **PoC Gate inserted** (W3-W6) - validates CSI-2 end-to-end throughput on actual Artix-7 hardware
6. **Performance envelope 3-tier** - resolves ambiguity; Target tier confirmed feasible on Artix-7
7. **Verification scope reduced** - 14 -> 11 FPGA test cases (USB-specific removed), 10 integration scenarios
8. **"MCU" renamed to "SoC Controller"** - aligns terminology with actual platform class
9. **M0 milestone added** - P0 decisions must be locked before any implementation
10. **Risks optimized** - R-09/R-11 eliminated, R-14 added for OSERDES limit; net risk reduction
11. **Hardware BOM simplified** - USB PHY module and USB analyzer removed from procurement list
12. **Hybrid methodology confirmed** - TDD for new code, DDD for existing/RTL per quality.yaml

### FPGA Upgrade Path

If XC7A35T proves insufficient during development:

| Device | LUTs | BRAMs | GTP | Package Compatible | Notes |
|--------|------|-------|-----|-------------------|-------|
| **XC7A35T** (current) | 20,800 | 50 | 4 | FGG484 | Baseline |
| XC7A50T | 32,600 | 75 | 4 | FGG484 | Pin-compatible upgrade, +57% LUTs |
| XC7A75T | 47,200 | 105 | 8 | FGG484 | Pin-compatible, +127% LUTs |
| XC7A100T | 63,400 | 135 | 8 | FGG484 | Pin-compatible, +205% LUTs |

All Artix-7 devices in FGG484 package are pin-compatible, enabling drop-in upgrade without PCB redesign.

---

*Generated by MoAI Agent Teams (researcher + analyst + architect)*
*Based on: X-ray_Detector_Dev_Plan_Final_v2.md + deep-research-report.md*
*FPGA Constraint: Xilinx Artix-7 XC7A35T-FGG484*
*Date: 2026-02-16*
