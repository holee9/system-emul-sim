# Research: SPEC-UI-001 Integrated Emulator GUI
Date: 2026-03-11
Analyst: MoAI Deep Research (Explore agents x2 + Synthesis)

---

## Project Purpose (Confirmed by User)

This project's goal is to verify an X-ray detector panel system entirely in software
without physical hardware. Each hardware layer is implemented as a software emulator,
and the GUI must serve as the integrated control center for the entire emulation system.

The 4-layer emulation pipeline: Panel → FPGA/CSI-2 → MCU/UDP → Host

---

## Existing Architecture

### 4-Layer Simulator Pipeline

All simulators implement `ISimulator` interface (Common.Dto/Interfaces/ISimulator.cs):
```csharp
public interface ISimulator {
    void Initialize(object config);
    object Process(object input);
    void Reset();
    string GetStatus();
}
```

**Data flow (current: file-based, target: in-memory):**
```
Panel.Process(PanelConfig) → FrameData (.raw)
  → FpgaSim.Process(FrameData) → Csi2Packets (.csi2)
    → McuSim.Process(Csi2Packets) → UdpPackets (.udp) [via FrameBufferManager]
      → NetworkChannel.Process(UdpPackets) → UdpPackets (with faults)
        → HostSim.Process(UdpPackets) → FrameData (.raw)
```

**`SimulatorPipeline`** in IntegrationRunner.Core orchestrates all layers.
Key file: `tools/IntegrationRunner/src/IntegrationRunner.Core/SimulatorPipeline.cs`

### Panel Simulator Parameters
- CLI: rows, cols, kvp (40-150), mas, noise (none/gaussian/composite), seed, fidelity (Low/Medium/High), config file
- Physics: PanelConfig { Rows, Cols, BitDepth, TestPattern, NoiseModel, NoiseStdDev, DefectRate, Seed }
- Test patterns: Counter, Checkerboard, FlatField
- Noise models: None, Gaussian, Poisson, Uniform (Box-Muller in PanelSimulator.cs:140-145)
- Output: XFRA binary (magic "XFRA", version, rows, cols, ushort[] pixels LE)

### FPGA/CSI-2 Simulator Parameters
- CLI: input (.raw), mode (single/continuous/calibration), protection (on/off), output (.csi2), seed, fidelity
- Config: FpgaConfig { Csi2Lanes, Csi2DataRateMbps, LineBufferDepth }
- Packet types: FrameStart, FrameEnd, LineStart, LineEnd, LineData
- Data types: Raw8/10/12/14/16 (Raw16 default for X-ray)
- CRC-16, ECC Hamming error correction per packet
- Output: XCS2 binary (magic "XCS2", count, [dataType+VC+len+payload]*N)

### MCU Simulator Parameters
- CLI: input (.csi2), buffers (default 4), command (start_scan/stop_scan/status), output (.udp)
- Config: SocConfig { EthernetPort, UdpPort, TcpPort, FrameBufferCount }
- FrameBufferManager: 4-buffer ring with oldest-drop policy
  - Buffer states: Free, Filling, Ready, Sending
  - Oldest-drop: prefer dropping Ready over Sending, never drop Filling
  - File: tools/McuSimulator/src/McuSimulator.Core/Buffer/FrameBufferManager.cs
- UDP frame header: 32 bytes (magic 0xD7E01234, version, frame ID, packet sequence, total packets, CRC-16)
- Max UDP payload: 8192 bytes per packet
- Output: XUDP binary

### Network Channel (MCU→Host)
- Config: NetworkChannelConfig { PacketLossRate(0-1), ReorderRate(0-1), MinDelayMs, MaxDelayMs, CorruptionRate(0-1), Seed }
- File: tools/IntegrationRunner/src/IntegrationRunner.Core/Network/NetworkChannelConfig.cs
- NetworkChannel.cs: applies loss → corruption → reordering in sequence
- Already used in SimulatorPipeline

### Host Simulator Parameters
- CLI: input (.udp), timeout (default 1000ms), output (.raw)
- Config: HostConfig { IpAddress, PacketTimeoutMs, ReceiveThreads }
- FrameReassembler: handles out-of-order packets
- Output formats: TIFF, RAW (XFRA format)

### Full Detector Configuration YAML
```yaml
panel: { rows, cols, bit_depth, pixel_pitch_um }
fpga: { csi2_lanes, csi2_data_rate_mbps, line_buffer_depth }
soc: { ethernet_port, udp_port, tcp_port, frame_buffer_count }
host: { ip_address, packet_timeout_ms, receive_threads }
simulation: { mode, seed, test_pattern, noise_stddev, max_frames }
```
File: tools/IntegrationRunner/src/IntegrationRunner.Core/Models/DetectorConfig.cs

---

## Existing GUI.Application (Current State)

### What's Implemented (SPEC-TOOLS-001 coverage)
- MVVM foundation: ObservableObject, RelayCommand, RelayCommand<T>
- MainViewModel: orchestrates IDetectorClient lifecycle
- StatusViewModel: connection state, acquisition state, throughput (Gbps), temperature, frames received/dropped
- FramePreviewViewModel: 16-bit→8-bit via WindowLevelMapper, DisplayPixels property
- MainWindow.xaml: 6 tabs defined (Status Dashboard, Frame Preview, Configuration)
- WriteableBitmap rendering in MainWindow.xaml.cs (just implemented this session)
- SimulatedDetectorClient: generates animated frames at 10fps (just implemented this session)
- 40 unit tests in GUI.Application.Tests

### What's Missing for Integrated Emulator GUI
1. Simulator mode selection UI (hardware vs pipeline emulation)
2. Panel parameter control panel (all physics parameters)
3. FPGA/CSI-2 configuration and statistics panel
4. MCU buffer state visualization and UDP config
5. Network fault injection UI
6. Pipeline orchestration (Start/Stop/Reset all 4 layers)
7. Per-layer statistics monitor
8. Scenario definition and execution UI (IT01-IT19 equivalents)
9. Configuration load/save (detector_config.yaml)
10. PipelineDetectorClient: IDetectorClient implementation wrapping SimulatorPipeline (in-memory)

---

## Key Architectural Decisions

### PipelineDetectorClient (New Core Component)
- Implements IDetectorClient (same interface as SimulatedDetectorClient, DetectorClient)
- Internally uses SimulatorPipeline from IntegrationRunner.Core
- Converts SimulatorPipeline from file-based to in-memory data flow
- Fires FrameReceived events for each processed frame
- Exposes configuration setters for each layer
- Thread-safe background loop running at configurable fps

### In-Memory Data Flow (Critical Refactoring)
Current SimulatorPipeline uses file I/O between layers.
Target: objects passed directly in memory:
```
PanelSimulator.Process(PanelConfig) → FrameData
  → Csi2TxPacketGenerator.GeneratePackets(FrameData) → List<Csi2Packet>
    → Csi2RxPacketParser.Reassemble(List<Csi2Packet>) → FrameData
      [MCU FrameBufferManager in between]
      → UdpFrameTransmitter.Fragment(FrameData) → List<UdpFramePacket>
        → NetworkChannel.Process(List<UdpFramePacket>) → List<UdpFramePacket>
          → FrameReassembler.Reassemble(List<UdpFramePacket>) → FrameData
```
This requires adding in-memory overloads to existing simulator classes.

### Scenario Execution
- Integration test scenarios (IT01-IT19) as named scenario definitions
- JSON-based scenario config: name, description, detectorConfig, networkConfig, frameCount, assertions
- ScenarioRunner service runs scenario, collects pass/fail results
- GUI shows scenario list, run button, live progress, results table

### Mode Architecture
```
GUI Mode Switch:
  [Simulated]          → SimulatedDetectorClient (current, 10fps animated frames)
  [Pipeline Emulation] → PipelineDetectorClient (new, full 4-layer)
```
Both implement IDetectorClient → existing FramePreviewViewModel unchanged.

---

## Reference Implementations Found

- `SimulatorPipeline.cs` — pipeline orchestration pattern to follow
- `SimulatedDetectorClient.cs` — IDetectorClient event-driven pattern (just created)
- `NetworkChannelConfig.cs` — fault injection configuration model
- `FrameBufferManager.cs` — buffer state machine to expose in UI
- `DetectorConfig.cs` — full configuration schema (YAML-serializable)
- `IntegrationRunner.Tests` IT01-IT19 — scenario patterns for GUI scenario runner

---

## Risks

| Risk | Level | Mitigation |
|------|-------|------------|
| SimulatorPipeline file I/O refactoring scope | High | Add in-memory overloads alongside existing file-based methods |
| Thread safety: PipelineDetectorClient + WPF UI thread | Medium | Dispatcher.Invoke pattern (already established) |
| MCU FrameBufferManager real-time polling | Low | GetStatistics() method exists, poll at 2Hz |
| IntegrationRunner.Core project reference from GUI | Medium | Add ProjectReference to GUI.Application.csproj |
| Scenario JSON schema design | Low | Model after existing DetectorConfig YAML schema |
