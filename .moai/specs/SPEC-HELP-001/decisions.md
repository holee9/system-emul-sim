---
id: SPEC-HELP-001
type: decisions
version: 1.0.0
status: active
created: 2026-03-11
purpose: Architecture Decision Records for retrospective analysis
---

## Decision Log

### Overview

SPEC-HELP-001의 계획 수립 과정에서 내려진 모든 의사결정을 기록합니다.
각 결정은 ADR (Architecture Decision Record) 형식으로 문서화되며,
구현 후 회고(retrospective)에서 결정의 적절성을 평가하는 데 사용됩니다.

### Decision Status Legend

| Status | Meaning |
|--------|---------|
| ACCEPTED | 채택되어 구현 예정 |
| REJECTED | 검토 후 기각 |
| SUPERSEDED | 이후 결정으로 대체됨 |
| VALIDATED | 구현 후 검증 완료 |
| REVISIT | 회고에서 재검토 필요 |

---

## Phase 1 Decisions

### DEC-001: Markdown 렌더링 엔진 선택

- **Date**: 2026-03-11
- **Status**: ACCEPTED
- **Context**: Help 콘텐츠를 WPF에서 렌더링하는 방법 선택
- **Options**:
  - A) WebView2 + HTML: 완전한 렌더링, 180MB 런타임 의존성
  - B) FlowDocument + Markdig: 경량, WPF 네이티브, 서브셋 지원
  - C) RichTextBox + 수동 파싱: 의존성 없음, 구현 복잡도 높음
- **Decision**: **Option B** (FlowDocument + Markdig)
- **Rationale**: 내부 엔지니어링 도구에 180MB WebView2 런타임은 과도함. Markdig는 5KB 미만. FlowDocument는 WPF 네이티브로 추가 의존성 없음. 3개 에이전트 중 2개(expert-frontend, expert-backend)가 동의.
- **Consequences**: Markdown 서브셋만 지원 (heading, paragraph, table, code, list, link). 복잡한 HTML/CSS는 미지원.
- **Retrospective Questions**:
  - FlowDocument 렌더링 품질이 충분했는가?
  - 지원하지 못한 Markdown 기능으로 인한 불편이 있었는가?
  - Markdig NuGet 버전 호환성 이슈가 있었는가?

### DEC-002: MVVM Framework 선택

- **Date**: 2026-03-11
- **Status**: ACCEPTED
- **Context**: 기존 코드가 CommunityToolkit.Mvvm NuGet을 참조하지만, 실제로는 custom ObservableObject + RelayCommand 사용
- **Options**:
  - A) CommunityToolkit.Mvvm으로 전환: 표준화, [ObservableProperty] 속성 사용
  - B) 기존 custom RelayCommand 유지: 일관성, 마이그레이션 비용 없음
- **Decision**: **Option B** (기존 custom RelayCommand 유지)
- **Rationale**: 기존 코드 전체가 custom 패턴 사용. 전환 시 모든 ViewModel 수정 필요. 일관성 > 표준화.
- **Consequences**: CommunityToolkit.Mvvm의 source generator 활용 불가. 보일러플레이트 유지.
- **Retrospective Questions**:
  - ViewModel 보일러플레이트가 개발 속도에 영향을 주었는가?
  - 향후 CommunityToolkit.Mvvm 전환이 필요한 시점은 언제인가?

### DEC-003: ApplicationInfo 구현 방식

- **Date**: 2026-03-11
- **Status**: ACCEPTED
- **Context**: 앱 버전, 빌드 날짜 등 시스템 정보 접근 패턴
- **Options**:
  - A) AboutViewModel 내부에서 직접 Assembly 읽기
  - B) 별도 ApplicationInfo Singleton 서비스로 분리
- **Decision**: **Option B** (별도 Singleton)
- **Rationale**: StatusBar 버전, About 다이얼로그, Help 콘텐츠 등 여러 곳에서 공유 필요. expert-frontend 제안.
- **Consequences**: 의존성 주입 필요. 테스트 시 mock 가능.
- **Retrospective Questions**:
  - ApplicationInfo를 실제로 여러 곳에서 사용했는가?
  - Singleton 패턴이 테스트를 방해하지 않았는가?

### DEC-004: Clipboard 추상화

- **Date**: 2026-03-11
- **Status**: ACCEPTED
- **Context**: About 다이얼로그의 "클립보드에 복사" 기능 테스트 가능성
- **Options**:
  - A) `Clipboard.SetText()` 직접 호출
  - B) `IClipboardService` 인터페이스 추상화
- **Decision**: **Option B** (IClipboardService)
- **Rationale**: 단위테스트에서 Clipboard 접근 불가 (STA thread 필요). Mock으로 검증 가능.
- **Consequences**: 1개 인터페이스 + 1개 구현 클래스 추가.
- **Retrospective Questions**:
  - IClipboardService mock이 실제 테스트에서 효과적이었는가?
  - 추상화 없이도 테스트 가능했을 상황은 없었는가?

---

## Phase 2 Decisions

### DEC-005: HelpProvider AttachedProperty 상속 전략

- **Date**: 2026-03-11
- **Status**: ACCEPTED
- **Context**: WPF AttachedProperty `FrameworkPropertyMetadataOptions.Inherits`는 UserControl 경계에서 상속이 끊김
- **Options**:
  - A) Inherits 설정만 믿고 자동 상속 기대
  - B) 각 View 루트 요소에 명시적 HelpTopicId 설정
- **Decision**: **Option B** (명시적 설정)
- **Rationale**: TabControl 내 UserControl 경계에서 Inherits가 동작하지 않음. expert-frontend가 교차검증에서 정확히 지적.
- **Consequences**: 각 View에 `views:HelpProvider.HelpTopicId="topic-id"` 1줄씩 추가. 자동 상속보다 명확.
- **Retrospective Questions**:
  - 명시적 설정이 누락된 View가 있었는가?
  - 향후 UserControl 추가 시 HelpTopicId 누락 방지 체크리스트가 필요한가?

### DEC-006: Help 콘텐츠 저장 방식

- **Date**: 2026-03-11
- **Status**: ACCEPTED
- **Context**: Help Markdown 파일의 배포 및 버전 동기화 전략
- **Options**:
  - A) EmbeddedResource (어셈블리 내장)
  - B) Content 파일 (실행 폴더 동봉)
  - C) 외부 서버 호스팅
- **Decision**: **Option A** (EmbeddedResource)
- **Rationale**: 오프라인 환경 필수 (내부 엔지니어링 도구). 앱 버전과 100% 동기화. 별도 배포 불필요.
- **Consequences**: 콘텐츠 수정 시 빌드 필요. 런타임 수정 불가. (Phase 1 범위에서는 문제 없음)
- **Retrospective Questions**:
  - 빈번한 콘텐츠 수정 요구가 있었는가?
  - EmbeddedResource 로딩 성능에 이슈가 있었는가?

### DEC-007: Settings 저장 위치

- **Date**: 2026-03-11
- **Status**: ACCEPTED
- **Context**: Welcome Wizard "다시 보지 않기" 등 사용자 설정 저장
- **Options**:
  - A) `%LOCALAPPDATA%/XrayDetector/settings.json` (Windows 표준)
  - B) `%APPDATA%/XrayDetector/` (로밍 프로필)
  - C) Registry
- **Decision**: **Option A** (%LOCALAPPDATA%)
- **Rationale**: Windows 표준 경로. 로밍 불필요 (내부 도구). Registry는 접근 복잡.
- **Consequences**: JSON 파일 직접 관리. System.Text.Json으로 직렬화.
- **Retrospective Questions**:
  - 설정 파일 접근 권한 이슈가 있었는가?
  - 설정 항목이 증가하여 별도 설정 프레임워크가 필요했는가?

---

## Phase 5 Decisions

### DEC-008: E2E 테스트 프레임워크 선택

- **Date**: 2026-03-11
- **Status**: ACCEPTED
- **Context**: WPF GUI E2E 자동화 프레임워크 선택
- **Options**:
  - A) FlaUI.UIA3: WPF UIA 최적화, .NET 8 지원, 활발한 유지보수
  - B) Appium + WinAppDriver: 크로스 플랫폼, 설정 복잡, WinAppDriver 유지보수 불확실
  - C) White (TestStack.White): 레거시, 더 이상 유지보수 안됨
- **Decision**: **Option A** (FlaUI.UIA3)
- **Rationale**: WPF 전용 최적화. AutomationId 기반 안정적 식별. expert-testing, expert-backend 양측 합의.
- **Consequences**: Windows 전용. CI/CD에서 별도 windows-latest runner 필요.
- **Retrospective Questions**:
  - FlaUI의 WPF 컨트롤 탐색이 안정적이었는가?
  - 특정 컨트롤(FlowDocument, TreeView)에서 FlaUI 한계가 있었는가?

### DEC-009: 로깅 프레임워크 선택

- **Date**: 2026-03-11
- **Status**: ACCEPTED
- **Context**: GUI 애플리케이션의 구조화 로깅 도입
- **Options**:
  - A) Serilog: .NET 생태계 표준, 다양한 Sink, 구조화 로깅
  - B) NLog: 유사 기능, XML 기반 설정
  - C) Microsoft.Extensions.Logging만 사용: 추가 의존성 없음, Sink 제한적
- **Decision**: **Option A** (Serilog)
- **Rationale**: InMemoryLogSink 커스텀 구현 용이. E2E 테스트에서 로그 기반 assertion 핵심. 풍부한 Enricher 생태계.
- **Consequences**: Serilog NuGet 4종 추가. 기존 Debug.WriteLine 전체 교체 필요.
- **Retrospective Questions**:
  - Serilog가 E2E 테스트에서 기대한 만큼 유용했는가?
  - InMemoryLogSink의 10K 바운드가 충분했는가?
  - 로그 기반 assertion이 UI assertion을 얼마나 보완했는가?

### DEC-010: E2E 테스트 병렬 실행 전략

- **Date**: 2026-03-11
- **Status**: ACCEPTED
- **Context**: 여러 E2E 테스트가 동일 GUI 프로세스에 접근할 때 경합 방지
- **Options**:
  - A) 직렬 실행 (maxParallelThreads=1): 안전, 느림
  - B) 병렬 실행 + 프로세스 격리 (테스트별 별도 프로세스): 빠름, 리소스 과다
  - C) 병렬 실행 + 잠금: 복잡, 동기화 버그 위험
- **Decision**: **Option A** (직렬 실행)
- **Rationale**: GUI E2E 특성상 윈도우 포커스, 마우스/키보드 입력이 프로세스 단위로 공유됨. 직렬이 가장 안정적.
- **Consequences**: E2E 테스트 실행 시간 증가. 15건 기준 약 2-3분 예상.
- **Retrospective Questions**:
  - 직렬 실행 시간이 허용 범위였는가?
  - 향후 테스트 증가 시 프로세스 격리 전환이 필요한가?

### DEC-011: Flaky 테스트 관리 전략

- **Date**: 2026-03-11
- **Status**: ACCEPTED
- **Context**: UI 자동화 테스트의 환경 의존적 불안정성 관리
- **Options**:
  - A) RetryFactAttribute (최대 2회): 간단, 근본 원인 마스킹 위험
  - B) WaitHelper만으로 해결: 원칙적, 모든 비동기 동작 예측 어려움
  - C) Quarantine (격리): 복잡, 관리 오버헤드
- **Decision**: **Option A** (RetryFact, 최대 2회) + **Option B** (WaitHelper 병행)
- **Rationale**: WaitHelper로 1차 방어, RetryFact로 2차 방어. 과도한 retry 방지(최대 2회).
- **Consequences**: 3회 연속 실패만 진짜 실패로 간주. Flaky 원인 분석 로그 유지.
- **Retrospective Questions**:
  - RetryFact가 실제 버그를 마스킹한 사례가 있었는가?
  - WaitHelper의 timeout 값이 적절했는가?

---

## Architectural Decisions (Cross-Phase)

### DEC-012: Wave 기반 일괄 실행 전략

- **Date**: 2026-03-11
- **Status**: ACCEPTED
- **Context**: 5개 Phase를 효율적으로 일괄 실행하면서도 품질을 보장하는 방법
- **Options**:
  - A) Phase별 순차 실행: 안전, 느림, 컨텍스트 전환 과다
  - B) 전체 한번에 구현 후 검증: 빠름, 오류 누적 위험
  - C) Wave 묶음 (의존성 기반 그룹) + 교차검증: 균형
- **Decision**: **Option C** (3-Wave 구조)
- **Rationale**: Wave 1(Foundation)이 Wave 2/3의 전제조건. 각 Wave 후 교차검증으로 오류 조기 발견. Debug/Fix 사이클로 품질 보장.
- **Consequences**: 3번의 검증 관문. Wave 간 의존성 명확. 총 실행 시간은 순차보다 짧고 일괄보다 안전.
- **Retrospective Questions**:
  - Wave 그룹핑이 적절했는가?
  - 교차검증이 실제로 오류를 조기 발견했는가?
  - Debug/Fix 반복 횟수가 적절했는가?

### DEC-013: 교차검증 에이전트 조합

- **Date**: 2026-03-11
- **Status**: ACCEPTED
- **Context**: 각 Wave 후 검증을 어떤 관점에서 수행할지
- **Decision**: 2중 교차검증 (Quality + Domain Expert)
  - 검증 A: manager-quality (TRUST 5, 커버리지, 코딩 표준)
  - 검증 B: 도메인 전문가 (expert-frontend for WPF / expert-testing for E2E / expert-backend for Logging)
- **Rationale**: Quality 관점과 도메인 관점의 이중 검증으로 누락 방지.
- **Retrospective Questions**:
  - 두 검증이 서로 다른 이슈를 발견했는가?
  - 검증 A와 B의 지적사항이 중복되는 경우가 많았는가?

---

## Retrospective Template

구현 완료 후, 각 결정에 대해 다음을 평가합니다:

### Per-Decision Review

| DEC-ID | 결정 | 결과 평가 | 교훈 | 상태 변경 |
|--------|------|----------|------|----------|
| DEC-001 | FlowDocument+Markdig | (구현 후 작성) | (구현 후 작성) | ACCEPTED → ? |
| ... | ... | ... | ... | ... |

### Overall Retrospective Questions

1. **전체 아키텍처**: Wave 구조가 프로젝트 규모에 적합했는가?
2. **기술 선택**: 선택한 라이브러리(Markdig, Serilog, FlaUI)가 기대를 충족했는가?
3. **품질 보증**: 교차검증 + Debug/Fix 사이클이 최종 품질에 기여했는가?
4. **생산성**: 일괄 실행 계획이 개별 실행 대비 효율적이었는가?
5. **예상 외 이슈**: 계획에 없던 문제가 발생했는가? 어떻게 해결했는가?
6. **재사용성**: 이 SPEC의 패턴 중 다른 SPEC에 재사용 가능한 것은?

### Lessons Learned (구현 후 작성)

(구현 완료 후 이 섹션에 교훈을 기록합니다)

---
