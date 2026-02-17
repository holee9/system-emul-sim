# SPEC-SIM-001: Implementation Plan

## Overview

This implementation plan outlines the phased approach to building the Simulator Suite for the X-ray Detector Panel System. The suite comprises five C# .NET 8.0 projects (PanelSimulator, FpgaSimulator, McuSimulator, HostSimulator, Common.Dto) following TDD methodology (RED-GREEN-REFACTOR) for all new code. Gate milestone: M2 (W9).

---

## Implementation Phases

### Phase 1: Foundation - Common.Dto and Project Structure

**Objective**: Establish solution structure, shared interfaces, and data transfer objects.

**Tasks**:

1. **Solution and Project Scaffolding**
   - Create .NET 8.0 solution `SimulatorSuite.sln`
   - Create 5 library projects: `Common.Dto`, `PanelSimulator`, `FpgaSimulator`, `McuSimulator`, `HostSimulator`
   - Create 5 test projects: `Common.Dto.Tests`, `PanelSimulator.Tests`, `FpgaSimulator.Tests`, `McuSimulator.Tests`, `HostSimulator.Tests`
   - Configure project references (all simulators reference only Common.Dto)
   - Configure coverlet for code coverage reporting

2. **ISimulator Interface (REQ-SIM-001, REQ-SIM-050)**
   - TDD: Write failing test for ISimulator interface methods
   - Define `ISimulator` with `Initialize(DetectorConfig)`, `Process(IDataPacket)`, `Reset()`, `GetStatus()`
   - Define `SimulatorStatus` enum (Idle, Running, Error, Completed)
   - Verify interface contract via unit tests

3. **Data Transfer Objects (REQ-SIM-051)**
   - TDD: Write failing tests for each DTO's immutability and serialization
   - Implement `FrameData`, `LineData`, `Csi2Packet`, `UdpPacket`, `SpiTransaction` as C# records
   - Implement JSON serialization/deserialization for all DTOs
   - Validate field constraints (non-negative dimensions, valid CRC)

4. **Configuration Loader (REQ-SIM-002)**
   - TDD: Write failing test for YAML configuration loading
   - Implement `DetectorConfig` class with YAML deserialization (YamlDotNet)
   - Map detector_config.yaml sections to strongly-typed config objects
   - Validate configuration at load time (resolution > 0, bit_depth 14 or 16, fps > 0)

**Deliverables**:
- Solution structure with all projects and test projects
- Common.Dto assembly with ISimulator, DTOs, and configuration types
- Dependency isolation verified (star topology)
- Unit tests for all Common.Dto types

**Dependencies**:
- detector_config.yaml schema definition
- .NET 8.0 SDK installed

---

### Phase 2: PanelSimulator Implementation

**Objective**: Build the pixel data generator with test patterns, noise, and defect injection.

**Tasks**:

1. **Counter Test Pattern (REQ-SIM-013)**
   - TDD: Write failing test for counter pattern formula: pixel[r][c] = (r * cols + c) % 2^bit_depth
   - Implement counter pattern generator
   - Verify exhaustive pixel correctness at 1024x1024

2. **Checkerboard Test Pattern (REQ-SIM-014)**
   - TDD: Write failing test for alternating 0/max pattern with row inversion
   - Implement checkerboard pattern generator
   - Verify pattern correctness

3. **Gaussian Noise Model (REQ-SIM-011)**
   - TDD: Write failing test for noise statistical properties (stddev within 5%, mean within 1%)
   - Implement seeded Gaussian noise generator
   - Implement pixel value clamping to [0, 2^bit_depth - 1]

4. **Pixel Defect Injection (REQ-SIM-012)**
   - TDD: Write failing test for defect rate and deterministic defect map
   - Implement dead pixel (value=0) and hot pixel (value=max) injection
   - Verify defect map determinism with seed

5. **Configurable Resolution and Bit Depth (REQ-SIM-010)**
   - TDD: Write failing test for configurable frame dimensions
   - Implement frame generation with configurable rows, cols, bit_depth
   - Verify at multiple resolutions (1024x1024, 2048x2048, 3072x3072)

6. **ISimulator Integration**
   - Implement ISimulator interface methods for PanelSimulator
   - Connect configuration loader to PanelSimulator initialization
   - Verify Process() outputs FrameData DTO

**Deliverables**:
- PanelSimulator with counter, checkerboard, noise, and defect modes
- PanelSimulator.Tests with >= 85% coverage
- All acceptance criteria AC-SIM-001, AC-SIM-002 passing

**Dependencies**:
- Phase 1 (Common.Dto) complete

---

### Phase 3: FpgaSimulator Implementation

**Objective**: Build the FPGA golden reference model with register map, FSM, line buffer, and CSI-2 TX.

**Tasks**:

1. **SPI Register Map (REQ-SIM-020)**
   - TDD: Write failing tests for register read/write operations
   - Implement complete register map per fpga-design.md Section 6
   - Implement read-only, write-only, and read-write register behaviors
   - Verify write-read consistency for all writable registers

2. **Panel Scan FSM (REQ-SIM-021)**
   - TDD: Write failing tests for each state transition
   - Implement FSM: IDLE -> INTEGRATE -> READOUT -> LINE_DONE -> FRAME_DONE -> IDLE
   - Implement ERROR state reachable from any state
   - Implement single, continuous, and calibration operating modes
   - Verify 100% FSM state coverage

3. **Ping-Pong Line Buffer (REQ-SIM-022)**
   - TDD: Write failing test for alternating bank behavior
   - Implement two-bank buffer with write/read alternation
   - Implement overflow detection (write overtakes read)
   - Verify buffer timing behavior

4. **CSI-2 Packet Generation (REQ-SIM-023)**
   - TDD: Write failing tests for packet structure and CRC
   - Implement Frame Start, Line Data, Frame End packet generation
   - Implement CRC-16 calculation per MIPI CSI-2 specification
   - Set Data Type = 0x2E (RAW16), Virtual Channel = 0
   - Verify packet count: 1 FS + N lines + 1 FE

5. **SPI Control Interface (REQ-SIM-024)**
   - TDD: Write failing test for start_scan trigger
   - Implement CONTROL register start_scan bit handling
   - Verify FSM transition triggered by SPI write

6. **Error Handling (REQ-SIM-025)**
   - TDD: Write failing tests for each error type
   - Implement timeout, overflow, CRC error detection
   - Implement ERROR_FLAGS register bits
   - Implement error clearing via CONTROL register
   - Provide error injection API for testing

7. **ISimulator Integration**
   - Implement ISimulator interface for FpgaSimulator
   - Input: FrameData (from PanelSimulator), Output: Csi2Packet[] (to McuSimulator)
   - Connect SPI interface for McuSimulator control

**Deliverables**:
- FpgaSimulator with register map, FSM, line buffer, CSI-2 TX
- FpgaSimulator.Tests with >= 85% coverage, 100% FSM coverage
- All acceptance criteria AC-SIM-003, AC-SIM-004, AC-SIM-005 passing

**Dependencies**:
- Phase 1 (Common.Dto) complete
- docs/architecture/fpga-design.md (register map, FSM specification)

---

### Phase 4: McuSimulator Implementation

**Objective**: Build the SoC controller model with SPI master, CSI-2 RX, and UDP TX.

**Tasks**:

1. **SPI Master Interface (REQ-SIM-030)**
   - TDD: Write failing tests for SPI write/read transactions
   - Implement SPI master with register address + data format
   - Verify bidirectional communication with FpgaSimulator SPI slave

2. **CSI-2 RX Parser (REQ-SIM-031)**
   - TDD: Write failing tests for CSI-2 packet parsing
   - Implement Frame Start, Line Data, Frame End packet parser
   - Implement CRC-16 validation on received Line Data packets
   - Extract pixel data from CSI-2 packets into frame buffer

3. **UDP Packet Generator (REQ-SIM-032)**
   - TDD: Write failing tests for UDP packet format
   - Implement frame header: magic (0xD7E01234), frame_seq, timestamp, width, height, bit_depth, packet_index, total_packets, crc16
   - Implement frame data packetization (split frame into UDP-sized chunks)
   - Verify total payload size = rows * cols * 2 bytes

4. **Frame Buffer Management (REQ-SIM-033)**
   - TDD: Write failing tests for buffer allocation and overflow
   - Implement configurable buffer count (default: 4)
   - Implement overflow detection when all buffers occupied
   - Report overflow via GetStatus()

5. **Sequence Engine (REQ-SIM-034)**
   - TDD: Write failing test for StartScan command flow
   - Implement command flow: StartScan -> SPI start_scan -> process CSI-2 -> UDP TX
   - Verify end-to-end data flow

6. **ISimulator Integration**
   - Implement ISimulator interface for McuSimulator
   - Input: Csi2Packet[] (from FpgaSimulator), Output: UdpPacket[] (to HostSimulator)
   - Connect SPI master to FpgaSimulator SPI slave

**Deliverables**:
- McuSimulator with SPI master, CSI-2 RX, frame buffer, UDP TX
- McuSimulator.Tests with >= 85% coverage
- Acceptance criterion AC-SIM-006 passing

**Dependencies**:
- Phase 1 (Common.Dto) complete
- Phase 3 (FpgaSimulator) complete (for SPI slave connection)
- docs/architecture/system-architecture.md Section 5.3 (UDP packet format)

---

### Phase 5: HostSimulator Implementation

**Objective**: Build the Host PC SDK model with UDP RX, frame reassembly, and file output.

**Tasks**:

1. **UDP Packet Receiver (REQ-SIM-040)**
   - TDD: Write failing tests for packet reception and validation
   - Implement UDP packet parser with header validation
   - Verify magic number and CRC-16 on received packets

2. **Frame Reassembly - In-Order (REQ-SIM-040)**
   - TDD: Write failing test for sequential reassembly
   - Implement packet buffer indexed by packet_index
   - Reconstruct 2D frame from sequential packets
   - Mark frame complete when all packets received

3. **Frame Reassembly - Out-of-Order (REQ-SIM-041)**
   - TDD: Write failing test for random-order reassembly
   - Implement packet_index-based positioning regardless of arrival order
   - Verify bit-exact result matches in-order reassembly

4. **Missing Packet Detection (REQ-SIM-042)**
   - TDD: Write failing test for timeout and missing packet reporting
   - Implement configurable packet_timeout_ms
   - Report missing packet indices for incomplete frames
   - Provide partial frame data for recovery

5. **File Output - TIFF and RAW (REQ-SIM-043)**
   - TDD: Write failing tests for TIFF header validation and RAW file size
   - Implement 16-bit grayscale TIFF output (uncompressed and LZW)
   - Implement RAW binary output (flat binary, rows * cols * 2 bytes)
   - Verify file format correctness with standard tools

6. **Multi-Threaded Reception (REQ-SIM-044)**
   - TDD: Write failing test for concurrent packet processing
   - Implement configurable receive thread count
   - Implement thread-safe packet queue
   - Verify no data corruption under concurrent access

7. **ISimulator Integration**
   - Implement ISimulator interface for HostSimulator
   - Input: UdpPacket[] (from McuSimulator), Output: FrameData (reassembled)
   - Connect file output to Process() completion

**Deliverables**:
- HostSimulator with UDP RX, reassembly, timeout, file output
- HostSimulator.Tests with >= 85% coverage
- Acceptance criteria AC-SIM-007, AC-SIM-008 passing

**Dependencies**:
- Phase 1 (Common.Dto) complete
- Phase 4 (McuSimulator) complete (for UDP packet format)

---

### Phase 6: Integration and Performance Validation

**Objective**: Connect all simulators, run end-to-end integration tests, and validate performance.

**Tasks**:

1. **IntegrationRunner Development**
   - Create IntegrationRunner console application
   - Wire simulators: Panel -> FPGA -> MCU -> Host
   - Implement frame-by-frame pipeline execution
   - Add performance timing instrumentation (Stopwatch)

2. **End-to-End Data Integrity (IT-01)**
   - Run full pipeline with counter pattern at Minimum tier
   - Verify bit-exact match: Panel input == Host output
   - Verify zero data corruption across all interfaces

3. **Performance Benchmarking**
   - Minimum tier: 1000 frames in <= 33 seconds (2x real-time)
   - Intermediate-A tier: 1000 frames in <= 67 seconds (1x real-time)
   - Memory profiling: < 500 MB peak for 10-frame pipeline
   - Per-frame latency measurement per component

4. **Execution Mode Validation**
   - Fast mode: verify >= 2x real-time (Minimum tier)
   - Real-time mode: verify ~1x real-time with accurate timing
   - Mode switching validation

5. **Golden Reference Baseline**
   - Generate reference output files for rtl_vs_sim_checker
   - Document output format for RTL comparison
   - Create comparison scripts

**Deliverables**:
- IntegrationRunner with full pipeline support
- IT-01 passing (end-to-end data integrity)
- Performance benchmark report
- Golden reference output files

**Dependencies**:
- Phases 1-5 complete (all simulators implemented and tested)

---

## Task Decomposition

### Priority-Based Milestones

**Primary Goal**: Common.Dto and PanelSimulator (Phases 1-2)
- Foundation for all other simulators
- Earliest testable output (frame generation)
- Validates TDD workflow and project structure

**Secondary Goal**: FpgaSimulator (Phase 3)
- Golden reference model (highest complexity)
- Validates register map and FSM against design documents
- Produces CSI-2 packets for downstream testing

**Tertiary Goal**: McuSimulator and HostSimulator (Phases 4-5)
- Completes the data pipeline
- Validates network packetization and reassembly
- Enables end-to-end integration testing

**Final Goal**: Integration and Performance Validation (Phase 6)
- Proves full pipeline data integrity
- Validates performance targets
- Generates golden reference for RTL comparison

**Optional Goal**: Extended features
- Additional noise models (REQ-SIM-070)
- DICOM output (REQ-SIM-071)
- Cycle-accurate timing (REQ-SIM-072)

---

## Technology Stack Specifications

### Runtime and Language

- **Runtime**: .NET 8.0 LTS (or later)
- **Language**: C# 12.0+
- **Target Framework**: `<TargetFramework>net8.0</TargetFramework>`
- **Nullable**: Enabled (`<Nullable>enable</Nullable>`)
- **Implicit Usings**: Enabled

### Testing Stack

- **Framework**: xUnit 2.x
- **Assertions**: FluentAssertions 6.x
- **Coverage**: coverlet.collector (integrated with dotnet test)
- **Mocking**: NSubstitute or Moq (if needed for interface mocking)
- **Coverage Target**: 85% minimum, 90% goal per module

### Build and CI

- **Build Command**: `dotnet build SimulatorSuite.sln`
- **Test Command**: `dotnet test --collect:"XPlat Code Coverage"`
- **Coverage Report**: `dotnet tool run reportgenerator`
- **CI Integration**: Gitea + n8n webhooks

### NuGet Dependencies

| Package | Version | Purpose | Project |
|---------|---------|---------|---------|
| YamlDotNet | 16.x | YAML config parsing | Common.Dto |
| xUnit | 2.9.x | Test framework | All .Tests projects |
| xunit.runner.visualstudio | 2.8.x | VS test runner | All .Tests projects |
| FluentAssertions | 7.x | Assertion library | All .Tests projects |
| coverlet.collector | 6.x | Code coverage | All .Tests projects |
| Microsoft.NET.Test.Sdk | 17.x | Test host | All .Tests projects |

### Project Structure

```
tools/
  SimulatorSuite.sln
  src/
    Common.Dto/
      Common.Dto.csproj
      ISimulator.cs
      DataTransferObjects/
        FrameData.cs
        LineData.cs
        Csi2Packet.cs
        UdpPacket.cs
        SpiTransaction.cs
      Configuration/
        DetectorConfig.cs
    PanelSimulator/
      PanelSimulator.csproj
      PanelSimulatorEngine.cs
      Patterns/
        CounterPattern.cs
        CheckerboardPattern.cs
      Noise/
        GaussianNoiseModel.cs
      Defects/
        DefectInjector.cs
    FpgaSimulator/
      FpgaSimulator.csproj
      FpgaSimulatorEngine.cs
      Registers/
        RegisterMap.cs
      Fsm/
        PanelScanFsm.cs
      Buffer/
        PingPongLineBuffer.cs
      Csi2/
        Csi2PacketGenerator.cs
        Crc16Calculator.cs
      Spi/
        SpiSlaveInterface.cs
    McuSimulator/
      McuSimulator.csproj
      McuSimulatorEngine.cs
      Spi/
        SpiMasterInterface.cs
      Csi2/
        Csi2PacketParser.cs
      Network/
        UdpPacketGenerator.cs
      Buffer/
        FrameBufferManager.cs
      Sequencer/
        SequenceEngine.cs
    HostSimulator/
      HostSimulator.csproj
      HostSimulatorEngine.cs
      Network/
        UdpPacketReceiver.cs
      Reassembly/
        FrameReassembler.cs
      Storage/
        TiffWriter.cs
        RawWriter.cs
  tests/
    Common.Dto.Tests/
    PanelSimulator.Tests/
    FpgaSimulator.Tests/
    McuSimulator.Tests/
    HostSimulator.Tests/
  integration/
    IntegrationRunner/
      IntegrationRunner.csproj
      Program.cs
```

---

## Risk Analysis

### Risk 1: FpgaSimulator Golden Reference Accuracy (R-SIM-001)

**Risk Description**: FpgaSimulator may not accurately model FPGA RTL behavior, leading to false validation results when used as golden reference.

**Probability**: Medium (30%)

**Impact**: High (invalid golden reference invalidates all RTL verification)

**Mitigation**:
- Derive register map and FSM directly from fpga-design.md (same source as RTL)
- Implement systematic cross-verification (rtl_vs_sim_checker) during HW phase
- Register-level and packet-level comparison with RTL simulation output
- Review FpgaSimulator design with FPGA engineer before implementation

**Contingency**:
- If discrepancies found, investigate root cause in both simulator and RTL
- Prioritize simulator corrections to maintain golden reference status
- Document known differences with technical justification

---

### Risk 2: Performance Bottleneck (R-SIM-002)

**Risk Description**: Simulator pipeline too slow for 1000+ frame integration testing at 2x real-time.

**Probability**: Low (15%)

**Impact**: Medium (extends CI test execution time)

**Mitigation**:
- Use Span<T> and Memory<T> for zero-copy data passing between simulators
- Profile critical paths early (CRC-16 calculation, pixel generation, packet assembly)
- Implement fast mode with no artificial timing delays
- Consider ArrayPool<T> for frame buffer allocation

**Contingency**:
- Optimize CRC-16 with lookup table implementation
- Reduce frame count for CI pipeline (100 frames for smoke test, 1000 for nightly)
- Parallelize independent simulator processing where possible

---

### Risk 3: Configuration Drift (R-SIM-003)

**Risk Description**: Simulator configuration diverges from hardware configuration after detector_config.yaml changes.

**Probability**: Medium (25%)

**Impact**: High (simulator produces incorrect reference output)

**Mitigation**:
- Schema validation at simulator initialization (reject invalid config)
- CI test that validates config round-trip (load -> save -> compare)
- Single config loader in Common.Dto (shared by all simulators)
- No default values for critical parameters (resolution, bit_depth, fps)

**Contingency**:
- Configuration change detection in CI (diff detector_config.yaml, re-run tests)
- Version config schema alongside simulator code

---

### Risk 4: CSI-2 Packet Format Mismatch (R-SIM-004)

**Risk Description**: FpgaSimulator CSI-2 packet output does not match MIPI CSI-2 v1.3 specification exactly, causing McuSimulator parsing failures.

**Probability**: Low (20%)

**Impact**: Medium (pipeline integration failure)

**Mitigation**:
- Reference MIPI CSI-2 v1.3 specification for packet structure
- Implement packet validation in both generator (FpgaSimulator) and parser (McuSimulator)
- Cross-validate packet format against reference implementations

**Contingency**:
- If format mismatch found, correct FpgaSimulator output (golden reference)
- Add packet format version field for future compatibility

---

## Dependencies

### External Dependencies

**Development Tools**:
- .NET 8.0 SDK (runtime and development)
- Visual Studio 2022 or VS Code with C# extensions
- Gitea server for version control

**NuGet Packages**:
- YamlDotNet (YAML configuration parsing)
- xUnit + FluentAssertions (testing)
- coverlet (code coverage)

**Reference Documents**:
- MIPI CSI-2 v1.3 specification (packet format, CRC-16, data types)
- docs/architecture/fpga-design.md (register map, FSM, timing)
- docs/architecture/system-architecture.md (data flow, UDP format)

---

### Internal Dependencies

**Project Documents**:
- `detector_config.yaml`: Configuration schema (required before Phase 1)
- `docs/architecture/fpga-design.md`: Register map, FSM specification (required before Phase 3)
- `docs/architecture/system-architecture.md`: UDP packet format (required before Phase 4)
- `SPEC-ARCH-001`: Technology stack decisions (.NET 8.0, CSI-2 interface)

**Inter-Phase Dependencies**:
- Phase 2 depends on Phase 1 (Common.Dto)
- Phase 3 depends on Phase 1 (Common.Dto) + fpga-design.md
- Phase 4 depends on Phase 1 + Phase 3 (FpgaSimulator SPI slave)
- Phase 5 depends on Phase 1 + Phase 4 (McuSimulator UDP format)
- Phase 6 depends on Phases 1-5 (all simulators)

---

### Milestone Dependencies

**M2 (W9) Gate**: Simulator suite complete and integrated
- All 5 projects implemented with >= 85% coverage
- IntegrationRunner passing IT-01 (end-to-end data integrity)
- Performance targets met (2x real-time Minimum tier)
- Golden reference output generated for RTL comparison

**M3 (W14) Dependency**: FpgaSimulator golden reference used for RTL verification
- rtl_vs_sim_checker tool available
- Reference output files generated at multiple resolutions

---

## Next Steps

### Immediate Actions (Phase 1 Start)

1. **Create Solution Structure**
   - Initialize SimulatorSuite.sln with all projects
   - Configure project references (star topology via Common.Dto)
   - Add .gitignore for .NET artifacts

2. **Implement Common.Dto**
   - TDD: ISimulator interface and SimulatorStatus enum
   - TDD: All five DTO types as immutable records
   - TDD: DetectorConfig YAML loader
   - Verify 85%+ coverage

3. **Begin PanelSimulator**
   - TDD: Counter pattern (simplest, validates pipeline)
   - TDD: Configurable resolution and bit depth
   - Verify AC-SIM-001 (counter pattern correctness)

### Transition to Phase 3

**Trigger**: PanelSimulator complete with all patterns, noise, and defects
**Next**: FpgaSimulator register map and FSM implementation

---

## Traceability

This implementation plan aligns with:

- **SPEC-SIM-001 spec.md**: All requirements (REQ-SIM-001 through REQ-SIM-072) mapped to implementation phases
- **SPEC-SIM-001 acceptance.md**: All scenarios mapped to integration test milestones
- **SPEC-ARCH-001**: Technology stack (.NET 8.0, C# 12, CSI-2 interface)
- **quality.yaml**: Hybrid development mode (TDD for new code), 85% coverage target
- **X-ray_Detector_Optimal_Project_Plan.md**: M2 milestone (W9) gate

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (spec-sim) | Initial implementation plan for SPEC-SIM-001 |

---

**END OF PLAN**
