# X-ray Detector Panel System - Technology Stack

**Status**: 📋 Technology Plan (Pre-implementation)
**Generated**: 2026-02-17
**Source**: X-ray_Detector_Optimal_Project_Plan.md Section 5.3, 9.3
**Last Updated**: 2026-02-17

⚠️ **Note**: Technology choices are from the approved project plan. Some items require confirmation at M0 milestone (e.g., final SoC platform choice).

**Update Triggers**:
- When SoC platform is confirmed (currently: i.MX8M Plus recommended)
- When FPGA IP licenses are acquired
- When development boards are procured
- When actual dependencies are installed

---

## Table of Contents

1. [Hardware Platform](#hardware-platform)
2. [FPGA Development Tools](#fpga-development-tools)
3. [Software Development](#software-development)
4. [Version Control & CI/CD](#version-control--cicd)
5. [Development Methodology](#development-methodology)
6. [Testing Frameworks](#testing-frameworks)
7. [Build & Deployment](#build--deployment)
8. [Dependencies & Prerequisites](#dependencies--prerequisites)
9. [MCP Server Integrations](#mcp-server-integrations)
10. [FPGA IP Requirements](#fpga-ip-requirements)
11. [Constraints & Limitations](#constraints--limitations)
12. [Procurement Checklist](#procurement-checklist)

---

## Hardware Platform

### FPGA

**Device**: Xilinx Artix-7 XC7A35T-FGG484 (confirmed, non-negotiable)

**Specifications**:
- **Logic Cells**: 33,280
- **LUTs** (6-input): 20,800
- **Flip-Flops**: 41,600
- **BRAMs** (36 Kbit): 50 (total 1.8 Mbit)
- **DSP Slices**: 90
- **Max Distributed RAM**: 400 Kbit (from LUTs)
- **CMTs** (Clock Management Tiles): 5
- **I/O Banks**: 8
- **Total I/O Pins**: 285 (FGG484 package)
- **Package**: FGG484 (23 mm × 23 mm, 1.0 mm ball pitch)

**Speed Grade**: -1 (standard speed)
**Datasheet**: Xilinx DS181 (Artix-7 Product Table)

**Why This Device**:
- Smallest Artix-7 in FGG484 package (required for sufficient I/O pins for ROIC parallel interface)
- CSI-2 MIPI D-PHY implementation fits within LUT budget (~3,000-4,300 LUTs = 14-21%)
- USB 3.x IMPOSSIBLE (would require 14,980-25,008 LUTs = 72-120% of device)

**OSERDES Capability**:
- Maximum serialization ratio: 10:1 at DDR 1.25 Gbps (per Xilinx UG471)
- **D-PHY lane speed ceiling**: ~1.0-1.25 Gbps/lane (hardware limit, not D-PHY spec limit)
- **4-lane aggregate**: ~4-5 Gbps raw bandwidth

**Target LUT Utilization**: <60% (<12,480 LUTs) for 40% timing margin

---

### SoC (System-on-Chip)

**Recommended Platform**: NXP i.MX8M Plus (TBD at M0 milestone)

**Key Requirements**:
- **CSI-2 Receiver**: 4-lane MIPI D-PHY RX, supports 1.0-1.25 Gbps/lane
- **10 Gigabit Ethernet**: Built-in 10 GbE MAC/PHY controller (or external via PCIe)
- **Processing Power**: Quad-core ARM Cortex-A53 @ 1.8 GHz (or equivalent)
- **Memory**: ≥2 GB DDR4 RAM (for frame buffer, firmware, OS)
- **Storage**: eMMC or SD card for firmware boot
- **Interfaces**: SPI master (for FPGA control), UART (debug console), GPIO

**NXP i.MX8M Plus Specifications**:
- **CPU**: 4× ARM Cortex-A53 @ 1.8 GHz + 1× Cortex-M7 @ 800 MHz
- **GPU**: Vivante GC7000UL (optional, for image preprocessing)
- **CSI-2**: 2× 4-lane MIPI CSI-2 RX (up to 2.5 Gbps/lane per spec, 1.25 Gbps/lane validated)
- **Ethernet**: 1 Gbps Ethernet standard (10 GbE requires external NIC via PCIe)
- **Memory**: LPDDR4 up to 4 GB
- **Package**: FCBGA 17×17 mm

**Alternative Platforms** (to be evaluated at M0):
- Xilinx Zynq UltraScale+ MPSoC (integrated FPGA + ARM, higher cost)
- Texas Instruments AM62x (ARM Cortex-A53, CSI-2 support, lower power)
- Rockchip RK3588 (8-core ARM, multiple CSI-2 RX, cost-optimized)

**Decision Criteria**:
1. CSI-2 4-lane RX support at 1.0-1.25 Gbps/lane
2. 10 GbE capability (built-in or via PCIe NIC)
3. Availability of evaluation board
4. Software ecosystem maturity (Linux BSP, drivers)
5. Unit cost and long-term availability

**M0 Decision Point**: Confirm SoC platform by Week 1 based on procurement lead time and validation board availability

---

### Interfaces

#### FPGA → SoC: CSI-2 MIPI 4-lane D-PHY

**Physical Layer**: MIPI D-PHY v1.2 (Xilinx implementation via OSERDES + LVDS I/O)
**Lane Count**: 4 data lanes + 1 clock lane
**Lane Speed**: ~1.0-1.25 Gbps/lane (Artix-7 OSERDES limit)
**Aggregate Bandwidth**: ~4-5 Gbps raw (before protocol overhead)
**Usable Bandwidth**: ~3.2-3.5 Gbps (after 20-30% CSI-2 packet overhead)

**Connector**: MIPI CSI-2 FPC (Flexible Printed Circuit) cable, 0.5 mm pitch
**Cable Length**: ≤15 cm (signal integrity constraint)

**Data Format**: RAW16 (16-bit raw pixel data, no compression)
**Virtual Channel**: VC0 (single virtual channel)

---

#### SoC → Host: 10 Gigabit Ethernet

**Physical Layer**: 10GBASE-T (twisted pair) or 10GBASE-SR (fiber optic)
**Bandwidth**: 10 Gbps = 1.25 GB/s effective throughput
**Protocol**: UDP (User Datagram Protocol) for low latency
**Port**: 50000 (configurable in detector_config.yaml)

**NIC Requirements**:
- Intel X550-T2 (10GBASE-T, dual-port)
- Mellanox ConnectX-4 (10GBASE-SR, single-port)
- Or equivalent 10 GbE NIC with Linux driver support

**Switch Requirements**:
- Managed 10 GbE switch with ≥2 ports (SoC + Host)
- Jumbo frame support (MTU 9000 bytes) for efficiency
- Low latency (<1 μs forwarding delay)

**Alternative (1 GbE)**: Supported for Minimum tier only (0.21 Gbps ≪ 1 Gbps)
- **Target tier** (2.01 Gbps) and **Maximum tier** (4.53 Gbps) REQUIRE 10 GbE

---

#### Control Channel: SPI

**Mode**: FPGA = SPI slave, SoC = SPI master
**Clock Speed**: Up to 50 MHz
**Mode**: SPI Mode 0 (CPOL=0, CPHA=0)
**Data Width**: 8-bit words
**CS (Chip Select)**: Active low

**Use Cases**:
- SoC writes configuration registers to FPGA
- SoC reads status registers from FPGA (frame count, error flags)
- SoC issues commands (start/stop acquisition, reset)

**Register Map** (example):
- `0x00`: Control register (start/stop, reset)
- `0x04`: Status register (frame count, errors)
- `0x08`: Frame rate configuration
- `0x0C`: Resolution configuration

---

### Development Boards

#### FPGA Development Board

**Required Specifications**:
- Xilinx Artix-7 XC7A35T-FGG484 device
- MIPI CSI-2 TX connector (or breakout pins for custom FPC cable)
- SPI slave interface (GPIO headers)
- JTAG programming interface (14-pin or 6-pin)
- Clock source: External oscillator (e.g., 100 MHz)
- Power: USB or barrel jack (5V input)

**Example Boards**:
- Digilent Arty A7-35T (academic/hobbyist, may lack MIPI connector)
- Avnet MiniZed (includes Zynq, overkill for FPGA-only design)
- Custom board (designed in-house, longer lead time)

**Procurement**: Week 1 (M0 milestone, critical for PoC at M0.5)

---

#### SoC Evaluation Board

**Required Specifications**:
- i.MX8M Plus SoC (or equivalent platform from M0 decision)
- MIPI CSI-2 4-lane RX connector (compatible with FPGA dev board FPC cable)
- 10 GbE port (built-in or via M.2/PCIe expansion)
- SPI master interface (GPIO headers)
- UART console (USB-to-serial or 3.3V TTL)
- Power: 12V barrel jack or USB-C PD

**Example Boards**:
- NXP i.MX8M Plus EVK (official evaluation board, ~$500)
- Variscite VAR-SOM-MX8M-PLUS (SoM + carrier board, ~$400)
- TechNexion PICO-IMX8M-PLUS (compact form factor, ~$350)

**Procurement**: Week 3 (M0.5 preparation, CSI-2 RX validation)

---

#### Network Infrastructure

**10 Gigabit Ethernet NIC** (for Host PC):
- Intel X550-T2 (PCIe 3.0 x8, dual-port 10GBASE-T, ~$300)
- Mellanox ConnectX-4 Lx (PCIe 3.0 x8, dual-port SFP+, ~$250)

**10 Gigabit Ethernet Switch**:
- Netgear XS708E-200NES (8-port 10GBASE-T managed switch, ~$700)
- MikroTik CRS312-4C+8XG-RM (12-port, 4× SFP+, 8× 10GBASE-T, ~$600)

**Cables**:
- Cat6a or Cat7 Ethernet cables (for 10GBASE-T)
- SFP+ DAC (Direct Attach Copper) cables (for 10GBASE-SR)

**Procurement**: Week 8 (M2 milestone, Host link testing)

---

#### Optional: Logic Analyzer

**Purpose**: CSI-2 D-PHY protocol debug and validation

**Specifications**:
- MIPI D-PHY decode capability (4-lane + clock)
- Sample rate: ≥5 GSa/s (for 1.25 Gbps/lane capture)
- Memory depth: ≥512 Mbit (for multi-frame capture)

**Example Devices**:
- Keysight U4164A (MIPI D-PHY protocol analyzer, ~$10,000)
- Tektronix DPO7000 Series (oscilloscope with MIPI D-PHY decode, ~$20,000)
- Total Phase Promira MIPI Protocol Analyzer (~$5,000, cost-optimized)

**Procurement**: Week 3 (optional, for CSI-2 validation at M0.5)

---

## FPGA Development Tools

### Synthesis and Implementation

**Primary Tool**: AMD Vivado Design Suite (Standard Edition or higher)
**Target Device**: Artix-7 (xc7a35tfgg484-1)
**Recommended Version**: Vivado 2023.2 or later

**Key Features Required**:
- Artix-7 device support (included in all editions)
- MIPI CSI-2 TX Subsystem IP (license required, see [FPGA IP Requirements](#fpga-ip-requirements))
- SystemVerilog synthesis (IEEE 1800-2017 subset)
- Vivado Integrated Logic Analyzer (ILA) for debug
- Static timing analysis (built-in)
- Power analysis (XPower Analyzer)

**License Types**:
- **Vivado HL WebPACK Edition**: Free, includes Artix-7 support, but may not include MIPI IP
- **Vivado HL Design Edition**: Paid, includes all IP cores and advanced features (~$3,000/year)
- **Vivado HL System Edition**: Paid, includes embedded design tools (~$5,000/year)

**Recommendation**: Vivado HL Design Edition (for MIPI CSI-2 IP license)

**Installation**:
```bash
# Download from AMD Xilinx website (account required)
# Install directory: /opt/Xilinx/Vivado/2023.2 (Linux) or C:\Xilinx\Vivado\2023.2 (Windows)
```

---

### Simulation

**Primary Simulator**: Vivado Simulator (XSim) - bundled with Vivado
**Alternative Simulators**:
- Mentor Graphics ModelSim (industry standard, ~$5,000/year)
- Siemens Questa Sim (advanced debug, ~$10,000/year)
- Synopsys VCS (enterprise, ~$15,000/year)

**Recommendation**: Vivado Simulator (XSim) for cost savings, upgrade to ModelSim if advanced debug needed

**Testbench Language**: SystemVerilog (IEEE 1800-2017)

**Coverage Tools**:
- Line coverage: Built-in (XSim coverage)
- Branch coverage: Built-in
- FSM state coverage: Built-in
- Toggle coverage: Built-in

**Waveform Viewer**: Vivado Waveform Viewer (built-in)

---

### RTL Language

**Language**: SystemVerilog (IEEE 1800-2017)
**Subset**: Synthesizable subset (no SystemVerilog verification constructs in RTL)
**Style Guide**: Adherence to Xilinx UG901 (SystemVerilog Coding Guidelines)

**Key Constructs Used**:
- `always_comb`, `always_ff` (instead of `always @(*)` or `always @(posedge clk)`)
- `logic` type (instead of `reg` or `wire`)
- Enumerated types for FSM states
- Packed structs for register maps
- Parameterized modules

**File Extension**: `.sv` (SystemVerilog)

---

### Debug Tools

**Vivado Integrated Logic Analyzer (ILA)**:
- Embedded logic analyzer IP core
- Captures internal FPGA signals at runtime
- Trigger conditions: Logic levels, edges, patterns
- Sample depth: Up to 128K samples (configurable)
- Bandwidth: Limited by JTAG speed (~10-30 MB/s)

**Usage**:
```tcl
# Insert ILA core in RTL or via Vivado GUI
create_debug_core u_ila ila
set_property C_DATA_DEPTH 8192 [get_debug_cores u_ila]
connect_debug_port u_ila/probe0 [get_nets {panel_scan_fsm/state[*]}]
```

---

### FPGA IP Cores

**AMD/Xilinx MIPI CSI-2 TX Subsystem**:
- Version: v3.1 or later
- License: Included in Vivado HL Design Edition (or separate license)
- Configuration: 4-lane D-PHY, ~1.0-1.25 Gbps/lane
- LUT usage: ~2,500-3,500 LUTs (estimated)

**OSERDES Primitive**:
- Xilinx OSERDES2 (Artix-7) for D-PHY serialization
- Serialization ratio: 10:1 at DDR 1.25 Gbps
- Configuration: via Xilinx IDELAY and ODELAY primitives

**LVDS I/O Buffers**:
- Xilinx OBUFDS (differential output buffer) for D-PHY lanes
- IOSTANDARD: LVDS_25 (2.5V LVDS)

**BRAM Controller**:
- Custom dual-port BRAM controller (line buffer)
- Ping-pong buffer pattern (write to buffer A while reading from buffer B)

**SPI Slave IP** (optional):
- Custom RTL (simple shift register + FSM)
- Or Xilinx AXI Quad SPI IP (if AXI interface used)

---

### Timing Analysis

**Static Timing Analysis (STA)**: Vivado built-in
**Clock Constraints**: `constraints/timing.xdc`
**Key Metrics**:
- Worst Negative Slack (WNS): Must be ≥0 ns (no timing violations)
- Total Negative Slack (TNS): Must be 0 ns
- Worst Hold Slack (WHS): Must be ≥0 ns

**Timing Closure Strategy**:
- Target clock frequency: Panel clock 50 MHz, CSI-2 clock 250 MHz, D-PHY clock 1.25 GHz
- Pipeline critical paths (insert flip-flops to reduce combinational delay)
- Use FPGA fabric optimizations (LUT combining, register duplication)

---

### Power Analysis

**XPower Analyzer** (Vivado built-in):
- Estimates dynamic and static power consumption
- Input: Post-implementation netlist + activity file (VCD or SAIF)
- Output: Power report (mW breakdown by module)

**Target Power Budget**: <5W total FPGA power (reasonable for Artix-7 35T with active cooling)

---

## Software Development

### Programming Languages

**Primary Languages**:
1. **C# (.NET 8.0+)**: Simulators, GUI tools, code generators, test orchestration
2. **C/C++**: SoC firmware, Host SDK (C++ library), low-level HAL drivers
3. **SystemVerilog**: FPGA RTL and testbenches
4. **Python**: Optional scripting (config converters, automation)
5. **YAML/JSON**: Configuration files (detector_config.yaml, JSON schemas)

---

### .NET Development (.NET 8.0+)

**Runtime**: .NET 8.0 LTS (Long-Term Support, released November 2023)
**Language**: C# 12
**Frameworks**:
- **WPF** (Windows Presentation Foundation): GUI applications (ParameterExtractor, GUI.Application)
- **xUnit**: Unit testing framework
- **Newtonsoft.Json** or **System.Text.Json**: JSON parsing

**IDE Options**:
- Visual Studio 2022 (Windows, full-featured IDE)
- Visual Studio Code + C# Dev Kit (cross-platform, lightweight)
- JetBrains Rider (cross-platform, commercial IDE)

**Build Tool**: `dotnet` CLI (included with .NET SDK)

**Installation**:
```bash
# Download .NET 8.0 SDK from https://dotnet.microsoft.com/download
# Linux (Ubuntu):
sudo apt-get install -y dotnet-sdk-8.0

# Windows:
# Download and run installer from Microsoft website

# Verify installation:
dotnet --version
# Expected output: 8.0.x
```

---

### C/C++ Development (SoC Firmware)

**C Standard**: C11 (ISO/IEC 9899:2011)
**C++ Standard**: C++17 (ISO/IEC 14882:2017)
**Compiler**: GCC 11+ or Clang 14+
**Cross-Compilation**: ARM Cortex-A53 target (aarch64-linux-gnu-gcc)

**Build Tool**: CMake 3.20+
**Dependency Management**: Conan or vcpkg (optional, for third-party libraries)

**IDE Options**:
- Visual Studio Code + C/C++ extension (cross-platform)
- CLion (JetBrains, commercial)
- Eclipse CDT (open-source)

**Installation**:
```bash
# Install cross-compiler (Linux, Ubuntu):
sudo apt-get install -y gcc-aarch64-linux-gnu g++-aarch64-linux-gnu

# Install CMake:
sudo apt-get install -y cmake

# Verify installation:
aarch64-linux-gnu-gcc --version
cmake --version
```

---

### GUI Framework (WPF - Windows Presentation Foundation)

**Platform**: Windows 10/11 (WPF is Windows-only)
**Language**: C# + XAML (eXtensible Application Markup Language)
**Architecture**: MVVM (Model-View-ViewModel) pattern

**Key Tools**:
- **ParameterExtractor**: PDF parsing → YAML export
- **GUI.Application**: System control, parameter tuning, image visualization

**Dependencies**:
- .NET 8.0 Windows Desktop Runtime
- Newtonsoft.Json (NuGet package)
- iTextSharp or PdfSharp (PDF parsing, NuGet)

**Alternative (Cross-Platform)**:
- Avalonia UI (WPF-like, cross-platform XAML framework)
- .NET MAUI (successor to Xamarin.Forms, mobile + desktop)

**Recommendation**: Stick with WPF for Windows-only deployment (faster development, mature ecosystem)

---

## Version Control & CI/CD

### Version Control System

**Platform**: Gitea (self-hosted Git service)
**Hosting**: On-premises server or cloud VPS
**Repository Count**: 6 (fpga/, fw/, sdk/, tools/, config/, docs/)

**Gitea Features**:
- Git repository hosting (like GitHub/GitLab)
- Web UI for code review, issue tracking
- Webhook support for CI/CD integration
- Access control (user/team permissions)
- API for automation

**Installation**:
```bash
# Docker installation (recommended):
docker run -d --name gitea -p 3000:3000 -p 222:22 \
  -v /var/lib/gitea:/data \
  gitea/gitea:latest
```

**Repository URLs** (example):
```
https://gitea.example.com/xray-detector/fpga.git
https://gitea.example.com/xray-detector/fw.git
https://gitea.example.com/xray-detector/sdk.git
https://gitea.example.com/xray-detector/tools.git
https://gitea.example.com/xray-detector/config.git
https://gitea.example.com/xray-detector/docs.git
```

---

### CI/CD Workflow Automation

**Platform**: n8n (workflow automation, open-source)
**Integration**: Gitea webhooks → n8n → Build/Test automation

**n8n Features**:
- Visual workflow editor (drag-and-drop nodes)
- Gitea webhook trigger (on push, pull_request, tag)
- SSH node (execute commands on build server)
- HTTP node (POST status to Gitea API)
- Email node (notify team on build failure)

**Example Workflow** (FPGA build on push):
```
Trigger: Gitea Webhook (Push to fpga/main)
│
├─> Filter: If push to main branch
│
├─> SSH: cd fpga/ && vivado -mode batch -source scripts/build.tcl
│
├─> SSH: Parse reports/utilization.rpt
│   ├─> If LUT usage >60%: Send warning email
│   └─> If LUT usage <60%: Continue
│
└─> HTTP: POST status to Gitea API (commit status: success/failure)
```

**Installation**:
```bash
# Docker installation:
docker run -d --name n8n -p 5678:5678 \
  -v /var/lib/n8n:/home/node/.n8n \
  n8nio/n8n
```

**Access**: http://localhost:5678

---

### Git Commit Convention

**Format**: Conventional Commits (https://www.conventionalcommits.org/)

**Structure**:
```
<type>(<scope>): <subject>

<body>

🗿 MoAI <email@mo.ai.kr>
```

**Types**:
- `feat`: New feature (e.g., `feat(fpga): add CSI-2 TX module`)
- `fix`: Bug fix (e.g., `fix(fw): correct frame buffer overflow`)
- `docs`: Documentation only (e.g., `docs(api): update API reference`)
- `refactor`: Code refactoring (no functional change)
- `test`: Add or update tests
- `chore`: Maintenance (e.g., `chore(deps): update .NET SDK to 8.0.2`)

**Scopes**:
- `fpga`, `fw`, `sdk`, `tools`, `config`, `docs`

**Body**: Optional, detailed explanation (can be in Korean with English technical terms)

**Footer**: Co-authored-by, issue references (e.g., `Fixes #123`)

**Example**:
```
feat(fpga): implement CSI-2 TX wrapper module

Added SystemVerilog wrapper for AMD MIPI CSI-2 TX Subsystem IP.
Configured for 4-lane D-PHY at 1.25 Gbps/lane.
LUT usage: 2,800 (13.5% of device).

🗿 MoAI <email@mo.ai.kr>
```

---

## Development Methodology

### Hybrid Approach (TDD + DDD)

**Configured in**: `.moai/config/sections/quality.yaml` → `development_mode: "hybrid"`

**Mode Selection Logic**:
- **New code** (new files, new modules): Use **TDD** (RED-GREEN-REFACTOR)
- **Existing code** (modifications, refactoring): Use **DDD** (ANALYZE-PRESERVE-IMPROVE)

**Why Hybrid**:
- Project has mix of greenfield code (simulators, tools) and to-be-developed code (FPGA RTL, firmware HAL)
- Maximizes test coverage while allowing characterization tests for hardware-centric modules

---

### TDD Workflow (Test-Driven Development)

**Cycle**: RED-GREEN-REFACTOR

**Step 1: RED** (Write a failing test):
```csharp
// Example: PanelSimulator unit test
[Fact]
public void GenerateFrame_ShouldReturn1024x1024Pixels()
{
    var simulator = new PanelSimulator(width: 1024, height: 1024, bitDepth: 14);
    var frame = simulator.GenerateFrame();

    Assert.Equal(1024, frame.Width);
    Assert.Equal(1024, frame.Height);
    Assert.Equal(1024 * 1024, frame.PixelData.Length);
}
```

**Run test**: `dotnet test` → **Fails** (PanelSimulator not implemented)

**Step 2: GREEN** (Write minimal code to pass):
```csharp
public class PanelSimulator
{
    public int Width { get; }
    public int Height { get; }

    public PanelSimulator(int width, int height, int bitDepth)
    {
        Width = width;
        Height = height;
    }

    public FrameData GenerateFrame()
    {
        return new FrameData
        {
            Width = Width,
            Height = Height,
            PixelData = new ushort[Width * Height]
        };
    }
}
```

**Run test**: `dotnet test` → **Passes** ✅

**Step 3: REFACTOR** (Improve code quality):
- Extract constants, clean up naming, add documentation

**Repeat** for next feature (e.g., noise injection, gain/offset)

---

### DDD Workflow (Domain-Driven Development)

**Cycle**: ANALYZE-PRESERVE-IMPROVE

**Use Case**: Refactoring FPGA RTL module (existing code)

**Step 1: ANALYZE** (Understand existing behavior):
- Read RTL code, identify FSM states, understand timing
- Map inputs/outputs, identify side effects

**Step 2: PRESERVE** (Create characterization tests):
```systemverilog
// Example: panel_scan_fsm characterization test
module panel_scan_fsm_tb;
    reg clk, rst_n, start;
    wire [2:0] state;
    wire frame_done;

    panel_scan_fsm dut (
        .clk(clk), .rst_n(rst_n), .start(start),
        .state(state), .frame_done(frame_done)
    );

    // Characterization: Capture current behavior
    initial begin
        // Test case 1: Normal frame scan
        start = 1;
        repeat(1024) @(posedge clk);
        assert(frame_done == 1) else $error("Frame not completed");

        // Test case 2: Reset during scan
        start = 1;
        repeat(100) @(posedge clk);
        rst_n = 0; @(posedge clk); rst_n = 1;
        assert(state == IDLE) else $error("Reset did not return to IDLE");
    end
endmodule
```

**Run test**: Verify current behavior is captured

**Step 3: IMPROVE** (Refactor with test validation):
- Make small, incremental changes to RTL
- Run characterization tests after each change
- Verify behavior is preserved (tests still pass)

---

### Coverage Targets

**RTL (FPGA)**:
- **Line Coverage**: ≥95% (all executable lines)
- **Branch Coverage**: ≥90% (all if/else branches)
- **FSM State Coverage**: 100% (all states visited)
- **Toggle Coverage**: ≥80% (for critical signals like CSI-2 data lanes)

**Software (C#/C++)**:
- **Per-Module Coverage**: 80-90%
- **Overall Coverage**: ≥85%

**Integration Tests**:
- 10 scenarios (IT-01 through IT-10) covering end-to-end data paths
- HIL test patterns with hardware-in-the-loop validation

**Coverage Measurement Tools**:
- RTL: Vivado Simulator coverage (XSim built-in)
- C#: dotnet-coverage (Microsoft tool)
- C++: gcov (GCC coverage tool) or lcov (HTML reports)

---

## Testing Frameworks

### .NET Testing (C# Simulators, Tools)

**Framework**: xUnit 2.6+
**Assertion Library**: xUnit assertions (built-in)
**Mocking**: Moq or NSubstitute (if needed for interfaces)
**Coverage**: dotnet-coverage (Microsoft official tool)

**Installation**:
```bash
# Add xUnit to project:
dotnet add package xunit
dotnet add package xunit.runner.visualstudio

# Run tests:
dotnet test

# Run tests with coverage:
dotnet test --collect:"XPlat Code Coverage"
```

**Example Test**:
```csharp
using Xunit;

public class FpgaSimulatorTests
{
    [Fact]
    public void Csi2Transmitter_ShouldGenerateValidPackets()
    {
        var transmitter = new Csi2Transmitter(lanes: 4, laneSpeedGbps: 1.25);
        var packets = transmitter.GenerateFramePackets(width: 2048, height: 2048, bitDepth: 16);

        Assert.Equal(2048, packets.Count); // One packet per line
        Assert.All(packets, p => Assert.Equal(0x2C, p.DataType)); // RAW16 data type
    }
}
```

---

### C++ Testing (Host SDK)

**Framework**: Google Test (gtest) 1.14+
**Assertion Library**: gtest assertions (built-in)
**Mocking**: Google Mock (gmock, part of Google Test)
**Coverage**: gcov + lcov (HTML reports)

**Installation**:
```bash
# Install Google Test (Ubuntu):
sudo apt-get install -y libgtest-dev

# Build and link against gtest:
# CMakeLists.txt:
find_package(GTest REQUIRED)
target_link_libraries(test_detector_control GTest::GTest GTest::Main)
```

**Example Test**:
```cpp
#include <gtest/gtest.h>
#include "detector_control.hpp"

TEST(DetectorControlTest, StartAcquisition_ShouldReturnSuccess) {
    DetectorControl controller;
    auto result = controller.StartAcquisition();
    EXPECT_EQ(result, Status::SUCCESS);
}
```

**Run Tests**:
```bash
cd sdk/cpp/build/
ctest --verbose
```

---

### SystemVerilog Testing (FPGA RTL)

**Framework**: SystemVerilog Testbenches (IEEE 1800-2017)
**Simulator**: Vivado Simulator (XSim) or ModelSim
**Assertions**: SystemVerilog Assertions (SVA) for property checking
**Coverage**: XSim built-in coverage or ModelSim coverage

**Example Testbench**:
```systemverilog
module spi_slave_tb;
    reg clk, sclk, mosi, cs_n;
    wire miso;

    spi_slave dut (.clk(clk), .sclk(sclk), .mosi(mosi), .cs_n(cs_n), .miso(miso));

    initial begin
        // Test: Write 0x55 to SPI slave
        cs_n = 0;
        repeat(8) begin
            mosi = 0x55[7]; sclk = 0; #10; sclk = 1; #10;
            0x55 = 0x55 << 1;
        end
        cs_n = 1;

        // Verify: Read back 0x55 from internal register
        assert(dut.rx_data == 8'h55) else $error("SPI write failed");
        $display("Test PASSED");
        $finish;
    end
endmodule
```

**Run Simulation**:
```bash
cd fpga/
vivado -mode batch -source scripts/simulate.tcl
```

---

### Python Testing (Config Converters, Automation)

**Framework**: pytest 8.0+
**Assertion Library**: pytest assertions (built-in)
**Mocking**: unittest.mock (standard library)
**Coverage**: pytest-cov (pytest plugin)

**Installation**:
```bash
pip install pytest pytest-cov
```

**Example Test**:
```python
import pytest
from yaml_to_verilog import YamlToVerilogConverter

def test_convert_panel_width():
    converter = YamlToVerilogConverter("detector_config.yaml")
    verilog_params = converter.convert()

    assert "`define PANEL_WIDTH 2048" in verilog_params
```

**Run Tests**:
```bash
cd config/converters/
pytest --cov=. --cov-report=html
```

---

## Build & Deployment

### FPGA Build (Vivado)

**Build Script**: `fpga/scripts/build.tcl`
**Build Command**:
```bash
cd fpga/
vivado -mode batch -source scripts/build.tcl
```

**Build Steps**:
1. Create Vivado project
2. Add RTL sources, IP cores, constraints
3. Run synthesis (synth_design)
4. Run implementation (opt_design, place_design, route_design)
5. Generate bitstream (write_bitstream)
6. Export reports (utilization, timing, power)

**Build Output**:
- `panel_acquisition.bit` (bitstream file, ~1-2 MB)
- `panel_acquisition.ltx` (ILA debug probes)
- `reports/utilization.rpt` (LUT/BRAM usage)
- `reports/timing.rpt` (WNS, TNS, WHS)

**Build Time**: ~10-20 minutes (depends on host CPU)

---

### SoC Firmware Build (CMake)

**Build Script**: `fw/build/CMakeLists.txt`
**Build Command**:
```bash
cd fw/build/
cmake -DCMAKE_BUILD_TYPE=Release ..
make -j4
```

**Build Steps**:
1. Configure CMake (generate Makefiles)
2. Cross-compile C/C++ sources (aarch64-linux-gnu-gcc)
3. Link libraries (pthread, m, lwip)
4. Generate ELF binary

**Build Output**:
- `firmware.elf` (ELF executable, debug symbols included)
- `firmware.bin` (raw binary, for flashing to eMMC)

**Build Time**: ~1-3 minutes

---

### Host SDK Build (CMake + dotnet)

**C++ SDK**:
```bash
cd sdk/cpp/
cmake -B build -DCMAKE_BUILD_TYPE=Release
cmake --build build -j4
cmake --install build --prefix /usr/local
```

**Build Output**: `libdetector_sdk.so` (shared library)

**C# SDK**:
```bash
cd sdk/csharp/
dotnet build -c Release
dotnet pack -c Release
```

**Build Output**: `DetectorSDK.1.0.0.nupkg` (NuGet package)

---

### Developer Tools Build (.NET)

**Build Command**:
```bash
cd tools/
dotnet build -c Release
```

**Build Output**:
- `PanelSimulator/bin/Release/net8.0/PanelSimulator.exe`
- `FpgaSimulator/bin/Release/net8.0/FpgaSimulator.exe`
- `ParameterExtractor/bin/Release/net8.0/ParameterExtractor.exe`
- ... (all tool executables)

**Publish (Standalone Executable)**:
```bash
cd tools/ParameterExtractor/
dotnet publish -c Release -r win-x64 --self-contained
```

**Output**: `bin/Release/net8.0/win-x64/publish/ParameterExtractor.exe` (includes .NET runtime, no installation required)

---

## Dependencies & Prerequisites

### Host PC Requirements

**Operating System**:
- Windows 10/11 (for WPF GUI tools, Vivado)
- Linux Ubuntu 22.04 LTS (for SoC firmware cross-compilation, SDK)

**CPU**: Intel Core i7 or AMD Ryzen 7 (≥4 cores, 8 threads)
**RAM**: ≥16 GB (32 GB recommended for Vivado)
**Storage**: ≥100 GB free space (FPGA tools, source code, builds)
**GPU**: Optional (for GUI acceleration)

**Network**: 10 GbE NIC (see [Procurement Checklist](#procurement-checklist))

---

### Software Dependencies

**FPGA Development**:
- AMD Vivado Design Suite 2023.2 or later (Standard Edition or higher)
- MIPI CSI-2 TX IP license (included in Vivado HL Design Edition)

**.NET Development**:
- .NET SDK 8.0 or later (https://dotnet.microsoft.com/download)
- Visual Studio 2022 (optional, for WPF GUI development)

**C/C++ Development**:
- GCC 11+ or Clang 14+ (native compilation)
- aarch64-linux-gnu-gcc 11+ (cross-compilation for SoC)
- CMake 3.20+

**Python** (optional, for config converters):
- Python 3.10+
- pip packages: PyYAML, jsonschema, jinja2

**Version Control**:
- Git 2.40+
- Gitea (self-hosted, Docker installation recommended)

**CI/CD**:
- n8n (self-hosted, Docker installation recommended)

---

### Hardware Dependencies

**Development Boards**:
- Xilinx Artix-7 35T FGG484 dev board (Week 1 procurement)
- i.MX8M Plus eval board (Week 3 procurement)

**Network Infrastructure**:
- 10 GbE NIC (Intel X550-T2 or equivalent)
- 10 GbE managed switch (Netgear XS708E or equivalent)
- Cat6a/Cat7 Ethernet cables

**Cables & Adapters**:
- MIPI CSI-2 FPC cable (0.5 mm pitch, ≤15 cm length)
- JTAG programmer (Xilinx Platform Cable USB II or compatible)
- USB-to-serial adapter (3.3V TTL, for SoC UART console)

---

## MCP Server Integrations

MoAI-ADK integrates multiple MCP (Model Context Protocol) servers for specialized capabilities:

### Sequential Thinking MCP

**Purpose**: Deep analysis for architecture decisions, technology trade-offs, problem decomposition

**Activation**: Use `--ultrathink` flag with any MoAI command
```bash
/moai plan --ultrathink "Design CSI-2 TX module with bandwidth optimization"
```

**Use Cases**:
- FPGA architecture decisions (e.g., CSI-2 vs. USB 3.x trade-off analysis)
- SoC platform selection (i.MX8M Plus vs. alternatives)
- Performance optimization strategies (Maximum tier: 4.53 Gbps, borderline CSI-2 bandwidth)

**Model**: Claude Opus 4.6 (higher-tier model for complex reasoning)

---

### Context7 MCP

**Purpose**: Retrieve up-to-date library documentation and API references

**Usage**: Automatically invoked when MoAI needs documentation for .NET, FPGA IP, or third-party libraries

**Example**:
```bash
# Internally, MoAI calls:
mcp__context7__resolve-library-id("dotnet", "8.0")
mcp__context7__get-library-docs(library_id, "System.Text.Json")
```

**Use Cases**:
- .NET SDK API reference (e.g., `System.Text.Json` for config parsing)
- AMD Xilinx IP documentation (e.g., MIPI CSI-2 TX Subsystem v3.1)
- Third-party library usage (e.g., iTextSharp for PDF parsing)

---

### Pencil MCP

**Purpose**: UI/UX design editing for .pen files (used by GUI tools)

**Activation**: Invoked by `expert-frontend` or `team-designer` agents when designing WPF GUI

**Use Cases**:
- ParameterExtractor GUI design (WPF layouts, control placement)
- GUI.Application interface design (image viewer, parameter tuning panels)

**Note**: Pencil MCP is optional; GUI design can also be done manually in Visual Studio XAML designer

---

## FPGA IP Requirements

### AMD/Xilinx MIPI CSI-2 TX Subsystem

**Product**: PG232 (MIPI CSI-2 TX Subsystem Product Guide)
**Version**: v3.1 or later (compatible with Vivado 2023.2+)
**License**: Included in Vivado HL Design Edition (or purchase separately)

**Configuration**:
- **Lanes**: 4 data lanes + 1 clock lane
- **Lane Speed**: 1.0-1.25 Gbps/lane (configurable via GUI or TCL)
- **D-PHY**: Xilinx D-PHY via OSERDES primitives + LVDS I/O buffers
- **Data Type**: RAW16 (0x2C, 16-bit raw pixel data)
- **Virtual Channel**: VC0 (single virtual channel)
- **Line Blanking**: Configurable (default: 100 pixel clocks)
- **Frame Blanking**: Configurable (default: 10 line times)

**Resource Usage** (from PG232):
- LUTs: ~2,500-3,500 (depends on configuration)
- BRAMs: 1-2 (internal packet FIFO)
- OSERDES: 8 (4 data lanes × 2 for DDR serialization)
- LVDS I/O Buffers: 5 (4 data lanes + 1 clock lane)

**IP Configuration Wizard** (Vivado):
```tcl
# Example TCL script for IP configuration
create_ip -name mipi_csi2_tx_subsystem -vendor xilinx.com -library ip -version 3.1 -module_name mipi_csi2_tx
set_property CONFIG.C_LANES {4} [get_ips mipi_csi2_tx]
set_property CONFIG.C_DPHY_LANES {4} [get_ips mipi_csi2_tx]
set_property CONFIG.C_LANE_SPEED {1250} [get_ips mipi_csi2_tx]
set_property CONFIG.C_HS_LINE_RATE {1.25} [get_ips mipi_csi2_tx]
generate_target all [get_ips mipi_csi2_tx]
```

**License Acquisition**:
- Option 1: Purchase Vivado HL Design Edition (~$3,000/year, includes all IP)
- Option 2: Purchase standalone MIPI CSI-2 IP license (~$1,500 one-time + annual support)

---

### D-PHY Implementation via OSERDES

**Primitive**: OSERDES2 (Artix-7 output serializer/deserializer)
**Serialization Ratio**: 10:1 (10 parallel bits → 1 serial bit)
**DDR Mode**: Enabled (output on both rising and falling clock edges)
**Lane Speed**: 1.0-1.25 Gbps/lane (DDR 625 MHz × 2 = 1.25 Gbps)

**Instantiation** (example):
```systemverilog
OSERDES2 #(
    .DATA_RATE_OQ("DDR"),
    .DATA_RATE_OT("DDR"),
    .SERDES_MODE("MASTER"),
    .DATA_WIDTH(10)
) oserdes_data_lane0 (
    .CLK0(clk_625mhz),         // 625 MHz clock
    .CLKDIV(clk_62p5mhz),      // 62.5 MHz parallel clock (625/10)
    .D1(data[0]), .D2(data[1]), .D3(data[2]), .D4(data[3]),
    .D5(data[4]), .D6(data[5]), .D7(data[6]), .D8(data[7]),
    .D9(data[8]), .D10(data[9]),
    .OCE(1'b1),
    .RST(rst),
    .OQ(serial_out)            // Serial output to LVDS buffer
);
```

**LVDS Output Buffer**:
```systemverilog
OBUFDS #(
    .IOSTANDARD("LVDS_25")
) obufds_data_lane0 (
    .I(serial_out),
    .O(dphy_data_p[0]),        // Differential positive
    .OB(dphy_data_n[0])        // Differential negative
);
```

---

### SPI Slave IP (Optional)

**Option 1**: Custom RTL (recommended for simplicity)
- Simple shift register + FSM (< 300 LUTs)
- Direct register access without AXI overhead

**Option 2**: Xilinx AXI Quad SPI IP (if AXI infrastructure used)
- Product: PG153 (AXI Quad SPI Product Guide)
- Configured as SPI slave mode
- AXI4-Lite interface to internal registers

**Recommendation**: Custom RTL (simpler, lower overhead, no AXI license required)

---

## Constraints & Limitations

### FPGA Resource Constraints (ABSOLUTE)

**Device**: Xilinx Artix-7 XC7A35T-FGG484
**LUT Budget**: 20,800 LUTs (target utilization <60% = <12,480 LUTs)
**BRAM Budget**: 50 BRAMs (36 Kbit each)
**Current Estimate**: ~10,250 LUTs (49.3%) ✅ Meets target with 10.7% margin

**Why USB 3.x is IMPOSSIBLE**:
- USB 3.0 SuperSpeed IP requires 14,980-17,400 LUTs (72-84% of device)
- USB 3.1 Gen2 IP requires 20,000-25,008 LUTs (96-120% of device, **EXCEEDS** capacity)
- After USB IP, remaining LUTs insufficient for panel control logic, line buffers, protection logic

**CSI-2 Resource Estimate**:
- MIPI CSI-2 TX Subsystem: ~2,500-3,500 LUTs (12-17%)
- D-PHY via OSERDES: ~500-800 LUTs (2-4%)
- **Total CSI-2**: ~3,000-4,300 LUTs (14-21%) ✅ Leaves 60-80% for application logic

---

### D-PHY Bandwidth Ceiling

**Artix-7 OSERDES Speed**: Maximum serialization ratio 10:1 at DDR 1.25 Gbps (per Xilinx DS181 datasheet)
**Lane Speed**: ~1.0-1.25 Gbps/lane (practical, with timing margin)
**4-Lane Aggregate**: ~4-5 Gbps raw bandwidth

**NOT a D-PHY Specification Limit**: D-PHY v2.5 supports up to 2.5 Gbps/lane, but Artix-7 OSERDES is the bottleneck
**Implication**: Cannot achieve full D-PHY v2.5 speed; limited to ~1.0-1.25 Gbps/lane by FPGA hardware

**CSI-2 Protocol Overhead**: ~20-30% (packet headers, line start/end, frame start/end, blanking intervals)
**Usable Bandwidth**: ~3.2-3.5 Gbps effective payload

**Performance Tier Implications**:
- **Minimum Tier** (0.21 Gbps): ✅ Well within capacity (15% utilization)
- **Target Tier** (2.01 Gbps): ✅ Fits comfortably (57-63% utilization)
- **Maximum Tier** (4.53 Gbps): ⚠️ Borderline, exceeds usable bandwidth, requires aggressive frame buffer optimization or compression

---

### Host Link Bandwidth Constraint

**1 Gigabit Ethernet**: ~125 MB/s (1 Gbps / 8) effective throughput
- **Minimum Tier**: 0.21 Gbps → 26.25 MB/s ✅ OK (21% utilization)
- **Target Tier**: 2.01 Gbps → 251.25 MB/s ❌ EXCEEDS 1 GbE capacity
- **Maximum Tier**: 4.53 Gbps → 566.25 MB/s ❌ FAR EXCEEDS 1 GbE capacity

**10 Gigabit Ethernet**: ~1.25 GB/s (10 Gbps / 8) effective throughput
- **Minimum Tier**: 0.21 Gbps → 26.25 MB/s ✅ OK (2% utilization)
- **Target Tier**: 2.01 Gbps → 251.25 MB/s ✅ OK (20% utilization)
- **Maximum Tier**: 4.53 Gbps → 566.25 MB/s ✅ OK (45% utilization)

**Recommendation**: **10 GbE REQUIRED** for Target and Maximum tiers

---

### FPGA LUT Budget Monitoring

**Target**: <60% LUT utilization (<12,480 LUTs)
**Risk Threshold**: >70% LUT utilization (>14,560 LUTs) triggers warning

**Monitoring Strategy**:
- CI/CD pipeline parses `reports/utilization.rpt` after each build
- If LUT usage >60%, send warning email to team
- If LUT usage >70%, block merge to main branch (manual review required)

**Mitigation if Budget Exceeded**:
- Reduce Maximum tier support (drop 3072×3072 resolution)
- Optimize line buffer (reduce BRAM usage, use LUTRAM instead)
- Remove optional features (thermal monitor, timing watchdog)
- Consider larger FPGA (Artix-7 50T or 100T, ~$100-200 higher cost)

---

## Procurement Checklist

### Hardware (M0-M0.5 Phase)

**Week 1 (M0 Milestone)**:
- [ ] **Xilinx Artix-7 35T FGG484 dev board**
  - Quantity: 1
  - Estimated Cost: $200-400
  - Vendor: Digilent, Avnet, or custom board
  - Lead Time: 2-4 weeks
  - Critical for: CSI-2 PoC at M0.5

**Week 3 (M0.5 Preparation)**:
- [ ] **i.MX8M Plus eval board** (or equivalent SoC platform)
  - Quantity: 1
  - Estimated Cost: $400-500
  - Vendor: NXP, Variscite, TechNexion
  - Lead Time: 2-3 weeks
  - Critical for: CSI-2 RX validation, SoC firmware development

- [ ] **MIPI CSI-2 FPC cable**
  - Type: 0.5 mm pitch, 15-pin (4 data lanes + 1 clock + ground)
  - Length: ≤15 cm
  - Quantity: 2 (backup)
  - Estimated Cost: $10-20 each
  - Vendor: Molex, Samtec, or cable manufacturer

- [ ] **Logic analyzer with MIPI D-PHY decode** (optional)
  - Model: Total Phase Promira MIPI Protocol Analyzer (cost-optimized)
  - Estimated Cost: $5,000
  - Lead Time: 3-4 weeks
  - Alternative: Tektronix or Keysight (higher cost, $10K-20K)

**Week 8 (M2 Milestone)**:
- [ ] **10 Gigabit Ethernet NIC** (for Host PC)
  - Model: Intel X550-T2 (dual-port 10GBASE-T)
  - Quantity: 1
  - Estimated Cost: $300
  - Vendor: Intel, Newegg, Amazon

- [ ] **10 Gigabit Ethernet switch**
  - Model: Netgear XS708E-200NES (8-port managed)
  - Quantity: 1
  - Estimated Cost: $700
  - Vendor: Netgear, CDW, Amazon

- [ ] **Cat6a Ethernet cables**
  - Quantity: 3 (SoC → switch, Host → switch, spare)
  - Length: 1-3 meters
  - Estimated Cost: $10-20 each

---

### Software Licenses (M0 Phase)

**FPGA Development**:
- [ ] **AMD Vivado HL Design Edition** (annual subscription)
  - Includes: Artix-7 support, MIPI CSI-2 IP, full IP library
  - Estimated Cost: $3,000/year
  - Lead Time: Immediate (online purchase + license file)
  - Alternative: Vivado HL WebPACK Edition (free, but may not include MIPI IP)

**Optional Tools**:
- [ ] **Mentor Graphics ModelSim** (if Vivado Simulator insufficient)
  - Estimated Cost: $5,000/year
  - Lead Time: 1-2 weeks (sales inquiry + license provisioning)

---

### Development Environment Setup (M0 Phase)

**Host PC Build/Upgrade**:
- [ ] **RAM Upgrade**: ≥16 GB (32 GB recommended for Vivado)
  - Estimated Cost: $100-200 (if upgrade needed)
- [ ] **Storage**: ≥100 GB free space (or add SSD)
  - Estimated Cost: $50-150 (if additional SSD needed)
- [ ] **10 GbE NIC**: (see hardware checklist above)

**Software Installation** (Week 1):
- [ ] AMD Vivado 2023.2+ (FPGA synthesis)
- [ ] .NET SDK 8.0+ (C# development)
- [ ] Visual Studio 2022 or VS Code (IDE)
- [ ] Git 2.40+ (version control)
- [ ] Docker (for Gitea + n8n hosting)

---

## Summary

This document catalogs the complete technology stack for the X-ray Detector Panel System. Key takeaways:

1. **Hardware Platform**: Xilinx Artix-7 XC7A35T FPGA + NXP i.MX8M Plus SoC (recommended)
2. **FPGA Tools**: AMD Vivado 2023.2+, MIPI CSI-2 TX IP, SystemVerilog RTL
3. **Software Stack**: .NET 8.0 C# (simulators/tools), C/C++ (firmware/SDK), Python (optional scripting)
4. **Version Control**: Gitea (self-hosted Git) + n8n (CI/CD automation)
5. **Development Methodology**: Hybrid TDD/DDD, 85%+ coverage, TRUST 5 compliance
6. **Testing**: xUnit (C#), Google Test (C++), SystemVerilog testbenches, pytest (Python)
7. **Critical Constraints**: FPGA LUT budget <60%, D-PHY lane speed ~1.0-1.25 Gbps, 10 GbE required for Target/Maximum tiers
8. **Procurement**: FPGA dev board (W1), SoC eval board (W3), 10 GbE infrastructure (W8)

**Next Steps**:
- Confirm SoC platform at M0 milestone (Week 1)
- Procure FPGA dev board (critical for PoC at M0.5)
- Acquire Vivado HL Design Edition license (includes MIPI CSI-2 IP)
- Install development environment on Host PC

---

**Document End**

*This is a pre-implementation technology plan. Run `/moai project --refresh` after code repositories are cloned to update documentation with actual dependencies and toolchain versions.*
