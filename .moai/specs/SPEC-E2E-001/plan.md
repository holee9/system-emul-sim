---
id: SPEC-E2E-001
type: plan
version: "1.0.0"
status: draft
created: "2026-03-13"
updated: "2026-03-13"
tags: [e2e, testing, infrastructure, wpf, flaui]
---

# SPEC-E2E-001 구현 계획

## 개요

AppFixture의 비대화형 환경 조기 감지(Fast-Fail)와 E2ETestBase의 Graceful Skip 구현을 통해, CI/bash 환경에서 발생하는 30초 hang을 제거한다.

## 마일스톤

### Primary Goal: AppFixture Fast-Fail 구현

**범위**: `Infrastructure/AppFixture.cs` 수정

**작업 항목**:

1. `IsDesktopAvailable` public 프로퍼티 추가
2. `IsInteractiveDesktop()` static 메서드 추가 (또는 `RequiresDesktopFactAttribute`에서 재사용)
3. `InitializeAsync()` 진입부에 환경 감지 로직 삽입
4. 비대화형 시 프로세스 시작 건너뛰기 (즉시 return)
5. 진단 출력 추가 (`Trace.WriteLine` 또는 `Console.Error.WriteLine`)

**기술 접근**:

환경 감지 로직 통합 방안:
- 방안 A: `RequiresDesktopFactAttribute.IsInteractiveDesktop()`을 `internal static`으로 변경하여 재사용
- 방안 B: `DesktopEnvironment` 정적 헬퍼 클래스 신설
- **권장**: 방안 A (최소 변경, 중복 없음, 동일 어셈블리 내)

`InitializeAsync()` 수정 구조:
```
public async Task InitializeAsync()
{
    IsDesktopAvailable = RequiresDesktopFactAttribute.IsInteractiveDesktop();

    if (!IsDesktopAvailable)
    {
        Trace.WriteLine("[AppFixture] Non-interactive session detected. Skipping WPF process launch.");
        return;  // 프로세스 시작 없이 즉시 반환
    }

    // ... 기존 프로세스 시작 및 MainWindow 대기 로직
}
```

**리스크**:
- `RequiresDesktopFactAttribute`의 접근 제한자 변경 시 다른 테스트 프로젝트 영향 가능 -> 동일 어셈블리이므로 `internal` 충분
- `DisposeAsync()` 에서 `_appProcess == null` 케이스 처리 필요 -> 기존 null 체크 로직으로 안전

### Secondary Goal: E2ETestBase Graceful Skip

**범위**: `Infrastructure/E2ETestBase.cs` 수정

**작업 항목**:

1. `MainWindow` 프로퍼티의 예외 메시지 개선
2. 비대화형 환경에서의 테스트 동작 검증
3. 기존 `[RequiresDesktopFact]` 어트리뷰트와의 연동 확인

**기술 접근**:

xUnit v2에서 Fixture 수준 Skip은 불가능하므로 이중 방어 전략 사용:
- 1차 방어: `[RequiresDesktopFact]` 어트리뷰트가 각 테스트 메서드를 Skip
- 2차 방어: `MainWindow` 프로퍼티 접근 시 명확한 에러 메시지

```
protected AutomationElement MainWindow =>
    Fixture.IsDesktopAvailable
        ? Fixture.MainWindow ?? throw new InvalidOperationException("Main window not available - WPF process may have failed to start")
        : throw new InvalidOperationException("E2E tests require interactive desktop session. Run from Visual Studio or PowerShell desktop terminal.");
```

실질적으로 `[RequiresDesktopFact]`가 모든 테스트를 이미 Skip 처리하므로, `MainWindow` 접근은 발생하지 않는다. 이 변경은 안전망(safety net) 역할이다.

### Tertiary Goal: 진단 로깅 강화

**범위**: `Infrastructure/AppFixture.cs`

**작업 항목**:

1. 환경 감지 결과 로깅
2. 프로세스 시작 경로 및 PID 로깅 (대화형 세션)
3. MainWindow 감지 성공/실패 로깅
4. 워밍업 진행 상황 로깅

**기술 접근**:

`System.Diagnostics.Trace`를 사용하여 진단 출력:
- `Trace.WriteLine("[AppFixture] ...")` 형식
- xUnit의 `ITestOutputHelper`는 Fixture에서 직접 사용 불가 (테스트 메서드 레벨 DI)
- `Trace` 리스너를 통해 테스트 러너 출력에 포함 가능

로깅 포인트:
- InitializeAsync() 진입: 환경 감지 결과
- 프로세스 시작: exe 경로, PID
- MainWindow 감지: 성공 시간, 실패 시 타임아웃 경과
- 메뉴 워밍업: 각 메뉴 아이템 발견 시간

### Optional Goal: E2E 테스트 실행 가이드 문서

**범위**: `docs/e2e-testing-guide.md` 또는 테스트 프로젝트 README

**작업 항목**:

1. 대화형 세션에서 E2E 테스트 실행 방법
2. CI 환경에서의 자동 Skip 동작 설명
3. FlaUI 디버깅 팁 (UIAutomation 트리 검사)
4. 일반적인 문제 해결 가이드

**내용 구성**:
- 사전 요구사항 (빌드 완료, .exe 존재 확인)
- PowerShell 터미널에서의 실행 명령
- Visual Studio Test Explorer에서의 실행
- 환경 변수 설정 (`CI=true` 시 Skip 동작)
- 디버그 모드 실행 및 진단 출력 확인
- FlaUI Inspect 도구 사용법

## 아키텍처 설계 방향

### 변경 범위 최소화 원칙

이 SPEC은 기존 E2E 테스트 인프라에 **최소한의 변경**으로 Fast-Fail을 구현한다:
- 새로운 클래스 생성 없음 (기존 파일 수정만)
- 기존 테스트 메서드 변경 없음
- 대화형 세션에서의 동작 100% 보존

### 의존성 그래프

```
RequiresDesktopFactAttribute  --[IsInteractiveDesktop()]--> AppFixture
                                                              |
                                                              v
                                                         E2ETestBase
                                                              |
                                                              v
                                                    AboutDialogE2ETests
                                                    CoreFlowE2ETests
                                                    HelpSystemE2ETests
                                                    ParameterExtractionE2ETests
```

### 수정 파일 목록

| 파일 | 변경 유형 | LOC 예상 |
|------|----------|---------|
| `Infrastructure/RequiresDesktopFactAttribute.cs` | 접근 제한자 변경 (`internal static`) | ~2 |
| `Infrastructure/AppFixture.cs` | Fast-fail + 로깅 추가 | ~15 |
| `Infrastructure/E2ETestBase.cs` | MainWindow 메시지 개선 | ~3 |
| `docs/e2e-testing-guide.md` (신규) | 개발자 가이드 | ~60 |

## 검증 전략

1. **CI 환경 시뮬레이션**: `CI=true` 환경 변수 설정 후 테스트 실행 -> 17개 테스트 전체 Skip, 총 소요 시간 < 5초
2. **대화형 세션 검증**: Visual Studio 또는 PowerShell 데스크톱 터미널에서 테스트 실행 -> 기존과 동일하게 동작
3. **진단 출력 확인**: 비대화형 세션에서 "[AppFixture] Non-interactive session detected" 메시지 출력 확인
