---
id: SPEC-E2E-001
version: "1.0.0"
status: draft
created: "2026-03-13"
updated: "2026-03-13"
author: drake
priority: medium
issue_number: 0
tags: [e2e, testing, infrastructure, wpf, flaui]
---

## HISTORY

| Version | Date | Author | Description |
|---------|------|--------|-------------|
| 1.0.0 | 2026-03-13 | drake | Initial SPEC creation |

---

# SPEC-E2E-001: E2E 테스트 인프라 개선 - AppFixture Fast-Fail 및 Graceful Skip

## 1. Environment (환경)

### 1.1 프로젝트 컨텍스트

- **프로젝트**: X-ray Detector Panel System (GUI.Application)
- **기술 스택**: C# 12 / .NET 8 / xUnit 2.9.3 / FlaUI 4.0.0 (UIA3)
- **대상 디렉토리**: `tools/GUI.Application/tests/GUI.Application.E2ETests/`
- **관련 파일**:
  - `Infrastructure/AppFixture.cs` - xUnit Collection Fixture (프로세스 수명주기 관리)
  - `Infrastructure/E2ETestBase.cs` - 모든 E2E 테스트의 기본 클래스
  - `Infrastructure/RequiresDesktopFactAttribute.cs` - 데스크톱 환경 감지 어트리뷰트

### 1.2 문제 상황

AppFixture.InitializeAsync()가 비대화형(non-interactive) 세션(bash, CI, GitHub Actions)에서 실행 시, FlaUI의 UIAutomation이 WPF 창을 감지하지 못하여 **30초간 폴링 후 TimeoutException**으로 실패한다.

- WPF 프로세스 자체는 정상 시작됨
- UIAutomation 서버가 비대화형 세션에서 프로세스에 바인딩되지 않음
- `GetMainWindow()` 호출이 항상 null을 반환하며 30초간 반복 폴링
- `[RequiresDesktopFact]` 어트리뷰트는 개별 테스트 Skip만 처리하고, Collection Fixture 초기화 단계의 hang은 방지하지 못함
- 17개 E2E 테스트 전체에 30초 불필요한 대기 시간 발생

### 1.3 영향 범위

| 파일 | 변경 유형 | 영향도 |
|------|----------|--------|
| `Infrastructure/AppFixture.cs` | 수정 | 높음 - Fast-fail 로직 추가 |
| `Infrastructure/E2ETestBase.cs` | 수정 | 중간 - Graceful skip 로직 추가 |
| `Infrastructure/RequiresDesktopFactAttribute.cs` | 참조 | 낮음 - IsInteractiveDesktop() 로직 재사용 |

## 2. Assumptions (가정)

- **A1**: xUnit 2.9.3의 Collection Fixture(`ICollectionFixture<T>`)는 테스트 메서드의 Skip 어트리뷰트와 독립적으로 `InitializeAsync()`를 실행한다.
- **A2**: `RequiresDesktopFactAttribute.IsInteractiveDesktop()` 메서드의 환경 감지 로직은 신뢰할 수 있다 (CI, GITHUB_ACTIONS, UserInteractive 체크).
- **A3**: xUnit v2에서는 `SkipException`이 공식 지원되지 않으므로, Fixture 수준에서 예외를 throw하면 테스트가 "Failed"로 표시된다 (Skip이 아닌 Error).
- **A4**: 대화형 데스크톱 세션에서의 E2E 테스트 동작은 변경하지 않는다.
- **A5**: FlaUI 4.0.0의 UIAutomation은 비대화형 세션에서 구조적으로 동작 불가능하므로, 우회가 아닌 조기 감지가 유일한 해결책이다.

## 3. Requirements (요구사항)

### REQ-E2E-001: AppFixture 비대화형 환경 조기 감지 (Fast-Fail)

**WHEN** AppFixture.InitializeAsync()가 비대화형 세션에서 호출되면,
**THEN** 시스템은 프로세스를 시작하지 않고 즉시 반환(early return)하며, `IsDesktopAvailable` 플래그를 `false`로 설정해야 한다.

- 30초 폴링 hang을 완전히 제거한다
- WPF 프로세스 시작, FlaUI Application attach, UIAutomation 초기화를 모두 건너뛴다
- 실행 시간: 30초 이상 -> 1초 미만

### REQ-E2E-002: E2ETestBase Graceful Skip

**WHEN** E2ETestBase 생성자에서 AppFixture.IsDesktopAvailable이 false이면,
**THEN** 테스트는 명확한 사유 메시지와 함께 실행을 건너뛰어야 한다.

- `MainWindow` 프로퍼티 접근 시 `InvalidOperationException` 대신 Skip 메커니즘 사용
- xUnit v2 호환: `Skip` 속성 또는 동등한 메커니즘 활용
- Skip 메시지에 "비대화형 환경에서 UIAutomation 사용 불가" 사유 포함

### REQ-E2E-003: 디버그 출력 및 로깅 개선

시스템은 **항상** AppFixture 초기화 과정에서 다음 정보를 진단 출력으로 기록해야 한다:

- 환경 감지 결과 (대화형/비대화형, CI 여부)
- 데스크톱 사용 불가 시 Skip 사유
- 대화형 세션에서: 프로세스 시작 경로, PID, MainWindow 감지 상태
- `ITestOutputHelper` 또는 `Trace.WriteLine`을 통한 진단 출력

### REQ-E2E-004: 개발자 E2E 테스트 실행 가이드

시스템은 **가능하면** E2E 테스트의 대화형 세션 실행 방법을 문서화해야 한다:

- PowerShell/Visual Studio 터미널에서의 실행 명령
- CI 환경 변수 설정에 따른 동작 차이 설명
- 디버그 정보 수집 방법 (FlaUI, UIAutomation 트리 검사)
- 문서 위치: `docs/e2e-testing-guide.md` 또는 테스트 프로젝트 README

## 4. Specifications (세부 사양)

### 4.1 IsDesktopAvailable 플래그 설계

```
AppFixture:
  + public bool IsDesktopAvailable { get; private set; }

  InitializeAsync():
    1. IsDesktopAvailable = IsInteractiveDesktop()
    2. IF !IsDesktopAvailable:
       - 진단 메시지 출력 ("E2E tests skipped: non-interactive session")
       - return (프로세스 시작 없이 즉시 반환)
    3. ELSE: 기존 로직 실행 (프로세스 시작, MainWindow 대기, 메뉴 워밍업)
```

### 4.2 IsInteractiveDesktop() 로직 통합

`RequiresDesktopFactAttribute.IsInteractiveDesktop()` 로직을 `AppFixture`에서 재사용:
- `CI=true` 환경 변수 체크
- `GITHUB_ACTIONS=true` 환경 변수 체크
- `Environment.UserInteractive` 체크
- 중복 코드 방지를 위해 공통 헬퍼 메서드 추출 고려 (`DesktopDetector` 또는 기존 어트리뷰트의 static 메서드 public 전환)

### 4.3 E2ETestBase Skip 전략

xUnit v2에서 Fixture 수준 Skip은 공식 지원되지 않으므로, 테스트 메서드 수준에서 처리:

```
E2ETestBase:
  constructor(AppFixture fixture):
    Fixture = fixture
    IF !fixture.IsDesktopAvailable:
      // 모든 테스트 메서드가 Skip되도록 보장
      // 방법 1: 각 테스트에서 [RequiresDesktopFact] 이미 적용됨
      // 방법 2: MainWindow 접근 시 명확한 에러 메시지 제공
```

### 4.4 기존 동작 보존

- 대화형 데스크톱 세션에서의 전체 E2E 테스트 흐름 변경 없음
- `[RequiresDesktopFact]` 어트리뷰트의 기존 Skip 메커니즘 유지
- 메뉴 워밍업 로직 (90초 타임아웃) 변경 없음
- `DisposeAsync()` 정리 로직 변경 없음 (프로세스 미시작 시 정리 불필요)

## 5. Constraints (제약사항)

- xUnit 2.9.3 - Collection Fixture에서 `SkipException` 미지원 (xUnit v3 기능)
- FlaUI 4.0.0 - UIAutomation은 Windows 데스크톱 세션 필수
- .NET 8.0-windows - Windows 전용 타겟 프레임워크
- 기존 17개 E2E 테스트의 `[RequiresDesktopFact]` 어트리뷰트 유지 필수

## 6. Traceability (추적성)

| 요구사항 | 관련 파일 | 테스트 |
|---------|----------|--------|
| REQ-E2E-001 | AppFixture.cs | CI 환경에서 1초 미만 완료 검증 |
| REQ-E2E-002 | E2ETestBase.cs | 비대화형에서 Skip 표시 검증 |
| REQ-E2E-003 | AppFixture.cs | 진단 출력 존재 확인 |
| REQ-E2E-004 | docs/e2e-testing-guide.md | 문서 존재 확인 |

---

**참조**: `.moai/specs/SPEC-E2E-001/research.md` - 기술 분석 상세
