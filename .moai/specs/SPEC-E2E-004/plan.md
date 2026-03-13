---
id: SPEC-E2E-004
version: "1.0.0"
status: planned
created: "2026-03-13"
updated: "2026-03-13"
author: drake
priority: high
---

# SPEC-E2E-004 구현 계획: AppFixture Attach Mode

## 개요

AI(Claude Code)가 E2E 테스트를 실행할 때 WPF 윈도우 스테이션 접근 문제를 우회하기 위해, 기존 실행 중인 GUI 프로세스에 FlaUI를 연결하는 Attach 모드를 구현한다.

## 마일스톤

### Primary Goal: EnvironmentDetector 확장 (TAG-002)

- `EnvironmentDetector.cs`에 `IsAttachMode()` 정적 메서드 추가
- `XRAY_E2E_ATTACH_PID` 환경변수 존재 및 비어있지 않음 확인
- 기존 `IsInteractiveDesktop()`, `IsCI()`, `IsForced()` 메서드에 영향 없음
- 단위 테스트 추가: `EnvironmentDetectorTests.cs`

### Secondary Goal: AppFixture Attach 모드 구현 (TAG-001, TAG-005, TAG-006)

- `AppFixture.cs`의 `InitializeAsync()`에 Attach 모드 분기 추가
- `_isAttachMode` 내부 플래그 도입
- Attach 모드 진입 시:
  - `XRAY_E2E_ATTACH_PID` 파싱 및 프로세스 유효성 검증
  - `EnvironmentDetector.IsInteractiveDesktop()` 검사 건너뛰기
  - `FlaUI.Core.Application.Attach(process)` 호출
  - 기존 메뉴 웜업 로직 실행
- `DisposeAsync()`에 Attach 모드 분기 추가:
  - Attach 모드: 프로세스 종료 건너뛰기, automation만 dispose
  - Launch 모드: 기존 동작 유지
- 무효 PID 시 명확한 예외 메시지 (ArgumentException 또는 InvalidOperationException)

### Tertiary Goal: Run-E2ETests.ps1 확장 (TAG-003)

- `-AttachPid` 매개변수 추가 (기본값: 0)
- PID 유효성 검증 (`Get-Process -Id $AttachPid`)
- `$env:XRAY_E2E_ATTACH_PID` 환경변수 설정
- 빌드 단계 건너뛰기 (앱이 이미 실행 중이므로)
- 정보 메시지 표시

### Final Goal: 단위 테스트 (TAG-004)

- `AppFixtureAttachTests.cs` 신규 생성
- 테스트 케이스:
  - `AttachMode_ValidPid_AttachesWithoutLaunching`
  - `AttachMode_InvalidPid_ThrowsMeaningfulError`
  - `AttachMode_DoesNotKillProcessOnDispose`
  - `EnvironmentDetector_IsAttachMode_WhenEnvVarSet`
  - `EnvironmentDetector_IsAttachMode_ReturnsFalse_WhenEnvVarNotSet`

## 기술 접근

### 아키텍처 설계 방향

기존 AppFixture의 `InitializeAsync`/`DisposeAsync` 패턴을 유지하면서 모드 분기를 추가하는 최소 침습적 접근:

```
AppFixture.InitializeAsync()
    |
    +-- IsAttachMode()? ----YES----> AttachToExistingProcess()
    |                                   |
    |                                   +-> Parse PID
    |                                   +-> Validate process
    |                                   +-> FlaUI.Attach()
    |                                   +-> WarmupMenus()
    |
    +-- IsAttachMode()? ----NO-----> (existing launch flow)
         |
         +-> IsInteractiveDesktop()?
         +-> Process.Start()
         +-> FlaUI.Attach()
         +-> WarmupMenus()
```

### 핵심 설계 결정

1. **환경변수 기반 모드 전환**: xUnit의 fixture 아키텍처에서는 생성자 매개변수 주입이 제한적이므로 환경변수가 가장 단순하고 확실한 제어 방법이다.

2. **프로세스 소유권 모델**: `_isAttachMode` 플래그로 프로세스 생명주기 관리 책임을 명확히 구분한다. Launch 모드에서는 AppFixture가 소유자, Attach 모드에서는 외부 소유.

3. **EnvironmentDetector 우회 근거**: Attach 모드에서는 사용자가 직접 앱을 실행했으므로 데스크톱 대화형 여부 검사가 불필요하다. 이미 가시적 윈도우가 존재한다는 것이 증거이다.

4. **PID 유효성 검증 시점**: `InitializeAsync` 진입 직후 즉시 검증하여 30초 hang을 방지한다. `Process.GetProcessById()`가 `ArgumentException`을 던지면 즉시 실패한다.

### 리스크 및 대응

| 리스크 | 영향 | 대응 |
|--------|------|------|
| 테스트 실행 중 대상 프로세스 종료 | FlaUI 접근 실패 | WaitHelper의 기존 타임아웃/재시도 메커니즘으로 처리 |
| PID가 GUI.Application이 아닌 다른 프로세스 | 잘못된 윈도우에 접근 | 프로세스 이름 검증 추가 고려 (Optional) |
| 여러 GUI.Application 인스턴스 실행 | 잘못된 인스턴스 연결 가능 | PID를 명시적으로 지정하므로 사용자 책임 |
| 앱 상태가 테스트 기대와 불일치 | 테스트 실패 | 메뉴 웜업으로 기본 상태 확인, 문서로 안내 |

## 추적성

| TAG | 마일스톤 | 파일 |
|-----|----------|------|
| TAG-001 | Secondary | `Infrastructure/AppFixture.cs` |
| TAG-002 | Primary | `Infrastructure/EnvironmentDetector.cs` |
| TAG-003 | Tertiary | `Run-E2ETests.ps1` |
| TAG-004 | Final | `Tests/Unit/AppFixtureAttachTests.cs`, `Tests/Unit/EnvironmentDetectorTests.cs` |
| TAG-005 | Secondary | `Infrastructure/AppFixture.cs` (DisposeAsync) |
| TAG-006 | Secondary | `Infrastructure/AppFixture.cs` (PID validation) |
