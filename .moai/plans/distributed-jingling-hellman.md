# Plan: Emulation/Simulation Architecture Review & System Validation Update

## Context

X-ray Detector Panel System은 3-tier 물리 하드웨어(FPGA -> SoC -> Host PC)를 소프트웨어로 에뮬레이션하여, 실제 장비 없이 전체 시스템을 검증하는 것이 목적이다. 현재 413+ 테스트가 통과하고 85%+ 커버리지를 달성했으나, **각 물리 모듈의 시뮬레이션 충실도 검증**과 **4-Layer 전체 파이프라인 end-to-end 테스트**가 부재하다. `SimulatorPipelineBuilder`는 실제 시뮬레이터를 연결하지 않는 설정 홀더이며, IT-01은 Host 레이어만 테스트한다.

---

## Phase 1: SimulatorPipelineBuilder 실체화 (Foundation)

**목적**: 4개 시뮬레이터(Panel -> FPGA -> MCU -> Host)를 실제로 연결하는 파이프라인 구축

### 수정 파일
- `tools/IntegrationTests/Helpers/SimulatorPipelineBuilder.cs` — **전면 재작성**

### 구현 내용
- 기존 `PipelineConfiguration` 기반 설정은 유지
- `PanelSimulator`, `Csi2TxPacketGenerator`, `Csi2RxPacketParser`, MCU `FrameReassembler`, `UdpFrameTransmitter`, Host `HostSimulator` 인스턴스 생성 및 연결
- `ProcessFrame()` 메서드: 전체 파이프라인 실행, 각 레이어 경계에서 중간 결과 반환
- `ProcessFrameWithCheckpoints()` 메서드: 각 checkpoint에서 데이터 무결성 검증 가능한 결과 객체 반환
- 기존 `PerformanceTier`, `StartAsync/StopAsync` API는 하위 호환 유지

### 참조 파일
- `tools/PanelSimulator/src/PanelSimulator/PanelSimulator.cs` — Panel 시뮬레이터
- `tools/FpgaSimulator/src/FpgaSimulator.Core/Csi2/Csi2TxPacketGenerator.cs` — CSI-2 TX
- `tools/McuSimulator/src/McuSimulator.Core/Csi2/Csi2RxPacketParser.cs` — CSI-2 RX
- `tools/McuSimulator/src/McuSimulator.Core/Frame/FrameReassembler.cs` — MCU 프레임 조립
- `tools/McuSimulator/src/McuSimulator.Core/Network/UdpFrameTransmitter.cs` — UDP 전송
- `tools/HostSimulator/src/HostSimulator.Core/HostSimulator.cs` — Host 수신/조립

---

## Phase 2: MCU FrameReassembler Bitmap 제한 수정 (Bug Fix)

**목적**: `ulong ReceivedLineBitmap` (64-bit)이 2048+ 행 프레임을 추적하지 못하는 문제 수정

### 수정 파일
- `tools/McuSimulator/src/McuSimulator.Core/Frame/FrameReassembler.cs`

### 구현 내용
- `ReassembledFrame.ReceivedLineBitmap` (ulong) → `BitArray` 또는 제거
  - `Dictionary<int, ushort[]> _lines`가 이미 모든 라인을 정확히 추적하므로, bitmap은 진단용으로만 사용
  - `BitArray`로 교체하여 임의 크기 프레임 지원
- 기존 `ReceivedLineBitmap` 필드를 사용하는 코드 업데이트

### 테스트
- MCU 2048행 프레임 재조립 정확성 테스트 추가

---

## Phase 3: 모듈별 시뮬레이션 검증 테스트 (Module Verification)

**목적**: 각 물리 모듈 시뮬레이터의 충실도를 독립적으로 검증

### 3.1 CSI-2 Round-Trip 테스트 (FPGA TX -> MCU RX)
- **파일**: `tools/FpgaSimulator/tests/FpgaSimulator.Tests/` (신규 테스트 클래스)
- **내용**: Counter 패턴 프레임 → `Csi2TxPacketGenerator.GenerateFullFrame()` → `Csi2RxPacketParser` + MCU `FrameReassembler` → 픽셀 bit-exact 비교
- **검증**: CSI-2 프로토콜 경계에서 데이터 손실 없음 확인

### 3.2 PanelSimulator 통계 검증
- **파일**: `tools/PanelSimulator/test/PanelSimulator.Tests/` (신규 테스트)
- **내용**:
  - FlatField: 모든 픽셀 동일 값 확인
  - Noise 통계: 평균/표준편차가 설정값과 일치 (3σ 범위)
  - Defect 비율: 설정된 defect rate와 실제 결함 픽셀 수 비교

### 3.3 Host Timeout 및 Storage Round-Trip
- **파일**: `tools/HostSimulator/tests/HostSimulator.Tests/` (신규 테스트)
- **내용**:
  - 불완전 프레임 전송 → timeout 감지 확인
  - TIFF 쓰기 → 바이트 읽기 → 픽셀 데이터 일치 확인
  - RAW 쓰기 → 파일 크기 == rows × cols × 2 확인

---

## Phase 4: IT-11 Full 4-Layer Pipeline 테스트 (System Integration)

**목적**: Panel → FPGA → MCU → Host → Storage 전체 경로의 bit-exact 데이터 무결성 검증

### 신규 파일
- `tools/IntegrationTests/Integration/IT11_FullFourLayerPipelineTests.cs`

### 테스트 시나리오
| 테스트 | 해상도 | 패턴 | 검증 항목 |
|--------|--------|------|-----------|
| Counter 패턴 소형 | 256×256 | Counter | 4-layer bit-exact match |
| Checkerboard 표준 | 1024×1024 | Checkerboard | 경계 checkpoint별 무결성 |
| FlatField 대형 | 2048×2048 | FlatField | 대형 프레임 파이프라인 안정성 |
| Noise 포함 | 512×512 | FlatField+Noise | 노이즈 데이터 보존 확인 |

### 각 테스트의 검증 checkpoint
1. **Panel → FPGA**: FrameData → CSI-2 패킷 수 == rows + 2 (FS + lines + FE)
2. **FPGA → MCU**: CSI-2 패킷 → 재조립된 2D 배열, 원본과 픽셀 일치
3. **MCU → Host**: UDP 패킷 → 재조립된 FrameData, 원본과 픽셀 일치
4. **Host → Storage**: TIFF/RAW 파일 출력, 파일 크기 정합성

---

## Phase 5: IT-12 Module Isolation 테스트

**목적**: 각 시뮬레이터를 독립적으로 교체/모킹 가능한지 확인 (ISimulator 계약 검증)

### 신규 파일
- `tools/IntegrationTests/Integration/IT12_ModuleIsolationTests.cs`

### 테스트 시나리오
- FPGA 시뮬레이터를 Mock으로 교체 → MCU + Host 정상 동작
- MCU 시뮬레이터를 Mock으로 교체 → Host 정상 동작
- 각 모듈의 `ISimulator.GetStatus()` 반환값 검증
- `ISimulator.Reset()` 후 상태 초기화 확인

---

## Phase 6: 시뮬레이션 범위 문서화

**목적**: 시뮬레이션이 커버하는 범위와 의도적 제외 사항을 명시

### 문서 내용 (SPEC-INTEG-001 업데이트 또는 별도 섹션)

| 카테고리 | 시뮬레이션 커버 | 의도적 제외 |
|----------|----------------|-------------|
| 데이터 경로 | Panel → FPGA → MCU → Host 전체 | LVDS/ROIC 전기적 인터페이스 |
| 프로토콜 | SPI, CSI-2, UDP, HMAC | PLL 클럭 드리프트, CDC 타이밍 |
| 에러 처리 | 8종 에러 플래그, 패킷 손실, 타임아웃 | 방사선 효과 (SEU) |
| 성능 | 30fps throughput, p95 latency | DDR4 물리 레이아웃, 전력 소비 |
| 주변장치 | - | 배터리 모니터링, 열 관리, GPIO |

---

## Verification Plan

### 실행 순서
```
Phase 1 (SimulatorPipelineBuilder) → Phase 2 (Bitmap Fix) → Phase 3 (Module Tests) → Phase 4 (IT-11) → Phase 5 (IT-12) → Phase 6 (Docs)
```

### 검증 명령
```bash
# 전체 테스트 실행
cd tools/IntegrationTests && dotnet test --verbosity normal

# 커버리지 확인
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# 특정 IT 테스트만 실행
dotnet test --filter "FullyQualifiedName~IT11"
dotnet test --filter "FullyQualifiedName~IT12"
```

### 성공 기준
- 기존 413+ 테스트 전부 통과 (regression 없음)
- IT-11: 4개 해상도/패턴 조합 모두 bit-exact match
- IT-12: 각 모듈 독립 교체 시 파이프라인 정상 동작
- 전체 커버리지 85%+ 유지
