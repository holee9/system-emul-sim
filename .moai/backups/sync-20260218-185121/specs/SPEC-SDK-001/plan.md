# SPEC-SDK-001: Implementation Plan

## Overview

This implementation plan outlines the phased approach to developing the Host SDK (DetectorClient API and Frame Processing). The SDK is implemented in C# .NET 8.0+ and provides the primary API for applications interacting with the X-ray Detector Panel System.

**Gate Milestone**: M4 (W18) - Host SDK development complete and integration-ready

---

## Implementation Phases

### Phase 1: Core Infrastructure (Priority: Primary Goal)

**Objective**: Implement packet reception, frame reassembly, and CRC validation - the foundational data pipeline.

**Tasks**:

1. **PacketReceiver Module** (REQ-SDK-030, REQ-SDK-004)
   - Implement high-performance UDP receiver using System.IO.Pipelines
   - Parse FrameHeader (32 bytes): magic number (0xD7E01234), sequence, packet index, CRC-16
   - Dedicated receive thread (non-blocking, REQ-SDK-030)
   - Bounded receive buffer to prevent memory pressure

2. **CRC-16 Validation** (Technical Constraint: CRC-16/CCITT, polynomial 0x8408)
   - Implement CRC-16/CCITT calculator
   - Validate CRC over header bytes 0-27
   - Discard packets with invalid CRC, emit warning log
   - Track CRC failure count for ScanStatus

3. **FrameReassembler Module** (REQ-SDK-004, REQ-SDK-031)
   - Reassemble out-of-order packets into complete frames
   - Maximum 8 concurrent reassembly slots
   - Frame timeout: 2 seconds for incomplete frames
   - Zero-fill missing packet regions (0x0000)
   - ArrayPool<ushort> for frame pixel buffers (no per-frame heap allocation)
   - Frame.Dispose returns buffers to pool

4. **Common.Dto Types** (REQ-SDK-003)
   - Frame class: Width, Height, BitDepth, PixelData (ReadOnlyMemory<ushort>), SequenceNumber, Timestamp, Flags
   - DetectorConfig: Resolution, BitDepth, FrameRate, NetworkSettings
   - ScanMode enum: Single, Continuous, Calibration
   - ScanStatus: IsConnected, IsScanning, DroppedFrames, FramesReceived, CurrentThroughputGbps
   - Error types: DetectorConnectionException, ErrorEventArgs, FrameEventArgs

**Deliverables**:
- PacketReceiver with UDP reception and CRC validation
- FrameReassembler with out-of-order handling and ArrayPool memory management
- Common.Dto shared type library
- Unit tests: 85%+ coverage for all modules

**Dependencies**:
- docs/api/ethernet-protocol.md (packet format specification)
- System.IO.Pipelines NuGet package

---

### Phase 2: DetectorClient API (Priority: Primary Goal)

**Objective**: Implement the IDetectorClient public API with connection management, acquisition control, and event infrastructure.

**Tasks**:

1. **Connection Management** (REQ-SDK-010, REQ-SDK-018)
   - ConnectAsync: Send PING (0x0007), wait for response, 10-second timeout
   - DisconnectAsync: Send DISCONNECT (0x0008), release resources, 5-second wait
   - Auto-reconnect: 5-second interval on connection loss (REQ-SDK-025)
   - ConnectionChanged event: Connected, Disconnected, Reconnecting states

2. **Acquisition Control** (REQ-SDK-011, REQ-SDK-012, REQ-SDK-020, REQ-SDK-021, REQ-SDK-022)
   - StartAcquisitionAsync: Send START_SCAN (0x0001), validate state
   - StopAcquisitionAsync: Send STOP_SCAN (0x0002), wait for current frame
   - ScanMode.Single: Auto-stop after one frame
   - ScanMode.Continuous: Bounded queue (16 frames), oldest-drop overflow
   - ScanMode.Calibration: Dark frame capture with calibration flag

3. **Frame Acquisition** (REQ-SDK-013, REQ-SDK-014, REQ-SDK-015)
   - CaptureFrameAsync: Return next complete frame with timeout
   - StreamFramesAsync: IAsyncEnumerable<Frame> with backpressure
   - FrameReceived event: Raised on thread pool thread for each complete frame

4. **Status and Error Reporting** (REQ-SDK-016, REQ-SDK-019)
   - GetStatusAsync: Return ScanStatus with 1 Hz minimum update rate
   - ErrorOccurred event: Network errors, FPGA errors, incomplete frames
   - IsRecoverable flag for error classification

5. **Configuration** (REQ-SDK-002)
   - ConfigureAsync: Apply DetectorConfig to SoC (resolution, bitDepth, frameRate)
   - Validate configuration against supported performance tiers

**Deliverables**:
- DetectorClient implementing IDetectorClient (REQ-SDK-002)
- Connection, acquisition, and status management
- Event infrastructure (FrameReceived, ErrorOccurred, ConnectionChanged)
- Unit tests: 85%+ coverage, integration tests with simulator mock

**Dependencies**:
- Phase 1 (PacketReceiver, FrameReassembler, Common.Dto)
- docs/api/host-sdk-api.md (API specification)

---

### Phase 3: Storage and Display Utilities (Priority: Secondary Goal)

**Objective**: Implement frame storage formats and display conversion utilities.

**Tasks**:

1. **TIFF Storage** (REQ-SDK-023)
   - 16-bit grayscale TIFF with LibTiff.NET
   - Required tags: IMAGEWIDTH, IMAGELENGTH, BITSPERSAMPLE=16, SAMPLESPERPIXEL=1, PHOTOMETRIC=MINISBLACK
   - Round-trip fidelity verification

2. **RAW Storage** (REQ-SDK-024)
   - Binary pixel data (little-endian uint16)
   - JSON metadata sidecar (width, height, bitDepth, timestamp, sequenceNumber)
   - Zero-overhead format for post-processing pipelines

3. **Window/Level Utility** (REQ-SDK-042, optional)
   - 16-bit to 8-bit mapping with adjustable center and width
   - WindowLevel.Apply(pixels, center, width) returns byte array
   - Useful for WPF WriteableBitmap display integration

4. **Frame Statistics** (REQ-SDK-043, optional)
   - Lazy computation of MinValue, MaxValue, MeanValue
   - Computed on first access, cached for subsequent calls

**Deliverables**:
- ImageEncoder module (TIFF, RAW, optional DICOM)
- WindowLevel display utility
- Frame statistics computation
- Unit tests: 85%+ coverage, round-trip fidelity tests

**Dependencies**:
- Phase 2 (Frame type with PixelData)
- LibTiff.NET NuGet package (2.4.6+)
- fo-dicom NuGet package (5.0+, optional for DICOM)

---

### Phase 4: Quality and Integration (Priority: Final Goal)

**Objective**: Achieve quality gates, performance targets, and integration readiness.

**Tasks**:

1. **Performance Optimization**
   - Frame drop rate < 0.01% at Intermediate-A tier (REQ-SDK-032)
   - Gen2 GC < 5 per 10,000 frames (REQ-SDK-031)
   - Frame reassembly latency < 10 ms
   - ArrayPool return rate >= 99%

2. **Integration Testing**
   - Integration with FPGA simulator (IT-01 through IT-10)
   - GUI.Application integration via IDetectorClient
   - Network protocol compliance with ethernet-protocol.md
   - Configuration loading from detector_config.yaml

3. **API Documentation**
   - XML documentation comments on all public types
   - Thread safety annotations
   - Usage examples for common workflows
   - NuGet package metadata

4. **Quality Gate Verification**
   - TRUST 5 compliance check
   - Code coverage report (target: 85%+)
   - Performance benchmark report
   - API design review sign-off

**Deliverables**:
- Performance benchmark results meeting all targets
- Integration test suite (IT-01 through IT-10) passing
- XML API documentation
- Quality gate compliance report

**Dependencies**:
- Phases 1-3 complete
- FPGA simulator available (SPEC-SIM-001)
- Integration test environment configured

---

## Task Decomposition

### Priority-Based Milestones

**Primary Goal**: Core data pipeline and public API
- Phase 1: PacketReceiver, FrameReassembler, Common.Dto
- Phase 2: IDetectorClient implementation, connection management, acquisition control
- Success criteria: Single frame capture and continuous streaming functional

**Secondary Goal**: Storage and display utilities
- Phase 3: TIFF/RAW storage, Window/Level utility
- Success criteria: Frame persistence with round-trip fidelity

**Final Goal**: Quality gates and integration readiness
- Phase 4: Performance optimization, integration testing, documentation
- Success criteria: All quality gates pass, integration tests complete

**Optional Goal**: Enhanced features
- Device discovery (REQ-SDK-040)
- DICOM format support (REQ-SDK-041)
- Success criteria: Optional features implemented if schedule permits

---

## Technology Stack Specifications

### SDK Architecture

**Project Structure** (C# .NET 8.0):

| Project | Type | Purpose |
|---------|------|---------|
| Common.Dto | Class Library | Shared types (Frame, DetectorConfig, ScanStatus) |
| XrayDetector.Sdk | Class Library | Core SDK (DetectorClient, PacketReceiver, FrameReassembler) |
| XrayDetector.Sdk.Storage | Class Library | Image storage (TIFF, RAW, DICOM) |
| XrayDetector.Sdk.Tests | xUnit Test | Unit and integration tests |
| GUI.Application | WPF Application | Desktop GUI (Windows only, references SDK) |

### NuGet Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| System.IO.Pipelines | 8.0.0 | High-performance I/O for UDP reception |
| LibTiff.NET | 2.4.6+ | TIFF file read/write (16-bit grayscale) |
| fo-dicom | 5.0+ | DICOM format support (optional) |
| xUnit | 2.7+ | Unit test framework |
| Moq | 4.20+ | Mock framework for unit testing |
| BenchmarkDotNet | 0.13+ | Performance benchmarking |

### Performance Targets

| Metric | Intermediate-A | Final Target | Measurement |
|--------|---------------|-------------|-------------|
| Resolution | 2048x2048 | 3072x3072 | Configured per tier |
| Bit Depth | 16-bit | 16-bit | Fixed |
| Frame Rate | 15 fps | 15 fps | Fixed |
| Data Rate | ~1.01 Gbps | ~2.26 Gbps | Calculated |
| Frame Size | ~8 MB | ~18 MB | Width * Height * 2 bytes |
| Packets/Frame | ~1,024 | ~2,304 | Frame Size / 8192 |
| Network | 1 GbE (min) | 10 GbE (required) | Per tier |

---

## Risk Analysis

### Risk 1: UDP Packet Loss Under Load

**Risk Description**: At high data rates (2.26 Gbps for Final target), UDP packet loss may exceed 0.01% threshold due to OS receive buffer saturation.

**Probability**: Medium (35%)

**Impact**: High (Frame drop rate exceeds REQ-SDK-032 requirement)

**Mitigation**:
- Increase OS UDP receive buffer size (SO_RCVBUF = 8 MB+)
- Use System.IO.Pipelines for zero-copy receive path
- Dedicated high-priority receive thread
- 10 GbE NIC with hardware offload features

**Contingency**:
- Implement application-layer retransmission for critical frames
- Reduce frame rate to lower packet arrival rate
- Use jumbo frames (9000 MTU) to reduce packet count

---

### Risk 2: GC Latency Spikes

**Risk Description**: .NET garbage collector may introduce latency spikes during sustained frame capture, causing frame drops or missed events.

**Probability**: Medium (30%)

**Impact**: Medium (Intermittent frame drops, P99 latency exceeds 50 ms)

**Mitigation**:
- ArrayPool<ushort> for all frame pixel buffers (REQ-SDK-031)
- Frame implements IDisposable to return buffers promptly
- Bounded frame queue prevents unbounded allocation
- Use GC.TryStartNoGCRegion for critical processing paths

**Contingency**:
- Switch to Server GC mode (gcServer=true)
- Profile with dotnet-trace and optimize hot paths
- Consider unsafe/stackalloc for packet header parsing

---

### Risk 3: Cross-Platform Compatibility

**Risk Description**: WPF GUI is Windows-only, limiting Linux deployment scenarios.

**Probability**: Low (20%)

**Impact**: Low (SDK core is cross-platform; only GUI is Windows-specific)

**Mitigation**:
- SDK core (XrayDetector.Sdk) targets netstandard2.1 or net8.0 (cross-platform)
- GUI.Application isolated in separate project with WPF dependency
- IDetectorClient interface enables alternative GUI implementations

**Contingency**:
- Avalonia UI for cross-platform GUI if Linux GUI required
- Headless SDK mode for Linux server deployments

---

### Risk 4: LibTiff.NET Compatibility

**Risk Description**: LibTiff.NET may have issues with 16-bit grayscale TIFF or .NET 8.0 compatibility.

**Probability**: Low (15%)

**Impact**: Medium (TIFF storage feature delayed)

**Mitigation**:
- Validate LibTiff.NET 2.4.6+ with .NET 8.0 early in Phase 3
- Round-trip fidelity test as part of unit test suite
- Fallback: System.Drawing or custom TIFF writer for simple 16-bit cases

**Contingency**:
- Implement minimal TIFF writer (16-bit grayscale only, no compression)
- Use RAW format as primary storage, TIFF as optional

---

## Dependencies

### External Dependencies

**NuGet Packages**:
- System.IO.Pipelines 8.0.0 (Microsoft, included in .NET 8.0 SDK)
- LibTiff.NET 2.4.6+ (BSD license, NuGet.org)
- fo-dicom 5.0+ (MS-PL license, NuGet.org, optional)
- xUnit 2.7+ (Apache 2.0, NuGet.org, test only)

**Development Tools**:
- .NET 8.0 SDK (free download)
- Visual Studio 2022 or VS Code with C# Dev Kit
- dotnet-trace and dotnet-counters (performance profiling)

**Documentation**:
- docs/api/ethernet-protocol.md (network packet format)
- docs/api/host-sdk-api.md (public API reference)
- docs/architecture/host-sdk-design.md (module design, class diagrams)

---

### Internal Dependencies

**Project Dependencies**:
- SPEC-ARCH-001: Architecture decisions (10 GbE, .NET 8.0, performance tiers)
- SPEC-SIM-001: FPGA simulator for integration testing
- SPEC-FW-001: SoC firmware network protocol (UDP packet format)
- detector_config.yaml: Performance tier configuration

**Module Dependencies**:
- Common.Dto: No dependencies (leaf module)
- XrayDetector.Sdk: Depends on Common.Dto
- XrayDetector.Sdk.Storage: Depends on Common.Dto, LibTiff.NET
- XrayDetector.Sdk.Tests: Depends on all SDK modules
- GUI.Application: Depends on XrayDetector.Sdk (IDetectorClient)

---

### Milestone Dependencies

**Prerequisites (before SDK development)**:
- SPEC-SDK-001 approved (this document)
- SPEC-ARCH-001 P0 decisions finalized (performance tiers, network interface)
- docs/api/ethernet-protocol.md completed (packet format)

**Parallel Development**:
- FPGA simulator (SPEC-SIM-001): Provides test data source for integration tests
- SoC firmware (SPEC-FW-001): Defines network protocol and command formats

**Integration Gate (M4, W18)**:
- SDK Phases 1-3 complete
- Integration tests (IT-01 through IT-10) passing
- Performance benchmarks meeting all targets
- Quality gate compliance verified

---

## Next Steps

### Immediate Actions (Post-Approval)

1. **Project Setup**
   - Create SDK solution structure (.sln, .csproj files)
   - Configure EditorConfig for C# coding style
   - Add NuGet package references
   - Set up xUnit test project with CI integration

2. **Phase 1 Kickoff**
   - Implement Common.Dto types (Frame, DetectorConfig, ScanMode, ScanStatus)
   - Implement CRC-16/CCITT calculator with test vectors
   - Implement PacketReceiver with mock UDP source
   - Implement FrameReassembler with out-of-order test cases

3. **Integration Planning**
   - Coordinate with SPEC-SIM-001 team for simulator API alignment
   - Coordinate with SPEC-FW-001 team for protocol specification
   - Plan integration test environment (FPGA simulator + SDK)

### Transition to Integration (Post-Phase 3)

**Trigger**: Phase 3 complete, all unit tests passing

**Activities**:
- Run integration test suite (IT-01 through IT-10)
- Performance benchmark at Intermediate-A tier
- GUI integration smoke test
- Quality gate compliance verification

**Success Criteria**:
- All integration tests pass
- Performance targets met (frame drop rate, GC pressure, latency)
- Code coverage >= 85%
- API documentation complete

---

## Traceability

This implementation plan aligns with:

- **SPEC-SDK-001 spec.md**: All requirements mapped to implementation phases
- **SPEC-ARCH-001**: Architecture decisions (REQ-ARCH-008, REQ-ARCH-015)
- **X-ray_Detector_Optimal_Project_Plan.md**: Phase 2 implementation (W9-W22)
- **docs/architecture/host-sdk-design.md**: Detailed module design
- **docs/api/ethernet-protocol.md**: Network protocol specification
- **docs/api/host-sdk-api.md**: Public API reference

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | ABYZ-Lab Agent (spec-sdk) | Initial implementation plan for SPEC-SDK-001 |

---

**END OF PLAN**
