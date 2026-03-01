# SPEC-EMUL-001: Emulator Module Revision Plan (v3 - Module Brainstormed)

## Context

**Problem**: SimulatorPipeline.ProcessFrame()이 MCU/Host를 실제로 통과하지 않고, InjectError/SetPacketLossRate가 빈 스텁이며, McuSimulator에 펌웨어 3대 핵심 모듈이 미구현.

**Goal**: HW 설계 검증용 Golden Reference로 사용 가능한 고충실도 에뮬레이터. 각 모듈 CLI 독립 실행 지원.

**Approach**: MCU 완성 + FPGA 강화 + 파이프라인 실체화 + Panel 물리 모델 순서.

**Key Decisions** (사용자 브레인스토밍 결과):
- HW 설계 검증용 (타이밍/프로토콜 정확도 최우선)
- MCU/SoC 완성이 가장 시급
- CLI 독립 실행 형태
- Panel: Level A 타이밍 인지 모델 + 4대 물리 모델 + 캘리브레이션
- FPGA: 제어 신호 + 보호 로직 + 타이밍 모델 강화
- MCU: fw/ C 코드 1:1 완전 포팅, 이벤트 콜백 추상화
- Network: 독립 NetworkChannel 클래스 (파이프라인 내부)
- Host: 파이프라인 연결 + 타임아웃 강화

---

## Module Brainstorming Summary

### Panel (사용자 선택 완료)
- **물리 모델**: 4가지 전체 선택 (노이즈, 게인/오프셋, X선 응답, 시간적 효과)
- **캘리브레이션**: 필요 (다크/플랫필드/바이어스 프레임 생성)
- **Gate/ROIC**: Level A 타이밍 인지 모델 (gate_on 이벤트 응답, 노출 비례 신호, 행별 ROIC 리드아웃)
- **출력 변경**: 2D 프레임 → Row-by-row LineData (FPGA LineBuffer 직접 입력)

### FPGA (사용자 선택 완료)
- **기능 범위**: A+B+C (제어 신호 + 보호 로직 + 타이밍 모델)
- **CSI-2**: 패킷 구조 보완 + 백프레셔 모델링
- **SPI**: STATUS 실시간 반영 + ILA 캡처
- **D-PHY**: 후속 SPEC으로 분리

### MCU (사용자 선택 완료)
- **포팅 범위**: fw/ C 코드 1:1 완전 포팅 (API/상태전이/에러코드 모두)
- **SequenceEngine TODO**: 이벤트 콜백(ISequenceCallback)으로 추상화 + SPI 실제 연동
- **CommandProtocol**: HMAC-SHA256 검증 + 리플레이 방지 포함
- **통합**: 모듈별 독립 + McuTopSimulator 조합 클래스

### Network (사용자 선택 완료)
- **구성**: 독립 NetworkChannel 클래스 (파이프라인 내부, 별도 프로젝트 아님)
- **기능**: 패킷 손실/재정렬/지연/손상 주입

### Host (사용자 선택 완료)
- **범위**: 파이프라인 연결 + 타임아웃 강화
- **변경**: 기존 FrameReassembler를 실제 파이프라인에 연결, 자동 타임아웃 폴링, 통계 대시보드

---

## Phase 1: MCU/SoC 에뮬레이터 완성

fw/ 헤더를 1:1 C# 포팅. 모든 API 시그니처, 상태 전이, 에러 코드가 펌웨어와 일치해야 함.

### 1A. SequenceEngine (fw/include/sequence_engine.h 기반)

**참조**: `fw/include/sequence_engine.h:28-62` - 7-state FSM, 8 events, 3 scan modes

| New File | Description |
|----------|-------------|
| `McuSimulator.Core/Sequence/SequenceState.cs` | `seq_state_t` enum: IDLE(0)~ERROR(6) |
| `McuSimulator.Core/Sequence/SequenceEvent.cs` | `seq_event_t` enum: START_SCAN(0)~COMPLETE(7) |
| `McuSimulator.Core/Sequence/ScanMode.cs` | `scan_mode_t` enum: Single(0), Continuous(1), Calibration(2) |
| `McuSimulator.Core/Sequence/SequenceStatistics.cs` | `seq_stats_t`: frames_received/sent, errors, retries |
| `McuSimulator.Core/Sequence/ISequenceCallback.cs` | 외부 연동 추상화: OnConfigure(), OnArm(), OnStop(), OnError() |
| `McuSimulator.Core/Sequence/SequenceEngine.cs` | 핵심 FSM (아래 상세) |

**FSM 상태 전이 테이블**:

```
Current State    | Event          | Next State   | Action
-----------------|----------------|--------------|------------------
IDLE             | START_SCAN     | CONFIGURE    | callback.OnConfigure(mode)
CONFIGURE        | CONFIG_DONE    | ARM          | callback.OnArm()
ARM              | ARM_DONE       | SCANNING     | -
SCANNING         | FRAME_READY    | STREAMING    | FrameBuffer.GetBuffer()
STREAMING        | COMPLETE       | COMPLETE     | FrameBuffer.CommitBuffer()
COMPLETE(single) | -              | IDLE         | Auto-transition
COMPLETE(cont.)  | -              | SCANNING     | Auto-transition
ANY              | ERROR          | ERROR        | retries++, if <3 → IDLE
ANY              | STOP_SCAN      | IDLE         | callback.OnStop()
ERROR            | ERROR_CLEARED  | IDLE         | Reset retries
```

**ISequenceCallback**: SPI 실제 연동을 콜백으로 추상화
```csharp
public interface ISequenceCallback
{
    void OnConfigure(ScanMode mode);  // SPI WriteReg(MODE, mode)
    void OnArm();                      // SPI WriteReg(CTRL, START)
    void OnStop();                     // SPI WriteReg(CTRL, STOP)
    void OnError(SequenceState state, string reason);
}
```

### 1B. FrameBufferManager (fw/include/frame_manager.h 기반)

**참조**: `fw/include/frame_manager.h:25-65` - 4-buffer ring, oldest-drop

| New File | Description |
|----------|-------------|
| `McuSimulator.Core/Buffer/BufferState.cs` | `buf_state_t` enum: FREE(0)~SENDING(3) |
| `McuSimulator.Core/Buffer/FrameBufferDescriptor.cs` | `frame_buffer_t` 매핑 |
| `McuSimulator.Core/Buffer/FrameManagerConfig.cs` | `frame_mgr_config_t` 매핑: rows, cols, bit_depth, num_buffers(=4) |
| `McuSimulator.Core/Buffer/FrameManagerStatistics.cs` | `frame_stats_t`: received/sent/dropped, packets_sent, bytes_sent, overruns |
| `McuSimulator.Core/Buffer/FrameBufferManager.cs` | 핵심 매니저 |

**API** (fw 1:1):
```csharp
int GetBuffer(uint frameNumber, out byte[] buf, out int size);    // FREE→FILLING
int CommitBuffer(uint frameNumber);                                // FILLING→READY
int GetReadyBuffer(out byte[] buf, out int size, out uint frameNumber); // READY→SENDING
int ReleaseBuffer(uint frameNumber);                               // SENDING→FREE
// Oldest-Drop: FREE 없으면 가장 오래된 READY 드롭 → overruns++
```

### 1C. HealthMonitor (fw/include/health_monitor.h 기반)

| New File | Description |
|----------|-------------|
| `McuSimulator.Core/Health/LogLevel.cs` | DEBUG(0)~CRITICAL(4) |
| `McuSimulator.Core/Health/RuntimeStatistics.cs` | 9개 카운터 |
| `McuSimulator.Core/Health/SystemStatus.cs` | state + stats + battery_soc + uptime_sec + fpga_temp |
| `McuSimulator.Core/Health/ISimulationClock.cs` | 테스트 시간 제어 인터페이스 |
| `McuSimulator.Core/Health/HealthMonitor.cs` | 워치독 + 통계 + 로깅 |

**Key**: `ISimulationClock` 의존으로 테스트에서 시간 제어 가능

### 1D. CommandProtocol (HMAC-SHA256)

| New File | Description |
|----------|-------------|
| `McuSimulator.Core/Command/CommandType.cs` | START_SCAN, STOP_SCAN, GET_STATUS, SET_CONFIG |
| `McuSimulator.Core/Command/CommandMessage.cs` | type + payload + hmac + timestamp |
| `McuSimulator.Core/Command/CommandProtocol.cs` | HMAC 검증 + 타임스탬프 리플레이 방지 + 명령 디스패치 |

### 1E. McuTopSimulator (통합 오케스트레이션)

| New File | Description |
|----------|-------------|
| `McuSimulator.Core/McuTopSimulator.cs` | SPI + CSI2 + Reassembler + SequenceEngine + FrameBuffer + UdpTx + Health + Command 전체 조합 |

**Process() 흐름**:
```
1. CommandProtocol.ValidateAndDispatch(command)
2. SequenceEngine.HandleEvent(START_SCAN)
3. ISequenceCallback.OnConfigure() → SpiMaster → FPGA
4. SequenceEngine → ARM → SCANNING
5. Csi2Rx.Parse(packets) → FrameReassembler.Reassemble()
6. FrameBufferManager.GetBuffer() → CommitBuffer()
7. SequenceEngine → STREAMING
8. FrameBufferManager.GetReadyBuffer()
9. UdpTransmitter.FragmentFrame()
10. FrameBufferManager.ReleaseBuffer()
11. HealthMonitor.UpdateStat("frames_sent", 1)
```

### Phase 1 Test Strategy (TDD - 신규 코드)
- SequenceEngine: 56 전이 table-driven test + ISequenceCallback mock
- FrameBufferManager: Producer-Consumer (정상, overflow, oldest-drop, 동시성)
- HealthMonitor: 워치독 timeout, 통계, GetStatus <50ms
- CommandProtocol: 유효/무효 HMAC, 리플레이, 타임스탬프 만료
- McuTopSimulator: 전체 흐름 통합 테스트
- **목표**: 각 클래스 90%+ 커버리지

---

## Phase 2: FPGA 시뮬레이터 강화

현재 ~65% 완성도를 ~85%로 향상. 제어 신호 + 보호 로직 + 타이밍 모델 추가.

### 2A. FSM 제어 신호 출력

**Modified**: `FpgaSimulator.Core/Fsm/PanelScanFsmSimulator.cs`

추가할 출력 신호:
- `GateOn`: bool - INTEGRATE 상태에서 Panel에 노출 제어 펄스
- `RoicSync`: bool - IDLE→INTEGRATE 전환 시 ROIC 읽기 트리거
- `LineValid`: bool - READOUT 완료 시 라인 데이터 유효 신호
- `FrameValid`: bool - FRAME_DONE 상태에서 프레임 완료 신호
- `LineWriteAddress`: ushort - READOUT 중 현재 쓰기 주소

**추가할 타이머 분리**:
- `_settleTimer` (8-bit): ROIC settle 카운트다운 (현재 합쳐져 있음)
- `_adcTimer` (8-bit): ADC 변환 카운트다운 (현재 합쳐져 있음)

### 2B. Protection Logic 구현

| New File | Description |
|----------|-------------|
| `FpgaSimulator.Core/Protection/ProtectionLogicSimulator.cs` | 워치독 + 타임아웃 + 안전 셧다운 |
| `FpgaSimulator.Core/Protection/ProtectionConfig.cs` | 워치독 100ms, 리드아웃 100us 등 설정 |

**핵심 기능**:
- 워치독 타이머: 100ms 기본, SPI heartbeat 리셋
- 리드아웃 타임아웃: 100us 기본
- 안전 셧다운 응답: 10 클럭 내 gate_safe/csi2_disable/buffer_disable
- Fatal vs Non-fatal 에러 분류
- ErrorFlags 래치 (error_clear까지 유지)

### 2C. CSI-2 TX 보완

**Modified**: `FpgaSimulator.Core/Csi2/Csi2TxPacketGenerator.cs`

- LS (Line Start, 0x02) / LE (Line End, 0x03) 패킷 추가
- ECC 알고리즘 정확도 개선 (full 6-bit MIPI spec)
- AXI4-Stream 백프레셔 모델링 (tready 기반 흐름 제어)

| New File | Description |
|----------|-------------|
| `FpgaSimulator.Core/Csi2/Csi2BackpressureModel.cs` | tready/tvalid 흐름 제어 시뮬레이션 |

### 2D. SPI 레지스터 실시간 반영

**Modified**: `FpgaSimulator.Core/Spi/SpiSlaveSimulator.cs`

- STATUS 레지스터: FSM 상태 + 버퍼 뱅크에서 실시간 계산
- ILA 캡처 레지스터: 내부 상태 스냅샷 기록 (에러 발생 시)
- `UpdateStatusFromFsm(FsmStatus status)` 메서드 추가

### 2E. CDC/타이밍 모델

| New File | Description |
|----------|-------------|
| `FpgaSimulator.Core/Timing/ClockDomain.cs` | 클럭 도메인 정의 (sys 100MHz, ROIC, CSI-2 125MHz) |
| `FpgaSimulator.Core/Timing/CdcSynchronizer.cs` | 2-stage FF CDC 지연 모델링 |

### Phase 2 Test Strategy (DDD - 기존 수정)
- PanelScanFsmSimulator: 기존 테스트 유지 + 제어 신호 출력 검증
- ProtectionLogic: 워치독 timeout, 안전 셧다운 타이밍
- CSI-2: LS/LE 패킷 생성, 백프레셔 시나리오
- **목표**: 각 클래스 85%+ 커버리지

---

## Phase 3: Panel 물리 모델 업그레이드

현재 Counter/Gaussian 노이즈만 → X선 물리 기반 모델로 확장.

### 3A. X선 응답 모델

| New File | Description |
|----------|-------------|
| `PanelSimulator/Models/Physics/ScintillatorModel.cs` | kVp/mAs → 광자수 변환, CsI(Tl) 발광 특성 |
| `PanelSimulator/Models/Physics/ExposureModel.cs` | gate_on 펄스 기반 노출 시간 계산, 노출량 비례 신호 |

### 3B. 복합 노이즈 모델

| New File | Description |
|----------|-------------|
| `PanelSimulator/Generators/CompositeNoiseGenerator.cs` | Poisson(광자) + Gaussian(전자) + Dark Current + 1/f Noise 합성 |
| `PanelSimulator/Generators/PoissonNoiseGenerator.cs` | 포아송 분포 광자 통계 노이즈 |

### 3C. 게인/오프셋 맵

| New File | Description |
|----------|-------------|
| `PanelSimulator/Models/Calibration/GainOffsetMap.cs` | 픽셀별 게인 변동 + 오프셋 맵 (파일 로딩/생성) |
| `PanelSimulator/Models/Calibration/CalibrationFrameGenerator.cs` | Dark/Flatfield/Bias 캘리브레이션 프레임 생성 |

### 3D. 시간적 효과

| New File | Description |
|----------|-------------|
| `PanelSimulator/Models/Temporal/LagModel.cs` | 잔상(ghosting) 시뮬레이션 - 이전 프레임 잔류 신호 |
| `PanelSimulator/Models/Temporal/DriftModel.cs` | 온도 드리프트 - 시간에 따른 오프셋 변화 |

### 3E. Gate/ROIC 인터페이스

| New File | Description |
|----------|-------------|
| `PanelSimulator/Models/Readout/RoicReadoutModel.cs` | 행별 ROIC 리드아웃 시뮬레이션, settle 시간 반영 |
| `PanelSimulator/Models/Readout/GateResponseModel.cs` | gate_on 이벤트 응답, 노출 시간 비례 신호 생성 |

**출력 변경**: `PanelSimulator.Process()` 가 2D ushort[,] 대신 Row-by-row `LineData[]` 반환
- FPGA LineBuffer에 직접 입력 가능
- 행 단위 타이밍이 ROIC settle/ADC 시간 반영

### 3F. 물리 파이프라인 체인

```
ScintillatorModel (kVp/mAs → 광자)
    → ExposureModel (gate_on → 신호량)
    → CompositeNoise (Poisson + Gaussian + Dark + 1/f)
    → GainOffsetMap (픽셀별 변동)
    → LagModel (잔상)
    → DefectMap (기존 결함 시뮬레이션)
    → RoicReadout (행별 리드아웃 + settle)
```

### Phase 3 Test Strategy (TDD - 신규)
- ScintillatorModel: 에너지 응답 특성 검증
- CompositeNoise: 통계적 분포 검증 (평균/분산)
- GainOffsetMap: 로딩/적용 정확성
- LagModel: 잔상 감쇠 특성
- RoicReadout: 행별 타이밍 검증
- **목표**: 각 클래스 85%+ 커버리지

---

## Phase 4: 파이프라인 실체화

SimulatorPipeline.ProcessFrame()이 실제로 모든 계층을 통과하도록 수정.

### 4A. ProcessFrame() 리팩터링

**Modified**: `IntegrationRunner.Core/SimulatorPipeline.cs`

**현재**:
```
Panel.Generate → FPGA.CSI2.Encode → [MCU/Host 건너뜀] → 바로 FrameData
```

**목표**:
```
Panel.Generate(gate_on) → Row-by-row LineData
  → FPGA.LineBuffer → CSI2.TX.Encode
  → MCU.CSI2.RX.Decode → Reassemble → FrameBuffer → UDP.Fragment
  → [NetworkChannel] (손실/재정렬/지연)
  → Host.Reassemble → FrameData
```

### 4B. NetworkChannel (파이프라인 내부)

| New File | Description |
|----------|-------------|
| `IntegrationRunner.Core/Network/NetworkChannel.cs` | MCU-Host 간 네트워크 채널: 패킷 손실, 재정렬, 지연, 손상 주입 |
| `IntegrationRunner.Core/Network/NetworkChannelConfig.cs` | 손실률, 재정렬률, 지연 범위, 시드 등 설정 |

### 4C. 스텁 제거 (실제 구현)

| Method | Implementation |
|--------|----------------|
| `InjectError(string)` | FPGA ProtectionLogic에 에러 주입 |
| `SetPacketLossRate(double)` | NetworkChannel.SetLossRate() 위임 |
| `SetPacketReorderRate(double)` | NetworkChannel.SetReorderRate() 위임 |
| `SetScanMode(ScanMode)` | SequenceEngine.StartScan(mode) 위임 |

### 4D. 체크포인트 모드

| New File | Description |
|----------|-------------|
| `IntegrationRunner.Core/PipelineCheckpoint.cs` | 각 계층 입출력 스냅샷 + 레이턴시 측정 |

### 4E. Host 타임아웃 강화

**Modified**: `HostSimulator.Core/Reassembly/FrameReassembler.cs`
- 자동 타임아웃 폴링 (수동 → 자동)
- 불완전 프레임 복구 시도 (수신된 패킷만으로 부분 프레임 반환)
- 재조립 통계 대시보드 (수신/완료/불완전/타임아웃 카운터)

### 4F. Builder 확장

**Modified**: `IntegrationTests/Helpers/SimulatorPipelineBuilder.cs`
- `.WithMcuSimulator()`, `.WithNetworkChannel()`, `.WithCheckpoints()` 추가

### Phase 4 Test Strategy (DDD - 기존 수정)
- IT-01~IT-12 회귀 테스트 필수 통과
- IT-03 (비순서): SetPacketReorderRate 실제 동작 검증
- IT-04 (에러 주입): InjectError → ProtectionLogic 실제 동작
- IT-07 (패킷 손실): SetPacketLossRate 실제 동작 검증
- **새 IT-13**: Pipeline 실체화 검증 (4계층 bit-exact)
- **새 IT-14**: SequenceEngine 전체 사이클
- **새 IT-15**: FrameBufferManager overflow + oldest-drop

---

## Phase 5: CLI 독립 실행 인프라

### 5A. 공통 CLI 프레임워크

| New File | Description |
|----------|-------------|
| `Common.Cli/CliFramework.cs` | --config, --output, --fidelity, --seed, --verbose |
| `Common.Cli/OutputFormatter.cs` | JSON/CSV/Binary 출력 |
| `Common.Cli/Common.Cli.csproj` | System.CommandLine 기반 |

### 5B. 모듈별 CLI

| Module | CLI Example | Output |
|--------|------------|--------|
| PanelSimulator | `--rows 2048 --cols 2048 --kvp 80 --noise composite` | .raw/.tiff |
| FpgaSimulator | `--input panel.raw --mode continuous --protection on` | .csi2 패킷 |
| McuSimulator | `--input csi2.bin --buffers 4 --command start_scan` | .udp 패킷 + 통계 |
| HostSimulator | `--input udp.bin --timeout 1000 --output frame.tiff` | 재조립 프레임 |
| IntegrationRunner | `--config detector_config.yaml --frames 100` | 전체 결과 |

### 5C. 데이터 직렬화

| New File | Description |
|----------|-------------|
| `Common.Dto/Serialization/FrameDataSerializer.cs` | FrameData ↔ .raw |
| `Common.Dto/Serialization/Csi2PacketSerializer.cs` | Csi2Packet[] ↔ .csi2 |
| `Common.Dto/Serialization/UdpPacketSerializer.cs` | UdpFramePacket[] ↔ .udp |

**Binary Format**: `[4B magic][4B version][4B count][N * item_bytes]`

---

## Dependency Graph

```
Phase 1: MCU 완성 ─────────────────────────────────
    │
    ├── 1A: SequenceEngine       (독립, TDD)
    ├── 1B: FrameBufferManager   (독립, TDD)
    ├── 1C: HealthMonitor        (독립, TDD)
    ├── 1D: CommandProtocol      (1C 의존, TDD)
    └── 1E: McuTopSimulator      (1A-1D 전체 의존)
                │
Phase 2: FPGA 강화 ────────────────────────────────
    │
    ├── 2A: FSM 제어 신호       (독립, DDD)
    ├── 2B: Protection Logic    (2A 의존, TDD)
    ├── 2C: CSI-2 TX 보완      (독립, DDD)
    ├── 2D: SPI 실시간 반영     (2A 의존, DDD)
    └── 2E: CDC/타이밍 모델     (독립, TDD)
                │
Phase 3: Panel 물리 ───────────────────────────────
    │
    ├── 3A: X선 응답 모델       (독립, TDD)
    ├── 3B: 복합 노이즈         (3A 의존, TDD)
    ├── 3C: 게인/오프셋 맵      (독립, TDD)
    ├── 3D: 시간적 효과         (독립, TDD)
    ├── 3E: Gate/ROIC 인터페이스 (2A 의존, TDD)
    └── 3F: 물리 파이프라인 체인 (3A-3E 전체 의존)
                │
Phase 4: 파이프라인 실체화 ────────────────────────
    │
    ├── 4A: ProcessFrame 리팩터링 (1E+2B+3F 의존, DDD)
    ├── 4B: NetworkChannel       (독립, TDD)
    ├── 4C: 스텁 제거            (4A+4B 의존, DDD)
    ├── 4D: 체크포인트 모드       (4A 의존, TDD)
    ├── 4E: Host 타임아웃 강화    (독립, DDD)
    └── 4F: Builder 확장          (4A 의존, DDD)
                │
Phase 5: CLI 독립 실행 ────────────────────────────
    │
    ├── 5A: 공통 CLI 프레임워크   (독립)
    ├── 5B: 모듈별 CLI           (5A + Phase 1-3 의존)
    └── 5C: 데이터 직렬화        (독립)
```

**병렬 실행 가능 그룹**:
- Group A: 1A, 1B, 1C (MCU 독립 모듈)
- Group B: 2A, 2C, 2E (FPGA 독립 항목)
- Group C: 3A, 3C, 3D (Panel 독립 모델)
- Group D: 4B, 4E, 5A, 5C (인프라 독립 항목)

---

## Scope Summary

| Category | Phase 1 | Phase 2 | Phase 3 | Phase 4 | Phase 5 | Total |
|----------|---------|---------|---------|---------|---------|-------|
| New source files | 15 | 5 | 10 | 3 | 9 | **42** |
| Modified files | 3 | 4 | 2 | 4 | 5 | **18** |
| New test files | 7 | 4 | 7 | 3 | 3 | **24** |
| New integration tests | 2 | 0 | 0 | 3 | 0 | **5** |

---

## Critical Files Reference

### Existing (Modify)
- `tools/IntegrationRunner/src/IntegrationRunner.Core/SimulatorPipeline.cs` - 파이프라인 리팩터링
- `tools/IntegrationTests/Helpers/SimulatorPipelineBuilder.cs` - 빌더 확장
- `tools/McuSimulator/src/McuSimulator.Core/Spi/SpiMasterSimulator.cs` - SequenceEngine 연동
- `tools/McuSimulator/src/McuSimulator.Core/Frame/FrameReassembler.cs` - FrameBufferManager 연동
- `tools/McuSimulator/src/McuSimulator.Core/Network/UdpFrameTransmitter.cs` - FrameBufferManager 연동
- `tools/FpgaSimulator/src/FpgaSimulator.Core/Fsm/PanelScanFsmSimulator.cs` - 제어 신호/타이밍
- `tools/FpgaSimulator/src/FpgaSimulator.Core/Csi2/Csi2TxPacketGenerator.cs` - LS/LE/백프레셔
- `tools/FpgaSimulator/src/FpgaSimulator.Core/Spi/SpiSlaveSimulator.cs` - STATUS 실시간
- `tools/PanelSimulator/src/PanelSimulator/PanelSimulator.cs` - 물리 파이프라인 체인
- `tools/HostSimulator/src/HostSimulator.Core/Reassembly/FrameReassembler.cs` - 타임아웃 강화

### Existing (Reference - 1:1 포팅 원본)
- `fw/include/sequence_engine.h` + `fw/src/sequence_engine.c`
- `fw/include/frame_manager.h` + `fw/src/frame_manager.c`
- `fw/include/health_monitor.h` + `fw/src/health_monitor.c`
- `fpga/rtl/panel_scan_fsm.sv` - FSM 제어 신호 참조
- `fpga/rtl/protection_logic.sv` - 보호 로직 참조

### Existing (Reuse)
- `tools/Common/src/Common.Dto/Interfaces/ISimulator.cs`
- `tools/Common/src/Common.Dto/Dtos/`
- `tools/FpgaSimulator/src/FpgaSimulator.Core/Fsm/ErrorFlags.cs`

---

## Verification

### Per-Phase
```bash
# Phase 1: MCU
cd tools/McuSimulator && dotnet test --verbosity normal
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Phase 2: FPGA
cd tools/FpgaSimulator && dotnet test --verbosity normal

# Phase 3: Panel
cd tools/PanelSimulator && dotnet test --verbosity normal

# Phase 4: Integration (회귀 포함)
cd tools/IntegrationTests && dotnet test --verbosity normal

# Phase 5: CLI
dotnet run --project tools/PanelSimulator -- --rows 256 --cols 256 --output test.raw
dotnet run --project tools/FpgaSimulator -- --input test.raw --output csi2.bin
dotnet run --project tools/McuSimulator -- --input csi2.bin --output udp.bin
dotnet run --project tools/HostSimulator -- --input udp.bin --output frame.tiff
```

### End-to-End
- IT-01~IT-12: 기존 테스트 전부 통과 (회귀 방지)
- IT-13 (new): Pipeline 실체화 검증 (4계층 bit-exact)
- IT-14 (new): SequenceEngine 전체 사이클
- IT-15 (new): FrameBufferManager overflow + oldest-drop
- IT-16 (new): Protection Logic 안전 셧다운
- IT-17 (new): Panel 물리 모델 통합 (노이즈 통계 검증)

---

## Execution Strategy

- **SPEC**: `.moai/specs/SPEC-EMUL-001/` 생성
- **Worktree**: 격리 브랜치에서 작업
- **Team Mode**: Phase별 병렬 그룹 활용
- **Development Mode**: Hybrid (신규=TDD, 기존수정=DDD)
- **Phase 실행 순서**: 1 → 2 → 3 → 4 → 5 (의존성 기반)
- **Phase 내 병렬**: Group A/B/C/D 동시 진행 가능

## Follow-up SPECs (후속)

| SPEC | Content | Trigger |
|------|---------|---------|
| SPEC-EMUL-002 | D-PHY 물리 계층 (LP/HS 전환, 레인 직렬화) | Phase 2 완료 후 |
| SPEC-EMUL-003 | NetworkSimulator 독립 프로젝트 (대역폭/지터/콘제스쳐) | Phase 4 완료 후 |
| SPEC-EMUL-004 | 3-Level Fidelity 인터페이스 (Level 0/1/2 전환) | 전체 완성 후 |
| SPEC-EMUL-005 | DICOM 인코딩 파이프라인 통합 | Phase 4 완료 후 |
