# SPEC-TOOLS-001: Implementation Plan

## Overview

This implementation plan outlines the phased approach to developing the five development tools: ParameterExtractor, CodeGenerator, ConfigConverter, IntegrationRunner, and GUI.Application. All tools are implemented in C# .NET 8.0+ and follow TDD methodology for new code.

**Gate Milestone**: M5 (W23) - All development tools complete and integration-ready

---

## Implementation Phases

### Phase 1: ConfigConverter (Priority: Primary Goal)

**Objective**: Implement the configuration conversion pipeline as the foundational tool. ConfigConverter is the most critical tool because all other tools and development workflows depend on correct configuration conversion.

**Tasks**:

1. **YAML Parsing and Schema Validation** (REQ-TOOLS-023)
   - Implement detector_config.yaml parser using YamlDotNet
   - Schema validation against config/schema/detector-config-schema.json using NJsonSchema
   - Error reporting with field path and constraint details
   - CLI entry point: `dotnet run --project ConfigConverter -- --validate config.yaml`

2. **Cross-Validation Engine** (REQ-TOOLS-024)
   - Bandwidth consistency check (resolution * bitDepth * fps vs CSI-2 D-PHY limit)
   - Buffer sizing validation (BRAM capacity vs frame buffer requirements)
   - Network bandwidth check (data rate vs host link capacity)
   - Warning for near-limit configurations (>80% utilization)

3. **XDC Generation** (REQ-TOOLS-020)
   - Clock constraints from timing parameters
   - SPI timing constraints (max 50 MHz)
   - CSI-2 byte clock constraint from data rate
   - Mapping rules from docs/config/conversion-mapping.md

4. **DTS Generation** (REQ-TOOLS-021)
   - MIPI CSI-2 lane count and link frequency
   - Ethernet configuration (IP, subnet, gateway)
   - I2C device entries (Battery, IMU, GPIO)
   - Device tree syntax validation (dtc-compatible output)

5. **JSON Generation** (REQ-TOOLS-022)
   - Computed values: frameSizeBytes, packetsPerFrame, rawDataRateGbps
   - Network parameters for Host SDK
   - Storage settings (default paths, formats)
   - System.Text.Json serialization

**Deliverables**:
- ConfigConverter CLI tool with validate, convert-xdc, convert-dts, convert-json, convert-all subcommands
- Schema validation with clear error reporting
- Cross-validation engine for system-level constraints
- Unit tests: 85%+ coverage (TDD)

**Dependencies**:
- config/schema/detector-config-schema.json (must exist)
- docs/config/conversion-mapping.md (mapping rules)
- YamlDotNet 13.0+ NuGet package
- NJsonSchema 11.0+ NuGet package

---

### Phase 2: CodeGenerator (Priority: Primary Goal)

**Objective**: Implement code skeleton generation for FPGA, firmware, and SDK targets from detector_config.yaml.

**Tasks**:

1. **Template Engine Setup**
   - Select and integrate template engine (Scriban recommended)
   - Define template directory structure (templates/rtl/, templates/fw/, templates/sdk/)
   - Template validation before generation

2. **SystemVerilog RTL Generation** (REQ-TOOLS-010)
   - panel_scan_fsm.sv with parameterized resolution, bit depth, timing
   - line_buffer.sv with correct BRAM sizing calculations
   - csi2_tx_wrapper.sv with lane configuration
   - AUTO-GENERATED header comment in all files

3. **C/C++ Header Generation** (REQ-TOOLS-011)
   - fpga_registers.h with register address map
   - Bit field macros for each register
   - Documentation comments for each register and field
   - Header guards and include protection

4. **C# Class Generation** (REQ-TOOLS-012)
   - DetectorConfig.cs with default values from config
   - FrameHeader.cs with struct layout matching ethernet-protocol.md
   - Namespace and using statements for XrayDetector.Sdk

5. **Compilation Verification** (REQ-TOOLS-013)
   - Vivado synthesis test for generated RTL
   - GCC ARM compilation test for generated C headers
   - dotnet build test for generated C# classes
   - CI-compatible verification scripts

**Deliverables**:
- CodeGenerator CLI tool with generate-rtl, generate-fw, generate-sdk, generate-all subcommands
- Template library for all three targets
- Compilation verification test suite
- Unit tests: 85%+ coverage (TDD)

**Dependencies**:
- Phase 1 (ConfigConverter for config parsing - reuse YAML parser)
- Scriban NuGet package (template engine)
- docs/api/ethernet-protocol.md (FrameHeader struct layout)
- Vivado, GCC ARM, dotnet CLI (for compilation verification)

---

### Phase 3: ParameterExtractor (Priority: Secondary Goal)

**Objective**: Implement PDF parsing, parameter validation, and WPF GUI for detector datasheet parameter extraction.

**Tasks**:

1. **PDF Parsing Engine** (REQ-TOOLS-001)
   - Integrate PDF parsing library (PdfPig recommended - MIT license)
   - Table extraction from native PDF documents
   - Parameter extraction: name, value, unit, min, max
   - Parsing time target: < 30 seconds per document

2. **Validation Rule Engine** (REQ-TOOLS-002)
   - Rule definitions in YAML format (rules.yaml)
   - Built-in rules: pixel_pitch > 0, bit_depth in {14, 16}, rows > 0, cols > 0
   - Extensible rule framework for custom constraints
   - Clear error messages with field, expected, and actual values

3. **WPF GUI** (REQ-TOOLS-003)
   - DataGrid with editable cells for parameter review
   - Validation status indicators (pass/fail/warning per parameter)
   - File open dialog for PDF import
   - YAML export with schema validation (REQ-TOOLS-004)

4. **YAML Export** (REQ-TOOLS-004)
   - Export to detector_config.yaml format
   - Pre-export schema validation against JSON Schema
   - Schema errors displayed in GUI before export

**Deliverables**:
- ParameterExtractor WPF application
- PDF parsing engine with table extraction
- Validation rule engine
- YAML export with schema validation
- Unit tests: 85%+ coverage (TDD for parsing and validation)

**Dependencies**:
- Phase 1 (ConfigConverter for schema validation - reuse validator)
- PdfPig NuGet package (PDF parsing)
- config/schema/detector-config-schema.json

---

### Phase 4: GUI.Application (Priority: Secondary Goal)

**Objective**: Implement the unified WPF GUI for detector configuration management, frame preview, and monitoring.

**Tasks**:

1. **Application Shell** (REQ-TOOLS-040)
   - WPF application with MVVM architecture
   - Tabbed interface: Configuration, Preview, Monitor, Tools
   - IDetectorClient dependency injection (REQ-TOOLS-043)
   - Application startup < 5 seconds

2. **Frame Preview** (REQ-TOOLS-041, REQ-TOOLS-042)
   - WriteableBitmap rendering for 16-bit frame display
   - Window/Level adjustment with sliders (center, width)
   - 16-bit to 8-bit lookup table conversion
   - Preview update at 15 fps
   - Window/Level response < 100 ms

3. **Status Dashboard** (REQ-TOOLS-045)
   - Connection state indicator (Connected, Disconnected, Reconnecting)
   - Scan mode display
   - Frames received / dropped counters
   - Throughput (Gbps) display
   - 1 Hz minimum update rate via GetStatusAsync polling

4. **Frame Save** (REQ-TOOLS-044)
   - Save dialog with format selection (TIFF, RAW)
   - Background save via SaveFrameAsync
   - Progress indicator and success/error notification
   - UI remains responsive during save

5. **Configuration Management**
   - detector_config.yaml viewer (read-only, REQ-TOOLS-050)
   - Performance tier display
   - Quick-launch for ConfigConverter and CodeGenerator

**Deliverables**:
- GUI.Application WPF project
- MVVM ViewModels binding to IDetectorClient
- Frame preview with Window/Level adjustment
- Status dashboard with real-time metrics
- Unit tests for ViewModels: 85%+ coverage

**Dependencies**:
- SPEC-SDK-001 (IDetectorClient interface)
- Phase 1 (ConfigConverter for config display)
- WriteableBitmap (WPF built-in)

---

### Phase 5: IntegrationRunner (Priority: Final Goal)

**Objective**: Implement the automated integration test runner for IT-01 through IT-10 scenarios.

**Tasks**:

1. **Scenario Framework** (REQ-TOOLS-030, REQ-TOOLS-031)
   - Scenario definition parser (from integration-test-plan.md)
   - Simulator pipeline instantiation (Panel -> FPGA -> MCU -> Host)
   - Configuration loading from detector_config.yaml
   - Per-scenario timeout enforcement (60 seconds)

2. **Scenario Execution Engine** (REQ-TOOLS-031)
   - Pipeline connection and data flow management
   - Test step execution (inject frame, verify output, measure metrics)
   - Frame comparison (bit-for-bit, with tolerance for lossy scenarios)
   - Error injection support for fault tolerance scenarios

3. **Results Reporting** (REQ-TOOLS-032)
   - Console output with pass/fail, bit errors, frame drops, throughput, time
   - Exit code: 0 for pass, 1 for fail
   - Optional JSON report file (--report flag)
   - Per-scenario and aggregate metrics

4. **Batch Execution** (REQ-TOOLS-033)
   - --all flag for sequential execution of IT-01 through IT-10
   - Aggregate report with per-scenario status
   - Continue on failure (report all results)
   - --scenario flag for individual scenario execution

**Deliverables**:
- IntegrationRunner CLI tool
- Scenario framework with pipeline instantiation
- Results reporting (console + JSON)
- Unit tests: 85%+ coverage (TDD)

**Dependencies**:
- SPEC-SIM-001 (all four simulators must be implemented)
- docs/testing/integration-test-plan.md (scenario definitions)
- detector_config.yaml (pipeline configuration)

---

## Task Decomposition

### Priority-Based Milestones

**Primary Goal**: Configuration pipeline tools
- Phase 1: ConfigConverter (validation, conversion to XDC, DTS, JSON)
- Phase 2: CodeGenerator (RTL, C headers, C# classes)
- Success criteria: Config changes propagate correctly to all targets, generated code compiles

**Secondary Goal**: User-facing tools
- Phase 3: ParameterExtractor (PDF parsing, validation, YAML export)
- Phase 4: GUI.Application (frame preview, status monitoring, frame save)
- Success criteria: Parameters extracted from PDF and exported to config, GUI displays frames at 15fps

**Final Goal**: Integration validation
- Phase 5: IntegrationRunner (IT-01 through IT-10 execution)
- Success criteria: All 10 integration test scenarios pass

**Optional Goal**: Enhanced features
- OCR support for scanned PDFs (REQ-TOOLS-060)
- Dark/light theme for GUI (REQ-TOOLS-061)
- Parallel scenario execution (REQ-TOOLS-062)
- Success criteria: Optional features implemented if schedule permits

---

## Technology Stack Specifications

### Shared Infrastructure

| Component | Technology | Purpose |
|-----------|-----------|---------|
| Framework | .NET 8.0 LTS | All tools target |
| Language | C# 12 | Primary language |
| YAML Parser | YamlDotNet 13.0+ | detector_config.yaml parsing |
| JSON Schema | NJsonSchema 11.0+ | Schema validation |
| CLI Framework | System.CommandLine 2.0+ | CLI argument parsing |
| Test Framework | xUnit 2.7+ | Unit testing |
| Mock Framework | Moq 4.20+ | Unit test mocking |
| EditorConfig | .editorconfig | Code style enforcement |

### Tool-Specific Dependencies

| Tool | Package | Version | Purpose |
|------|---------|---------|---------|
| ParameterExtractor | PdfPig | 0.1.8+ | PDF table extraction |
| CodeGenerator | Scriban | 5.9+ | Template engine |
| GUI.Application | WPF (.NET 8.0) | Built-in | UI framework |
| IntegrationRunner | SPEC-SIM-001 simulators | N/A | Pipeline components |

### Project Structure

| Project | Type | Dependencies |
|---------|------|-------------|
| Common.Dto | Class Library | None (leaf) |
| Tools.ConfigConverter | Console App | YamlDotNet, NJsonSchema, Common.Dto |
| Tools.CodeGenerator | Console App | Scriban, YamlDotNet, Common.Dto |
| Tools.ParameterExtractor | WPF App | PdfPig, YamlDotNet, NJsonSchema |
| Tools.IntegrationRunner | Console App | Simulator assemblies, Common.Dto |
| GUI.Application | WPF App | XrayDetector.Sdk, Common.Dto |
| Tools.Tests | xUnit Test | All tool projects |

---

## Risk Analysis

### Risk 1: PDF Parsing Accuracy

**Risk Description**: PDF table extraction may fail on complex layouts, multi-column tables, or scanned datasheets.

**Probability**: Medium (35%)

**Impact**: Medium (Manual parameter entry required as fallback)

**Mitigation**:
- Use PdfPig with table extraction heuristics for native PDFs
- Support manual parameter entry as primary fallback
- Validation rules catch obviously wrong extractions
- Test with representative sample of detector datasheets

**Contingency**:
- Manual-only mode if PDF parsing accuracy < 70%
- Consider Tesseract OCR for scanned PDFs (REQ-TOOLS-060, optional)

---

### Risk 2: Generated Code Drift

**Risk Description**: Developers manually edit generated files, creating conflicts when code is regenerated.

**Probability**: Medium (30%)

**Impact**: Low (Merge conflicts, configuration mismatch)

**Mitigation**:
- AUTO-GENERATED header comment in all generated files (REQ-TOOLS-051)
- CI check: verify generated files match config (regenerate and diff)
- Document: never edit generated files, modify templates instead
- .gitattributes marking generated files

**Contingency**:
- Regeneration on every config change via CI pipeline
- Git merge strategy favoring regenerated version

---

### Risk 3: IntegrationRunner Simulator Dependencies

**Risk Description**: IntegrationRunner depends on all four simulators (SPEC-SIM-001) being complete before testing can begin.

**Probability**: Medium (40%)

**Impact**: High (IntegrationRunner development blocked until simulators ready)

**Mitigation**:
- IntegrationRunner framework developed with mock simulators first
- Simulator interfaces defined early (ISimulator from Common.Dto)
- Individual simulator integration added incrementally
- Phase 5 scheduled last to maximize simulator availability

**Contingency**:
- Test with subset of available simulators
- Mock remaining simulators for partial integration testing
- Defer full IT-01 through IT-10 to after simulator completion

---

### Risk 4: WPF Cross-Platform Limitation

**Risk Description**: WPF is Windows-only, limiting GUI.Application and ParameterExtractor to Windows hosts.

**Probability**: Low (20%)

**Impact**: Low (CLI tools are cross-platform; GUI tools are developer workstation tools)

**Mitigation**:
- CLI tools (ConfigConverter, CodeGenerator, IntegrationRunner) target netstandard2.1/net8.0
- GUI tools (ParameterExtractor, GUI.Application) Windows-only is acceptable for developer workstations
- IDetectorClient interface enables alternative GUI implementations

**Contingency**:
- Avalonia UI migration if cross-platform GUI becomes critical
- Headless CLI modes for Linux CI/CD pipelines

---

### Risk 5: Template Engine Complexity

**Risk Description**: Code generation templates may become complex and hard to maintain as the system evolves.

**Probability**: Low (25%)

**Impact**: Medium (Template maintenance burden, generation errors)

**Mitigation**:
- Use Scriban (Liquid-like syntax, good .NET integration)
- Template unit tests: verify output for known inputs
- Keep templates simple: one template per output file
- Template documentation with example input/output

**Contingency**:
- Switch to T4 templates if Scriban proves insufficient
- Split complex templates into composable partials

---

## Dependencies

### External Dependencies

**NuGet Packages**:
- YamlDotNet 13.0+ (MIT license, YAML parsing)
- NJsonSchema 11.0+ (MIT license, JSON Schema validation)
- System.CommandLine 2.0+ (MIT license, CLI framework)
- PdfPig 0.1.8+ (Apache 2.0, PDF parsing)
- Scriban 5.9+ (BSD 2-Clause, template engine)
- xUnit 2.7+ (Apache 2.0, testing)
- Moq 4.20+ (BSD 3-Clause, mocking)

**Development Tools**:
- .NET 8.0 SDK
- Visual Studio 2022 (WPF designer)
- AMD Vivado (RTL compilation verification)
- GCC ARM cross-compiler (C header compilation verification)

### Internal Dependencies

**Project Dependencies**:
- SPEC-ARCH-001: Architecture decisions (performance tiers, technology stack)
- SPEC-SDK-001: IDetectorClient interface (GUI.Application dependency)
- SPEC-SIM-001: Simulator assemblies (IntegrationRunner dependency)
- detector_config.yaml: Single source of truth for all tools
- config/schema/detector-config-schema.json: Schema for validation
- docs/config/conversion-mapping.md: ConfigConverter mapping rules
- docs/testing/integration-test-plan.md: IntegrationRunner scenario definitions
- docs/api/ethernet-protocol.md: FrameHeader struct layout for CodeGenerator

### Milestone Dependencies

**Prerequisites (before tools development)**:
- SPEC-TOOLS-001 approved (this document)
- config/schema/detector-config-schema.json created
- docs/config/conversion-mapping.md completed

**Parallel Development**:
- Simulators (SPEC-SIM-001): IntegrationRunner depends on simulator completion
- Host SDK (SPEC-SDK-001): GUI.Application depends on IDetectorClient

**Integration Gate (M5, W23)**:
- All five tools complete and tested
- ConfigConverter produces valid output for all targets
- CodeGenerator output compiles on all target toolchains
- IntegrationRunner passes IT-01 through IT-10
- GUI.Application displays frames from simulator

---

## Next Steps

### Immediate Actions (Post-Approval)

1. **Project Setup**
   - Create tools/ solution structure (.sln, .csproj files)
   - Configure EditorConfig for C# coding style
   - Add shared NuGet package references
   - Set up xUnit test projects per tool

2. **Phase 1 Kickoff (ConfigConverter)**
   - Implement YAML parser with YamlDotNet
   - Implement schema validator with NJsonSchema
   - Implement cross-validation engine
   - Write TDD tests for all conversion targets

3. **Documentation Dependencies**
   - Verify config/schema/detector-config-schema.json exists
   - Verify docs/config/conversion-mapping.md exists
   - Verify docs/testing/integration-test-plan.md exists
   - Create missing documentation if needed

### Transition to Integration (Post-Phase 5)

**Trigger**: All five tools complete, unit tests passing

**Activities**:
- Run IntegrationRunner --all with real simulators
- GUI.Application integration smoke test with simulator
- ConfigConverter round-trip verification
- CodeGenerator compilation verification on all toolchains

**Success Criteria**:
- All integration tests pass (IT-01 through IT-10)
- Generated code compiles on all targets
- GUI displays frames at 15 fps
- Code coverage >= 85% per tool
- Quality gate compliance verified

---

## Traceability

This implementation plan aligns with:

- **SPEC-TOOLS-001 spec.md**: All requirements mapped to implementation phases
- **SPEC-ARCH-001**: Architecture decisions (performance tiers, technology stack)
- **SPEC-SDK-001**: Host SDK API (IDetectorClient used by GUI.Application)
- **SPEC-SIM-001**: Simulator system (pipeline used by IntegrationRunner)
- **X-ray_Detector_Optimal_Project_Plan.md**: Phase 2 implementation (W9-W22)
- **detector_config.yaml**: Single source of truth for all tools
- **docs/config/conversion-mapping.md**: ConfigConverter mapping rules
- **docs/testing/integration-test-plan.md**: IntegrationRunner scenarios

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (spec-sdk) | Initial implementation plan for SPEC-TOOLS-001 |

---

**END OF PLAN**
