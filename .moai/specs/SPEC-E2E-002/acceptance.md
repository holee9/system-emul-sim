# SPEC-E2E-002 Acceptance Criteria

## AC-001: E2ELogger 파일 생성

- [ ] `dotnet test` 실행 후 `TestResults/Logs/e2e_*.log` 파일 생성 확인
- [ ] 로그 파일에 `[INFO]`, `[STEP]`, `[WARN]`, `[FAIL]` 레벨 항목 포함
- [ ] AppFixture 초기화 단계 모두 로깅됨 (Process start, MainWindow detect, warmup)
- [ ] 타임스탬프 형식: `[HH:mm:ss.fff]`

## AC-002: 자동 스크린샷 (실패 시)

- [ ] `RunWithScreenshot("testName", () => { throw new Exception(); })` 호출 시 스크린샷 생성
- [ ] 성공 케이스에서는 스크린샷 미생성
- [ ] 파일 경로: `TestResults/Screenshots/{testName}_{timestamp}.png`

## AC-003: 타이밍 계측

- [ ] 로그에서 `MainWindow found after Xs` 확인
- [ ] 로그에서 `Warmup done: File (Xs)` 확인
- [ ] 로그에서 `Warmup done: Help (Xs)` 확인
- [ ] 로그에서 `Total init: Xs` 확인

## AC-004: UIAutomation 트리 덤프

- [ ] WaitForElementAsync 타임아웃 시 로그에 트리 덤프 포함
- [ ] 트리 덤프에 `[ControlType] id='...' name='...'` 형식 항목 포함
- [ ] 최대 깊이 4

## AC-005: PowerShell 스크립트

- [ ] `Run-E2ETests.ps1 -NoBuild` 실행 성공
- [ ] CI 환경 변수 제거 후 실행 (interactive mode)
- [ ] 결과 로그 파일 경로 출력
- [ ] `--filter` 파라미터로 특정 테스트만 실행 가능

## AC-006: 회귀 방지 (SPEC-E2E-001)

- [ ] `$env:CI="true"` 설정 후 21개 테스트 모두 Skip (0.84s 이내)
- [ ] 빌드 경고 0개
