---
id: SPEC-UI-001
type: plan
version: 1.0.0
created: 2026-03-11
updated: 2026-03-11
tags: [GUI, WPF, MVVM, Pipeline, Emulator, Integration]
---

## Implementation Plan: SPEC-UI-001 Integrated Emulator GUI

### Technology Stack

- C# 12 / .NET 8.0-windows
- WPF + MVVM (기존 ObservableObject, RelayCommand 활용)
- xUnit + FluentAssertions + NSubstitute (ViewModel/Service 단위 테스트)
- IntegrationRunner.Core (ProjectReference)
- System.Text.Json (.NET 8 내장, 시나리오 JSON 파싱)
- 새 NuGet 패키지 추가 없음

---

### New Files to Create

#### Services

1. **`tools/GUI.Application/src/GUI.Application/Services/PipelineDetectorClient.cs`**
   - IDetectorClient 구현, SimulatorPipeline in-memory 래핑
   - Background Task 기반 프레임 생성 루프
   - CancellationToken 생명주기 관리
   - Thread-safe, configurable fps
   - 외부에서 DetectorConfig/NetworkChannelConfig 변경 가능

2. **`tools/GUI.Application/src/GUI.Application/Services/ScenarioRunner.cs`**
   - JSON 시나리오 파일 로드 (System.Text.Json)
   - SimulatorPipeline in-memory 실행
   - Assertion 평가 및 결과 수집
   - 진행률 콜백 (IProgress<int>)

#### ViewModels

3. **`tools/GUI.Application/src/GUI.Application/ViewModels/SimulatorControlViewModel.cs`**
   - Panel/FPGA/MCU/Network 파라미터용 바인딩 프로퍼티
   - Start/Stop/Reset Command (RelayCommand)
   - 파라미터 범위 유효성 검사
   - MCU 버퍼 상태(Free/Filling/Ready/Sending) 실시간 표시
   - ObservableObject 상속 (기존 프로젝트 패턴)

4. **`tools/GUI.Application/src/GUI.Application/ViewModels/PipelineStatusViewModel.cs`**
   - 계층별 통계 (FramesProcessed, FramesFailed, AvgProcessingTimeMs)
   - 계층별 상태 인디케이터 (Green/Yellow/Red)
   - NetworkChannelStats 표시 (sent/lost/reordered/corrupted)
   - DispatcherTimer 기반 2Hz 폴링

5. **`tools/GUI.Application/src/GUI.Application/ViewModels/ScenarioRunnerViewModel.cs`**
   - 시나리오 목록 (ObservableCollection)
   - 선택/실행 Command
   - 진행률 바인딩 (0-100%)
   - 결과 표시 (PASS/FAIL + 상세 메시지)

#### Views (XAML UserControls)

6. **`tools/GUI.Application/src/GUI.Application/Views/SimulatorControlView.xaml`**
   - Panel/FPGA/MCU/Network 파라미터 입력 컨트롤
   - Start/Stop/Reset 버튼
   - MCU 버퍼 상태 시각화

7. **`tools/GUI.Application/src/GUI.Application/Views/PipelineStatusView.xaml`**
   - 계층별 통계 DataGrid 또는 StackPanel
   - 색상 인디케이터 (Ellipse + Color binding)
   - NetworkChannelStats 표시 영역

8. **`tools/GUI.Application/src/GUI.Application/Views/ScenarioRunnerView.xaml`**
   - 시나리오 목록 ListView
   - Run 버튼 및 ProgressBar
   - 결과 DataGrid (Name, Status, Message)

#### Test Files

9. **`tools/GUI.Application/tests/GUI.Application.Tests/ViewModels/SimulatorControlViewModelTests.cs`**
10. **`tools/GUI.Application/tests/GUI.Application.Tests/ViewModels/PipelineStatusViewModelTests.cs`**
11. **`tools/GUI.Application/tests/GUI.Application.Tests/ViewModels/ScenarioRunnerViewModelTests.cs`**
12. **`tools/GUI.Application/tests/GUI.Application.Tests/Services/PipelineDetectorClientTests.cs`**
13. **`tools/GUI.Application/tests/GUI.Application.Tests/Services/ScenarioRunnerTests.cs`**

---

### Files to Modify

1. **`tools/GUI.Application/src/GUI.Application/GUI.Application.csproj`**
   - IntegrationRunner.Core에 대한 ProjectReference 추가

2. **`tools/GUI.Application/src/GUI.Application/Views/MainWindow.xaml`**
   - TabControl에 3개 새 탭 추가 (Simulator Control, Pipeline Monitor, Scenario Runner)

3. **`tools/GUI.Application/src/GUI.Application/ViewModels/MainViewModel.cs`**
   - 자식 ViewModel 인스턴스 추가 (SimulatorControlViewModel, PipelineStatusViewModel, ScenarioRunnerViewModel)
   - 에뮬레이터 모드 전환 로직

4. **`tools/GUI.Application/src/GUI.Application/App.xaml.cs`**
   - 시작 시 모드 선택 또는 기본 모드 설정

5. **`tools/IntegrationRunner/src/IntegrationRunner.Core/SimulatorPipeline.cs`**
   - `ProcessInMemory()` 메서드 추가 (in-memory 데이터 플로우)
   - 기존 file-based 메서드는 변경하지 않음 (backward compatibility)

---

### Task Decomposition (Implementation Order)

#### Primary Goal: Core Infrastructure

**Task 1: IntegrationRunner.Core in-memory 파이프라인 지원**
- REQ-UI-011 관련
- SimulatorPipeline에 ProcessInMemory() 메서드 추가
- 기존 file-based ProcessAsync() 유지 (backward compatibility)
- 각 계층 결과를 메모리로 전달하는 오버로드

**Task 2: PipelineDetectorClient 구현**
- REQ-UI-010, REQ-UI-011, REQ-UI-012 관련
- IDetectorClient 인터페이스 구현
- SimulatorPipeline in-memory 래핑
- Background Task 프레임 생성 루프
- FrameReceived 이벤트 발생
- 단위 테스트 작성 (TDD)

**Task 3: SimulatorControlViewModel 구현**
- REQ-UI-020 ~ REQ-UI-024 관련
- Panel/FPGA/MCU/Network 파라미터 바인딩 프로퍼티
- Start/Stop/Reset RelayCommand
- 파라미터 유효성 검사 로직
- MCU 버퍼 상태 폴링
- 단위 테스트 작성 (TDD)

#### Secondary Goal: Monitoring & Statistics

**Task 4: PipelineStatusViewModel 구현**
- REQ-UI-030 ~ REQ-UI-033 관련
- PipelineStatistics에서 계층별 데이터 추출
- 2Hz DispatcherTimer 폴링
- 상태 인디케이터 로직 (Green/Yellow/Red)
- NetworkChannelStats 바인딩
- 단위 테스트 작성 (TDD)

#### Final Goal: Scenario & Configuration

**Task 5: ScenarioRunner 서비스 + ScenarioRunnerViewModel 구현**
- REQ-UI-040 ~ REQ-UI-044 관련
- JSON 시나리오 로드/파싱
- 파이프라인 실행 및 결과 수집
- 진행률 리포팅
- 사전 정의 시나리오 번들 (IT01-IT19 동등)
- 단위 테스트 작성 (TDD)

**Task 6: 구성 관리 구현**
- REQ-UI-050 ~ REQ-UI-052 관련
- YAML 로드/저장 (detector_config.yaml)
- 구성 변경 시 파이프라인 반영
- 파라미터 범위 유효성 검사 UI 피드백

#### Optional Goal: UI Integration

**Task 7: XAML Views + MainWindow 통합**
- SimulatorControlView.xaml, PipelineStatusView.xaml, ScenarioRunnerView.xaml 작성
- MainWindow.xaml TabControl에 3개 탭 추가
- MainViewModel에 자식 ViewModel 연결
- App.xaml.cs 모드 선택 로직

---

### Architecture Design Direction

```
MainWindow (TabControl)
  +-- Tab 1: Frame Preview (기존 - FramePreviewViewModel)
  +-- Tab 2: Status Dashboard (기존 - StatusViewModel)
  +-- Tab 3: Simulator Control (신규 - SimulatorControlViewModel)
  +-- Tab 4: Pipeline Monitor (신규 - PipelineStatusViewModel)
  +-- Tab 5: Scenario Runner (신규 - ScenarioRunnerViewModel)
  +-- Tab 6: Configuration (신규 - SimulatorControlViewModel 확장 또는 별도)

MainViewModel
  +-- IDetectorClient (mode-switchable)
  |     +-- SimulatedDetectorClient (기존, 기본값)
  |     +-- PipelineDetectorClient (신규, Pipeline 모드)
  +-- SimulatorControlViewModel
  +-- PipelineStatusViewModel
  +-- ScenarioRunnerViewModel
```

**데이터 플로우 (Pipeline 모드):**
```
PipelineDetectorClient
  -> SimulatorPipeline.ProcessInMemory()
    -> PanelSimulator (kVp, mAs, noise, pattern)
    -> FpgaSimulator (CSI-2 framing)
    -> McuSimulator (UDP packetization)
    -> NetworkChannel (fault injection)
    -> Host reconstruction
  -> FrameReceived event
  -> FramePreviewViewModel (기존 렌더링)
```

---

### Risk Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| In-memory 파이프라인 성능 | 프레임 처리 지연 | ProcessInMemory()를 별도 메서드로 분리, 기존 메서드 불변 |
| Thread safety (UI 업데이트) | UI freeze 또는 crash | Dispatcher.Invoke() 패턴 사용 (기존 패턴 따름) |
| 기존 테스트 회귀 | 40개 테스트 실패 | 기존 IDetectorClient 구현 변경 없음, 새 구현 추가만 |
| SimulatorPipeline 의존성 | 프로젝트 참조 순환 | ProjectReference 단방향 (GUI -> IntegrationRunner.Core) |
| YAML 파싱 복잡도 | 구성 로드/저장 오류 | 기존 DetectorConfig 직렬화 패턴 재사용 |
| 시나리오 JSON 스키마 변경 | 호환성 문제 | System.Text.Json + 엄격한 스키마 검증 |

---

### Dependencies

- IntegrationRunner.Core: SimulatorPipeline, NetworkChannel, NetworkChannelConfig
- GUI.Application: IDetectorClient, ObservableObject, RelayCommand, DispatcherTimer
- IntegrationRunner.Core: PipelineStatistics (통계 데이터 모델)
- 기존 DetectorConfig, SimulationConfig 모델
