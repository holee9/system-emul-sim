# SPEC-SIM-001: Simulator Suite Requirements Specification

---
id: SPEC-SIM-001
version: 1.0.0
status: draft
created: 2026-02-17
updated: 2026-02-17
author: MoAI Agent (analyst)
priority: high
milestone: M2
gate_week: W9
---

## HISTORY

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (analyst) | Initial SPEC creation for simulator suite |

---

## Overview

### Project Context

The X-ray Detector Panel System requires a software simulation environment that models the entire data acquisition pipeline without physical hardware. The simulator suite enables early software development, algorithm validation, and serves as a golden reference for hardware verification.

### Scope

This SPEC covers four simulators and the common interface layer:

| Simulator | Purpose | Role in Pipeline |
|-----------|---------|-----------------|
| **PanelSimulator** | Model X-ray detector panel pixels | Generate pixel data with noise and defects |
| **FpgaSimulator** | Model FPGA data acquisition | SPI registers, FSM, line buffer, CSI-2 TX (golden reference) |
| **McuSimulator** | Model SoC controller firmware | CSI-2 RX, SPI master, Ethernet TX |
| **HostSimulator** | Model Host PC SDK | UDP receive, frame reassembly, storage |
| **Common.Dto** | Shared interfaces and DTOs | ISimulator, data transfer objects |

### Development Methodology

All simulators are **new code** and follow **TDD (RED-GREEN-REFACTOR)** per `quality.yaml` hybrid settings.

---

## Requirements

### 1. Ubiquitous Requirements (All Simulators)

**REQ-SIM-001**: All simulators **shall** implement the `ISimulator` interface defined in `Common.Dto`.

**WHY**: Uniform interface enables the IntegrationRunner to instantiate, configure, and connect simulators polymorphically.

**IMPACT**: Each simulator must implement `Initialize()`, `Process()`, `Reset()`, and `GetStatus()` methods.

---

**REQ-SIM-002**: All simulators **shall** be configurable via `detector_config.yaml`.

**WHY**: Single source of truth for configuration. Simulator behavior must match hardware behavior for the same configuration.

**IMPACT**: Each simulator loads its relevant section from detector_config.yaml at initialization. No hard-coded parameters for configurable values.

---

**REQ-SIM-003**: All simulators **shall** produce deterministic output for the same input and configuration.

**WHY**: Determinism enables reproducible testing and golden reference comparison with RTL simulation.

**IMPACT**: Random number generators must accept seeds. All floating-point operations must use consistent precision.

---

**REQ-SIM-004**: All simulators **shall** target .NET 8.0 LTS or later (C#).

**WHY**: Project-wide technology stack decision. .NET 8.0 provides long-term support, performance improvements, and cross-platform compatibility.

**IMPACT**: Projects use `<TargetFramework>net8.0</TargetFramework>` or later. No .NET Framework dependencies.

---

**REQ-SIM-005**: All simulators **shall** have unit test coverage of 85%+ per module.

**WHY**: Quality KPI defined in quality.yaml hybrid settings. Ensures simulator correctness for golden reference use.

**IMPACT**: Each simulator project has a corresponding test project (e.g., `PanelSimulator.Tests`). Coverage measured by coverlet. Target: 85% minimum, 90% goal.

---

**REQ-SIM-006**: The simulator pipeline **shall** simulate the Minimum tier (1024x1024, 14-bit, 15 fps) at 2x real-time or faster on a standard x86-64 development machine.

**WHY**: Integration testing requires running 1000+ frame scenarios. At 2x real-time, 1000 frames at 15 fps = 33 seconds wall clock time (acceptable for CI pipeline).

**IMPACT**: Target performance: 1000 frames in <= 33 seconds on x86-64 (Intel Core i7 or equivalent). Measured via `dotnet-trace` or `Stopwatch` in IntegrationRunner output. Optimization mandatory if performance falls below threshold.

---

**REQ-SIM-007**: Simulators **shall** support two execution modes: `fast` (functional, >= 2x real-time) and `realtime` (timing-accurate, ~1x real-time).

**WHY**: Fast mode enables rapid integration testing (IT-01~IT-10). Real-time mode validates timing-sensitive behavior (frame rate, inter-frame gap).

**IMPACT**: Mode configured via `simulation.mode` in detector_config.yaml or command line flag. Default mode: `fast`.

---

### 2. PanelSimulator Requirements

**REQ-SIM-010**: The PanelSimulator **shall** generate a 2D pixel matrix with configurable resolution (rows x cols) and bit depth (14-bit or 16-bit).

**WHY**: Panel output is the source of all downstream data. Resolution and bit depth are configurable per performance tier.

**IMPACT**: Matrix size matches `panel.rows` x `panel.cols` from config. Pixel values clamped to `2^bit_depth - 1`.

---

**REQ-SIM-011**: **WHEN** a frame is generated **THEN** the PanelSimulator **shall** apply a configurable noise model to each pixel.

**WHY**: Realistic noise simulation validates downstream signal processing and image quality assessment.

**IMPACT**: Supports Gaussian noise model with configurable standard deviation. Noise is additive to base signal.

---

**REQ-SIM-012**: **WHEN** a frame is generated **THEN** the PanelSimulator **shall** inject configurable pixel defects (dead pixels and hot pixels).

**WHY**: Detector panels have manufacturing defects. Simulators must model these for defect correction algorithm validation.

**IMPACT**: Defect rate configurable (e.g., 0.1%). Dead pixels output 0, hot pixels output max value. Defect map is deterministic for given seed.

---

**REQ-SIM-013**: **IF** the test pattern mode is "counter" **THEN** the PanelSimulator **shall** output sequential pixel values: pixel[row][col] = (row * cols + col) % 2^bit_depth.

**WHY**: Counter pattern enables simple data integrity verification across the entire pipeline.

**IMPACT**: Counter mode bypasses noise and defect injection. Used for integration testing (IT-01 to IT-04).

---

**REQ-SIM-014**: **IF** the test pattern mode is "checkerboard" **THEN** the PanelSimulator **shall** output alternating max/zero pixel values.

**WHY**: Checkerboard pattern provides maximum toggle rate for electrical stress testing validation.

**IMPACT**: Even pixels = 0, odd pixels = max value. Pattern inverts every other row.

---

### 3. FpgaSimulator Requirements

**REQ-SIM-020**: The FpgaSimulator **shall** model the complete FPGA register map as defined in `docs/architecture/fpga-design.md` Section 6.

**WHY**: FpgaSimulator serves as the golden reference for RTL development. Register-accurate behavior enables bit-exact comparison.

**IMPACT**: All registers (CONTROL, STATUS, FRAME_COUNTER, timing, panel config, CSI-2 config, error flags, identification) must be modeled.

---

**REQ-SIM-021**: The FpgaSimulator **shall** model the Panel Scan FSM with all states: IDLE, INTEGRATE, READOUT, LINE_DONE, FRAME_DONE, ERROR.

**WHY**: FSM behavior is the core FPGA logic. Simulator FSM must match RTL FSM state transitions exactly.

**IMPACT**: State transitions triggered by timing parameters and SPI commands. All operating modes (single, continuous, calibration) supported.

---

**REQ-SIM-022**: The FpgaSimulator **shall** model the Ping-Pong line buffer behavior.

**WHY**: Line buffer timing affects data flow and must be verified against RTL implementation.

**IMPACT**: Two-bank buffer model with alternating write/read. Buffer overflow detection when write overtakes read.

---

**REQ-SIM-023**: The FpgaSimulator **shall** generate CSI-2 packet format output matching the MIPI CSI-2 specification.

**WHY**: CSI-2 packet format is the FPGA output interface. McuSimulator consumes these packets.

**IMPACT**: Generates Frame Start, Line Data (with CRC-16), and Frame End packets. RAW16 data type (0x2E, per MIPI CSI-2 v1.3). Virtual Channel 0.

---

**REQ-SIM-024**: **WHEN** a SPI write to CONTROL register sets `start_scan` **THEN** the FpgaSimulator **shall** transition from IDLE to INTEGRATE state.

**WHY**: SPI control interface is the primary FPGA control mechanism.

**IMPACT**: McuSimulator triggers scans via SPI write. FpgaSimulator responds with state transition and begins data output.

---

**REQ-SIM-025**: **WHEN** an error condition occurs (timeout, overflow, CRC error) **THEN** the FpgaSimulator **shall** set the corresponding ERROR_FLAGS bit and transition to ERROR state.

**WHY**: Protection logic behavior must be modeled for error handling verification.

**IMPACT**: Error injection API allows tests to trigger specific error conditions. Error clearing via CONTROL register write.

---

**REQ-SIM-026**: The FpgaSimulator output **shall** be bit-exact compared to FPGA RTL simulation output for the same input and configuration.

**WHY**: The simulator is the golden reference model. Any discrepancy indicates a defect in either simulator or RTL.

**IMPACT**: Comparison framework (`rtl_vs_sim_checker`) validates bit-exact match. Deviations must be investigated and resolved.

---

### 4. McuSimulator Requirements

**REQ-SIM-030**: The McuSimulator **shall** model the SoC SPI master interface for FPGA control.

**WHY**: SoC controls FPGA via SPI. McuSimulator must send correct SPI transactions.

**IMPACT**: Implements SPI write (register address + data) and SPI read (register address, return data). Connects to FpgaSimulator SPI slave.

---

**REQ-SIM-031**: The McuSimulator **shall** model CSI-2 RX functionality by consuming CSI-2 packets from FpgaSimulator.

**WHY**: SoC receives pixel data via CSI-2. McuSimulator validates packet parsing and frame buffer assembly.

**IMPACT**: Parses Frame Start, Line Data, Frame End packets. Extracts pixel data and validates CRC-16.

---

**REQ-SIM-032**: The McuSimulator **shall** generate UDP packets with the frame header format defined in `system-architecture.md` Section 5.3.

**WHY**: SoC streams frames to Host PC via UDP. Packet format must match SDK expectations.

**IMPACT**: Frame header includes: magic (0xDEADBEEF), frame_seq, timestamp, width, height, bit_depth, packet_index, total_packets, crc16.

---

**REQ-SIM-033**: The McuSimulator **shall** model frame buffer management with configurable buffer count (default: 4).

**WHY**: SoC uses ping-pong double-buffering in DDR4. Buffer management affects data integrity.

**IMPACT**: Frame buffers allocated at initialization. Buffer overflow detected when all buffers are full.

---

**REQ-SIM-034**: **WHEN** the Sequence Engine receives a StartScan command **THEN** the McuSimulator **shall** send SPI start_scan to FpgaSimulator and begin frame streaming.

**WHY**: Sequence Engine coordinates the scan lifecycle between Host commands and FPGA control.

**IMPACT**: Complete command flow: Host SDK -> McuSimulator -> FpgaSimulator -> pixel data -> McuSimulator -> UDP.

---

### 5. HostSimulator Requirements

**REQ-SIM-040**: The HostSimulator **shall** receive UDP packets and reassemble complete frames.

**WHY**: Host SDK core functionality is frame reassembly from UDP packet stream.

**IMPACT**: Receives packets with frame header. Uses frame_seq and packet_index to reconstruct 2D frame.

---

**REQ-SIM-041**: **WHEN** packets arrive out of order **THEN** the HostSimulator **shall** correctly reassemble the frame using packet_index.

**WHY**: UDP does not guarantee packet ordering. SDK must handle out-of-order delivery.

**IMPACT**: Packet buffer indexed by packet_index. Frame marked complete when all packets received.

---

**REQ-SIM-042**: **WHEN** a packet is missing after the configured timeout **THEN** the HostSimulator **shall** mark the frame as incomplete and report the missing packet indices.

**WHY**: Network packet loss must be detected and reported. Incomplete frames should not be silently discarded.

**IMPACT**: Timeout configurable via `host.network.packet_timeout_ms`. Incomplete frame data available for partial recovery.

---

**REQ-SIM-043**: The HostSimulator **shall** save frames in TIFF format (uncompressed and LZW) and RAW format.

**WHY**: Multiple storage formats required for different use cases: TIFF for viewing, RAW for processing, DICOM for clinical.

**IMPACT**: TIFF: 16-bit grayscale, valid TIFF header. RAW: flat binary file (rows * cols * 2 bytes).

---

**REQ-SIM-044**: The HostSimulator **shall** support multi-threaded packet reception with configurable thread count.

**WHY**: High-throughput streaming requires concurrent packet processing to prevent receive buffer overflow.

**IMPACT**: Thread count configurable via `host.network.receive_threads`. Thread-safe packet queue.

---

### 6. Common.Dto Requirements

**REQ-SIM-050**: Common.Dto **shall** define the `ISimulator` interface with methods: `Initialize(config)`, `Process(input)`, `Reset()`, `GetStatus()`.

**WHY**: Uniform interface enables polymorphic simulator management by IntegrationRunner.

**IMPACT**: All four simulators implement this interface. IntegrationRunner depends only on Common.Dto.

---

**REQ-SIM-051**: Common.Dto **shall** define data transfer objects for inter-simulator communication: `FrameData`, `LineData`, `Csi2Packet`, `UdpPacket`, `SpiTransaction`.

**WHY**: Strongly-typed DTOs prevent data format errors between simulator modules.

**IMPACT**: DTOs are immutable records with validation. Serializable to JSON for debugging.

---

**REQ-SIM-052**: Common.Dto **shall not** depend on any simulator implementation.

**WHY**: Common.Dto is the dependency root. Circular dependencies would prevent clean architecture.

**IMPACT**: Only interfaces, DTOs, enums, and constants. No implementation logic.

---

### 7. Unwanted Requirements

**REQ-SIM-060**: Simulators **shall not** depend on each other's implementation directly. All inter-simulator communication **shall** go through Common.Dto interfaces and DTOs.

**WHY**: Dependency isolation enables independent testing, replacement, and parallel development.

**IMPACT**: PanelSimulator does not reference FpgaSimulator assembly. Only Common.Dto is shared.

---

**REQ-SIM-061**: Simulators **shall not** use hard-coded configuration values for any parameter available in detector_config.yaml.

**WHY**: Hard-coded values create divergence between simulator and hardware behavior when configuration changes.

**IMPACT**: All configurable values loaded from config at initialization. Default values used only when config field is absent.

---

**REQ-SIM-062**: Simulators **shall not** introduce external dependencies beyond .NET 8.0 BCL without approval.

**WHY**: Minimize dependency surface for maintainability and security.

**IMPACT**: Allowed: .NET BCL, System.Text.Json, System.IO.Compression. Requires approval: third-party NuGet packages.

---

### 8. Optional Requirements

**REQ-SIM-070**: **Where possible**, PanelSimulator should support additional noise models (Poisson, uniform, salt-and-pepper).

**WHY**: Different noise models better approximate different X-ray imaging conditions.

**IMPACT**: Noise model selectable via simulation config. Gaussian is minimum required.

---

**REQ-SIM-071**: **Where possible**, HostSimulator should support DICOM format output.

**WHY**: DICOM is the standard medical imaging format. Early DICOM support simplifies clinical integration.

**IMPACT**: Requires DICOM library (fo-dicom NuGet package). Priority: low (after TIFF and RAW).

---

**REQ-SIM-072**: **Where possible**, FpgaSimulator should support cycle-accurate timing simulation.

**WHY**: Cycle-accurate simulation enables precise performance analysis and timing verification.

**IMPACT**: Optional mode: cycle-accurate (slow, for validation) vs functional (fast, for integration testing).

---

---

## Technical Constraints

### Platform Constraints

- **Runtime**: .NET 8.0 LTS or later
- **Language**: C# 12.0+
- **Test Framework**: xUnit + FluentAssertions
- **Coverage Tool**: coverlet
- **Build**: `dotnet build` / `dotnet test`

### Performance Constraints

- **Minimum tier (1024x1024, 14-bit, 15 fps)**: >= 2x real-time in fast mode (1000 frames in <= 33 seconds on x86-64)
- **Intermediate-A tier (2048x2048, 16-bit, 15 fps)**: >= 1x real-time in fast mode (1000 frames in <= 67 seconds)
- **Frame generation**: < 100 ms per frame at Intermediate-A tier (2048x2048x16-bit)
- **Pipeline throughput**: >= 15 frames/second in fast simulation mode (Minimum tier)
- **Memory**: < 500 MB peak memory for 10-frame pipeline simulation

### Interface Constraints

- All simulators communicate via Common.Dto types only
- Configuration loaded from `detector_config.yaml` via YAML parser
- No direct file system access in simulator core logic (inject via interface)

---

## Acceptance Criteria

### AC-SIM-001: PanelSimulator Counter Pattern

**GIVEN**: PanelSimulator initialized with rows=1024, cols=1024, bit_depth=16, pattern=counter
**WHEN**: One frame is generated
**THEN**: pixel[r][c] == (r * 1024 + c) % 65536 for all r,c
**AND**: Frame dimensions match configuration exactly

---

### AC-SIM-002: PanelSimulator Noise Model

**GIVEN**: PanelSimulator initialized with noise_model=gaussian, noise_stddev=100, seed=42
**WHEN**: 100 frames are generated
**THEN**: Pixel value standard deviation is within 5% of configured stddev (95-105)
**AND**: Mean pixel value is within 1% of base signal

---

### AC-SIM-003: FpgaSimulator SPI Register Access

**GIVEN**: FpgaSimulator initialized with default configuration
**WHEN**: SPI write 0xFF to CONTROL register (0x00), then SPI read CONTROL (0x00)
**THEN**: Read returns 0xFF (write-read consistency)
**AND**: STATUS register (0x04) reflects expected state

---

### AC-SIM-004: FpgaSimulator FSM State Transitions

**GIVEN**: FpgaSimulator in IDLE state
**WHEN**: SPI write start_scan to CONTROL register
**THEN**: FSM transitions through INTEGRATE -> READOUT -> LINE_DONE -> (repeat for all lines) -> FRAME_DONE
**AND**: Frame counter increments by 1

---

### AC-SIM-005: FpgaSimulator CSI-2 Packet Generation

**GIVEN**: FpgaSimulator processes one frame of counter pattern (1024x1024)
**WHEN**: CSI-2 output is captured
**THEN**: Output contains 1 Frame Start packet, 1024 Line Data packets, 1 Frame End packet
**AND**: Each Line Data packet has correct CRC-16
**AND**: Data type = 0x2E (RAW16, per MIPI CSI-2 v1.3)

---

### AC-SIM-006: McuSimulator End-to-End Pipeline

**GIVEN**: McuSimulator connected to FpgaSimulator (CSI-2) and configured for UDP output
**WHEN**: StartScan command is sent, one frame is processed
**THEN**: UDP packets generated with correct frame header (magic=0xDEADBEEF)
**AND**: Total UDP payload size = rows * cols * 2 bytes
**AND**: packet_index is sequential from 0 to total_packets-1

---

### AC-SIM-007: HostSimulator Frame Reassembly

**GIVEN**: HostSimulator receives all UDP packets for one frame (in order)
**WHEN**: Frame reassembly completes
**THEN**: Reassembled frame is bit-exact match to PanelSimulator input
**AND**: Frame marked as complete (no missing packets)

---

### AC-SIM-008: HostSimulator Out-of-Order Handling

**GIVEN**: HostSimulator receives UDP packets in random order
**WHEN**: All packets for one frame are delivered
**THEN**: Reassembled frame is bit-exact match to PanelSimulator input
**AND**: Frame marked as complete despite out-of-order delivery

---

### AC-SIM-009: Full Pipeline Integration (IT-01 Validation)

**GIVEN**: All four simulators connected: Panel -> FPGA -> MCU -> Host
**WHEN**: One frame at minimum tier (1024x1024, 14-bit) is processed through full pipeline
**THEN**: Host output frame is bit-exact match to Panel input frame
**AND**: Zero data corruption across all interfaces

---

### AC-SIM-009a: Bit-Accurate RTL vs Simulator Comparison

**GIVEN**: Vivado xsim RTL simulation output (FPGA packet dump)
**AND**: Same input/config with FpgaSimulator C# output
**WHEN**: rtl_vs_sim_checker compares both outputs
**THEN**: CSI-2 packet headers (Data Type, VC, Word Count) match bit-accurately
**AND**: All pixel payload bytes match bit-accurately (tolerance = 0)
**AND**: CRC-16 values match bit-accurately
**AND**: FSM state transition sequences are identical (log comparison)
**AND**: On comparison failure, report first mismatch location and value

---

### AC-SIM-010: Configuration from YAML

**GIVEN**: detector_config.yaml with panel.rows=2048, panel.cols=2048, panel.bit_depth=16
**WHEN**: All simulators are initialized from this configuration
**THEN**: PanelSimulator generates 2048x2048 frames
**AND**: FpgaSimulator line buffer sized for 2048 pixels
**AND**: McuSimulator UDP packets sized for 2048x2048x16-bit frames
**AND**: HostSimulator expects 2048x2048 frame dimensions

---

### AC-SIM-011: Minimum Tier 2x Real-Time Performance

**GIVEN**: Full simulator pipeline (Panel -> FPGA -> MCU -> Host) configured for Minimum tier (1024x1024, 14-bit, 15 fps), fast mode
**WHEN**: IntegrationRunner executes 1000 frames
**THEN**: Total elapsed wall-clock time <= 33 seconds (2x real-time)
**AND**: All 1000 frames reassembled correctly at Host (zero data errors)
**AND**: Frame throughput reported >= 30 frames/second in IntegrationRunner output

---

### AC-SIM-012: Deterministic Reproducibility

**GIVEN**: Simulator pipeline initialized with seed=12345, Gaussian noise model
**WHEN**: Same 100-frame sequence is generated twice with identical seed and configuration
**THEN**: Both runs produce identical pixel-level output (bit-exact match)
**AND**: Error injection points occur at identical frame indices in both runs

---

## Dependencies

### Internal Dependencies

- `detector_config.yaml` schema (SPEC-SIM-001 requires config/schema/detector-config-schema.json)
- `docs/architecture/system-architecture.md` (data flow, interface definitions)
- `docs/architecture/fpga-design.md` (register map, FSM, CSI-2 packet format)

### External Dependencies

- .NET 8.0 SDK (development and runtime)
- xUnit NuGet package (testing framework)
- coverlet NuGet package (code coverage)
- YamlDotNet or similar (YAML configuration parsing)

---

## Risks

### R-SIM-001: Golden Reference Accuracy

**Risk**: FpgaSimulator does not accurately model FPGA RTL behavior, leading to false validation results.

**Probability**: Medium
**Impact**: High (invalid golden reference invalidates all RTL verification)

**Mitigation**: Systematic cross-verification against RTL simulation outputs. Register-level and packet-level comparison.

---

### R-SIM-002: Performance Bottleneck

**Risk**: Simulators too slow for large-scale integration testing (1000+ frames).

**Probability**: Low
**Impact**: Medium (extends test execution time)

**Mitigation**: Optimize critical paths (line buffer, CRC calculation). Use span-based memory for zero-copy data passing.

---

### R-SIM-003: Configuration Drift

**Risk**: Simulator configuration diverges from hardware configuration after detector_config.yaml changes.

**Probability**: Medium
**Impact**: High (simulator produces incorrect results)

**Mitigation**: Schema validation at simulator initialization. CI test that validates config round-trip.

---

## Traceability

### Parent Documents

- `README.md`: Solution structure, simulator descriptions, quality KPIs
- `SPEC-ARCH-001`: Technology stack (.NET 8.0, CSI-2 interface)
- `docs/architecture/system-architecture.md`: Data flow, interface specs
- `docs/architecture/fpga-design.md`: Register map, FSM, CSI-2 packet format

### Child Documents

- `docs/testing/unit-test-plan.md`: SW-01 to SW-04 (simulator unit tests)
- `docs/testing/integration-test-plan.md`: IT-01 to IT-10 (pipeline integration tests)

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (analyst) | Initial SPEC creation for simulator suite |

---

## Review Record

- Date: 2026-02-17
- Reviewer: manager-quality
- Status: Approved
- TRUST 5: T:5 R:5 U:5 S:4 T:5

---

**END OF SPEC**
