---
id: SPEC-E2E-004
version: "1.0.0"
status: planned
created: "2026-03-13"
updated: "2026-03-13"
author: drake
priority: high
---

# SPEC-E2E-004 수락 기준: AppFixture Attach Mode

## 시나리오 1: Attach 모드 -- 유효 PID로 연결 (TAG-001)

```gherkin
Given GUI.Application.exe가 대화형 데스크톱에서 실행 중이고
  And 해당 프로세스의 PID를 알고 있을 때
When XRAY_E2E_ATTACH_PID 환경변수를 해당 PID로 설정하고
  And dotnet test를 실행하면
Then AppFixture는 새 프로세스를 시작하지 않고
  And FlaUI.Core.Application.Attach()를 통해 기존 프로세스에 연결하고
  And EnvironmentDetector.IsInteractiveDesktop() 검사를 건너뛰고
  And WarmupSingleMenuAsync("File", "MenuFileExit")를 실행하고
  And WarmupSingleMenuAsync("Help", "MenuHelpTopics")를 실행한다
```

## 시나리오 2: Attach 모드 -- 무효 PID 처리 (TAG-006)

```gherkin
Given GUI.Application.exe가 실행 중이지 않을 때
When XRAY_E2E_ATTACH_PID를 존재하지 않는 PID(예: 99999)로 설정하고
  And dotnet test를 실행하면
Then AppFixture는 즉시 의미 있는 오류 메시지와 함께 실패하고
  And 30초간 hang 하지 않는다
```

## 시나리오 3: Attach 모드 -- 프로세스 종료 금지 (TAG-005)

```gherkin
Given Attach 모드로 실행 중인 프로세스에 연결되어 있을 때
When 테스트가 완료되고 AppFixture.DisposeAsync()가 호출되면
Then 대상 프로세스는 종료되지 않고
  And UIA3Automation만 dispose 되고
  And 대상 프로세스가 계속 실행 중임을 확인한다
```

## 시나리오 4: EnvironmentDetector.IsAttachMode() -- 환경변수 설정됨 (TAG-002)

```gherkin
Given XRAY_E2E_ATTACH_PID 환경변수가 "12345"로 설정되어 있을 때
When EnvironmentDetector.IsAttachMode()를 호출하면
Then true를 반환한다
```

## 시나리오 5: EnvironmentDetector.IsAttachMode() -- 환경변수 미설정 (TAG-002)

```gherkin
Given XRAY_E2E_ATTACH_PID 환경변수가 설정되어 있지 않을 때
When EnvironmentDetector.IsAttachMode()를 호출하면
Then false를 반환한다
```

## 시나리오 6: EnvironmentDetector.IsAttachMode() -- 빈 문자열 (TAG-002)

```gherkin
Given XRAY_E2E_ATTACH_PID 환경변수가 빈 문자열("")로 설정되어 있을 때
When EnvironmentDetector.IsAttachMode()를 호출하면
Then false를 반환한다
```

## 시나리오 7: Run-E2ETests.ps1 -AttachPid (TAG-003)

```gherkin
Given GUI.Application.exe가 PID 1234로 실행 중일 때
When Run-E2ETests.ps1 -AttachPid 1234를 실행하면
Then "Attaching to existing GUI.Application (PID=1234)" 메시지가 표시되고
  And XRAY_E2E_ATTACH_PID 환경변수가 1234로 설정되고
  And 빌드 단계가 건너뛰어지고
  And dotnet test가 실행된다
```

## 시나리오 8: Run-E2ETests.ps1 -AttachPid 무효 PID (TAG-003)

```gherkin
Given PID 99999에 해당하는 프로세스가 없을 때
When Run-E2ETests.ps1 -AttachPid 99999를 실행하면
Then 오류 메시지가 출력되고
  And exit code 1로 종료된다
```

## 시나리오 9: 하위 호환성 -- Launch 모드 유지 (TAG-001)

```gherkin
Given XRAY_E2E_ATTACH_PID 환경변수가 설정되어 있지 않고
  And 대화형 데스크톱 환경일 때
When dotnet test를 실행하면
Then AppFixture는 기존과 동일하게 새 프로세스를 시작하고
  And EnvironmentDetector.IsInteractiveDesktop() 검사를 수행하고
  And 테스트 완료 후 프로세스를 종료한다
```

## 시나리오 10: XRAY_E2E_FORCE와 독립성

```gherkin
Given XRAY_E2E_FORCE=1이 설정되어 있고
  And XRAY_E2E_ATTACH_PID가 설정되어 있지 않을 때
When dotnet test를 실행하면
Then AppFixture는 기존 Force 모드 동작을 유지하고
  And Attach 모드와 무관하게 동작한다
```

## 필수 단위 테스트 목록

| 테스트 이름 | 검증 대상 | TAG |
|------------|----------|-----|
| `AttachMode_ValidPid_AttachesWithoutLaunching` | 유효 PID로 연결 시 프로세스 미시작 | TAG-001 |
| `AttachMode_InvalidPid_ThrowsMeaningfulError` | 무효 PID 시 명확한 예외 | TAG-006 |
| `AttachMode_DoesNotKillProcessOnDispose` | Dispose 시 프로세스 미종료 | TAG-005 |
| `EnvironmentDetector_IsAttachMode_WhenEnvVarSet` | 환경변수 설정 시 true | TAG-002 |
| `EnvironmentDetector_IsAttachMode_ReturnsFalse_WhenEnvVarNotSet` | 환경변수 미설정 시 false | TAG-002 |

## Quality Gate 기준

- 모든 기존 22개 E2E 단위 테스트 통과 (regression 없음)
- 신규 단위 테스트 5개 이상 통과
- Attach 모드/Launch 모드 분기 로직 커버리지 100%
- EnvironmentDetector.IsAttachMode() 커버리지 100%
- Run-E2ETests.ps1 -AttachPid 매개변수 정상 동작

## Definition of Done

- [ ] EnvironmentDetector.IsAttachMode() 구현 및 테스트
- [ ] AppFixture.InitializeAsync() Attach 모드 분기 구현
- [ ] AppFixture.DisposeAsync() Attach 모드 분기 구현
- [ ] Run-E2ETests.ps1 -AttachPid 매개변수 구현
- [ ] 무효 PID 오류 처리 구현 및 테스트
- [ ] 하위 호환성 검증 (기존 테스트 전체 통과)
- [ ] 단위 테스트 5개 이상 작성 및 통과
