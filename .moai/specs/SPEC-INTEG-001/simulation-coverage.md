# Simulation Coverage Report: SPEC-INTEG-001

---
id: SPEC-INTEG-001-SIM-COV
version: 1.0.0
status: current
created: 2026-03-01
author: abyz-lab
parent: SPEC-INTEG-001
---

## Overview

This document describes the simulation coverage for the X-ray Detector Panel System integration test suite. The system emulates the full 3-tier hardware stack (FPGA -> SoC -> Host PC) in software, enabling comprehensive integration testing without physical hardware.

The simulation stack consists of four layers that replicate the real data path from photon detection through image storage.

---

## Simulation Architecture

### Layer 1: PanelSimulator

Generates synthetic pixel data simulating the ROIC (Read-Out Integrated Circuit) output.

**Capabilities**:
- Configurable resolution: 64x64 through 3072x3072
- 16-bit pixel depth (14-bit padded to 16-bit)
- Three test patterns: Counter (sequential values), Checkerboard (alternating blocks), FlatField (uniform with optional Gaussian noise)
- Optional Gaussian noise injection with configurable standard deviation
- Defect pixel injection with configurable defect rate
- Deterministic output via seed-controlled random generation
- Implements `ISimulator` contract (Initialize, Process, GetStatus, Reset)

**Source**: `tools/PanelSimulator/src/PanelSimulator/PanelSimulator.cs`

### Layer 2: FpgaSimulator

Converts raw pixel frames into MIPI CSI-2 protocol packets, emulating the Artix-7 XC7A35T FPGA TX subsystem.

**Capabilities**:
- CSI-2 v1.3 packet generation (FrameStart, LineData, FrameEnd)
- RAW16 data type encoding on Virtual Channel 0
- CRC-16/CCITT computation per line (polynomial 0x1021, non-reflected, init 0xFFFF)
- Packet structure: one FrameStart + N LineData packets (one per row) + one FrameEnd
- 4-lane D-PHY emulation at logical level

**Source**: `tools/FpgaSimulator/src/FpgaSimulator.Core/Csi2/Csi2TxPacketGenerator.cs`

### Layer 3: McuSimulator

Receives CSI-2 packets, reassembles frames, and fragments them into UDP packets for network transmission. Emulates the NXP i.MX8M Plus SoC.

**Capabilities**:
- CSI-2 RX packet parsing with CRC-16 validation (polynomial 0x1021, non-reflected)
- Frame reassembly via `FrameReassembler` with `BitArray` tracking for received lines
- Handles missing packets and out-of-order delivery gracefully
- UDP packet fragmentation with 32-byte headers
- CRC-16/CCITT header checksum (polynomial 0x1021, non-reflected, init 0xFFFF)
- SPI register read/write emulation for configuration updates

**Source**: `tools/McuSimulator/src/McuSimulator.Core/`

### Layer 4: HostSimulator

Receives UDP packets, reassembles frames, and saves to storage. Emulates the Host PC SDK.

**Capabilities**:
- UDP packet reception and frame reassembly via `FrameReassembler`
- CRC-16/CCITT header validation (polynomial 0x1021, non-reflected)
- Frame storage to TIFF/RAW formats
- Configurable packet timeout for loss detection
- Implements `ISimulator` contract

**Source**: `tools/HostSimulator/src/HostSimulator.Core/HostSimulator.cs`

---

## Simulation Coverage Matrix

| Category | Simulated | Intentionally Excluded |
|----------|-----------|------------------------|
| **Data Path** | Panel -> FPGA -> MCU -> Host full pipeline (bit-exact verified) | LVDS/ROIC electrical interface |
| **Protocol** | SPI, CSI-2, UDP, HMAC-SHA256 | PLL clock drift, CDC timing |
| **Error Handling** | 8 error flags, packet loss, timeout, buffer overflow | Radiation effects (SEU) |
| **Performance** | 30fps throughput, p95 latency measurement | DDR4 physical layout, power consumption |
| **Peripherals** | -- | Battery monitoring, thermal management, GPIO |

### Detailed Coverage Breakdown

**Data Path Coverage**:
- Panel pixel generation with 3 test patterns (Counter, Checkerboard, FlatField)
- FPGA CSI-2 TX packetization with CRC-16 per line
- MCU CSI-2 RX parsing, frame reassembly, UDP fragmentation
- Host UDP reassembly, CRC validation, storage output
- Bit-exact verification from Panel input to Host output (IT-11)

**Protocol Coverage**:
- CSI-2: FrameStart/LineData/FrameEnd packet structure, magic number (0xD7E01234), CRC-16 validation
- UDP: 32-byte header format, sequence numbering, CRC-16/CCITT header checksum
- SPI: Register write/read-back verification (IT-03)
- HMAC-SHA256: Command authentication on port 8001, valid/invalid/missing signature testing (IT-06)

**Error Handling Coverage**:
- Packet loss injection and recovery (IT-08)
- Frame buffer overflow with 4-frame ring buffer (IT-05)
- Timeout detection and recovery
- Invalid HMAC rejection (100% rejection rate required)
- Sequence engine error state entry and recovery (IT-07)
- Missing packet detection via BitArray bitmap
- Out-of-order packet reassembly
- CRC mismatch detection

---

## Test Coverage Summary

### Test Distribution by Project

| Project | Tests Passing | Tests Skipped | Total |
|---------|--------------|---------------|-------|
| PanelSimulator.Tests | 52 | 0 | 52 |
| FpgaSimulator.Tests | 81 | 0 | 81 |
| McuSimulator.Tests | 28 | 0 | 28 |
| HostSimulator.Tests | 57 | 0 | 57 |
| IntegrationTests | 169 | 4 | 173 |
| **Total** | **387** | **4** | **391** |

The 4 skipped tests are performance variance tests that exhibit non-deterministic timing behavior in CI environments. They are skipped for CI stability but can be run manually in controlled environments.

### Integration Test Scenarios (IT-01 through IT-12)

| Test ID | Description | Status |
|---------|-------------|--------|
| IT-01 | Single Frame Capture (1024x1024@15fps) | Passing |
| IT-02 | Continuous Capture 300 Frames (2048x2048@30fps) | Passing |
| IT-03 | SPI Configuration Update (10 register round-trips) | Passing |
| IT-04 | CSI-2 Protocol Validation (magic, CRC, sequencing) | Passing |
| IT-05 | Frame Buffer Overflow Recovery (4-frame ring buffer) | Passing |
| IT-06 | HMAC-SHA256 Command Authentication (valid/invalid/missing) | Passing |
| IT-07 | Sequence Engine State Machine (6-state FSM, 5 cycles) | Passing |
| IT-08 | 10GbE Packet Loss and Retransmission (0.1% loss) | Passing |
| IT-09 | Maximum Tier Stress Test (3072x3072@30fps, 60s) | Passing |
| IT-10 | End-to-End Latency Measurement (p95 < 50ms) | Passing |
| IT-11 | Full 4-Layer Pipeline Bit-Exact Verification (256x256 to 2048x2048) | Passing |
| IT-12 | Module Isolation and ISimulator Contract Verification | Passing |

---

## Layer Boundary Verification Points

The integration test suite validates data integrity at each layer boundary using checkpoint-based verification in the `SimulatorPipelineBuilder`.

### Boundary 1: Panel -> FPGA

**Verification**: Pixel data generated by PanelSimulator is correctly packetized into CSI-2 packets by FpgaSimulator.

- Packet count equals rows + 2 (1 FrameStart + N LineData + 1 FrameEnd)
- First packet is FrameStart, last packet is FrameEnd
- Each LineData packet contains exactly `cols` pixels (width of the frame)
- CRC-16 computed per line matches stored value

**Test coverage**: IT-04, IT-11

### Boundary 2: FPGA -> MCU

**Verification**: CSI-2 packets are correctly parsed and reassembled into a complete frame by McuSimulator's `FrameReassembler`.

- Reassembled frame dimensions match original (rows x cols)
- `ReceivedLineBitmap` (BitArray) shows all lines received (all bits set)
- Pixel values in reassembled 2D array match original Panel output pixel-for-pixel
- Missing line detection works correctly (zeros for unreceived lines)

**Test coverage**: IT-04, IT-05, IT-08, IT-11

### Boundary 3: MCU -> Host

**Verification**: UDP packets produced by McuSimulator are correctly reassembled by HostSimulator into the final frame.

- Host output frame dimensions match MCU reassembled frame
- Pixel values in Host output match original Panel input (bit-exact)
- UDP header CRC-16/CCITT validates correctly
- Packet sequence numbering is continuous

**Test coverage**: IT-01, IT-02, IT-08, IT-11

### Boundary 4: Host -> Storage

**Verification**: HostSimulator produces valid FrameData output suitable for TIFF/RAW storage.

- Output FrameData contains correct Width, Height, and Pixels array
- Pixel array length equals width * height
- Storage format preserves 16-bit pixel depth without truncation

**Test coverage**: IT-01, IT-11, IT-12

---

## Key Bugs Found and Fixed

### 1. CRC-16 Algorithm Mismatch Between Layers

**Problem**: The SDK `Crc16CcittValidator` uses a reflected polynomial (0x8408) for frame header validation, while the FPGA, MCU, and Host simulators use a non-reflected polynomial (0x1021) for CSI-2 line CRC and UDP header CRC. Early development had inconsistent CRC computation causing validation failures at layer boundaries.

**Root cause**: Two distinct CRC-16/CCITT variants are used in the system for different purposes:
- **Frame header CRC** (SDK): Reflected polynomial 0x8408, init 0xFFFF, XorOut 0x0000 -- used for the 32-byte ethernet protocol header at bytes 28-29
- **CSI-2 line CRC and UDP header CRC** (FPGA/MCU/Host): Non-reflected polynomial 0x1021, init 0xFFFF -- used for per-line data integrity and UDP packet headers

**Resolution**: Both CRC variants are now explicitly documented and implemented with separate classes:
- `Crc16CcittValidator` (SDK): Reflected, for frame headers
- `Crc16Ccitt` (HostSimulator), inline CRC methods (FpgaSimulator, McuSimulator): Non-reflected, for protocol packets
- `PacketFactory` (IntegrationTests): Provides both `CalculateCrc16Ccitt()` (non-reflected) and `CalculateReflectedCrc16()` (reflected) for test packet generation

**Impact**: IT-04 now explicitly validates CRC computation against known test vectors. IT-11 confirms bit-exact CRC match across all 4 layers.

### 2. BitArray vs. ulong for Line Tracking

**Problem**: The initial `FrameReassembler` implementation used a `ulong` bitmask to track received lines, limiting maximum frame height to 64 rows.

**Root cause**: A `ulong` provides only 64 bits, which is insufficient for production frame sizes (1024, 2048, or 3072 rows).

**Resolution**: Replaced `ulong` with `System.Collections.BitArray` in `McuSimulator.Core.Frame.FrameReassembler`. The `ReceivedLineBitmap` property is now a `BitArray` initialized to the frame's row count, supporting arbitrary frame dimensions.

**Impact**: IT-11 verifies that BitArray correctly tracks all 2048 rows in standard resolution frames. The `ReceivedLineBitmap.Length` assertion confirms the bitmap covers the full frame height.

---

## Architectural Decisions

### ADR-001: BitArray Over ulong for Line Tracking

**Context**: The `FrameReassembler` needs to track which lines have been received during CSI-2 frame reassembly to detect missing packets.

**Decision**: Use `System.Collections.BitArray` instead of `ulong` bitmask.

**Rationale**:
- `ulong` limits tracking to 64 lines maximum, which is only suitable for small test frames
- Production frames are 1024 to 3072 rows, far exceeding 64-bit capacity
- `BitArray` supports arbitrary sizes with O(1) set/get operations
- `BitArray` is part of the .NET standard library with no external dependencies
- Memory overhead is minimal: 3072 bits = 384 bytes per frame, negligible compared to pixel data (18+ MB)

**Consequences**: Slight API change from `ulong` to `BitArray` requires callers to use indexer syntax (`bitmap[lineNum]`) instead of bitwise operations. All integration tests updated to use BitArray assertions.

### ADR-002: Non-Reflected CRC-16 for Protocol Packets

**Context**: The system uses CRC-16/CCITT checksums in two contexts: frame header validation (SDK) and protocol packet validation (CSI-2 lines, UDP headers).

**Decision**: Use non-reflected CRC-16/CCITT (polynomial 0x1021) for all protocol-level checksums. Retain reflected CRC-16 (polynomial 0x8408) only for the SDK frame header validator which follows an existing specification.

**Rationale**:
- Non-reflected CRC-16/CCITT (XMODEM variant) is the standard for MIPI CSI-2 protocol implementations
- Matches hardware FPGA CRC module behavior (shift-left implementation is simpler in RTL)
- The SDK frame header CRC uses the reflected variant to maintain compatibility with the existing ethernet-protocol.md specification
- Keeping both variants explicit prevents accidental substitution

**Consequences**: Two CRC implementations coexist in the codebase. The `PacketFactory` test helper exposes both variants to prevent confusion during test development. Documentation explicitly states which variant applies at each layer boundary.

---

## What Is Not Simulated

The following aspects of the physical system are intentionally excluded from simulation:

| Excluded Item | Reason |
|---------------|--------|
| LVDS/ROIC electrical interface | Analog signal characteristics are not relevant to digital data path validation |
| PLL clock drift and CDC timing | Clock domain crossing effects require hardware measurement |
| Radiation effects (SEU) | Single-event upsets require radiation testing facilities |
| DDR4 physical layout | Memory timing and signal integrity are board-level concerns |
| Power consumption | Requires physical power measurement infrastructure |
| Battery monitoring | Peripheral subsystem not part of data acquisition path |
| Thermal management | Requires physical temperature sensors and cooling system |
| GPIO control lines | Low-level hardware control not exercised in software simulation |
| Network jitter and reordering | Partially covered by packet loss tests (IT-08); full network simulation is out of scope |
| Real-time OS scheduling | Linux kernel scheduling behavior differs between simulation and target SoC |

These exclusions are acceptable because the simulation focuses on **data path correctness and protocol compliance**, which are the primary risk areas for software integration. Hardware-specific behaviors will be validated during the physical hardware integration phase (post-M3).

---

## Traceability

| Document | Relationship |
|----------|--------------|
| SPEC-INTEG-001 | Parent specification defining IT-01 through IT-10 requirements |
| SPEC-INTEG-001/plan.md | Implementation plan with task breakdown and file ownership |
| SPEC-SDK-001 | Host SDK API contract (IDetectorClient, Frame, ScanStatus) |
| SPEC-FPGA-001 | FPGA RTL and CSI-2 protocol specification |
| SPEC-FW-001 | SoC firmware sequence engine FSM specification |
| docs/api/ethernet-protocol.md | 32-byte UDP header format, CRC field layout |
| docs/api/csi2-protocol.md | CSI-2 packet format and state machine |

---

**END OF DOCUMENT**
