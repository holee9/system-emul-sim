# Tool Usage Guide

**Project**: X-ray Detector Panel System
**Version**: 1.0.0
**Last Updated**: 2026-02-17

---

## 1. Overview

This guide describes how to use the development tools included in the X-ray Detector Panel System project. These tools automate configuration management, code generation, parameter extraction, and integration testing.

### 1.1 Tool Summary

| Tool | Type | Purpose |
|------|------|---------|
| **ParameterExtractor** | WPF GUI | Extract parameters from PDF datasheets |
| **CodeGenerator** | CLI | Generate skeleton code for FPGA/MCU/SDK |
| **ConfigConverter** | CLI | Convert `detector_config.yaml` to target formats |
| **IntegrationRunner** | CLI | Execute integration test scenarios (IT-01~IT-10) |
| **GUI.Application** | WPF GUI | Unified management and monitoring interface |

---

## 2. ParameterExtractor

### 2.1 Purpose

The ParameterExtractor parses PDF datasheets from detector panel manufacturers and extracts tabular parameter data (name, value, unit, min, max). Extracted parameters can be reviewed, edited, and exported to `detector_config.yaml` format.

### 2.2 Launch

```bash
# Windows only (WPF application)
dotnet run --project tools/ParameterExtractor
```

### 2.3 Workflow

1. **Load PDF**: File > Open PDF, select the panel datasheet
2. **Extract Parameters**: Click "Extract" to parse tables from the PDF
3. **Review**: Parameters appear in a DataGrid with editable cells
4. **Validate**: Click "Validate" to check against known constraints
   - Pixel pitch > 0
   - Bit depth in {14, 16}
   - Resolution within supported range
5. **Export**: File > Export YAML to save as `detector_config.yaml` format

### 2.4 Features

| Feature | Description |
|---------|-------------|
| PDF Table Parsing | Extract tabular data from native and scanned PDFs |
| Rule Engine | Validate parameters against configurable rules |
| Editable DataGrid | Manually correct or override extracted values |
| Schema Validation | Verify output against `detector-config-schema.json` |
| YAML Export | Output compatible with `detector_config.yaml` |

### 2.5 Supported Parameter Types

| Category | Example Parameters |
|----------|-------------------|
| Panel | rows, cols, pixel_pitch_um, bit_depth |
| Timing | gate_on_us, gate_off_us, roic_settle_us, adc_conv_us |
| Electrical | supply_voltage, current_draw, temperature_range |
| Mechanical | panel_width_mm, panel_height_mm, active_area_mm |

### 2.6 Validation Rules

Rules are defined in JSON format and extensible:

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
      "field": "panel.pixel_pitch_um",
      "type": "positive",
      "message": "Pixel pitch must be positive"
    }
  ]
}
```

---

## 3. CodeGenerator

### 3.1 Purpose

The CodeGenerator reads `detector_config.yaml` and generates skeleton source code for all target platforms. Generated code includes correct parameters, register definitions, and struct layouts.

### 3.2 Usage

```bash
# Generate all targets
dotnet run --project tools/CodeGenerator -- \
    --config config/detector_config.yaml \
    --output generated/ \
    --target all

# Generate FPGA RTL skeletons only
dotnet run --project tools/CodeGenerator -- \
    --config config/detector_config.yaml \
    --output generated/fpga/ \
    --target fpga

# Generate SoC firmware headers only
dotnet run --project tools/CodeGenerator -- \
    --config config/detector_config.yaml \
    --output generated/fw/ \
    --target firmware

# Generate Host SDK classes only
dotnet run --project tools/CodeGenerator -- \
    --config config/detector_config.yaml \
    --output generated/sdk/ \
    --target sdk
```

### 3.3 Generated Files

#### FPGA RTL (SystemVerilog)

| File | Content |
|------|---------|
| `panel_scan_fsm.sv` | Parameterized FSM with correct timing values |
| `line_buffer.sv` | BRAM instance with correct depth/width |
| `spi_slave.sv` | Register map with all addresses defined |
| `csi2_tx_wrapper.sv` | CSI-2 configuration parameters |
| `fpga_params.svh` | SystemVerilog header with all parameters |

Example generated parameter header:

```systemverilog
// AUTO-GENERATED - DO NOT EDIT
// Generated from detector_config.yaml
// Date: 2026-02-17

package fpga_params;
    // Panel configuration
    parameter int unsigned PANEL_ROWS    = 2048;
    parameter int unsigned PANEL_COLS    = 2048;
    parameter int unsigned BIT_DEPTH     = 16;

    // Timing (in clock cycles at 100 MHz)
    parameter int unsigned GATE_ON_CYCLES  = 100000;  // 1000 us
    parameter int unsigned GATE_OFF_CYCLES = 20000;   // 200 us

    // Line buffer
    parameter int unsigned LINE_BUF_DEPTH = 2048;
    parameter int unsigned LINE_BUF_WIDTH = 16;

    // CSI-2
    parameter int unsigned CSI2_LANES     = 4;
    parameter int unsigned CSI2_DATA_TYPE = 8'h2C; // RAW16

    // Device identification
    parameter logic [15:0] DEVICE_ID = 16'hA735;
endpackage
```

#### SoC Firmware (C Headers)

| File | Content |
|------|---------|
| `fpga_registers.h` | Register addresses, bit field macros |
| `detector_config.h` | Configuration struct definitions |
| `frame_header.h` | Network packet header struct |

Example generated register header:

```c
// AUTO-GENERATED - DO NOT EDIT
// Generated from detector_config.yaml

#ifndef FPGA_REGISTERS_H
#define FPGA_REGISTERS_H

#include <stdint.h>

// Register addresses
#define REG_CONTROL         0x00
#define REG_STATUS          0x04
#define REG_FRAME_COUNTER   0x08
#define REG_LINE_COUNTER    0x0C
#define REG_ERROR_FLAGS     0x10

// Timing registers
#define REG_GATE_ON_US      0x20
#define REG_GATE_OFF_US     0x24
#define REG_ROIC_SETTLE_US  0x28
#define REG_ADC_CONV_US     0x2C

// Panel config registers
#define REG_PANEL_ROWS      0x40
#define REG_PANEL_COLS      0x44
#define REG_BIT_DEPTH       0x48

// CSI-2 config registers
#define REG_CSI2_LANES      0x80
#define REG_CSI2_DATA_TYPE  0x84
#define REG_CSI2_LANE_SPEED 0x88

// Identification registers
#define REG_DEVICE_ID       0xF0
#define REG_FW_VERSION      0xF4

// CONTROL register bit fields
#define CTRL_START_SCAN     (1 << 0)
#define CTRL_STOP_SCAN      (1 << 1)
#define CTRL_RESET          (1 << 2)
#define CTRL_ERROR_CLEAR    (1 << 3)
#define CTRL_MODE_MASK      (0x03 << 2)

// Expected values
#define EXPECTED_DEVICE_ID  0xA735

#endif // FPGA_REGISTERS_H
```

#### Host SDK (C#)

| File | Content |
|------|---------|
| `DetectorConfig.cs` | Configuration class with defaults |
| `FrameHeader.cs` | Network packet header struct |
| `Constants.cs` | Protocol constants (magic numbers, ports) |

### 3.4 Compilation Verification

After generation, verify that all generated code compiles:

```bash
# Verify FPGA RTL compiles
cd generated/fpga
vivado -mode batch -source verify_compile.tcl

# Verify firmware headers compile
cd generated/fw
gcc -fsyntax-only -include fpga_registers.h test_include.c

# Verify SDK classes compile
cd generated/sdk
dotnet build
```

---

## 4. ConfigConverter

### 4.1 Purpose

The ConfigConverter transforms `detector_config.yaml` into target-specific configuration formats. It validates the input against the JSON Schema and performs cross-validation checks.

### 4.2 Usage

```bash
# Convert to all targets
dotnet run --project tools/ConfigConverter -- \
    --input config/detector_config.yaml \
    --output generated/ \
    --target all

# Convert to FPGA constraints (.xdc)
dotnet run --project tools/ConfigConverter -- \
    --input config/detector_config.yaml \
    --output generated/timing.xdc \
    --target xdc

# Convert to SoC device tree overlay (.dts)
dotnet run --project tools/ConfigConverter -- \
    --input config/detector_config.yaml \
    --output generated/detector-overlay.dts \
    --target dts

# Convert to Host SDK config (.json)
dotnet run --project tools/ConfigConverter -- \
    --input config/detector_config.yaml \
    --output generated/sdk-config.json \
    --target json

# Validate only (no output)
dotnet run --project tools/ConfigConverter -- \
    --input config/detector_config.yaml \
    --validate-only
```

### 4.3 Output Formats

#### FPGA Constraints (.xdc)

Generated XDC includes timing constraints derived from configuration:

```tcl
# AUTO-GENERATED from detector_config.yaml
# Clock constraints
create_clock -period 10.000 -name clk_100mhz [get_ports clk_100mhz]

# SPI timing
set_input_delay -clock spi_sclk -max 5.000 [get_ports spi_mosi]
set_output_delay -clock spi_sclk -max 5.000 [get_ports spi_miso]

# CSI-2 byte clock (derived from lane speed)
# Lane speed: 800 Mbps -> byte clock: 100 MHz
create_clock -period 10.000 -name clk_csi2_byte [get_pins clk_gen/clk_csi2_byte]
```

#### SoC Device Tree Overlay (.dts)

Generated DTS matches FPGA CSI-2 TX configuration:

```dts
/* AUTO-GENERATED from detector_config.yaml */
/dts-v1/;
/plugin/;

&mipi_csi2 {
    status = "okay";
    #address-cells = <1>;
    #size-cells = <0>;

    port@0 {
        reg = <0>;
        mipi_csi2_in: endpoint {
            remote-endpoint = <&fpga_csi2_out>;
            data-lanes = <1 2 3 4>;
            clock-lanes = <0>;
            link-frequencies = /bits/ 64 <800000000>;
        };
    };
};

&eth1 {
    status = "okay";
    phy-mode = "rgmii-id";
    /* 10 GbE configuration */
};
```

#### Host SDK Config (.json)

Generated JSON includes computed values:

```json
{
  "detector": {
    "panel": {
      "rows": 2048,
      "cols": 2048,
      "bitDepth": 16,
      "pixelPitchUm": 150
    },
    "network": {
      "socAddress": "192.168.1.100",
      "dataPort": 8000,
      "controlPort": 8001,
      "payloadSize": 8192
    },
    "computed": {
      "frameSizeBytes": 8388608,
      "packetsPerFrame": 1024,
      "rawDataRateGbps": 1.01,
      "frameIntervalMs": 66.7
    },
    "storage": {
      "defaultFormat": "tiff",
      "outputPath": "./frames"
    }
  }
}
```

### 4.4 Schema Validation

The ConfigConverter validates input against the JSON Schema before conversion:

```bash
# Validate only
dotnet run --project tools/ConfigConverter -- \
    --input config/detector_config.yaml \
    --validate-only

# Example output for invalid config:
# VALIDATION ERRORS:
# - panel.rows: Required field missing
# - fpga.timing.gate_on_us: Value must be > 0
# - fpga.spi.clock_hz: Value exceeds maximum (50000000)
```

### 4.5 Cross-Validation Checks

Beyond schema validation, the converter performs system-level consistency checks:

| Check | Condition | Severity |
|-------|-----------|----------|
| Bandwidth | raw_data_rate <= CSI-2 bandwidth | Error |
| Buffer sizing | line_buffer_depth >= panel_cols | Error |
| Network throughput | raw_data_rate <= Ethernet bandwidth | Warning |
| BRAM usage | line_buffer BRAMs <= 50% budget | Warning |
| SPI clock | clock_hz <= 50 MHz | Error |

---

## 5. IntegrationRunner

### 5.1 Purpose

The IntegrationRunner executes automated integration test scenarios that validate the complete simulator pipeline (Panel -> FPGA -> MCU -> Host).

### 5.2 Usage

```bash
# Run a single scenario
dotnet run --project tools/IntegrationRunner -- --scenario IT-01

# Run all scenarios
dotnet run --project tools/IntegrationRunner -- --all

# Verbose output
dotnet run --project tools/IntegrationRunner -- --scenario IT-01 --verbose

# Generate JSON report
dotnet run --project tools/IntegrationRunner -- --all --report output/report.json

# Use custom configuration
dotnet run --project tools/IntegrationRunner -- \
    --scenario IT-01 \
    --config config/detector_config_custom.yaml
```

### 5.3 Scenario Details

#### IT-01: Single Frame, Minimum Tier

- **Config**: 1024x1024, 14-bit, counter pattern
- **Pipeline**: Panel -> FPGA -> MCU -> Host
- **Validation**: Bit-exact input/output match
- **Pass Criteria**: Zero bit errors

#### IT-02: Single Frame, Target Tier

- **Config**: 3072x3072, 16-bit, counter pattern
- **Validation**: Bit-exact match at maximum resolution
- **Pass Criteria**: Zero bit errors

#### IT-03: Continuous Capture

- **Config**: 2048x2048, 16-bit, 100 frames
- **Validation**: All frames received, sequential frame numbers
- **Pass Criteria**: Zero dropped frames

#### IT-04: Data Integrity (All Tiers)

- **Config**: All tier configurations, counter pattern
- **Validation**: Bit-exact match for each tier
- **Pass Criteria**: Zero errors across all tiers

#### IT-05: CRC-16 Validation

- **Config**: 2048x2048, 16-bit
- **Validation**: CRC-16 in CSI-2 packets and UDP headers verified
- **Pass Criteria**: All CRCs valid

#### IT-06: Frame Drop Simulation

- **Config**: 2048x2048, 16-bit, 1000 frames
- **Validation**: Drop rate measurement under load
- **Pass Criteria**: Drop rate < 0.01%

#### IT-07: Out-of-Order Packet Handling

- **Config**: 2048x2048, shuffled packet delivery
- **Validation**: Correct reassembly despite disorder
- **Pass Criteria**: Bit-exact output

#### IT-08: Error Injection and Recovery

- **Config**: Error injection (timeout, overflow, CRC)
- **Validation**: Error detection, safe state, recovery
- **Pass Criteria**: All errors detected and recovered

#### IT-09: Storage Verification

- **Config**: TIFF and RAW storage
- **Validation**: Round-trip fidelity (write then read)
- **Pass Criteria**: Zero data loss in storage

#### IT-10: Performance Benchmark

- **Config**: All tiers, 100 frames each
- **Validation**: Throughput measurement
- **Metrics**: Frames/second, bytes/second, latency

### 5.4 Report Format

```json
{
  "timestamp": "2026-02-17T14:30:00Z",
  "config": "detector_config.yaml",
  "results": [
    {
      "scenario": "IT-01",
      "status": "PASS",
      "duration_ms": 45,
      "metrics": {
        "bit_errors": 0,
        "frame_drops": 0,
        "throughput_mbps": 215.3
      }
    }
  ],
  "summary": {
    "total": 10,
    "passed": 10,
    "failed": 0,
    "execution_time_s": 12.3
  }
}
```

---

## 6. GUI.Application

### 6.1 Purpose

The GUI.Application provides a unified WPF interface for detector configuration management, real-time simulator monitoring, and frame preview.

### 6.2 Launch

```bash
# Windows only (WPF application)
dotnet run --project tools/GUI.Application
```

### 6.3 Features

| Tab | Function |
|-----|----------|
| **Configuration** | View/edit `detector_config.yaml` with visual controls |
| **Simulator** | Start/stop simulators, view pipeline status |
| **Frame Viewer** | Real-time 16-bit image display with Window/Level |
| **Integration** | Run IT scenarios, view pass/fail results |
| **Logs** | Structured log viewer with filtering |

### 6.4 Frame Viewer

The frame viewer supports:

- **16-bit to 8-bit mapping**: Window/Level adjustment (center/width sliders)
- **Real-time display**: Up to 15 fps preview during continuous scan
- **Zoom/Pan**: Mouse wheel zoom, drag to pan
- **Pixel Inspector**: Hover to see pixel value at cursor position
- **Histogram**: Pixel value distribution display

### 6.5 Simulator Control Panel

```
+------------------------------------------+
| Simulator Pipeline                        |
|                                           |
| [Panel] ---> [FPGA] ---> [MCU] ---> [Host]|
|  Ready       Idle        Idle       Idle  |
|                                           |
| Config: 2048x2048, 16-bit, 15 fps        |
| Pattern: Counter                          |
|                                           |
| [Start Scan]  [Stop]  [Single Frame]     |
|                                           |
| Frames: 0  |  Errors: 0  |  Drops: 0     |
+------------------------------------------+
```

---

## 7. Common Tool Workflows

### 7.1 New Panel Integration Workflow

When integrating a new X-ray detector panel:

```bash
# 1. Extract parameters from datasheet
dotnet run --project tools/ParameterExtractor
# -> Load PDF, extract, validate, export YAML

# 2. Update detector_config.yaml with extracted parameters
# (Manual merge or direct export)

# 3. Validate configuration
dotnet run --project tools/ConfigConverter -- \
    --input config/detector_config.yaml --validate-only

# 4. Generate target-specific files
dotnet run --project tools/CodeGenerator -- \
    --config config/detector_config.yaml --output generated/ --target all

dotnet run --project tools/ConfigConverter -- \
    --input config/detector_config.yaml --output generated/ --target all

# 5. Run integration tests
dotnet run --project tools/IntegrationRunner -- --all
```

### 7.2 Configuration Change Workflow

When changing system parameters:

```bash
# 1. Edit detector_config.yaml
# 2. Validate
dotnet run --project tools/ConfigConverter -- --input config/detector_config.yaml --validate-only

# 3. Regenerate all target files
dotnet run --project tools/CodeGenerator -- --config config/detector_config.yaml --output generated/ --target all
dotnet run --project tools/ConfigConverter -- --input config/detector_config.yaml --output generated/ --target all

# 4. Run integration tests to verify
dotnet run --project tools/IntegrationRunner -- --all
```

---

## 8. Troubleshooting

| Issue | Cause | Solution |
|-------|-------|---------|
| ParameterExtractor won't launch on Linux | WPF is Windows-only | Use Windows or extract parameters manually |
| PDF parsing fails | Complex or scanned PDF | Try different PDF, or enter values manually |
| Generated code doesn't compile | Config values out of range | Validate config first with ConfigConverter |
| IntegrationRunner timeout | Slow machine or large tier | Increase timeout: `--timeout 60000` |
| GUI frame display is blank | No frames generated | Start scan or load test frame |

---

## 9. Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (architect) | Initial tool usage guide |

---
