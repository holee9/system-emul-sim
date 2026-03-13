---
id: SPEC-E2E-003
type: acceptance
version: 1.0.0
---

# SPEC-E2E-003 수락 기준

## AC-1: 로컬 bash fast-fail

**Given** CI 환경변수(`CI`, `GITHUB_ACTIONS`)가 설정되지 않은 bash 환경 (Git Bash, WSL, Windows Terminal)
**And** `System.Environment.UserInteractive`가 `true`
**And** 실제 대화형 데스크톱 세션이 아닌 환경 (Explorer shell 없음)

**When** `dotnet test` 를 실행하면

**Then** 5초 이내에 22개 테스트가 "Skip" 결과로 보고됨
**And** Skip 사유에 환경 감지 정보가 포함됨 (예: "Non-interactive terminal detected: SESSIONNAME=not set, TERM=xterm-256color")
**And** GUI.Application 프로세스가 시작되지 않음
**And** 30초 hang이 발생하지 않음

### 검증 방법

- Git Bash에서 `dotnet test` 실행 후 소요 시간 측정
- Windows Terminal (비대화형 세션)에서 동일 검증
- `XRAY_E2E_FORCE=1` 설정 시 Skip 대신 실행 시도 확인

---

## AC-2: CI 환경 instant skip

**Given** `CI=true` 환경변수가 설정된 환경

**When** `dotnet test` 를 실행하면

**Then** 1초 이내에 22개 테스트가 "Skip" 결과로 보고됨
**And** 에러 또는 경고 없음
**And** Skip 사유에 "CI environment detected" 포함

### 검증 방법

- `CI=true dotnet test` 실행 후 결과 확인
- GitHub Actions workflow에서 실행 확인 (기존 `.github/workflows/e2e-tests.yml`)

---

## AC-3: 대화형 세션 전체 실행

**Given** 대화형 Windows 데스크톱 세션 (Explorer shell 실행 중)
**And** `GUI.Application.exe`가 빌드된 상태

**When** `Run-E2ETests.ps1` 또는 `dotnet test`를 실행하면

**Then** 22개 테스트가 정상 실행됨
**And** WaitHelper가 매 폴링마다 진행 상황 로그를 출력 (시도 횟수, 경과 시간)
**And** xUnit 출력 콘솔에 per-test 로그가 표시됨
**And** 기존 테스트의 Pass/Fail 결과가 변경되지 않음

### 검증 방법

- Windows 데스크톱 세션에서 `dotnet test --logger "console;verbosity=detailed"` 실행
- xUnit 출력에 WaitHelper 로그 포함 확인
- SPEC-E2E-002 커밋 시점의 테스트 결과와 비교하여 회귀 없음 확인

---

## AC-4: 요소 검색 실패 진단

**Given** 대화형 세션에서 GUI.Application이 실행 중
**And** 특정 UI 요소가 존재하지 않는 상황 (예: 비정상 종료 후)

**When** `WaitHelper.WaitForElementAsync`가 타임아웃되면

**Then** 로그에 다음 정보가 포함됨:
  - 검색 대상 요소 설명 (description 파라미터)
  - 총 시도 횟수
  - 총 경과 시간 (ms)
  - UIAutomation tree dump (현재 윈도우의 전체 자동화 트리)
**And** 스크린샷이 `TestResults/Screenshots/` 디렉토리에 자동 저장됨
**And** tree dump가 `.txt` 파일로 동일 디렉토리에 저장됨

### 검증 방법

- 의도적으로 존재하지 않는 요소를 검색하는 테스트 시나리오 실행
- 로그 출력 및 파일 생성 확인
- tree dump 내용이 유효한 UIAutomation 구조인지 확인

---

## AC-5: 디버그 모드 상세 로그

**Given** `XRAY_E2E_DEBUG=1` 환경변수가 설정된 상태
**And** 대화형 데스크톱 세션

**When** 테스트를 실행하면

**Then** xUnit 출력에 per-test 구조화 로그가 표시됨:
  - 테스트 시작/종료 타임스탬프
  - 환경 감지 요약 정보
  - WaitHelper 매 시도 상세 로그
  - 모든 테스트에 스크린샷 자동 저장 (실패뿐 아니라 성공도)
**And** 비디버그 모드 대비 추가 오버헤드가 테스트당 500ms 이내

### 검증 방법

- `XRAY_E2E_DEBUG=1 dotnet test` 실행
- xUnit 출력에 구조화 로그 포함 확인
- `XRAY_E2E_DEBUG` 미설정 시 상세 로그가 출력되지 않음 확인
- 디버그 모드 ON/OFF 간 실행 시간 비교

---

## Definition of Done

- [ ] 모든 AC(AC-1 ~ AC-5) 검증 통과
- [ ] 기존 22개 E2E 테스트 회귀 없음 (대화형 세션에서 동일 결과)
- [ ] CI 환경에서 instant skip 유지
- [ ] 로컬 bash에서 5초 이내 skip 완료
- [ ] 추가 NuGet 패키지 없음
- [ ] 코드 리뷰 완료
- [ ] `EnvironmentDetector` 단위 테스트 작성 (환경변수 모킹)
