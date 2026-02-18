# GUI Test Tool 교차 검증 보고서

**SPEC**: SPEC-GUITOOLS-001 (GUI Test Automation Tool Requirements)
**검증 일자**: 2026-02-18
**검증 방법**: 실증적 코드베이스 분석 + SPEC 검토

---

## 실행 요약

| 항목 | 결과 | 상태 |
|------|------|------|
| 실현 가능성 | **조건부 가능** | ⚠️ 주의 |
| SPEC 완결성 | **불완전** | ❌ 수정 필요 |
| 기술 타당성 | **기술적으로 가능** | ✅ |
| 선행 조건 | **미충족** | ❌ BLOCKER |

---

## 1. 실제 코드베이스 상태 (팩트)

### 1.1 타겟 앱 구현 상태

| 애플리케이션 | 경로 | 상태 | .NET 버전 |
|-------------|------|------|-----------|
| GUI.Application | tools/GUI.Application/ | ✅ 구현됨 | net8.0-windows |
| ParameterExtractor.Wpf | tools/ParameterExtractor/ | ✅ 구현됨 | net8.0-windows |

### 1.2 UI Automation 준비 상태

| 항목 | SPEC 가정 | 실제 상태 | 영향 |
|------|-----------|-----------|------|
| AutomationId | 모든 컨트롤에 존재 | ❌ **없음** | 🔴 **CRITICAL** |
| Name 속성 | 대체 수단으로 사용 가능 | ⚠️ 제한적 (중복 가능) | 🟡 중간 |
| XPath | 최후 수단 | ⚠️ 취약 (구조 변경에 민감) | 🟡 중간 |

**실제 XAML 확인**:
- `GUI.Application/Views/MainWindow.xaml`: AutomationId 없음
- `ParameterExtractor.Wpf/Views/MainWindow.xaml`: AutomationId 없음
- 모든 컨트롤이 Name 속성 없이 정의됨

### 1.3 로깅 시스템 상태

| 항목 | SPEC 가정 | 실제 상태 | 영향 |
|------|-----------|-----------|------|
| Serilog 사용 | 타겟 앱에 Serilog 통합 | ❌ **없음** | 🔴 **CRITICAL** |
| 로그 파일 | logs/gui_<date>.log 형식 | ❌ **없음** | 🔴 **CRITICAL** |
| 로그 포맷 | 구조화된 Serilog 포맷 | Debug.WriteLine만 사용 | 🔴 **CRITICAL** |

**실제 코드 확인**:
- `GUI.Application/App.xaml.cs`: `System.Diagnostics.Debug.WriteLine()`만 사용
- `ParameterExtractor.Wpf/App.xaml.cs`: `MessageBox.Show()` + `Debug.WriteLine()`
- Serilog NuGet 패키지 없음

### 1.4 CI/CD 인프라 상태

| 항목 | SPEC 가정 | 실제 상태 | 영향 |
|------|-----------|-----------|------|
| GitHub Actions | .github/workflows/ 존재 | ❌ **없음** | 🟡 설정 필요 |
| GUI Test Workflow | gui-test.yml 정의 | ❌ **없음** | 🟡 작성 필요 |

---

## 2. SPEC 검토 결과

### 2.1 REQ-GUITOOLS-060 위배 (심각)

**요구사항**: "테스트 도구는 타겟 애플리케이션 코드를 수정하면 안 됨"

**현실**:
1. UI Automation을 위해서는 **AutomationId 추가가 필수적**
2. LogVerifier를 위해서는 **Serilog 통합이 필수적**
3. 이것은 REQ-GUITOOLS-060과 **직접 모순**

**결론**: REQ-GUITOOLS-060은 현실적으로 불가능한 요구사항

### 2.2 Performance Constraint 검토

| 제약조건 | SPEC 값 | 평가 |
|----------|---------|------|
| Test startup time | < 3 seconds | ✅ 현실적 |
| Element find timeout | < 10 seconds (x2 margin) | ✅ 합리적 |
| Log pattern match | < 20 seconds (x2 margin) | ✅ 합리적 |
| Total scenario execution | < 120 seconds | ⚠️ 앱 동작 시간 반영 |

**평가**: 성능 제약조건은 실용적이고 현실적임

### 2.3 Risk Mitigation 검토

| 위험 | 완화 전략 | 충분성 |
|------|-----------|--------|
| R-GUITOOLS-001: 타이밍 변동 | x2 margin, exponential backoff | ✅ 충분 |
| R-GUITOOLS-002: CI 환경 제약 | windows-latest 사용 | ✅ 합리적 |
| R-GUITOOLS-003: 로그 파일 충돌 | 고유 로그 파일 | ✅ 충분 |

**평가**: 위험 완화 전략은 적절함

---

## 3. 기술적 타당성 분석

### 3.1 FlaUI.UIA3 선택

| 항목 | 검토 결과 |
|------|-----------|
| WPF 지원 | ✅ UIA3는 WPF에 최적화됨 |
| Active Development | ✅ FlaUI 4.0+ 활발히 개발 중 |
| AutomationId 지원 | ✅ 자동 지원 (단, 타겟 앱에 추가 필요) |
| Name/XPath Fallback | ✅ 지원하나 취약함 |

**결론**: FlaUI.UIA3는 올바른 선택이나, 타겟 앱 수정 필요

### 3.2 분리된 프로세스 아키텍처

| 장점 | 단점 |
|------|------|
| ✅ 테스트와 앱 완전 분리 | ⚠️ AutomationId 필수 |
| ✅ 타겟 앱 테스트 종속성 없음 | ⚠️ 타겟 앱 로깅 필요 |
| ✅ 독립적 개발 주기 | ⚠️ LogVerifier 동작 불가 (현재) |

**결론**: 아키텍처는 타당하나, 선행 조건 미충족

### 3.3 GitHub Actions windows-latest

| 검토 항목 | 결과 |
|-----------|------|
| GUI Automation 지원 | ✅ Desktop API 지원 |
| Screenshot 캡처 | ✅ 가능 |
| Artifact 업로드 | ✅ 지원됨 |
| 병렬 실행 | ✅ Matrix strategy 지원 |

**결론**: CI/CD 접근법은 실현 가능

---

## 4. BLOCKER 항목

### 🔴 BLOCKER-1: AutomationId 부재

**영향**: UI Automation이 극도로 불안정해짐
- Name 속성만 사용: 중복 가능성 높음
- XPath만 사용: UI 구조 변경에 취약

**해결 옵션**:
1. ~~타겟 앱 수정~~ (REQ-GUITOOLS-060 위배)
2. SPEC 수정: AutomationId를 타겟 앱의 필수 요구사항으로 명시
3. Name 기반 자동화 수용 (안정성 희생)

### 🔴 BLOCKER-2: Serilog 미통합

**영향**: LogVerifier 전체가 동작 불가
- REQ-GUITOOLS-040~043 (LogVerifier) 구현 불가
- AC-GUITOOLS-003, 004 검증 불가

**해결 옵션**:
1. ~~타겟 앱에 Serilog 추가~~ (REQ-GUITOOLS-060 위배)
2. SPEC 수정: LogVerifier를 Optional에서 제외
3. 대체 검증 방법 (stdout 캡처 등)

### 🟡 BLOCKER-3: CI/CD 미설정

**영향**: 자동화된 테스트 실행 불가
- 수동 테스트만 가능
- CI 통합 계획 (REQ-GUITOOLS-050~053) 불가

**해결**: GitHub Actions workflow 작성 (구현 가능)

---

## 5. SPEC 수정 권고사항

### 5.1 REQ-GUITOOLS-060 수정 (필수)

**현재**: "테스트 도구는 타겟 애플리케이션 코드를 수정하면 안 됨"

**권고**: 다음과 같이 분리하여 명시
```
REQ-GUITOOLS-060-A: 테스트 도구는 타겟 애플리케이션의 비즈니스 로직을 수정하면 안 됨
REQ-GUITOOLS-060-B: 테스트 가능성을 위해 타겟 애플리케이션은 최소한의 UI Automation 준비를 해야 함:
  - 모든 대화형 컨트롤에 AutomationId 추가 (또는 고유 Name)
  - 구조화된 로깅 (Serilog 또는 표준 출력)
```

### 5.2 LogVerifier 수정 (필수)

**옵션 A**: Serilog를 타겟 앱의 필수 의존성으로 명시
- SPEC-TOOLS-001에 Serilog 요구사항 추가
- detector_config.yaml에 로깅 설정 추가

**옵션 B**: LogVerifier를 Optional에서 완전 제외
- LogVerifier 관련 REQ 삭제 (REQ-GUITOOLS-040~043)
- UI 상태 검증으로 대체

### 5.3 Acceptance Criteria 수정

**AC-GUITOOLS-003**: 로그 패턴 검증 → 제외 또는 수정
**AC-GUITOOLS-004**: 실패 시 스크린샷 → 유지 (가능)

---

## 6. 구현 로드맵 (수정 후)

### Phase 1: 타겟 앱 준비 (선행 조건)

| 작업 | 예상 시간 | 의존성 |
|------|-----------|--------|
| GUI.Application에 AutomationId 추가 | 2시간 | XAML 파일 수정 |
| ParameterExtractor.Wpf에 AutomationId 추가 | 1시간 | XAML 파일 수정 |
| Serilog 통합 (선택 시) | 3시간 | 두 앱 모두 |
| CI/CD 초기 설정 | 1시간 | 없음 |

### Phase 2: GuiTestRunner 개발

| 작업 | 예상 시간 | 의존성 | Phase 1 완료 |
|------|-----------|--------|---------------|
| FlaUI Wrapper 구현 | 4시간 | FlaUI 패키지 | ✅ 필요 |
| TestScenario JSON 파서 | 3시간 | 없음 | ❌ 없음 |
| LogVerifier (선택) | 5시간 | Serilog 통합 | ✅ 필요 |
| CLI 인터페이스 | 2시간 | 없음 | ❌ 없음 |

### Phase 3: CI/CD 통합

| 작업 | 예상 시간 | 의존성 |
|------|-----------|--------|
| GitHub Actions workflow | 3시간 | Phase 2 완료 |
| Artifact 업로드 | 1시간 | workflow 작성 |

**총 예상 시간**: 24시간 (Serilog 포함) / 16시간 (Serilog 제외)

---

## 7. 최종 권고사항

### 7.1 즉시 조치 (SPEC 수정)

1. **REQ-GUITOOLS-060을 수정하여 타겟 앱의 준비 의무 명시**
2. **LogVerifier를 Optional에서 제외 또는 Serilog를 필수로 명시**
3. **AC-GUITOOLS-003 (로그 검증)을 수정 또는 삭제**

### 7.2 타겟 앱 수정 (구현 전 선행)

1. **모든 대화형 컨트롤에 AutomationId 추가**
   - Button: AutomationId="{Binding}Button"
   - TextBox: AutomationId="{Binding}TextBox"
   - MenuItem: AutomationId="{Binding}MenuItem"

2. **Serilog 통합 (LogVerifier 유지 시)**
   ```csharp
   // App.xaml.cs
   Log.Logger = new LoggerConfiguration()
       .WriteTo.File("logs/gui_.log", rollingInterval: RollingInterval.Day)
       .CreateLogger();
   ```

### 7.3 순차적 접근

1. **1단계**: UI Automation만으로 최소 기능 구현
   - FlaUI Wrapper + TestScenario 파서
   - Click, Type, Verify 스텝만

2. **2단계**: LogVerifier 추가 (선택)
   - Serilog 통합 후
   - LogCheck 스텝 구현

3. **3단계**: CI/CD 통합
   - GitHub Actions workflow
   - 자동화된 regression testing

---

## 8. 결론

### 요약

| 평가 항목 | 결과 |
|-----------|------|
| 기술적 실현 가능성 | ✅ 가능 (FlaUI, GitHub Actions 모두 사용 가능) |
| SPEC 완결성 | ❌ 불완전 (선행 조건 미명시, 모순 있음) |
| 현재 상태로 구현 | ❌ 불가 (BLOCKER 3건 존재) |
| 수정 후 구현 | ✅ 가능 (SPEC과 타겟 앱 수정 필요) |

### 최종 판단

**SPEC-GUITOOLS-001은 기술적으로 타당하나, 현재 상태로는 구현 불가능합니다.**

**필수 수정 사항**:
1. REQ-GUITOOLS-060 수정 (타겟 앱 준비 의무 명시)
2. LogVerifier 관련 요구사항 재검토
3. 타겟 앱에 AutomationId 및 Serilog 추가

**수정 완료 후**: 구현 가능하며, 예상 개발 시간은 16-24시간입니다.

---

**보고서 작성**: 2026-02-18
**검증자**: MoAI Cross-Validation Team
**승인 상태**: ✅ CROSS-VALIDATION COMPLETE

---

## 부록 A: 에이전트 검증 결과

### Research Agent (team-researcher)
- **결과**: CRITICAL GAPS 확인
- **주요 발견**:
  - AutomationId: GUI.Application, ParameterExtractor.Wpf 모두 없음
  - Serilog: 두 앱 모두 미사용 (Debug.WriteLine만)
  - 로그 파일: 출력 없음
- **권고**: Option C (하이브리드) - Serilog 추가 + name/xpath 타겟팅

### Analyst Agent (team-analyst)
- **결과**: MOSTLY REALISTIC (2 critical gaps)
- **주요 발견**:
  - 요구사항 완결성: 양호
  - 성능 제약조건: x2 margin 적절
  - 위험 완화: CI 디스플레이 확인 필요
- **권고**: Prerequisites 섹션 추가, CI 환경 명시

### Architect Agent (team-architect)
- **결과**: SOUND (with conditions)
- **주요 발견**:
  - FlaUI.UIA3: 올바른 선택 ✅
  - Windows-only: 적절한 제약 ✅
  - CI/CD: github actions windows-latest 확인 ✅
- **권고**: Serilog 의존성 추가, artifact size mitigiation

---

## 부록 B: 3-에이전트 합의 사항

**합의된 BLOCKER**:
1. AutomationId 부재 (3/3 에이전트 확인)
2. Serilog 미통합 (3/3 에이전트 확인)
3. CI 디스플레이 요구사항 불확실 (2/3 에이전트 확인)

**합의된 권고**:
1. SPEC 수정: REQ-GUITOOLS-060 재검토
2. 타겟 앱에 Serilog 추가 (또는 LogVerifier 제외)
3. Prerequisites 섹션 추가

**기술적 타당성**: 만장일치 확신 (3/3 에이전트 동의)
