# HW Verification Guide

X-ray Detector Panel System Emulator — Golden Reference Verification

## Overview

This guide describes how to use the software emulator suite as a **Golden Reference** for verifying X-ray detector panel hardware designs. The emulator implements the complete 4-layer pipeline (Panel → FPGA → MCU → Host) in software, enabling protocol, timing, and error-handling pre-verification before physical hardware is available.

**Target Audience**: RTL developers, firmware engineers, and system integrators who need to validate hardware behavior against a known-correct software reference.

---

## Architecture Reference

The 4-layer pipeline under verification:

```
[X-ray Panel]  →  [FPGA: Artix-7]  →  [SoC: i.MX8MP]  →  [Host PC]
  PanelSimulator   FpgaSimulator       McuSimulator       HostSimulator
```

Each layer is implemented as a C# emulator that faithfully models the corresponding hardware component. The emulators are connected end-to-end for integration testing and can also run in isolation for unit-level verification.

---

## Test Scenario Categories (168 Total)

The 168 verification scenarios are organized by hardware layer. Coverage status is tracked in `.moai/specs/SPEC-EMUL-003/scenarios-coverage-matrix.md`.

### Panel Scenarios (P-01 to P-22)

These scenarios verify the X-ray detector panel physics model.

| Range | Category | Count | Purpose |
|-------|----------|-------|---------|
| P-01 to P-05 | X-ray Physics Response | 5 | kVp/mAs linearity, saturation limits |
| P-06 to P-10 | Noise Characteristics | 5 | Poisson, Gaussian, 1/f, composite noise |
| P-11 to P-14 | Calibration Data | 4 | Dark, flatfield, offset, defect maps |
| P-15 to P-18 | Temporal Effects | 4 | Ghosting, lag, temperature drift, stability |
| P-19 to P-22 | Gate/ROIC Interaction | 4 | Gate timing, row-by-row readout, settle time |

### FPGA Scenarios (F-01 to F-36)

These scenarios verify the FPGA data acquisition logic.

| Range | Category | Count | Purpose |
|-------|----------|-------|---------|
| F-01 to F-06 | FSM State Transitions | 6 | Idle/Integrate/Readout/LineDone/FrameDone/Error |
| F-07 to F-12 | Control Signal Timing | 6 | Gate pulse width, ROIC sync, line/frame valid |
| F-13 to F-20 | Protection Logic | 8 | Watchdog, readout timeout, overflow, safe shutdown |
| F-21 to F-26 | SPI Register | 6 | Register read/write, read-only protection, frame counter |
| F-27 to F-30 | Line Buffer | 4 | Ping-pong operation, overflow detection |
| F-31 to F-36 | CSI-2 TX | 6 | Packet sequence, ECC, CRC-16, virtual channel |

### MCU/SoC Scenarios (M-01 to M-38)

These scenarios verify the SoC controller firmware behavior.

| Range | Category | Count | Purpose |
|-------|----------|-------|---------|
| M-01 to M-08 | SequenceEngine FSM | 8 | Scan cycles, error recovery, emergency stop |
| M-09 to M-17 | FrameBufferManager | 9 | DDR4 ring buffer, overflow policy, DMA |
| M-18 to M-23 | HealthMonitor | 6 | Temperature, heartbeat, threshold alerts |
| M-24 to M-28 | CommandProtocol | 5 | HMAC-SHA256 authentication, replay detection |
| M-29 to M-38 | UDP TX | 10 | Frame packetization, CRC-16, port configuration |

### Network Scenarios (N-01 to N-18)

These scenarios verify 10 GbE UDP transmission resilience.

| Range | Category | Count | Purpose |
|-------|----------|-------|---------|
| N-01 to N-06 | Packet Loss | 6 | Random loss rates 0.01% to 10% |
| N-07 to N-10 | Packet Reordering | 4 | Out-of-order arrival, window size |
| N-11 to N-14 | Delay and Jitter | 4 | Variable latency, timeout boundaries |
| N-15 to N-18 | Corruption | 4 | CRC errors, header corruption, truncation |

### Host Scenarios (H-01 to H-12)

These scenarios verify the Host PC frame reassembly and storage.

| Range | Category | Count | Purpose |
|-------|----------|-------|---------|
| H-01 to H-05 | Frame Reassembly | 5 | Packet ordering, completeness detection |
| H-06 to H-09 | Timeout and Recovery | 4 | 2-second timeout, zero-fill, partial frames |
| H-10 to H-12 | Storage | 3 | TIFF 16-bit, RAW + JSON sidecar, DICOM |

### End-to-End Scenarios (E-01 to E-15)

These scenarios verify the complete 4-layer pipeline.

| Range | Category | Count | Purpose |
|-------|----------|-------|---------|
| E-01 to E-04 | Normal Operation | 4 | Single frame, multi-frame, continuous mode |
| E-05 to E-09 | Error and Recovery | 5 | Fault injection at each layer |
| E-10 to E-12 | Checkpoint | 3 | Bit-exact verification at layer boundaries |
| E-13 to E-15 | Performance | 3 | Latency (p95 < 50ms), throughput, jitter |

### CLI Scenarios (C-01 to C-18)

These scenarios verify standalone command-line execution of each module.

| Range | Category | Count | Purpose |
|-------|----------|-------|---------|
| C-01 to C-05 | Single Module | 5 | panel-sim, fpga-sim, mcu-sim, host-sim standalone |
| C-06 to C-10 | Pipeline Composition | 5 | Chained execution with intermediate files |
| C-11 to C-18 | Debug and Inspection | 8 | Intermediate data serialization, verbose output |

### HW Verification Scenarios (G-01 to G-15)

These scenarios define the interface between the software emulator and real hardware.

| Range | Category | Count | Purpose |
|-------|----------|-------|---------|
| G-01 to G-05 | RTL Validation | 5 | Emulator output used as RTL stimulus/reference |
| G-06 to G-10 | Firmware Support | 5 | C header generation, register map validation |
| G-11 to G-15 | System Integration | 5 | HIL test patterns, bring-up sequences |

---

## Integration Tests: IT01–IT18

The following integration tests (in `tools/IntegrationTests/Integration/`) directly exercise the Golden Reference scenarios.

| Test ID | Test Class | Scenarios Covered | Description |
|---------|-----------|-------------------|-------------|
| IT01 | `It01FullPipelineTests` | E-01, E-10 | Single frame capture, 1024×1024 @ 15fps, bit-exact |
| IT02 | `It02PerformanceTargetTierTests` | E-13, E-14 | 300-frame continuous, 2048×2048 @ 30fps |
| IT03 | `IT03_SpiConfigurationTests` | F-21 to F-26 | SPI register round-trips, read-only protection |
| IT04 | `It04Csi2ProtocolValidationTests` | F-31 to F-34, F-17 | CSI-2 magic, CRC-16, ECC, virtual channel |
| IT05 | `IT05_FrameBufferOverflowTests` | F-27, F-28, F-16 | Ping-pong BRAM, 4-frame ring overflow |
| IT06 | `IT06_HmacAuthenticationTests` | M-24 to M-28 | HMAC-SHA256 valid/invalid/missing/replay |
| IT07 | `IT07_SequenceEngineTests` | M-01 to M-07 | 6-state FSM, 5 scan cycles, invalid start |
| IT08 | `IT08_PacketLossRetransmissionTests` | N-01 to N-04 | 0.1% packet loss, reassembly with gaps |
| IT09 | `IT09_MaximumTierStressTests` | E-14, P-18 | 3072×3072 @ 30fps, 60-second stress |
| IT10 | `IT10_LatencyMeasurementTests` | E-13 | End-to-end latency, p95 < 50ms assertion |
| IT11 | `IT11_FullFourLayerPipelineTests` | E-10, E-11 | 256×256 to 2048×2048 bit-exact pipeline |
| IT12 | `IT12_ModuleIsolationTests` | E-12 | ISimulator contract, module independence |
| IT13 | `IT13_PipelineRealizationTests` | E-05 to E-09 | 4-layer connection, NetworkChannel, fault injection |
| IT14 | `IT14_SequenceEngineFullCycleTests` | M-01 to M-07, F-01 to F-04 | Full 56-transition state machine |
| IT15 | `IT15_FrameBufferOverflowTests` | F-28, M-09 to M-17 | FrameBufferManager DDR4 ring, overflow policy |
| IT16 | `IT16_ProtectionLogicShutdownTests` | F-13 to F-20 | Watchdog, readout timeout, safe shutdown sequence |
| IT17 | `IT17_PanelPhysicsModelTests` | P-01, P-06, P-07, P-15, P-16 | X-ray physics, noise, lag model |
| IT18 | `IT18_ScenarioCoverageTests` | P-05, P-08, P-11, P-12, P-17 to P-22 | Saturation, calibration, Gate/ROIC |

---

## Running the Verification Suite

### Prerequisites

- .NET 8.0 SDK
- 4 GB RAM minimum (8 GB recommended for stress tests)
- No special hardware required — all tests run on software emulators

### Full Suite Execution

```bash
# Build all projects
dotnet build tools/ --configuration Release

# Run all integration tests (IT01-IT18)
dotnet test tools/IntegrationTests/IntegrationTests.csproj \
    --configuration Release \
    --verbosity normal

# Run with coverage collection
dotnet test tools/IntegrationTests/IntegrationTests.csproj \
    --configuration Release \
    --collect:"XPlat Code Coverage" \
    --results-directory ./coverage-results
```

### Targeted Execution

```bash
# Run a specific IT scenario
dotnet test tools/IntegrationTests/IntegrationTests.csproj \
    --filter "FullyQualifiedName~IT11"

# Run all FPGA protection logic tests
dotnet test tools/IntegrationTests/IntegrationTests.csproj \
    --filter "FullyQualifiedName~IT16"

# Run Panel physics tests
dotnet test tools/IntegrationTests/IntegrationTests.csproj \
    --filter "FullyQualifiedName~IT17 | FullyQualifiedName~IT18"
```

### Unit Test Execution per Module

```bash
# PanelSimulator unit tests (52 tests)
dotnet test tools/PanelSimulator/test/PanelSimulator.Tests/PanelSimulator.Tests.csproj --verbosity normal

# FpgaSimulator unit tests (81 tests)
dotnet test tools/FpgaSimulator/tests/FpgaSimulator.Tests/FpgaSimulator.Tests.csproj --verbosity normal

# McuSimulator unit tests (28 tests)
dotnet test tools/McuSimulator/tests/McuSimulator.Tests/McuSimulator.Tests.csproj --verbosity normal

# HostSimulator unit tests (61 tests)
dotnet test tools/HostSimulator/tests/HostSimulator.Tests/HostSimulator.Tests.csproj --verbosity normal
```

---

## Acceptance Criteria

### Per-Test Pass Criteria

A test is accepted when:

1. The xUnit test exits with status `Passed` (no assertion failures)
2. No exceptions propagate outside the test boundary
3. All timeout assertions succeed (e.g., p95 latency < 50ms in IT10)
4. Bit-exact pixel comparisons return zero difference (IT01, IT11)

### Module-Level Coverage Targets

| Module | Required Coverage | Current Coverage |
|--------|------------------|-----------------|
| PanelSimulator | >= 85% | 86.9% |
| FpgaSimulator | >= 85% | 98.7% |
| McuSimulator | >= 85% | 92.3% |
| HostSimulator | >= 85% | 86.4% |
| Common.Dto | >= 85% | 97.1% |

### Scenario Coverage Targets

| Category | Target | Current (full COVERED) |
|----------|--------|----------------------|
| Panel (P) | >= 80% | 36% (91% with partial) |
| FPGA (F) | >= 80% | 61% (94% with partial) |
| MCU (M) | >= 85% | 89% (100% with partial) |
| Network (N) | >= 70% | 56% (83% with partial) |
| Host (H) | >= 70% | 42% (58% with partial) |
| End-to-End (E) | >= 85% | 87% (100% with partial) |

### Overall Acceptance Gate

The verification suite **passes** when:
- All 212 non-skipped tests pass (0 failures)
- Module code coverage >= 85% per module
- No regressions from previous baseline

---

## Interpreting Test Results

### Test Output Format

```
Test run for tools/IntegrationTests/IntegrationTests.csproj (.NETCoreApp, Version=v8.0)
Microsoft (R) Test Execution Command Line Tool Version 17.x

Starting test execution, please wait...

  Passed  IT01 - Full Pipeline: SingleFrameCapture_1024x1024 [1.2s]
  Passed  IT11 - FullFourLayerPipeline_BitExact_256x256 [0.4s]
  Skipped IT09 - StressTest_3072x3072_60s [SKIPPED: CI stability]

Test Run Successful.
Total tests: 212
     Passed: 212
    Skipped: 4
 Total time: 45.6 Seconds
```

### Exit Codes

| Exit Code | Meaning | Action |
|-----------|---------|--------|
| 0 | All tests passed | Proceed to next stage |
| 1 | One or more tests failed | Investigate failures before proceeding |
| 2 | Test execution error (e.g., build failure) | Fix build errors first |

### Skipped Tests

Four tests are intentionally skipped for CI stability. These require extended runtimes (60+ seconds) or specific timing conditions that are unreliable in virtualized CI environments:

- Stress tests with 3072×3072 resolution at 30fps for 60 seconds
- Long-term stability tests requiring 10,000+ frames

To run skipped tests locally:

```bash
# Run all tests including normally-skipped ones (set environment variable)
RUN_STRESS_TESTS=true dotnet test tools/IntegrationTests/IntegrationTests.csproj
```

### Common Failure Patterns

**Bit-exact failure (IT01, IT11)**: Indicates a change in the pixel data path. Check recent modifications to `PanelSimulator`, `FpgaSimulator`, or the CSI-2 encoding logic.

**Latency failure (IT10)**: The p95 latency exceeded 50ms. This is typically caused by system load on the test machine. Re-run in isolation.

**HMAC failure (IT06)**: Authentication key mismatch. Verify that `CommandProtocol` uses the same test key as the test fixture.

**Protection logic failure (IT16)**: The safe shutdown did not complete within the 10-clock deadline. Check the `ProtectionLogic` timer implementation.

---

## Known Limitations

### Software vs. Real Hardware

The software emulators differ from real hardware in the following ways:

| Aspect | Software Emulator | Real Hardware |
|--------|------------------|---------------|
| Timing precision | Microsecond-level simulation; not clock-accurate | Nanosecond-accurate RTL |
| CSI-2 D-PHY | Protocol-level only; no D-PHY lane serialization | Physical D-PHY lanes at 400M or 800M per lane |
| SPI timing | Transaction-level; no clock stretching | 50 MHz clock with actual propagation delays |
| DDR4 latency | Synchronous, zero-latency access | Variable latency with refresh cycles |
| Network jitter | Probabilistic model; deterministic per seed | Real-world jitter from NIC and OS scheduler |
| Noise statistics | RNG-based approximation | True physical noise sources (shot, thermal) |
| Gate/ROIC timing | Discrete-time step model | Sub-microsecond analog settling |
| Overheat protection | Temperature increment model | Actual thermal sensor ADC reading |

### Gaps in Current Coverage

The following scenarios are not yet covered by automated tests (as of SPEC-EMUL-003):

- **C-01 to C-15** (CLI scenarios): Standalone command-line execution not yet tested
- **F-35, F-36** (CSI-2 backpressure): Not implemented in the C# emulator
- **P-09** (1/f flicker noise): Flicker noise model exists but no dedicated test
- **H-10 to H-12** (Host storage tests): TIFF/RAW save path not covered

These gaps are tracked as priority items for SPEC-EMUL-004 and future iterations.

### Performance Constraints

- Tests requiring 3072×3072 pixel frames at 30fps consume approximately 4 GB of RAM and take 60+ seconds
- These tests are skipped in CI by default and must be run locally

---

## Related Documents

| Document | Path | Description |
|----------|------|-------------|
| SPEC-EMUL-001 | `.moai/specs/SPEC-EMUL-001/spec.md` | Emulator module revision specification |
| Scenarios (168) | `.moai/specs/SPEC-EMUL-001/scenarios.md` | Complete scenario definitions |
| Coverage Matrix | `.moai/specs/SPEC-EMUL-003/scenarios-coverage-matrix.md` | Per-scenario coverage status |
| Integration Test Plan | `docs/testing/integration-test-plan.md` | IT-01 to IT-12 test strategy |
| HIL Test Plan | `docs/testing/hil-test-plan.md` | Hardware-in-the-loop plan |
| System Architecture | `docs/architecture/system-architecture.md` | Full system design |

---

*ABYZ Lab — X-ray Detector Panel System Emulator*
*Last updated: 2026-03-10 (SPEC-EMUL-004 Golden Reference Hardening)*
