# AI Agent Orchestration Guide

AI agent 간 리소스 분배를 통한 개발 속도 향상 운영 지침입니다.

## 개요

### 목적
- **병렬 처리**: 독립적인 작업을 여러 AI agent에 동시 분배
- **리소스 최적화**: 각 agent의 강점에 따른 작업 할당
- **속도 향상**: 순차 처리 → 병렬 처리로 전환하여 개발 시간 단축

### Agent 생태계

```
MoAI (Claude Code)          Codex (OpenAI ChatGPT)
    ↓                              ↓
Strategic Orchestrator       Specialized Executor
- 아키텍처 결정              - 수학 계산
- SPEC 작성                  - 코드 생성 (TS/JS/React)
- 품질 검증                  - 컴포넌트 스캐폴딩
- 워크플로우 조율            - 알고리즘 구현
```

## 리소스 분배 전략

### MoAI 담당 영역 (Primary)

1. **프로젝트 오케스트레이션**
   - SPEC workflow (plan, run, sync)
   - Git 관리 (commit, branch, PR)
   - 품질 게이트 검증 (TRUST 5)
   - 문서 생성 (.md, API docs)

2. **도메인 전문 작업**
   - FPGA RTL (SystemVerilog)
   - C# .NET 시뮬레이터
   - 펌웨어 (C/C++)
   - 설정 파일 (YAML, JSON)

3. **아키텍처 의사결정**
   - 시스템 설계
   - 인터페이스 정의
   - 성능 최적화
   - 위험 관리

### Codex 담당 영역 (Delegation)

1. **계산 집약적 작업**
   - 수학 공식 계산
   - 대역폭/리소스 시뮬레이션
   - 성능 벤치마크 분석

2. **JavaScript/TypeScript 생태계**
   - React/Next.js 컴포넌트
   - Node.js 유틸리티
   - 프론트엔드 UI 코드
   - npm 패키지 통합

3. **빠른 프로토타이핑**
   - 간단한 알고리즘 검증
   - 데이터 변환 스크립트
   - 테스트 데이터 생성

4. **독립적인 모듈**
   - 외부 의존성 없는 함수
   - 유틸리티 헬퍼
   - 샘플 코드 생성

## Codex MCP 통합 설정

### 전제 조건
- OpenAI ChatGPT VSCode extension (`openai.chatgpt`) 설치
- ChatGPT 계정 로그인 (Plus, Pro, Business, Edu, Enterprise)

### 설정 파일

**`.mcp.json` (글로벌)**
```json
{
  "mcpServers": {
    "codex": {
      "command": "C:\\Users\\user\\.vscode\\extensions\\openai.chatgpt-0.4.74-win32-x64\\bin\\windows-x86_64\\codex.exe",
      "args": ["mcp-server"]
    }
  },
  "staggeredStartup": {
    "enabled": true,
    "delayMs": 500,
    "connectionTimeout": 60000
  }
}
```

**`.claude/settings.json` (프로젝트)**
```json
{
  "permissions": {
    "allow": [
      "mcp__codex__codex",
      "mcp__codex__codex-reply"
    ]
  }
}
```

### 검증
```bash
"C:\Users\user\.vscode\extensions\openai.chatgpt-0.4.74-win32-x64\bin\windows-x86_64\codex.exe" login status
# Expected: "Logged in using ChatGPT"
```

## 사용 시나리오

### 시나리오 1: 대역폭 계산 위임

**상황**: CSI-2 인터페이스 대역폭 계산 필요

**Before (MoAI only)**:
- MoAI가 직접 계산 수행
- 단일 스레드 순차 처리
- 시간: ~30초

**After (MoAI + Codex)**:
```
MoAI: 대역폭 계산 공식 정의 및 제약사항 제공
  ↓
Codex: mcp__codex__codex({
  prompt: "Calculate CSI-2 bandwidth: 4 lanes, 1.25 Gbps/lane,
           2048x2048 pixels, 16-bit, 30fps.
           Include overhead (20%) and margin analysis."
})
  ↓ (병렬 처리)
MoAI: 동시에 FPGA 리소스 제약사항 검증
  ↓
통합: 결과 병합 및 의사결정
```
- 시간: ~15초 (50% 단축)

### 시나리오 2: 테스트 데이터 생성

**상황**: 시뮬레이터 테스트용 샘플 프레임 생성

**Before**:
- MoAI가 C# 코드 작성
- 순차 검증 및 수정

**After**:
```
MoAI: 요구사항 정의 (해상도, 비트 깊이, 노이즈 모델)
Codex: TypeScript 프레임 생성기 구현
MoAI: C# 시뮬레이터에 통합 및 검증
```
- 병렬 처리로 30% 속도 향상

### 시나리오 3: 다중 컴포넌트 개발

**상황**: Host SDK GUI 개발 (React)

**Before**:
- MoAI가 순차적으로 컴포넌트 작성

**After**:
```
MoAI: 컴포넌트 아키텍처 설계 및 API 정의
  ↓ (병렬 분배)
Codex Thread 1: FrameViewer 컴포넌트
Codex Thread 2: ControlPanel 컴포넌트
Codex Thread 3: StatusMonitor 컴포넌트
  ↓
MoAI: 통합, 상태 관리, 테스트
```
- 3개 컴포넌트 병렬 개발: 60% 시간 단축

## 통합 워크플로우

### 기본 패턴

```
1. 작업 분석 (MoAI)
   - 독립성 확인
   - 복잡도 평가
   - Codex 적합성 판단

2. 작업 분배
   - MoAI: 핵심 아키텍처, FPGA/펌웨어, 품질
   - Codex: 계산, JS/TS, 프로토타입

3. 병렬 실행
   - MoAI와 Codex 동시 작업
   - 독립적인 컨텍스트 유지

4. 결과 통합 (MoAI)
   - 코드 병합
   - 품질 검증
   - Git 커밋
```

### 의사결정 트리

```
작업 시작
  ↓
JavaScript/TypeScript? → YES → Codex 위임 고려
  ↓ NO
순수 계산/알고리즘? → YES → Codex 위임 고려
  ↓ NO
FPGA/펌웨어/C#? → YES → MoAI 직접 수행
  ↓ NO
복잡한 아키텍처? → YES → MoAI 직접 수행
  ↓ NO
프로토타입/유틸? → YES → Codex 위임 고려
```

## 성능 최적화 가이드

### 병렬화 가능 작업 식별

**✅ 병렬화 적합**:
- 독립적인 모듈 개발
- 다중 컴포넌트 생성
- 계산 + 구현 동시 진행
- 테스트 데이터 + 테스트 코드

**❌ 병렬화 부적합**:
- 순차 의존성 있는 작업
- 상태 공유가 필요한 작업
- 단일 파일 편집
- Git 충돌 가능성 높은 작업

### 컨텍스트 관리

**MoAI 컨텍스트**:
- 프로젝트 전체 아키텍처
- MEMORY.md 참조
- Git 히스토리
- TRUST 5 기준

**Codex 컨텍스트**:
- 명확한 입력 사양
- 독립적인 실행 환경
- 결과만 반환
- 컨텍스트 분리

### 통신 오버헤드 최소화

```
Bad: Codex에 여러 번 질문
  MoAI → Codex: "컴포넌트 구조는?"
  MoAI → Codex: "Props 타입은?"
  MoAI → Codex: "이벤트 핸들러는?"

Good: 한 번에 명확한 사양 전달
  MoAI → Codex: "React 컴포넌트 생성:
    - Props: { data, onUpdate }
    - UI: 테이블 + 버튼
    - 타입: TypeScript 엄격 모드"
```

## 활용 예시 (프로젝트 적용)

### 1. CSI-2 대역폭 검증

**MoAI 역할**:
- FPGA D-PHY 제약사항 파악
- detector_config.yaml 읽기
- 최종 의사결정

**Codex 위임**:
```javascript
mcp__codex__codex({
  prompt: `
Calculate CSI-2 MIPI D-PHY bandwidth for X-ray detector:
- Lanes: 4
- Lane speed: 1.0-1.25 Gbps (Artix-7 OSERDES limit)
- Pixel format: RAW16 (16-bit)
- Resolution tiers:
  * Minimum: 1024x1024 @ 15fps
  * Target: 2048x2048 @ 30fps
  * Maximum: 3072x3072 @ 30fps
- Overhead: 20% (D-PHY protocol)

Output:
1. Effective bandwidth per tier (Gbps)
2. Feasibility analysis (lane speed vs required bandwidth)
3. Margin calculation
  `
})
```

### 2. 파라미터 추출기 리팩토링

**MoAI 역할**:
- C# 아키텍처 설계
- YAML 파싱 로직
- 테스트 및 통합

**Codex 위임**:
```javascript
mcp__codex__codex({
  prompt: `
TypeScript로 YAML 파라미터 추출 유틸리티 함수 작성:
- Input: detector_config.yaml 경로
- Output: { resolution, bitDepth, fps, lanes, laneSpeed }
- Error handling: 파일 없음, 파싱 실패
- Dependencies: js-yaml 사용
  `
})
```

### 3. 프레임 시뮬레이터 UI

**MoAI 역할**:
- 전체 UI 아키텍처
- PanelSimulator C# 통합
- 상태 관리

**Codex 위임 (병렬)**:
```javascript
// Thread 1: 프레임 뷰어
mcp__codex__codex({ prompt: "React FrameViewer component..." })

// Thread 2: 설정 패널
mcp__codex__codex({ prompt: "React ConfigPanel component..." })

// Thread 3: 통계 대시보드
mcp__codex__codex({ prompt: "React StatsDashboard component..." })
```

## 트러블슈팅

### Codex 연결 실패

**증상**: MCP tools not loading

**해결**:
1. `.mcp.json`의 `connectionTimeout` 확인 (60000ms)
2. Codex 로그인 상태 확인
3. Claude Code 재시작

### 컨텍스트 불일치

**증상**: Codex가 프로젝트 제약을 무시

**해결**:
- Codex에 명시적 제약사항 전달
- MoAI가 결과 검증 및 수정
- 중요 결정은 MoAI가 담당

### 병렬 처리 충돌

**증상**: Git merge conflict, 중복 코드

**해결**:
- 파일 소유권 명확히 분리
- MoAI가 통합 전 검토
- 병렬화 적합성 재평가

## 모니터링 및 개선

### 성능 지표

| 메트릭 | 측정 방법 | 목표 |
|--------|----------|------|
| 작업 완료 시간 | Before/After 비교 | 30% 단축 |
| 병렬화 비율 | Codex 위임 작업 수 / 전체 | 20%+ |
| 통합 오버헤드 | 병합 시간 / 총 시간 | <10% |
| 코드 품질 | TRUST 5 통과율 | 100% |

### 지속적 개선

1. **위임 패턴 학습**
   - 성공한 시나리오 문서화
   - 실패 사례 분석
   - MEMORY.md 업데이트

2. **프롬프트 최적화**
   - Codex 응답 품질 향상
   - 재작업 비율 감소
   - 명확한 사양 템플릿 구축

3. **워크플로우 개선**
   - 병목 지점 식별
   - 병렬화 확대
   - 자동화 도입

## 베스트 프랙티스

### DO ✅
- 독립적인 작업은 Codex 위임
- MoAI가 최종 통합 및 검증
- 명확한 입력 사양 제공
- 결과를 TRUST 5로 검증

### DON'T ❌
- 아키텍처 결정을 Codex에 위임
- FPGA/펌웨어 코드를 Codex에 위임
- Git 커밋을 Codex가 직접 수행
- 품질 검증을 생략

## 참조

### 관련 문서
- `MEMORY.md` - 즉시 실행 정책
- `.mcp.json` - Codex MCP 설정
- `CHEATSHEET.md` - 일상 명령어
- `docs/references/quality-management.md` - TRUST 5 기준

### 외부 리소스
- [Claude Code MCP Documentation](https://github.com/anthropics/claude-code)
- [OpenAI Codex Extension](https://marketplace.visualstudio.com/items?itemName=openai.chatgpt)

---

*Last Updated: 2026-02-16*
*Version: 1.0.0*
