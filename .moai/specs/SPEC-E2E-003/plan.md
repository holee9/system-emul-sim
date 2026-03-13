---
id: SPEC-E2E-003
type: plan
version: 1.0.0
---

# SPEC-E2E-003 구현 계획

## 개요

FlaUI E2E 테스트 인프라의 환경 감지, 로깅, 진단 자동화를 강화하여 세 환경(CI, 로컬 bash, 대화형 데스크톱)에서 테스트가 명확하게 동작하도록 한다.

## 마일스톤

### Primary Goal: 환경 감지 및 fast-fail (M1)

**Phase 1: EnvironmentDetector 신규 생성**

- 파일: `Infrastructure/EnvironmentDetector.cs`
- `IsInteractiveDesktop()` 구현:
  1. `CI` 또는 `GITHUB_ACTIONS` 환경변수 확인 -> false
  2. `XRAY_E2E_FORCE=1` 확인 -> true (강제 실행)
  3. `SESSIONNAME` 확인: `Console` 또는 `RDP-Tcp#`으로 시작 -> true
  4. `UserInteractive` 확인 + 추가 터미널 감지 (TERM, WT_SESSION, MSYSTEM 환경변수)
  5. 판정 불가 시 -> false (안전한 기본값)
- `IsCI()`, `IsForced()`, `GetEnvironmentSummary()` 구현
- 정적 클래스, 순수 함수, 부작용 없음

**Phase 2: RequiresDesktopFactAttribute 강화**

- 파일: `Infrastructure/RequiresDesktopFactAttribute.cs`
- 기존 `IsInteractiveDesktop()` 로직을 `EnvironmentDetector` 호출로 교체
- Skip 메시지에 `EnvironmentDetector.GetEnvironmentSummary()` 포함
- 기존 22개 테스트의 동작 변경 없음 확인 (대화형 세션에서)

### Secondary Goal: WaitHelper 및 로깅 개선 (M2, M4)

**Phase 3: WaitHelper 개선**

- 파일: `Infrastructure/WaitHelper.cs`
- `WaitForElementAsync` 시그니처에 `string description` 파라미터 추가
- 매 폴링마다 로그: `[WaitHelper] '{description}' attempt {n}/{max}, elapsed {ms}ms`
- 타임아웃 시 자동으로 `TreeDumper.DumpTree()` 호출하여 로그에 첨부
- `XRAY_E2E_TIMEOUT_MS` 환경변수로 기본 타임아웃 오버라이드
- 기존 호출자 코드 업데이트 (description 파라미터 추가)

**Phase 4: E2ELogger + ITestOutputHelper 브리지**

- 파일: `Infrastructure/E2ELogger.cs`, `Infrastructure/E2ETestBase.cs`
- `E2ELogger`에 `ThreadLocal<ITestOutputHelper?>` 정적 필드 추가
- `SetTestOutput(ITestOutputHelper)` / `ClearTestOutput()` 메서드 추가
- `Log()` 메서드에서 ThreadLocal의 `ITestOutputHelper`가 있으면 `WriteLine` 호출
- `E2ETestBase` 생성자: `E2ELogger.SetTestOutput(output)` 호출
- `E2ETestBase.DisposeAsync`: `E2ELogger.ClearTestOutput()` 호출 (격리 보장)

### Tertiary Goal: 실패 진단 자동화 (M3)

**Phase 5: ScreenshotHelper 실패 훅 통합**

- 파일: `Infrastructure/ScreenshotHelper.cs`, `Infrastructure/E2ETestBase.cs`
- `E2ETestBase.DisposeAsync`에서 현재 테스트 실패 여부 확인
- 실패 시 `ScreenshotHelper.CaptureAsync()` 자동 호출
- 스크린샷 저장 경로: `XRAY_E2E_SCREENSHOT_DIR` 환경변수 또는 기본값 `TestResults/Screenshots/`
- 파일명 패턴: `{TestClassName}_{TestMethodName}_{Timestamp}.png`
- 실패 시 tree dump도 동시 저장 (`.txt` 파일)

**Phase 6: RetryFactAttribute 자동 재시도 구현**

- 파일: `Infrastructure/RetryFactAttribute.cs`
- xUnit `IXunitTestCaseRunner` 접근법 검토 (xUnit 2.x에서의 제약 확인)
- 대안: `RetryTestCase` + `RetryTestCaseRunner` 패턴으로 자동 재시도
- 최대 재시도 횟수: 기본 3회, `MaxRetries` 속성으로 설정 가능
- 각 재시도 시 E2ELogger로 시도 번호 기록

### Optional Goal: 환경 체크 및 설정 외부화 (M5)

**Phase 7: Run-E2ETests.ps1 사전 환경 체크**

- 파일: `Run-E2ETests.ps1`
- 스크립트 시작 시 `EnvironmentDetector`와 동일한 로직으로 환경 체크
- 비대화형 환경 감지 시 경고 메시지 출력 후 사용자에게 확인 요청
- `--force` 플래그로 환경 체크 우회 가능

**Phase 8: 환경변수 기반 타임아웃 통합**

- 모든 환경변수를 한 곳에서 관리: `E2EEnvironment` 정적 클래스 (또는 EnvironmentDetector에 통합)
- `XRAY_E2E_TIMEOUT_MS` (기본: 30000)
- `XRAY_E2E_DEBUG` (기본: 0)
- `XRAY_E2E_FORCE` (기본: 0)
- `XRAY_E2E_SCREENSHOT_DIR` (기본: TestResults/Screenshots)
- 환경변수 파싱 실패 시 기본값 사용 + 경고 로그

## 의존성 그래프

```
Phase 1: EnvironmentDetector (독립)
    |
    v
Phase 2: RequiresDesktopFactAttribute (Phase 1 의존)
    |
Phase 3: WaitHelper (독립, Phase 4와 병렬 가능)
Phase 4: E2ELogger 브리지 (독립, Phase 3와 병렬 가능)
    |
    v
Phase 5: ScreenshotHelper (Phase 4 의존)
Phase 6: RetryFactAttribute (Phase 4 의존)
    |
    v
Phase 7: Run-E2ETests.ps1 (Phase 1 의존)
Phase 8: 환경변수 통합 (Phase 1~6 완료 후)
```

## 수정 대상 파일

| 파일 | 작업 | Phase |
|------|------|-------|
| `Infrastructure/EnvironmentDetector.cs` | 신규 생성 | 1 |
| `Infrastructure/RequiresDesktopFactAttribute.cs` | 수정 | 2 |
| `Infrastructure/WaitHelper.cs` | 수정 | 3 |
| `Infrastructure/E2ELogger.cs` | 수정 | 4 |
| `Infrastructure/E2ETestBase.cs` | 수정 | 4, 5 |
| `Infrastructure/ScreenshotHelper.cs` | 수정 | 5 |
| `Infrastructure/RetryFactAttribute.cs` | 수정 | 6 |
| `Run-E2ETests.ps1` | 수정 | 7 |
| 기존 테스트 파일 (5개) | WaitHelper 호출 업데이트 | 3 |

## 리스크 및 대응

| 리스크 | 영향 | 대응 |
|--------|------|------|
| `RetryFactAttribute`: xUnit 2.x에서 자동 재시도 구현 제약 | 높음 | `IXunitTestCaseRunner` 대신 `BeforeAfterTestAttribute` + 래퍼 패턴 검토 |
| `ThreadLocal<ITestOutputHelper>`: 비동기 컨텍스트에서 누락 가능 | 중간 | `AsyncLocal<T>` 사용 검토, DisposeAsync에서 반드시 정리 |
| `SESSIONNAME` 환경변수: 모든 Windows 버전에서 일관성 | 낮음 | 폴백 로직 포함 (SESSIONNAME 없으면 UserInteractive 단독 판정 + 타임아웃 단축) |
| 기존 22개 테스트 회귀 | 높음 | Phase별 대화형 세션에서 전체 테스트 실행 확인 |

## 기술 스택

- 추가 NuGet 패키지: 없음 (기존 생태계 내 구현)
- 타겟 프레임워크: net8.0
- 테스트 프레임워크: xUnit 2.9
