# Research: SPEC-E2E-003 — FlaUI E2E 환경 정비

Generated: 2026-03-13

## 현재 구현 현황

### SPEC-E2E-001/002 완료 내용

| SPEC | 커밋 | 주요 변경 |
|------|------|-----------|
| SPEC-E2E-001 | 6503cf8 | AppFixture fast-fail (CI 환경 30초 hang 제거), `IsDesktopAvailable` 플래그 |
| SPEC-E2E-002 | d16883d, 5f1769b | E2ELogger, TreeDumper, WaitHelper, E2ETestBase, Run-E2ETests.ps1 |

### 환경 감지 로직 (`RequiresDesktopFactAttribute.cs`)

```
IsInteractiveDesktop() 판정:
  1. CI=true         → false (skip)
  2. GITHUB_ACTIONS  → false (skip)
  3. UserInteractive → 결과 반환
```

### 핵심 파일 현황

| 파일 | 라인 | 상태 |
|------|------|------|
| `AppFixture.cs` | 205 | SPEC-E2E-001/002 완료 |
| `E2ETestBase.cs` | 114 | SPEC-E2E-002 완료 |
| `E2ELogger.cs` | 94 | SPEC-E2E-002 완료 |
| `TreeDumper.cs` | 46 | SPEC-E2E-002 완료 |
| `WaitHelper.cs` | 49 | 기본만 구현 |
| `ScreenshotHelper.cs` | 34 | 미통합 |
| `RetryFactAttribute.cs` | 79 | 수동 재시도만 |
| `RequiresDesktopFactAttribute.cs` | 26 | 불완전 (bash 미감지) |
| `Run-E2ETests.ps1` | 120 | SPEC-E2E-002 완료 |
| `.github/workflows/e2e-tests.yml` | 68 | workflow_dispatch only |

### 테스트 커버리지 현황

| 파일 | 테스트 수 | 내용 |
|------|----------|------|
| `AppLaunchTests.cs` | 4 | 기본 실행, 버전, 탭, 메뉴 |
| `CoreFlowE2ETests.cs` | 5 | 핵심 플로우 |
| `HelpSystemE2ETests.cs` | 8 | 도움말 시스템 |
| `AboutDialogE2ETests.cs` | 3 | About 다이얼로그 |
| `ParameterExtractionE2ETests.cs` | 2 | 파라미터 추출 |
| **합계** | **22** | |

---

## 남아있는 이슈

### P1 — Critical

**1. 로컬 bash 30초 hang (CI 아닌 환경)**
- 증상: CI 변수 없는 bash에서 `UserInteractive=true`이므로 WPF 시작 시도, 창 감지 실패 시 30초 타임아웃
- 근본 원인: `UserInteractive`는 bash vs. 데스크톱 세션을 구분하지 못함
- 현재 상태: SPEC-E2E-001이 CI 환경은 해결했지만 일반 bash는 미해결

**2. 재시도 루프에 로깅 없음**
- 패턴: `for (int attempt = 0; attempt < 200; attempt++) { /* 로깅 없음 */ }`
- 200회 × 200ms = 40초 대기 중 진행 상황 전혀 없음
- 파일: `AppLaunchTests.cs:57-62`, `HelpSystemE2ETests.cs` 다수

**3. RetryFactAttribute 비기능**
- 속성은 있지만 실제 자동 재시도 미구현
- `RetryHelper`는 존재하지만 명시적 테스트 코드 호출 필요

### P2 — Important

**4. ITestOutputHelper 미연결**
- 모든 테스트 생성자가 `ITestOutputHelper output`을 받지만 사용 안 함
- 로그가 파일(`E2ELogger`)로만 가고 xUnit 출력 콘솔에는 없음

**5. 모달 다이얼로그 Win32 fragility**
- `FindWindow(null, "About X-ray Detector GUI")` — 제목 하드코딩
- 한국어 제목 포함 → 대소문자/인코딩 이슈 가능성
- 폴백 전략 없음

**6. 하드코딩 딜레이**
- 100ms, 200ms, 300ms, 500ms, 2000ms 각지에 산재
- 환경변수로 조정 불가

### P3 — Nice to Have

**7. 메뉴 워밍업 90초 하드코딩**
- WPF AutomationPeer 등록에 26-40초 소요 (첫 확장 시)

**8. ScreenshotHelper 미호출**
- 구현은 있지만 실패 시 자동 캡처 연결 없음

**9. 프로세스 진단 로깅 없음**
- GUI.Application PID, 메모리, 종료 코드 미기록
- 초기화 실패 스택트레이스 없음

---

## 개선 기회

### 인프라 갭

| 항목 | 설명 | 우선순위 |
|------|------|----------|
| 환경 감지 강화 | bash vs. 데스크톱 세션 구분 (SESSIONNAME, DISPLAY, XRAY_E2E_FORCE 환경변수 활용) | P1 |
| TestContext ThreadLocal | xUnit ITestOutputHelper을 fixture에서 접근 가능하게 | P2 |
| WaitHelper 강화 | 시도 횟수, 경과 시간 로깅, tree dump 콜백 | P1 |
| RetryFactAttribute 자동화 | 실제 자동 재시도 + 백오프 구현 | P2 |
| 실패 훅 | 테스트 실패 시 스크린샷 + tree dump 자동 캡처 | P2 |

### 진단 강화

| 항목 | 설명 |
|------|------|
| 요소 검색 실패 시 tree dump | 요소를 못 찾으면 자동으로 UIAutomation 트리 덤프 |
| 타이밍 텔레메트리 | 모든 폴링과 재시도에 시도 횟수 + 경과 시간 기록 |
| 프로세스 진단 | GUI.Application 프로세스 상태 모니터링 |
| 조건부 상세 로깅 | `XRAY_E2E_DEBUG` 환경변수로 상세 로깅 제어 |

---

## 구현 접근법 권장사항

### SPEC-E2E-003 범위 (권장)

**포함:**
- `IsInteractiveDesktop()` 강화 — SESSIONNAME, 터미널 타입 감지 추가
- `WaitHelper` 개선 — 시도 횟수 로깅, tree dump 통합
- `ScreenshotHelper` 실패 훅 연결
- `ITestOutputHelper` → E2ELogger 브리지 (ThreadLocal)
- 환경변수 기반 타임아웃 설정 (`XRAY_E2E_TIMEOUT_MS`)
- `Run-E2ETests.ps1` 개선 — 사전 환경 체크 추가

**제외 (별도 SPEC):**
- 새 E2E 테스트 케이스 추가
- Headless WPF 테스팅 (아키텍처 한계)
- GitHub Actions 데스크톱 모드

### 핵심 수정 파일

```
tools/GUI.Application/tests/GUI.Application.E2ETests/
├── Infrastructure/
│   ├── AppFixture.cs              ← 환경 감지 강화
│   ├── RequiresDesktopFactAttribute.cs  ← SESSIONNAME/터미널 감지
│   ├── WaitHelper.cs              ← 로깅 + tree dump 콜백 추가
│   ├── E2ELogger.cs               ← ThreadLocal xUnit 출력 연결
│   ├── E2ETestBase.cs             ← ThreadLocal 컨텍스트 초기화
│   ├── ScreenshotHelper.cs        ← 실패 훅 통합
│   ├── RetryFactAttribute.cs      ← 자동 재시도 구현
│   └── (신규) EnvironmentDetector.cs  ← bash vs. 데스크톱 로직
└── Run-E2ETests.ps1               ← 사전 환경 체크 강화
```

### 성공 기준

- 로컬 bash 환경에서 30초 hang 없음 (빠른 감지 + skip)
- 모든 요소 검색 재시도가 로깅됨 (40초 침묵 없음)
- 테스트 실패 시 스크린샷 자동 캡처
- xUnit 출력에 구조화된 per-test 로그 표시
- `XRAY_E2E_DEBUG=1`로 상세 진단 로그 활성화 가능
