# X-ray Detector Panel System - Project Structure

**Status**: 📋 Planned Structure (Not Yet Implemented)
**Generated**: 2026-02-17
**Source**: X-ray_Detector_Optimal_Project_Plan.md Section 5.2, 5.3
**Last Updated**: 2026-02-17

⚠️ **Critical**: This documents the PLANNED structure. The 6 Gitea repositories are separate and NOT cloned into this workspace yet.

**Current Directory Status**:
- 📄 Documentation: README.md, project plans, guides
- ⚙️ Configuration: .abyz-lab/ configuration files
- ❌ Source Code: None (pre-implementation phase)

**Update Triggers**:
- When repositories are cloned: `git clone <gitea-url>/fpga.git` (repeat for 6 repos)
- When actual module structure differs from plan
- When configuration schema (detector_config.yaml) is finalized

---

## Table of Contents

1. [Multi-Repository Architecture](#multi-repository-architecture)
2. [Software Module Organization](#software-module-organization)
3. [FPGA Block Hierarchy](#fpga-block-hierarchy)
4. [Configuration Management](#configuration-management)
5. [Build System](#build-system)
6. [Test Organization](#test-organization)
7. [Future Integration Plan](#future-integration-plan)

---

## Multi-Repository Architecture

The project is organized into **6 separate Gitea repositories** to enable parallel development, clear ownership boundaries, and independent release cycles.

### Repository Overview

| Repository | Technology | Content | Responsible Role | Lines of Code (Est.) |
|-----------|-----------|---------|-----------------|---------------------|
| **fpga/** | SystemVerilog | RTL modules, testbenches, constraints | FPGA Engineer | ~5,000 RTL + ~8,000 TB |
| **fw/** | C/C++ | SoC firmware, HAL, drivers | Firmware Developer | ~10,000 C/C++ |
| **sdk/** | C++, C# | Host SDK libraries, API wrappers | Software Developer | ~8,000 C++ + ~6,000 C# |
| **tools/** | C# .NET 8.0+ | Simulators, GUI, code generators | Software Developer | ~15,000 C# |
| **config/** | YAML, JSON | detector_config.yaml, schemas, converters | System Architect | ~2,000 Python/C# |
| **docs/** | Markdown | Architecture docs, API reference, guides | Technical Writer | ~10,000 MD |

**Total Estimated LOC**: ~64,000 lines (excluding tests and generated code)

### Repository Responsibilities

#### fpga/ - FPGA RTL and Verification
**Purpose**: Hardware description and verification for Xilinx Artix-7 XC7A35T FPGA

**Structure**:
```
fpga/
├── rtl/
│   ├── top/
│   │   └── panel_acquisition_top.sv          # Top-level module
│   ├── control/
│   │   ├── spi_slave.sv                      # SPI control interface
│   │   └── panel_scan_fsm.sv                 # Panel sequencing state machine
│   ├── acquisition/
│   │   ├── roic_interface.sv                 # ROIC parallel data capture
│   │   └── line_buffer.sv                    # Dual-port BRAM line buffer
│   ├── streaming/
│   │   ├── csi2_tx_wrapper.sv                # MIPI CSI-2 TX subsystem wrapper
│   │   └── dphy_lane_controller.sv           # D-PHY lane management
│   └── protection/
│       ├── thermal_monitor.sv                # Temperature sensor interface
│       └── timing_watchdog.sv                # Timing violation detector
├── tb/
│   ├── panel_acquisition_tb.sv               # Top-level testbench
│   ├── spi_slave_tb.sv                       # SPI unit test
│   └── integration/
│       └── csi2_validation_tb.sv             # CSI-2 protocol checker
├── constraints/
│   ├── timing.xdc                            # Timing constraints
│   ├── pinout.xdc                            # FGG484 pinout mapping
│   └── physical.xdc                          # Floorplanning, placement
├── ip/
│   └── mipi_csi2_tx/                         # AMD/Xilinx IP configuration
└── scripts/
    ├── build.tcl                             # Vivado batch build script
    └── simulate.tcl                          # Simulation automation
```

**Key Files**:
- `rtl/top/panel_acquisition_top.sv`: Top-level FPGA design (~500 lines)
- `rtl/streaming/csi2_tx_wrapper.sv`: CSI-2 transmitter integration (~300 lines)
- `constraints/timing.xdc`: Clock definitions, input/output delays (~200 lines)

**Build Output**: `panel_acquisition.bit` (FPGA bitstream), `panel_acquisition.ltx` (ILA debug probes)

---

#### fw/ - SoC Firmware
**Purpose**: Embedded C/C++ firmware for NXP i.MX8M Plus (or equivalent SoC)

**Structure**:
```
fw/
├── src/
│   ├── main.c                                # Firmware entry point
│   ├── hal/
│   │   ├── csi2_receiver.c                   # CSI-2 RX driver
│   │   ├── ethernet_driver.c                 # 10 GbE MAC driver
│   │   └── spi_master.c                      # SPI master for FPGA control
│   ├── protocol/
│   │   ├── frame_handler.c                   # Frame buffer management
│   │   └── host_protocol.c                   # Host communication protocol
│   └── diagnostics/
│       ├── health_monitor.c                  # System health checks
│       └── logging.c                         # Structured logging
├── include/
│   ├── csi2_receiver.h
│   ├── ethernet_driver.h
│   └── frame_handler.h
├── tests/
│   ├── test_csi2_receiver.c                  # CSI-2 RX unit test
│   └── test_frame_handler.c                  # Frame buffer unit test
├── third_party/
│   ├── FreeRTOS/                             # Real-time OS (if used)
│   └── lwip/                                 # Lightweight TCP/IP stack
└── build/
    └── CMakeLists.txt                        # CMake build configuration
```

**Key Files**:
- `src/hal/csi2_receiver.c`: CSI-2 receiver HAL (~800 lines)
- `src/protocol/frame_handler.c`: Frame buffer and DMA management (~1,200 lines)
- `src/hal/ethernet_driver.c`: 10 GbE transmit logic (~600 lines)

**Build Output**: `firmware.elf` (ELF binary), `firmware.bin` (raw binary for flashing)

---

#### sdk/ - Host SDK
**Purpose**: Host PC libraries (C++ and C#) for system control and image acquisition

**Structure**:
```
sdk/
├── cpp/
│   ├── include/
│   │   ├── detector_control.hpp              # Detector control API
│   │   ├── image_acquisition.hpp             # Image acquisition API
│   │   └── configuration.hpp                 # Configuration management
│   ├── src/
│   │   ├── detector_control.cpp
│   │   ├── image_acquisition.cpp
│   │   └── ethernet_transport.cpp            # 10 GbE transport layer
│   └── tests/
│       ├── test_detector_control.cpp         # Unit tests
│       └── test_image_acquisition.cpp
├── csharp/
│   ├── DetectorSDK/
│   │   ├── DetectorControl.cs                # C# wrapper around C++ SDK
│   │   ├── ImageAcquisition.cs
│   │   └── Configuration.cs
│   ├── DetectorSDK.Tests/
│   │   ├── DetectorControlTests.cs           # xUnit tests
│   │   └── ImageAcquisitionTests.cs
│   └── DetectorSDK.sln                       # Visual Studio solution
└── examples/
    ├── cpp/
    │   └── simple_capture.cpp                # C++ example: capture single frame
    └── csharp/
        └── SimpleCaptureApp/                 # C# WPF example app
```

**Key Files**:
- `cpp/src/image_acquisition.cpp`: Frame capture and buffering (~1,500 lines)
- `csharp/DetectorSDK/ImageAcquisition.cs`: C# interop wrapper (~800 lines)

**Build Output**:
- `libdetector_sdk.so` (Linux shared library)
- `DetectorSDK.dll` (.NET assembly)
- `DetectorSDK.1.0.0.nupkg` (NuGet package)

---

#### tools/ - Developer Tools
**Purpose**: Simulation, GUI, code generation utilities (C# .NET 8.0+)

**Structure**:
```
tools/
├── PanelSimulator/
│   ├── PanelSimulator.cs                     # X-ray panel analog output model
│   └── NoiseGenerator.cs                     # Configurable noise injection
├── FpgaSimulator/
│   ├── FpgaSimulator.cs                      # Cycle-accurate FPGA behavioral model
│   ├── Csi2Transmitter.cs                    # CSI-2 TX emulation
│   └── SpiSlave.cs                           # SPI slave emulation
├── McuSimulator/
│   ├── McuSimulator.cs                       # SoC firmware emulation
│   ├── Csi2Receiver.cs                       # CSI-2 RX emulation
│   └── EthernetEndpoint.cs                   # 10 GbE endpoint emulation
├── HostSimulator/
│   ├── HostSimulator.cs                      # Host SDK test harness
│   └── ImageValidator.cs                     # Frame integrity validation
├── ParameterExtractor/
│   ├── MainWindow.xaml                       # WPF GUI (C#)
│   ├── PdfParser.cs                          # Extract parameters from vendor PDFs
│   └── YamlExporter.cs                       # Export to detector_config.yaml
├── CodeGenerator/
│   ├── TemplateEngine.cs                     # Mustache/Liquid template rendering
│   ├── VerilogGenerator.cs                   # Generate RTL parameter modules
│   └── CHeaderGenerator.cs                   # Generate C header files
├── ConfigConverter/
│   ├── YamlToVerilog.cs                      # detector_config.yaml → RTL params
│   ├── YamlToCHeader.cs                      # detector_config.yaml → C header
│   └── YamlToCSharp.cs                       # detector_config.yaml → C# class
├── IntegrationRunner/
│   ├── TestOrchestrator.cs                   # Coordinate multi-simulator HIL tests
│   └── ScenarioLoader.cs                     # Load test scenarios from JSON
├── GUI.Application/
│   ├── MainWindow.xaml                       # Primary GUI (C# WPF)
│   ├── ViewModels/                           # MVVM view models
│   └── Controls/                             # Custom WPF controls
└── Common.Dto/
    ├── FrameData.cs                          # Shared DTO for frame data
    ├── ConfigurationDto.cs                   # Shared DTO for configuration
    └── DiagnosticsDto.cs                     # Shared DTO for diagnostics
```

**Key Files**:
- `FpgaSimulator/FpgaSimulator.cs`: Behavioral FPGA model (~2,500 lines)
- `ParameterExtractor/PdfParser.cs`: PDF text extraction and regex parsing (~1,000 lines)
- `CodeGenerator/TemplateEngine.cs`: Template rendering engine (~800 lines)

**Build Output**:
- `PanelSimulator.exe`, `FpgaSimulator.exe`, etc. (standalone executables)
- `ParameterExtractor.exe` (GUI tool)
- `IntegrationRunner.exe` (CLI test orchestrator)

---

#### config/ - Configuration Management
**Purpose**: Single source of truth for system configuration and schema validation

**Structure**:
```
config/
├── detector_config.yaml                      # Master configuration file
├── schemas/
│   ├── detector_config.schema.json           # JSON schema for YAML validation
│   └── validation_rules.yaml                 # Custom validation rules
├── converters/
│   ├── yaml_to_verilog.py                    # Python converter (alternative)
│   ├── yaml_to_c_header.py
│   └── yaml_to_csharp.py
├── templates/
│   ├── verilog_params.v.mustache             # Verilog template
│   ├── c_header.h.mustache                   # C header template
│   └── csharp_class.cs.mustache              # C# class template
└── examples/
    ├── example_1024x1024.yaml                # Example: Minimum tier config
    ├── example_2048x2048.yaml                # Example: Target tier config
    └── example_3072x3072.yaml                # Example: Maximum tier config
```

**Key Files**:
- `detector_config.yaml`: Master configuration (~500 lines, YAML)
- `schemas/detector_config.schema.json`: JSON schema validation (~300 lines)
- `converters/yaml_to_verilog.py`: Converter to Verilog parameters (~400 lines)

**Build Output**:
- `fpga_params.vh` (Verilog header)
- `detector_config.h` (C header)
- `DetectorConfig.cs` (C# class)

---

#### docs/ - Documentation
**Purpose**: Architecture documentation, API reference, user guides

**Structure**:
```
docs/
├── architecture/
│   ├── system-overview.md                    # High-level architecture
│   ├── fpga-design.md                        # FPGA architecture deep dive
│   ├── firmware-architecture.md              # SoC firmware design
│   └── host-sdk-design.md                    # Host SDK architecture
├── api-reference/
│   ├── fpga-registers.md                     # FPGA SPI register map
│   ├── host-sdk-api.md                       # Host SDK API reference (C++)
│   └── csharp-api.md                         # C# SDK API reference
├── guides/
│   ├── getting-started.md                    # Quick start guide
│   ├── configuration-guide.md                # detector_config.yaml guide
│   ├── testing-guide.md                      # How to run tests
│   └── troubleshooting.md                    # Common issues and solutions
├── references/
│   ├── csi2-protocol.md                      # CSI-2 protocol summary
│   ├── dphy-timing.md                        # D-PHY timing diagrams
│   └── fpga-resources.md                     # Artix-7 resource utilization
└── diagrams/
    ├── system-block-diagram.svg              # SVG system diagram
    └── data-flow.svg                         # SVG data flow diagram
```

**Key Files**:
- `architecture/system-overview.md`: System architecture (~2,000 lines)
- `api-reference/host-sdk-api.md`: Host SDK API docs (~3,000 lines)
- `guides/getting-started.md`: Quick start guide (~800 lines)

---

## Software Module Organization

### Module Dependency Graph

```
┌──────────────────┐
│   Common.Dto    │  ← Hub: Shared data transfer objects
└────────┬─────────┘
         │
    ┌────┴─────────────┬──────────────────┬───────────────┬────────────────┐
    │                  │                  │               │                │
┌───▼────────┐   ┌────▼────────┐   ┌────▼─────┐   ┌────▼──────┐   ┌────▼─────────┐
│ PanelSim   │   │  FpgaSim    │   │  McuSim  │   │ HostSim   │   │ ParamExtract │
└────────────┘   └─────────────┘   └──────────┘   └───────────┘   └──────────────┘
                                                                              │
    ┌────────────────────────────────────────────────────────────────────────┘
    │
┌───▼──────────┐   ┌──────────────┐   ┌─────────────┐   ┌──────────────┐
│ CodeGen      │   │ ConfigConv   │   │ IntegRunner │   │ GUI.App      │
└──────────────┘   └──────────────┘   └─────────────┘   └──────────────┘
```

### Module Descriptions

**Common.Dto (Data Transfer Objects)**:
- Purpose: Shared interfaces and DTOs to prevent circular dependencies
- Content: FrameData, ConfigurationDto, DiagnosticsDto, TimingParameters
- Dependencies: None (hub module)
- Language: C# (.NET 8.0)
- Estimated LOC: ~500

**PanelSimulator**:
- Purpose: Models X-ray panel analog output with configurable noise, gain, offset
- Dependencies: Common.Dto
- Language: C# (.NET 8.0)
- Estimated LOC: ~1,200

**FpgaSimulator**:
- Purpose: Cycle-accurate behavioral model of FPGA logic (CSI-2 TX, SPI slave, line buffer)
- Dependencies: Common.Dto
- Language: C# (.NET 8.0)
- Estimated LOC: ~2,500

**McuSimulator (SoC Simulator)**:
- Purpose: Emulates SoC firmware (CSI-2 RX, Ethernet endpoint, frame buffer)
- Dependencies: Common.Dto
- Language: C# (.NET 8.0)
- Estimated LOC: ~2,000

**HostSimulator**:
- Purpose: Host SDK test harness for integration scenarios
- Dependencies: Common.Dto, Host SDK (C# wrapper)
- Language: C# (.NET 8.0)
- Estimated LOC: ~1,500

**ParameterExtractor**:
- Purpose: GUI tool (C# WPF) to parse detector vendor PDFs and extract parameters
- Dependencies: Common.Dto
- Language: C# WPF (.NET 8.0)
- Estimated LOC: ~2,000

**CodeGenerator**:
- Purpose: Template-based code generation for RTL blocks and boilerplate firmware
- Dependencies: Common.Dto
- Language: C# (.NET 8.0)
- Estimated LOC: ~1,500

**ConfigConverter**:
- Purpose: Converts detector_config.yaml to FPGA RTL params, SoC C headers, Host C# classes
- Dependencies: Common.Dto
- Language: C# (.NET 8.0) or Python
- Estimated LOC: ~1,200

**IntegrationRunner**:
- Purpose: Automated test orchestration for multi-layer HIL scenarios
- Dependencies: All simulators, Common.Dto
- Language: C# (.NET 8.0)
- Estimated LOC: ~1,800

**GUI.Application**:
- Purpose: Primary user interface for system control, parameter tuning, image visualization
- Dependencies: Host SDK (C# wrapper), Common.Dto
- Language: C# WPF (.NET 8.0)
- Estimated LOC: ~3,000

---

## FPGA Block Hierarchy

### RTL Module Breakdown (with LUT Estimates)

| Module | Purpose | Estimated LUTs | % of 20,800 LUTs | Criticality |
|--------|---------|---------------|-----------------|-------------|
| **panel_scan_fsm** | Panel sequencing state machine | ~800 | 3.8% | High |
| **roic_interface** | Parallel data capture from ROIC | ~600 | 2.9% | High |
| **line_buffer** | Dual-port BRAM line buffer (ping-pong) | ~400 | 1.9% | Medium |
| **spi_slave** | SPI control interface | ~300 | 1.4% | Medium |
| **csi2_tx_wrapper** | MIPI CSI-2 TX subsystem integration | ~2,500 | 12.0% | Critical |
| **dphy_lane_controller** | D-PHY lane management (OSERDES) | ~800 | 3.8% | High |
| **thermal_monitor** | Temperature sensor interface | ~200 | 1.0% | Low |
| **timing_watchdog** | Timing violation detector | ~150 | 0.7% | Low |
| **panel_acquisition_top** | Top-level integration, clock domains | ~1,000 | 4.8% | High |
| **Glue Logic & Misc** | Interconnect, debug probes, resets | ~500 | 2.4% | Low |
| **TOTAL (Application Logic)** | | **~7,250** | **~34.9%** | |
| **CSI-2 IP (AMD/Xilinx)** | MIPI CSI-2 TX IP core | ~3,000 | 14.4% | Critical |
| **GRAND TOTAL** | | **~10,250** | **~49.3%** | |

**Target Utilization**: <60% (<12,480 LUTs) → **10,250 LUTs = 49.3%** ✅ **Meets target with 10.7% margin**

### Clock Domain Structure

**Primary Clocks**:
1. **clk_panel** (e.g., 50 MHz): Panel scan timing, ROIC interface
2. **clk_csi2** (e.g., 250 MHz): CSI-2 packet generation, line buffer read
3. **clk_dphy** (e.g., 1.0-1.25 GHz): D-PHY serialization (OSERDES DDR)
4. **clk_spi** (e.g., 50 MHz max): SPI slave interface

**Clock Domain Crossings (CDCs)**:
- Panel domain → CSI-2 domain: Asynchronous FIFO (line buffer)
- SPI domain → Panel domain: Dual-clock synchronizer (control registers)

**Timing Constraints**:
- Panel clock: Relaxed timing (50 MHz = 20 ns period)
- CSI-2 clock: Moderate timing (250 MHz = 4 ns period)
- D-PHY clock: Tight timing (1.25 GHz = 0.8 ns period, OSERDES timing critical)

### BRAM Utilization

**Line Buffer**:
- Dual-port BRAM (ping-pong buffer)
- Size: 1 line × maximum width × bit depth = 3072 pixels × 16 bits = 49,152 bits = ~48 Kbit
- BRAMs used: 2 (36 Kbit each) = 2/50 = 4% ✅

**CSI-2 TX FIFO**:
- AMD/Xilinx IP internal FIFO
- Size: ~8-16 Kbit (configurable)
- BRAMs used: 1-2 (estimated)

**TOTAL BRAMs**: ~3-4 / 50 = **~6-8%** ✅ **Well within budget**

### Protection Logic

**Thermal Monitor**:
- Interface to on-board temperature sensor (I2C or SPI)
- Threshold comparator: Shutdown if T > 85°C

**Timing Watchdog**:
- Monitors panel scan FSM state transitions
- Triggers error flag if state machine stalls for >10 ms
- Resets FSM on timeout

**Emergency Shutdown Path**:
- Hardware-based shutdown (no firmware involvement)
- Disables panel power, resets all state machines

---

## Configuration Management

### detector_config.yaml - Single Source of Truth

**Purpose**: Centralized configuration file defining panel geometry, timing, interfaces, performance tiers

**Schema** (example excerpt):
```yaml
# detector_config.yaml
version: "1.0"
metadata:
  project: "X-ray Detector Panel System"
  generated_by: "ParameterExtractor v1.0"
  generated_date: "2026-02-17"

panel:
  model: "Custom ROIC 2048x2048"
  manufacturer: "Vendor XYZ"
  resolution:
    width: 2048
    height: 2048
  pixel_pitch_um: 150.0              # 150 microns
  bit_depth: 16
  frame_rate_fps: 30

timing:
  line_period_ns: 5000               # 5 microseconds per line
  frame_period_ms: 33.33             # 30 fps → 33.33 ms/frame
  readout_delay_ns: 200              # ROIC readout delay

interfaces:
  fpga_to_soc:
    type: "CSI-2 MIPI D-PHY"
    lanes: 4
    lane_speed_gbps: 1.25
  soc_to_host:
    type: "10 GbE"
    protocol: "UDP"
    port: 50000
  control:
    type: "SPI"
    clock_speed_mhz: 50
    mode: 0                          # CPOL=0, CPHA=0

performance_tier: "target"           # "minimum", "target", "maximum"
```

**Conversion Flow**:
```
detector_config.yaml
    │
    ├──> ConfigConverter (C# or Python)
    │       │
    │       ├──> fpga_params.vh (Verilog header)
    │       │      `define PANEL_WIDTH 2048
    │       │      `define PANEL_HEIGHT 2048
    │       │      `define BIT_DEPTH 16
    │       │      `define LINE_PERIOD_NS 5000
    │       │
    │       ├──> detector_config.h (C header for SoC firmware)
    │       │      #define PANEL_WIDTH 2048
    │       │      #define PANEL_HEIGHT 2048
    │       │      ...
    │       │
    │       └──> DetectorConfig.cs (C# class for Host SDK)
    │              public class DetectorConfig {
    │                  public int Width = 2048;
    │                  public int Height = 2048;
    │                  ...
    │              }
    │
    └──> JSON Schema Validation (detector_config.schema.json)
           ✅ Validated: All fields present, types correct, constraints met
```

**Validation Rules**:
- `resolution.width` and `resolution.height`: Must be power of 2 or common resolution (1024, 2048, 3072)
- `bit_depth`: Must be 12, 14, or 16
- `frame_rate_fps`: Must be 15, 30, or 60
- `interfaces.fpga_to_soc.lanes`: Must be 4 (fixed for this design)
- `interfaces.fpga_to_soc.lane_speed_gbps`: Must be ≤1.25 (Artix-7 OSERDES limit)

**Benefits**:
- No configuration drift: All targets generated from single YAML file
- Version control: YAML file tracked in Git
- Validation: JSON schema prevents invalid configurations
- Auditable: Changes visible in Git history

---

## Build System

### Per-Repository Build Tools

#### fpga/ - Vivado Batch Scripts
```tcl
# build.tcl (Vivado TCL script)
# Source: fpga/scripts/build.tcl

# Set project parameters
set project_name "panel_acquisition"
set part "xc7a35tfgg484-1"

# Create project
create_project $project_name ./$project_name -part $part

# Add RTL sources
add_files -fileset sources_1 [glob rtl/**/*.sv]
add_files -fileset constrs_1 [glob constraints/*.xdc]

# Add IP
add_files -fileset sources_1 ip/mipi_csi2_tx/mipi_csi2_tx.xci

# Set top module
set_property top panel_acquisition_top [current_fileset]

# Run synthesis
launch_runs synth_1 -jobs 4
wait_on_run synth_1

# Run implementation
launch_runs impl_1 -jobs 4
wait_on_run impl_1

# Generate bitstream
launch_runs impl_1 -to_step write_bitstream -jobs 4
wait_on_run impl_1

# Export reports
open_run impl_1
report_utilization -file reports/utilization.rpt
report_timing -file reports/timing.rpt
report_power -file reports/power.rpt

close_project
```

**Build Command**:
```bash
cd fpga/
vivado -mode batch -source scripts/build.tcl
```

**Build Output**: `fpga/panel_acquisition/panel_acquisition.runs/impl_1/panel_acquisition_top.bit`

---

#### fw/ - CMake Cross-Compilation
```cmake
# CMakeLists.txt (SoC firmware)
# Source: fw/build/CMakeLists.txt

cmake_minimum_required(VERSION 3.20)
project(detector_firmware C CXX)

# Cross-compile toolchain (example for ARM Cortex-A53)
set(CMAKE_SYSTEM_NAME Linux)
set(CMAKE_SYSTEM_PROCESSOR aarch64)
set(CMAKE_C_COMPILER aarch64-linux-gnu-gcc)
set(CMAKE_CXX_COMPILER aarch64-linux-gnu-g++)

# Include directories
include_directories(${CMAKE_SOURCE_DIR}/include)
include_directories(${CMAKE_SOURCE_DIR}/third_party/FreeRTOS/include)

# Source files
file(GLOB_RECURSE SOURCES "src/*.c" "src/*.cpp")

# Executable
add_executable(firmware ${SOURCES})

# Link libraries
target_link_libraries(firmware pthread m)

# Optimization flags
set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -O2 -Wall -Wextra")
set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -O2 -Wall -Wextra -std=c++17")
```

**Build Command**:
```bash
cd fw/build/
cmake ..
make -j4
```

**Build Output**: `fw/build/firmware.elf`, `fw/build/firmware.bin`

---

#### sdk/ - Multi-Language Build (CMake + dotnet)

**C++ SDK** (CMakeLists.txt):
```cmake
# sdk/cpp/CMakeLists.txt
cmake_minimum_required(VERSION 3.20)
project(detector_sdk CXX)

# C++17 standard
set(CMAKE_CXX_STANDARD 17)

# Include directories
include_directories(include)

# Source files
file(GLOB_RECURSE SOURCES "src/*.cpp")

# Shared library
add_library(detector_sdk SHARED ${SOURCES})

# Install targets
install(TARGETS detector_sdk DESTINATION lib)
install(DIRECTORY include/ DESTINATION include)
```

**Build Command**:
```bash
cd sdk/cpp/
cmake -B build -DCMAKE_BUILD_TYPE=Release
cmake --build build -j4
cmake --install build
```

**C# SDK** (DetectorSDK.csproj):
```xml
<!-- sdk/csharp/DetectorSDK/DetectorSDK.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
    <PackageId>DetectorSDK</PackageId>
    <Version>1.0.0</Version>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="System.Runtime.InteropServices" Version="4.3.0" />
  </ItemGroup>
</Project>
```

**Build Command**:
```bash
cd sdk/csharp/
dotnet build -c Release
dotnet pack -c Release
```

**Build Output**: `sdk/csharp/bin/Release/DetectorSDK.1.0.0.nupkg`

---

#### tools/ - .NET Solution Build
```bash
cd tools/
dotnet build -c Release
```

**Build Output**: Executables for each tool (e.g., `PanelSimulator.exe`, `ParameterExtractor.exe`)

---

## Test Organization

### Test Hierarchy

**Level 1: Unit Tests**
- FPGA: SystemVerilog testbenches per module (`tb/*.sv`)
- Firmware: C unit tests per HAL module (`fw/tests/*.c`)
- SDK: C++ unit tests (`sdk/cpp/tests/*.cpp`), C# xUnit tests (`sdk/csharp/DetectorSDK.Tests/*.cs`)
- Tools: C# xUnit tests (`tools/*.Tests/*.cs`)

**Level 2: Integration Tests**
- Multi-simulator HIL tests orchestrated by IntegrationRunner
- 10 test scenarios (IT-01 through IT-10)

**Level 3: Hardware-in-the-Loop (HIL) Tests**
- Real FPGA dev board + SoC eval board + Host PC
- Validation of Minimum, Target, Maximum performance tiers

### Integration Test Scenarios (Planned)

| Scenario | Description | Simulators Involved | Pass Criteria |
|----------|-------------|-------------------|--------------|
| **IT-01** | Single frame capture (Minimum tier) | Panel, FPGA, MCU, Host | Frame received intact, CRC valid |
| **IT-02** | Continuous capture (30 fps, Target tier) | Panel, FPGA, MCU, Host | 300 frames captured, <1% loss |
| **IT-03** | SPI configuration update | FPGA, MCU | Register write/read verified |
| **IT-04** | CSI-2 protocol validation | FPGA, MCU | Packet headers correct, payload intact |
| **IT-05** | Frame buffer overflow recovery | FPGA, MCU, Host | System recovers, no crash |
| **IT-06** | Thermal shutdown trigger | FPGA | FPGA shuts down on T>85°C |
| **IT-07** | Timing watchdog trigger | FPGA | FSM reset on stall >10 ms |
| **IT-08** | Ethernet packet loss handling | MCU, Host | Retransmission succeeds |
| **IT-09** | Maximum tier stress test | Panel, FPGA, MCU, Host | 3072×3072@30fps sustained for 60s |
| **IT-10** | End-to-end latency measurement | Panel, FPGA, MCU, Host | Latency <50 ms (panel trigger → host display) |

---

## Future Integration Plan

### When Repositories Are Available

#### Step 1: Clone Repositories

**Example** (replace `<gitea-url>` with actual Gitea server):
```bash
cd D:/workspace-github/system-emul-sim

# Clone all 6 repositories
git clone <gitea-url>/fpga.git
git clone <gitea-url>/fw.git
git clone <gitea-url>/sdk.git
git clone <gitea-url>/tools.git
git clone <gitea-url>/config.git
git clone <gitea-url>/docs.git

# Verify structure
ls -la
# Expected:
#   fpga/
#   fw/
#   sdk/
#   tools/
#   config/
#   docs/
#   README.md
#   X-ray_Detector_Optimal_Project_Plan.md
#   .abyz-lab/
```

---

#### Step 2: Verify Structure

```bash
# Regenerate documentation from actual code
/abyz-lab project --refresh

# Compare actual vs. planned structure
# Manual review: Check for deviations from this document
```

---

#### Step 3: Validate Alignment

- Compare actual repository structure with planned structure in this document
- Update this document if deviations found (with rationale in Git commit message)
- Verify `detector_config.yaml` schema matches plan

---

#### Step 4: Activate Workspace

**Option A: Git Submodules (Monorepo-like)**
```bash
cd D:/workspace-github/system-emul-sim

# Initialize git (if not already a repo)
git init

# Add repositories as submodules
git submodule add <gitea-url>/fpga.git fpga
git submodule add <gitea-url>/fw.git fw
git submodule add <gitea-url>/sdk.git sdk
git submodule add <gitea-url>/tools.git tools
git submodule add <gitea-url>/config.git config
git submodule add <gitea-url>/docs.git docs

# Commit submodule configuration
git commit -m "Add 6 repositories as submodules"
```

**Option B: Multi-Repo Workflow (Independent)**
- Keep repositories separate (already cloned above)
- Configure `.abyz-lab/config/sections/workflow.yaml` for multi-repo support
- Use `/abyz-lab project` to coordinate across repositories

---

#### Step 5: Set Up CI/CD

**Configure n8n + Gitea Webhooks**:
1. Install n8n (workflow automation platform)
2. Create webhook endpoint in n8n
3. Configure Gitea webhooks for each repository (push, pull_request events)
4. n8n workflow:
   - Trigger on push to `main` branch
   - Run build script per repository
   - Execute unit tests
   - Report status to Gitea (commit status API)

**Example n8n Workflow** (pseudo-code):
```
Trigger: Gitea Webhook (Push to fpga/main)
Step 1: SSH to build server
Step 2: cd fpga/ && vivado -mode batch -source scripts/build.tcl
Step 3: Parse utilization.rpt → Check LUT usage <60%
Step 4: POST status to Gitea API (success/failure)
```

---

### Workspace Organization Trade-offs

**Monorepo Approach (Git Submodules)**:
- **Pros**: Single workspace, unified commit history, easier cross-repo refactoring
- **Cons**: Slower git operations, submodule complexity, single failure point

**Multi-Repo Approach (Independent Repositories)**:
- **Pros**: Parallel development, independent release cycles, clear ownership
- **Cons**: Cross-repo synchronization overhead, version compatibility tracking

**Recommendation for This Project**: **Multi-Repo** (independent repositories with coordination via ABYZ-Lab workflows)
- Rationale: 6 repositories with distinct technologies (SystemVerilog, C, C#), different build systems, and independent release cycles
- ABYZ-Lab workflows (`/abyz-lab project`, `/abyz-lab run`) can coordinate across repositories without git submodule complexity

---

## Summary

This document outlines the **planned** project structure for the X-ray Detector Panel System. Key takeaways:

1. **6 Gitea Repositories**: fpga/, fw/, sdk/, tools/, config/, docs/ (not yet cloned)
2. **10 Software Modules**: Hub pattern with Common.Dto, 4 simulators, 5 tools
3. **FPGA Block Hierarchy**: ~10,250 LUTs (49.3% utilization) ✅ Meets <60% target
4. **Single Configuration Source**: `detector_config.yaml` with JSON schema validation
5. **Multi-Language Build**: Vivado (FPGA), CMake (C/C++), dotnet (C#)
6. **3-Level Test Hierarchy**: Unit → Integration → HIL

**Next Steps**:
- Wait for repository creation and procurement (M0 milestone, Week 1)
- Clone repositories when available
- Run `/abyz-lab project --refresh` to update documentation from actual code

---

**Document End**

*This is a pre-implementation planning document. Run `/abyz-lab project --refresh` after code repositories are cloned to regenerate from actual implementation.*
