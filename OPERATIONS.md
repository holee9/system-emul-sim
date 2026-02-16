# 운영 계획 (Operations Plan)

X-ray Detector Panel System 프로젝트의 일상 운영, 협업 프로세스, 품질 관리를 위한 실행 계획입니다.

## 1. 개발 환경 설정

### 1.1 필수 도구 설치

#### FPGA 개발
```bash
# AMD Vivado 설치 (Artix-7 지원)
# https://www.xilinx.com/support/download.html
# 버전: 2023.2 이상

# Vivado 라이선스 설정
export XILINXD_LICENSE_FILE=/path/to/license.lic
```

#### .NET 개발
```bash
# .NET 8.0 SDK 설치
winget install Microsoft.DotNet.SDK.8

# 설치 확인
dotnet --version
```

#### 버전 관리
```bash
# Git 설정
git config --global user.name "Your Name"
git config --global user.email "your.email@example.com"

# Gitea 원격 저장소 설정
git remote add origin https://gitea.example.com/xray-detector/system-emul-sim.git
```

### 1.2 프로젝트 클론 및 빌드

```bash
# 프로젝트 클론
git clone https://gitea.example.com/xray-detector/system-emul-sim.git
cd system-emul-sim

# 서브모듈 초기화 (있는 경우)
git submodule update --init --recursive

# .NET 프로젝트 빌드
cd tools
dotnet restore
dotnet build

# 테스트 실행
dotnet test
```

## 2. 일상 개발 워크플로우

### 2.1 작업 시작 루틴

```bash
# 1. 최신 변경사항 동기화
git pull origin main

# 2. 브랜치 생성 (feature/fix/docs)
git checkout -b feature/panel-simulator-noise-model

# 3. 이슈 확인 (Redmine)
# https://redmine.example.com/projects/xray-detector

# 4. 작업 시작
```

### 2.2 개발 중 루틴

#### TDD 사이클 (신규 코드)
```csharp
// 1. RED: 실패하는 테스트 작성
[Fact]
public void PanelSimulator_AppliesGaussianNoise_WithCorrectStdDev()
{
    // Arrange
    var simulator = new PanelSimulator(noiseModel: NoiseModel.Gaussian, stdDev: 5.0);

    // Act
    var frame = simulator.CaptureFrame();
    var actualStdDev = CalculateStdDev(frame);

    // Assert
    Assert.InRange(actualStdDev, 4.5, 5.5);
}

// 2. GREEN: 최소 구현
public class PanelSimulator
{
    public Frame CaptureFrame()
    {
        // 최소한의 구현으로 테스트 통과
        return ApplyGaussianNoise(baseFrame, stdDev);
    }
}

// 3. REFACTOR: 코드 개선
// - 중복 제거
// - 명명 개선
// - 구조 최적화
```

#### DDD 사이클 (기존 코드/RTL)
```systemverilog
// 1. ANALYZE: 기존 동작 이해
// - RTL 코드 읽기
// - Waveform 분석
// - 의존성 파악

// 2. PRESERVE: 특성화 테스트 작성
task test_line_buffer_pingpong();
    // 현재 동작을 테스트로 캡처
    write_line_to_buffer(bank_a, test_data);
    assert(bank_ready_a == 1'b1);
    read_line_from_buffer(bank_b, read_data);
    assert(read_data == test_data);
endtask

// 3. IMPROVE: 점진적 개선
// - 작은 변경
// - 테스트 실행
// - 통과 확인
```

### 2.3 커밋 전 체크리스트

```bash
# 1. 모든 테스트 통과 확인
dotnet test                    # .NET
vivado -mode batch -source run_tests.tcl  # FPGA

# 2. 코드 스타일 검사
dotnet format --verify-no-changes

# 3. Lint 검사
# RTL: Vivado lint 또는 Verilator

# 4. 커버리지 확인
dotnet test --collect:"XPlat Code Coverage"

# 5. Secrets 스캔
git secrets --scan

# 6. 커밋
git add .
git commit -m "feat(panel-sim): Add Gaussian noise model

픽셀 데이터에 Gaussian 노이즈를 추가하는 기능을 구현했습니다.
- 표준 편차 설정 가능
- 단위 테스트 추가 (커버리지 92%)
- 기존 NoiseModel enum 확장

🗿 MoAI <email@mo.ai.kr>"
```

### 2.4 Pull Request 프로세스

```bash
# 1. 원격 브랜치로 푸시
git push origin feature/panel-simulator-noise-model

# 2. Gitea에서 PR 생성
# - 제목: 간결하고 명확하게
# - 설명: 변경사항, 테스트 결과, 스크린샷 (필요시)
# - 리뷰어 지정

# 3. CI/CD 파이프라인 통과 대기

# 4. 코드 리뷰 대응
# - 리뷰 의견에 답변
# - 필요한 변경사항 수정
# - 재푸시

# 5. 승인 후 머지
# - Squash merge 또는 Merge commit (팀 규칙 따름)

# 6. 브랜치 정리
git checkout main
git pull
git branch -d feature/panel-simulator-noise-model
```

## 3. 협업 프로세스

### 3.1 Daily Standup (15분)

**시간**: 매일 오전 9:30
**참석자**: 전체 팀원

**질문 3가지**:
1. 어제 완료한 작업
2. 오늘 할 작업
3. 장애물 (Blocker)

**기록**: Redmine 댓글 또는 Slack

### 3.2 Sprint Planning (2주 단위)

**시간**: Sprint 시작일 오전 10:00
**기간**: 2시간
**참석자**: 전체 팀원

**안건**:
1. 지난 Sprint 회고
2. 이번 Sprint 목표 설정
3. 백로그에서 작업 선택
4. 작업 분배 및 추정

### 3.3 Sprint Review & Retrospective

**시간**: Sprint 종료일 오후 2:00
**기간**: 2시간
**참석자**: 전체 팀원 + Stakeholder (선택)

**Review (1시간)**:
- 완료된 작업 시연
- Stakeholder 피드백
- Acceptance criteria 확인

**Retrospective (1시간)**:
- 잘된 점 (Keep)
- 개선할 점 (Improve)
- 시도할 것 (Try)
- 액션 아이템 정의

### 3.4 코드 리뷰 규칙

#### 리뷰어 책임
- 24시간 내 초기 피드백
- 건설적인 의견
- TRUST 5 기준 확인

#### 작성자 책임
- 리뷰 가능한 크기 유지 (<500 LOC)
- 명확한 PR 설명
- 리뷰 의견에 48시간 내 대응

#### 리뷰 체크리스트
- [ ] 테스트 통과
- [ ] 커버리지 목표 달성
- [ ] 명명 규칙 준수
- [ ] 문서화 완료
- [ ] TRUST 5 기준 만족

## 4. 품질 관리

### 4.1 자동화된 품질 게이트

#### Pre-Commit Hook
```bash
# .git/hooks/pre-commit (자동 실행)
#!/bin/bash

echo "Running pre-commit checks..."

# Lint 검사
dotnet format --verify-no-changes
if [ $? -ne 0 ]; then
    echo "ERROR: Code formatting issues detected"
    exit 1
fi

# 단위 테스트
dotnet test
if [ $? -ne 0 ]; then
    echo "ERROR: Tests failed"
    exit 1
fi

# Secrets 스캔
git secrets --scan
if [ $? -ne 0 ]; then
    echo "ERROR: Potential secrets detected"
    exit 1
fi

echo "Pre-commit checks passed!"
```

#### CI/CD Pipeline (n8n + Gitea)
```
Git Push
  ↓
Gitea Webhook
  ↓
n8n Workflow
  ↓
┌─────────────────────────────────────┐
│ RTL Pipeline                        │
│ - Lint                              │
│ - Simulation (FV-01~FV-11)          │
│ - Coverage Report                   │
└─────────────────────────────────────┘
  ↓
┌─────────────────────────────────────┐
│ SW Pipeline                         │
│ - Build                             │
│ - Unit Test                         │
│ - Coverage                          │
│ - Integration Test (IT-01~IT-06)   │
└─────────────────────────────────────┘
  ↓
Redmine Ticket Update
Dashboard Notification
```

### 4.2 수동 품질 검토

#### 주간 품질 리뷰 (매주 금요일)
- KPI 대시보드 확인
- 커버리지 트렌드 분석
- 기술 부채 식별
- 다음 주 우선순위 결정

#### 마일스톤 품질 게이트
- M2: 모든 시뮬레이터 단위 테스트 통과
- M3: IT-01~IT-06 통합 테스트 통과
- M4: HIL 테스트 통과
- M6: 실제 패널 통합 및 보정 완료

## 5. 문서화 프로세스

### 5.1 문서 작성 시점

| 문서 유형 | 작성 시점 | 위치 |
|----------|-----------|------|
| 아키텍처 문서 | 설계 단계 (Phase 1) | `docs/architecture/` |
| API 문서 | 구현 완료 후 | `docs/api/` |
| 사용자 가이드 | Phase 5-6 | `docs/user-guide/` |
| SPEC 문서 | Plan Phase | `.moai/specs/SPEC-XXX/` |
| 릴리스 노트 | 각 릴리스 전 | `CHANGELOG.md` |

### 5.2 문서 업데이트 규칙

- 코드 변경시 관련 문서 즉시 업데이트
- PR에 문서 변경 포함
- README.md는 항상 최신 상태 유지

### 5.3 문서 생성 자동화

```bash
# API 문서 생성
cd docs
docfx build docfx.json

# 문서 사이트 배포
docfx serve _site
```

## 6. 위험 관리 프로세스

### 6.1 위험 식별

**방법**:
- Daily standup에서 Blocker 공유
- Sprint retrospective에서 문제점 분석
- 마일스톤 리뷰에서 전체 위험 재평가

**기록**:
- `memory/risk-management.md` 업데이트
- Redmine 위험 이슈 생성

### 6.2 위험 대응

**Level 1: 팀 내 해결** (24시간)
- 팀 리드 판단
- 즉시 대응

**Level 2: PM 개입** (48시간)
- 일정/리소스 조정
- 우선순위 변경

**Level 3: Stakeholder 결정** (1주)
- 아키텍처 변경
- 성능 목표 조정
- 예산 영향

### 6.3 위험 모니터링

**Weekly Risk Review**:
- 활성 위험 상태 확인
- 완화 조치 진행도 확인
- 새로운 위험 식별

**Milestone Risk Gate**:
- 전체 위험 재평가
- GO/NO-GO 결정
- 다음 Phase 리스크 계획

## 7. 릴리스 프로세스

### 7.1 버전 관리 전략

**Semantic Versioning**: MAJOR.MINOR.PATCH
- MAJOR: 호환성 깨지는 변경
- MINOR: 기능 추가 (호환성 유지)
- PATCH: 버그 수정

**브랜치 전략**:
```
main        - 안정 버전
develop     - 개발 통합
feature/*   - 기능 개발
hotfix/*    - 긴급 수정
release/*   - 릴리스 준비
```

### 7.2 릴리스 체크리스트

- [ ] 모든 테스트 통과 (단위 + 통합 + HIL)
- [ ] 커버리지 목표 달성 (≥85%)
- [ ] TRUST 5 기준 만족
- [ ] CHANGELOG.md 업데이트
- [ ] 릴리스 노트 작성
- [ ] 태그 생성 (v1.0.0)
- [ ] 빌드 아티팩트 생성
- [ ] 배포 문서 준비

### 7.3 릴리스 커맨드

```bash
# 1. 릴리스 브랜치 생성
git checkout -b release/v1.0.0 develop

# 2. 버전 번호 업데이트
# - AssemblyInfo.cs
# - package.json
# - README.md

# 3. CHANGELOG 업데이트
# CHANGELOG.md에 릴리스 노트 추가

# 4. 최종 테스트
dotnet test
vivado -mode batch -source run_all_tests.tcl

# 5. 머지 및 태그
git checkout main
git merge --no-ff release/v1.0.0
git tag -a v1.0.0 -m "Release version 1.0.0"
git push origin main --tags

# 6. develop 브랜치에도 머지
git checkout develop
git merge --no-ff release/v1.0.0
git push origin develop

# 7. 릴리스 브랜치 삭제
git branch -d release/v1.0.0
```

## 8. 모니터링 및 대시보드

### 8.1 KPI 대시보드

**위치**: Redmine Dashboard Plugin

**표시 항목**:
- CI Build Success Rate (목표: ≥95%)
- Code Coverage (목표: ≥85%)
- Open Issues Count
- Critical Issue Resolution Time (목표: ≤5일)
- Sprint Velocity
- FPGA Resource Utilization (목표: <60%)

### 8.2 알림 설정

**Slack/Teams 통합**:
- CI/CD 파이프라인 실패
- Critical Issue 생성
- PR 리뷰 요청
- 마일스톤 달성

## 9. 온보딩 프로세스

### 9.1 신규 팀원 체크리스트

**Day 1**:
- [ ] 개발 환경 설정
- [ ] Git 저장소 접근 권한
- [ ] Redmine 계정 생성
- [ ] Slack/Teams 초대
- [ ] README.md 및 OPERATIONS.md 읽기

**Week 1**:
- [ ] 코드베이스 둘러보기
- [ ] 첫 번째 작은 이슈 해결
- [ ] 코드 리뷰 참여 (리뷰어로)
- [ ] Daily standup 참여

**Month 1**:
- [ ] 주요 기능 구현 완료
- [ ] 문서화 기여
- [ ] Sprint planning 참여
- [ ] 팀 프로세스 이해

### 9.2 온보딩 버디 시스템

신규 팀원마다 경험 많은 팀원 1명 배정:
- 질문 답변
- 코드 리뷰 멘토링
- 프로세스 안내

## 10. 비상 대응 계획

### 10.1 Critical Bug 대응

**정의**: 프로덕션 중단, 데이터 손실, 보안 취약점

**프로세스**:
```
1. 감지 및 보고 (즉시)
   - Slack #critical-alerts 채널 알림
   - PM 및 Tech Lead 즉시 통지

2. 평가 (1시간 내)
   - 영향 범위 파악
   - 우선순위 결정 (P0/P1/P2)

3. 긴급 수정 (P0: 4시간, P1: 24시간)
   - Hotfix 브랜치 생성
   - 최소한의 수정
   - 빠른 테스트

4. 배포 (가능한 빠르게)
   - 긴급 릴리스
   - 모니터링 강화

5. 사후 분석 (1주 내)
   - Root cause 분석
   - 재발 방지책 수립
   - 프로세스 개선
```

### 10.2 백업 및 복구

**코드 백업**: Git 원격 저장소 (자동)
**문서 백업**: Git + 주간 아카이브
**빌드 아티팩트**: 릴리스별 보관 (1년)

---

## 참고 문서

- [MEMORY.md](C:\Users\user\.claude\projects\D--workspace-github-system-emul-sim\memory\MEMORY.md) - 프로젝트 핵심 정보
- [Development Workflow](C:\Users\user\.claude\projects\D--workspace-github-system-emul-sim\memory\development-workflow.md) - 개발 워크플로우 상세
- [Quality Management](C:\Users\user\.claude\projects\D--workspace-github-system-emul-sim\memory\quality-management.md) - 품질 관리 프로세스
- [FPGA Patterns](C:\Users\user\.claude\projects\D--workspace-github-system-emul-sim\memory\fpga-patterns.md) - FPGA 개발 패턴
- [Risk Management](C:\Users\user\.claude\projects\D--workspace-github-system-emul-sim\memory\risk-management.md) - 위험 관리 프로토콜

---

*Last Updated: 2026-02-16*
*Version: 1.0.0*
