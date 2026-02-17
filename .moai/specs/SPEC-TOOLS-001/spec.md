# SPEC-TOOLS-001: Development Tools Requirements Specification

---
id: SPEC-TOOLS-001
version: 1.0.0
status: approved
created: 2026-02-17
updated: 2026-02-17
author: MoAI Agent (analyst)
priority: medium
milestone: M5
gate_week: W23
---

## HISTORY

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (analyst) | Initial SPEC creation for development tools |

---

## Overview

### Scope

This SPEC covers four development tools and the GUI application:

| Tool | Purpose | Technology |
|------|---------|-----------|
| **ParameterExtractor** | Extract detector parameters from PDF datasheets | C# WPF |
| **CodeGenerator** | Generate skeleton code for FPGA/MCU/Host SDK | C# CLI |
| **ConfigConverter** | Convert detector_config.yaml to target-specific formats | C# CLI |
| **IntegrationRunner** | Execute IT-01~IT-10 integration test scenarios | C# CLI |
| **GUI.Application** | Unified WPF GUI for parameter management and monitoring | C# WPF |

### Development Methodology

All tools are **new code** and follow **TDD (RED-GREEN-REFACTOR)** per quality.yaml.

---

## Requirements

### 1. ParameterExtractor Requirements

**REQ-TOOLS-001**: The ParameterExtractor **shall** parse PDF datasheet files and extract tabular parameter data (name, value, unit, min, max).

**WHY**: Detector panel datasheets contain timing, electrical, and dimensional parameters needed for detector_config.yaml generation.

**IMPACT**: PDF parsing library required. Supports table extraction from scanned and native PDF formats.

---

**REQ-TOOLS-002**: The ParameterExtractor **shall** provide a rule engine for validating extracted parameters against known constraints.

**WHY**: Extracted values must be validated against physical limits (e.g., pixel pitch > 0, bit depth in {14, 16}).

**IMPACT**: Rules defined in JSON/YAML format. Extensible for new parameter types.

---

**REQ-TOOLS-003**: The ParameterExtractor **shall** provide a WPF GUI for parameter review, editing, and export.

**WHY**: Human review of extracted parameters is essential for accuracy before committing to detector_config.yaml.

**IMPACT**: DataGrid with editable cells. Export to YAML format matching detector_config.yaml schema.

---

**REQ-TOOLS-004**: **WHEN** parameters are exported **THEN** the output **shall** conform to the detector_config.yaml JSON Schema (`config/schema/detector-config-schema.json`).

**WHY**: Schema conformance ensures compatibility with all downstream tools and simulators.

**IMPACT**: Schema validation performed before export. Errors reported with field-level detail.

---

### 2. CodeGenerator Requirements

**REQ-TOOLS-010**: The CodeGenerator **shall** generate SystemVerilog RTL skeleton files from detector_config.yaml.

**WHY**: RTL skeletons with correct parameters (resolution, bit depth, timing) reduce boilerplate and prevent configuration errors.

**IMPACT**: Generated modules include parameterized `panel_scan_fsm.sv`, `line_buffer.sv` stubs with correct BRAM sizing.

---

**REQ-TOOLS-011**: The CodeGenerator **shall** generate C/C++ header files with FPGA register map definitions from detector_config.yaml.

**WHY**: SoC firmware needs register address definitions matching FPGA implementation.

**IMPACT**: Generated `fpga_registers.h` includes all register addresses, bit field macros, and documentation comments.

---

**REQ-TOOLS-012**: The CodeGenerator **shall** generate C# SDK class skeletons from detector_config.yaml.

**WHY**: Host SDK classes need correct frame dimensions, packet sizes, and configuration defaults.

**IMPACT**: Generated `DetectorConfig.cs` with default values, `FrameHeader.cs` with struct layout matching protocol.

---

**REQ-TOOLS-013**: **WHEN** generated code is compiled **THEN** it **shall** compile without errors using the respective toolchain.

**WHY**: Generated code that does not compile is useless and wastes developer time.

**IMPACT**: Compilation verification test: Vivado synth for RTL, GCC for C headers, dotnet build for C# classes.

---

### 3. ConfigConverter Requirements

**REQ-TOOLS-020**: The ConfigConverter **shall** convert detector_config.yaml to FPGA constraints (.xdc) following the mapping rules in `docs/config/conversion-mapping.md`.

**WHY**: FPGA constraints must match detector configuration for correct timing and pin assignment.

**IMPACT**: Generated .xdc file includes clock constraints, SPI timing, and CSI-2 byte clock.

---

**REQ-TOOLS-021**: The ConfigConverter **shall** convert detector_config.yaml to SoC device tree overlay (.dts) following the mapping rules.

**WHY**: SoC device tree must match FPGA CSI-2 TX configuration for correct receiver setup.

**IMPACT**: Generated .dts includes MIPI CSI-2 lane count, link frequency, and Ethernet configuration.

---

**REQ-TOOLS-022**: The ConfigConverter **shall** convert detector_config.yaml to Host SDK configuration (.json) following the mapping rules.

**WHY**: Host SDK needs matching image dimensions, network parameters, and storage settings.

**IMPACT**: Generated .json includes computed values (frameSizeBytes, packetsPerFrame, rawDataRateGbps).

---

**REQ-TOOLS-023**: The ConfigConverter **shall** validate the input YAML against `config/schema/detector-config-schema.json` before conversion.

**WHY**: Invalid configuration must be caught before generating target files that would cause runtime errors.

**IMPACT**: Schema validation errors reported with field path and expected constraints.

---

**REQ-TOOLS-024**: The ConfigConverter **shall** perform cross-validation checks (bandwidth consistency, buffer sizing) as defined in the conversion mapping.

**WHY**: Individual fields may be valid but their combination may violate system constraints.

**IMPACT**: Warnings for bandwidth exceeded, errors for impossible configurations.

---

### 4. IntegrationRunner Requirements

**REQ-TOOLS-030**: The IntegrationRunner **shall** execute integration test scenarios IT-01 through IT-10 as defined in `docs/testing/integration-test-plan.md`.

**WHY**: Automated integration testing validates the simulator pipeline before hardware availability.

**IMPACT**: CLI interface: `dotnet run --project IntegrationRunner -- --scenario IT-01`

---

**REQ-TOOLS-031**: **WHEN** a scenario is executed **THEN** the IntegrationRunner **shall** instantiate all required simulators, connect them in pipeline order, and execute the test steps.

**WHY**: Each scenario requires a specific simulator configuration and data flow.

**IMPACT**: Simulator pipeline: Panel -> FPGA -> MCU -> Host. Configuration loaded from detector_config.yaml.

---

**REQ-TOOLS-032**: The IntegrationRunner **shall** report pass/fail results with metrics: bit errors, frame drops, throughput, execution time.

**WHY**: Quantitative results enable quality gate decisions at M3 milestone.

**IMPACT**: Console output and optional JSON report file. Exit code 0 for pass, 1 for fail.

---

**REQ-TOOLS-033**: The IntegrationRunner **shall** support `--all` flag to execute all scenarios sequentially and report aggregate results.

**WHY**: CI pipeline needs single-command execution for all integration tests.

**IMPACT**: Aggregate report includes per-scenario pass/fail and overall summary.

---

### 5. GUI.Application Requirements

**REQ-TOOLS-040**: The GUI.Application **shall** provide a unified WPF interface for detector configuration management and monitoring.

**WHY**: Unified GUI reduces tool switching and provides visual feedback during development.

**IMPACT**: WPF application with tabbed interface for configuration, monitoring, and parameter extraction.

---

**REQ-TOOLS-041**: The GUI.Application **shall** display real-time simulator status and frame preview at up to 15 fps.

**WHY**: Visual feedback during simulation validates image quality and system behavior.

**IMPACT**: WriteableBitmap with Window/Level mapping. 16-bit to 8-bit conversion for display.

---

**REQ-TOOLS-042**: **WHEN** the user adjusts Window/Level sliders **THEN** the GUI.Application **shall** update the frame preview within 100 ms.

**WHY**: Interactive Window/Level adjustment is essential for evaluating X-ray image quality during development and calibration.

**IMPACT**: Window center and width parameters applied to 16-bit pixel data via lookup table. WPF WriteableBitmap updated on UI thread.

---

**REQ-TOOLS-043**: The GUI.Application **shall** integrate with IDetectorClient (SPEC-SDK-001) for connection management, frame acquisition, and status monitoring.

**WHY**: The GUI is the primary user interface for the Host SDK and must expose all SDK capabilities.

**IMPACT**: MVVM architecture with IDetectorClient injected via dependency injection. ViewModel binds to SDK events (FrameReceived, ErrorOccurred, ConnectionChanged).

---

**REQ-TOOLS-044**: **WHEN** the user clicks Save Frame **THEN** the GUI.Application **shall** invoke SaveFrameAsync with the selected format (TIFF or RAW) and target path.

**WHY**: Frame storage from the GUI enables quick capture and archival during testing.

**IMPACT**: File save dialog with format selection. SaveFrameAsync called on background thread to avoid UI freeze.

---

**REQ-TOOLS-045**: **WHEN** the GUI.Application is connected to a SoC or simulator **THEN** it **shall** display a status dashboard showing: connection state, scan mode, frames received, dropped frames, and throughput (Gbps).

**WHY**: Real-time status monitoring enables rapid diagnosis of performance and connectivity issues.

**IMPACT**: Status bar and dashboard panel updated at 1 Hz minimum via GetStatusAsync polling.

---

### 6. Unwanted Requirements

**REQ-TOOLS-050**: Tools **shall not** modify detector_config.yaml directly without user confirmation.

**WHY**: Accidental configuration changes could invalidate hardware/software integration.

**IMPACT**: All tools use read-only access to config unless user explicitly triggers export/save.

---

**REQ-TOOLS-051**: Generated code **shall not** contain tool-specific dependencies.

**WHY**: Generated code must be standalone and compilable without the generator tool.

**IMPACT**: No code generator imports, no runtime dependencies on tools/ projects.

---

### 7. Optional Requirements

**REQ-TOOLS-060**: **Where possible**, the ParameterExtractor should support OCR for scanned PDF datasheets.

**WHY**: Some legacy detector datasheets are scanned images rather than native PDF with selectable text.

**IMPACT**: OCR library (e.g., Tesseract) integration. Priority: low. Manual entry remains as fallback.

---

**REQ-TOOLS-061**: **Where possible**, the GUI.Application should support dark/light theme switching.

**WHY**: Medical imaging workstations typically use dark themes to reduce eye strain during extended use.

**IMPACT**: WPF resource dictionaries for theme support. Priority: low.

---

**REQ-TOOLS-062**: **Where possible**, the IntegrationRunner should support parallel scenario execution.

**WHY**: Parallel execution reduces total integration test time for CI pipelines.

**IMPACT**: Requires isolated simulator instances per scenario. Priority: low.

---

## Technical Constraints

### Platform Constraints

| Constraint | Value | Rationale |
|-----------|-------|-----------|
| Target Framework | .NET 8.0 LTS | Consistent with Host SDK (SPEC-SDK-001) |
| Language | C# 12 | Modern language features, consistent toolchain |
| GUI Framework | WPF (Windows only) | Rich databinding, WriteableBitmap for 16-bit display |
| CLI Runtime | Cross-platform (.NET 8.0) | CLI tools run on Windows and Linux |
| OS Support (GUI) | Windows 10+ | WPF requirement |
| OS Support (CLI) | Windows 10+, Linux (Ubuntu 22.04+) | CLI tools are cross-platform |

### Tool-Specific Constraints

| Tool | Constraint | Value |
|------|-----------|-------|
| ParameterExtractor | PDF library | iTextSharp or PdfPig (MIT/AGPL license check) |
| CodeGenerator | Template engine | Scriban or T4 templates |
| ConfigConverter | YAML parser | YamlDotNet 13.0+ |
| ConfigConverter | JSON Schema validator | NJsonSchema 11.0+ |
| IntegrationRunner | Test timeout | 60 seconds per scenario |
| GUI.Application | Frame preview | WriteableBitmap, 15 fps target |
| GUI.Application | Window/Level update | < 100 ms response time |

### Performance Constraints

| Metric | Target | Tool |
|--------|--------|------|
| PDF parsing time | < 30 seconds per document | ParameterExtractor |
| Code generation time | < 5 seconds per target | CodeGenerator |
| Config conversion time | < 2 seconds for all targets | ConfigConverter |
| Integration test timeout | < 60 seconds per scenario | IntegrationRunner |
| Frame preview rate | 15 fps | GUI.Application |
| Window/Level response | < 100 ms | GUI.Application |
| GUI startup time | < 5 seconds | GUI.Application |

---

## Quality Gates

### QG-001: TRUST 5 Framework Compliance

- **Tested**: 85%+ code coverage per tool (TDD for all new code)
- **Readable**: English code comments, XML documentation on public APIs
- **Unified**: Consistent C# coding style (EditorConfig), shared Common.Dto types
- **Secured**: No secret exposure, input validation on all file inputs (PDF, YAML, JSON)
- **Trackable**: Git-tracked with conventional commits, SPEC-TOOLS-001 traceability tags

### QG-002: Tool Quality Review

- All tools compile without errors or warnings
- All generated code compiles without errors using target toolchain
- CLI tools return correct exit codes (0 for success, 1 for failure)
- GUI application passes manual usability review

### QG-003: Integration Readiness

- ConfigConverter output matches expected format for FPGA, SoC, and Host SDK
- IntegrationRunner successfully executes with all four simulators (SPEC-SIM-001)
- GUI.Application connects to Host SDK and displays frames

---

## Traceability

### Parent Documents

- **SPEC-ARCH-001**: P0 Architecture Decisions (technology stack, performance tiers)
- **SPEC-SDK-001**: Host SDK API (IDetectorClient interface used by GUI.Application)
- **SPEC-SIM-001**: Simulator system (pipeline used by IntegrationRunner)
- **X-ray_Detector_Optimal_Project_Plan.md**: Section 5.3 Phase 4 (Tools Development)

### Configuration References

- **detector_config.yaml**: Single source of truth consumed by all tools
- **config/schema/detector-config-schema.json**: JSON Schema for validation
- **docs/config/conversion-mapping.md**: ConfigConverter mapping rules

### Child Documents

- Integration test scenarios (IT-01 through IT-10) executed by IntegrationRunner
- Generated code artifacts consumed by FPGA, firmware, and SDK projects

---

## Acceptance Criteria

### AC-TOOLS-001: ParameterExtractor PDF Parsing

**GIVEN**: Sample PDF datasheet with tabular parameter data
**WHEN**: PDF is loaded and parsed
**THEN**: Extracted parameters include name, value, unit with >= 90% accuracy
**AND**: User can review and edit extracted values in GUI

---

### AC-TOOLS-002: CodeGenerator RTL Compilation

**GIVEN**: detector_config.yaml with target tier configuration (2048x2048, 16-bit)
**WHEN**: RTL skeleton is generated and compiled with Vivado
**THEN**: Synthesis completes with zero errors
**AND**: Generated parameters match config values

---

### AC-TOOLS-003: ConfigConverter Round-Trip

**GIVEN**: detector_config.yaml with all fields populated
**WHEN**: Converted to .xdc, .dts, and .json, then values compared back
**THEN**: All mapped values match original YAML values exactly
**AND**: Cross-validation checks pass (bandwidth, buffer sizing)

---

### AC-TOOLS-004: ConfigConverter Schema Validation

**GIVEN**: Invalid detector_config.yaml (missing required field `panel.rows`)
**WHEN**: Schema validation is performed
**THEN**: Validation fails with clear error: "Required field 'panel.rows' is missing"
**AND**: No target files are generated

---

### AC-TOOLS-005: IntegrationRunner IT-01

**GIVEN**: IntegrationRunner configured for IT-01 (single frame, minimum tier)
**WHEN**: `--scenario IT-01` is executed
**THEN**: All 4 simulators instantiated and connected
**AND**: Output frame matches input (zero bit errors)
**AND**: Pass/fail result reported with metrics

---

### AC-TOOLS-006: IntegrationRunner --all

**GIVEN**: All simulators and IntegrationRunner ready
**WHEN**: `--all` flag is used
**THEN**: IT-01 through IT-10 execute sequentially
**AND**: Aggregate report shows per-scenario status
**AND**: Exit code reflects overall pass/fail

---

### AC-TOOLS-007: GUI Frame Preview

**GIVEN**: GUI.Application connected to SoC simulator via IDetectorClient
**WHEN**: Continuous scan is started at Intermediate-A tier (2048x2048@15fps)
**THEN**: Frame preview displays at 15 fps
**AND**: Window/Level adjustment updates preview within 100 ms
**AND**: Status dashboard shows connection state, frames received, throughput

---

### AC-TOOLS-008: GUI Frame Save

**GIVEN**: GUI.Application displaying a captured frame
**WHEN**: User clicks Save Frame and selects TIFF format
**THEN**: Frame is saved to selected path via SaveFrameAsync
**AND**: Saved file round-trip matches displayed frame data
**AND**: UI remains responsive during save operation

---

### AC-TOOLS-009: CodeGenerator C Header Output

**GIVEN**: detector_config.yaml with Intermediate-A tier configuration
**WHEN**: C header generation is executed
**THEN**: Generated fpga_registers.h compiles with GCC without errors
**AND**: Register addresses and bit field macros match config values
**AND**: Header contains AUTO-GENERATED comment

---

### AC-TOOLS-010: ConfigConverter Cross-Validation

**GIVEN**: detector_config.yaml with bandwidth exceeding CSI-2 limit (e.g., 5.0 Gbps)
**WHEN**: Cross-validation is performed
**THEN**: ConfigConverter reports bandwidth constraint violation
**AND**: No target files are generated
**AND**: Error message includes actual vs. maximum bandwidth values

---

## Dependencies

- All four simulators (SPEC-SIM-001) must be implemented before IntegrationRunner can execute
- `config/schema/detector-config-schema.json` must exist for ConfigConverter and ParameterExtractor
- `docs/config/conversion-mapping.md` defines ConfigConverter mapping rules
- `docs/testing/integration-test-plan.md` defines IntegrationRunner scenarios

---

## Risks

### R-TOOLS-001: PDF Parsing Accuracy

**Risk**: PDF table extraction fails on complex or scanned datasheet layouts.
**Probability**: Medium. **Impact**: Medium.
**Mitigation**: Support manual parameter entry as fallback. Consider OCR library for scanned PDFs.

### R-TOOLS-002: Generated Code Maintenance

**Risk**: Generated code drifts from manual edits, creating merge conflicts.
**Probability**: Medium. **Impact**: Low.
**Mitigation**: Generated files marked with `// AUTO-GENERATED - DO NOT EDIT` header. Regeneration on config change.

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (analyst) | Initial SPEC creation for development tools |

---

## Review Record

- Date: 2026-02-17
- Reviewer: manager-quality
- Status: Approved
- TRUST 5: T:4 R:5 U:5 S:4 T:4

---

**END OF SPEC**
