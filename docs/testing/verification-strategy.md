# Verification Strategy

**Project**: X-ray Detector Panel System
**Document Version**: 2.0.0
**Last Updated**: 2026-02-17
**Status**: Reviewed - Approved

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
              |   Layer 2: Unit Tests              |    M2-M3 (W9-W16)
              |   FV-01~FV-11 (RTL, 97 tests)     |    Module-level verification
              |   SW-01~SW-09 (Simulators, 56 tests)|    254 total test cases
              |   FW-UT-01~08 (Firmware, 68 tests) |
              |   SDK-01~SDK-05 (Host SDK, 38 tests)|
              +-----------------+------------------+
                                |
         +----------------------+----------------------+
         |     Layer 1: Static Analysis               |    Continuous
         |     RTL lint, CDC checks, compile warnings  |    Every commit
         |     C# analyzers, FW gcc -Wall, cppcheck   |
         +---------------------------------------------+
```

---

## Layer Details

### Layer 1: Static Analysis (Continuous)

**When**: Every commit, CI pipeline
**Tools**: Vivado RTL lint, C# Roslyn analyzers, gcc/cppcheck (firmware), ruff (Python)

| Check | Tool | Threshold | Action on Failure |
|-------|------|-----------|-------------------|
| RTL syntax | Vivado `synth_design` | Zero errors | Block merge |
| RTL lint | Vivado lint rules | Zero warnings (critical) | Block merge |
| CDC (Clock Domain Crossing) | Vivado CDC report | Zero violations | Block merge |
| C# compilation | `dotnet build` | Zero errors | Block merge |
| C# analyzers | Roslyn (CA rules) | Zero errors, max 10 warnings | Block merge on errors |
| C# code formatting | `dotnet format` / EditorConfig | No diff | Auto-fix in CI |
| FW compilation | `aarch64-poky-linux-gcc -Wall -Werror` | Zero errors, zero warnings | Block merge |
| FW static analysis | cppcheck (Yocto SDK) | Zero errors | Block merge |
| FW MISRA compliance | cppcheck --addon=misra (advisory) | Report only | Informational |

---

### Layer 2: Unit Tests (M2-M3 Gates)

**When**: Every commit (SW/FW), per-build (RTL)
**Test Plans**: `unit-test-plan.md` (v2.0.0, 254 test cases)

#### FPGA RTL (Vivado Simulator / ModelSim / Questa)

| Domain | Tests | Test Count | Coverage Target | Gate |
|--------|-------|-----------|----------------|------|
| Panel Scan FSM | FV-01 | 11 | Line >= 95%, Branch >= 90%, FSM 100% | M2 |
| Line Buffer | FV-02 | 8 | Line >= 95%, Branch >= 90% | M2 |
| CSI-2 TX | FV-03 | 10 | Line >= 95%, Branch >= 90% | M2 |
| SPI Slave | FV-04 | 8 | Line >= 95%, Branch >= 90% | M2 |
| Protection Logic | FV-05 | 10 | Line >= 95%, Branch >= 90% | M2 |
| Clock Manager | FV-06 | 8 | Line >= 95%, Branch >= 90% | M2 |
| Reset Controller | FV-07 | 8 | Line >= 95%, Branch >= 90% | M2 |
| D-PHY Serializer | FV-08 | 8 | Line >= 95%, Branch >= 90% | M2 |
| Frame Timing Gen | FV-09 | 8 | Line >= 95%, Branch >= 90% | M2 |
| Test Pattern Gen | FV-10 | 8 | Line >= 95%, Branch >= 90% | M2 |
| Top-Level Integration | FV-11 | 8 | Line >= 95%, Branch >= 90%, FSM 100% | M2 |

**RTL Subtotal**: 97 test cases across 11 modules

#### Simulators and Tools (xUnit, .NET 8.0+)

| Domain | Tests | Test Count | Coverage Target | Gate |
|--------|-------|-----------|----------------|------|
| PanelSimulator | SW-01 | 8 | 85%+ | M2 |
| FpgaSimulator | SW-02 | 8 | 85%+ | M2 |
| McuSimulator | SW-03 | 7 | 85%+ | M2 |
| HostSimulator | SW-04 | 8 | 85%+ | M2 |
| ParameterExtractor | SW-05 | 4 | 85%+ | M2 |
| CodeGenerator | SW-06 | 4 | 85%+ | M2 |
| ConfigConverter | SW-07 | 7 | 85%+ | M2 |
| IntegrationRunner | SW-08 | 4 | 85%+ | M2 |
| Common.Dto | SW-09 | 3 | 85%+ | M2 |

**Simulator/Tools Subtotal**: 53 test cases across 9 modules

#### SoC Firmware (CMocka / Unity, C)

| Domain | Tests | Test Count | Coverage Target | Gate |
|--------|-------|-----------|----------------|------|
| SPI Master HAL | FW-UT-01 | 8 | 85%+ | M3 |
| Frame Header | FW-UT-02 | 8 | 85%+ | M3 |
| CRC-16 | FW-UT-03 | 6 | 85%+ | M3 |
| Config YAML Parser | FW-UT-04 | 10 | 85%+ | M3 |
| Sequence Engine FSM | FW-UT-05 | 13 | 85%+ | M3 |
| Frame Buffer Manager | FW-UT-06 | 9 | 85%+ | M3 |
| Command Protocol | FW-UT-07 | 8 | 85%+ | M3 |
| Health Monitor | FW-UT-08 | 6 | 85%+ | M3 |

**Firmware Subtotal**: 68 test cases across 8 modules

#### Host SDK (xUnit, .NET 8.0+)

| Domain | Tests | Test Count | Coverage Target | Gate |
|--------|-------|-----------|----------------|------|
| PacketReceiver | SDK-01 | 8 | 85%+ | M3 |
| FrameReassembler | SDK-02 | 8 | 85%+ | M3 |
| DetectorClient API | SDK-03 | 10 | 85%+ | M3 |
| ImageEncoder | SDK-04 | 7 | 85%+ | M3 |
| Frame Memory Mgmt | SDK-05 | 5 | 85%+ | M3 |

**SDK Subtotal**: 38 test cases across 5 modules

**Grand Total**: 254 unit test cases (with 2 additional test cases for Common.Dto = 256)

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
| Simulator/Tools Coverage | >= 85% per module | xUnit + coverlet | M2 |
| FW Unit Coverage | >= 85% per module | CMocka + gcov + lcov | M3 |
| SDK Unit Coverage | >= 85% per module | xUnit + coverlet | M3 |
| Overall Coverage | >= 85% | Weighted average all domains | M3 |
| Static Analysis | Zero critical issues | CI pipeline (all domains) | Continuous |
| FW Static Analysis | Zero cppcheck errors | cppcheck in CI | Continuous |

### System Performance KPIs

| KPI | Target | Measurement | Milestone |
|-----|--------|-------------|-----------|
| SPI Round-trip Latency | < 10 ms | Time from SoC command to FPGA response | M4 |
| SPI Polling Jitter (avg) | 100 us +/- 10 us | High-resolution timer, SCHED_FIFO | M3 |
| SPI Polling Jitter (P99) | < 500 us | 99th percentile, 10,000 cycles | M3 |
| FW Frame Latency | < 45 ms (CSI-2 RX to UDP TX) | Timestamp at each stage | M3 |
| End-to-End Latency | < 100 ms (capture to Host display) | Timestamp delta full pipeline | M4 |
| FPGA LUT Utilization | < 60% | Vivado utilization report | M2 |
| FPGA BRAM Utilization | < 50% | Vivado utilization report | M2 |
| FW CPU Utilization | < 80% (during sustained scan) | top/perf on target hardware | M4 |
| SDK GC Pressure | < 5 Gen2 GCs per 10,000 frames | dotnet-trace GC event monitor | M3 |
| Memory Stability | Zero leaks (FW + SDK) | 1-hour continuous test, valgrind (FW) | M4 |

---

## Traceability Matrix

### Requirements to Tests

#### FPGA RTL (SPEC-FPGA-001)

| Requirement Area | SPEC Requirements | Unit Tests | Integration Tests | HIL Tests |
|-----------------|-------------------|------------|-------------------|-----------|
| Panel Scan FSM | REQ-FPGA-010~016 | FV-01 (11 tests) | IT-06 | HIL-B-01, HIL-B-02 |
| Line Buffer | REQ-FPGA-020~024 | FV-02 (8 tests) | IT-01 | HIL-A-01 |
| CSI-2 TX | REQ-FPGA-030~036 | FV-03 (10 tests) | IT-01, IT-03 | HIL-A-01, HIL-A-02 |
| SPI Slave | REQ-FPGA-040~044 | FV-04 (8 tests) | IT-06 | HIL-B-01 |
| Protection Logic | REQ-FPGA-050~054 | FV-05 (10 tests) | IT-07 | HIL-B-03 |
| Clock Manager | REQ-FPGA-060 | FV-06 (8 tests) | FV-11 | HIL-A-01 |
| Reset Controller | REQ-FPGA-062 | FV-07 (8 tests) | FV-11 | HIL-B-01 |
| D-PHY Serializer | REQ-FPGA-035 | FV-08 (8 tests) | IT-03 | HIL-A-02 |
| Frame Timing Gen | REQ-FPGA-012~015 | FV-09 (8 tests) | IT-06 | HIL-B-02 |
| Test Pattern Gen | (test infrastructure) | FV-10 (8 tests) | IT-01~IT-04 | HIL-A-01 |
| Top-Level | (all REQs) | FV-11 (8 tests) | IT-01~IT-06 | HIL-A/B |

#### Simulators and Tools (SPEC-SIM-001)

| Requirement Area | SPEC Requirements | Unit Tests | Integration Tests | HIL Tests |
|-----------------|-------------------|------------|-------------------|-----------|
| PanelSimulator | REQ-SIM-010~014 | SW-01 (8 tests) | IT-05 | N/A |
| FpgaSimulator | REQ-SIM-020~026 | SW-02 (8 tests) | IT-01~IT-04 | N/A |
| McuSimulator | REQ-SIM-030~034 | SW-03 (7 tests) | IT-01~IT-04 | N/A |
| HostSimulator | REQ-SIM-040~044 | SW-04 (8 tests) | IT-01~IT-04 | N/A |
| Common.Dto | REQ-SIM-050~052 | SW-09 (3 tests) | IT-01~IT-10 | N/A |
| ConfigConverter | (SPEC-TOOLS-001) | SW-07 (7 tests) | IT-08 | N/A |
| CodeGenerator | (SPEC-TOOLS-001) | SW-06 (4 tests) | IT-09 | N/A |
| ParameterExtractor | (SPEC-TOOLS-001) | SW-05 (4 tests) | N/A | N/A |
| IntegrationRunner | (SPEC-TOOLS-001) | SW-08 (4 tests) | IT-01~IT-10 | N/A |

#### SoC Firmware (SPEC-FW-001)

| Requirement Area | SPEC Requirements | Unit Tests | Integration Tests | HIL Tests |
|-----------------|-------------------|------------|-------------------|-----------|
| SPI Master HAL | REQ-FW-020~023 | FW-UT-01 (8 tests) | FW-IT-02 | HIL-B-01 |
| Frame Header | REQ-FW-040~042 | FW-UT-02 (8 tests) | FW-IT-03 | HIL-A-02 |
| CRC-16 | REQ-FW-042 | FW-UT-03 (6 tests) | FW-IT-03 | N/A |
| Config Parser | REQ-FW-003, REQ-FW-130~131 | FW-UT-04 (10 tests) | N/A | N/A |
| Sequence Engine | REQ-FW-030~033 | FW-UT-05 (13 tests) | FW-IT-03 | HIL-B-01, HIL-B-02 |
| Frame Buffer Mgr | REQ-FW-050~052 | FW-UT-06 (9 tests) | FW-IT-04 | HIL-B-05 |
| Command Protocol | REQ-FW-043, REQ-FW-100~101 | FW-UT-07 (8 tests) | FW-IT-05 | HIL-B-06 |
| Health Monitor | REQ-FW-060, REQ-FW-110~112 | FW-UT-08 (6 tests) | FW-IT-05 | HIL-B-05 |

#### Host SDK (SPEC-SDK-001)

| Requirement Area | SPEC Requirements | Unit Tests | Integration Tests | HIL Tests |
|-----------------|-------------------|------------|-------------------|-----------|
| PacketReceiver | REQ-SDK-010, REQ-SDK-030 | SDK-01 (8 tests) | IT-01~IT-04 | HIL-A-02 |
| FrameReassembler | REQ-SDK-013~015, REQ-SDK-032 | SDK-02 (8 tests) | IT-01~IT-04 | HIL-A-02 |
| DetectorClient API | REQ-SDK-002, REQ-SDK-010~019 | SDK-03 (10 tests) | IT-01, IT-10 | HIL-B-06 |
| ImageEncoder | REQ-SDK-017, REQ-SDK-023~024 | SDK-04 (7 tests) | IT-10 | N/A |
| Frame Memory | REQ-SDK-004, REQ-SDK-031 | SDK-05 (5 tests) | IT-04 | HIL-B-05 |

### Milestones to Verification Gates

| Milestone | Week | Required Verification |
|-----------|------|-----------------------|
| M0 | W1 | All SPEC documents approved (FPGA, FW, SDK, SIM, POC, TOOLS) |
| M1 | W3 | Architecture review, schema validated, static analysis baseline |
| M2 | W9 | RTL unit tests pass (FV-01~11, 97 tests), Simulator unit tests pass (SW-01~09, 53 tests) |
| M3 | W14 | FW unit tests pass (FW-UT-01~08, 68 tests), SDK unit tests pass (SDK-01~05, 38 tests), IT-01~IT-06 pass |
| M3.5 | W16 | IT-07~IT-10 pass, FW-IT-01~05 pass |
| M4 | W18 | HIL Pattern A/B core scenarios pass |
| M0.5 | W26 | CSI-2 PoC pass (SPEC-POC-001) |
| M5 | W23 | Code generator RTL passes testbench |
| M6 | W28 | System V&V with real panel (SYS-01~05) |

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

| Code Type | Domain | Methodology | Test Timing | Coverage Target |
|-----------|--------|-------------|-------------|-----------------|
| New simulators (C#) | SPEC-SIM-001 | TDD | Write test BEFORE code | 85% |
| New SDK modules (C#) | SPEC-SDK-001 | TDD | Write test BEFORE code | 85% |
| New tools (C#) | SPEC-TOOLS-001 | TDD | Write test BEFORE code | 85% |
| FPGA RTL (existing IP) | SPEC-FPGA-001 | DDD | Characterization test first | 95% line |
| New RTL modules | SPEC-FPGA-001 | Hybrid (TDD) | Test-first for new blocks | 95% line |
| New FW modules (C) | SPEC-FW-001 | TDD | Write test BEFORE code | 85% |
| FW HAL integration | SPEC-FW-001 | DDD | Characterization test first | 85% |
| BQ40z50 driver port | SPEC-FW-001 | DDD | ANALYZE 4.4, PRESERVE, IMPROVE | 85% |

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

## 2.5 GbE Driver Verification Plan

The on-board 2.5 GbE chip model is TBD (requires `lspci -nn` on target hardware). This section defines the verification steps to identify the chip and validate the driver before any network performance testing.

**Milestone**: M1 (W3) - chip identification; M4 (W18) - throughput validation

### Step 1: Chip Identification

**When**: W1-W2 (Yocto Scarthgap migration, Day 7)
**Method**: Execute on VAR-SOM-MX8M-PLUS hardware with Scarthgap image booted:

```sh
lspci -nn | grep -i ethernet
lspci -nn | grep -i network
# Also check RGMII-connected chips (may not appear in lspci):
cat /sys/class/net/*/device/uevent 2>/dev/null | grep -i modalias
dmesg | grep -i eth
```

**Pass Criteria**: Chip vendor and model identified (e.g., Realtek RTL8125, Intel I225, Aquantia AQC107).
**Action on Failure**: Escalate - chip identification is a hard dependency for all subsequent network verification.

---

### Step 2: Kernel 6.6 In-Tree Driver Check

**When**: W2 (immediately after Step 1)
**Method**: Cross-reference identified chip against kernel 6.6 driver tree:

```sh
# On build host with kernel 6.6 source:
grep -r "<CHIP_VENDOR_ID>:<CHIP_DEVICE_ID>" drivers/net/ethernet/
# Or check loaded modules on target:
lsmod | grep -i <driver_name>
modinfo <driver_name>
```

**Pass Criteria**: Driver is confirmed in-tree for kernel 6.6 AND loads successfully on target hardware.
**Action on Failure (driver not in-tree)**: Proceed to Step 3 (driver port). Log as Risk R-NET-001.

---

### Step 3: Out-of-Tree Driver Port (Conditional)

**When**: W3-W4 (if Step 2 fails - driver not in-tree for kernel 6.6)
**Method**: Port driver from vendor source or backport from later kernel:

1. Obtain vendor driver source or identify the kernel version where the driver was introduced.
2. Apply kernel 6.6 API compatibility patches (netdev API, DMA API, PCI probe interface).
3. Add driver to Yocto custom layer as an external kernel module.
4. Validate driver loads without errors: `insmod <driver>.ko && dmesg | tail -20`.

**Pass Criteria**: Driver compiles for kernel 6.6, loads without errors, network interface appears (`ip link`).
**Fallback**: If port is not feasible within W4, use USB 3.0 to 2.5 GbE adapter as temporary workaround for development (not production).

---

### Step 4: Bandwidth Test with iperf3

**When**: W15-W18 (after firmware network stack is operational)
**Method**: Measure sustained throughput between VAR-SOM-MX8M-PLUS and Host PC:

```sh
# On Host PC (iperf3 server):
iperf3 -s -p 5201

# On VAR-SOM-MX8M-PLUS (iperf3 client):
iperf3 -c <host_ip> -p 5201 -t 60 -P 4 --bidir
# Target: >= 2.2 Gbps sustained (Mid-A tier data rate = 1.01 Gbps, requires headroom)
```

**Pass Criteria**: Sustained bidirectional throughput >= 2.2 Gbps over 60 seconds with 4 parallel streams. CPU utilization during test < 50% (leaves capacity for firmware processing).
**Note**: The Target (Final Goal) tier requires 2.26 Gbps. The 2.5 GbE interface must sustain at least 2.2 Gbps to provide adequate headroom.

---

### Step 5: 24-Hour Stress Test

**When**: W18 (HIL Pattern B gate, M4)
**Method**: Continuous frame transmission at Mid-A tier (2048x2048@15fps, ~1.01 Gbps) for 24 hours:

```sh
# Run detector_daemon in continuous scan mode for 24 hours
# Monitor on Host PC:
watch -n 10 'iperf3 -c <soc_ip> -p 5201 -t 10 | tail -4'

# Monitor on VAR-SOM-MX8M-PLUS:
ethtool -S <eth_iface> | grep -i error   # Check for hardware errors
cat /proc/net/dev | grep <eth_iface>      # Check packet counters
```

**Pass Criteria**:
- Zero network hardware errors (ethtool statistics show zero rx_errors, tx_errors)
- Zero frame drops due to network exhaustion (firmware drop counter stable)
- Throughput remains >= 1.01 Gbps throughout 24-hour period
- Network interface does not reset or disconnect

**KPI Reference**: This test maps to the Frame Drop Rate KPI (< 0.01%) and Memory Stability KPI (zero leaks, 1-hour continuous) in the Quality KPIs table above.

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | ABYZ-Lab Agent (analyst) | Initial verification strategy |
| 2.0.0 | 2026-02-17 | spec-fw agent | Added FW/SDK to pyramid, Layer 1, Layer 2 (4 sub-tables). Expanded traceability matrix (4 domain tables with SPEC requirement links). Updated milestones (added M3.5). Added FW-specific KPIs (SPI jitter, CPU util). Fixed coverage targets to 85%+. |
| 2.1.0 | 2026-02-17 | ABYZ-Lab Agent | MAJOR-010: Added 2.5 GbE Driver Verification Plan (Steps 1-5: chip identification via lspci, kernel 6.6 in-tree driver check, optional out-of-tree port, iperf3 bandwidth test targeting 2.2 Gbps, 24-hour stress test at Mid-A tier). |

---
