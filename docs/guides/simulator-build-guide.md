# Simulator Build Guide

**Project**: X-ray Detector Panel System
**Framework**: .NET 8.0 LTS, C# 12
**Version**: 1.0.0
**Last Updated**: 2026-02-17

---

## 1. Overview

The simulator suite models the complete X-ray Detector Panel data acquisition pipeline in software. Simulators enable early software development, algorithm validation, and serve as golden reference models for hardware verification -- all without requiring physical hardware.

### 1.1 Simulator Suite

| Simulator | Purpose | Pipeline Position |
|-----------|---------|------------------|
| **PanelSimulator** | Generate pixel data with noise and defects | Source (input) |
| **FpgaSimulator** | Model FPGA registers, FSM, line buffer, CSI-2 TX | Layer 1 |
| **McuSimulator** | Model SoC CSI-2 RX, SPI master, Ethernet TX | Layer 2 |
| **HostSimulator** | Model Host UDP receive, frame reassembly, storage | Layer 3 |
| **Common.Dto** | Shared interfaces (`ISimulator`) and DTOs | Shared library |

### 1.2 Data Flow Through Simulators

```
PanelSimulator (pixel matrix)
    |
    | FrameData (rows x cols x 16-bit)
    v
FpgaSimulator (SPI regs, FSM, line buffer, CSI-2 TX)
    |
    | Csi2Packet[] (Frame Start, Line Data + CRC, Frame End)
    v
McuSimulator (CSI-2 RX, SPI master, ETH TX)
    |
    | UdpPacket[] (32-byte header + 8192-byte payload)
    v
HostSimulator (frame reassembly, storage)
    |
    | Completed Frame (TIFF/RAW output)
    v
Output Files
```

---

## 2. Prerequisites

| Software | Version | Purpose |
|----------|---------|---------|
| .NET SDK | 8.0 LTS | Build and run |
| xUnit | 2.6+ | Test framework |
| coverlet | 6.0+ | Code coverage |
| FluentAssertions | 6.12+ | Test assertions |

```bash
# Verify .NET SDK
dotnet --version
# Expected: 8.0.x
```

---

## 3. Project Structure

```
tools/
  Common.Dto/
    Common.Dto.csproj
    ISimulator.cs               # Shared interface for all simulators
    Interfaces/
      ICodeGenerator.cs
    Models/
      FrameData.cs              # 2D pixel frame
      LineData.cs               # Single line of pixels
      Csi2Packet.cs             # CSI-2 packet representation
      UdpPacket.cs              # UDP packet with frame header
      SpiTransaction.cs         # SPI register read/write
      SimulatorStatus.cs        # Runtime status
    Enums/
      ScanMode.cs               # Single, Continuous, Calibration
      FsmState.cs               # IDLE, INTEGRATE, READOUT, etc.
      ErrorCode.cs              # Error flag codes

  PanelSimulator/
    PanelSimulator.csproj
    PanelSimulator.cs           # Main simulator class
    NoiseModel/
      GaussianNoise.cs          # Gaussian noise generator
      INoiseModel.cs            # Noise model interface
    DefectModel/
      PixelDefectInjector.cs    # Dead/hot pixel injection
    Patterns/
      CounterPattern.cs         # Sequential counter pattern
      CheckerboardPattern.cs    # Alternating max/zero pattern

  PanelSimulator.Tests/
    PanelSimulator.Tests.csproj
    PanelSimulatorTests.cs
    NoiseModelTests.cs
    PatternTests.cs

  FpgaSimulator/
    FpgaSimulator.csproj
    FpgaSimulator.cs            # Main simulator class
    RegisterFile.cs             # Complete FPGA register map
    PanelScanFsm.cs             # FSM state machine model
    LineBuffer.cs               # Ping-Pong buffer model
    Csi2TxEngine.cs             # CSI-2 packet generator
    Crc16Engine.cs              # CRC-16 computation

  FpgaSimulator.Tests/
    FpgaSimulator.Tests.csproj
    RegisterFileTests.cs
    PanelScanFsmTests.cs
    LineBufferTests.cs
    Csi2TxEngineTests.cs
    Crc16EngineTests.cs

  McuSimulator/
    McuSimulator.csproj
    McuSimulator.cs             # Main simulator class
    Csi2RxDecoder.cs            # CSI-2 packet parser
    SpiMaster.cs                # SPI transaction generator
    EthernetTx.cs               # UDP packet builder
    SequenceEngine.cs           # Scan sequence control
    FrameBufferManager.cs       # Buffer lifecycle

  McuSimulator.Tests/
    McuSimulator.Tests.csproj

  HostSimulator/
    HostSimulator.csproj
    HostSimulator.cs            # Main simulator class
    PacketReceiver.cs           # UDP packet intake
    FrameReassembler.cs         # Packet-to-frame assembly
    ImageWriter/
      TiffWriter.cs             # 16-bit TIFF output
      RawWriter.cs              # Binary RAW output

  HostSimulator.Tests/
    HostSimulator.Tests.csproj

  IntegrationRunner/
    IntegrationRunner.csproj
    Program.cs                  # CLI entry point
    Scenarios/
      IT01_SingleFrameMinTier.cs
      IT02_SingleFrameTargetTier.cs
      IT03_ContinuousCapture.cs
      IT04_DataIntegrity.cs
      ...
```

---

## 4. Build All Simulators

### 4.1 Build from Project Root

```bash
cd system-emul-sim

# Restore all NuGet packages
dotnet restore

# Build all projects
dotnet build

# Build in Release mode
dotnet build -c Release
```

### 4.2 Build Individual Simulators

```bash
# Build Common.Dto first (dependency root)
dotnet build tools/Common.Dto/Common.Dto.csproj

# Build simulators
dotnet build tools/PanelSimulator/PanelSimulator.csproj
dotnet build tools/FpgaSimulator/FpgaSimulator.csproj
dotnet build tools/McuSimulator/McuSimulator.csproj
dotnet build tools/HostSimulator/HostSimulator.csproj

# Build IntegrationRunner
dotnet build tools/IntegrationRunner/IntegrationRunner.csproj
```

### 4.3 Build Order (Dependencies)

```
Common.Dto (no dependencies)
    ^
    |--- PanelSimulator
    |--- FpgaSimulator
    |--- McuSimulator
    |--- HostSimulator
    |       ^
    |       |
    |--- IntegrationRunner (depends on all simulators)
```

Build `Common.Dto` first, then simulators in any order, then `IntegrationRunner` last.

---

## 5. Run Tests

### 5.1 Run All Simulator Tests

```bash
# Run all tests in the solution
dotnet test

# Run all tests with verbose output
dotnet test -v detailed
```

### 5.2 Run Individual Simulator Tests

```bash
# PanelSimulator tests
dotnet test tools/PanelSimulator.Tests/

# FpgaSimulator tests
dotnet test tools/FpgaSimulator.Tests/

# McuSimulator tests
dotnet test tools/McuSimulator.Tests/

# HostSimulator tests
dotnet test tools/HostSimulator.Tests/
```

### 5.3 Run Specific Test Categories

```bash
# Run only counter pattern tests
dotnet test --filter "FullyQualifiedName~CounterPattern"

# Run only FSM tests
dotnet test --filter "FullyQualifiedName~PanelScanFsm"

# Run only CRC tests
dotnet test --filter "FullyQualifiedName~Crc16"

# Run tests by trait
dotnet test --filter "Category=GoldenReference"
```

### 5.4 Code Coverage

```bash
# Collect coverage for all simulators
dotnet test --collect:"XPlat Code Coverage" --results-directory TestResults

# Generate report
dotnet tool install -g dotnet-reportgenerator-globaltool 2>/dev/null || true
reportgenerator \
    -reports:"TestResults/*/coverage.cobertura.xml" \
    -targetdir:TestResults/CoverageReport \
    -reporttypes:Html

# View report
start TestResults/CoverageReport/index.html  # Windows
xdg-open TestResults/CoverageReport/index.html  # Linux
```

**Coverage Target**: 80-90% per simulator module (per quality.yaml)

---

## 6. Run Simulators

### 6.1 PanelSimulator

```bash
# Generate a single frame with counter pattern
dotnet run --project tools/PanelSimulator -- \
    --config config/detector_config.yaml \
    --pattern counter \
    --output output/panel_frame.bin

# Generate frame with Gaussian noise
dotnet run --project tools/PanelSimulator -- \
    --config config/detector_config.yaml \
    --pattern noise \
    --noise-stddev 100 \
    --seed 42 \
    --output output/panel_frame_noisy.bin

# Generate checkerboard pattern
dotnet run --project tools/PanelSimulator -- \
    --config config/detector_config.yaml \
    --pattern checkerboard \
    --output output/panel_frame_checker.bin
```

### 6.2 FpgaSimulator

```bash
# Process a frame through FPGA simulation
dotnet run --project tools/FpgaSimulator -- \
    --config config/detector_config.yaml \
    --input output/panel_frame.bin \
    --output output/fpga_csi2_packets.bin

# Run FSM simulation only
dotnet run --project tools/FpgaSimulator -- \
    --config config/detector_config.yaml \
    --mode fsm-only \
    --verbose
```

### 6.3 McuSimulator

```bash
# Process CSI-2 packets to UDP packets
dotnet run --project tools/McuSimulator -- \
    --config config/detector_config.yaml \
    --input output/fpga_csi2_packets.bin \
    --output output/mcu_udp_packets.bin
```

### 6.4 HostSimulator

```bash
# Reassemble frame from UDP packets
dotnet run --project tools/HostSimulator -- \
    --config config/detector_config.yaml \
    --input output/mcu_udp_packets.bin \
    --output output/host_frame.tiff \
    --format tiff

# Save as RAW with JSON sidecar
dotnet run --project tools/HostSimulator -- \
    --config config/detector_config.yaml \
    --input output/mcu_udp_packets.bin \
    --output output/host_frame.raw \
    --format raw
```

### 6.5 Full Pipeline (Manual)

```bash
# Run complete pipeline: Panel -> FPGA -> MCU -> Host
mkdir -p output

dotnet run --project tools/PanelSimulator -- \
    --config config/detector_config.yaml \
    --pattern counter --output output/step1_panel.bin

dotnet run --project tools/FpgaSimulator -- \
    --config config/detector_config.yaml \
    --input output/step1_panel.bin --output output/step2_fpga.bin

dotnet run --project tools/McuSimulator -- \
    --config config/detector_config.yaml \
    --input output/step2_fpga.bin --output output/step3_mcu.bin

dotnet run --project tools/HostSimulator -- \
    --config config/detector_config.yaml \
    --input output/step3_mcu.bin --output output/step4_host.tiff --format tiff
```

---

## 7. IntegrationRunner

### 7.1 Run Integration Test Scenarios

The IntegrationRunner automates the full pipeline and validates data integrity:

```bash
# Run single scenario
dotnet run --project tools/IntegrationRunner -- --scenario IT-01
dotnet run --project tools/IntegrationRunner -- --scenario IT-02

# Run all scenarios
dotnet run --project tools/IntegrationRunner -- --all

# Run with verbose output
dotnet run --project tools/IntegrationRunner -- --scenario IT-01 --verbose

# Generate JSON report
dotnet run --project tools/IntegrationRunner -- --all --report output/integration-report.json
```

### 7.2 Integration Test Scenarios

| Scenario | Description | Tier | Frames |
|----------|-------------|------|--------|
| IT-01 | Single frame, minimum tier | 1024x1024, 14-bit | 1 |
| IT-02 | Single frame, target tier | 3072x3072, 16-bit | 1 |
| IT-03 | Continuous capture (100 frames) | 2048x2048, 16-bit | 100 |
| IT-04 | Data integrity (counter pattern) | All tiers | 10 |
| IT-05 | CRC-16 validation | 2048x2048, 16-bit | 10 |
| IT-06 | Frame drop simulation | 2048x2048, 16-bit | 1000 |
| IT-07 | Out-of-order packet handling | 2048x2048, 16-bit | 10 |
| IT-08 | Error injection and recovery | 1024x1024, 14-bit | 10 |
| IT-09 | TIFF/RAW storage verification | 2048x2048, 16-bit | 5 |
| IT-10 | Performance benchmark | All tiers | 100 |

### 7.3 Expected Output

```
========================================
X-ray Detector Integration Test Runner
========================================

[IT-01] Single Frame, Minimum Tier
  Config: 1024x1024, 14-bit, counter pattern
  Pipeline: Panel -> FPGA -> MCU -> Host
  Validation: Bit-exact match
  Result: PASS (0 bit errors, 45 ms)

[IT-04] Data Integrity, All Tiers
  Config: 1024/2048/3072, 14/16-bit
  Pipeline: Full pipeline x 10 frames each
  Validation: Input vs output comparison
  Result: PASS (0 bit errors across all tiers)

========================================
Summary: 10/10 PASS, 0 FAIL
Execution time: 12.3 seconds
========================================
```

---

## 8. Configuration

### 8.1 detector_config.yaml

All simulators read configuration from `config/detector_config.yaml`:

```yaml
panel:
  rows: 2048                  # Image height (pixels)
  cols: 2048                  # Image width (pixels)
  pixel_pitch_um: 150         # Pixel pitch (micrometers)
  bit_depth: 16               # Pixel bit depth (14 or 16)

fpga:
  timing:
    gate_on_us: 1000          # X-ray gate ON duration (microseconds)
    gate_off_us: 200          # Gate OFF duration
    roic_settle_us: 50        # ROIC settling time
    adc_conv_us: 10           # ADC conversion time
  line_buffer:
    depth_lines: 2            # Ping-pong (always 2)
    bram_width_bits: 16       # Data width
  data_interface:
    primary: csi2
    csi2:
      lane_count: 4           # Number of D-PHY data lanes
      lane_speed_mbps: 800    # Per-lane speed (Mbps)
      data_type: RAW16        # MIPI data type
      virtual_channel: 0      # Virtual channel ID
  spi:
    clock_hz: 50000000        # SPI clock frequency
    mode: 0                   # SPI mode (CPOL=0, CPHA=0)

controller:
  platform: imx8mp
  ethernet:
    speed: 10gbe
    protocol: udp
    port: 8000
    control_port: 8001
    payload_size: 8192        # UDP payload (bytes)

host:
  storage:
    format: tiff              # Default: tiff, raw
    path: "./frames"
  display:
    fps: 15
    color_map: gray
  network:
    receive_threads: 2
    packet_timeout_ms: 2000   # Frame completion timeout
```

### 8.2 Simulation-Specific Configuration

Simulators accept additional CLI parameters:

| Parameter | Default | Description |
|-----------|---------|-------------|
| `--config` | `config/detector_config.yaml` | Configuration file path |
| `--seed` | 0 (random) | Random seed for deterministic output |
| `--verbose` | false | Enable verbose logging |
| `--output` | stdout | Output file path |
| `--format` | binary | Output format (binary, json, tiff, raw) |

---

## 9. Golden Reference Validation

### 9.1 FpgaSimulator as Golden Reference

The FpgaSimulator serves as the bit-exact golden reference for FPGA RTL verification:

```bash
# 1. Generate golden reference output
dotnet run --project tools/FpgaSimulator -- \
    --config config/detector_config.yaml \
    --input test_data/counter_1024x1024.bin \
    --output golden/fpga_output.bin \
    --seed 0

# 2. Compare with RTL simulation output
# (RTL simulation outputs are generated by Vivado xsim)
diff golden/fpga_output.bin rtl_sim/fpga_output.bin
# Expected: Files identical (bit-exact match)
```

### 9.2 Register-Level Comparison

```bash
# Generate register trace
dotnet run --project tools/FpgaSimulator -- \
    --config config/detector_config.yaml \
    --trace-registers golden/register_trace.json

# Compare with RTL register dump
# (RTL testbench generates register trace in same format)
```

---

## 10. Troubleshooting

### 10.1 Build Issues

| Issue | Cause | Solution |
|-------|-------|---------|
| "Common.Dto not found" | Missing dependency | Build Common.Dto first: `dotnet build tools/Common.Dto/` |
| "TargetFramework mismatch" | Wrong .NET version | Ensure all projects target `net8.0` |
| NuGet restore timeout | Slow network | Add local NuGet cache: `dotnet nuget add source /path/to/cache` |

### 10.2 Test Issues

| Issue | Cause | Solution |
|-------|-------|---------|
| Determinism failure | Missing seed | Always pass `--seed` for reproducible tests |
| CRC-16 mismatch | Different polynomial | Verify CRC-16/CCITT (polynomial 0x8408) |
| Frame size mismatch | Config not loaded | Check `detector_config.yaml` path is correct |
| Coverage below target | Missing test cases | Add tests for edge cases (empty frame, max size, overflow) |

### 10.3 Runtime Issues

| Issue | Cause | Solution |
|-------|-------|---------|
| Out of memory | Large frame at max tier | Increase heap: `dotnet run -- --config ... -e DOTNET_GCHeapHardLimit=0x20000000` |
| Slow simulation | Unoptimized build | Use Release mode: `dotnet run -c Release` |
| File format error | Wrong output format | Verify `--format` parameter matches expected consumer |

---

## 11. Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (architect) | Initial simulator build guide |

---
