# Verification Strategy

**Project**: X-ray Detector Panel System
**Document Version**: 1.0.0
**Last Updated**: 2026-02-17

---

## Overview

This document defines the overall verification approach, mapping the verification pyramid to project milestones and defining quality KPIs.

---

## Verification Pyramid

```
                    +-----------------------+
                    |  Layer 4: System V&V  |    M6 (W28)
                    |  Real panel, clinical |    Actual X-ray panel integration
                    +-----------+-----------+
                                |
                   +------------+------------+
                   | Layer 3: Integration    |    M3-M4 (W14-W18)
                   | IT-01~IT-10, HIL A/B   |    End-to-end pipeline, HW/SW
                   +------------+------------+
                                |
              +-----------------+------------------+
              |   Layer 2: Unit Tests              |    M2 (W9)
              |   FV-01~FV-11 (RTL)               |    Module-level verification
              |   SW-01~SW-08 (xUnit)              |
              +-----------------+------------------+
                                |
         +----------------------+----------------------+
         |     Layer 1: Static Analysis               |    Continuous
         |     RTL lint, CDC checks, compile warnings  |    Every commit
         |     C# analyzers, code style               |
         +---------------------------------------------+
```

---

## Layer Details

### Layer 1: Static Analysis (Continuous)

**When**: Every commit, CI pipeline
**Tools**: Vivado RTL lint, C# Roslyn analyzers, ruff (Python)

| Check | Tool | Threshold | Action on Failure |
|-------|------|-----------|-------------------|
| RTL syntax | Vivado `synth_design` | Zero errors | Block merge |
| RTL lint | Vivado lint rules | Zero warnings (critical) | Block merge |
| CDC (Clock Domain Crossing) | Vivado CDC report | Zero violations | Block merge |
| C# compilation | `dotnet build` | Zero errors | Block merge |
| C# analyzers | Roslyn (CA rules) | Zero errors, max 10 warnings | Block merge on errors |
| Code formatting | `dotnet format` / ruff | No diff | Auto-fix in CI |

---

### Layer 2: Unit Tests (M2 Gate)

**When**: Every commit (SW), per-build (RTL)
**Test Plans**: `unit-test-plan.md`

| Domain | Tests | Coverage Target | Gate |
|--------|-------|----------------|------|
| FPGA RTL | FV-01 to FV-11 | Line >= 95%, Branch >= 90%, FSM 100% | M2 |
| PanelSimulator | SW-01 | 80-90% | M2 |
| FpgaSimulator | SW-02 | 80-90% | M2 |
| McuSimulator | SW-03 | 80-90% | M2 |
| HostSimulator | SW-04 | 80-90% | M2 |
| ParameterExtractor | SW-05 | 80-90% | M2 |
| CodeGenerator | SW-06 | 80-90% | M2 |
| ConfigConverter | SW-07 | 80-90% | M2 |
| IntegrationRunner | SW-08 | 80-90% | M2 |

---

### Layer 3: Integration Tests (M3-M4 Gates)

**When**: After all unit tests pass
**Test Plans**: `integration-test-plan.md`, `hil-test-plan.md`

| Type | Tests | Gate | Hardware Required |
|------|-------|------|-------------------|
| Software Integration | IT-01 to IT-10 | M3 (W14) | No (simulators only) |
| HIL Pattern A | HIL-A-01 to HIL-A-04 | M4 (W18) | FPGA + SoC + Host |
| HIL Pattern B | HIL-B-01 to HIL-B-06 | M4 (W18) | FPGA + SoC + Host |

---

### Layer 4: System V&V (M6 Gate)

**When**: After HIL tests pass, real X-ray panel available
**Scope**: End-to-end validation with actual X-ray detector panel

| Test | Description | Pass Criteria |
|------|-------------|---------------|
| SYS-01 | Flat-field image capture | Uniform response, noise < 1% CV |
| SYS-02 | Dark frame calibration | Baseline noise characterization |
| SYS-03 | Resolution target | MTF meets clinical requirement |
| SYS-04 | Simulator correlation | Simulator output matches HW within 5% |
| SYS-05 | Full clinical workflow | Capture -> process -> store -> display |

---

## Quality KPIs

### Data Quality KPIs

| KPI | Target | Measurement | Milestone |
|-----|--------|-------------|-----------|
| Bit Error Rate | 0 | Frame comparison (expected vs actual) | M2+ |
| Frame Drop Rate | < 0.01% | Sequence number gaps / total frames | M3+ |
| Data Integrity | 100% (zero errors) | CRC validation across all interfaces | M2+ |
| CSI-2 Throughput | >= 1.01 Gbps (Intermediate-A) | Measured bytes / time | M4 |

### Code Quality KPIs

| KPI | Target | Measurement | Milestone |
|-----|--------|-------------|-----------|
| RTL Line Coverage | >= 95% | Vivado coverage report | M2 |
| RTL Branch Coverage | >= 90% | Vivado coverage report | M2 |
| RTL FSM Coverage | 100% | State/transition coverage | M2 |
| SW Unit Coverage | 80-90% per module | xUnit + coverlet | M2 |
| Overall Coverage | >= 85% | Weighted average | M3 |
| Static Analysis | Zero critical issues | CI pipeline | Continuous |

### System Performance KPIs

| KPI | Target | Measurement | Milestone |
|-----|--------|-------------|-----------|
| Command Latency | < 10 ms (SPI round-trip) | Time from SDK command to FPGA response | M4 |
| Frame Latency | < 100 ms (capture to display) | Timestamp delta end-to-end | M4 |
| FPGA LUT Utilization | < 60% | Vivado utilization report | M2 |
| FPGA BRAM Utilization | < 50% | Vivado utilization report | M2 |
| Memory Stability | Zero leaks | 1-hour continuous test | M4 |

---

## Traceability Matrix

### Requirements to Tests

| Requirement Area | Unit Tests | Integration Tests | HIL Tests |
|-----------------|------------|-------------------|-----------|
| Panel Scan FSM | FV-01 | IT-06 | HIL-B-01, HIL-B-02 |
| Line Buffer | FV-02 | IT-01 | HIL-A-01 |
| CSI-2 TX | FV-03 | IT-01, IT-03 | HIL-A-01, HIL-A-02 |
| SPI Control | FV-04 | IT-06 | HIL-B-01 |
| Protection Logic | FV-05 | IT-07 | HIL-B-03 |
| PanelSimulator | SW-01 | IT-05 | N/A |
| FpgaSimulator | SW-02 | IT-01~IT-04 | N/A |
| McuSimulator | SW-03 | IT-01~IT-04 | N/A |
| HostSimulator | SW-04 | IT-01~IT-04 | N/A |
| ConfigConverter | SW-07 | IT-08 | N/A |
| Storage Formats | SW-04 | IT-10 | N/A |

### Milestones to Verification Gates

| Milestone | Week | Required Verification |
|-----------|------|-----------------------|
| M0 | W1 | SPEC documents approved |
| M0.5 | W26 | CSI-2 PoC pass (SPEC-POC-001) |
| M1 | W3 | Architecture review, schema validated |
| M2 | W9 | All unit tests pass (FV-01~11, SW-01~08) |
| M3 | W14 | IT-01~IT-06 pass (P0 scenarios) |
| M4 | W18 | HIL Pattern A/B core scenarios pass |
| M5 | W23 | Code generator RTL passes testbench |
| M6 | W28 | System V&V with real panel |

---

## Risk-Based Testing Priority

Tests are prioritized based on risk analysis:

| Priority | Criteria | Examples |
|----------|----------|---------|
| P0 (Critical) | Data integrity, safety | FV-01 (FSM), IT-01 (data path), HIL-A-02 (streaming) |
| P1 (High) | Performance, reliability | IT-04 (bandwidth), HIL-B-05 (thermal) |
| P2 (Medium) | Usability, robustness | IT-08 (reconfig), HIL-B-06 (network) |
| P3 (Low) | Nice-to-have, edge cases | IT-10 (multi-format), Optional SI tests |

---

## Development Methodology Mapping

### Hybrid Methodology Application

| Code Type | Methodology | Test Timing | Coverage Target |
|-----------|-------------|-------------|-----------------|
| New simulators (C#) | TDD | Write test BEFORE code | 85% |
| New SDK modules (C#) | TDD | Write test BEFORE code | 85% |
| New tools (C#) | TDD | Write test BEFORE code | 85% |
| FPGA RTL (existing IP) | DDD | Characterization test first | 95% line |
| Firmware HAL (existing) | DDD | Characterization test first | 85% |
| New RTL modules | Hybrid (TDD) | Test-first for new blocks | 95% line |

### Test-First Enforcement

For TDD modules:
1. Write failing test (RED)
2. Implement minimal code to pass (GREEN)
3. Refactor while keeping tests green (REFACTOR)
4. Coverage must increase monotonically per commit

For DDD modules:
1. Analyze existing behavior (ANALYZE)
2. Write characterization tests (PRESERVE)
3. Modify code with test validation (IMPROVE)
4. Characterization tests must not regress

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (analyst) | Initial verification strategy |

---
