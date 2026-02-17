# Development Environment Setup Guide

**Project**: X-ray Detector Panel System
**Version**: 1.0.0
**Last Updated**: 2026-02-17

---

## 1. Overview

This guide describes how to set up a complete development environment for the X-ray Detector Panel System. The system spans three layers (FPGA, SoC firmware, Host PC software) and requires specific toolchains for each.

### 1.1 Target Audience

Developers with experience in embedded systems and/or FPGA development who will be contributing to any part of the X-ray Detector Panel System.

### 1.2 Development Layers

| Layer | Technology | Primary Language | Toolchain |
|-------|-----------|-----------------|-----------|
| FPGA RTL | Xilinx Artix-7 | SystemVerilog | AMD Vivado |
| SoC Firmware | NXP i.MX8M Plus | C (C11) | Yocto SDK (GCC cross-compiler) |
| Host SDK | .NET 8.0 | C# 12 | .NET SDK |
| Simulators | .NET 8.0 | C# 12 | .NET SDK |
| Tools (GUI) | .NET 8.0 / WPF | C# 12 | .NET SDK (Windows) |

### 1.3 Supported Platforms

| Component | Windows 10/11 | Ubuntu 22.04+ | macOS |
|-----------|:------------:|:------------:|:-----:|
| FPGA Development (Vivado) | Yes | Yes | No |
| SoC Firmware (Cross-compile) | WSL2 required | Yes | No |
| Host SDK (.NET) | Yes | Yes | Yes |
| Simulators (.NET) | Yes | Yes | Yes |
| GUI Application (WPF) | Yes | No | No |

---

## 2. Prerequisites

### 2.1 Hardware Requirements

| Component | Minimum | Recommended |
|-----------|---------|-------------|
| CPU | 4-core x86-64 | 8-core x86-64 |
| RAM | 8 GB | 32 GB |
| Storage | 100 GB free | 250 GB SSD |
| Network | 1 GbE | 10 GbE (for Target tier testing) |
| Display | 1920x1080 | 2560x1440 |

**Note**: Vivado synthesis requires significant memory. 32 GB RAM is strongly recommended for FPGA builds.

### 2.2 Software Prerequisites

The following software must be installed before proceeding:

| Software | Version | Purpose | Download |
|----------|---------|---------|----------|
| Git | 2.30+ | Version control | https://git-scm.com/ |
| AMD Vivado | 2023.2+ HL Design Edition | FPGA synthesis, simulation | AMD website (license required) |
| .NET SDK | 8.0 LTS | C# development | https://dotnet.microsoft.com/download |
| Visual Studio 2022 | 17.8+ (Community or Pro) | C# IDE, WPF designer | Microsoft website |
| VS Code | Latest | Lightweight editor | https://code.visualstudio.com/ |
| Python | 3.10+ | Build scripts, automation | https://python.org/ |

**Optional** (for SoC firmware development):

| Software | Version | Purpose |
|----------|---------|---------|
| Yocto SDK | Kirkstone (5.15) | i.MX8M Plus cross-compiler |
| CMake | 3.20+ | Firmware build system |
| GDB Multiarch | Latest | Remote debugging |
| Docker | 24+ | Reproducible build environment |

---

## 3. Repository Setup

### 3.1 Clone the Repository

```bash
# Clone main development repository
git clone <gitea-url>/system-emul-sim.git
cd system-emul-sim
```

### 3.2 Repository Structure

```
system-emul-sim/
  .claude/                      # MoAI agent configuration
  .moai/                        # SPEC documents and project config
    config/                     # Project configuration files
      sections/
        language.yaml           # Language settings
        quality.yaml            # Development methodology (hybrid)
        user.yaml               # User preferences
    specs/                      # SPEC documents
  config/                       # detector_config.yaml and schema
  docs/
    architecture/               # Architecture design documents
    api/                        # API documentation
    guides/                     # Development and deployment guides
    references/                 # Reference documents
    testing/                    # Test plans and reports
  fpga/                         # FPGA RTL source (SystemVerilog)
    rtl/                        # RTL source files
    tb/                         # Testbench files
    constraints/                # XDC constraint files
    ip/                         # Vivado IP catalog projects
    sim/                        # Simulation scripts
  fw/                           # SoC firmware (C)
    src/                        # Source files
    tests/                      # Unit tests
    toolchain/                  # Cross-compilation toolchain files
  sdk/                          # Host SDK (.NET C#)
    XrayDetector.Sdk/           # SDK library
    XrayDetector.Sdk.Tests/     # SDK unit tests
  tools/                        # Development tools
    Common.Dto/                 # Shared interfaces and DTOs
    PanelSimulator/             # Panel pixel simulator
    FpgaSimulator/              # FPGA golden reference simulator
    McuSimulator/               # SoC firmware simulator
    HostSimulator/              # Host SDK simulator
    ParameterExtractor/         # PDF parameter extraction GUI
    CodeGenerator/              # Skeleton code generator
    ConfigConverter/            # Config format converter
    IntegrationRunner/          # Integration test runner
    GUI.Application/            # Unified WPF GUI
```

### 3.3 Configuration Files

The project uses `detector_config.yaml` as the single source of truth:

```bash
# View the main configuration
cat config/detector_config.yaml
```

Key configuration sections:
- `panel`: Resolution, bit depth, pixel pitch
- `fpga`: Timing parameters, line buffer, CSI-2, SPI
- `controller`: SoC platform, Ethernet settings
- `host`: Storage format, display settings

### 3.4 Git Configuration

```bash
# Configure git for the project
git config user.name "Your Name"
git config user.email "your.email@example.com"

# Verify branch naming convention
git checkout -b feat/your-feature-name
```

**Commit Message Format**:
```
<type>(<scope>): <subject>

<body>

🗿 MoAI <email@mo.ai.kr>
```

Types: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`
Scopes: `fpga`, `fw`, `sdk`, `tools`, `config`, `docs`

---

## 4. .NET Development Environment

### 4.1 Install .NET 8.0 SDK

**Windows**:
```powershell
# Using winget
winget install Microsoft.DotNet.SDK.8

# Verify installation
dotnet --version
# Expected: 8.0.x
```

**Linux (Ubuntu 22.04+)**:
```bash
# Add Microsoft package repository
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Install .NET SDK
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0

# Verify
dotnet --version
```

### 4.2 Verify .NET Installation

```bash
# Check SDK version
dotnet --list-sdks

# Check runtime
dotnet --list-runtimes

# Create and run a test project
dotnet new console -o /tmp/test-dotnet
dotnet run --project /tmp/test-dotnet
# Expected: "Hello, World!"
```

### 4.3 Build All Simulators

```bash
# Navigate to project root
cd system-emul-sim

# Restore NuGet packages
dotnet restore

# Build all projects
dotnet build

# Run all unit tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### 4.4 IDE Setup: Visual Studio 2022

1. Open `system-emul-sim.sln` (if solution file exists) or the project folder
2. Install required extensions:
   - **C# Dev Kit** (VS Code) or built-in C# support (Visual Studio)
   - **EditorConfig** support (for consistent formatting)
3. Configure code analysis:
   - Enable nullable reference types
   - Enable implicit usings

### 4.5 IDE Setup: VS Code

```bash
# Install recommended extensions
code --install-extension ms-dotnettools.csharp
code --install-extension ms-dotnettools.csdevkit
code --install-extension editorconfig.editorconfig
code --install-extension ms-dotnettools.vscode-dotnet-runtime
```

Create `.vscode/settings.json` in the project root:
```json
{
    "dotnet.defaultSolution": "system-emul-sim.sln",
    "editor.formatOnSave": true,
    "omnisharp.enableRoslynAnalyzers": true,
    "omnisharp.enableEditorConfigSupport": true,
    "files.exclude": {
        "**/bin": true,
        "**/obj": true
    }
}
```

---

## 5. FPGA Development Environment

### 5.1 Install AMD Vivado

**System Requirements**:
- OS: Windows 10/11 or Ubuntu 22.04 LTS
- Disk: 80+ GB for full installation
- RAM: 16+ GB
- License: HL Design Edition (required for MIPI CSI-2 TX IP)

**Installation Steps**:

1. Download AMD Vivado from the AMD website
2. Run the installer:
   ```bash
   # Linux
   chmod +x Xilinx_Unified_*.bin
   ./Xilinx_Unified_*.bin

   # Windows: Run the .exe installer
   ```
3. Select **Vivado HL Design Edition**
4. Select target device families:
   - **Artix-7** (required)
   - Deselect all other families to save disk space
5. Install to default location or custom path

### 5.2 Vivado License Setup

The project uses **AMD MIPI CSI-2 TX Subsystem IP v3.1**, which requires a Vivado HL Design Edition license.

```bash
# Linux: Set license file location
export XILINX_LOCAL_USER_DATA=no
export XILINXD_LICENSE_FILE=/path/to/license.lic

# Add to .bashrc for persistence
echo 'export XILINXD_LICENSE_FILE=/path/to/license.lic' >> ~/.bashrc
```

**Windows**: Set via Vivado License Manager (Help > Manage License).

### 5.3 Vivado Environment Setup

```bash
# Linux: Source Vivado settings
source /opt/Xilinx/Vivado/2023.2/settings64.sh

# Verify Vivado installation
vivado -version
# Expected: Vivado v2023.2 (or later)

# Add to .bashrc for persistence
echo 'source /opt/Xilinx/Vivado/2023.2/settings64.sh' >> ~/.bashrc
```

### 5.4 Create Vivado Project

```bash
cd system-emul-sim/fpga

# If a project creation script exists:
vivado -mode batch -source scripts/create_project.tcl

# Or create manually:
vivado -mode gui
# File > New Project > RTL Project
# Target device: xc7a35tfgg484-2
# Add sources from fpga/rtl/
# Add constraints from fpga/constraints/
```

### 5.5 FPGA Target Device Configuration

| Parameter | Value |
|-----------|-------|
| Family | Artix-7 |
| Device | XC7A35T |
| Package | FGG484 |
| Speed Grade | -2 |
| Part Number | `xc7a35tfgg484-2` |

### 5.6 Add MIPI CSI-2 TX IP

1. Open Vivado IP Catalog (Window > IP Catalog)
2. Search for "MIPI CSI-2 TX Subsystem"
3. Configure:
   - Number of Lanes: 4
   - Lane Speed: 1000 Mbps (initial conservative)
   - Data Type: RAW16
   - Virtual Channel: VC0
4. Generate IP output products
5. Instantiate in top-level RTL

### 5.7 Simulation Setup

```bash
# Run simulation using xsim
cd fpga/sim

# Compile testbench
xvlog -sv ../rtl/*.sv ../tb/*.sv

# Elaborate
xelab -debug all -top tb_top -snapshot tb_snap

# Run simulation
xsim tb_snap -runall

# Or use the Vivado GUI simulator
vivado -mode gui
# Flow > Run Simulation > Run Behavioral Simulation
```

---

## 6. SoC Firmware Development Environment

### 6.1 Cross-Compilation Toolchain

The SoC firmware targets NXP i.MX8M Plus (ARM Cortex-A53). A cross-compilation toolchain is required.

**Option A: Yocto SDK (Recommended)**

```bash
# Install Yocto SDK for i.MX8M Plus
# (Obtain SDK installer from NXP BSP or build from Yocto)
chmod +x fsl-imx-xwayland-glibc-x86_64-meta-toolchain-cortexa53-crypto-toolchain-5.15-kirkstone.sh
./fsl-imx-xwayland-glibc-x86_64-meta-toolchain-cortexa53-crypto-toolchain-5.15-kirkstone.sh

# Source the environment
source /opt/fsl-imx-xwayland/5.15-kirkstone/environment-setup-cortexa53-crypto-poky-linux

# Verify cross-compiler
$CC --version
# Expected: aarch64-poky-linux-gcc (GCC) 11.x or later
```

**Option B: Linaro Toolchain**

```bash
# Download Linaro AArch64 toolchain
wget https://releases.linaro.org/components/toolchain/binaries/latest-7/aarch64-linux-gnu/gcc-linaro-7.5.0-2019.12-x86_64_aarch64-linux-gnu.tar.xz
tar xf gcc-linaro-7.5.0-2019.12-x86_64_aarch64-linux-gnu.tar.xz
export PATH=$PWD/gcc-linaro-7.5.0-2019.12-x86_64_aarch64-linux-gnu/bin:$PATH

# Verify
aarch64-linux-gnu-gcc --version
```

**Option C: Docker-based Build (Reproducible)**

```bash
# Build using Docker container
docker build -t xray-fw-build -f fw/Dockerfile .
docker run -v $(pwd)/fw:/workspace xray-fw-build make
```

### 6.2 Install CMake

```bash
# Ubuntu
sudo apt-get install -y cmake

# Verify
cmake --version
# Required: 3.20+
```

### 6.3 Build Firmware

```bash
cd system-emul-sim/fw

# Source cross-compiler environment
source /opt/fsl-imx-xwayland/5.15-kirkstone/environment-setup-cortexa53-crypto-poky-linux

# Create build directory
mkdir -p build && cd build

# Configure with cross-compilation toolchain
cmake -DCMAKE_TOOLCHAIN_FILE=../toolchain/imx8mp-toolchain.cmake ..

# Build
make -j$(nproc)

# The output binaries:
# - detector_daemon
# - detector_cli
```

### 6.4 Firmware Unit Tests

Unit tests run on the host (x86) using CMocka or Unity framework:

```bash
cd fw/build

# Configure for host testing (no cross-compilation)
cmake -DCMAKE_BUILD_TYPE=Debug -DBUILD_TESTS=ON ..
make -j$(nproc)

# Run tests
ctest --output-on-failure
```

### 6.5 Deploy to SoC

```bash
# Deploy firmware to SoC over SSH
SoC_IP=192.168.1.100

scp build/detector_daemon root@${SoC_IP}:/usr/bin/
scp build/detector_cli root@${SoC_IP}:/usr/bin/
scp ../config/detector_config.yaml root@${SoC_IP}:/etc/detector/

# Restart service
ssh root@${SoC_IP} "systemctl restart detector"
```

---

## 7. Host SDK Development Environment

### 7.1 SDK Project Setup

The Host SDK is a .NET 8.0 C# library:

```bash
cd system-emul-sim/sdk

# Restore packages
dotnet restore

# Build SDK
dotnet build XrayDetector.Sdk/XrayDetector.Sdk.csproj

# Run tests
dotnet test XrayDetector.Sdk.Tests/XrayDetector.Sdk.Tests.csproj
```

### 7.2 NuGet Package Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| LibTiff.NET | 2.4.6+ | TIFF file read/write |
| System.IO.Pipelines | 8.0.0 | High-performance I/O |
| fo-dicom | 5.0+ | DICOM format (optional) |
| xUnit | 2.6+ | Test framework |
| FluentAssertions | 6.12+ | Test assertions |
| coverlet.collector | 6.0+ | Code coverage |

### 7.3 Running SDK Integration Tests

```bash
# Run with a simulator backend
cd system-emul-sim

# Start HostSimulator (provides mock detector)
dotnet run --project tools/HostSimulator -- --mode server

# In another terminal, run SDK integration tests
dotnet test sdk/XrayDetector.Sdk.Tests/ --filter "Category=Integration"
```

---

## 8. Tools and Simulator Environment

### 8.1 Build All Tools

```bash
cd system-emul-sim

# Build all projects in the solution
dotnet build

# Or build individual projects
dotnet build tools/Common.Dto/
dotnet build tools/PanelSimulator/
dotnet build tools/FpgaSimulator/
dotnet build tools/McuSimulator/
dotnet build tools/HostSimulator/
dotnet build tools/IntegrationRunner/
dotnet build tools/ConfigConverter/
dotnet build tools/CodeGenerator/
```

### 8.2 Run Simulators

```bash
# Run a single simulator
dotnet run --project tools/PanelSimulator -- --config config/detector_config.yaml

# Run integration test suite
dotnet run --project tools/IntegrationRunner -- --scenario IT-01

# Run all integration tests
dotnet run --project tools/IntegrationRunner -- --all
```

### 8.3 GUI Application (Windows Only)

The GUI.Application and ParameterExtractor use WPF and require Windows:

```bash
# Build GUI
dotnet build tools/GUI.Application/

# Run GUI
dotnet run --project tools/GUI.Application/

# Build ParameterExtractor
dotnet build tools/ParameterExtractor/
```

---

## 9. Development Methodology Setup

### 9.1 Hybrid Development Mode

The project uses Hybrid development methodology (configured in `.moai/config/sections/quality.yaml`):

| Code Type | Methodology | Workflow |
|-----------|------------|---------|
| New code (Simulators, SDK, Tools) | TDD | RED-GREEN-REFACTOR |
| Existing code (FPGA RTL, FW HAL) | DDD | ANALYZE-PRESERVE-IMPROVE |

### 9.2 TDD Workflow (New Code)

1. **RED**: Write a failing test
   ```csharp
   [Fact]
   public void PanelSimulator_CounterPattern_GeneratesSequentialPixels()
   {
       var sim = new PanelSimulator(rows: 4, cols: 4, bitDepth: 16, pattern: "counter");
       var frame = sim.GenerateFrame();
       Assert.Equal(0, frame.GetPixel(0, 0));
       Assert.Equal(1, frame.GetPixel(0, 1));
       Assert.Equal(4, frame.GetPixel(1, 0));
   }
   ```

2. **GREEN**: Write minimal code to pass
3. **REFACTOR**: Clean up while keeping tests green

### 9.3 DDD Workflow (Existing Code)

1. **ANALYZE**: Read existing code, identify dependencies
2. **PRESERVE**: Write characterization tests capturing current behavior
3. **IMPROVE**: Make incremental changes, verifying tests pass after each change

### 9.4 Code Coverage

```bash
# Run tests with coverage collection
dotnet test --collect:"XPlat Code Coverage"

# Generate coverage report (requires ReportGenerator)
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:coveragereport -reporttypes:Html

# Open coverage report
open coveragereport/index.html
```

**Coverage Targets**:
- RTL: Line >= 95%, Branch >= 90%, FSM 100%
- Software: 80-90% per module
- Overall: 85%+

---

## 10. Continuous Integration

### 10.1 Local CI Checks

Before committing, run the full validation suite:

```bash
# 1. Build all projects
dotnet build --configuration Release

# 2. Run all tests
dotnet test --configuration Release

# 3. Check for lint/formatting issues (if configured)
dotnet format --verify-no-changes

# 4. FPGA: Run simulation (if FPGA files changed)
cd fpga && make sim

# 5. Firmware: Run unit tests (if firmware files changed)
cd fw/build && ctest
```

### 10.2 CI Pipeline

The project uses Gitea + n8n webhooks for CI:

```
Push to Gitea
    |
    v
n8n Webhook triggered
    |
    +--> .NET Build + Test
    |     - dotnet build
    |     - dotnet test
    |     - Coverage report
    |
    +--> FPGA Lint (if RTL changed)
    |     - Vivado lint check
    |
    +--> Firmware Build (if FW changed)
          - Cross-compile check
```

---

## 11. Debugging Tools

### 11.1 .NET Debugging

```bash
# Debug with VS Code
# launch.json configuration:
{
    "version": "0.2.0",
    "configurations": [
        {
            "name": "Debug Simulator",
            "type": "coreclr",
            "request": "launch",
            "program": "${workspaceFolder}/tools/IntegrationRunner/bin/Debug/net8.0/IntegrationRunner.dll",
            "args": ["--scenario", "IT-01"],
            "cwd": "${workspaceFolder}",
            "console": "integratedTerminal"
        }
    ]
}
```

### 11.2 FPGA Debugging

- **ILA (Integrated Logic Analyzer)**: Real-time signal probing via JTAG
- **VIO (Virtual I/O)**: Runtime register access
- **xsim waveform viewer**: Simulation waveform analysis

### 11.3 Firmware Debugging

```bash
# Remote GDB debugging via JTAG or SSH
# On SoC:
gdbserver :2345 /usr/bin/detector_daemon

# On host:
aarch64-linux-gnu-gdb build/detector_daemon
(gdb) target remote ${SoC_IP}:2345
(gdb) break main
(gdb) continue
```

---

## 12. Troubleshooting

### 12.1 Common Issues

| Issue | Cause | Solution |
|-------|-------|---------|
| `dotnet build` fails with "SDK not found" | .NET SDK not installed or wrong version | Install .NET 8.0 SDK, verify with `dotnet --version` |
| Vivado license error | License file not found | Set `XILINXD_LICENSE_FILE` environment variable |
| Cross-compiler not found | Yocto SDK not sourced | Run `source /opt/fsl-imx-xwayland/.../environment-setup-*` |
| NuGet restore fails | Network or proxy issue | Check proxy settings, try `dotnet nuget locals all --clear` |
| WPF app won't start on Linux | WPF is Windows-only | Use Windows or skip GUI projects on Linux |
| FPGA synthesis out of memory | Insufficient RAM | Increase RAM to 32 GB or use cloud build |
| SPI communication fails | SoC SPI device not enabled | Check device tree overlay, verify `/dev/spidev0.0` exists |

### 12.2 Getting Help

1. Check existing documentation in `docs/` directory
2. Review architecture design documents in `docs/architecture/`
3. Consult SPEC documents in `.moai/specs/`
4. Check project plan: `X-ray_Detector_Optimal_Project_Plan.md`

---

## 13. Quick Start Summary

For developers who want to get started immediately:

```bash
# 1. Clone repository
git clone <gitea-url>/system-emul-sim.git && cd system-emul-sim

# 2. Install .NET 8.0 SDK (if not already installed)
# See Section 4.1 for platform-specific instructions

# 3. Build everything
dotnet build

# 4. Run tests
dotnet test

# 5. Run a simulator
dotnet run --project tools/IntegrationRunner -- --scenario IT-01

# 6. Start developing!
# - New code: TDD (write test first)
# - Existing code: DDD (write characterization test first)
```

---

## 14. Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (architect) | Initial development environment setup guide |

---
