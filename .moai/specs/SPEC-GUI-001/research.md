# SPEC-GUI-001 Research Report
# X-ray Detector Integrated GUI Simulator — Reality Audit

**Date**: 2026-03-16
**Purpose**: Deep codebase analysis to inform realistic MVP implementation plan
**Methodology**: Parallel agent exploration of GUI, simulator, and SPEC layers

---

## 1. Critical Discovery: The Simulator-GUI Disconnect

### What Was Claimed
- M5-UI Complete (100%)
- 4-layer pipeline integrated in GUI
- 1,423 tests passing with 85%+ coverage

### What Is Actually True

**The simulators ARE fully implemented and work correctly:**
- `SimulatorPipeline.ProcessFrame()` executes real 4-layer chain
- Panel → FPGA CSI-2 (ECC/CRC) → MCU CSI-2 RX + UDP TX → Host Reassembly
- IT-11 integration test validates bit-exact round-trip
- All protocol logic is genuine (not mocked)

**The GUI is a demo facade with fake data:**
- `App.xaml.cs` hardcodes `new SimulatedDetectorClient()`
- `PipelineDetectorClient` class exists but is NEVER instantiated anywhere
- `SimulatedDetectorClient.GenerateFrame()` produces 256×256 synthetic data (diagonal gradient + Gaussian noise + pulsing spot)
- Parameter changes in "Simulator Control" tab DO NOT affect output

---

## 2. Tab-by-Tab GUI Reality

| Tab | Claimed | Actual | % Real |
|-----|---------|--------|--------|
| 1. Status Dashboard | Real-time connection stats | ✅ StatusTimer fires every 500ms, reads from SimulatedDetectorClient | 95% |
| 2. Frame Preview | 16-bit grayscale from PanelSimulator | ⚠️ Renders frames, but source is fake synthetic generator, NOT PanelSimulator | 50% |
| 3. Parameter Extraction | PDF → parameters → simulation | ⚠️ PDF loading works, only 6/~30 params mapped, params NOT fed to pipeline | 40% |
| 4. Simulator Control | Parameter control affecting simulation | ❌ UI-only; Start/Stop buttons are empty `() => { }` lambdas; UpdateConfig() never called | 15% |
| 5. Pipeline Status | Real-time 4-layer stats | ❌ `OnPollingTick()` is empty with `// TODO` comment; all values hardcoded zeros | 5% |
| 6. Scenario Runner | Run IT-01~IT-19 | ❌ 3 hardcoded scenarios; `ExecuteScenarioAsync()` is a 50ms delay animation; always returns Passed=true | 5% |

**Overall: ~45% of UI works, but 0% of actual 4-layer pipeline is connected**

---

## 3. Simulator Layer Reality

### Layer 1: PanelSimulator ✅ GENUINE
**Location**: `tools/PanelSimulator/src/PanelSimulator/`
- Real physics: scintillator model (CsI(Tl)), X-ray quantum efficiency
- Noise models: Gaussian (Box-Muller), Poisson shot noise
- Defect mapping: dead pixels, bright pixels configurable
- Calibration: GainOffsetMap, DriftModel, LagModel
- Test patterns: Counter, Checkerboard, Flat field
- Output: `FrameData { FrameNumber, Height, Width, Pixels: ushort[] }`

### Layer 2: FpgaSimulator ✅ GENUINE
**Location**: `tools/FpgaSimulator/src/FpgaSimulator.Core/`
- Real MIPI CSI-2 v1.3 packet generation
- ECC: Hamming(6,24) over 24-bit headers
- CRC-16: XMODEM polynomial (0x1021)
- Real FSM: Idle→Integrate→Readout→FrameDone
- Line buffer: dual-bank (A/B), overflow detection
- Protection logic: watchdog timer, error latch flags

### Layer 3: McuSimulator ✅ GENUINE
**Location**: `tools/McuSimulator/src/McuSimulator.Core/`
- Real CSI-2 RX parsing via `Csi2RxPacketParser`
- Frame reassembly with BitArray tracking
- Real UDP fragmentation per ethernet-protocol.md spec
- 32-byte frame header: Magic=0xD7E01234, CRC-16/CCITT at offset 28

### Layer 4: HostSimulator ✅ GENUINE
**Location**: `tools/HostSimulator/src/HostSimulator.Core/`
- Real UDP packet reassembly with timeout
- Out-of-order packet reordering
- TIFF and RAW 16-bit image storage

### IntegrationRunner.Core — The Hidden Heart
**Location**: `tools/IntegrationRunner/src/IntegrationRunner.Core/SimulatorPipeline.cs`
- `ProcessFrame()` (lines 156-249): Executes complete 4-layer chain
- NetworkChannel: configurable packet loss, reorder, corruption
- Statistics tracking: frames processed/completed/failed
- **THIS IS WHAT THE GUI NEEDS TO USE — but doesn't**

---

## 4. The Single Most Impactful Change

**File**: `tools/GUI.Application/src/GUI.Application/App.xaml.cs`

**Current (broken):**
```csharp
// Line ~32: Always creates fake client
_simulatedClient = new SimulatedDetectorClient();
```

**Required (real pipeline):**
```csharp
// Create real 4-layer pipeline
var panelConfig = new PanelConfig { Rows = 256, Cols = 256, Pattern = TestPattern.Counter };
var pipeline = new SimulatorPipeline(panelConfig, ...);
_pipelineClient = new PipelineDetectorClient(pipeline);
```

This one change would transform the GUI from demo to reality.

**Supporting changes needed:**
1. `MainViewModel`: Call `_detectorClient.UpdateConfig()` when params change
2. `PipelineStatusViewModel.OnPollingTick()`: Read from `_pipeline.GetStatistics()`
3. `ScenarioRunner`: Execute real IntegrationTests scenarios
4. `ParameterExtractorService`: Map all parameters (not just 6)

---

## 5. SPEC Gap Analysis

### What Already Has SPECs
All 17 SPEC documents exist with approved acceptance criteria:
- Architecture (SPEC-ARCH-001)
- FPGA RTL (SPEC-FPGA-001)
- SoC Firmware (SPEC-FW-001)
- Simulator Suite (SPEC-SIM-001)
- Host SDK (SPEC-SDK-001)
- Tools (SPEC-TOOLS-001)
- GUI (SPEC-UI-001, SPEC-HELP-001)
- E2E infrastructure (SPEC-E2E-001~004)

### What Has NO SPEC: GUI-Pipeline Integration
There is NO SPEC for:
- Connecting PipelineDetectorClient to GUI application
- Wiring parameter changes to simulation output
- Real scenario execution from GUI
- Full parameter extraction mapping (PDF → all params)
- Frame export from GUI

**This is the gap SPEC-GUI-001 fills.**

---

## 6. Technical Constraints

- **Platform**: WPF (.NET 8.0-windows), C# 12
- **Pipeline assembly**: `IntegrationRunner.Core.dll` (already exists)
- **Frame rate**: SimulatedDetectorClient runs at 10fps; real pipeline rate depends on frame size
- **Min frame size**: 256×256 (development), Max: 3072×3072 (production)
- **Threading**: `SimulatorPipeline.ProcessFrame()` is synchronous; need Task.Run wrapper for UI
- **Existing test infrastructure**: IT-11 validates bit-exact pipeline (reuse in scenario runner)
- **Config format**: `detector_config.yaml` is single source of truth

---

## 7. Implementation Risk Assessment

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| PipelineDetectorClient has bugs | Low | Medium | IT-11 already validates pipeline; just needs wiring |
| Thread safety in GUI pipeline | Medium | High | SimulatorPipeline has internal locking already |
| Real pipeline too slow for 30fps | Medium | Medium | Start with 256×256; add async processing |
| Parameter extraction coverage | Low | Medium | Map incrementally; show "not extracted" for missing |
| Scenario runner test isolation | Low | Low | Run each IT test in separate Task |

---

## 8. Archived Documents

The following documents have been superseded and archived to `.moai/archive/`:
- `X-ray_Detector_Optimal_Project_Plan_ARCHIVED_20260316.md` — Original 28-week plan (W1-W28)
- `WBS_ARCHIVED_20260316.md` — Original WBS with team roles

**Reason for archival**: The original plan was hardware-first with SW simulation as a tool.
The revised plan (SPEC-GUI-001) is SW-simulation-first with incremental user-facing delivery.
