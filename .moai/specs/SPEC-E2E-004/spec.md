---
id: SPEC-E2E-004
version: "1.0.0"
status: implemented
created: "2026-03-13"
updated: "2026-03-13"
author: drake
priority: high
issue_number: 0
---

# SPEC-E2E-004: AppFixture Attach Mode -- AI 주도 E2E 테스트 루프

## 배경

SPEC-E2E-001~003을 통해 E2E 테스트 인프라(AppFixture, EnvironmentDetector, E2ELogger, WaitHelper, TreeDumper)를 구축했다. 그러나 AI(Claude Code)가 `dotnet test`를 실행할 때 근본적인 문제가 발견되었다:

**문제**: Claude Code -> bash -> PowerShell -> dotnet test -> testhost -> Process.Start(GUI.exe, UseShellExecute=false) 체인에서 WPF Dispatcher가 가시적 윈도우를 생성할 수 없다. 프로세스는 시작되지만(PID 존재) `MainWindowHandle`이 0으로 유지되어 FlaUI가 윈도우에 접근할 수 없다.

**진단 결과**:
- `SESSIONNAME=Console` -- 정상
- `UserInteractive=True` -- 정상
- 프로세스 시작(PID 확인) -- 정상
- `MainWindowHandle = 0` (15초+ 대기 후에도) -- WPF 윈도우 스테이션 접근 차단

**근본 원인**: WPF 윈도우 스테이션 접근이 Claude Code의 프로세스 트리에서 차단된다. `UseShellExecute=false`로 시작된 프로세스는 Window Station/Desktop 핸들을 상속받지 못하여 GUI 렌더링이 불가능하다.

## 환경 (Environment)

- C# 12 / .NET 8
- xUnit 2.9 / FluentAssertions 6.12
- FlaUI 4.0.0 (UIAutomation3 wrapper)
- 프로젝트 경로: `tools/GUI.Application/tests/GUI.Application.E2ETests/`
- 대상 환경:
  - AI 세션 (Claude Code): bash -> PowerShell 프로세스 트리
  - 대화형 Windows 데스크톱 세션: Explorer shell, RDP, 콘솔 세션
  - CI (GitHub Actions): `CI=true` (기존과 동일하게 skip)

## 가정 (Assumptions)

- SPEC-E2E-001/002/003 구현이 유지된다 (AppFixture, EnvironmentDetector, E2ELogger, WaitHelper, E2ETestBase, RequiresDesktopFactAttribute)
- 사용자가 GUI.Application.exe를 수동으로 실행하거나 AI가 `Get-Process GUI.Application`으로 PID를 검색할 수 있다
- Attach 대상 프로세스의 MainWindow가 이미 화면에 표시되어 있다 (WPF Dispatcher가 정상 작동 중)
- 외부 패키지 추가 없이 기존 FlaUI/xUnit 생태계 내에서 구현 가능하다
- 기존 `[RequiresDesktopFact]` 테스트는 변경 없이 계속 동작해야 한다

## 요구사항 (Requirements)

### REQ-1: 유비쿼터스 (Ubiquitous)

시스템은 **항상** `XRAY_E2E_ATTACH_PID` 환경변수의 유무에 따라 Launch 모드와 Attach 모드를 명확히 구분해야 한다.

### REQ-2: 이벤트 구동 (Event-Driven) -- Attach 모드 진입

**WHEN** `XRAY_E2E_ATTACH_PID` 환경변수가 유효한 프로세스 ID로 설정되어 있을 때,
**THEN** AppFixture는 새 프로세스를 시작하지 않고 `FlaUI.Core.Application.Attach(Process.GetProcessById(pid))`를 통해 기존 프로세스에 연결해야 한다.

### REQ-3: 상태 구동 (State-Driven) -- Attach 모드 시 EnvironmentDetector 우회

**IF** Attach 모드가 활성화된 상태이면,
**THEN** AppFixture는 `EnvironmentDetector.IsInteractiveDesktop()` 검사를 건너뛰어야 한다 (사용자가 앱을 실행했으므로 데스크톱이 대화형임이 자명하다).

### REQ-4: 이벤트 구동 (Event-Driven) -- 메뉴 웜업 유지

**WHEN** Attach 모드로 프로세스에 연결된 후,
**THEN** AppFixture는 기존과 동일한 `WarmupSingleMenuAsync` 호출(File/MenuFileExit, Help/MenuHelpTopics)을 수행해야 한다.

### REQ-5: 원치 않는 동작 (Unwanted) -- 프로세스 종료 금지

AppFixture는 Attach 모드에서 `DisposeAsync` 시 대상 프로세스를 종료**하지 않아야 한다** (프로세스는 테스트 외부에서 시작되었으므로 테스트가 관리하지 않는다).

### REQ-6: 원치 않는 동작 (Unwanted) -- 무효 PID 처리

**IF** `XRAY_E2E_ATTACH_PID`에 지정된 PID가 무효하거나 해당 프로세스가 실행 중이 아닌 경우,
**THEN** AppFixture는 의미 있는 오류 메시지와 함께 실패해야 하며, 30초간 hang 하지 않아야 한다.

### REQ-7: 이벤트 구동 (Event-Driven) -- Run-E2ETests.ps1 확장

**WHEN** `Run-E2ETests.ps1`에 `-AttachPid <PID>` 매개변수가 전달될 때,
**THEN** 스크립트는 `XRAY_E2E_ATTACH_PID` 환경변수를 설정하고, 빌드/실행 단계를 건너뛰고, "Attaching to existing GUI.Application (PID=$AttachPid)" 메시지를 표시해야 한다.

### REQ-8: 원치 않는 동작 (Unwanted) -- 무효 AttachPid 스크립트 처리

**IF** `-AttachPid`로 지정된 프로세스가 실행 중이 아닌 경우,
**THEN** `Run-E2ETests.ps1`은 오류 메시지를 출력하고 exit code 1로 종료해야 한다.

### REQ-9: 이벤트 구동 (Event-Driven) -- EnvironmentDetector 확장

**WHEN** `XRAY_E2E_ATTACH_PID` 환경변수가 설정되고 비어있지 않을 때,
**THEN** `EnvironmentDetector.IsAttachMode()`는 `true`를 반환해야 한다.

### REQ-10: 유비쿼터스 (Ubiquitous) -- 하위 호환성

시스템은 **항상** `XRAY_E2E_ATTACH_PID`가 설정되지 않은 경우 기존 Launch 동작을 변경 없이 유지해야 한다. `XRAY_E2E_FORCE=1`은 Attach 모드와 독립적으로 작동해야 한다.

## 사양 (Specifications)

### SPEC-1: AppFixture.InitializeAsync 분기 로직

```
IF IsAttachMode():
    1. Parse XRAY_E2E_ATTACH_PID to integer
    2. Validate Process.GetProcessById(pid) -- throws if invalid
    3. Skip EnvironmentDetector.IsInteractiveDesktop() check
    4. _flaUiApp = FlaUI.Core.Application.Attach(process)
    5. _automation = new UIA3Automation()
    6. Set _isAttachMode = true (internal flag)
    7. Perform WarmupSingleMenuAsync("File", "MenuFileExit")
    8. Perform WarmupSingleMenuAsync("Help", "MenuHelpTopics")
ELSE:
    (existing launch behavior unchanged)
```

### SPEC-2: AppFixture.DisposeAsync 분기 로직

```
IF _isAttachMode:
    1. Dispose _automation (UIA3Automation)
    2. Do NOT call _appProcess.Kill() or _appProcess.CloseMainWindow()
    3. Do NOT call _appProcess.WaitForExit()
ELSE:
    (existing dispose behavior unchanged)
```

### SPEC-3: EnvironmentDetector.IsAttachMode()

```
public static bool IsAttachMode()
    => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("XRAY_E2E_ATTACH_PID"));
```

### SPEC-4: Run-E2ETests.ps1 -AttachPid 매개변수

```
param(
    ...existing params...
    [int]$AttachPid = 0
)

IF $AttachPid -ne 0:
    1. Validate: Get-Process -Id $AttachPid (error -> exit 1)
    2. Set $env:XRAY_E2E_ATTACH_PID = $AttachPid
    3. Display: "Attaching to existing GUI.Application (PID=$AttachPid)"
    4. Skip build step
    5. Run dotnet test
```

### SPEC-5: AI 주도 E2E 테스트 루프 워크플로우

의도된 AI 주도 E2E 테스트 워크플로우:

1. 사용자가 GUI.Application.exe를 수동 실행 (더블클릭 또는 터미널에서)
2. 사용자가 PID를 AI에게 전달하거나, AI가 `Get-Process GUI.Application`으로 검색
3. AI가 `XRAY_E2E_ATTACH_PID` 설정 후 `dotnet test` 실행
4. FlaUI가 실행 중인 프로세스에 연결 (윈도우가 이미 화면에 표시됨)
5. 테스트 실행, 로그가 TestResults/Logs/에 기록
6. AI가 로그를 읽고, 실패를 분석하고, 코드를 수정
7. 필요 시 사용자가 앱을 재실행 (또는 앱이 테스트 실행 사이에 유지됨)
8. AI가 3단계부터 반복

## 영향 받는 파일

| 파일 | 변경 유형 |
|------|-----------|
| `Infrastructure/AppFixture.cs` | 수정 -- Attach 모드 분기 추가 |
| `Infrastructure/EnvironmentDetector.cs` | 수정 -- IsAttachMode() 추가 |
| `Run-E2ETests.ps1` | 수정 -- -AttachPid 매개변수 추가 |
| `Tests/Unit/AppFixtureAttachTests.cs` | 신규 -- Attach 모드 단위 테스트 |
| `Tests/Unit/EnvironmentDetectorTests.cs` | 수정 -- IsAttachMode 테스트 추가 |

## 제약사항 (Constraints)

- 외부 NuGet 패키지 추가 금지
- 기존 22개 E2E 단위 테스트에 영향 없어야 함
- Attach 모드는 환경변수로만 제어 (xUnit 아키텍처 내에서 fixture 구성 변경 불가)
- `UseShellExecute=true`로의 전환은 이 SPEC의 범위 밖 (보안 및 권한 복잡성)

## 추적성 (Traceability)

| TAG | 설명 |
|-----|------|
| TAG-001 | AppFixture Attach 모드 분기 로직 |
| TAG-002 | EnvironmentDetector.IsAttachMode() |
| TAG-003 | Run-E2ETests.ps1 -AttachPid 매개변수 |
| TAG-004 | Attach 모드 단위 테스트 |
| TAG-005 | DisposeAsync 프로세스 종료 금지 로직 |
| TAG-006 | 무효 PID 오류 처리 |
