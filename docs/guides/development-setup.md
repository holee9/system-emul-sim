# Development Environment Setup Guide

**Document Version**: 1.0.0
**Status**: Draft
**Last Updated**: 2026-02-17

---

## Prerequisites

### Hardware Requirements

| Component | Minimum | Recommended |
|-----------|---------|-------------|
| CPU | 4-core x86-64 | 8-core x86-64 |
| RAM | 16 GB | 32 GB |
| Storage | 150 GB free SSD | 500 GB NVMe SSD |
| Network | 1 GbE | 10 GbE |
| Display | 1920x1080 | Dual monitor |

### Evaluation Boards (Hardware Verification)

| Board | Purpose | Status |
|-------|---------|--------|
| Xilinx Artix-7 XC7A35T-FGG484 | FPGA RTL development and PoC | Available |
| NXP i.MX8M Plus EVK (Variscite VAR-SOM-MX8M-PLUS) | SoC firmware development | Available |

### Required Software Versions

| Software | Minimum Version | Purpose |
|----------|----------------|---------|
| Git | 2.40+ | Version control |
| AMD Vivado | 2023.2+ HL Design Edition | FPGA synthesis and simulation |
| ModelSim/Questa | 2021.4+ | RTL simulation |
| .NET SDK | 8.0 LTS | Host SDK, simulators, tools |
| Visual Studio 2022 | 17.8+ | C# IDE with WPF designer |
| VS Code | Latest | Lightweight editor (Linux/Windows) |
| GCC ARM cross-compiler | 13.x (arm-linux-gnueabihf) | SoC firmware cross-compilation |
| CMake | 3.20+ | Firmware build system |

---

## Setup

### Windows Development Environment

#### Install .NET 8.0 SDK

```powershell
winget install Microsoft.DotNet.SDK.8
```

Verify installation:

```powershell
dotnet --version
# Expected: 8.0.x
dotnet --list-sdks
```

#### Install AMD Vivado 2023.2

1. Download Vivado HL Design Edition from the AMD website.
2. Run the installer with administrator privileges.
3. Select **Vivado HL Design Edition** (required for MIPI CSI-2 TX Subsystem IP).
4. Select only **Artix-7** device family to save disk space (~80 GB required).
5. Default installation path: `C:\Xilinx\Vivado\2023.2`.

Set environment variable after installation:

```powershell
[Environment]::SetEnvironmentVariable("VIVADO_DIR", "C:\Xilinx\Vivado\2023.2", "User")
[Environment]::SetEnvironmentVariable("DOTNET_ROOT", "C:\Program Files\dotnet", "User")
```

Verify Vivado access:

```cmd
vivado -version
# Expected: Vivado v2023.2 (64-bit)
```

#### Configure Vivado License

The MIPI CSI-2 TX Subsystem IP requires an HL Design Edition license.

1. Open **Vivado License Manager**: Help > Manage License.
2. Add your license file (`.lic`) or point to a network license server.
3. Verify the `mipi_csi2_tx_subsystem` IP is licensed.

Set the license file via environment variable (alternative method):

```powershell
[Environment]::SetEnvironmentVariable("XILINXD_LICENSE_FILE", "C:\Xilinx\license.lic", "User")
```

#### Install Git with Credential Manager

```powershell
winget install Git.Git
```

Configure credential manager for Gitea:

```bash
git config --global credential.helper manager
git config --global user.name "Your Name"
git config --global user.email "your.email@example.com"
```

### Linux Development Environment (Ubuntu 22.04 LTS)

Ubuntu 22.04 LTS is recommended for Yocto builds and firmware cross-compilation.

#### Install Essential Build Packages

```bash
sudo apt-get update
sudo apt-get install -y \
    build-essential \
    chrpath \
    diffstat \
    gawk \
    wget \
    curl \
    git \
    cmake \
    ninja-build \
    python3 \
    python3-pip \
    texinfo \
    zlib1g-dev \
    libssl-dev \
    libglib2.0-dev \
    socat \
    cpio \
    xterm \
    lzop
```

#### Install .NET 8.0 SDK on Linux

```bash
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0
```

#### Install Vivado on Linux

```bash
chmod +x Xilinx_Unified_2023.2_*.bin
./Xilinx_Unified_2023.2_*.bin
```

Source Vivado environment:

```bash
source /opt/Xilinx/Vivado/2023.2/settings64.sh

# Add to .bashrc for persistence
echo 'source /opt/Xilinx/Vivado/2023.2/settings64.sh' >> ~/.bashrc
```

#### Yocto Scarthgap 5.0 Setup (for SoC Firmware)

Fetch the Yocto Scarthgap base:

```bash
git clone -b scarthgap git://git.yoctoproject.org/poky
```

Set up the Variscite BSP layer:

```bash
git clone -b scarthgap https://github.com/varigit/meta-variscite-bsp.git
git clone -b scarthgap https://github.com/varigit/meta-variscite-imx.git
```

Source the Yocto build environment:

```bash
cd poky
source oe-init-build-env build-imx8mp
```

Add Variscite layers to `conf/bblayers.conf`:

```
BBLAYERS += "/path/to/meta-variscite-bsp"
BBLAYERS += "/path/to/meta-variscite-imx"
```

Alternative: Use Docker for reproducible Yocto builds:

```bash
docker pull crops/poky:ubuntu-22.04
docker run --rm -it -v $(pwd):/workdir crops/poky:ubuntu-22.04 \
    --workdir=/workdir bash
```

---

## Repository Clone and Setup

```bash
# Clone the Yocto BSP repository
git clone http://100.126.59.10:7001/drake.lee/yocto_scarthgap.git

# Clone the simulator and SDK repository
git clone https://github.com/holee9/system-emul-sim.git
cd system-emul-sim
```

Verify the repository structure:

```
system-emul-sim/
  config/                   # detector_config.yaml (single source of truth)
  docs/                     # Architecture docs, guides, API docs
  fpga/                     # RTL source (SystemVerilog), testbenches, constraints
  fw/                       # SoC firmware (C/C++)
  sdk/                      # Host SDK (C#, .NET 8.0)
  tools/                    # Simulators, GUI, code generators
```

Restore .NET packages and verify build:

```bash
cd system-emul-sim
dotnet restore
dotnet build
```

---

## VS Code Workspace Configuration

Create `.vscode/settings.json` in the project root:

```json
{
    "dotnet.defaultSolution": "system-emul-sim.sln",
    "editor.formatOnSave": true,
    "editor.rulers": [120],
    "omnisharp.enableRoslynAnalyzers": true,
    "omnisharp.enableEditorConfigSupport": true,
    "files.exclude": {
        "**/bin": true,
        "**/obj": true,
        "**/.vivado": true
    },
    "files.associations": {
        "*.sv": "systemverilog",
        "*.svh": "systemverilog",
        "*.xdc": "tcl",
        "*.tcl": "tcl"
    },
    "[csharp]": {
        "editor.defaultFormatter": "ms-dotnettools.csharp"
    }
}
```

Install recommended VS Code extensions:

```bash
code --install-extension ms-dotnettools.csharp
code --install-extension ms-dotnettools.csdevkit
code --install-extension editorconfig.editorconfig
code --install-extension ms-dotnettools.vscode-dotnet-runtime
code --install-extension mshr-h.veriloghdl
```

---

## Build

### Build All .NET Projects

```bash
cd system-emul-sim

# Restore dependencies
dotnet restore

# Build in Release configuration
dotnet build --configuration Release

# Run all unit tests
dotnet test --configuration Release
```

### FPGA Environment Verification

```bash
# Linux
source /opt/Xilinx/Vivado/2023.2/settings64.sh
vivado -version
# Expected: Vivado v2023.2

# Test ModelSim/Questa
vsim -version
# Expected: ModelSim or QuestaSim version info
```

### Cross-Compiler Verification (ARM)

```bash
# After sourcing Yocto SDK or installing arm-linux-gnueabihf toolchain
arm-linux-gnueabihf-gcc --version
# Expected: arm-linux-gnueabihf-gcc (GCC) 13.x
```

---

## Test

Run unit tests for all .NET projects:

```bash
dotnet test --collect:"XPlat Code Coverage" \
    --results-directory ./coverage
```

Run a quick integration smoke test:

```bash
dotnet run --project tools/IntegrationRunner -- --scenario IT-01
```

Verify FPGA simulation compiles:

```bash
# Using xsim (requires Vivado environment sourced)
cd fpga/sim
xvlog -sv ../rtl/*.sv ../tb/*.sv 2>&1 | grep -E "ERROR|WARNING" | head -20
```

---

## Troubleshooting

### Common Setup Issues

| Issue | Cause | Solution |
|-------|-------|---------|
| `dotnet: command not found` | .NET SDK not installed | Install .NET 8.0 SDK, verify PATH |
| `dotnet --version` shows 6.x or 7.x | Wrong SDK version active | Install 8.0, use `global.json` to pin version |
| `vivado: command not found` | Vivado not sourced | Run `source /opt/Xilinx/Vivado/2023.2/settings64.sh` |
| NuGet restore fails | Network or proxy issue | Check proxy settings, clear cache with `dotnet nuget locals all --clear` |
| WPF projects fail on Linux | WPF is Windows-only | Build only cross-platform projects on Linux |
| FPGA synthesis out of memory | Insufficient RAM | Increase RAM to 32 GB; synthesis uses 8-16 GB |
| `arm-linux-gnueabihf-gcc not found` | Cross-compiler not installed | Source Yocto SDK or install `gcc-arm-linux-gnueabihf` |

### Fixing .NET SDK Version

If the system has multiple .NET versions, pin to 8.0 via `global.json`:

```json
{
    "sdk": {
        "version": "8.0.0",
        "rollForward": "latestMinor"
    }
}
```

### Vivado License Error

If Vivado reports a license error for MIPI CSI-2 TX IP:

```bash
# Check license status
lmutil lmstat -a -c $XILINXD_LICENSE_FILE

# Common fix: set the correct license path
export XILINXD_LICENSE_FILE=/path/to/Xilinx.lic
```

---

## Common Errors

| Error Message | Root Cause | Fix |
|---------------|-----------|-----|
| `MSBUILD: error MSB1003` | Solution file not found | Run commands from project root directory |
| `error CS0234: The type or namespace does not exist` | Missing NuGet package | Run `dotnet restore` |
| `Unable to find package Microsoft.NET.Test.Sdk` | NuGet feed unreachable | Check network/proxy configuration |
| `ERROR: [Vivado 12-508] No license` | HL Design Edition license missing | Add license file via Vivado License Manager |
| `xvlog: command not found` | Vivado environment not sourced | Source `/opt/Xilinx/Vivado/2023.2/settings64.sh` |
| `arm-linux-gnueabihf-gcc: not found` | Cross-compiler path not in PATH | Source Yocto SDK environment script |

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent | Complete developer setup guide |
