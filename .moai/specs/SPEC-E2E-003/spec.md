---
id: SPEC-E2E-003
version: 1.0.0
status: planned
created: 2026-03-13
updated: 2026-03-13
author: drake
priority: high
---

# SPEC-E2E-003: FlaUI E2E 테스트 인프라 강화

## 배경

SPEC-E2E-001(AppFixture fast-fail)과 SPEC-E2E-002(E2ELogger/E2ETestBase/WaitHelper/TreeDumper)로 E2E 테스트 기반 인프라를 구현했다. 그러나 다음과 같은 인프라 갭이 남아 있어 테스트 신뢰성과 진단 가능성이 부족하다:

- 로컬 bash 환경(CI 변수 없음)에서 `UserInteractive=true`로 판정되어 WPF 시작을 시도하고 30초간 hang 발생
- 요소 검색 재시도 루프(200회 x 200ms = 40초)에 로깅이 전혀 없어 진단 불가
- `RetryFactAttribute`가 선언만 있고 자동 재시도 미구현
- `ITestOutputHelper`가 `E2ELogger`와 미연결
- `ScreenshotHelper`가 실패 훅에 미통합
- 타임아웃 값이 하드코딩되어 환경별 조정 불가

이 SPEC은 위 갭을 해소하여 세 환경(CI, 로컬 bash, 대화형 데스크톱)에서 테스트가 명확하게 실행/스킵/실패하도록 인프라를 강화한다.

## 환경 (Environment)

- C# 12 / .NET 8
- xUnit 2.9 / FluentAssertions 6.12
- FlaUI 4.0.0 (UIAutomation3 wrapper)
- 프로젝트 경로: `tools/GUI.Application/tests/GUI.Application.E2ETests/`
- 대상 환경:
  - CI (GitHub Actions): `CI=true` 또는 `GITHUB_ACTIONS=true`
  - 로컬 bash (Git Bash, WSL, Windows Terminal): 비대화형, CI 변수 없음
  - 대화형 Windows 데스크톱 세션: Explorer shell, RDP, 콘솔 세션

## 가정 (Assumptions)

- SPEC-E2E-001/002 구현이 유지된다 (AppFixture, E2ELogger, TreeDumper, WaitHelper, E2ETestBase)
- 외부 패키지 추가 없이 기존 FlaUI/xUnit 생태계 내에서 구현 가능하다
- Windows 환경변수 `SESSIONNAME`으로 RDP/콘솔 세션 판별이 가능하다
- `System.Environment.UserInteractive`만으로는 bash와 데스크톱 세션을 구분할 수 없다
- E2E 테스트 수는 현재 22개이며, 이 SPEC에서 새 테스트 케이스를 추가하지 않는다

## 요구사항 (Requirements)

### REQ-1: 유비쿼터스 (Ubiquitous)

시스템은 **항상** 모든 환경에서 E2E 테스트 실행 결과를 명확하게 보고해야 한다 (실행 Pass/Fail, Skip 사유 포함).

- 테스트 결과는 "실행됨(Pass/Fail)" 또는 "Skip(사유 명시)" 중 하나여야 한다
- 불확정 상태(hang, 무한 대기, 원인 불명 타임아웃)가 발생하지 않아야 한다

### REQ-2: 이벤트 기반 (Event-Driven)

**WHEN** WPF 창 감지가 실패하면 **THEN** 시스템은 진단 정보를 자동으로 수집해야 한다.

- 수집 항목: UIAutomation tree dump, 프로세스 PID/상태, 경과 시간, 시도 횟수
- 수집된 정보는 E2ELogger를 통해 파일 로그와 xUnit 출력 양쪽에 기록

### REQ-3: 원치 않는 동작 (Unwanted Behavior)

시스템은 로컬 bash 환경에서 30초 이상 hang **하지 않아야 한다**.

- `EnvironmentDetector`가 bash/비대화형 터미널을 5초 이내에 감지
- 감지 시 즉시 Skip 처리하고 사유를 로그에 기록
- `XRAY_E2E_FORCE=1` 환경변수로 강제 실행 가능 (우회 경로)

### REQ-4: 상태 기반 (State-Driven)

**IF** 대화형 Windows 데스크톱 세션이면 **THEN** FlaUI E2E 테스트를 정상 실행한다.

- `SESSIONNAME` 환경변수가 `Console` 또는 `RDP-Tcp#`으로 시작하는 경우 대화형으로 판정
- `WaitHelper`가 매 시도마다 진행 상황(시도 횟수, 경과 시간)을 로그에 기록
- 요소 검색 타임아웃 시 tree dump를 자동으로 첨부

### REQ-5: 선택적 기능 (Optional Feature)

**가능하면** `XRAY_E2E_DEBUG=1` 환경변수로 상세 진단 로그를 활성화 제공.

- 활성화 시: per-test 구조화 로그, 스크린샷 자동 저장, UIAutomation tree dump 상세 출력
- 비활성화 시: 기본 수준 로깅 (Pass/Fail/Skip 결과만)

## 모듈 구조 (Specifications)

### M1: 환경 감지 강화

- **신규** `EnvironmentDetector.cs` 생성
  - `IsInteractiveDesktop()`: SESSIONNAME, 터미널 타입, UserInteractive 복합 판정
  - `IsCI()`: CI/GITHUB_ACTIONS 환경변수 확인
  - `IsForced()`: XRAY_E2E_FORCE 환경변수 확인
  - `GetEnvironmentSummary()`: 현재 환경 진단 문자열 반환
- `RequiresDesktopFactAttribute.cs` 개선: `EnvironmentDetector` 활용

### M2: WaitHelper 개선

- 시도 횟수, 경과 시간을 매 폴링마다 E2ELogger로 기록
- `description` 파라미터 추가 (어떤 요소를 찾고 있는지 명시)
- 타임아웃 시 tree dump 콜백 자동 호출
- 환경변수 `XRAY_E2E_TIMEOUT_MS`로 기본 타임아웃 오버라이드 가능

### M3: 실패 진단 자동화

- `ScreenshotHelper.cs`를 테스트 실패 시 자동 호출하도록 `E2ETestBase.DisposeAsync`에 통합
- 실패 시 스크린샷 + tree dump를 자동 저장
- 저장 경로: `TestResults/Screenshots/{TestName}_{Timestamp}.png`

### M4: xUnit 출력 연결

- `E2ELogger`에 `ThreadLocal<ITestOutputHelper>` 브리지 추가
- `E2ETestBase` 생성자에서 `ITestOutputHelper` 등록
- `Dispose`에서 ThreadLocal 정리 (테스트 격리 보장)
- 로그가 파일과 xUnit 출력 콘솔 양쪽에 동시 기록

### M5: 타임아웃 설정 외부화

- `XRAY_E2E_TIMEOUT_MS`: WaitHelper 기본 타임아웃 (기본값: 30000)
- `XRAY_E2E_DEBUG`: 상세 로깅 활성화 (기본값: 0)
- `XRAY_E2E_FORCE`: 비대화형 환경에서도 강제 실행 (기본값: 0)
- `XRAY_E2E_SCREENSHOT_DIR`: 스크린샷 저장 경로 (기본값: TestResults/Screenshots)

## 의존성

- SPEC-E2E-001: AppFixture fast-fail (선행, 완료)
- SPEC-E2E-002: E2ELogger/E2ETestBase/WaitHelper/TreeDumper (선행, 완료)

## 범위 제외

- 새 E2E 테스트 케이스 추가
- Headless WPF 테스팅 (WPF 아키텍처 한계)
- GitHub Actions 데스크톱 모드 지원
- 모달 다이얼로그 Win32 fragility 수정 (별도 SPEC)
- 메뉴 워밍업 90초 하드코딩 최적화 (별도 SPEC)

## 추적성 (Traceability)

| 요구사항 | 모듈 | 수락 기준 |
|----------|------|-----------|
| REQ-1 | M1, M5 | AC-1, AC-2 |
| REQ-2 | M2, M3 | AC-4 |
| REQ-3 | M1 | AC-1 |
| REQ-4 | M1, M2 | AC-3, AC-4 |
| REQ-5 | M4, M5 | AC-5 |
