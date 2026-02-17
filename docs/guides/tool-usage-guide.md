# Tool Usage Guide

**Document Version**: 1.0.0
**Status**: Reviewed - Approved
**Last Updated**: 2026-02-17

## Table of Contents

1. [Overview](#1-overview)
2. [ParameterExtractor](#2-parameterextractor)
3. [CodeGenerator](#3-codegenerator)
4. [ConfigConverter](#4-configconverter)
5. [IntegrationRunner](#5-integrationrunner)
6. [Common Workflows](#6-common-workflows)
7. [Troubleshooting](#7-troubleshooting)
8. [Revision History](#8-revision-history)

---

## 1. Overview

This guide describes the four development tools included in the X-ray Detector Panel System project. These tools automate the workflow from datasheet parameter extraction through code generation, configuration conversion, and integration testing.

### 1.1 Tool Summary

| Tool | Type | Purpose |
|------|------|---------|
| **ParameterExtractor** | WPF GUI + CLI | Extract timing parameters from ROIC/panel PDF datasheets |
| **CodeGenerator** | CLI | Generate FPGA RTL, firmware, and Host SDK skeletons from `detector_config.yaml` |
| **ConfigConverter** | CLI | Convert `detector_config.yaml` to per-target formats |
| **IntegrationRunner** | CLI | Execute IT-01 through IT-10 integration test scenarios |

### 1.2 Prerequisites

All tools require .NET 8.0 SDK:

```bash
dotnet --version
# Expected: 8.0.x
```

Build all tools from the repository root:

```bash
cd D:/workspace-github/system-emul-sim
dotnet build tools/
```

---

## 2. ParameterExtractor

### 2.1 Purpose

The ParameterExtractor reads PDF datasheets from detector panel and ROIC manufacturers and extracts tabular timing parameter data. The extracted parameters are reviewed, corrected if necessary, and exported to `detector_config.yaml` format. This tool eliminates manual transcription errors when configuring a new panel.

Supported parameter types:

| Category | Parameters | Unit |
|----------|-----------|------|
| Gate timing | `gate_on`, `gate_off`, `gate_period` | µs |
| ROIC settle | `roic_settle`, `bias_settle` | µs |
| ADC conversion | `adc_conv`, `sample_hold` | µs |
| Panel geometry | `rows`, `cols`, `pixel_pitch` | pixels, µm |
| Electrical | `supply_voltage`, `current_draw` | V, mA |

### 2.2 GUI Mode

Launch the WPF application (Windows only):

```bash
dotnet run --project tools/ParameterExtractor
```

#### GUI Walkthrough

**Step 1 - Open PDF**

Select File > Open PDF or press Ctrl+O. A standard Windows file open dialog appears. Navigate to the panel or ROIC datasheet PDF and click Open. The tool processes the PDF and extracts all tables automatically. Processing time depends on PDF size; a 50-page datasheet typically takes 3-5 seconds.

**Step 2 - Review Extracted Parameters**

The main window shows a DataGrid with all extracted parameters. Each row represents one parameter with columns for Name, Value, Unit, Min, Max, and Source (page number in the PDF). Values extracted from text are shown in black; values inferred by the tool are shown in blue.

```
+--------------------+----------+-------+--------+--------+----------+
| Name               | Value    | Unit  | Min    | Max    | Source   |
+--------------------+----------+-------+--------+--------+----------+
| gate_on            | 1000.0   | µs    | 500    | 5000   | Page 12  |
| gate_off           | 200.0    | µs    | 100    | 1000   | Page 12  |
| roic_settle        | 50.0     | µs    | 10     | 200    | Page 15  |
| adc_conv           | 10.0     | µs    | 5      | 50     | Page 15  |
| panel_rows         | 2048     | pixels| 256    | 4096   | Page 3   |
| panel_cols         | 2048     | pixels| 256    | 4096   | Page 3   |
| pixel_pitch_um     | 150.0    | µm    | 50     | 500    | Page 3   |
+--------------------+----------+-------+--------+--------+----------+
```

**Step 3 - Manual Correction**

Click any cell in the Value column to edit it directly. The original extracted value appears in the tooltip. Press Enter to confirm. Edited cells are highlighted in orange.

To correct a misread value from the PDF (for example if the PDF parser read "1,000" as 1 instead of 1000), click the cell, type the correct value, and press Enter.

**Step 4 - Validate**

Click the Validate button or press Ctrl+V. The tool checks all parameters against the built-in constraint rules. Validation errors appear in a panel below the DataGrid:

```
VALIDATION RESULTS:
[OK] gate_on: 1000.0 µs - within range [500, 5000]
[OK] gate_off: 200.0 µs - within range [100, 1000]
[OK] roic_settle: 50.0 µs - within range [10, 200]
[ERROR] panel_bit_depth: value '0' is not in allowed set {14, 16}
[OK] pixel_pitch_um: 150.0 µm - positive value
```

Fix any errors before exporting.

**Step 5 - Export to YAML**

Select File > Export YAML or press Ctrl+S. A save dialog appears. The default filename is `detector_config_extracted.yaml`. The exported file contains only the parameters extracted from the datasheet; merge it into the master `detector_config.yaml` using a text editor or the GUI merge function under File > Merge into Config.

### 2.3 CLI Mode

For automation and CI/CD pipelines, use CLI mode:

```bash
# Extract parameters from a PDF and save to YAML
ParameterExtractor.exe --input datasheet.pdf --output params.yaml

# Validate an existing params YAML against the config schema
ParameterExtractor.exe --validate params.yaml --schema config/config-schema.json

# Batch extract from a directory of PDFs
ParameterExtractor.exe --input-dir ./datasheets/ --output-dir ./extracted/ --batch

# Extract and merge directly into an existing config
ParameterExtractor.exe --input datasheet.pdf --merge-into config/detector_config.yaml
```

CLI options:

| Option | Description | Default |
|--------|------------|---------|
| `--input` | Path to input PDF file | Required |
| `--output` | Path for output YAML file | `params.yaml` |
| `--validate` | Path to YAML file to validate | - |
| `--schema` | Path to JSON Schema for validation | Built-in |
| `--merge-into` | Path to existing config to merge into | - |
| `--batch` | Process all PDFs in `--input-dir` | false |
| `--verbose` | Print extraction details | false |

### 2.4 Validation Rules

The validation rule engine checks extracted parameters against configurable constraints defined in `config/extraction-rules.json`:

```json
{
  "rules": [
    {
      "field": "panel.rows",
      "type": "range",
      "min": 256,
      "max": 4096,
      "message": "Panel rows must be between 256 and 4096"
    },
    {
      "field": "panel.bit_depth",
      "type": "enum",
      "values": [14, 16],
      "message": "Bit depth must be 14 or 16"
    },
    {
      "field": "fpga.timing.gate_on_us",
      "type": "positive",
      "message": "Gate ON time must be positive"
    },
    {
      "field": "fpga.spi.clock_hz",
      "type": "range",
      "min": 1000,
      "max": 50000000,
      "message": "SPI clock must be between 1 kHz and 50 MHz"
    }
  ]
}
```

---

## 3. CodeGenerator

### 3.1 Purpose

The CodeGenerator reads `detector_config.yaml` and produces skeleton source code files for all three system layers. Generated files contain correct parameter values, register definitions, struct layouts, and timing constants derived from the single configuration source. This ensures that FPGA, firmware, and Host SDK always use consistent values.

### 3.2 Usage

```bash
# Generate FPGA RTL skeletons
CodeGenerator.exe --config detector_config.yaml --target fpga --output ./generated/rtl

# Generate SoC firmware skeletons
CodeGenerator.exe --config detector_config.yaml --target firmware --output ./generated/fw

# Generate Host SDK skeletons
CodeGenerator.exe --config detector_config.yaml --target sdk --output ./generated/sdk

# Generate all targets at once
CodeGenerator.exe --config detector_config.yaml --target all --output ./generated/

# Specify a custom template directory
CodeGenerator.exe --config detector_config.yaml --target fpga \
  --output ./generated/rtl --templates ./my-templates/fpga/

# Dry-run (show what would be generated without writing files)
CodeGenerator.exe --config detector_config.yaml --target all --dry-run
```

Using `dotnet run` from source:

```bash
dotnet run --project tools/CodeGenerator -- \
    --config config/detector_config.yaml \
    --target fpga \
    --output generated/rtl/
```

### 3.3 Generated Files per Target

#### FPGA RTL (`--target fpga`)

| File | Content |
|------|---------|
| `fpga_params.svh` | SystemVerilog package with all timing and geometry parameters |
| `panel_scan_fsm.sv` | Parameterized scan FSM with correct timing values |
| `line_buffer.sv` | BRAM instantiation with correct depth and width |
| `spi_slave.sv` | SPI register map with all addresses |
| `csi2_tx_wrapper.sv` | CSI-2 configuration parameters |

Example generated `fpga_params.svh`:

```systemverilog
// AUTO-GENERATED - DO NOT EDIT
// Source: detector_config.yaml
// Generated: 2026-02-17

package fpga_params;
    // Panel geometry
    parameter int unsigned PANEL_ROWS    = 2048;
    parameter int unsigned PANEL_COLS    = 2048;
    parameter int unsigned BIT_DEPTH     = 16;

    // Timing (clock cycles at 100 MHz)
    parameter int unsigned GATE_ON_CYCLES   = 100000;  // 1000 us
    parameter int unsigned GATE_OFF_CYCLES  =  20000;  //  200 us
    parameter int unsigned ROIC_SETTLE_CYC  =   5000;  //   50 us
    parameter int unsigned ADC_CONV_CYCLES  =   1000;  //   10 us

    // Line buffer
    parameter int unsigned LINE_BUF_DEPTH   = 2048;
    parameter int unsigned LINE_BUF_WIDTH   = 16;

    // CSI-2
    parameter int unsigned CSI2_LANES       = 4;
    parameter logic [7:0]  CSI2_DATA_TYPE   = 8'h2E; // RAW16

    // Device identification
    parameter logic [31:0] DEVICE_ID        = 32'hD7E00001;
endpackage
```

#### SoC Firmware (`--target firmware`)

| File | Content |
|------|---------|
| `fpga_registers.h` | Register address definitions and bit-field macros |
| `detector_config.h` | Configuration struct with default values |
| `frame_header.h` | Network packet header struct |
| `timing_params.h` | Timing constants derived from config |

Example generated `fpga_registers.h`:

```c
// AUTO-GENERATED - DO NOT EDIT
// Source: detector_config.yaml

#ifndef FPGA_REGISTERS_H
#define FPGA_REGISTERS_H

#include <stdint.h>

// Register map (see docs/api/spi-register-map.md for full address space)
#define REG_DEVICE_ID        0x00  // RO: Device ID upper 16-bit (0xD7E0)
#define REG_DEVICE_ID_LO     0x01  // RO: Device ID lower 16-bit (0x0001)
#define REG_FW_VERSION       0x02  // RO: Firmware version [major:minor]
#define REG_BUILD_DATE       0x03  // RO: Build date BCD (MMDD)
#define REG_STATUS           0x20  // RO: FSM and hardware status
#define REG_CONTROL          0x21  // RW: Scan control and reset
#define REG_FRAME_COUNT_LO   0x30  // RO: Frame counter lower 16-bit
#define REG_FRAME_COUNT_HI   0x31  // RO: Frame counter upper 16-bit
#define REG_TIMING_GATE_ON   0x50  // RW: Gate ON time (10ns units)
#define REG_TIMING_GATE_OFF  0x51  // RW: Gate OFF time (10ns units)
#define REG_ERROR_FLAGS      0x80  // RW1C: Error flag bits (write-1-clear)

// CONTROL register bits (REG_CONTROL 0x21)
#define CTRL_START          (1u << 0)
#define CTRL_STOP           (1u << 1)
#define CTRL_SOFT_RESET     (1u << 7)

// STATUS register bits (REG_STATUS 0x20)
#define STATUS_BUSY         (1u << 0)
#define STATUS_ERROR        (1u << 1)
#define STATUS_FRAME_READY  (1u << 2)

// Expected DEVICE_ID
#define EXPECTED_DEVICE_ID  0xD7E00001u

#endif // FPGA_REGISTERS_H
```

#### Host SDK (`--target sdk`)

| File | Content |
|------|---------|
| `DetectorConfig.cs` | Configuration record with default values |
| `FrameHeader.cs` | Network packet header struct |
| `Constants.cs` | Protocol constants (ports, magic numbers) |
| `PerformanceTiers.cs` | Named tier definitions (Minimum, Intermediate, Final) |

### 3.4 Template Customization

The code generator uses Handlebars templates stored in `tools/CodeGenerator/templates/`. To customize generated output without modifying the tool source code:

1. Copy the template file you want to modify:
   ```bash
   cp tools/CodeGenerator/templates/fpga/fpga_params.svh.hbs \
      my-templates/fpga/fpga_params.svh.hbs
   ```

2. Edit the `.hbs` file. Template variables follow `{{config.section.field}}` syntax:
   ```handlebars
   // Custom header
   // Generated for project: {{config.project.name}}
   parameter int unsigned MY_ROWS = {{config.panel.rows}};
   ```

3. Run CodeGenerator with your custom template directory:
   ```bash
   CodeGenerator.exe --config detector_config.yaml --target fpga \
     --output ./generated/ --templates ./my-templates/
   ```

### 3.5 Compilation Verification

After generation, verify that all generated code compiles:

```bash
# Verify FPGA RTL compiles (requires Vivado)
cd generated/rtl
vivado -mode batch -source verify_compile.tcl

# Verify firmware headers compile
gcc -fsyntax-only -include generated/fw/fpga_registers.h /dev/null

# Verify SDK classes compile
cd generated/sdk
dotnet build
```

---

## 4. ConfigConverter

### 4.1 Purpose

The ConfigConverter transforms `detector_config.yaml` into target-specific configuration formats. It validates the input against the JSON Schema, performs system-level cross-validation checks, and produces output files consumed directly by each subsystem.

### 4.2 Usage

```bash
# Convert to Vivado TCL constraints
ConfigConverter.exe --input detector_config.yaml --format vivado-tcl

# Convert to C header (equivalent to CodeGenerator firmware target)
ConfigConverter.exe --input detector_config.yaml --format c-header

# Convert to JSON schema for the Host SDK
ConfigConverter.exe --input detector_config.yaml --format json-schema

# Convert to Linux device tree overlay
ConfigConverter.exe --input detector_config.yaml --format dts

# Validate only without generating output
ConfigConverter.exe --input detector_config.yaml --validate-only

# Convert and write to a specific output file
ConfigConverter.exe --input detector_config.yaml --format vivado-tcl \
  --output generated/timing_constraints.tcl
```

Supported formats:

| Format | Output File | Consumer |
|--------|------------|---------|
| `vivado-tcl` | `timing_constraints.tcl` | Vivado implementation |
| `c-header` | `fpga_registers.h` | SoC firmware build |
| `json-schema` | `sdk-config.json` | Host SDK runtime |
| `dts` | `detector-overlay.dts` | Linux kernel device tree |

### 4.3 Cross-Validation Checks

The converter performs system-level consistency checks beyond schema validation:

| Check | Condition | Severity |
|-------|-----------|----------|
| CSI-2 bandwidth | `raw_data_rate_gbps <= csi2_bandwidth_gbps` | Error |
| Line buffer sizing | `line_buffer_depth >= panel_cols` | Error |
| SPI clock limit | `spi_clock_hz <= 50_000_000` | Error |
| Network throughput | `raw_data_rate_gbps <= 10` | Warning |
| BRAM budget | estimated BRAMs `<= 25` (50% of 50) | Warning |

Example validation output for an invalid configuration:

```
VALIDATION ERRORS:
[ERROR] csi2_bandwidth: raw data rate 4.53 Gbps exceeds CSI-2 capacity 3.2 Gbps at 800 Mbps/lane
[ERROR] spi_clock_hz: 60000000 Hz exceeds maximum 50000000 Hz
[WARNING] bram_usage: estimated 32 BRAMs, budget is 25 (64% of limit)

Fix errors before generating target files.
```

---

## 5. IntegrationRunner

### 5.1 Purpose

The IntegrationRunner executes automated integration test scenarios that validate the complete simulator pipeline. Each scenario (IT-01 through IT-10) exercises a specific aspect of the system from panel data generation through CSI-2 transmission, SoC processing, and Host SDK reception.

### 5.2 Usage

```bash
# List all available scenarios
IntegrationRunner.exe --list-scenarios

# Run a single scenario
IntegrationRunner.exe --scenario IT-01

# Run with verbose output (shows packet-level details)
IntegrationRunner.exe --scenario IT-01 --verbose

# Run all scenarios and save JSON report
IntegrationRunner.exe --all --report ./reports/run-$(date +%Y%m%d).json

# Run all scenarios with a custom configuration
IntegrationRunner.exe --all --config config/detector_config_custom.yaml

# Run a specific scenario with custom timeout (milliseconds)
IntegrationRunner.exe --scenario IT-06 --timeout 120000
```

Using `dotnet run` from source:

```bash
dotnet run --project tools/IntegrationRunner -- --scenario IT-01 --verbose
```

### 5.3 Scenario Reference

#### IT-01: Single Frame, Minimum Tier

- **Resolution**: 1024x1024, 14-bit
- **Pattern**: Counting pixel values (0, 1, 2, ...)
- **Pipeline**: Panel Simulator -> FPGA Simulator -> MCU Simulator -> Host SDK
- **Pass Criteria**: Zero bit errors in output vs. input
- **Typical Duration**: < 500 ms

#### IT-02: Single Frame, Target Tier

- **Resolution**: 3072x3072, 16-bit
- **Pattern**: Counting pixel values
- **Pass Criteria**: Zero bit errors
- **Typical Duration**: < 2 s

#### IT-03: Continuous Capture (100 Frames)

- **Resolution**: 2048x2048, 16-bit, 15 fps
- **Validation**: All frames received with sequential frame numbers, zero gaps
- **Pass Criteria**: Zero dropped frames
- **Typical Duration**: ~7 s

#### IT-04: Data Integrity Across All Tiers

- **Tests**: Minimum, Intermediate-A, and Final tiers in sequence
- **Pass Criteria**: Zero errors across all three tiers

#### IT-05: CRC-16 Validation

- **Resolution**: 2048x2048, 16-bit
- **Validation**: CRC-16 fields in CSI-2 packets and UDP headers verified
- **Pass Criteria**: All CRCs valid, zero CRC mismatches

#### IT-06: Frame Drop Rate Under Load

- **Resolution**: 2048x2048, 16-bit
- **Frames**: 1000 frames at 15 fps
- **Pass Criteria**: Drop rate < 0.01% (maximum 0 dropped out of 1000)

#### IT-07: Out-of-Order Packet Handling

- **Resolution**: 2048x2048
- **Injection**: UDP packets delivered in shuffled order
- **Pass Criteria**: Output matches input exactly despite disorder

#### IT-08: Error Injection and Recovery

- **Injections**: Timeout, buffer overflow, CRC error, D-PHY failure
- **Validation**: Each error detected, safe state reached, scan restarted
- **Pass Criteria**: All injected errors detected and recovered within 5 s

#### IT-09: Storage Verification

- **Formats**: TIFF and RAW
- **Validation**: Write a frame, read it back, compare pixel data
- **Pass Criteria**: Zero data loss (bit-exact round trip)

#### IT-10: Performance Benchmark

- **Tiers**: All three tiers, 100 frames each
- **Metrics**: Frames per second, bytes per second, frame latency (ms)
- **Pass Criteria**: Minimum tier >= 14 fps, Intermediate >= 14 fps, Final >= 14 fps

### 5.4 Pass/Fail Criteria Summary

| Scenario | Primary Metric | Pass Threshold |
|----------|---------------|----------------|
| IT-01 | Bit errors | 0 |
| IT-02 | Bit errors | 0 |
| IT-03 | Dropped frames | 0 |
| IT-04 | Errors across tiers | 0 |
| IT-05 | CRC mismatches | 0 |
| IT-06 | Drop rate | < 0.01% |
| IT-07 | Output vs. input delta | 0 |
| IT-08 | Undetected errors | 0 |
| IT-09 | Storage round-trip delta | 0 |
| IT-10 | Frame rate per tier | >= 14 fps |

### 5.5 Report Format

JSON report generated with `--report`:

```json
{
  "timestamp": "2026-02-17T14:30:00Z",
  "config": "config/detector_config.yaml",
  "suite_version": "1.0.0",
  "results": [
    {
      "scenario": "IT-01",
      "status": "PASS",
      "duration_ms": 420,
      "metrics": {
        "bit_errors": 0,
        "frame_drops": 0,
        "throughput_mbps": 215.3,
        "latency_ms": 68.2
      }
    },
    {
      "scenario": "IT-06",
      "status": "PASS",
      "duration_ms": 66800,
      "metrics": {
        "total_frames": 1000,
        "dropped_frames": 0,
        "drop_rate_pct": 0.000
      }
    }
  ],
  "summary": {
    "total": 10,
    "passed": 10,
    "failed": 0,
    "execution_time_s": 84.5
  }
}
```

---

## 6. Common Workflows

### 6.1 New Panel Integration Workflow

When integrating a new X-ray detector panel for the first time:

```bash
# 1. Extract parameters from the panel and ROIC datasheets
ParameterExtractor.exe --input panel_datasheet.pdf --output panel_params.yaml
ParameterExtractor.exe --input roic_datasheet.pdf --output roic_params.yaml

# 2. Manually merge extracted params into the master config
#    (or use File > Merge in the GUI)
#    Edit config/detector_config.yaml as needed

# 3. Validate the complete configuration
ConfigConverter.exe --input config/detector_config.yaml --validate-only

# 4. Generate all target-specific files
CodeGenerator.exe --config config/detector_config.yaml --target all \
  --output generated/

ConfigConverter.exe --input config/detector_config.yaml --format vivado-tcl \
  --output generated/timing_constraints.tcl

# 5. Run integration tests against the simulator
IntegrationRunner.exe --all --report reports/new_panel_validation.json
```

### 6.2 Configuration Change Workflow

When modifying timing or resolution parameters:

```bash
# 1. Edit config/detector_config.yaml

# 2. Validate changes
ConfigConverter.exe --input config/detector_config.yaml --validate-only

# 3. Regenerate all derived files
CodeGenerator.exe --config config/detector_config.yaml --target all \
  --output generated/

# 4. Verify with integration tests
IntegrationRunner.exe --all
```

### 6.3 CI/CD Integration

Add to `.github/workflows/ci.yml` or your CI system:

```bash
# Validate configuration
ConfigConverter.exe --input config/detector_config.yaml --validate-only

# Run all integration tests
IntegrationRunner.exe --all \
  --report reports/ci-$(git rev-parse --short HEAD).json

# Fail the build if any test fails (exit code 1 on failure)
```

---

## 7. Troubleshooting

| Issue | Cause | Solution |
|-------|-------|---------|
| ParameterExtractor.exe fails on Linux | WPF is Windows-only | Use `--cli` mode on Linux: `dotnet run -- --input file.pdf --output params.yaml` |
| PDF parsing returns no parameters | Scanned/image-based PDF | OCR preprocessing required; enter values manually in GUI |
| CodeGenerator produces empty files | Config validation errors | Run `ConfigConverter.exe --validate-only` first |
| IntegrationRunner IT-06 fails | Small UDP socket buffer in CI | Add `--env NET_RMEM_MAX=67108864` to CI environment |
| `dotnet run` fails with SDK error | Wrong .NET SDK version | Confirm `dotnet --version` shows 8.0.x |

---

## 8. Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | ABYZ-Lab Docs Agent | Complete tool usage guide with GUI walkthrough and CLI reference |
| 1.0.1 | 2026-02-17 | manager-quality | Fix fpga_registers.h: corrected register addresses to match spi-register-map.md (DEVICE_ID=0x00, STATUS=0x20, CONTROL=0x21, FRAME_COUNT=0x30, TIMING_GATE_ON=0x50, TIMING_GATE_OFF=0x51, ERROR_FLAGS=0x80). Removed duplicate address conflicts. |
| 1.0.2 | 2026-02-17 | manager-docs (doc-approval-sprint) | Reviewed → Approved. Fix fpga_registers.h generated code: CTRL_SOFT_RESET corrected from (1u<<2) to (1u<<7); STATUS bits corrected from IDLE/SCANNING/ERROR/FRAME_DONE to BUSY/ERROR/FRAME_READY per canonical register map. |

---

## Review Record

- Date: 2026-02-17
- Reviewer: manager-quality
- Status: Approved (with corrections applied)
- TRUST 5: T:4 R:5 U:4 S:4 T:4

---

## Review Notes

**TRUST 5 Assessment**

- **Testable (4/5)**: All CLI commands and workflows are reproducible. Compilation verification steps are included. GUI walkthrough is detailed and actionable. Minor gap: no automated verification that generated code compiles cleanly after CodeGenerator execution.
- **Readable (5/5)**: Clear section structure with table of contents. Each tool has consistent subsections (Purpose, Usage, Options). Generated code examples aid understanding of tool output.
- **Unified (4/5)**: Consistent CLI option table format across tools. Tools correctly reference `detector_config.yaml` as the single source of truth. One minor inconsistency: ParameterExtractor uses `.exe` suffix while others use `dotnet run` equivalents.
- **Secured (4/5)**: No credential exposure. SPI clock maximum (50 MHz) is enforced via validation rules. No discussion of output file permissions for generated code artifacts.
- **Trackable (4/5)**: Revision history present with two prior correction entries. Tool workflows map clearly to development phases.

**Corrections Applied**

1. `fpga_registers.h` generated code example — CONTROL register bit macros corrected:
   - `CTRL_SCAN_ENABLE (1u << 0)` → `CTRL_START (1u << 0)` (name aligned with register map)
   - `CTRL_SCAN_STOP (1u << 1)` → `CTRL_STOP (1u << 1)` (name aligned with register map)
   - `CTRL_SOFT_RESET (1u << 2)` → `CTRL_SOFT_RESET (1u << 7)` (bit7 per canonical register map)
   - `CTRL_ERROR_CLEAR (1u << 8)` removed (not in canonical register map)
2. `fpga_registers.h` generated code example — STATUS register bit macros corrected:
   - `STATUS_IDLE (1u << 0)` → `STATUS_BUSY (1u << 0)` (bit0=BUSY per canonical register map)
   - `STATUS_SCANNING (1u << 1)` → `STATUS_ERROR (1u << 1)` (bit1=ERROR per canonical register map)
   - `STATUS_ERROR (1u << 2)` → `STATUS_FRAME_READY (1u << 2)` (bit2=FRAME_READY per canonical register map)
   - `STATUS_FRAME_DONE (1u << 3)` removed (not in canonical register map)

**Minor Observations (non-blocking)**

- The `detector_cli read-reg 0x02` command in Section 5.2 (SPI) checks firmware version to confirm FPGA is running; this is technically correct but the comment says "Expected: 0x0000 (IDLE)" which is misleading for a version register — acceptable as a health check shorthand.
