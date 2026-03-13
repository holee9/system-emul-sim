---
id: SPEC-E2E-001
type: acceptance
version: "1.0.0"
status: draft
created: "2026-03-13"
updated: "2026-03-13"
tags: [e2e, testing, infrastructure, wpf, flaui]
---

# SPEC-E2E-001 수락 기준

## 시나리오 1: CI/비대화형 환경에서 Fast-Fail (REQ-E2E-001)

### Scenario 1.1: CI 환경에서 AppFixture 즉시 반환

```gherkin
Given CI=true 환경 변수가 설정된 비대화형 세션
When E2E 테스트 스위트가 실행되면
Then AppFixture.InitializeAsync()는 1초 미만에 완료되어야 한다
And WPF 프로세스(GUI.Application.exe)는 시작되지 않아야 한다
And AppFixture.IsDesktopAvailable은 false여야 한다
```

### Scenario 1.2: GITHUB_ACTIONS 환경에서 Fast-Fail

```gherkin
Given GITHUB_ACTIONS=true 환경 변수가 설정된 환경
When E2E 테스트 스위트가 실행되면
Then AppFixture.InitializeAsync()는 프로세스를 시작하지 않고 즉시 반환해야 한다
And 30초 타임아웃 hang이 발생하지 않아야 한다
```

### Scenario 1.3: bash 비대화형 세션에서 Fast-Fail

```gherkin
Given Environment.UserInteractive가 false인 비대화형 bash 세션
And CI 또는 GITHUB_ACTIONS 환경 변수가 미설정 상태
When E2E 테스트 스위트가 실행되면
Then AppFixture.IsDesktopAvailable은 false여야 한다
And 프로세스 시작을 건너뛰고 즉시 반환해야 한다
```

## 시나리오 2: E2ETestBase Graceful Skip (REQ-E2E-002)

### Scenario 2.1: 비대화형 환경에서 테스트 Skip

```gherkin
Given AppFixture.IsDesktopAvailable이 false인 상태
When 17개 E2E 테스트가 실행되면
Then 모든 테스트는 "Skipped" 상태로 표시되어야 한다
And "Failed" 또는 "Error" 상태의 테스트가 없어야 한다
And Skip 사유에 "interactive desktop" 또는 "UIAutomation unavailable" 문구가 포함되어야 한다
```

### Scenario 2.2: MainWindow 접근 시 명확한 에러 메시지

```gherkin
Given AppFixture.IsDesktopAvailable이 false인 상태
When E2ETestBase.MainWindow 프로퍼티에 접근하면
Then InvalidOperationException이 발생해야 한다
And 예외 메시지에 "interactive desktop session" 문구가 포함되어야 한다
And 실행 방법 안내 (Visual Studio 또는 PowerShell 터미널) 가 메시지에 포함되어야 한다
```

## 시나리오 3: 대화형 세션 동작 보존

### Scenario 3.1: 대화형 데스크톱에서 정상 동작

```gherkin
Given 대화형 Windows 데스크톱 세션 (Visual Studio 또는 PowerShell 터미널)
And GUI.Application.exe가 빌드 완료 상태
When E2E 테스트 스위트가 실행되면
Then AppFixture.IsDesktopAvailable은 true여야 한다
And WPF 프로세스가 정상 시작되어야 한다
And MainWindow가 30초 이내에 감지되어야 한다
And 메뉴 워밍업이 정상 수행되어야 한다
And 모든 E2E 테스트가 정상 실행되어야 한다 (Pass 또는 Fail)
```

### Scenario 3.2: 기존 RequiresDesktopFact 동작 유지

```gherkin
Given 대화형 데스크톱 세션
When [RequiresDesktopFact] 어트리뷰트가 적용된 테스트가 실행되면
Then 테스트는 Skip 없이 정상 실행되어야 한다
And RequiresDesktopFactAttribute의 기존 로직이 변경 없이 동작해야 한다
```

## 시나리오 4: 진단 출력 (REQ-E2E-003)

### Scenario 4.1: 비대화형 환경 진단 출력

```gherkin
Given 비대화형 세션에서 E2E 테스트가 실행되면
When AppFixture.InitializeAsync()가 호출되면
Then "Non-interactive session detected" 진단 메시지가 출력되어야 한다
And 감지된 환경 정보 (CI, GITHUB_ACTIONS, UserInteractive 값)가 포함되어야 한다
```

### Scenario 4.2: 대화형 환경 진단 출력

```gherkin
Given 대화형 데스크톱 세션에서 E2E 테스트가 실행되면
When AppFixture.InitializeAsync()가 WPF 프로세스를 시작하면
Then 실행 파일 경로와 프로세스 PID가 진단 메시지에 포함되어야 한다
And MainWindow 감지 성공 시간이 출력되어야 한다
```

## 시나리오 5: 전체 실행 시간 검증

### Scenario 5.1: CI 환경 총 실행 시간

```gherkin
Given CI=true 환경에서 E2E 테스트 프로젝트 실행
When dotnet test가 완료되면
Then 총 실행 시간은 10초 미만이어야 한다 (기존 30초+ -> 10초 미만)
And 17개 테스트 모두 Skipped 상태여야 한다
And 테스트 결과에 0 Failed가 표시되어야 한다
```

## Quality Gate

| 항목 | 기준 | 검증 방법 |
|------|------|----------|
| Fast-Fail 동작 | CI에서 InitializeAsync < 1초 | dotnet test + 시간 측정 |
| Skip 처리 | 17개 테스트 전체 Skipped (CI) | dotnet test --verbosity normal |
| 대화형 보존 | 데스크톱에서 기존과 동일 동작 | 수동 검증 (Visual Studio) |
| 진단 출력 | 환경 정보 메시지 존재 | 출력 로그 확인 |
| 코드 변경 최소화 | 수정 파일 3개 이하 | git diff --stat |
| 기존 테스트 미파괴 | 모든 비-E2E 테스트 Pass | dotnet test (전체 솔루션) |

## Definition of Done

- [ ] AppFixture.InitializeAsync()가 비대화형 환경에서 1초 미만에 반환
- [ ] CI 환경에서 17개 E2E 테스트 전체 Skipped (0 Failed)
- [ ] 대화형 데스크톱에서 기존 E2E 테스트 동작 100% 보존
- [ ] 진단 로깅 메시지가 비대화형/대화형 모두에서 출력
- [ ] 기존 비-E2E 테스트에 영향 없음 확인
- [ ] E2E 테스트 실행 가이드 문서 작성 (Optional)
