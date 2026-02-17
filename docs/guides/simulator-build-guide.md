# Software Simulator Build and Validation Guide

**Document Version**: 1.0.0
**Status**: Reviewed
**Last Updated**: 2026-02-17

---

## Prerequisites

### Required Software

| Software | Version | Purpose |
|----------|---------|---------|
| .NET SDK | 8.0 LTS | Build and run all simulators |
| Visual Studio 2022 / VS Code | Latest | IDE or editor |
| ReportGenerator | Latest | HTML coverage report |

```bash
dotnet --version
# Expected: 8.0.x

# Install ReportGenerator (one-time)
dotnet tool install -g dotnet-reportgenerator-globaltool
```

---

## Setup

### Simulator Architecture Overview

The simulator suite models the complete X-ray detector data acquisition pipeline in software. Four simulators together form a chain that mirrors the physical hardware layers:

```
PanelSimulator  -->  FpgaSimulator  -->  McuSimulator  -->  HostSimulator
  (pixel data)        (FPGA logic)        (SoC layer)        (Host SDK)
       |                    |                   |                  |
  FrameData[]          Csi2Packet[]        UdpPacket[]      TIFF/RAW output
```

| Simulator | Purpose | Maps to Hardware |
|-----------|---------|-----------------|
| PanelSimulator | Generate pixel frames with configurable noise and defect patterns | X-ray detector panel ROIC |
| FpgaSimulator | Model FPGA registers, scan FSM, line buffer, CSI-2 TX | Xilinx Artix-7 FPGA |
| McuSimulator | Model SoC CSI-2 RX, SPI master, Ethernet TX | Variscite VAR-SOM-MX8M-PLUS |
| HostSimulator | Model UDP frame reception, reassembly, TIFF/RAW storage | Host PC SDK |

A shared library `Common.Dto` provides the `ISimulator` interface and all shared data transfer objects (DTOs).

### Project Structure

```
tools/
├── Common.Dto/           # Shared interfaces and DTOs (ISimulator, FrameData, etc.)
├── PanelSimulator/       # Panel pixel data generator
├── FpgaSimulator/        # FPGA golden reference model
├── McuSimulator/         # SoC firmware model
├── HostSimulator/        # Host SDK model
├── IntegrationRunner/    # Integration test runner (IT-01 to IT-10)
├── ParameterExtractor/   # PDF parameter extraction GUI (Windows)
├── CodeGenerator/        # Skeleton code generator
├── ConfigConverter/      # YAML/JSON config converter
└── GUI.Application/      # Unified WPF GUI (Windows)
```

---

## Build

### Build All Simulators

```bash
cd tools

# Restore NuGet packages for all projects
dotnet restore

# Build all projects in Release configuration
dotnet build --configuration Release

# Run all unit tests with coverage
dotnet test --filter "Category=Unit" --collect:"XPlat Code Coverage"
```

### Build Individual Simulators

```bash
# Build only PanelSimulator
dotnet build PanelSimulator/PanelSimulator.csproj --configuration Release

# Build the full integration runner
dotnet build IntegrationRunner/IntegrationRunner.csproj --configuration Release
```

---

## Running Simulators

### PanelSimulator

Generates synthetic pixel frames with configurable resolution, frame rate, and noise model:

```bash
dotnet run --project PanelSimulator -- \
    --rows 1024 --cols 1024 --fps 15 --seed 42
```

Available options:

| Option | Default | Description |
|--------|---------|-------------|
| `--rows` | 1024 | Frame row count |
| `--cols` | 1024 | Frame column count |
| `--fps` | 15 | Target frames per second |
| `--seed` | 0 | Random seed for deterministic output |
| `--noise` | gaussian | Noise model: `gaussian`, `salt_pepper`, `none` |
| `--bit-depth` | 16 | Pixel bit depth: 14 or 16 |

### FpgaSimulator with SPI Register Dump

Runs the FPGA golden reference model and dumps the full SPI register map to stdout:

```bash
dotnet run --project FpgaSimulator -- --dump-registers
```

The register dump shows all 256 registers with names, addresses, and current values. This is the primary tool for verifying that FPGA RTL register maps match the software model.

To run the FpgaSimulator with custom configuration:

```bash
dotnet run --project FpgaSimulator -- \
    --config /path/to/config/detector_config.yaml \
    --lane-speed 400
```

### Full Pipeline Simulation

Run a complete end-to-end pipeline simulation for integration test scenario IT-01:

```bash
dotnet run --project IntegrationRunner -- --scenario IT-01
```

Available integration test scenarios:

| Scenario | Description |
|----------|-------------|
| IT-01 | 1024x1024, 14-bit, 15fps — minimal configuration baseline |
| IT-02 | 2048x2048, 16-bit, 15fps — medium-A configuration |
| IT-03 | SPI register write and read-back |
| IT-04 | CSI-2 packet format validation |
| IT-05 | Frame reassembly with packet loss injection |
| IT-06 | TIFF and RAW storage format verification |
| IT-07 | Multi-frame continuous acquisition |
| IT-08 | Error injection and protection logic |
| IT-09 | Calibration mode frame processing |
| IT-10 | Full pipeline golden reference comparison |

---

## Configuration

### Configure via detector_config.yaml

The simulators read from `config/detector_config.yaml`, which is the single source of truth for all system parameters:

```yaml
simulator:
  mode: deterministic      # deterministic | random
  seed: 42                 # Random seed for reproducible frames
  noise: gaussian          # Noise model: gaussian | salt_pepper | none
  defect_rate: 0.001       # Fraction of defective pixels (0.0 to 1.0)

panel:
  rows: 1024
  cols: 1024
  bit_depth: 16
  fps: 15

fpga:
  lane_speed_mbps: 400     # CSI-2 D-PHY lane speed: 400 (stable) or 800 (in debug)
  num_lanes: 4

host:
  storage_format: tiff     # tiff | raw
  output_dir: ./output
```

Override individual parameters on the command line without modifying the YAML file:

```bash
dotnet run --project PanelSimulator -- \
    --config config/detector_config.yaml \
    --rows 2048 --cols 2048 --fps 15
```

---

## Performance Benchmarks

Expected performance on a recommended development machine (8-core, 32 GB RAM):

| Simulator | Scenario | Target Performance |
|-----------|----------|--------------------|
| PanelSimulator | 1024x1024 @ 15fps | >= 2x real-time (>= 30fps generation) |
| FpgaSimulator | SPI transactions | <= 10 µs simulated latency per transaction |
| McuSimulator | CSI-2 RX processing | Frame reassembly <= 2ms for 1024x1024 |
| HostSimulator | Frame reassembly | <= 5ms for 1024x1024 frame |
| IntegrationRunner IT-01 | End-to-end pipeline | Completes in < 30s |

Run the built-in benchmark:

```bash
dotnet run --project IntegrationRunner -- --scenario IT-01 --benchmark
```

---

## ILA Signal Debugging in FpgaSimulator

The FpgaSimulator supports simulated ILA (Integrated Logic Analyzer) signal capture, mirroring the hardware ILA debug infrastructure:

```csharp
// Enable ILA capture with 1024-sample depth
fpgaSim.EnableILA(captureDepth: 1024);

// Set trigger condition: capture on LineValid rising edge
fpgaSim.ILATrigger = ILATrigger.LineValid;

// Run simulation and capture ILA data
fpgaSim.RunFrames(count: 1);

// Dump captured ILA signals to CSV
fpgaSim.DumpILA("ila_capture.csv");
```

The ILA capture CSV contains columns for: `timestamp_ns`, `fsm_state`, `pixel_data`, `line_valid`, `frame_valid`, `error_flags`, `csi2_tvalid`, `csi2_tready`.

---

## Generating Test Reports

Generate a structured JSON report for an integration test scenario:

```bash
dotnet run --project IntegrationRunner -- \
    --scenario IT-01 \
    --report ./reports/IT-01-result.json
```

The JSON report includes:
- Pass/fail status for each assertion
- Timing measurements (frame generation, pipeline latency)
- Error counts and types
- Golden reference comparison results

Generate reports for all scenarios:

```bash
for scenario in IT-01 IT-02 IT-03 IT-04 IT-05 IT-06 IT-07 IT-08 IT-09 IT-10; do
    dotnet run --project IntegrationRunner -- \
        --scenario $scenario \
        --report ./reports/${scenario}-result.json
    echo "$scenario exit code: $?"
done
```

---

## Test

### Unit Tests

Run all unit tests with coverage:

```bash
cd tools

dotnet test --filter "Category=Unit" \
    --collect:"XPlat Code Coverage" \
    --results-directory ./coverage

reportgenerator \
    -reports:coverage/**/coverage.cobertura.xml \
    -targetdir:coverage/report \
    -reporttypes:Html
```

Coverage target for simulator projects: >= 85% line coverage.

### Deterministic Output Verification

Verify that simulations with the same seed produce identical output across runs:

```bash
# Run 1
dotnet run --project PanelSimulator -- \
    --rows 64 --cols 64 --fps 1 --seed 42 \
    --output /tmp/frame_run1.bin

# Run 2
dotnet run --project PanelSimulator -- \
    --rows 64 --cols 64 --fps 1 --seed 42 \
    --output /tmp/frame_run2.bin

# Should produce no output (files are identical)
diff /tmp/frame_run1.bin /tmp/frame_run2.bin && echo "PASS: Deterministic output verified"
```

### Golden Reference Comparison

The FpgaSimulator produces reference output that the FPGA RTL simulation is expected to match bit-exactly:

```bash
# Generate FpgaSimulator golden reference output
dotnet run --project FpgaSimulator -- \
    --config config/detector_config.yaml \
    --output fpga/sim/golden_reference.bin \
    --scenario FV-06

# Compare against RTL simulation output (produced by Vivado xsim)
diff fpga/sim/golden_reference.bin fpga/sim/rtl_output.bin \
    && echo "PASS: RTL matches golden reference" \
    || echo "FAIL: RTL output differs from golden reference"
```

---

## Troubleshooting

### Build Issues

| Issue | Cause | Solution |
|-------|-------|---------|
| Build fails with missing `Common.Dto` reference | `Common.Dto` not built first | Build `Common.Dto` before other projects: `dotnet build Common.Dto/` |
| `dotnet restore` fails | NuGet feed unreachable | Check network; clear cache with `dotnet nuget locals all --clear` |

### Runtime Issues

| Issue | Cause | Solution |
|-------|-------|---------|
| `FileNotFoundException: detector_config.yaml` | Config file path wrong | Pass `--config /absolute/path/to/config/detector_config.yaml` |
| Simulator output differs between runs | Non-deterministic mode | Set `--seed 42` or set `simulator.mode: deterministic` in YAML |
| Pipeline simulation hangs | Deadlock between simulator stages | Check for unmatched producer/consumer thread counts in IntegrationRunner |
| IT test fails with packet loss | Simulated network drops | Check `fpga.lane_speed_mbps` setting; reduce from 800 to 400 for stability |

---

## Common Errors

| Error | Context | Meaning | Fix |
|-------|---------|---------|-----|
| `System.InvalidOperationException: Simulator not initialized` | Runtime | `Initialize()` not called before `Run()` | Call `simulator.Initialize(config)` before `simulator.Run()` |
| `ArgumentOutOfRangeException: rows must be <= 3072` | PanelSimulator | Resolution exceeds maximum | Use rows/cols <= 3072 |
| `CRC mismatch in packet` | FpgaSimulator | Data corruption in simulated CSI-2 packet | Check `fpga.lane_speed_mbps`; use 400 for stable simulation |
| `Frame timeout: no frame received in 5000ms` | IntegrationRunner | Pipeline stalled | Check that all simulator instances are running; review logs |
| xUnit test fails: `Assert.Equal failed` | Unit test | Implementation does not match expected | Review test expectations against `detector_config.yaml` parameters |

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent | Complete software simulator build and validation guide |
