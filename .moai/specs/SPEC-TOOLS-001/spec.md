# SPEC-TOOLS-001: Development Tools Requirements Specification

---
id: SPEC-TOOLS-001
version: 1.0.0
status: draft
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

### 6. Unwanted Requirements

**REQ-TOOLS-050**: Tools **shall not** modify detector_config.yaml directly without user confirmation.

**WHY**: Accidental configuration changes could invalidate hardware/software integration.

**IMPACT**: All tools use read-only access to config unless user explicitly triggers export/save.

---

**REQ-TOOLS-051**: Generated code **shall not** contain tool-specific dependencies.

**WHY**: Generated code must be standalone and compilable without the generator tool.

**IMPACT**: No code generator imports, no runtime dependencies on tools/ projects.

---

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

**END OF SPEC**
