# Plan: Add Simulator/Emulator Section to README

## Context

사용자 피드백: README.md에서 Panel, FPGA, SoC가 **실제 하드웨어가 아닌 시뮬레이션용 에뮬레이터**로 구현되었다는 점이 명확하지 않음.

**누락된 내용**:
- 이 프로젝트는 하드웨어 없이 전체 시스템을 검증할 수 있는 **소프트웨어 시뮬레이터**를 제공
- PanelSimulator, FpgaSimulator, McuSimulator, HostSimulator의 역할과 구현 상태
- 각 시뮬레이터가 무엇을 에뮬레이션하는지에 대한 명확한 설명

## Problem Analysis

현재 README.md의 문제:
1. **시뮬레이터 개념 불명확**: "소프트웨어 시뮬레이터"라고만 언급, 구체적인 설명 없음
2. **에뮬레이터 역할 미설명**: Panel/FPGA/SoC가 에뮬레이터로 구현되었다는 점이 드러나지 않음
3. **구현 상태 불명확**: 각 시뮬레이터의 테스트 통과 여부가 테이블에 있지만, 무엇을 시뮬레이션하는지 설명 없음

## Objectives

README.md에 **Simulators & Emulators** 섹션을 추가하여:
1. 각 시뮬레이터의 목적과 역할 명확화
2. 시뮬레이션 대상 (Panel, FPGA, SoC)이 실제 하드웨어가 아님을 명시
3. 구현 상태와 테스트 커버리지 명시

## Implementation Plan

### 1. Add Simulators & Emulators Section

README.md의 "## System Architecture" 섹션 다음에 새 섹션 추가:

```markdown
## Simulators & Emulators

이 프로젝트는 **실제 하드웨어 없이 전체 시스템을 검증**할 수 있는 소프트웨어 시뮬레이터를 제공합니다.

> **중요**: Panel, FPGA, SoC는 실제 하드웨어가 아닌 **C# 기반 에뮬레이터**로 구현되었습니다.
> 이를 통해 하드웨어 없이도 전체 데이터 흐름과 프로토콜을 검증할 수 있습니다.

### Emulator Components

| Emulator | Simulates | Purpose | Test Status |
|----------|-----------|---------|-------------|
| **PanelSimulator** | X-ray Detector Panel | 픽셀 매트릭스 생성, 노이즈/결함 시뮬레이션 | 52 tests passing |
| **FpgaSimulator** | Xilinx Artix-7 FPGA | FSM, SPI Slave, Line Buffer, CSI-2 TX | 85 tests passing |
| **McuSimulator** | NXP i.MX8M Plus SoC | SPI Master, CSI-2 RX, UDP Frame TX | 35 tests passing |
| **HostSimulator** | Host PC SDK | UDP RX, Frame Reassembly, Image Storage | 36 tests passing |

### Simulator Architecture

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│  PanelSimulator │───▶│  FpgaSimulator  │───▶│  McuSimulator   │───▶│  HostSimulator  │
│                 │    │                 │    │    (SoC)        │    │    (Host PC)    │
│  Pixel Matrix   │    │  FSM + SPI      │    │  SPI Master     │    │  UDP Receiver   │
│  Noise Model    │    │  Line Buffer    │    │  CSI-2 RX       │    │  Reassembly     │
│  Test Patterns  │    │  CSI-2 TX       │    │  UDP TX         │    │  TIFF/RAW Save  │
└─────────────────┘    └─────────────────┘    └─────────────────┘    └─────────────────┘
        │                      │                      │                      │
        └──────────────────────┴──────────────────────┴──────────────────────┘
                              Common.Dto (ISimulator Interface)
```

### What Each Simulator Emulates

#### PanelSimulator (X-ray Panel Emulator)
- **픽셀 매트릭스 생성**: 2048×2048 / 3072×3072, 14/16-bit depth
- **노이즈 모델**: Gaussian noise, offset drift
- **결함 시뮬레이션**: Hot pixels, dead pixels
- **테스트 패턴**: Counter, Checkerboard, FlatField

#### FpgaSimulator (FPGA Emulator)
- **Panel Scan FSM**: Idle → Integrate → Readout → LineDone → FrameDone
- **SPI Slave**: 레지스터 read/write (0x00-0xFF)
- **Line Buffer**: Ping-Pong BRAM, overflow handling
- **CSI-2 TX**: RAW16 packet encoding, 4-lane D-PHY

#### McuSimulator (SoC Controller Emulator)
- **SPI Master**: FPGA 레지스터 제어
- **CSI-2 RX**: Packet parsing, validation
- **Frame Buffer**: DDR4 4× buffer simulation
- **UDP TX**: 10 GbE packet transmission with CRC-16

#### HostSimulator (Host PC Emulator)
- **UDP Receiver**: Packet reception, CRC validation
- **Frame Reassembly**: Out-of-order packet handling, timeout detection
- **Image Storage**: TIFF 16-bit, RAW + JSON sidecar

### Integration Test Coverage

| Test ID | Scenario | Status |
|---------|----------|--------|
| IT-01 | Full pipeline data integrity | ✅ Passing |
| IT-02 | Performance (2048×2048@30fps, 300 frames) | ✅ Passing |
| IT-03 | SPI configuration validation | ✅ Passing |
| IT-04 | CSI-2 protocol validation | ✅ Passing |
| IT-05 | Frame buffer overflow recovery | ✅ Passing |
| IT-06 | HMAC authentication | ✅ Passing |
| IT-07 | Sequence engine validation | ✅ Passing |
| IT-08 | Packet loss retransmission | ✅ Passing |
| IT-09 | Stress test (3072×3072@30fps, 60s) | ✅ Passing |
| IT-10 | Latency measurement (p95 < 50ms) | ✅ Passing |

### Total Test Statistics

| Metric | Value |
|--------|-------|
| Unit Tests | 413 tests passing |
| Integration Tests | 10 scenarios passing |
| Code Coverage | 85%+ |
| Simulator Projects | 4 (Panel, FPGA, MCU, Host) |
| Test Projects | 12 |
```

### 2. Update Current Status Section

기존 "### 현재 구현 상태 (M2-Impl + Tools)" 테이블 위에 에뮬레이터 설명 추가:

```markdown
### Simulator Implementation Status

모든 시뮬레이터는 **100% 구현 완료**되었으며, 실제 하드웨어 없이 전체 시스템을 검증할 수 있습니다.
```

---

## Critical Files

- `README.md` - 수정 대상
- `tools/PanelSimulator/` - PanelSimulator 소스
- `tools/FpgaSimulator/` - FpgaSimulator 소스
- `tools/McuSimulator/` - McuSimulator 소스
- `tools/HostSimulator/` - HostSimulator 소스
- `tools/IntegrationTests/` - IT-01~IT-10 테스트

---

## Execution Steps

1. README.md "## System Architecture" 섹션 다음에 "## Simulators & Emulators" 섹션 추가
2. 각 시뮬레이터의 역할과 구현 상태를 테이블로 정리
3. 시뮬레이터 아키텍처 다이어그램 추가
4. Integration Test Coverage 테이블 추가
5. 변경사항 커밋 및 푸시

---

## Verification Plan

1. README.md가 GitHub에서 올바르게 렌더링되는지 확인
2. 모든 링크가 올바르게 작동하는지 확인
3. Mermaid 다이어그램이 정상 표시되는지 확인

---

## Expected Outcome

README.md에서 다음이 명확해짐:
- 이 프로젝트는 **시뮬레이터/에뮬레이터 기반** 검증 환경
- Panel, FPGA, SoC는 실제 하드웨어가 아닌 **C# 에뮬레이터**
- 각 시뮬레이터가 무엇을 시뮬레이션하는지 명확한 설명
- 413개 단위 테스트 + 10개 통합 테스트 모두 통과
