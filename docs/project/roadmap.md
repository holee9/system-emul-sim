# Project Roadmap

**Project**: X-ray Detector Panel System
**Duration**: 28 weeks (W1-W28)
**Version**: 1.0.0
**Last Updated**: 2026-02-17

---

## 1. Milestone Overview

```
W1  W2  W3  W4  W5  W6  W7  W8  W9  W10 W11 W12 W13 W14 W15 W16 W17 W18 W19 W20 W21 W22 W23 W24 W25 W26 W27 W28
|===M0===|==M1==|                                                                                                |
|        |======== PoC (M0.5) ========|                                                                          |
|        |================ P2: Simulators (M2) =============|                                                    |
|                    |======================== P3: FPGA RTL (M3) ========================|                       |
|                              |========================= P4: SoC FW ========================|                  |
|                                    |============================ P5: Host SDK ==================|              |
|                                                        |========== P6: Integration (M3) =======|              |
|                                                                    |============ P7: HIL (M4) ============|   |
|                                                                                            |== P8: V&V (M6) ==|
```

---

## 2. Milestones

### M0: Project Kickoff (W1)

**Status**: In Progress

**Deliverables**:
- P0 critical decisions locked (performance tier, Host link, SoC platform)
- Architecture design documents completed
- SPEC documents for all subsystems
- Development environment setup verified
- `detector_config.yaml` schema defined

**Gate Criteria**:
- All P0 decisions documented and approved
- Architecture review meeting held

---

### M0.5: CSI-2 PoC Gate (W6)

**Status**: Planned

**Objective**: Validate CSI-2 MIPI D-PHY interface between FPGA and SoC

**Deliverables**:
- FPGA CSI-2 TX transmitting test pattern at 400 Mbps/lane (stable)
- SoC CSI-2 RX capturing frames via V4L2
- Lane speed sweep: 400M, 600M, 800M, 1000M, 1250M Mbps/lane
- Throughput measurement >= 70% of target tier
- Bit error rate measurement

**Gate Criteria**:
- CSI-2 at 400 Mbps/lane: zero bit errors in 1000 frames
- Throughput measurement report completed
- Go/No-Go decision on 800M lane speed

**Risk**: If 800M debugging fails, fall back to Intermediate-A tier (2048x2048@15fps)

---

### M1: Architecture Review (W3)

**Status**: Planned

**Deliverables**:
- Architecture review completed with all stakeholders
- Configuration schema (`detector-config-schema.json`) finalized
- Register map frozen for firmware development
- Interface specifications (CSI-2, SPI, Ethernet) documented
- Development toolchain verified for all layers

**Gate Criteria**:
- Architecture document approved
- No open P0 decisions
- All interface specifications documented

---

### M2: Simulator Suite Complete (W9)

**Status**: Planned

**Deliverables**:
- PanelSimulator: pixel generation with noise and defect models
- FpgaSimulator: complete register map, FSM, line buffer, CSI-2 TX (golden reference)
- McuSimulator: CSI-2 RX, SPI master, Ethernet TX
- HostSimulator: UDP reception, frame reassembly, TIFF/RAW storage
- Common.Dto: shared interfaces and DTOs
- Unit test coverage: 80-90% per module

**Gate Criteria**:
- All simulator unit tests pass
- Coverage >= 80% per module
- FpgaSimulator validated as golden reference

---

### M3: Integration Tests Pass (W14)

**Status**: Planned

**Deliverables**:
- IT-01 through IT-06 integration scenarios pass
- IntegrationRunner CLI functional
- FPGA RTL synthesis and simulation complete
- Full pipeline data integrity verified (bit-exact)

**Gate Criteria**:
- IT-01~IT-06: all pass with zero bit errors
- FPGA LUT < 60%, WNS >= 1 ns
- Pipeline throughput >= Intermediate-A tier (1.01 Gbps)

---

### M4: HIL Validation (W18)

**Status**: Planned

**Deliverables**:
- HIL Pattern A: FPGA + SoC data integrity test (at least 1 scenario pass)
- HIL Pattern B: Full system (FPGA + SoC + Host) streaming test
- IT-07 through IT-10 integration scenarios pass
- Frame drop rate < 0.01% in 1-hour continuous test

**Gate Criteria**:
- HIL Pattern A: zero bit errors in 1000 frames
- HIL Pattern B: continuous streaming stable for 1 hour
- Frame drop rate < 0.01%

---

### M5: Code Generator v1 (W23)

**Status**: Planned

**Deliverables**:
- CodeGenerator produces RTL skeletons that compile in Vivado
- CodeGenerator produces firmware headers that compile with GCC
- CodeGenerator produces SDK classes that compile with dotnet
- ConfigConverter generates .xdc, .dts, .json from detector_config.yaml
- ParameterExtractor GUI functional for PDF parsing

**Gate Criteria**:
- All generated code compiles without errors
- ConfigConverter round-trip test passes
- ParameterExtractor extracts parameters with >= 90% accuracy

---

### M6: System Validation (W28)

**Status**: Planned

**Deliverables**:
- Real X-ray detector panel frame acquisition
- Simulator calibration against real hardware data
- Complete system validation report
- Production deployment documentation
- Final release package (v1.0.0)

**Gate Criteria**:
- Real panel frame captured, stored, and displayed
- Simulator output matches real hardware within tolerance
- All documentation complete and reviewed
- Release package tested in staging environment

---

## 3. Phase Details

### Phase 1: Architecture Design (W1-W3)

| Week | Activities |
|------|-----------|
| W1 | P0 decisions, project setup, environment verification |
| W2 | Architecture design documents, SPEC writing |
| W3 | Architecture review, config schema finalization |

### Phase 2: Simulator Development (W3-W9)

| Week | Activities |
|------|-----------|
| W3-W4 | Common.Dto interfaces, PanelSimulator (TDD) |
| W5-W6 | FpgaSimulator: register file, FSM (TDD) |
| W7-W8 | McuSimulator, HostSimulator (TDD) |
| W9 | Integration, coverage analysis, M2 gate |

### CSI-2 PoC (W3-W6)

| Week | Activities |
|------|-----------|
| W3 | FPGA CSI-2 TX IP integration, 400M baseline |
| W4 | SoC CSI-2 RX driver, V4L2 capture |
| W5 | Lane speed sweep testing (400M-1250M) |
| W6 | Throughput measurement, PoC report, M0.5 gate |

### Phase 3: FPGA RTL Development (W5-W14)

| Week | Activities |
|------|-----------|
| W5-W6 | Panel Scan FSM, clock generation (DDD) |
| W7-W8 | Line buffer, CDC synchronizers |
| W9-W10 | CSI-2 TX wrapper, CRC-16 engine |
| W11-W12 | SPI slave, register map |
| W13 | Protection logic, error handling |
| W14 | Integration, timing closure, M3 gate |

### Phase 4: SoC Firmware (W7-W16)

| Week | Activities |
|------|-----------|
| W7-W8 | HAL: SPI master, V4L2 CSI-2 RX driver |
| W9-W10 | Sequence engine, frame buffer manager |
| W11-W12 | Ethernet TX, UDP packet builder |
| W13-W14 | Command protocol, host integration |
| W15-W16 | Testing, optimization, M3 support |

### Phase 5: Host SDK (W8-W18)

| Week | Activities |
|------|-----------|
| W8-W10 | DetectorClient API, PacketReceiver (TDD) |
| W11-W12 | FrameReassembler, CRC-16 validation |
| W13-W14 | Storage: TIFF, RAW writers |
| W15-W16 | GUI viewer: WPF WriteableBitmap, Window/Level |
| W17-W18 | Integration testing, optimization |

### Phase 6: Integration Testing (W12-W18)

| Week | Activities |
|------|-----------|
| W12-W13 | IT-01~IT-04: basic pipeline, data integrity |
| W14-W15 | IT-05~IT-06: CRC validation, frame drops |
| W16-W17 | IT-07~IT-10: error handling, performance |
| W18 | Full integration report, M4 preparation |

### Phase 7: HIL Testing (W14-W22)

| Week | Activities |
|------|-----------|
| W14-W16 | HIL Pattern A: FPGA + SoC data integrity |
| W17-W18 | HIL Pattern B: full system streaming (M4 gate) |
| W19-W20 | Extended endurance testing (1-hour continuous) |
| W21-W22 | Performance optimization, edge case testing |

### Phase 8: System V&V (W22-W28)

| Week | Activities |
|------|-----------|
| W22-W23 | CodeGenerator and tools finalization (M5 gate) |
| W24-W25 | Real panel integration and calibration |
| W26-W27 | System validation testing |
| W28 | Final release, documentation, M6 gate |

---

## 4. Key Feature Schedule

| Feature | Start | Complete | Milestone |
|---------|-------|----------|-----------|
| Architecture documents | W1 | W3 | M1 |
| CSI-2 PoC (400M stable) | W3 | W6 | M0.5 |
| PanelSimulator | W3 | W5 | M2 |
| FpgaSimulator (golden ref) | W4 | W8 | M2 |
| McuSimulator | W6 | W8 | M2 |
| HostSimulator | W7 | W9 | M2 |
| FPGA Panel Scan FSM | W5 | W8 | M3 |
| FPGA CSI-2 TX | W8 | W12 | M3 |
| FPGA SPI Slave | W10 | W12 | M3 |
| SoC CSI-2 RX Driver | W7 | W10 | M4 |
| SoC Sequence Engine | W9 | W12 | M4 |
| Host SDK DetectorClient | W8 | W14 | M4 |
| Integration Tests (IT-01~10) | W12 | W18 | M3/M4 |
| HIL Testing | W14 | W22 | M4 |
| CodeGenerator | W18 | W23 | M5 |
| ConfigConverter | W18 | W23 | M5 |
| Real Panel Integration | W24 | W28 | M6 |

---

## 5. Risk-Adjusted Timeline

### High-Risk Items

| Risk | Impact | Mitigation | Schedule Buffer |
|------|--------|-----------|----------------|
| CSI-2 800M debugging | Target tier blocked | Fall back to Intermediate-A | W4 decision point |
| FPGA LUT > 60% | Feature reduction needed | Optimize or upgrade to XC7A50T | W10 checkpoint |
| V4L2 driver instability | SoC firmware delays | Pipeline restart, alternative SoC | +2 weeks buffer |
| 10 GbE availability | Host link bandwidth | Compression or reduced tier | W6 decision point |

### Schedule Contingency

- 2-week buffer built into Phases 7-8
- Phases 3-5 have partial overlap for risk absorption
- PoC gate at W6 enables early course correction

---

## 6. Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | ABYZ-Lab Agent (architect) | Initial project roadmap |

---
