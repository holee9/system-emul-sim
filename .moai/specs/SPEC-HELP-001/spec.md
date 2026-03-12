---
id: SPEC-HELP-001
version: 2.0.0
status: planned
created: 2026-03-11
updated: 2026-03-11
author: MoAI (manager-spec)
priority: medium
milestone: M5
parent: SPEC-UI-001
tags: [GUI, WPF, Help, About, UX, Accessibility, MVVM]
---

## HISTORY

| Version | Date       | Author              | Description          |
|---------|------------|----------------------|----------------------|
| 1.0.0   | 2026-03-11 | MoAI (manager-spec) | Initial SPEC creation |
| 1.1.0   | 2026-03-11 | MoAI (cross-verify)  | Phase 5 추가: E2E Testing (FlaUI) + Structured Logging (Serilog) |
| 2.0.0   | 2026-03-11 | MoAI (batch-plan)    | Wave 일괄 실행 구조, 교차검증+Debug/Fix 사이클, decisions.md 추가 |

---

## Overview

### Scope

GUI.Application의 미구현 메뉴 아이템(Help > About, File > Exit, View > Status Bar, View > Full Screen)을 바인딩하고,
포괄적인 Help 시스템을 구축한다. About 다이얼로그, 내장형 도움말 뷰어, 파라미터 Rich ToolTip,
키보드 단축키 시스템, 첫 실행 Welcome 마법사를 포함한다.

모든 도움말 콘텐츠는 임베디드 리소스로 번들되어 오프라인 환경에서도 동작하며,
앱 버전과 동기화된다.

### Parent SPEC

SPEC-UI-001 (통합 에뮬레이터 GUI)

### Traceability

- Parent: SPEC-UI-001
- Related: SPEC-TOOLS-001 (기본 GUI), SPEC-EMUL-003 (시나리오 검증)

---

## Phase 1: About 다이얼로그 + 미바인드 메뉴 아이템

### REQ-HELP-010: About 다이얼로그

**WHEN** 사용자가 Help > About 메뉴를 클릭하면
**THEN** 시스템은 AboutWindow 다이얼로그를 모달로 표시한다.

About 다이얼로그는 다음 정보를 포함한다:

| Section             | Content                                                        |
|---------------------|----------------------------------------------------------------|
| Application Info    | 앱 이름, 버전 (Assembly에서 읽기), 빌드 날짜 (AssemblyMetadata) |
| Runtime Info        | .NET 버전, OS, CPU 코어 수, 가용 메모리                         |
| Pipeline Status     | Panel/FPGA/MCU/Host 연결 상태 시각화                            |
| Dependencies        | 임베디드 리소스로 빌드 시 생성된 의존성 목록                     |
| Actions             | "클립보드에 복사" 버튼 (버그 리포트용), GitHub 링크 (클릭 가능) |

### REQ-HELP-011: About 다이얼로그 MVVM 패턴

시스템은 **항상** AboutWindow.xaml + AboutViewModel.cs 구조를 유지해야 한다.

- AboutViewModel은 ObservableObject를 상속한다
- 모든 데이터는 ViewModel 프로퍼티로 바인딩된다
- CopyToClipboardCommand (RelayCommand)로 전체 시스템 정보를 클립보드에 복사한다
- OpenGitHubCommand (RelayCommand)로 Process.Start를 통해 GitHub URL을 연다

### REQ-HELP-012: 버전 정보 동적 로딩

시스템은 **항상** 버전 정보를 Assembly에서 동적으로 읽어야 한다.

- `Assembly.GetExecutingAssembly().GetName().Version` 으로 버전 획득
- `AssemblyMetadataAttribute("BuildDate", ...)` 으로 빌드 날짜 획득
- csproj에 `<AssemblyMetadata Include="BuildDate" Value="$([System.DateTime]::UtcNow.ToString('yyyy-MM-dd'))" />` 추가
- StatusBar의 하드코딩된 "v1.0.0"도 동일한 Assembly 버전으로 교체

### REQ-HELP-013: File > Exit 커맨드 바인딩

**WHEN** 사용자가 File > Exit 메뉴를 클릭하면
**THEN** 시스템은 `Application.Current.Shutdown()`을 호출하여 애플리케이션을 종료한다.

- MainViewModel에 ExitCommand (RelayCommand) 추가
- XAML에서 `Command="{Binding ExitCommand}"` 바인딩

### REQ-HELP-014: View > Status Bar 토글

**WHEN** 사용자가 View > Status Bar 메뉴를 토글하면
**THEN** 하단 StatusBar의 Visibility가 토글된다.

- MainViewModel에 `IsStatusBarVisible` (bool, 기본값 true) 프로퍼티 추가
- StatusBar에 `Visibility="{Binding IsStatusBarVisible, Converter={StaticResource BoolToVisibilityConverter}}"` 바인딩
- MenuItem의 `IsChecked="{Binding IsStatusBarVisible}"` 바인딩

### REQ-HELP-015: View > Full Screen 토글

**WHEN** 사용자가 View > Full Screen 메뉴를 클릭하거나 F11을 누르면
**THEN** 윈도우가 전체 화면 모드로 전환된다.

- MainViewModel에 `IsFullScreen` (bool) 프로퍼티 추가
- FullScreenCommand (RelayCommand) 추가
- 전체 화면: `WindowState = Maximized`, `WindowStyle = None`, `ResizeMode = NoResize`
- 일반 화면: 이전 `WindowState`, `WindowStyle`, `ResizeMode` 복원
- MainWindow.xaml.cs에서 ViewModel 프로퍼티 변경 시 Window 속성 업데이트

---

## Phase 2: Help 시스템 인프라

### REQ-HELP-020: HelpProvider AttachedProperty

시스템은 **항상** `HelpProvider.HelpTopicId` AttachedProperty를 제공해야 한다.

- `FrameworkPropertyMetadataOptions.Inherits` 설정으로 자식 컨트롤에 자동 전파
- 각 주요 UI 영역에 HelpTopicId를 지정한다
- 예: `views:HelpProvider.HelpTopicId="panel-simulation"` on Simulator Control tab

### REQ-HELP-021: F1 전역 핸들러

**WHEN** 사용자가 F1 키를 누르면
**THEN** 시스템은 현재 포커스된 컨트롤의 HelpTopicId에 해당하는 도움말 페이지를 HelpWindow에서 연다.

- App.xaml.cs에서 `ApplicationCommands.Help` CommandBinding 등록
- 포커스된 컨트롤에서 HelpTopicId를 탐색 (자식에서 부모로)
- HelpTopicId가 없으면 overview 페이지를 표시

### REQ-HELP-022: IHelpContentService 인터페이스

시스템은 **항상** `IHelpContentService` 인터페이스를 통해 도움말 콘텐츠에 접근해야 한다.

```
interface IHelpContentService
{
    IReadOnlyList<HelpTopic> GetTopics();
    HelpTopic? GetTopic(string topicId);
    string GetContent(string topicId);  // Returns Markdown string
}
```

- `EmbeddedHelpContentService` 구현: 임베디드 리소스에서 Markdown 로드
- 토픽 트리 구조를 지원 (부모-자식 관계)

### REQ-HELP-023: HelpWindow

**WHEN** 도움말이 요청되면
**THEN** 시스템은 HelpWindow를 표시한다.

- 좌측: TreeView 네비게이션 (토픽 계층 구조)
- 우측: FlowDocumentScrollViewer (Markdown 렌더링 결과)
- TreeView 선택 변경 시 우측 콘텐츠 갱신
- 검색 기능 (토픽 제목 기반 필터링)

### REQ-HELP-024: Markdown 렌더링

시스템은 **항상** Markdig NuGet 패키지를 사용하여 Markdown을 FlowDocument로 변환해야 한다.

- Markdig 파이프라인: Tables, EmphasisExtras, TaskLists 확장 활성화
- Markdown -> HTML -> FlowDocument 변환 체인
- 코드 블록은 고정폭 폰트로 표시
- 링크는 클릭 시 외부 브라우저에서 열기
- WebView2 의존성 없이 FlowDocument만 사용

### REQ-HELP-025: Help 콘텐츠 임베디드 리소스 구조

시스템은 **항상** 다음 디렉터리 구조로 도움말 콘텐츠를 관리해야 한다:

```
Help/Topics/
  overview.md
  getting-started.md
  panel-simulation.md
  fpga-csi2.md
  mcu-udp.md
  host-pipeline.md
  parameters-ref.md
  keyboard-shortcuts.md
  troubleshooting.md
```

- csproj에 `<EmbeddedResource Include="Help\Topics\**\*.md" />` 추가
- 빌드 시 자동으로 어셈블리에 포함

---

## Phase 3: Help 콘텐츠 (Markdown)

### REQ-HELP-030: 도움말 문서 작성

시스템은 **항상** 다음 도움말 문서를 한국어로 제공해야 한다. (기술 용어는 영어 보존)

| File                  | Title                  | Description                                         |
|-----------------------|------------------------|-----------------------------------------------------|
| overview.md           | 시스템 개요            | 4계층 파이프라인 아키텍처 설명, 구성 다이어그램       |
| getting-started.md    | 빠른 시작 가이드        | 10분 퀵스타트, 기본 사용법                           |
| panel-simulation.md   | Panel 시뮬레이션       | kVp, mAs, NoiseType, DefectRate 파라미터 설명        |
| fpga-csi2.md          | FPGA/CSI-2 처리        | CSI-2 프로토콜, 프레임 버퍼 동작 설명                |
| mcu-udp.md            | MCU/UDP 통신           | UDP 패킷 처리, 패킷 손실/재정렬 시뮬레이션            |
| host-pipeline.md      | Host 파이프라인        | 프레임 수신, 실시간 표시 메커니즘                     |
| parameters-ref.md     | 파라미터 레퍼런스       | 모든 파라미터 완전 참조 테이블 (이름, 타입, 범위, 설명)|
| keyboard-shortcuts.md | 키보드 단축키           | 전체 단축키 참조                                     |
| troubleshooting.md    | 문제 해결              | FAQ + 일반적인 문제와 해결 방법                       |

### REQ-HELP-031: 도움말 콘텐츠 버전 동기화

시스템은 **항상** 도움말 콘텐츠가 임베디드 리소스로 포함되어 앱 버전과 동기화되어야 한다.

- 외부 파일 의존성 없음 (오프라인 동작 보장)
- 업데이트는 앱 릴리즈 주기와 동일

---

## Phase 4: UX 향상

### REQ-HELP-040: Rich ToolTip

**WHEN** 사용자가 시뮬레이션 파라미터 컨트롤 위에 마우스를 올리면
**THEN** 시스템은 3줄 Rich ToolTip을 표시한다.

| Line | Content                                                  |
|------|----------------------------------------------------------|
| 1    | 파라미터 이름 (Bold)                                     |
| 2    | 범위/설명 (예: "Range: 40-150 kV")                       |
| 3    | 물리적 의미 (예: "X선관의 가속 전압으로 투과력을 결정") |

대상 파라미터:
- kVp, mAs, DefectRate, PacketLossRate, ReorderRate, CorruptionRate
- CSI-2 Lanes, Data Rate, Line Buffer Depth
- Frame Buffer Count, UDP Port

### REQ-HELP-041: StatusBar 힌트

**WHEN** 컨트롤이 포커스를 받으면
**THEN** StatusBar에 해당 컨트롤의 컨텍스트 힌트를 표시한다.

- 각 주요 컨트롤에 `Tag` 속성으로 힌트 메시지 저장
- GotFocus/LostFocus 이벤트로 StatusMessage 업데이트
- ToolTip과 중복되지 않는 보충 정보 제공

### REQ-HELP-042: 키보드 단축키

시스템은 **항상** 다음 키보드 단축키를 지원해야 한다:

| Shortcut    | Action                    |
|-------------|---------------------------|
| Ctrl+R      | 파이프라인 Start (Run)    |
| Ctrl+S      | 파이프라인 Stop           |
| Alt+1~6     | 탭 전환 (1=Status ~ 6=Scenario) |
| F1          | 도움말 (컨텍스트)         |
| F11         | 전체 화면 토글            |
| Ctrl+/      | 단축키 오버레이 표시      |
| Ctrl+Q      | 종료 (Exit)               |

- MainWindow에 `InputBindings` + `KeyBinding` 으로 구현
- Ctrl+/ 오버레이: 반투명 패널로 전체 단축키 목록 표시, 아무 키 누르면 닫힘

### REQ-HELP-043: First-Run Welcome 마법사

**IF** 애플리케이션 최초 실행인 경우
**THEN** 시스템은 3단계 Welcome 마법사를 표시한다.

| Step | Title         | Content                                          |
|------|---------------|--------------------------------------------------|
| 1    | 소개          | 앱 소개, 4계층 파이프라인 개요 다이어그램          |
| 2    | 기본 구성     | 주요 파라미터 설정 (kVp, mAs, 해상도)             |
| 3    | 첫 시뮬레이션  | 기본값으로 파이프라인 시작, 결과 확인              |

- 최초 실행 감지: `%LOCALAPPDATA%/XrayDetector/settings.json` 에 `firstRunCompleted: true` 저장
- "다시 보지 않기" 체크박스 제공
- 마법사 완료 시 자동으로 기본 시뮬레이션 시작

---

## Phase 5: E2E Testing + Structured Logging

### Phase 5A: E2E Test Infrastructure (FlaUI)

#### REQ-HELP-050: FlaUI E2E Test Project

시스템은 **항상** FlaUI.UIA3 기반의 E2E 테스트 프로젝트를 별도로 유지해야 한다.

- 프로젝트: `tools/GUI.Application/tests/GUI.Application.E2ETests/`
- NuGet: FlaUI.UIA3 4.0.0, FlaUI.Core 4.0.0
- 테스트 프레임워크: xUnit 2.9.3 + FluentAssertions 7.0.0
- `xunit.maxParallelThreads=1` (직렬 실행, UI 경합 방지)

#### REQ-HELP-051: AppFixture (Process Lifecycle)

시스템은 **항상** `AppFixture` 클래스로 GUI 프로세스 생명주기를 관리해야 한다.

- `IAsyncLifetime` 구현
- `InitializeAsync()`: GUI.Application.exe 프로세스 시작, UIA3Automation 초기화, 메인 윈도우 대기
- `DisposeAsync()`: 프로세스 종료, 리소스 정리
- 앱 시작 대기: 최대 30초 timeout
- 환경 변수 `XRAY_E2E_MODE=true` 설정 (Welcome Wizard 억제용)

#### REQ-HELP-052: Page Object Pattern

시스템은 **항상** Page Object 패턴으로 E2E 테스트를 구조화해야 한다.

| Page Object | 대상 | 주요 메서드 |
|-------------|------|------------|
| `MainWindowPage` | MainWindow | ClickMenu(), GetStatusBarText(), GetActiveTab() |
| `AboutDialogPage` | AboutWindow | GetVersion(), ClickCopyToClipboard(), Close() |
| `HelpWindowPage` | HelpWindow | SelectTopic(), GetContent(), Search() |
| `SimulatorControlPage` | SimulatorControlView | SetKvp(), GetToolTipText(), StartSimulation() |

#### REQ-HELP-053: AutomationId 요구사항

**WHEN** FlaUI E2E 테스트에서 UI 요소를 식별할 때
**THEN** 시스템은 `AutomationProperties.AutomationId` 속성을 사용해야 한다.

필수 AutomationId 대상:
- 메뉴 아이템: `MenuFileExit`, `MenuHelpAbout`, `MenuViewStatusBar`, `MenuViewFullScreen`
- StatusBar 버전: `StatusBarVersion`
- 탭: `TabStatus`, `TabImage`, `TabConfig`, `TabNetwork`, `TabLog`, `TabScenario`
- 파라미터 입력: `InputKvp`, `InputMas`, `InputRows`, `InputCols`
- 버튼: `BtnStart`, `BtnStop`, `BtnReset`, `BtnLoadConfig`, `BtnSaveConfig`

#### REQ-HELP-054: E2E Test Scenarios

시스템은 다음 E2E 테스트 시나리오를 포함해야 한다:

**Smoke Tests (앱 시작/종료):**
- App launches and main window is visible
- StatusBar displays dynamic version (not "v1.0.0")
- All 6 tabs are accessible
- File > Exit closes application

**SPEC-HELP-001 Feature Tests:**
- Help > About opens modal dialog with correct info
- About dialog Copy to Clipboard works
- F1 opens HelpWindow (Phase 2 이후)
- HelpWindow topic navigation works (Phase 2 이후)
- Rich ToolTip displays on parameter hover (Phase 4 이후)

**Core Flow Tests:**
- Start/Stop acquisition via menu
- Tab switching via Alt+1~6 (Phase 4 이후)
- Full Screen toggle via F11 (Phase 1 이후)

#### REQ-HELP-055: E2E Failure Diagnostics

**WHEN** E2E 테스트가 실패하면
**THEN** 시스템은 다음 진단 정보를 수집해야 한다:

- Screenshot 캡처 (`TestResults/Screenshots/{TestName}_{Timestamp}.png`)
- 애플리케이션 로그 덤프 (InMemoryLogSink에서 추출)
- UI Automation 트리 스냅샷

#### REQ-HELP-056: Retry Policy

시스템은 **항상** 불안정한 E2E 테스트에 대해 최대 2회 재시도를 허용해야 한다.

- `[RetryFact(MaxRetries = 2)]` 커스텀 속성 사용
- 재시도 간 500ms 대기
- 3회 연속 실패 시 테스트 실패로 확정

### Phase 5B: Structured Logging (Serilog)

#### REQ-HELP-057: Serilog Integration

시스템은 **항상** Serilog를 구조화 로깅 프레임워크로 사용해야 한다.

- NuGet: Serilog 4.2.0, Serilog.Sinks.File, Serilog.Sinks.Async, Serilog.Enrichers.Thread
- 기존 `Debug.WriteLine` 호출을 `Log.Information/Warning/Error`로 교체
- App.xaml.cs `OnStartup`에서 LoggingBootstrap 초기화

#### REQ-HELP-058: Log Categories

시스템은 다음 로그 카테고리를 사용해야 한다:

| Category | SourceContext | 용도 |
|----------|--------------|------|
| Pipeline | `XrayDetector.Gui.Pipeline` | 파이프라인 시작/정지/프레임 처리 |
| UI | `XrayDetector.Gui.UI` | 뷰 전환, 다이얼로그 열기/닫기 |
| Performance | `XrayDetector.Gui.Performance` | FPS, 렌더링 latency, 메모리 |
| UserAction | `XrayDetector.Gui.UserAction` | 메뉴 클릭, 버튼 클릭, 단축키 |
| Help | `XrayDetector.Gui.Help` | 도움말 토픽 열기, 검색 |
| App | `XrayDetector.Gui.App` | 시작, 종료, 예외 처리 |

#### REQ-HELP-059: InMemoryLogSink (E2E Test Support)

**WHEN** E2E 테스트 모드(`XRAY_E2E_MODE=true`)에서 실행될 때
**THEN** 시스템은 InMemoryLogSink를 활성화하여 로그 기반 assertion을 지원해야 한다.

- Thread-safe ConcurrentQueue 기반
- 최대 10,000 이벤트 바운드 (FIFO overflow)
- LINQ 쿼리 지원: `GetEvents(category, level, timeRange)`
- E2E 테스트에서 `LogAssertions.ShouldHaveNoErrors()` 등의 헬퍼 사용

### Phase 5C: CI/CD Integration

#### REQ-HELP-05A: E2E CI Workflow

시스템은 **항상** Windows 환경에서 E2E 테스트를 실행하는 별도 CI workflow를 유지해야 한다.

- `.github/workflows/e2e-tests.yml`
- Runner: `windows-latest`
- Trigger: `push` to main, `pull_request` to main
- Steps: Build → Start GUI (background) → Run E2E tests → Upload screenshots on failure
- 기존 `ci.yml` (ubuntu-latest) 과 분리

---

## Unwanted Requirements

### REQ-HELP-060: WebView2 의존성 금지

시스템은 WebView2를 도움말 렌더링에 사용**하지 않아야 한다**.
FlowDocument와 Markdig만을 사용한다.

### REQ-HELP-061: 외부 도움말 서버 금지

시스템은 외부 서버에서 도움말 콘텐츠를 로드**하지 않아야 한다**.
모든 콘텐츠는 임베디드 리소스로만 제공된다.

### REQ-HELP-062: 자동 업데이트 금지

시스템은 도움말 콘텐츠의 자동 업데이트 메커니즘을 구현**하지 않아야 한다**.

---

## Constraints

- C# 12 / .NET 8.0-windows / WPF
- 기존 MVVM 패턴 유지 (ObservableObject, SetField, RelayCommand)
- 기존 83개 단위 테스트 통과 유지 (하위 호환성)
- 한국어 사용자 텍스트, 영어 기술 용어 보존 (kVp, mAs, CSI-2, UDP, FPGA, MCU)
- 추가 NuGet: Markdig (FlowDocument 렌더링), Serilog (구조화 로깅), FlaUI.UIA3 (E2E 테스트)
- 오프라인 우선 (모든 콘텐츠 임베디드)
- WebView2 의존성 없음

---
