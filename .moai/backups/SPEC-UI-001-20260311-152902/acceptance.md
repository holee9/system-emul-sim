---
id: SPEC-UI-001
type: acceptance
version: 1.0.0
created: 2026-03-11
updated: 2026-03-11
tags: [GUI, WPF, MVVM, Pipeline, Emulator, Integration]
---

## Acceptance Criteria: SPEC-UI-001 Integrated Emulator GUI

---

### AC-UI-001: 에뮬레이터 모드 전환

**관련 요구사항:** REQ-UI-010, REQ-UI-011, REQ-UI-012

```gherkin
GIVEN GUI가 SimulatedDetectorClient 모드로 실행 중일 때
WHEN  사용자가 "Pipeline Emulation" 모드로 전환하면
THEN  PipelineDetectorClient가 초기화되고 Panel 파라미터 제어판이 활성화된다
AND   기존 프레임 표시 탭은 변경 없이 동작을 유지한다
```

**검증 방법:**
- MainViewModel의 ActiveDetectorClient 타입이 PipelineDetectorClient로 변경됨을 assert
- FramePreviewViewModel이 IDetectorClient.FrameReceived를 정상 구독함을 확인
- SimulatorControlView가 Visible 상태로 전환됨을 확인

---

### AC-UI-002: Panel 파라미터 제어

**관련 요구사항:** REQ-UI-020

```gherkin
GIVEN Pipeline Emulation 모드가 활성화된 상태에서
WHEN  사용자가 kVp를 80에서 120으로 변경하고 Start를 누르면
THEN  Panel 에뮬레이터가 새 kVp(120)로 프레임을 생성하고
AND   Frame Preview 탭에 변경된 밝기의 프레임이 표시된다
```

**검증 방법:**
- SimulatorControlViewModel.Kvp 프로퍼티 변경 시 DetectorConfig.Kvp에 반영됨을 assert
- PipelineDetectorClient가 업데이트된 config로 프레임을 생성함을 확인
- FrameReceived 이벤트의 프레임 데이터가 kVp 변경을 반영함을 검증

---

### AC-UI-003: 네트워크 결함 주입

**관련 요구사항:** REQ-UI-023, REQ-UI-032

```gherkin
GIVEN Pipeline Emulation 모드로 프레임이 흐르는 상태에서
WHEN  사용자가 패킷 손실률을 50%로 설정하면
THEN  Pipeline Monitor 탭에서 PacketsLost 카운터가 증가한다
AND   FramesFailed 카운터 또는 DroppedFrames가 증가한다
```

**검증 방법:**
- NetworkChannelConfig.PacketLossRate가 0.5로 설정됨을 assert
- PipelineStatusViewModel.PacketsLost > 0 을 확인
- 충분한 프레임(예: 100) 전송 후 PacketsLost/PacketsSent 비율이 약 50%임을 검증

---

### AC-UI-004: 시나리오 실행

**관련 요구사항:** REQ-UI-040, REQ-UI-041, REQ-UI-042, REQ-UI-043

```gherkin
GIVEN Scenario Runner 탭에서 "IT01_FullPipeline" 시나리오가 목록에 있을 때
WHEN  사용자가 해당 시나리오를 선택하고 Run을 누르면
THEN  진행률 바가 0%에서 100%로 증가하며 프레임이 처리된다
AND   완료 후 PASS/FAIL 결과가 상세 메시지와 함께 표시된다
```

**검증 방법:**
- ScenarioRunnerViewModel.Scenarios 컬렉션에 "IT01_FullPipeline"이 존재함을 assert
- RunCommand 실행 시 Progress가 0에서 100으로 증가함을 확인
- 실행 완료 후 SelectedScenario.Result가 PASS 또는 FAIL임을 확인
- Result.Message가 비어 있지 않음을 검증

---

### AC-UI-005: 구성 저장/로드

**관련 요구사항:** REQ-UI-050, REQ-UI-051

```gherkin
GIVEN 사용자가 Panel kVp=120, 버퍼 수=8로 설정한 상태에서
WHEN  "Save Config"를 눌러 detector_config.yaml로 저장하고 다시 로드하면
THEN  kVp=120, 버퍼 수=8이 그대로 복원된다
```

**검증 방법:**
- SaveConfigCommand 실행 후 파일이 생성됨을 assert
- LoadConfigCommand 실행 후 SimulatorControlViewModel.Kvp == 120 확인
- SimulatorControlViewModel.FrameBufferCount == 8 확인
- 파일 내용이 YAML 형식이며 유효함을 검증

---

### AC-UI-006: MCU 버퍼 상태 실시간 표시

**관련 요구사항:** REQ-UI-022, REQ-UI-033

```gherkin
GIVEN Pipeline Emulation 모드로 고속 프레임 취득 중일 때
WHEN  Simulator Control 탭의 MCU 섹션을 확인하면
THEN  Free/Filling/Ready/Sending 각 상태의 버퍼 수가 2Hz로 갱신된다
```

**검증 방법:**
- SimulatorControlViewModel.BufferStatusFree 등 프로퍼티가 2Hz(500ms)로 갱신됨을 확인
- 모든 버퍼 상태 합계가 frameBufferCount와 일치함을 assert
- PropertyChanged 이벤트가 500ms 간격으로 발생함을 타이밍 테스트로 검증

---

### AC-UI-007: SPEC-TOOLS-001 하위 호환성

**관련 요구사항:** REQ-UI-010, REQ-UI-012

```gherkin
GIVEN GUI.Application이 수정된 상태에서
WHEN  SimulatedDetectorClient 모드(기본값)로 실행하면
THEN  기존 SPEC-TOOLS-001의 모든 기능(Frame Preview, Status Dashboard, W/L 조절)이 동일하게 동작한다
```

**검증 방법:**
- 기존 40개 단위 테스트 전체 통과 확인
- FramePreviewViewModel이 SimulatedDetectorClient와 정상 동작함을 확인
- StatusViewModel이 기존과 동일한 데이터를 표시함을 확인
- Window/Level 조절 기능이 정상 동작함을 검증

---

### AC-UI-008: 파라미터 유효성 검사

**관련 요구사항:** REQ-UI-052

```gherkin
GIVEN Pipeline Emulation 모드에서 Simulator Control 탭이 표시된 상태에서
WHEN  사용자가 kVp를 200(범위 초과)으로 입력하면
THEN  입력 필드에 빨간색 테두리가 표시된다
AND   Start 버튼이 비활성화된다
AND   유효 범위(40-150)를 안내하는 툴팁이 표시된다
```

**검증 방법:**
- SimulatorControlViewModel.HasValidationErrors == true 확인
- StartCommand.CanExecute == false 확인
- kVp를 유효 범위(80)로 복원 시 오류 해제 및 Start 활성화 확인

---

### AC-UI-009: 파이프라인 상태 인디케이터

**관련 요구사항:** REQ-UI-031

```gherkin
GIVEN Pipeline Emulation 모드로 파이프라인이 정상 동작 중일 때
WHEN  네트워크 corruptionRate를 90%로 설정하면
THEN  Host 계층의 상태 인디케이터가 Red로 변경된다
AND   Panel, FPGA, MCU 계층은 Green을 유지한다
```

**검증 방법:**
- PipelineStatusViewModel.HostStatus == "Red" 확인
- PipelineStatusViewModel.PanelStatus == "Green" 확인
- FpgaStatus, McuStatus도 "Green" 유지 확인

---

### AC-UI-010: 사전 정의 시나리오 번들

**관련 요구사항:** REQ-UI-044

```gherkin
GIVEN GUI가 처음 실행된 상태에서
WHEN  Scenario Runner 탭을 열면
THEN  IT01-IT19에 대응하는 사전 정의 시나리오가 목록에 표시된다
AND   각 시나리오에 name, description이 포함되어 있다
```

**검증 방법:**
- ScenarioRunnerViewModel.Scenarios.Count >= 19 확인
- 각 시나리오에 Name, Description이 null/empty가 아님을 assert
- "IT01_FullPipeline", "IT15_BufferOverflow" 등 대표 시나리오 존재 확인

---

## Quality Gate Criteria

### Definition of Done

- [ ] 모든 신규 코드에 대해 단위 테스트 작성 완료
- [ ] 기존 40개 GUI.Application 단위 테스트 전체 통과
- [ ] 기존 IT01-IT19 통합 테스트 전체 통과 (회귀 없음)
- [ ] 모든 ViewModel이 ObservableObject 상속, RelayCommand 사용 (기존 패턴)
- [ ] UI 업데이트 시 Dispatcher.Invoke() 사용 (thread safety)
- [ ] 새 NuGet 패키지 추가 없음
- [ ] IntegrationRunner.Core 기존 file-based 메서드 변경 없음
- [ ] SimulatedDetectorClient 모드(기본값)에서 기존 기능 100% 동작
- [ ] 코드 주석은 영어로 작성

### Test Coverage Target

| Component                    | Target Coverage |
|------------------------------|----------------|
| PipelineDetectorClient       | >= 85%         |
| ScenarioRunner               | >= 85%         |
| SimulatorControlViewModel    | >= 85%         |
| PipelineStatusViewModel      | >= 85%         |
| ScenarioRunnerViewModel      | >= 85%         |
