# SPEC-SIM-001: Acceptance Criteria and Test Scenarios

## Overview

This document defines the acceptance criteria, test scenarios, and quality gates for SPEC-SIM-001 (Simulator Suite Requirements Specification). All scenarios use Given/When/Then (Gherkin) format for clarity and traceability. Each scenario maps to one or more requirements from spec.md.

---

## Test Scenarios

### Scenario 1: ISimulator Interface Compliance

**Objective**: Verify that all simulators implement the `ISimulator` interface uniformly (REQ-SIM-001, REQ-SIM-050).

```gherkin
Given all four simulators (PanelSimulator, FpgaSimulator, McuSimulator, HostSimulator) are compiled
When each simulator is instantiated via the ISimulator interface
Then each shall expose the following methods:
  | Method | Signature | Return |
  | Initialize | Initialize(DetectorConfig config) | void |
  | Process | Process(IDataPacket input) | IDataPacket |
  | Reset | Reset() | void |
  | GetStatus | GetStatus() | SimulatorStatus |
And each method shall be callable without casting to a concrete type
And Common.Dto shall have zero dependencies on any simulator implementation (REQ-SIM-052)
```

**Success Criteria**:
- All four simulators implement `ISimulator` from Common.Dto
- IntegrationRunner can instantiate all simulators polymorphically
- Common.Dto assembly has no project references to simulator assemblies
- Compilation succeeds with only Common.Dto referenced

**Verification Method**: Compilation test, dependency analysis, unit test

---

### Scenario 2: Configuration from detector_config.yaml

**Objective**: Verify that all simulators load configuration from `detector_config.yaml` and produce matching behavior (REQ-SIM-002, REQ-SIM-061).

```gherkin
Given detector_config.yaml with panel.rows=2048, panel.cols=2048, panel.bit_depth=16
When all simulators are initialized from this configuration
Then PanelSimulator shall generate 2048x2048x16-bit frames
And FpgaSimulator line buffer shall be sized for 2048 pixels x 16-bit
And McuSimulator UDP packets shall carry 2048x2048x16-bit payload
And HostSimulator shall expect 2048x2048 frame dimensions
And no simulator shall use hard-coded values for any parameter present in detector_config.yaml
```

**Success Criteria**:
- Changing detector_config.yaml values propagates to all simulators without code changes
- No hard-coded resolution, bit depth, or frame rate values in simulator source code
- Configuration round-trip test: write config -> load -> verify parameters match

**Verification Method**: Integration test (AC-SIM-010), code review for hard-coded values

---

### Scenario 3: Deterministic Reproducibility

**Objective**: Verify that simulators produce bit-exact output for the same input, configuration, and random seed (REQ-SIM-003).

```gherkin
Given simulator pipeline initialized with seed=12345, noise_model=gaussian, noise_stddev=100
When the same 100-frame sequence is generated twice with identical seed and configuration
Then both runs shall produce pixel-level bit-exact output
And error injection points shall occur at identical frame indices in both runs
And floating-point operations shall use consistent precision across runs
```

**Success Criteria**:
- Two independent runs with same seed produce identical byte-level output
- Random number generators accept and respect seed parameter
- No non-deterministic operations (e.g., unordered dictionary iteration, thread-dependent timing)

**Verification Method**: Unit test (AC-SIM-012), byte comparison of output files

---

### Scenario 4: PanelSimulator Counter Pattern

**Objective**: Verify PanelSimulator counter test pattern output for data integrity (REQ-SIM-013).

```gherkin
Given PanelSimulator initialized with rows=1024, cols=1024, bit_depth=16, pattern=counter
When one frame is generated
Then pixel[r][c] == (r * 1024 + c) % 65536 for all r, c
And frame dimensions shall be exactly 1024 rows x 1024 columns
And no noise or defect injection shall be applied in counter mode
```

**Success Criteria**:
- Every pixel value matches the counter formula exactly
- Frame dimensions match configuration
- Counter mode bypasses noise and defect injection completely

**Verification Method**: Unit test (AC-SIM-001), exhaustive pixel verification

---

### Scenario 5: PanelSimulator Checkerboard Pattern

**Objective**: Verify PanelSimulator checkerboard test pattern output (REQ-SIM-014).

```gherkin
Given PanelSimulator initialized with rows=1024, cols=1024, bit_depth=16, pattern=checkerboard
When one frame is generated
Then even-index pixels shall be 0
And odd-index pixels shall be 65535 (2^16 - 1)
And the pattern shall invert every other row
And no noise or defect injection shall be applied
```

**Success Criteria**:
- Alternating 0/max pattern correct for all pixels
- Row inversion applied correctly
- Checkerboard mode bypasses noise and defect injection

**Verification Method**: Unit test, exhaustive pixel verification

---

### Scenario 6: PanelSimulator Noise Model

**Objective**: Verify PanelSimulator Gaussian noise model statistical properties (REQ-SIM-011).

```gherkin
Given PanelSimulator initialized with noise_model=gaussian, noise_stddev=100, seed=42
When 100 frames are generated
Then pixel value standard deviation shall be within 5% of configured stddev (95-105)
And mean pixel value shall be within 1% of base signal
And all pixel values shall be clamped to [0, 2^bit_depth - 1]
```

**Success Criteria**:
- Statistical analysis of 100 frames confirms stddev within tolerance
- Mean value within tolerance of base signal
- No pixel values outside valid range (clamping works)

**Verification Method**: Unit test (AC-SIM-002), statistical analysis

---

### Scenario 7: PanelSimulator Defect Injection

**Objective**: Verify PanelSimulator pixel defect injection (REQ-SIM-012).

```gherkin
Given PanelSimulator initialized with defect_rate=0.001 (0.1%), seed=42
When one frame is generated at 1024x1024 resolution
Then approximately 1048 pixels (0.1% of 1,048,576) shall be defective
And dead pixels shall output value 0
And hot pixels shall output value 2^bit_depth - 1
And the defect map shall be identical for the same seed across runs
```

**Success Criteria**:
- Defect count within statistical tolerance of configured rate
- Dead pixels are exactly 0, hot pixels are exactly max value
- Defect map is deterministic for given seed

**Verification Method**: Unit test, defect map comparison across runs

---

### Scenario 8: FpgaSimulator Register Map Access

**Objective**: Verify FpgaSimulator SPI register read/write accuracy (REQ-SIM-020, REQ-SIM-024).

```gherkin
Given FpgaSimulator initialized with default configuration
When SPI write 0xFF to CONTROL register (address 0x00)
And SPI read from CONTROL register (address 0x00)
Then read value shall be 0xFF (write-read consistency)
And STATUS register (address 0x04) shall reflect current FSM state
And FRAME_COUNTER register shall reflect number of completed frames
And all registers defined in fpga-design.md Section 6 shall be accessible
```

**Success Criteria**:
- Write-read consistency for all writable registers
- Read-only registers return correct values
- All registers from fpga-design.md modeled

**Verification Method**: Unit test (AC-SIM-003), register map exhaustive test

---

### Scenario 9: FpgaSimulator FSM State Transitions

**Objective**: Verify FpgaSimulator Panel Scan FSM transitions (REQ-SIM-021).

```gherkin
Given FpgaSimulator in IDLE state
When SPI write start_scan to CONTROL register
Then FSM shall transition: IDLE -> INTEGRATE -> READOUT -> LINE_DONE -> (repeat for all lines) -> FRAME_DONE
And FRAME_COUNTER register shall increment by 1 after FRAME_DONE
And all operating modes (single, continuous, calibration) shall be supported
And ERROR state shall be reachable from any state upon error condition
```

**Success Criteria**:
- State transition sequence matches RTL FSM specification
- All states reachable and observable via STATUS register
- Single-shot scan returns to IDLE after FRAME_DONE
- Continuous mode repeats scan cycle

**Verification Method**: Unit test (AC-SIM-004), FSM coverage test

---

### Scenario 10: FpgaSimulator CSI-2 Packet Generation

**Objective**: Verify FpgaSimulator CSI-2 output packet format (REQ-SIM-023).

```gherkin
Given FpgaSimulator processes one frame of counter pattern (1024x1024, 16-bit)
When CSI-2 output is captured
Then output shall contain exactly 1 Frame Start packet
And output shall contain exactly 1024 Line Data packets
And output shall contain exactly 1 Frame End packet
And each Line Data packet shall have correct CRC-16
And Data Type shall be 0x2E (RAW16, per MIPI CSI-2 v1.3)
And Virtual Channel shall be 0
```

**Success Criteria**:
- Packet count matches expected (1 FS + N lines + 1 FE)
- CRC-16 verification passes for all Line Data packets
- Data Type and Virtual Channel fields correct
- Packet structure conforms to MIPI CSI-2 specification

**Verification Method**: Unit test (AC-SIM-005), packet structure validation

---

### Scenario 11: FpgaSimulator Ping-Pong Line Buffer

**Objective**: Verify FpgaSimulator ping-pong line buffer behavior (REQ-SIM-022).

```gherkin
Given FpgaSimulator processing a frame with line buffer enabled
When lines are read out sequentially
Then two-bank buffer shall alternate between write and read operations
And buffer overflow shall be detected when write overtakes read
And buffer timing shall match RTL implementation behavior
```

**Success Criteria**:
- Alternating bank usage observable in simulation log
- Overflow detection triggers error flag (REQ-SIM-025)
- No data corruption during normal operation

**Verification Method**: Unit test, buffer state trace analysis

---

### Scenario 12: FpgaSimulator Error Handling

**Objective**: Verify FpgaSimulator error condition detection and reporting (REQ-SIM-025).

```gherkin
Given FpgaSimulator in normal operation
When an error condition is injected (timeout, overflow, or CRC error)
Then the corresponding ERROR_FLAGS bit shall be set
And FSM shall transition to ERROR state
And error shall be clearable via CONTROL register write
And error injection API shall support all defined error types
```

**Success Criteria**:
- Each error type sets correct ERROR_FLAGS bit
- FSM transitions to ERROR state on any error
- Error clearing restores previous operational state
- Test API allows programmatic error injection

**Verification Method**: Unit test, error injection test suite

---

### Scenario 13: FpgaSimulator Bit-Exact Golden Reference

**Objective**: Verify FpgaSimulator output matches RTL simulation bit-exactly (REQ-SIM-026).

```gherkin
Given Vivado xsim RTL simulation output (FPGA packet dump)
And same input/config with FpgaSimulator C# output
When rtl_vs_sim_checker compares both outputs
Then CSI-2 packet headers (Data Type, VC, Word Count) shall match bit-accurately
And all pixel payload bytes shall match bit-accurately (tolerance = 0)
And CRC-16 values shall match bit-accurately
And FSM state transition sequences shall be identical (log comparison)
And on comparison failure, report first mismatch location and value
```

**Success Criteria**:
- Zero byte-level differences between simulator and RTL output
- Comparison tool reports PASS with zero tolerance
- Any mismatch triggers investigation and resolution

**Verification Method**: Cross-verification test (AC-SIM-009a), rtl_vs_sim_checker tool

---

### Scenario 14: McuSimulator SPI Master Interface

**Objective**: Verify McuSimulator SPI master communication with FpgaSimulator (REQ-SIM-030).

```gherkin
Given McuSimulator connected to FpgaSimulator via SPI interface
When McuSimulator sends SPI write (register address + data)
Then FpgaSimulator shall receive and store the written value
When McuSimulator sends SPI read (register address)
Then McuSimulator shall receive the correct register value
And SPI transactions shall use Common.Dto SpiTransaction type
```

**Success Criteria**:
- Bidirectional SPI communication works correctly
- Register values round-trip through SPI interface
- SpiTransaction DTO used for all SPI communication

**Verification Method**: Unit test, integration test (McuSimulator + FpgaSimulator)

---

### Scenario 15: McuSimulator CSI-2 RX and UDP TX

**Objective**: Verify McuSimulator end-to-end data flow from CSI-2 input to UDP output (REQ-SIM-031, REQ-SIM-032, REQ-SIM-034).

```gherkin
Given McuSimulator connected to FpgaSimulator (CSI-2) and configured for UDP output
When StartScan command is sent and one frame is processed
Then McuSimulator shall parse CSI-2 Frame Start, Line Data, Frame End packets
And McuSimulator shall validate CRC-16 on each Line Data packet
And UDP packets shall be generated with correct frame header:
  | Field | Value |
  | magic | 0xDEADBEEF |
  | frame_seq | Sequential frame number |
  | width | Configured panel width |
  | height | Configured panel height |
  | bit_depth | Configured bit depth |
  | packet_index | Sequential from 0 to total_packets-1 |
  | crc16 | Valid CRC-16 of packet payload |
And total UDP payload size shall equal rows * cols * 2 bytes
```

**Success Criteria**:
- CSI-2 packet parsing extracts correct pixel data
- CRC-16 validation passes for all Line Data packets
- UDP frame header fields are correct
- Total payload size matches expected frame size

**Verification Method**: Unit test (AC-SIM-006), integration test

---

### Scenario 16: McuSimulator Frame Buffer Management

**Objective**: Verify McuSimulator frame buffer allocation and overflow detection (REQ-SIM-033).

```gherkin
Given McuSimulator initialized with buffer_count=4
When frames arrive faster than they can be transmitted via UDP
Then buffer overflow shall be detected when all 4 buffers are full
And overflow event shall be reported via GetStatus()
And no data corruption shall occur during buffer management
```

**Success Criteria**:
- Buffer allocation at initialization matches configured count
- Overflow detection triggers when all buffers occupied
- Normal operation with sufficient buffers shows no overflow

**Verification Method**: Unit test, stress test with controlled timing

---

### Scenario 17: HostSimulator Frame Reassembly (In-Order)

**Objective**: Verify HostSimulator frame reassembly from ordered UDP packets (REQ-SIM-040).

```gherkin
Given HostSimulator receives all UDP packets for one frame in order
When frame reassembly completes
Then reassembled frame shall be bit-exact match to PanelSimulator input
And frame shall be marked as complete (no missing packets)
And frame dimensions shall match configured resolution
```

**Success Criteria**:
- Bit-exact pixel data preservation through entire pipeline
- Complete frame status reported
- Frame metadata (seq, dimensions, bit_depth) correct

**Verification Method**: Unit test (AC-SIM-007), pipeline integration test

---

### Scenario 18: HostSimulator Out-of-Order Reassembly

**Objective**: Verify HostSimulator handles out-of-order UDP packet delivery (REQ-SIM-041).

```gherkin
Given HostSimulator receives UDP packets in random order
When all packets for one frame are delivered
Then reassembled frame shall be bit-exact match to PanelSimulator input
And frame shall be marked as complete despite out-of-order delivery
And packet_index shall be used for correct reassembly position
```

**Success Criteria**:
- Out-of-order delivery produces identical result to in-order delivery
- Packet buffer indexed by packet_index regardless of arrival order
- No data corruption from reordering

**Verification Method**: Unit test (AC-SIM-008), randomized packet order test

---

### Scenario 19: HostSimulator Missing Packet Timeout

**Objective**: Verify HostSimulator detects missing packets and reports incomplete frames (REQ-SIM-042).

```gherkin
Given HostSimulator configured with packet_timeout_ms=1000
When 95 of 100 expected packets arrive and timeout expires
Then frame shall be marked as incomplete
And missing packet indices shall be reported (5 specific indices)
And partial frame data shall be available for recovery
And timeout shall be configurable via host.network.packet_timeout_ms
```

**Success Criteria**:
- Incomplete frame detected after timeout
- Missing packet indices accurately reported
- Partial frame data accessible (not discarded)

**Verification Method**: Unit test, timeout simulation test

---

### Scenario 20: HostSimulator File Output Formats

**Objective**: Verify HostSimulator saves frames in required formats (REQ-SIM-043).

```gherkin
Given HostSimulator has reassembled a complete frame (2048x2048, 16-bit)
When frame is saved to disk
Then TIFF output shall have valid TIFF header and 16-bit grayscale data (uncompressed)
And TIFF LZW output shall decompress to identical pixel data
And RAW output shall be flat binary (rows * cols * 2 bytes = 8,388,608 bytes)
And RAW file size shall be exactly width * height * 2 bytes
```

**Success Criteria**:
- TIFF files readable by standard image viewers (ImageJ, GIMP)
- TIFF and RAW contain identical pixel data
- File sizes match expected calculations

**Verification Method**: Unit test, file format validation tool

---

### Scenario 21: Full Pipeline Integration (IT-01)

**Objective**: Verify end-to-end data integrity across all four simulators (REQ-SIM-001 through REQ-SIM-052).

```gherkin
Given all four simulators connected: Panel -> FPGA -> MCU -> Host
And configured for Minimum tier (1024x1024, 14-bit, 15fps), counter pattern
When one frame is processed through the full pipeline
Then Host output frame shall be bit-exact match to Panel input frame
And zero data corruption across all interfaces (SPI, CSI-2, UDP)
And all intermediate DTOs (FrameData, Csi2Packet, UdpPacket) shall be valid
```

**Success Criteria**:
- Input equals output (bit-exact) across 4 simulators
- Zero errors in any interface
- All acceptance criteria AC-SIM-001 through AC-SIM-009 pass

**Verification Method**: Integration test (AC-SIM-009), IntegrationRunner execution

---

### Scenario 22: Performance - Minimum Tier 2x Real-Time

**Objective**: Verify simulator pipeline meets 2x real-time performance for Minimum tier (REQ-SIM-006).

```gherkin
Given full simulator pipeline configured for Minimum tier (1024x1024, 14-bit, 15fps), fast mode
When IntegrationRunner executes 1000 frames
Then total elapsed wall-clock time shall be <= 33 seconds (2x real-time)
And all 1000 frames shall be reassembled correctly (zero data errors)
And frame throughput reported >= 30 frames/second
```

**Success Criteria**:
- 1000 frames complete in <= 33 seconds on x86-64 (Intel Core i7 or equivalent)
- Zero frame errors across entire run
- Performance measured via Stopwatch in IntegrationRunner

**Verification Method**: Performance test (AC-SIM-011), benchmark execution

---

### Scenario 23: Performance - Intermediate-A Tier Real-Time

**Objective**: Verify simulator pipeline meets 1x real-time for Intermediate-A tier.

```gherkin
Given full simulator pipeline configured for Intermediate-A tier (2048x2048, 16-bit, 15fps), fast mode
When IntegrationRunner executes 1000 frames
Then total elapsed wall-clock time shall be <= 67 seconds (1x real-time)
And frame generation shall be < 100 ms per frame
And peak memory usage shall be < 500 MB for 10-frame pipeline simulation
```

**Success Criteria**:
- 1000 frames complete in <= 67 seconds
- Per-frame generation under 100 ms
- Memory usage within 500 MB budget

**Verification Method**: Performance test, memory profiling (dotnet-counters)

---

### Scenario 24: Execution Modes (Fast vs Real-Time)

**Objective**: Verify simulator supports both execution modes (REQ-SIM-007).

```gherkin
Given simulator pipeline configured for Minimum tier
When fast mode is selected via simulation.mode=fast
Then pipeline shall run at >= 2x real-time (no artificial delays)
When realtime mode is selected via simulation.mode=realtime
Then pipeline shall run at ~1x real-time (timing-accurate inter-frame gaps)
And mode shall be configurable via detector_config.yaml or command line flag
```

**Success Criteria**:
- Fast mode achieves >= 2x real-time throughput
- Real-time mode maintains accurate frame timing (66.7ms per frame at 15fps)
- Mode switching works without reinitialization

**Verification Method**: Performance test, timing measurement

---

### Scenario 25: Common.Dto Data Transfer Objects

**Objective**: Verify Common.Dto defines all required DTOs with immutability and validation (REQ-SIM-051).

```gherkin
Given Common.Dto assembly is compiled
When DTOs are inspected
Then the following types shall be defined:
  | DTO | Key Fields | Serializable |
  | FrameData | width, height, bit_depth, pixels[] | Yes (JSON) |
  | LineData | line_index, pixel_count, pixels[] | Yes (JSON) |
  | Csi2Packet | data_type, virtual_channel, word_count, payload[], crc16 | Yes (JSON) |
  | UdpPacket | magic, frame_seq, packet_index, total_packets, payload[], crc16 | Yes (JSON) |
  | SpiTransaction | address, data, is_read | Yes (JSON) |
And all DTOs shall be immutable records
And all DTOs shall support JSON serialization for debugging
```

**Success Criteria**:
- All five DTO types defined in Common.Dto
- DTOs are C# records (immutable)
- JSON round-trip serialization works for all DTOs
- No implementation logic in Common.Dto (interfaces and DTOs only)

**Verification Method**: Unit test, serialization test

---

### Scenario 26: Dependency Isolation

**Objective**: Verify simulators communicate only through Common.Dto (REQ-SIM-060).

```gherkin
Given all simulator projects and Common.Dto
When project dependencies are analyzed
Then PanelSimulator shall reference only Common.Dto (no other simulator)
And FpgaSimulator shall reference only Common.Dto
And McuSimulator shall reference only Common.Dto
And HostSimulator shall reference only Common.Dto
And Common.Dto shall reference no simulator project (REQ-SIM-052)
```

**Success Criteria**:
- Dependency graph shows star topology (Common.Dto at center)
- No inter-simulator assembly references
- Each simulator independently testable

**Verification Method**: Project reference analysis, dependency graph tool

---

### Scenario 27: External Dependency Restriction

**Objective**: Verify no unauthorized external dependencies (REQ-SIM-062).

```gherkin
Given all simulator projects
When NuGet package references are inspected
Then only the following dependencies shall be present:
  | Package | Purpose | Status |
  | .NET 8.0 BCL | Runtime library | Allowed |
  | System.Text.Json | JSON serialization | Allowed |
  | System.IO.Compression | LZW compression for TIFF | Allowed |
  | YamlDotNet | YAML config parsing | Allowed |
  | xUnit | Test framework | Allowed (test projects only) |
  | FluentAssertions | Test assertions | Allowed (test projects only) |
  | coverlet | Code coverage | Allowed (test projects only) |
And any additional NuGet package shall require documented approval
```

**Success Criteria**:
- No unauthorized NuGet packages in simulator projects
- DICOM library (fo-dicom) only if explicitly approved (REQ-SIM-071)

**Verification Method**: NuGet package audit, project file review

---

### Scenario 28: Unit Test Coverage

**Objective**: Verify all simulators meet 85%+ unit test coverage (REQ-SIM-005).

```gherkin
Given all simulator test projects execute via dotnet test
When coverlet generates coverage report
Then PanelSimulator coverage shall be >= 85%
And FpgaSimulator coverage shall be >= 85%
And McuSimulator coverage shall be >= 85%
And HostSimulator coverage shall be >= 85%
And Common.Dto coverage shall be >= 85%
```

**Success Criteria**:
- Each module independently meets 85% line coverage
- Coverage report generated by coverlet in CI pipeline
- Goal: 90% coverage per module

**Verification Method**: coverlet report, CI pipeline validation

---

## Edge Case Testing

### Edge Case 1: Maximum Resolution Frame

**Scenario**:
```gherkin
Given PanelSimulator configured for 3072x3072, 16-bit (Final target tier)
When one frame is generated and processed through full pipeline
Then frame shall be correctly generated (3072x3072 pixels)
And FpgaSimulator shall generate 3072 Line Data CSI-2 packets
And HostSimulator shall reassemble the full 3072x3072 frame
And RAW file size shall be 18,874,368 bytes (3072 * 3072 * 2)
```

**Expected Outcome**: Pipeline handles maximum resolution without data corruption or memory overflow.

**Verification Method**: Integration test at maximum resolution

---

### Edge Case 2: Zero-Length Frame (Error Condition)

**Scenario**:
```gherkin
Given PanelSimulator configured with rows=0 or cols=0
When Initialize() is called
Then PanelSimulator shall throw ArgumentException with descriptive message
And no partial initialization shall occur
```

**Expected Outcome**: Invalid configuration rejected at initialization time.

**Verification Method**: Unit test, exception handling validation

---

### Edge Case 3: All Packets Lost (Timeout)

**Scenario**:
```gherkin
Given HostSimulator configured with packet_timeout_ms=500
When zero packets arrive for a frame
Then after 500ms timeout, frame shall be marked as failed
And frame status shall report 0 received, total_packets expected
And no crash or infinite wait shall occur
```

**Expected Outcome**: Graceful timeout handling with accurate reporting.

**Verification Method**: Unit test, timeout simulation

---

### Edge Case 4: Maximum Defect Rate

**Scenario**:
```gherkin
Given PanelSimulator configured with defect_rate=1.0 (100% defective)
When one frame is generated
Then all pixels shall be either 0 (dead) or max_value (hot)
And frame generation shall complete without error
```

**Expected Outcome**: Extreme defect rate handled gracefully.

**Verification Method**: Unit test, boundary value analysis

---

### Edge Case 5: Rapid Consecutive Scans

**Scenario**:
```gherkin
Given FpgaSimulator in continuous scan mode
When 100 frames are scanned without pause
Then FRAME_COUNTER shall increment to 100
And no buffer overflow shall occur under normal timing
And all 100 frames shall produce valid CSI-2 output
```

**Expected Outcome**: Continuous operation stability verified.

**Verification Method**: Stress test, continuous scan validation

---

## Performance Criteria

### Pipeline Throughput

**Criterion**: Minimum tier pipeline must achieve >= 2x real-time throughput.

**Metrics**:
| Tier | Resolution | Target FPS | Required Throughput | Measurement |
|------|-----------|------------|--------------------|----|
| Minimum | 1024x1024, 14-bit | 15 fps | >= 30 fps (2x) | Stopwatch / 1000 frames |
| Intermediate-A | 2048x2048, 16-bit | 15 fps | >= 15 fps (1x) | Stopwatch / 1000 frames |

**Acceptance Threshold**: Pass both throughput requirements in fast mode.

**Verification Method**: IntegrationRunner benchmark, dotnet-trace profiling

---

### Memory Usage

**Criterion**: Pipeline memory must stay within budget.

**Metrics**:
- Peak memory < 500 MB for 10-frame pipeline simulation
- No memory leaks over 1000-frame continuous run
- GC pressure acceptable (< 100 Gen2 collections per 1000 frames)

**Acceptance Threshold**: Memory within budget, no leaks detected.

**Verification Method**: dotnet-counters, memory profiling

---

### Per-Frame Latency

**Criterion**: Individual frame processing must meet latency targets.

**Metrics**:
- PanelSimulator frame generation: < 10 ms (Minimum tier)
- FpgaSimulator frame processing: < 20 ms (Minimum tier)
- McuSimulator frame packetization: < 15 ms (Minimum tier)
- HostSimulator frame reassembly: < 10 ms (Minimum tier)
- Full pipeline per frame: < 100 ms (Intermediate-A tier)

**Acceptance Threshold**: All per-component latencies within budget.

**Verification Method**: Per-component timing instrumentation

---

## Quality Gates

### TRUST 5 Framework Compliance

**Tested (T)**:
- Unit test coverage >= 85% per module (coverlet)
- Integration tests IT-01 through IT-10 passing
- Performance benchmarks meeting throughput targets
- Edge cases covered (boundary values, error conditions)
- TDD methodology followed (RED-GREEN-REFACTOR for all new code)

**Readable (R)**:
- Code follows C# naming conventions (PascalCase types, camelCase locals)
- English comments in code (per language.yaml code_comments: en)
- XML documentation on all public APIs
- Clear method names describing behavior

**Unified (U)**:
- All simulators implement ISimulator consistently
- Common.Dto used for all inter-simulator communication
- Consistent error handling patterns across simulators
- Consistent logging format (structured logging)

**Secured (S)**:
- No hard-coded credentials or paths
- Input validation on all public API parameters
- File I/O uses safe path handling (Path.Combine, no string concatenation)
- No SQL injection risk (no database access)

**Trackable (T)**:
- Git commits follow conventional commit format
- Each requirement (REQ-SIM-XXX) traceable to tests
- Coverage reports generated per CI build
- Performance benchmarks logged with timestamps

---

### Technical Review Checklist

- [ ] All 28 scenarios have corresponding unit/integration tests
- [ ] ISimulator interface compliance verified for all 4 simulators
- [ ] Common.Dto dependency isolation confirmed
- [ ] detector_config.yaml round-trip test passing
- [ ] FpgaSimulator register map matches fpga-design.md
- [ ] CSI-2 packet format matches MIPI CSI-2 v1.3 specification
- [ ] UDP frame header matches system-architecture.md Section 5.3
- [ ] Performance benchmarks meet targets (2x real-time Minimum, 1x Intermediate-A)
- [ ] Memory usage within 500 MB budget
- [ ] Coverage >= 85% per module
- [ ] No unauthorized NuGet dependencies
- [ ] TDD methodology compliance (test-first for all new code)

---

## Traceability Matrix

| Requirement ID | Scenario | Edge Case | Quality Gate |
|---------------|----------|-----------|--------------|
| REQ-SIM-001 | Scenario 1 | - | TRUST 5 Unified |
| REQ-SIM-002 | Scenario 2 | - | TRUST 5 Unified |
| REQ-SIM-003 | Scenario 3 | - | TRUST 5 Tested |
| REQ-SIM-004 | Scenario 27 | - | Technical Review |
| REQ-SIM-005 | Scenario 28 | - | TRUST 5 Tested |
| REQ-SIM-006 | Scenario 22 | - | Performance |
| REQ-SIM-007 | Scenario 24 | - | Performance |
| REQ-SIM-010 | Scenario 4, 5 | EC-1 | TRUST 5 Tested |
| REQ-SIM-011 | Scenario 6 | - | TRUST 5 Tested |
| REQ-SIM-012 | Scenario 7 | EC-4 | TRUST 5 Tested |
| REQ-SIM-013 | Scenario 4 | - | TRUST 5 Tested |
| REQ-SIM-014 | Scenario 5 | - | TRUST 5 Tested |
| REQ-SIM-020 | Scenario 8 | - | Technical Review |
| REQ-SIM-021 | Scenario 9 | EC-5 | Technical Review |
| REQ-SIM-022 | Scenario 11 | - | Technical Review |
| REQ-SIM-023 | Scenario 10 | - | Technical Review |
| REQ-SIM-024 | Scenario 9 | - | Technical Review |
| REQ-SIM-025 | Scenario 12 | - | Technical Review |
| REQ-SIM-026 | Scenario 13 | - | Technical Review |
| REQ-SIM-030 | Scenario 14 | - | TRUST 5 Tested |
| REQ-SIM-031 | Scenario 15 | - | TRUST 5 Tested |
| REQ-SIM-032 | Scenario 15 | - | TRUST 5 Tested |
| REQ-SIM-033 | Scenario 16 | - | TRUST 5 Tested |
| REQ-SIM-034 | Scenario 15 | - | TRUST 5 Tested |
| REQ-SIM-040 | Scenario 17 | - | TRUST 5 Tested |
| REQ-SIM-041 | Scenario 18 | - | TRUST 5 Tested |
| REQ-SIM-042 | Scenario 19 | EC-3 | TRUST 5 Tested |
| REQ-SIM-043 | Scenario 20 | - | TRUST 5 Tested |
| REQ-SIM-044 | Scenario 23 | - | Performance |
| REQ-SIM-050 | Scenario 1 | - | TRUST 5 Unified |
| REQ-SIM-051 | Scenario 25 | - | TRUST 5 Unified |
| REQ-SIM-052 | Scenario 26 | - | TRUST 5 Unified |
| REQ-SIM-060 | Scenario 26 | - | TRUST 5 Unified |
| REQ-SIM-061 | Scenario 2 | - | TRUST 5 Unified |
| REQ-SIM-062 | Scenario 27 | - | Technical Review |
| REQ-SIM-070 | Optional | - | N/A |
| REQ-SIM-071 | Optional | - | N/A |
| REQ-SIM-072 | Optional | - | N/A |

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (spec-sim) | Initial acceptance criteria for SPEC-SIM-001 |

---

**END OF ACCEPTANCE CRITERIA**
