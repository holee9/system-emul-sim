# SPEC-TOOLS-001: Acceptance Criteria and Test Scenarios

## Overview

This document defines the acceptance criteria, test scenarios, and quality gates for SPEC-TOOLS-001 (Development Tools Requirements). All scenarios use Given/When/Then format for clarity and traceability. Covers ParameterExtractor, CodeGenerator, ConfigConverter, IntegrationRunner, and GUI.Application.

---

## Test Scenarios

### Scenario 1: ParameterExtractor PDF Parsing

**Objective**: Verify that ParameterExtractor correctly extracts tabular parameter data from PDF datasheets.

```gherkin
Given a sample PDF datasheet with tabular parameter data (name, value, unit, min, max)
When the PDF is loaded into ParameterExtractor
Then parameters shall be extracted with >= 90% accuracy
And each parameter shall contain: name, value, unit
And optional fields (min, max) shall be populated when present in the table
And extraction shall complete within 30 seconds
```

**Success Criteria**:
- Extraction accuracy >= 90% on native PDF with selectable text (REQ-TOOLS-001)
- Parameters presented in WPF DataGrid for review (REQ-TOOLS-003)
- Parsing time < 30 seconds per document

**Verification Method**: Unit test with sample PDF, accuracy measurement against known parameter list

---

### Scenario 2: ParameterExtractor Validation Rules

**Objective**: Verify that the rule engine validates extracted parameters against physical constraints.

```gherkin
Given extracted parameters from a PDF datasheet
And validation rules defined in rules.yaml (e.g., pixel_pitch > 0, bit_depth in {14, 16})
When validation is executed
Then valid parameters shall pass all rules
And invalid parameters shall be flagged with rule violation details
And the user shall be able to correct flagged values in the GUI
```

**Success Criteria**:
- Rule engine validates all parameters against defined constraints (REQ-TOOLS-002)
- Clear error messages for each rule violation (field name, expected range, actual value)
- User can edit and re-validate in the WPF GUI (REQ-TOOLS-003)

**Verification Method**: Unit test with valid and invalid parameter sets, rule engine coverage

---

### Scenario 3: ParameterExtractor YAML Export

**Objective**: Verify that exported parameters conform to detector_config.yaml schema.

```gherkin
Given reviewed and validated parameters in ParameterExtractor GUI
When the user clicks Export to YAML
Then output file shall conform to detector_config.yaml JSON Schema
And schema validation shall pass with zero errors
And exported YAML shall be loadable by ConfigConverter without errors
```

**Success Criteria**:
- Exported YAML validates against config/schema/detector-config-schema.json (REQ-TOOLS-004)
- Schema validation errors reported with field-level detail before export
- Round-trip: export -> ConfigConverter load succeeds

**Verification Method**: Schema validation test, round-trip integration test

---

### Scenario 4: CodeGenerator SystemVerilog Output

**Objective**: Verify that generated RTL skeletons compile with Vivado.

```gherkin
Given detector_config.yaml with Intermediate-A tier configuration (2048x2048, 16-bit)
When CodeGenerator generates SystemVerilog RTL skeletons
Then generated panel_scan_fsm.sv shall contain parameterized width=2048, height=2048, bit_depth=16
And generated line_buffer.sv shall contain correct BRAM sizing
And all generated .sv files shall compile with Vivado synthesis (zero errors)
And generated files shall contain AUTO-GENERATED header comment
```

**Success Criteria**:
- Generated RTL parameters match detector_config.yaml values (REQ-TOOLS-010)
- Vivado synthesis completes with zero errors (REQ-TOOLS-013)
- AUTO-GENERATED header present (REQ-TOOLS-051)
- Generation time < 5 seconds

**Verification Method**: Vivado synthesis test, parameter value comparison

---

### Scenario 5: CodeGenerator C Header Output

**Objective**: Verify that generated C headers compile with GCC and match register map.

```gherkin
Given detector_config.yaml with all register definitions
When CodeGenerator generates C/C++ header files
Then generated fpga_registers.h shall include all register addresses
And bit field macros shall match FPGA register map
And header shall compile with GCC ARM cross-compiler (zero errors)
And documentation comments shall be included for each register
```

**Success Criteria**:
- Generated fpga_registers.h compiles with GCC without errors (REQ-TOOLS-011, REQ-TOOLS-013)
- Register addresses and bit fields match config values
- AUTO-GENERATED header present
- No tool-specific dependencies in generated code (REQ-TOOLS-051)

**Verification Method**: GCC compilation test, register address verification

---

### Scenario 6: CodeGenerator C# Class Output

**Objective**: Verify that generated C# classes compile with dotnet build and match configuration.

```gherkin
Given detector_config.yaml with all SDK-relevant parameters
When CodeGenerator generates C# class skeletons
Then generated DetectorConfig.cs shall contain correct default values
And generated FrameHeader.cs shall have struct layout matching ethernet-protocol.md
And all generated .cs files shall compile with dotnet build (zero errors)
And generated classes shall be usable by XrayDetector.Sdk project
```

**Success Criteria**:
- Generated C# classes compile with dotnet build without errors (REQ-TOOLS-012, REQ-TOOLS-013)
- Default values match detector_config.yaml
- FrameHeader struct layout matches protocol specification
- No tool-specific dependencies (REQ-TOOLS-051)

**Verification Method**: dotnet build test, default value comparison

---

### Scenario 7: ConfigConverter YAML to XDC

**Objective**: Verify that ConfigConverter generates correct FPGA constraints.

```gherkin
Given detector_config.yaml with all fields populated
When ConfigConverter generates .xdc FPGA constraints
Then output shall include clock constraints matching config
And SPI timing constraints shall be present (max 50 MHz)
And CSI-2 byte clock constraint shall match selected data rate
And all mapped values shall match original YAML values exactly
```

**Success Criteria**:
- Generated .xdc contains correct clock, SPI, and CSI-2 constraints (REQ-TOOLS-020)
- Values match conversion-mapping.md rules
- Round-trip comparison passes

**Verification Method**: Value comparison test, Vivado constraint load test

---

### Scenario 8: ConfigConverter YAML to DTS

**Objective**: Verify that ConfigConverter generates correct SoC device tree overlay.

```gherkin
Given detector_config.yaml with CSI-2 and network configuration
When ConfigConverter generates .dts device tree overlay
Then output shall include MIPI CSI-2 lane count (4)
And link frequency shall match selected D-PHY data rate
And Ethernet configuration shall match network settings
And device tree syntax shall be valid (dtc compilation)
```

**Success Criteria**:
- Generated .dts contains correct CSI-2 and network configuration (REQ-TOOLS-021)
- Device tree compiles with dtc (device tree compiler) without errors
- Lane count, link frequency match config values

**Verification Method**: dtc compilation test, value comparison

---

### Scenario 9: ConfigConverter YAML to JSON

**Objective**: Verify that ConfigConverter generates correct Host SDK configuration.

```gherkin
Given detector_config.yaml with Intermediate-A tier settings
When ConfigConverter generates .json Host SDK configuration
Then output shall include computed values:
  | Field | Expected Value |
  | frameSizeBytes | 8388608 (2048*2048*2) |
  | packetsPerFrame | 1024 (8388608/8192) |
  | rawDataRateGbps | 1.01 |
And all mapped values shall match original YAML
And JSON shall be parseable by System.Text.Json
```

**Success Criteria**:
- Generated .json contains all required fields with computed values (REQ-TOOLS-022)
- Computed values are mathematically correct
- JSON schema validation passes

**Verification Method**: Value computation test, JSON deserialization test

---

### Scenario 10: ConfigConverter Schema Validation (Valid Input)

**Objective**: Verify that schema validation passes for valid configuration.

```gherkin
Given detector_config.yaml with all required fields populated correctly
When ConfigConverter performs schema validation
Then validation shall pass with zero errors
And conversion to all three targets (.xdc, .dts, .json) shall proceed
```

**Success Criteria**:
- Schema validation passes (REQ-TOOLS-023)
- All three target files generated successfully
- No warnings for valid configuration

**Verification Method**: Unit test with valid config

---

### Scenario 11: ConfigConverter Schema Validation (Invalid Input)

**Objective**: Verify that schema validation rejects invalid configuration.

```gherkin
Given invalid detector_config.yaml (missing required field 'panel.rows')
When ConfigConverter performs schema validation
Then validation shall fail with clear error: "Required field 'panel.rows' is missing"
And no target files shall be generated
And exit code shall be 1 (failure)
```

**Success Criteria**:
- Schema validation catches missing required fields (REQ-TOOLS-023)
- Error message includes field path and expected constraint
- No partial output generated on validation failure

**Verification Method**: Unit test with various invalid configs (missing fields, wrong types, out-of-range)

---

### Scenario 12: ConfigConverter Cross-Validation

**Objective**: Verify that cross-validation catches system-level constraint violations.

```gherkin
Given detector_config.yaml with bandwidth exceeding CSI-2 limit (e.g., 3072x3072, 16-bit, 30fps = 4.53 Gbps)
When ConfigConverter performs cross-validation
Then cross-validation shall report: "Bandwidth 4.53 Gbps exceeds CSI-2 D-PHY limit of 3.2 Gbps"
And no target files shall be generated
And error shall include actual vs. maximum bandwidth values
```

**Success Criteria**:
- Cross-validation catches bandwidth violations (REQ-TOOLS-024)
- Buffer sizing validation catches impossible configurations
- Warnings for near-limit configurations (e.g., >80% bandwidth utilization)

**Verification Method**: Unit test with over-limit and near-limit configurations

---

### Scenario 13: IntegrationRunner IT-01 Execution

**Objective**: Verify that IntegrationRunner executes IT-01 (single frame, minimum tier) end-to-end.

```gherkin
Given IntegrationRunner configured for IT-01 (single frame, minimum tier 1024x1024@15fps)
When --scenario IT-01 is executed
Then all 4 simulators shall be instantiated (Panel, FPGA, MCU, Host)
And simulators shall be connected in pipeline order (Panel -> FPGA -> MCU -> Host)
And a single frame shall propagate through the pipeline
And output frame shall match input frame (zero bit errors)
And pass/fail result shall be reported with metrics (bit errors, throughput, time)
And execution shall complete within 60 seconds
```

**Success Criteria**:
- All 4 simulators instantiated from detector_config.yaml (REQ-TOOLS-031)
- Pipeline execution produces correct output frame
- Zero bit errors for IT-01 (REQ-TOOLS-032)
- Execution time < 60 seconds

**Verification Method**: End-to-end integration test with all simulators

---

### Scenario 14: IntegrationRunner All Scenarios

**Objective**: Verify that --all flag executes all scenarios and produces aggregate report.

```gherkin
Given all simulators and IntegrationRunner ready
When --all flag is used
Then IT-01 through IT-10 shall execute sequentially
And each scenario shall report individual pass/fail with metrics
And aggregate report shall show per-scenario status and overall summary
And exit code shall be 0 if all pass, 1 if any fail
And optional JSON report shall be generated when --report flag is used
```

**Success Criteria**:
- All 10 scenarios execute sequentially (REQ-TOOLS-033)
- Aggregate report contains per-scenario results
- Exit code reflects overall pass/fail
- JSON report file generated with --report flag

**Verification Method**: Full integration test execution, report validation

---

### Scenario 15: GUI Frame Preview

**Objective**: Verify that GUI displays real-time frame preview at 15 fps.

```gherkin
Given GUI.Application connected to SoC simulator via IDetectorClient
When continuous scan is started at Intermediate-A tier (2048x2048@15fps)
Then frame preview shall display at 15 fps using WriteableBitmap
And Window/Level adjustment shall update preview within 100 ms
And 16-bit to 8-bit conversion shall use Window/Level lookup table
And UI thread shall remain responsive during preview
```

**Success Criteria**:
- Frame preview rate matches scan rate (15 fps) (REQ-TOOLS-041)
- Window/Level response < 100 ms (REQ-TOOLS-042)
- No UI freeze during continuous preview (non-blocking rendering)

**Verification Method**: Manual UI test, frame rate measurement, responsiveness test

---

### Scenario 16: GUI Status Dashboard

**Objective**: Verify that GUI displays real-time status from SDK.

```gherkin
Given GUI.Application connected to SoC or simulator
When GetStatusAsync is polled at 1 Hz
Then status dashboard shall display:
  | Field | Source |
  | Connection State | ConnectionChanged event |
  | Scan Mode | Current ScanMode |
  | Frames Received | ScanStatus.FramesReceived |
  | Dropped Frames | ScanStatus.DroppedFrames |
  | Throughput (Gbps) | ScanStatus.CurrentThroughputGbps |
And dashboard shall update at 1 Hz minimum
```

**Success Criteria**:
- All status fields displayed and updated (REQ-TOOLS-045)
- Update frequency >= 1 Hz
- Values match SDK ScanStatus (REQ-SDK-019)

**Verification Method**: Integration test with simulator, status value comparison

---

### Scenario 17: GUI Frame Save

**Objective**: Verify that GUI saves frames via SDK SaveFrameAsync.

```gherkin
Given GUI.Application displaying a captured frame
When user clicks Save Frame and selects TIFF format with target path
Then SaveFrameAsync shall be invoked with correct Frame, path, and ImageFormat.Tiff
And save shall execute on background thread (UI remains responsive)
And success notification shall appear after save completes
And saved file shall be valid TIFF with correct pixel data
```

**Success Criteria**:
- Frame saved via IDetectorClient.SaveFrameAsync (REQ-TOOLS-044)
- UI remains responsive during save (background thread)
- Saved file round-trip matches displayed frame

**Verification Method**: Integration test, round-trip file verification

---

## Edge Case Testing

### Edge Case 1: Corrupt PDF Input

**Scenario**:
```gherkin
Given a corrupt or password-protected PDF file
When ParameterExtractor attempts to parse it
Then a clear error message shall be displayed
And the application shall not crash
And the user shall be offered manual parameter entry as fallback
```

**Expected Outcome**:
- Graceful error handling for corrupt/inaccessible PDFs
- No unhandled exceptions or application crash
- Manual entry fallback available

**Verification Method**: Unit test with corrupt PDF, password-protected PDF, empty PDF

---

### Edge Case 2: Empty detector_config.yaml

**Scenario**:
```gherkin
Given an empty or minimal detector_config.yaml
When ConfigConverter attempts validation and conversion
Then schema validation shall fail with list of all missing required fields
And no target files shall be generated
And error report shall be clear enough for user to fix the configuration
```

**Expected Outcome**:
- All missing required fields reported
- No partial output generated
- Actionable error messages

**Verification Method**: Unit test with empty config, minimal config missing various fields

---

### Edge Case 3: CodeGenerator Template Corruption

**Scenario**:
```gherkin
Given a modified or corrupt code generation template
When CodeGenerator attempts to generate code
Then a clear error shall be reported indicating which template is invalid
And no partial output files shall be written
And exit code shall be 1
```

**Expected Outcome**:
- Template validation before generation
- No partial/corrupt output files
- Clear error identification

**Verification Method**: Unit test with invalid templates

---

### Edge Case 4: IntegrationRunner Simulator Timeout

**Scenario**:
```gherkin
Given IntegrationRunner executing IT-05 (stress test scenario)
When a simulator becomes unresponsive for > 60 seconds
Then the scenario shall timeout with TIMEOUT status
And remaining scenarios shall continue if --all flag is used
And timeout details shall be included in the report
```

**Expected Outcome**:
- Per-scenario timeout enforcement (60 seconds)
- Timeout does not block remaining scenarios in --all mode
- Clear timeout reporting

**Verification Method**: Integration test with intentionally slow simulator mock

---

### Edge Case 5: GUI Connection Loss During Preview

**Scenario**:
```gherkin
Given GUI.Application displaying continuous frame preview
When the SoC/simulator connection is lost
Then frame preview shall freeze on last received frame
And status dashboard shall show Disconnected state
And auto-reconnect indicator shall appear
And preview shall resume automatically after reconnection
```

**Expected Outcome**:
- Graceful handling of connection loss in GUI
- No crash or unhandled exception
- Automatic recovery matches SDK auto-reconnect behavior (REQ-SDK-025)

**Verification Method**: Manual test with simulated network interruption

---

### Edge Case 6: Concurrent Tool Execution

**Scenario**:
```gherkin
Given ConfigConverter and CodeGenerator both reading detector_config.yaml
When both tools are executed simultaneously
Then both shall read the config file without locking conflicts
And both shall produce correct output independently
And no file corruption shall occur
```

**Expected Outcome**:
- Read-only access to detector_config.yaml (REQ-TOOLS-050)
- No file locking issues
- Independent correct output

**Verification Method**: Concurrent execution stress test

---

## Performance Criteria

### PDF Parsing Performance

**Criterion**: ParameterExtractor shall parse PDFs within 30 seconds.

**Metrics**:
| Document Type | Size | Target Time |
|--------------|------|-------------|
| Native PDF (selectable text) | < 10 MB | < 15 seconds |
| Large PDF (> 50 pages) | < 50 MB | < 30 seconds |

**Acceptance Threshold**: All parsing completes within 30 seconds

**Verification Method**: Timed test with representative PDF samples

---

### Code Generation Performance

**Criterion**: CodeGenerator shall generate all targets within 5 seconds per target.

**Metrics**:
| Target | File Count | Target Time |
|--------|-----------|-------------|
| SystemVerilog RTL | 3-5 files | < 5 seconds |
| C/C++ headers | 2-3 files | < 3 seconds |
| C# classes | 2-3 files | < 3 seconds |
| All targets combined | 7-11 files | < 10 seconds |

**Acceptance Threshold**: All targets generated within 5 seconds each

**Verification Method**: Timed generation test

---

### Config Conversion Performance

**Criterion**: ConfigConverter shall convert to all targets within 2 seconds.

**Metrics**:
| Target | Target Time |
|--------|-------------|
| .xdc (FPGA constraints) | < 1 second |
| .dts (device tree overlay) | < 1 second |
| .json (SDK configuration) | < 1 second |
| All targets + validation | < 2 seconds |

**Acceptance Threshold**: Full conversion pipeline within 2 seconds

**Verification Method**: Timed conversion test

---

### Integration Test Performance

**Criterion**: Each integration test scenario shall complete within 60 seconds.

**Metrics**:
| Scenario Category | Target Time | Timeout |
|-------------------|-------------|---------|
| Single frame (IT-01, IT-02) | < 10 seconds | 60 seconds |
| Multi-frame (IT-03, IT-04) | < 30 seconds | 60 seconds |
| Stress test (IT-05, IT-06) | < 60 seconds | 60 seconds |
| Error injection (IT-07, IT-08) | < 30 seconds | 60 seconds |
| Performance (IT-09, IT-10) | < 60 seconds | 60 seconds |
| All scenarios (--all) | < 10 minutes | 10 minutes |

**Acceptance Threshold**: All scenarios within timeout, --all within 10 minutes

**Verification Method**: Timed execution, CI pipeline measurement

---

### GUI Responsiveness

**Criterion**: GUI shall maintain responsive UI during all operations.

**Metrics**:
| Operation | Response Target |
|-----------|----------------|
| Startup | < 5 seconds to interactive |
| Frame preview | 15 fps sustained |
| Window/Level adjustment | < 100 ms |
| Status dashboard update | 1 Hz minimum |
| Frame save | UI responsive during background save |

**Acceptance Threshold**: All response targets met under normal load

**Verification Method**: Manual UI test, automated frame rate measurement

---

## Quality Gates

### TRUST 5 Framework Compliance

**Tested (T)**:
- 85%+ code coverage per tool (TDD for all new code)
- Unit tests for all tool modules (PDF parser, code generators, converters)
- Integration tests for IntegrationRunner with simulator pipeline
- GUI manual test scenarios documented and executed

**Readable (R)**:
- English XML documentation comments on all public types and methods
- Clear CLI help text for all command-line tools
- Consistent error messages with actionable guidance
- Code comments in English (per language.yaml)

**Unified (U)**:
- Consistent C# coding style enforced by EditorConfig
- Shared Common.Dto types across all tools
- Consistent CLI argument parsing (System.CommandLine)
- Consistent exit code convention (0=success, 1=failure)

**Secured (S)**:
- Input validation on all file inputs (PDF, YAML, JSON)
- No arbitrary code execution from parsed files
- No secret exposure in generated code or configuration
- Path traversal prevention on file operations

**Trackable (T)**:
- Git-tracked with conventional commits (feat, fix, test, refactor)
- SPEC-TOOLS-001 traceability tags on implementation commits
- Version history in SPEC revision table
- Tool version embedded in generated code headers

---

### Tool Quality Review

**Review Criteria**:
- All tools compile without errors or warnings
- All generated code compiles using target toolchain
- CLI tools have --help documentation
- Error messages are clear and actionable

**Reviewers**:
- Tools Lead: Tool architecture and usability
- FPGA Engineer: Generated RTL and constraints validation
- Firmware Engineer: Generated C headers and device tree validation
- SDK Engineer: Generated C# classes and JSON config validation

**Approval Criteria**:
- Zero compilation errors across all tools and generated code
- All reviewers sign off on generated output quality
- Test coverage >= 85% per tool
- CLI exit codes correct for all scenarios

---

## Traceability Matrix

| Requirement ID | Acceptance Criterion | Test Scenario | Quality Gate |
|---------------|---------------------|---------------|--------------|
| REQ-TOOLS-001 | PDF parsing accuracy | Scenario 1 | Tested |
| REQ-TOOLS-002 | Validation rules | Scenario 2 | Tested |
| REQ-TOOLS-003 | WPF GUI review | Scenarios 1, 2, 3 | Tested |
| REQ-TOOLS-004 | Schema-conformant export | Scenario 3 | Tested |
| REQ-TOOLS-010 | RTL skeleton generation | Scenario 4 | Tool Quality Review |
| REQ-TOOLS-011 | C header generation | Scenario 5 | Tool Quality Review |
| REQ-TOOLS-012 | C# class generation | Scenario 6 | Tool Quality Review |
| REQ-TOOLS-013 | Generated code compiles | Scenarios 4, 5, 6 | Tool Quality Review |
| REQ-TOOLS-020 | YAML to XDC | Scenario 7 | Tool Quality Review |
| REQ-TOOLS-021 | YAML to DTS | Scenario 8 | Tool Quality Review |
| REQ-TOOLS-022 | YAML to JSON | Scenario 9 | Tool Quality Review |
| REQ-TOOLS-023 | Schema validation | Scenarios 10, 11 | Tested |
| REQ-TOOLS-024 | Cross-validation | Scenario 12 | Tested |
| REQ-TOOLS-030 | IT scenario execution | Scenario 13 | Integration Readiness |
| REQ-TOOLS-031 | Pipeline instantiation | Scenario 13 | Integration Readiness |
| REQ-TOOLS-032 | Metrics reporting | Scenarios 13, 14 | Tested |
| REQ-TOOLS-033 | --all flag | Scenario 14 | Integration Readiness |
| REQ-TOOLS-040 | Unified WPF GUI | Scenarios 15, 16, 17 | Tested |
| REQ-TOOLS-041 | Frame preview 15fps | Scenario 15 | GUI Responsiveness |
| REQ-TOOLS-042 | Window/Level response | Scenario 15 | GUI Responsiveness |
| REQ-TOOLS-043 | IDetectorClient integration | Scenarios 15, 16, 17 | Integration Readiness |
| REQ-TOOLS-044 | Save Frame | Scenario 17 | Tested |
| REQ-TOOLS-045 | Status dashboard | Scenario 16 | Tested |
| REQ-TOOLS-050 | No config modification | Edge Case 6 | Secured |
| REQ-TOOLS-051 | No tool dependencies | Scenarios 4, 5, 6 | Tool Quality Review |
| REQ-TOOLS-060 | OCR support (optional) | Deferred | N/A |
| REQ-TOOLS-061 | Theme switching (optional) | Deferred | N/A |
| REQ-TOOLS-062 | Parallel scenarios (optional) | Deferred | N/A |

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | ABYZ-Lab Agent (spec-sdk) | Initial acceptance criteria for SPEC-TOOLS-001 |

---

**END OF ACCEPTANCE CRITERIA**
