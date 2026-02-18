# SPEC-GUITOOLS-001 최적 구현 계획

**작성일**: 2026-02-18
**목적**: 최소 노력으로 최대 효과를 얻는 최적화된 계획

---

## 1. 타겟 앱 분석 결과

### 1.1 GUI.Application 컨트롤 구조

**XAML 분석 (MainWindow.xaml, 201 lines)**:
```
MainWindow
├── Menu (File, Connection, Acquisition, View, Help)
│   ├── MenuItem: File > Open Config, Save Frame, Exit
│   ├── MenuItem: Connection > Connect, Disconnect
│   ├── MenuItem: Acquisition > Start, Stop
│   ├── MenuItem: View > Status Bar, Full Screen
│   └── MenuItem: Help > About
├── TabControl (3 tabs)
│   ├── TabItem: Status Dashboard
│   │   ├── TextBox: HostAddress, Port (readonly)
│   │   └── TextBlock: Connection State, Throughput
│   ├── TabItem: Frame Preview
│   │   ├── Slider: WindowCenter, WindowWidth
│   │   ├── TextBox: WindowCenter, WindowWidth
│   │   ├── Button: Auto
│   │   └── Image: FrameImage
│   └── TabItem: Configuration
│       └── TextBox: Width, Height, Bit Depth (readonly)
└── StatusBar
    └── StatusItem: StatusMessage, Version
```

**Command Binding 분석 (MainViewModel.cs)**:
```csharp
Commands:
- ConnectCommand:        HostAddress, Port 입력 → ConnectAsync()
- DisconnectCommand:     DisconnectAsync()
- StartAcquisitionCommand: StartAcquisitionAsync()
- StopAcquisitionCommand:  StopAcquisitionAsync()
- SaveFrameCommand:      SaveFrameAsync()
- AutoWindowLevelCommand: AutoWindowLevel()
- OpenConfigCommand:     (TODO)
```

### 1.2 ParameterExtractor.Wpf 컨트롤 구조

**XAML 분석 (MainWindow.xaml, 130 lines)**:
```
MainWindow
├── Header (TextBlocks)
├── ToolBar
│   ├── Button: Load PDF, Validate All, Add Parameter, Remove Selected, Export YAML
├── Border (Source file info)
├── DataGrid (6 columns: Name, Value, Unit, Category, Status, Message)
├── ItemsControl (Validation Summary)
└── StatusBar
    ├── StatusItem: StatusMessage
    └── ProgressBar
```

**Command Binding 분석 (MainWindowViewModel.cs)**:
```csharp
Commands:
- LoadPdfCommand:        OpenFileDialog → LoadPdfAsync()
- ValidateAllCommand:    ValidateAll()
- ExportCommand:         SaveFileDialog → ExportAsync()
- AddParameterCommand:   AddParameter()
- RemoveParameterCommand: RemoveParameter()
- EditParameterCommand:  EditParameter()
```

---

## 2. 최소 컨트롤 선정 (Smoke Test 기준)

### 2.1 GUI.Application 최소 컨트롤 (16개)

| 우선순위 | 컨트롤 | AutomationId | 테스트 시나리오 | 수정 시간 |
|----------|--------|---------------|----------------|-----------|
| 1 | Connect MenuItem | `ConnectMenuItem` | 연결 테스트 | 1분 |
| 2 | Disconnect MenuItem | `DisconnectMenuItem` | 연결 해제 | 1분 |
| 3 | Start Acquisition MenuItem | `StartAcquisitionMenuItem` | 획득 시작 | 1분 |
| 4 | Stop Acquisition MenuItem | `StopAcquisitionMenuItem` | 획득 중지 | 1분 |
| 5 | HostAddress TextBox | `HostAddressTextBox` | 주소 입력 | 1분 |
| 6 | Port TextBox | `PortTextBox` | 포트 입력 | 1분 |
| 7 | Auto Button | `AutoWindowLevelButton` | 자동 레벨 | 1분 |
| 8 | TabItem: Status Dashboard | `StatusDashboardTabItem` | 탭 전환 | 1분 |
| 9 | TabItem: Frame Preview | `FramePreviewTabItem` | 탭 전환 | 1분 |
| 10 | Save Frame MenuItem | `SaveFrameMenuItem` | 프레임 저장 | 1분 |
| 11 | WindowCenter Slider | `WindowCenterSlider` | 레벨 조정 | 1분 |
| 12 | WindowWidth Slider | `WindowWidthSlider` | 레벨 조정 | 1분 |
| 13 | MainWindow | `MainWindow` | 윈도우 식별 | 1분 |
| 14 | Open Config MenuItem | `OpenConfigMenuItem` | 설정 열기 | 1분 |
| 15 | Exit MenuItem | `ExitMenuItem` | 앱 종료 | 1분 |
| 16 | Status Bar | `StatusBar` | 상태 확인 | 1분 |

**총 수정 시간**: 16분

### 2.2 ParameterExtractor.Wpf 최소 컨트롤 (10개)

| 우선순위 | 컨트롤 | AutomationId | 테스트 시나리오 | 수정 시간 |
|----------|--------|---------------|----------------|-----------|
| 1 | Load PDF Button | `LoadPdfButton` | PDF 로드 | 1분 |
| 2 | Validate All Button | `ValidateAllButton` | 검증 실행 | 1분 |
| 3 | Export YAML Button | `ExportYamlButton` | YAML 내보내기 | 1분 |
| 4 | Add Parameter Button | `AddParameterButton` | 파라미터 추가 | 1분 |
| 5 | Remove Selected Button | `RemoveSelectedButton` | 파라미터 삭제 | 1분 |
| 6 | DataGrid | `ParametersDataGrid` | 테이블 확인 | 1분 |
| 7 | MainWindow | `MainWindow` | 윈도우 식별 | 1분 |
| 8 | Status Message | `StatusMessageTextBlock` | 상태 확인 | 1분 |
| 9 | ProgressBar | `BusyProgressBar` | 진행률 확인 | 1분 |
| 10 | Validation Summary | `ValidationSummaryItemsControl` | 요약 확인 | 1분 |

**총 수정 시간**: 10분

---

## 3. 최소 Serilog 통합

### 3.1 GUI.Application Serilog 추가

**수정 파일**: `App.xaml.cs`

```csharp
// 상단 using 추가
using Serilog;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Serilog 초기화 (추가된 부분)
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                path: "logs/gui_.log",
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
            )
            .CreateLogger();

        Log.Information("GUI.Application started");

        // 기존 코드
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Log.Fatal("CRITICAL: {Exception}", e.ExceptionObject);  // 수정
        System.Diagnostics.Debug.WriteLine($"CRITICAL: {e.ExceptionObject}");
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("UI Exception: {Exception}", e.Exception);  // 추가
        System.Diagnostics.Debug.WriteLine($"UI Exception: {e.Exception}");
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("GUI.Application exiting");  // 추가
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
```

**수정 시간**: 5분

### 3.2 ParameterExtractor.Wpf Serilog 추가

```csharp
// App.xaml.cs에 동일하게 추가
using Serilog;

protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);

    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .WriteTo.File("logs/parameter_extractor_.log",
                     rollingInterval: RollingInterval.Day)
        .CreateLogger();

    Log.Information("ParameterExtractor.Wpf started");

    // 기존 코드
    AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    DispatcherUnhandledException += OnDispatcherUnhandledException;
}
```

**수정 시간**: 5분

---

## 4. 최적 Phase 0 계획

### 4.1 병렬 작업 가능성

```
┌─────────────────────────────────────────────────────────────┐
│                    GUI.Application (wpf-dev)                 │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │ 1. XAML에 AutomationId 추가 (16개, 16분)                │ │
│  │ 2. App.xaml.cs에 Serilog 추가 (5분)                     │ │
│  │ 3. 빌드 및 테스트 (5분)                                 │ │
│  └─────────────────────────────────────────────────────────┘ │
│                          │                                  │
│                          ├─── 26분 ───│                     │
│                          │                                  │
└──────────────────────────┼──────────────────────────────────┘
                           │
                           │ (독립적 실행 가능)
                           │
┌──────────────────────────┼──────────────────────────────────┐
│                    ParameterExtractor.Wpf (wpf-dev)          │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │ 1. XAML에 AutomationId 추가 (10개, 10분)                │ │
│  │ 2. App.xaml.cs에 Serilog 추가 (5분)                     │ │
│  │ 3. 빌드 및 테스트 (5분)                                 │ │
│  └─────────────────────────────────────────────────────────┘ │
│                          │                                  │
│                          ├─── 20분 ───│                     │
└──────────────────────────┴──────────────────────────────────┘
```

### 4.2 NuGet 패키지 추가

**GUI.Application.csproj**:
```xml
<ItemGroup>
  <PackageReference Include="Serilog" Version="4.0.0" />
  <PackageReference Include="Serilog.Sinks.File" Version="6.0.0" />
</ItemGroup>
```

**ParameterExtractor.Wpf.csproj**:
```xml
<ItemGroup>
  <PackageReference Include="Serilog" Version="4.0.0" />
  <PackageReference Include="Serilog.Sinks.File" Version="6.0.0" />
</ItemGroup>
```

---

## 5. 최적화된 시간 추정

| 작업 | GUI.Application | ParameterExtractor | 합계 | 병렬 가능 |
|------|-----------------|---------------------|------|-----------|
| AutomationId 추가 | 16분 | 10분 | 26분 | ✅ 병렬 |
| Serilog 추가 | 5분 | 5분 | 10분 | ✅ 병렬 |
| 빌드 및 테스트 | 5분 | 5분 | 10분 | ✅ 병렬 |
| **병렬 총합** | **26분** | **20분** | **26분** | ✅ |
| **순차 총합** | - | - | **46분** | ❌ |

**최적 Phase 0 시간**: **26분** (병렬 실행 시)

---

## 6. ViewModel 로깅 추가 (선택)

LogVerifier 검증을 위해 주요 Command 실행에 로그 추가:

### GUI.Application MainViewModel.cs

```csharp
// 상단에 추가
using Serilog;

// 각 Command에 로그 추가
private async Task OnConnectAsync()
{
    Log.Information("ConnectCommand executing: {Host}:{Port}", HostAddress, Port);
    try
    {
        StatusMessage = $"Connecting to {HostAddress}:{Port}...";
        await _detectorClient.ConnectAsync(HostAddress, Port);
        _statusTimer.Start();
        Log.Information("Connected successfully to {Host}:{Port}", HostAddress, Port);
    }
    catch (Exception ex)
    {
        Log.Error("Connection failed: {Message}", ex.Message);
        StatusMessage = $"Connection failed: {ex.Message}";
    }
}

private async Task OnDisconnectAsync()
{
    Log.Information("DisconnectCommand executing");
    try
    {
        StatusMessage = "Disconnecting...";
        _statusTimer.Stop();
        await _detectorClient.DisconnectAsync();
        StatusViewModel.Reset();
        FramesReceived = 0;
        DroppedFrames = 0;
        StatusMessage = "Disconnected";
        Log.Information("Disconnected successfully");
    }
    catch (Exception ex)
    {
        Log.Error("Disconnect failed: {Message}", ex.Message);
        StatusMessage = $"Disconnect failed: {ex.Message}";
    }
}

// 다른 Command들도 동일하게 로그 추가
```

**추가 시간**: 각 앱 15분 (Command 6-7개당 각 2분)

---

## 7. 최종 최적 계획

### 옵션 A: 최소 계획 (26분)
```
┌─────────────────────────────────────────────────────────────┐
│ Phase 0-A: Minimum Viable Preparation                       │
├─────────────────────────────────────────────────────────────┤
│ 1. AutomationId: GUI 16개 + PE 10개 = 26개 (병렬 16분)      │
│ 2. Serilog 초기화: 양쪽 앱 (병렬 10분)                       │
│ 3. 빌드 및 검증 (병렬 10분)                                  │
├─────────────────────────────────────────────────────────────┤
│ 총 시간: 26분 (병렬)                                        │
│ 성공률: 85% (AutomationId 있음, Serilog 있음)                │
└─────────────────────────────────────────────────────────────┘
```

### 옵션 B: 표준 계획 (56분)
```
┌─────────────────────────────────────────────────────────────┐
│ Phase 0-B: Standard Preparation                             │
├─────────────────────────────────────────────────────────────┤
│ 옵션 A +                                                    │
│ 4. ViewModel 로깅: GUI 15분 + PE 15분 (병렬 15분)          │
├─────────────────────────────────────────────────────────────┤
│ 총 시간: 56분 (26분 + 30분)                                │
│ 성공률: 95% (LogVerifier 완전 동작)                         │
└─────────────────────────────────────────────────────────────┘
```

### 옵션 C: 완전 계획 (86분)
```
┌─────────────────────────────────────────────────────────────┐
│ Phase 0-C: Complete Preparation                             │
├─────────────────────────────────────────────────────────────┤
│ 옵션 B +                                                    │
│ 5. 모든 컨트롤에 AutomationId: GUI 30개 + PE 20개            │
│ 6. 고급 Serilog 설정 (structured logging, context)          │
├─────────────────────────────────────────────────────────────┤
│ 총 시간: 86분                                              │
│ 성공률: 99%                                                 │
└─────────────────────────────────────────────────────────────┘
```

---

## 8. 권고 사항

### 추천: **옵션 A (최소 계획, 26분)**

**근거**:
1. **빠른 피드백**: 30분 내에 Phase 0 완료
2. **충분한 테스트 가능성**: Smoke Test에 필요한 컨트롤만 포함
3. **확장 가능**: 추후 필요한 컨트롤 추가 가능
4. **낮은 리스크**: 최소 수정으로 버그 발생 확률 최소화

### 병렬 실행 전략

**wpf-dev 에이전트 2명 병렬 배치**:
```
Agent 1: GUI.Application 수정 (26분)
Agent 2: ParameterExtractor.Wpf 수정 (20분)
```

---

## 9. 다음 단계

1. **사용자 승인 획득**: 옵션 A/B/C 선택
2. **wpf-dev 에이전트 배치**: 병렬 작업 시작
3. **빌드 및 검증**: 각 앱 독립적으로 테스트
4. **Phase 1 진입**: GuiTestRunner 개발

---

**계획 작성**: 2026-02-18
**예상 시간**: 26분 (최소) ~ 86분 (완전)
**승인 상태**: 🔴 PENDING USER APPROVAL
