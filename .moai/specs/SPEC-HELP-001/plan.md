---
id: SPEC-HELP-001
type: plan
version: 2.0.0
status: planned
created: 2026-03-11
updated: 2026-03-11
---

## Implementation Plan

### Dependency Analysis

#### NuGet Additions

| Package | Version | Target Project | Purpose |
|---------|---------|----------------|---------|
| Markdig | 0.37.0+ | GUI.Application | Markdown -> HTML conversion |
| Serilog | 4.2.0 | GUI.Application | Structured logging core |
| Serilog.Sinks.File | 6.0.0 | GUI.Application | File logging sink |
| Serilog.Sinks.Async | 2.1.0 | GUI.Application | Async wrapper for sinks |
| Serilog.Enrichers.Thread | 4.0.0 | GUI.Application | Thread ID enrichment |
| FlaUI.UIA3 | 4.0.0 | GUI.Application.E2ETests | WPF UI Automation |
| FlaUI.Core | 4.0.0 | GUI.Application.E2ETests | FlaUI core framework |

#### Project Reference Changes

- None (기존 IntegrationRunner.Core 참조 유지)

#### csproj Modifications (GUI.Application.csproj)

- `<PackageReference Include="Markdig" />` 추가
- `<PackageReference Include="Serilog" />` + Sinks 3종 추가
- `<AssemblyMetadata Include="BuildDate" .../>` 추가
- `<EmbeddedResource Include="Help\Topics\**\*.md" />` 추가

---

### Phase 1: About Dialog + Unbound Menu Items

**Priority: High (Primary Goal)**

#### New Files

| File | Type | Description |
|------|------|-------------|
| `Views/AboutWindow.xaml` | View | About 다이얼로그 XAML |
| `Views/AboutWindow.xaml.cs` | Code-behind | 모달 다이얼로그 표시 로직 |
| `ViewModels/AboutViewModel.cs` | ViewModel | About 정보 수집 및 바인딩 |
| `Core/BoolToVisibilityConverter.cs` | Converter | bool -> Visibility 변환 |

#### Modified Files

| File | Changes |
|------|---------|
| `GUI.Application.csproj` | AssemblyMetadata(BuildDate) 추가 |
| `ViewModels/MainViewModel.cs` | ExitCommand, ToggleStatusBarCommand, FullScreenCommand, ShowAboutCommand, IsStatusBarVisible, IsFullScreen 프로퍼티 추가 |
| `Views/MainWindow.xaml` | Exit/StatusBar/FullScreen/About 메뉴 Command 바인딩, StatusBar Visibility 바인딩, StatusBar 버전을 Assembly 버전으로 교체, BoolToVisibilityConverter 리소스 등록 |
| `Views/MainWindow.xaml.cs` | Full Screen 토글 시 WindowState/WindowStyle 변경 처리, F11 KeyBinding |

#### Implementation Approach

1. **BoolToVisibilityConverter** 생성 -- IValueConverter 구현
2. **AboutViewModel** 생성:
   - Assembly 버전, 빌드 날짜 읽기
   - `Environment.Version`, `Environment.OSVersion`, `Environment.ProcessorCount`, `GC.GetGCMemoryInfo()` 수집
   - `CopyToClipboardCommand`: 전체 정보를 텍스트 형식으로 Clipboard에 복사
   - `OpenGitHubCommand`: `Process.Start(new ProcessStartInfo { UseShellExecute = true, FileName = url })`
3. **AboutWindow.xaml** 생성:
   - Grid 레이아웃, 앱 아이콘, 정보 섹션, 버튼 영역
   - 파이프라인 상태 표시 (4개 색상 인디케이터)
4. **MainViewModel** 수정:
   - `ExitCommand = new RelayCommand(() => Application.Current.Shutdown())`
   - `IsStatusBarVisible` bool 프로퍼티
   - `IsFullScreen` bool 프로퍼티
   - `ShowAboutCommand = new RelayCommand(OnShowAbout)`
   - `ToggleFullScreenCommand = new RelayCommand(OnToggleFullScreen)`
5. **MainWindow.xaml** 수정:
   - 메뉴 아이템 Command 바인딩
   - StatusBar `Visibility` 바인딩
   - 버전 텍스트를 `{Binding AppVersion}` 으로 교체
6. **csproj** 수정:
   - `<AssemblyMetadata Include="BuildDate" Value="$([System.DateTime]::UtcNow.ToString('yyyy-MM-dd'))" />`

---

### Phase 2: Help System Infrastructure

**Priority: Medium (Secondary Goal)**

#### New Files

| File | Type | Description |
|------|------|-------------|
| `Help/IHelpContentService.cs` | Interface | 도움말 콘텐츠 서비스 인터페이스 |
| `Help/HelpTopic.cs` | Model | 도움말 토픽 데이터 모델 |
| `Help/EmbeddedHelpContentService.cs` | Service | 임베디드 리소스에서 Markdown 로드 |
| `Help/HelpProvider.cs` | AttachedProperty | HelpTopicId 첨부 속성 |
| `Help/MarkdownToFlowDocumentConverter.cs` | Converter | Markdig를 사용한 Markdown -> FlowDocument 변환 |
| `Views/HelpWindow.xaml` | View | 도움말 뷰어 윈도우 |
| `Views/HelpWindow.xaml.cs` | Code-behind | TreeView 선택 및 검색 처리 |
| `ViewModels/HelpViewModel.cs` | ViewModel | 도움말 토픽 트리 및 콘텐츠 관리 |

#### Modified Files

| File | Changes |
|------|---------|
| `GUI.Application.csproj` | Markdig PackageReference 추가, EmbeddedResource glob 추가 |
| `App.xaml.cs` | ApplicationCommands.Help 바인딩 (F1 핸들러) |
| `Views/MainWindow.xaml` | Help > Topics 메뉴 추가, HelpProvider.HelpTopicId 첨부 |

#### Implementation Approach

1. **HelpTopic** 모델: `string Id`, `string Title`, `string? ParentId`, `List<HelpTopic> Children`
2. **IHelpContentService** 인터페이스 정의
3. **EmbeddedHelpContentService** 구현:
   - `Assembly.GetManifestResourceNames()` 로 `Help.Topics.*.md` 스캔
   - 파일명에서 토픽 ID 추출
   - 토픽 메타데이터는 Markdown frontmatter 또는 하드코딩된 매핑
4. **HelpProvider** AttachedProperty:
   - `DependencyProperty.RegisterAttached("HelpTopicId", typeof(string), typeof(HelpProvider), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits))`
5. **MarkdownToFlowDocumentConverter**:
   - Markdig Pipeline 구성 (Tables, EmphasisExtras, TaskLists)
   - Markdown -> HTML (Markdig)
   - HTML -> FlowDocument (수동 파싱 또는 간단한 XAML 변환)
   - 코드 블록: Consolas 폰트, 회색 배경
   - 링크: Hyperlink 요소로 `Process.Start`
6. **HelpViewModel**: Topics 트리 로드, 선택된 토픽 콘텐츠 표시
7. **HelpWindow.xaml**: TreeView + FlowDocumentScrollViewer 분할 레이아웃
8. **F1 핸들러**: `Keyboard.FocusedElement`에서 HelpProvider.GetHelpTopicId 탐색

---

### Phase 3: Help Contents (Markdown)

**Priority: Medium (Secondary Goal)**

#### New Files

| File | Type | Description |
|------|------|-------------|
| `Help/Topics/overview.md` | Content | 시스템 개요 |
| `Help/Topics/getting-started.md` | Content | 빠른 시작 가이드 |
| `Help/Topics/panel-simulation.md` | Content | Panel 시뮬레이션 설명 |
| `Help/Topics/fpga-csi2.md` | Content | FPGA/CSI-2 처리 설명 |
| `Help/Topics/mcu-udp.md` | Content | MCU/UDP 통신 설명 |
| `Help/Topics/host-pipeline.md` | Content | Host 파이프라인 설명 |
| `Help/Topics/parameters-ref.md` | Content | 파라미터 완전 참조 테이블 |
| `Help/Topics/keyboard-shortcuts.md` | Content | 키보드 단축키 참조 |
| `Help/Topics/troubleshooting.md` | Content | FAQ 및 문제 해결 |

#### Implementation Approach

1. 각 Markdown 파일을 한국어로 작성 (기술 용어 영어 보존)
2. 파이프라인 구조 다이어그램은 ASCII 아트 또는 텍스트 기반
3. 파라미터 참조 테이블은 Markdown 테이블 형식
4. 모든 파일을 csproj에서 EmbeddedResource로 포함

---

### Phase 4: UX Enhancement

**Priority: Low (Optional Goal)**

#### New Files

| File | Type | Description |
|------|------|-------------|
| `Help/ParameterTooltips.cs` | Resource | 파라미터별 ToolTip 데이터 (데이터 드리븐) |
| `Views/KeyboardShortcutOverlay.xaml` | View | Ctrl+/ 단축키 오버레이 |
| `Views/WelcomeWizardWindow.xaml` | View | 첫 실행 마법사 |
| `Views/WelcomeWizardWindow.xaml.cs` | Code-behind | 마법사 스텝 관리 |
| `ViewModels/WelcomeWizardViewModel.cs` | ViewModel | 마법사 상태 및 구성 |
| `Core/FirstRunManager.cs` | Service | 최초 실행 감지 및 설정 파일 관리 |

#### Modified Files

| File | Changes |
|------|---------|
| `Views/MainWindow.xaml` | InputBindings (Ctrl+R, Ctrl+S, Alt+1~6, Ctrl+/, Ctrl+Q), 단축키 오버레이 Popup |
| `Views/SimulatorControlView.xaml` | 파라미터 컨트롤에 Rich ToolTip 추가 |
| `Views/MainWindow.xaml.cs` | 단축키 오버레이 표시/숨김 로직 |
| `App.xaml.cs` | 최초 실행 감지 및 Welcome 마법사 표시 |

#### Implementation Approach

1. **ParameterTooltips**: Dictionary<string, ParameterTooltipInfo> 정적 데이터
   - `record ParameterTooltipInfo(string Name, string RangeDescription, string PhysicalMeaning)`
   - XAML에서 ToolTip 템플릿 사용
2. **StatusBar 힌트**: GotFocus/LostFocus 이벤트 → MainViewModel.StatusMessage 업데이트
3. **키보드 단축키**:
   - MainWindow.xaml에 `<Window.InputBindings>` 추가
   - `<KeyBinding Key="R" Modifiers="Ctrl" Command="{Binding StartPipelineCommand}" />`
   - Alt+1~6: TabControl.SelectedIndex 바인딩
   - Ctrl+/: 오버레이 Popup 토글
4. **Welcome 마법사**:
   - `FirstRunManager.IsFirstRun()` 확인 (`%LOCALAPPDATA%/XrayDetector/settings.json`)
   - 3개 UserControl 페이지를 ContentControl에서 전환
   - 완료 시 `firstRunCompleted: true` 저장

---

### Phase 5: E2E Testing + Structured Logging

**Priority: Medium (Quality Assurance)**

#### Phase 5A: E2E Test Infrastructure

##### New Files

| File | Type | Description |
|------|------|-------------|
| `tests/GUI.Application.E2ETests/GUI.Application.E2ETests.csproj` | Project | E2E 테스트 프로젝트 |
| `tests/GUI.Application.E2ETests/Infrastructure/AppFixture.cs` | Fixture | GUI 프로세스 생명주기 관리 |
| `tests/GUI.Application.E2ETests/Infrastructure/WaitHelper.cs` | Helper | 비동기 폴링 대기 유틸리티 |
| `tests/GUI.Application.E2ETests/Infrastructure/ScreenshotHelper.cs` | Helper | 실패 시 스크린샷 캡처 |
| `tests/GUI.Application.E2ETests/Infrastructure/RetryFactAttribute.cs` | Attribute | Flaky 테스트 재시도 (최대 2회) |
| `tests/GUI.Application.E2ETests/Infrastructure/E2ETestBase.cs` | Base Class | 공통 setup/teardown |
| `tests/GUI.Application.E2ETests/PageObjects/MainWindowPage.cs` | Page Object | MainWindow UI 추상화 |
| `tests/GUI.Application.E2ETests/PageObjects/AboutDialogPage.cs` | Page Object | AboutWindow UI 추상화 |
| `tests/GUI.Application.E2ETests/PageObjects/HelpWindowPage.cs` | Page Object | HelpWindow UI 추상화 |
| `tests/GUI.Application.E2ETests/PageObjects/SimulatorControlPage.cs` | Page Object | SimulatorControlView UI 추상화 |
| `tests/GUI.Application.E2ETests/Smoke/AppLaunchTests.cs` | Test | Smoke: 앱 시작/종료 4건 |
| `tests/GUI.Application.E2ETests/Features/AboutDialogE2ETests.cs` | Test | About 다이얼로그 E2E 3건 |
| `tests/GUI.Application.E2ETests/Features/HelpSystemE2ETests.cs` | Test | Help 시스템 E2E 4건 |
| `tests/GUI.Application.E2ETests/Features/CoreFlowE2ETests.cs` | Test | 핵심 흐름 E2E 4건 |
| `tests/GUI.Application.E2ETests/xunit.runner.json` | Config | `maxParallelThreads: 1` |

##### Modified Files

| File | Changes |
|------|---------|
| `Views/MainWindow.xaml` | 모든 테스트 대상 컨트롤에 `AutomationProperties.AutomationId` 추가 |
| `Views/SimulatorControlView.xaml` | 파라미터 입력 필드에 AutomationId 추가 |
| `Views/AboutWindow.xaml` | About 다이얼로그 요소에 AutomationId 추가 (Phase 1과 동시 진행) |

#### Phase 5B: Structured Logging

##### New Files

| File | Type | Description |
|------|------|-------------|
| `Logging/LogCategories.cs` | Constants | 로그 카테고리 상수 정의 |
| `Logging/LoggingBootstrap.cs` | Bootstrap | Serilog 초기화 (File + Console + InMemory sinks) |
| `Logging/InMemoryLogSink.cs` | Sink | E2E 테스트용 in-memory 로그 저장소 (thread-safe, 10K bound) |
| `Logging/LogAssertions.cs` | Helper | E2E 테스트용 로그 assertion 헬퍼 |

##### Modified Files

| File | Changes |
|------|---------|
| `GUI.Application.csproj` | Serilog NuGet 패키지 4종 추가 |
| `App.xaml.cs` | `LoggingBootstrap.Initialize()` 호출, `Debug.WriteLine` → `Log.*` 교체 |
| `ViewModels/MainViewModel.cs` | 주요 Command 실행 시 `UserAction` 카테고리 로깅 추가 |

#### Phase 5C: CI/CD

##### New Files

| File | Type | Description |
|------|------|-------------|
| `.github/workflows/e2e-tests.yml` | CI | Windows E2E 테스트 workflow |

##### Implementation Approach

1. **E2E 프로젝트 생성**: xUnit + FlaUI.UIA3 + FluentAssertions
2. **AppFixture**: `Process.Start` → `UIA3Automation` → `Application.GetMainWindow` → `WaitWhileBusy`
3. **Page Objects**: 각 Window/View에 대한 UI 추상화 계층
4. **AutomationId 추가**: 테스트 대상 XAML 컨트롤에 `AutomationProperties.AutomationId` 속성
5. **Serilog 설정**: `LoggerConfiguration().WriteTo.File().WriteTo.Sink<InMemoryLogSink>().CreateLogger()`
6. **InMemoryLogSink**: `ConcurrentQueue<LogEvent>`, 10K 바운드, FIFO overflow
7. **LogAssertions**: `ShouldHaveNoErrors()`, `ShouldEventuallyLogAsync()` 메서드
8. **E2E CI**: `windows-latest`, Build → dotnet publish → Start background → dotnet test → Upload artifacts

#### Phase 5 Implementation Order

```
Phase 5B (Logging) → Phase 5A (E2E Infra) → Phase 5C (CI)
                  ↑                       ↑
            로깅 먼저 구축        로그 기반 assertion 활용
```

- Phase 5B를 먼저 구현: E2E에서 InMemoryLogSink 활용을 위해
- Phase 5A는 Phase 1 완료 후 점진 확장 (Phase별 E2E 추가)
- Phase 5C는 E2E 테스트 안정화 후

---

### Risk Assessment

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Markdig FlowDocument 변환 복잡성 | Medium | High | 단순한 Markdown 서브셋만 지원 (heading, paragraph, table, code, list, link). 복잡한 HTML은 미지원 |
| 기존 83개 테스트 회귀 | High | Low | Phase 1에서 기존 ViewModel 인터페이스 변경 없이 추가만 수행. InitializeCommands에 새 커맨드 추가 |
| 전체 화면 모드에서 WindowStyle 복원 실패 | Low | Medium | 전환 전 이전 상태 저장. try-catch로 복원 실패 시 기본값 사용 |
| 임베디드 리소스 로딩 실패 | Medium | Low | fallback 텍스트 제공. Assembly.GetManifestResourceStream null 체크 |
| 대용량 Markdown 렌더링 성능 | Low | Low | 도움말 문서는 각각 100줄 이하로 제한. 캐싱 적용 |
| FlaUI UI 요소 탐색 실패 | Medium | Medium | AutomationId로 안정적 식별, WaitHelper로 비동기 대기, RetryFact로 재시도 |
| E2E 테스트 Flaky | Medium | High | 직렬 실행(maxParallelThreads=1), 최대 2회 재시도, 500ms 대기 |
| Serilog 메모리 사용량 | Low | Low | InMemoryLogSink 10K 바운드, E2E 모드에서만 활성화 |
| Windows CI Runner 가용성 | Low | Low | GitHub Actions windows-latest 안정적 제공 |

---

### File Summary

#### New Files (Total: ~48)

| Phase | Files | Description |
|-------|-------|-------------|
| Phase 1 | 4 | AboutWindow (View+ViewModel), BoolToVisibilityConverter |
| Phase 2 | 8 | Help 인프라 (Service, Provider, Converter, Window, ViewModel) |
| Phase 3 | 9 | Help 콘텐츠 Markdown 파일 |
| Phase 4 | 6 | ToolTip, Overlay, Welcome Wizard, FirstRunManager |
| Phase 5A | 15 | E2E 프로젝트, Infrastructure, PageObjects, Tests, CI |
| Phase 5B | 4 | Serilog Logging (Bootstrap, Sink, Assertions, Categories) |
| Phase 5C | 1 | E2E CI workflow |

#### Modified Files (Total: ~10)

| File | Phases |
|------|--------|
| `GUI.Application.csproj` | 1, 2, 5B |
| `ViewModels/MainViewModel.cs` | 1, 4, 5B |
| `Views/MainWindow.xaml` | 1, 2, 4, 5A |
| `Views/MainWindow.xaml.cs` | 1, 4 |
| `App.xaml.cs` | 2, 4, 5B |
| `Views/SimulatorControlView.xaml` | 4, 5A |
| `App.xaml` | 1 (BoolToVisibilityConverter 리소스) |
| `Views/AboutWindow.xaml` | 1, 5A (AutomationId) |

---

### Architecture Design Direction

```
MainViewModel
  +-- AboutViewModel (Phase 1)
  +-- HelpViewModel (Phase 2)
  +-- WelcomeWizardViewModel (Phase 4)
  |
  +-- ExitCommand
  +-- ToggleStatusBarCommand (IsStatusBarVisible)
  +-- ToggleFullScreenCommand (IsFullScreen)
  +-- ShowAboutCommand
  +-- ShowHelpCommand
  +-- ShowShortcutOverlayCommand

Help Infrastructure:
  IHelpContentService <-- EmbeddedHelpContentService
  HelpProvider (AttachedProperty)
  MarkdownToFlowDocumentConverter (Markdig)

Support:
  BoolToVisibilityConverter
  FirstRunManager
  ParameterTooltips (data-driven)
```

---

### Batch Execution Plan (일괄 실행 계획)

전체 SPEC을 3개 Wave로 묶어 한 번에 실행합니다.
각 Wave는 **구현 → 단위테스트 → 빌드검증 → 교차검증 → Debug/Fix** 사이클을 포함합니다.

#### Wave 구조

```
┌─────────────────────────────────────────────────────────────────┐
│ Wave 1: Foundation (Phase 1 + 5B)                               │
│   구현 → 단위테스트 → 빌드검증 → 교차검증 → Debug/Fix          │
├─────────────────────────────────────────────────────────────────┤
│ Wave 2: Help System (Phase 2 + 3 + 4)                           │
│   구현 → 단위테스트 → 빌드검증 → 교차검증 → Debug/Fix          │
├─────────────────────────────────────────────────────────────────┤
│ Wave 3: E2E Validation (Phase 5A + 5C)                          │
│   구현 → E2E 실행 → 빌드검증 → 교차검증 → Debug/Fix → 최종검증 │
└─────────────────────────────────────────────────────────────────┘
```

#### Wave 1: Foundation (Phase 1 + 5B)

**범위**: About Dialog, 미바인드 메뉴 4종, Serilog 로깅 인프라
**의존성**: 없음 (독립 실행 가능)
**예상 파일**: 신규 8, 수정 6

| Step | Action | Agent | 성공 기준 |
|------|--------|-------|----------|
| W1.1 | Phase 5B 구현 (Logging) | manager-tdd | LogCategories, LoggingBootstrap, InMemoryLogSink, LogAssertions 생성 |
| W1.2 | Phase 1 구현 (About+Menu) | manager-tdd | AboutWindow, MainViewModel 커맨드, AutomationId 포함 |
| W1.3 | 단위테스트 실행 | manager-tdd | 기존 83개 + 신규 ~30개 전체 통과 |
| W1.4 | 빌드 검증 | expert-debug | `dotnet build` 성공, 0 errors, 0 warnings |
| W1.5 | **교차검증 A** | manager-quality | TRUST 5 검증: MVVM 패턴, 네이밍, 커버리지 85%+ |
| W1.6 | **교차검증 B** | expert-frontend | WPF 바인딩 정합성, XAML 리소스, AutomationId 완전성 |
| W1.7 | **Debug/Fix 사이클** | expert-debug | W1.5/W1.6 지적사항 수정, 최대 3회 반복 |
| W1.8 | Wave 1 완료 확인 | manager-quality | 모든 테스트 통과, 교차검증 Pass |

**Wave 1 Exit Criteria**:
- `dotnet build` 0 errors
- 기존 83 + 신규 단위테스트 전체 통과
- About 다이얼로그 정상 표시 (수동 확인 또는 E2E)
- File > Exit, View > StatusBar, View > FullScreen 동작
- Serilog 로깅 App.xaml.cs에서 동작 확인

#### Wave 2: Help System (Phase 2 + 3 + 4)

**범위**: Help 인프라, Markdown 콘텐츠 9개, UX 향상 (ToolTip, 단축키, Welcome)
**의존성**: Wave 1 완료
**예상 파일**: 신규 23, 수정 5

| Step | Action | Agent | 성공 기준 |
|------|--------|-------|----------|
| W2.1 | Phase 2 구현 (Help Infra) | manager-tdd | IHelpContentService, HelpProvider, MarkdownConverter, HelpWindow |
| W2.2 | Phase 3 구현 (Markdown) | manager-docs | 9개 Help 토픽 Markdown 작성 |
| W2.3 | Phase 4 구현 (UX) | manager-tdd | ToolTip, 단축키, Welcome Wizard |
| W2.4 | 단위테스트 실행 | manager-tdd | 기존 + Wave1 + 신규 ~35개 전체 통과 |
| W2.5 | 빌드 검증 | expert-debug | `dotnet build` 성공, EmbeddedResource 확인 |
| W2.6 | **교차검증 A** | manager-quality | TRUST 5, Markdig 변환 정확성, 임베디드 리소스 완전성 |
| W2.7 | **교차검증 B** | expert-frontend | F1 핸들러 동작, HelpProvider 상속, ToolTip 3줄 형식 |
| W2.8 | **Debug/Fix 사이클** | expert-debug | W2.6/W2.7 지적사항 수정, 최대 3회 반복 |
| W2.9 | Wave 2 완료 확인 | manager-quality | 모든 테스트 통과, 교차검증 Pass |

**Wave 2 Exit Criteria**:
- `dotnet build` 0 errors
- F1 → HelpWindow 표시 → 토픽 선택 → 콘텐츠 렌더링 동작
- 9개 Markdown EmbeddedResource 빌드 포함 확인
- Rich ToolTip, 키보드 단축키, Welcome Wizard 동작
- 전체 단위테스트 통과 (기존 83 + Wave1 + Wave2)

#### Wave 3: E2E Validation (Phase 5A + 5C)

**범위**: FlaUI E2E 테스트 전체, CI/CD workflow
**의존성**: Wave 1 + Wave 2 완료
**예상 파일**: 신규 16, 수정 3

| Step | Action | Agent | 성공 기준 |
|------|--------|-------|----------|
| W3.1 | E2E 프로젝트 생성 | expert-testing | csproj, AppFixture, WaitHelper, ScreenshotHelper, RetryFact |
| W3.2 | Page Objects 구현 | expert-testing | MainWindowPage, AboutDialogPage, HelpWindowPage, SimulatorControlPage |
| W3.3 | Smoke Tests 구현 | expert-testing | AppLaunchTests 4건 통과 |
| W3.4 | Feature Tests 구현 | expert-testing | AboutDialog 3건 + HelpSystem 4건 + CoreFlow 4건 |
| W3.5 | E2E 전체 실행 | expert-testing | 15건 E2E 테스트 통과 |
| W3.6 | **교차검증 A** | manager-quality | E2E 커버리지, 로그 assertion 동작, flaky 감지 |
| W3.7 | **교차검증 B** | expert-backend | InMemoryLogSink 연동, LogAssertions 정확성 |
| W3.8 | **Debug/Fix 사이클** | expert-debug | Flaky 테스트 수정, AutomationId 누락 보완, 최대 5회 반복 |
| W3.9 | CI workflow 생성 | expert-devops | `.github/workflows/e2e-tests.yml` on windows-latest |
| W3.10 | **최종 통합 검증** | manager-quality + expert-testing | 단위(~148) + E2E(15) 전체 통과, TRUST 5 Pass |

**Wave 3 Exit Criteria**:
- E2E Smoke 4건 + Feature 11건 전체 통과
- InMemoryLogSink → LogAssertions 연동 확인
- `e2e-tests.yml` workflow 파일 생성 완료
- 전체 단위테스트 + E2E 테스트 통과

#### Debug/Fix 사이클 상세

각 Wave의 Debug/Fix 사이클은 다음 프로토콜을 따릅니다:

```
교차검증 결과 수신
    ↓
Issue 분류: [Critical / Major / Minor]
    ↓
Critical → 즉시 수정 (expert-debug)
Major    → 현재 Wave 내 수정 (expert-debug)
Minor    → 다음 Wave로 이월 가능 (기록 후 진행)
    ↓
수정 후 재빌드 + 재테스트
    ↓
교차검증 에이전트 재확인 (Pass/Fail)
    ↓
Pass → Wave 완료 / Fail → 재수정 (최대 N회)
```

**최대 반복 횟수**:
- Wave 1/2: 최대 3회 Debug/Fix 반복
- Wave 3 (E2E): 최대 5회 (E2E 특성상 환경 의존 이슈 가능)
- 초과 시: 사용자에게 AskUserQuestion으로 개입 요청

#### 전체 실행 흐름 (한 번에 실행)

```
/moai run SPEC-HELP-001
    │
    ├── Wave 1: Foundation ──────────────────────────┐
    │   W1.1 Logging → W1.2 About+Menu →            │
    │   W1.3 Test → W1.4 Build →                     │
    │   W1.5-W1.6 CrossVerify → W1.7 Debug/Fix      │
    │                                    ↓ Pass       │
    ├── Wave 2: Help System ─────────────────────────┤
    │   W2.1 HelpInfra → W2.2 Content → W2.3 UX →   │
    │   W2.4 Test → W2.5 Build →                     │
    │   W2.6-W2.7 CrossVerify → W2.8 Debug/Fix      │
    │                                    ↓ Pass       │
    ├── Wave 3: E2E Validation ──────────────────────┤
    │   W3.1-W3.4 E2E Impl → W3.5 E2E Run →         │
    │   W3.6-W3.7 CrossVerify → W3.8 Debug/Fix →    │
    │   W3.9 CI → W3.10 Final Verify                │
    │                                    ↓ Pass       │
    └── COMPLETE ────────────────────────────────────┘
```

---
