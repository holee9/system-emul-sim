# SPEC-E2E-002: E2E Test FlaUI Execution Logging and Debug Infrastructure

## Metadata

- **SPEC-ID**: SPEC-E2E-002
- **Status**: Plan Complete
- **Created**: 2026-03-13
- **Author**: MoAI
- **Depends-On**: SPEC-E2E-001 (RequiresDesktopFactAttribute, AppFixture fast-fail)

## Summary

현재 E2E 테스트는 대화형 세션에서 실행되지만 실패 시 진단 정보가 부족하다.
이 SPEC은 구조화된 로깅, 자동 스크린샷, 타이밍 계측, UIAutomation 트리 덤프, PowerShell 실행 스크립트를 구현하여 FlaUI E2E 테스트 디버깅 역량을 강화한다.

## Scope

**In Scope:**
- E2ELogger: AppFixture-held 구조화 로거 (파일 + 콘솔 출력)
- ScreenshotHelper 자동 호출 (실패 시 자동 캡처)
- WaitHelper 타임아웃 시 UIAutomation 트리 덤프
- AppFixture warmup 타이밍 계측
- PowerShell 대화형 실행 스크립트

**Out of Scope:**
- CI 환경에서의 E2E 실행 (SPEC-E2E-001에서 skip 처리 완료)
- 새 E2E 테스트 케이스 추가 (별도 SPEC)
- Headless WPF 테스트

## Context

### Current Infrastructure

```
Infrastructure/
  AppFixture.cs          — WPF 프로세스 시작, 90s 메뉴 warmup
  WaitHelper.cs          — 폴링 대기 (timeout 시 boolean만 반환)
  ScreenshotHelper.cs    — 수동 호출 전용
  RequiresDesktopFactAttribute.cs — CI skip (SPEC-E2E-001)
  E2ETestBase.cs         — 베이스 클래스 (MainWindow 접근)
PageObjects/
  MainWindowPage.cs, AboutDialogPage.cs, SimulatorControlPage.cs, HelpWindowPage.cs
Features/
  CoreFlowE2ETests.cs (4), HelpSystemE2ETests.cs (8),
  AboutDialogE2ETests.cs (3), ParameterExtractionE2ETests.cs (2)
Smoke/
  AppLaunchTests.cs (4)
```

### Key Pain Points

1. WaitHelper 타임아웃 시 어떤 element가 없는지 알 수 없음
2. 테스트 실패 시 스크린샷이 자동 캡처되지 않음
3. AppFixture warmup이 왜 90초씩 걸리는지 타이밍 데이터 없음
4. ITestOutputHelper 미사용 → xUnit 콘솔 출력 없음
5. 대화형 세션에서 실행하는 표준 스크립트 없음

## Requirements (EARS Format)

### REQ-E2E2-001: Structured Logging (E2ELogger)

**WHEN** E2E 테스트가 대화형 세션에서 실행되면,
**THE SYSTEM SHALL** 타임스탬프와 함께 모든 중요 이벤트를 TestResults/Logs/e2e_{timestamp}.log 파일에 기록한다.

**Acceptance Criteria:**
- AppFixture가 E2ELogger 인스턴스를 소유
- 로그 항목 형식: `[HH:mm:ss.fff] [LEVEL] message`
- 레벨: INFO, STEP, WARN, FAIL
- AppFixture.InitializeAsync()의 모든 단계 로깅
- 각 테스트 클래스에서 ITestOutputHelper로 플러시 가능

### REQ-E2E2-002: Automatic Screenshot on Failure

**WHEN** [RequiresDesktopFact] 테스트가 실패하면,
**THE SYSTEM SHALL** 즉시 스크린샷을 TestResults/Screenshots/ 에 자동으로 저장한다.

**Acceptance Criteria:**
- E2ETestBase에 `RunWithScreenshot(string testName, Action test)` 헬퍼 추가
- 예외 발생 시 ScreenshotHelper.CaptureOnFailure() 자동 호출
- 성공 시 스크린샷 미저장 (불필요한 파일 생성 방지)
- 파일명: `{TestClass}_{TestMethod}_{timestamp}.png`

### REQ-E2E2-003: Timing Instrumentation

**WHEN** AppFixture가 초기화될 때,
**THE SYSTEM SHALL** 각 단계(프로세스 시작, MainWindow 감지, 각 메뉴 warmup)의 소요 시간을 로깅한다.

**Acceptance Criteria:**
- Process start → first MainWindow detect: ms 단위 로깅
- WarmupSingleMenuAsync("File"): 시작/완료 시간 로깅
- WarmupSingleMenuAsync("Help"): 시작/완료 시간 로깅
- 총 초기화 시간 요약 로깅
- E2ELogger로 통합 출력

### REQ-E2E2-004: UIAutomation Tree Dump on Timeout

**WHEN** WaitHelper.WaitUntilAsync() 또는 WaitForElementAsync()가 타임아웃되면,
**THE SYSTEM SHALL** 현재 MainWindow의 UIAutomation 요소 트리를 로그에 덤프한다.

**Acceptance Criteria:**
- `TreeDumper.Dump(AutomationElement root, int maxDepth = 4)` 정적 메서드 추가
- WaitHelper에 `WaitForElementAsync(AutomationElement root, Func<AutomationElement?> finder, ...)` 추가
- 타임아웃 시 TreeDumper.Dump() 호출 후 결과 로깅
- 최대 깊이 4 (이상은 트리가 너무 커짐)
- AutomationId, Name, ControlType 포함

### REQ-E2E2-005: PowerShell Interactive Run Script

**WHEN** 개발자가 대화형 세션에서 E2E 테스트를 실행하려 할 때,
**THE SYSTEM SHALL** 환경 설정, 빌드, 테스트 실행, 로그 수집을 자동화하는 PowerShell 스크립트를 제공한다.

**Acceptance Criteria:**
- 파일: `tools/GUI.Application/tests/GUI.Application.E2ETests/Run-E2ETests.ps1`
- CI 환경 변수 제거 (Git Bash에서 실행 시 CI=true가 있으면 제거)
- GUI.Application 빌드 (Debug 구성)
- dotnet test 실행 (--logger "console;verbosity=detailed")
- 결과 로그를 TestResults/Logs/ 에 저장
- 실행 후 스크린샷/로그 위치 출력

## Architecture

### New Files

```
Infrastructure/
  E2ELogger.cs           — 구조화 로거 (NEW)
  TreeDumper.cs          — UIAutomation 트리 덤프 (NEW)
Run-E2ETests.ps1         — PowerShell 실행 스크립트 (NEW)
```

### Modified Files

```
Infrastructure/
  AppFixture.cs          — E2ELogger 통합, 타이밍 계측
  WaitHelper.cs          — WaitForElementAsync + TreeDumper 통합
  E2ETestBase.cs         — RunWithScreenshot 헬퍼 추가
```

### E2ELogger Design

```csharp
public sealed class E2ELogger : IDisposable
{
    // AppFixture가 생성/소유
    // 로그 파일: TestResults/Logs/e2e_{yyyyMMdd_HHmmss}.log
    public void Info(string message);
    public void Step(string message);   // 테스트 단계 강조
    public void Warn(string message);
    public void Fail(string message);
    public void FlushTo(ITestOutputHelper output);  // xUnit 콘솔 출력
    public void Dispose();  // 파일 스트림 닫기
}
```

### TreeDumper Design

```csharp
public static class TreeDumper
{
    // UIAutomation 요소 트리를 문자열로 덤프
    public static string Dump(AutomationElement root, int maxDepth = 4);
}
```

### WaitHelper Enhancement

```csharp
// 기존 WaitUntilAsync 유지 + 새 오버로드 추가
public static async Task<AutomationElement?> WaitForElementAsync(
    AutomationElement root,
    Func<AutomationElement?> finder,
    int timeoutMs = 5000,
    int pollIntervalMs = 100,
    E2ELogger? logger = null);
// 타임아웃 시 logger?.Fail(TreeDumper.Dump(root)) 호출
```

### E2ETestBase Enhancement

```csharp
// 실패 시 자동 스크린샷
protected void RunWithScreenshot(string testName, Action test)
{
    try { test(); }
    catch (Exception ex)
    {
        ScreenshotHelper.CaptureOnFailure(testName, Fixture.IsDesktopAvailable ? MainWindow : null);
        throw;
    }
}
```
