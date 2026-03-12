---
id: SPEC-HELP-001
type: acceptance
version: 1.0.0
status: planned
created: 2026-03-11
---

## Acceptance Criteria

### Phase 1: About Dialog + Unbound Menu Items

#### AC-HELP-010: About Dialog Display

```gherkin
Feature: About 다이얼로그

Scenario: Help > About 메뉴 클릭 시 About 다이얼로그 표시
  Given GUI 애플리케이션이 실행 중이다
  When 사용자가 Help > About 메뉴를 클릭한다
  Then AboutWindow가 모달 다이얼로그로 표시된다
  And 애플리케이션 이름 "X-ray Detector Panel System"이 표시된다
  And Assembly에서 읽은 버전 번호가 표시된다
  And 빌드 날짜가 표시된다
  And .NET 런타임 버전이 표시된다
  And OS 정보가 표시된다
  And CPU 코어 수가 표시된다

Scenario: About 다이얼로그에서 클립보드 복사
  Given About 다이얼로그가 열려 있다
  When 사용자가 "클립보드에 복사" 버튼을 클릭한다
  Then 시스템 정보 전체가 텍스트 형식으로 클립보드에 복사된다
  And 복사된 텍스트에 버전, 빌드 날짜, .NET 버전, OS 정보가 포함된다

Scenario: About 다이얼로그에서 GitHub 링크 클릭
  Given About 다이얼로그가 열려 있다
  When 사용자가 GitHub 링크를 클릭한다
  Then 외부 브라우저에서 GitHub 리포지토리 URL이 열린다
```

#### AC-HELP-011: AboutViewModel MVVM Compliance

```gherkin
Scenario: AboutViewModel은 ObservableObject를 상속한다
  Given AboutViewModel 인스턴스가 생성된다
  Then AboutViewModel은 ObservableObject를 상속해야 한다
  And AppVersion 프로퍼티가 null이 아니어야 한다
  And BuildDate 프로퍼티가 null이 아니어야 한다
  And DotNetVersion 프로퍼티가 null이 아니어야 한다
  And OsInfo 프로퍼티가 null이 아니어야 한다
  And CpuCoreCount가 0보다 커야 한다

Scenario: CopyToClipboardCommand가 올바르게 동작한다
  Given AboutViewModel 인스턴스가 생성된다
  When CopyToClipboardCommand를 실행한다
  Then 예외가 발생하지 않아야 한다
  And CanExecute는 항상 true여야 한다
```

#### AC-HELP-012: Dynamic Version Loading

```gherkin
Scenario: StatusBar 버전이 Assembly에서 동적으로 로드된다
  Given GUI 애플리케이션이 실행 중이다
  Then StatusBar에 표시된 버전이 Assembly.GetExecutingAssembly().GetName().Version과 일치한다
  And "v1.0.0" 하드코딩 문자열이 XAML에 존재하지 않는다

Scenario: csproj에 BuildDate AssemblyMetadata가 설정된다
  Given GUI.Application.csproj를 확인한다
  Then AssemblyMetadata Include="BuildDate" 항목이 존재한다
```

#### AC-HELP-013: File > Exit

```gherkin
Scenario: File > Exit 메뉴 클릭 시 애플리케이션 종료
  Given GUI 애플리케이션이 실행 중이다
  When 사용자가 File > Exit 메뉴를 클릭한다
  Then Application.Current.Shutdown()이 호출된다

Scenario: ExitCommand가 MainViewModel에 존재한다
  Given MainViewModel 인스턴스가 생성된다
  Then ExitCommand가 null이 아니어야 한다
  And ExitCommand.CanExecute는 항상 true여야 한다
```

#### AC-HELP-014: View > Status Bar Toggle

```gherkin
Scenario: StatusBar 토글
  Given GUI 애플리케이션이 실행 중이다
  And StatusBar가 표시 상태이다 (IsStatusBarVisible = true)
  When 사용자가 View > Status Bar 메뉴를 토글한다
  Then StatusBar가 숨겨진다 (Visibility = Collapsed)
  And IsStatusBarVisible이 false가 된다

Scenario: StatusBar 토글 복원
  Given StatusBar가 숨겨진 상태이다 (IsStatusBarVisible = false)
  When 사용자가 View > Status Bar 메뉴를 다시 토글한다
  Then StatusBar가 표시된다 (Visibility = Visible)
  And IsStatusBarVisible이 true가 된다
```

#### AC-HELP-015: View > Full Screen Toggle

```gherkin
Scenario: 전체 화면 전환
  Given GUI 애플리케이션이 일반 윈도우 모드로 실행 중이다
  When 사용자가 View > Full Screen을 클릭하거나 F11을 누른다
  Then WindowState가 Maximized가 된다
  And WindowStyle이 None이 된다
  And IsFullScreen이 true가 된다

Scenario: 전체 화면 해제
  Given 전체 화면 모드가 활성화되어 있다
  When 사용자가 View > Full Screen을 다시 클릭하거나 F11을 누른다
  Then WindowState가 이전 상태로 복원된다
  And WindowStyle이 SingleBorderWindow로 복원된다
  And IsFullScreen이 false가 된다
```

---

### Phase 2: Help System Infrastructure

#### AC-HELP-020: HelpProvider AttachedProperty

```gherkin
Scenario: HelpProvider.HelpTopicId가 자식 컨트롤에 상속된다
  Given 부모 요소에 HelpProvider.HelpTopicId="panel-simulation"이 설정된다
  When 자식 컨트롤에서 HelpProvider.GetHelpTopicId를 호출한다
  Then "panel-simulation"이 반환된다
```

#### AC-HELP-021: F1 Global Handler

```gherkin
Scenario: F1 키로 컨텍스트 도움말 표시
  Given Simulator Control 탭이 활성화되어 있다
  And HelpProvider.HelpTopicId="panel-simulation"이 설정되어 있다
  When 사용자가 F1 키를 누른다
  Then HelpWindow가 "panel-simulation" 토픽을 선택한 상태로 열린다

Scenario: HelpTopicId가 없는 영역에서 F1
  Given HelpTopicId가 설정되지 않은 영역에 포커스가 있다
  When 사용자가 F1 키를 누른다
  Then HelpWindow가 "overview" 토픽을 선택한 상태로 열린다
```

#### AC-HELP-022: IHelpContentService

```gherkin
Scenario: EmbeddedHelpContentService가 모든 토픽을 로드한다
  Given EmbeddedHelpContentService 인스턴스가 생성된다
  When GetTopics()를 호출한다
  Then 9개 이상의 토픽이 반환된다
  And 각 토픽에 Id, Title이 존재한다

Scenario: 특정 토픽 콘텐츠 로드
  Given EmbeddedHelpContentService 인스턴스가 생성된다
  When GetContent("overview")를 호출한다
  Then 비어있지 않은 Markdown 문자열이 반환된다

Scenario: 존재하지 않는 토픽 요청
  Given EmbeddedHelpContentService 인스턴스가 생성된다
  When GetTopic("nonexistent")를 호출한다
  Then null이 반환된다
```

#### AC-HELP-023: HelpWindow

```gherkin
Scenario: HelpWindow에서 토픽 선택
  Given HelpWindow가 열려 있다
  And TreeView에 토픽 목록이 표시된다
  When 사용자가 "Panel 시뮬레이션" 토픽을 선택한다
  Then 우측 영역에 panel-simulation.md 콘텐츠가 FlowDocument로 렌더링된다

Scenario: HelpWindow 검색
  Given HelpWindow가 열려 있다
  When 사용자가 검색 텍스트박스에 "kVp"를 입력한다
  Then TreeView에 "kVp"가 포함된 토픽만 표시된다
```

#### AC-HELP-024: Markdown Rendering

```gherkin
Scenario: Markdown 헤딩 렌더링
  Given Markdown 콘텐츠에 "## Section Title"이 포함되어 있다
  When MarkdownToFlowDocumentConverter로 변환한다
  Then FlowDocument에 Bold 스타일의 "Section Title" Paragraph가 포함된다

Scenario: Markdown 테이블 렌더링
  Given Markdown 콘텐츠에 테이블이 포함되어 있다
  When MarkdownToFlowDocumentConverter로 변환한다
  Then FlowDocument에 Table 요소가 포함된다

Scenario: Markdown 코드 블록 렌더링
  Given Markdown 콘텐츠에 코드 블록이 포함되어 있다
  When MarkdownToFlowDocumentConverter로 변환한다
  Then 코드 블록이 Consolas 폰트로 표시된다
```

---

### Phase 3: Help Contents

#### AC-HELP-030: Help Content Completeness

```gherkin
Scenario: 모든 도움말 파일이 임베디드 리소스로 존재한다
  Given 빌드가 완료된다
  When Assembly.GetManifestResourceNames()를 확인한다
  Then 다음 리소스가 존재한다:
    | Resource Name Pattern                          |
    | *.Help.Topics.overview.md                      |
    | *.Help.Topics.getting-started.md               |
    | *.Help.Topics.panel-simulation.md              |
    | *.Help.Topics.fpga-csi2.md                     |
    | *.Help.Topics.mcu-udp.md                       |
    | *.Help.Topics.host-pipeline.md                 |
    | *.Help.Topics.parameters-ref.md                |
    | *.Help.Topics.keyboard-shortcuts.md            |
    | *.Help.Topics.troubleshooting.md               |

Scenario: 도움말 콘텐츠는 한국어로 작성된다
  Given 각 도움말 Markdown 파일을 확인한다
  Then 본문이 한국어로 작성되어 있다
  And 기술 용어 (kVp, mAs, CSI-2, UDP, FPGA, MCU)는 영어가 보존된다
```

---

### Phase 4: UX Enhancement

#### AC-HELP-040: Rich ToolTips

```gherkin
Scenario: kVp 파라미터에 Rich ToolTip 표시
  Given Simulator Control 탭이 표시된다
  When 사용자가 kVp 입력 필드 위에 마우스를 올린다
  Then 3줄 ToolTip이 표시된다:
    | Line | Content                                    |
    | 1    | kVp (Bold)                                 |
    | 2    | Range: 40-150 kV                           |
    | 3    | X선관의 가속 전압으로 투과력을 결정합니다   |

Scenario: PacketLossRate 파라미터에 Rich ToolTip 표시
  Given Simulator Control 탭이 표시된다
  When 사용자가 PacketLossRate 입력 필드 위에 마우스를 올린다
  Then 3줄 ToolTip이 표시된다:
    | Line | Content                                            |
    | 1    | Packet Loss Rate (Bold)                            |
    | 2    | Range: 0.0-1.0 (0=no loss, 1=100% loss)          |
    | 3    | 네트워크 전송 중 패킷 손실 비율을 시뮬레이션합니다 |
```

#### AC-HELP-041: StatusBar Hints

```gherkin
Scenario: 컨트롤 포커스 시 StatusBar 힌트 표시
  Given GUI 애플리케이션이 실행 중이다
  When kVp 입력 필드가 포커스를 받는다
  Then StatusBar에 "X선관 가속 전압을 입력하세요 (40-150 kV)"가 표시된다

Scenario: 포커스 해제 시 힌트 초기화
  Given kVp 입력 필드에 포커스가 있다
  When 포커스가 다른 컨트롤로 이동한다
  Then StatusBar 메시지가 기본 상태("Ready")로 복원된다
```

#### AC-HELP-042: Keyboard Shortcuts

```gherkin
Scenario: Ctrl+R로 파이프라인 시작
  Given Pipeline 모드가 활성화되어 있다
  And 파이프라인이 정지 상태이다
  When 사용자가 Ctrl+R을 누른다
  Then 파이프라인이 시작된다

Scenario: Alt+1~6으로 탭 전환
  Given GUI 애플리케이션이 실행 중이다
  When 사용자가 Alt+3을 누른다
  Then 3번째 탭 (Configuration)이 선택된다

Scenario: Ctrl+/로 단축키 오버레이 표시
  Given GUI 애플리케이션이 실행 중이다
  When 사용자가 Ctrl+/를 누른다
  Then 반투명 오버레이에 모든 키보드 단축키 목록이 표시된다
  When 사용자가 아무 키를 누른다
  Then 오버레이가 닫힌다

Scenario: F11으로 전체 화면 토글
  Given GUI 애플리케이션이 일반 모드로 실행 중이다
  When 사용자가 F11을 누른다
  Then 전체 화면 모드로 전환된다
```

#### AC-HELP-043: First-Run Welcome Wizard

```gherkin
Scenario: 최초 실행 시 Welcome 마법사 표시
  Given 애플리케이션을 처음 실행한다
  And %LOCALAPPDATA%/XrayDetector/settings.json이 존재하지 않는다
  When 애플리케이션이 시작된다
  Then 3단계 Welcome 마법사가 표시된다

Scenario: Welcome 마법사 완료
  Given Welcome 마법사 Step 3이 표시된다
  When 사용자가 "완료" 버튼을 클릭한다
  Then settings.json에 firstRunCompleted: true가 저장된다
  And 기본 시뮬레이션이 자동 시작된다

Scenario: 두 번째 실행 시 마법사 미표시
  Given %LOCALAPPDATA%/XrayDetector/settings.json에 firstRunCompleted: true가 있다
  When 애플리케이션이 시작된다
  Then Welcome 마법사가 표시되지 않는다

Scenario: "다시 보지 않기" 체크박스
  Given Welcome 마법사가 표시된다
  When 사용자가 "다시 보지 않기" 체크박스를 선택하고 닫는다
  Then settings.json에 firstRunCompleted: true가 저장된다
```

---

### Phase 5: E2E Testing + Structured Logging

#### AC-HELP-050: E2E App Launch (Smoke)

```gherkin
Feature: E2E Smoke Tests

Scenario: 앱이 정상적으로 시작된다
  Given GUI.Application.exe를 FlaUI로 시작한다
  When 메인 윈도우가 30초 내에 표시된다
  Then MainWindow의 Title이 "X-ray Detector"를 포함한다
  And 6개 탭이 모두 존재한다

Scenario: StatusBar에 동적 버전이 표시된다
  Given 앱이 실행 중이다
  When StatusBar의 버전 텍스트를 읽는다
  Then "v1.0.0" 하드코딩이 아닌 Assembly 버전이 표시된다

Scenario: File > Exit으로 앱이 종료된다
  Given 앱이 실행 중이다
  When File > Exit 메뉴를 클릭한다
  Then 앱 프로세스가 5초 내에 종료된다
```

#### AC-HELP-051: E2E About Dialog

```gherkin
Scenario: About 다이얼로그가 올바른 정보를 표시한다
  Given 앱이 실행 중이다
  When Help > About 메뉴를 클릭한다
  Then AboutWindow가 모달로 표시된다
  And 버전 번호가 표시된다
  And .NET 런타임 버전이 표시된다

Scenario: About 다이얼로그 클립보드 복사
  Given AboutWindow가 열려 있다
  When "클립보드에 복사" 버튼을 클릭한다
  Then 로그에 "UserAction" 카테고리로 "CopyToClipboard" 이벤트가 기록된다
```

#### AC-HELP-052: E2E Core Flows

```gherkin
Scenario: 전체 화면 토글
  Given 앱이 일반 모드로 실행 중이다
  When View > Full Screen을 클릭한다
  Then 윈도우가 최대화된다
  When View > Full Screen을 다시 클릭한다
  Then 윈도우가 이전 크기로 복원된다

Scenario: StatusBar 토글
  Given StatusBar가 표시 상태이다
  When View > Status Bar를 클릭한다
  Then StatusBar가 숨겨진다
```

#### AC-HELP-057: Structured Logging

```gherkin
Scenario: Serilog가 초기화된다
  Given E2E 모드에서 앱이 시작된다
  When 앱이 정상적으로 로드된다
  Then InMemoryLogSink에 "App" 카테고리의 시작 로그가 존재한다

Scenario: 로그에 에러가 없다
  Given E2E 테스트가 완료된다
  When InMemoryLogSink의 Error 레벨 이벤트를 조회한다
  Then Error 이벤트가 0건이어야 한다

Scenario: 사용자 액션이 로깅된다
  Given 앱이 실행 중이다
  When Help > About 메뉴를 클릭한다
  Then InMemoryLogSink에 "UserAction" 카테고리로 "ShowAbout" 이벤트가 기록된다
```

#### AC-HELP-05A: E2E CI Workflow

```gherkin
Scenario: E2E CI가 Windows에서 실행된다
  Given .github/workflows/e2e-tests.yml이 존재한다
  Then runs-on이 "windows-latest"이다
  And GUI.Application.E2ETests 프로젝트를 빌드하고 실행한다
  And 실패 시 스크린샷을 아티팩트로 업로드한다
```

---

## Test Plan

### Unit Tests (Phase 1)

| Test Class | Test Cases | Description |
|------------|------------|-------------|
| `AboutViewModelTests.cs` | 8 | 버전 로딩, 빌드 날짜, 런타임 정보, CopyToClipboard, OpenGitHub |
| `MainViewModel_MenuTests.cs` | 6 | ExitCommand, ToggleStatusBar, ToggleFullScreen, ShowAbout |
| `BoolToVisibilityConverterTests.cs` | 4 | true->Visible, false->Collapsed, 역변환 |

### Unit Tests (Phase 2)

| Test Class | Test Cases | Description |
|------------|------------|-------------|
| `EmbeddedHelpContentServiceTests.cs` | 6 | GetTopics, GetTopic, GetContent, null handling |
| `HelpViewModelTests.cs` | 5 | 토픽 로드, 선택 변경, 검색 필터링 |
| `MarkdownToFlowDocumentConverterTests.cs` | 8 | Heading, paragraph, table, code, link, list 변환 |
| `HelpProviderTests.cs` | 3 | AttachedProperty get/set, 상속 |

### Unit Tests (Phase 4)

| Test Class | Test Cases | Description |
|------------|------------|-------------|
| `ParameterTooltipsTests.cs` | 4 | 데이터 존재 확인, 3줄 형식 검증 |
| `FirstRunManagerTests.cs` | 5 | 최초 실행 감지, 설정 저장/로드 |
| `WelcomeWizardViewModelTests.cs` | 4 | 스텝 전환, 완료 처리 |

### Unit Tests (Phase 5B - Logging)

| Test Class | Test Cases | Description |
|------------|------------|-------------|
| `InMemoryLogSinkTests.cs` | 5 | Emit, 10K 바운드, thread-safety, FIFO overflow, 카테고리 필터 |
| `LoggingBootstrapTests.cs` | 3 | 초기화, E2E 모드 InMemory 활성화, 카테고리 설정 |
| `LogAssertionsTests.cs` | 4 | ShouldHaveNoErrors, ShouldEventuallyLog, 카테고리 필터, 시간 범위 |

### E2E Tests (Phase 5A)

| Test Class | Test Cases | Description |
|------------|------------|-------------|
| `AppLaunchTests.cs` | 4 | Smoke: 앱 시작, 버전 표시, 탭 존재, Exit 종료 |
| `AboutDialogE2ETests.cs` | 3 | About 열기, 정보 표시, 클립보드 복사 |
| `HelpSystemE2ETests.cs` | 4 | F1 열기, 토픽 선택, 검색, 콘텐츠 렌더링 |
| `CoreFlowE2ETests.cs` | 4 | 전체화면 토글, StatusBar 토글, 탭 전환, Start/Stop |

### Integration Tests

| Test | Description |
|------|-------------|
| `HelpSystemIntegrationTest` | F1 -> HelpWindow 표시 -> 토픽 선택 -> 콘텐츠 렌더링 전체 플로우 |
| `MenuBindingIntegrationTest` | 모든 메뉴 아이템의 Command 바인딩 검증 (null이 아닌지) |

### Regression Verification

- 기존 83개 테스트 전체 통과 확인
- 기존 6개 탭 동작 검증
- SimulatedDetectorClient / PipelineDetectorClient 모드 전환 검증

---

## Quality Gate Criteria

### Definition of Done

- [ ] 모든 Phase의 해당 요구사항 구현 완료
- [ ] 모든 신규 코드에 XML 문서 주석 작성
- [ ] 기존 83개 테스트 + 신규 테스트 전체 통과
- [ ] 하드코딩된 "v1.0.0"이 XAML에서 제거됨
- [ ] 4개 미바인드 메뉴 아이템 (Exit, StatusBar, FullScreen, About) 모두 동작
- [ ] About 다이얼로그에서 정확한 시스템 정보 표시
- [ ] F1 키로 컨텍스트 도움말 접근 가능 (Phase 2)
- [ ] 모든 도움말 콘텐츠가 임베디드 리소스로 빌드됨 (Phase 3)
- [ ] Rich ToolTip이 주요 파라미터에 표시됨 (Phase 4)
- [ ] E2E Smoke 테스트 4건 통과 (Phase 5A)
- [ ] E2E SPEC-HELP-001 Feature 테스트 통과 (Phase 5A)
- [ ] Serilog 구조화 로깅 동작 확인 (Phase 5B)
- [ ] InMemoryLogSink E2E assertion 동작 확인 (Phase 5B)
- [ ] E2E CI workflow가 windows-latest에서 실행됨 (Phase 5C)

### TRUST 5 Verification

| Pillar | Criteria |
|--------|----------|
| Tested | 신규 코드 85%+ 커버리지, 기존 83개 테스트 회귀 없음 |
| Readable | 명확한 네이밍, 영어 XML 주석, 일관된 MVVM 패턴 |
| Unified | ObservableObject/RelayCommand 패턴 준수, WPF 표준 |
| Secured | 사용자 입력 검증, 외부 프로세스 실행 시 UseShellExecute |
| Trackable | Conventional commit, SPEC-HELP-001 참조 |

---
