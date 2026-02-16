# Quality Management

품질 관리 프로세스, 기준, 검증 방법을 기록합니다.

## TRUST 5 Framework

### T - Tested (테스트됨)
**기준**:
- RTL: Line ≥95%, Branch ≥90%, FSM 100%
- SW: 모듈당 80-90%
- 전체 목표: 85%+

**검증 방법**:
```bash
# RTL 커버리지
# Vivado에서 coverage 보고서 생성
# 또는 ModelSim/Questa coverage

# SW 커버리지 (.NET)
dotnet test --collect:"XPlat Code Coverage"

# 커버리지 보고서 확인
reportgenerator -reports:coverage.xml -targetdir:htmlcov
```

**예외 처리**:
- 커버리지 제외 필요시 justification 문서화
- 최대 5% 예외 허용 (quality.yaml 설정)

### R - Readable (가독성)
**기준**:
- 명확한 변수명 (snake_case for RTL/C, PascalCase for C#)
- 영어 주석 (code_comments: en)
- 복잡한 로직에 설명 주석 필수

**검증 방법**:
```bash
# RTL Lint
# Vivado 또는 Verilator lint

# C# Lint
dotnet format --verify-no-changes
dotnet build /warnaserror

# C/C++ Lint (펌웨어)
cppcheck --enable=all src/
```

**명명 규칙**:
- FPGA RTL: `panel_scan_fsm`, `line_buffer_ctrl`
- C#: `PanelSimulator`, `FpgaSimulator`
- 펌웨어: `spi_driver`, `csi2_rx_handler`

### U - Unified (통일성)
**기준**:
- 일관된 코드 스타일
- .NET: EditorConfig, Ruff/Black 스타일
- RTL: 일관된 indent, signal naming

**검증 방법**:
```bash
# C# 포맷 검사
dotnet format

# RTL 스타일 검사
# 프로젝트 스타일 가이드 참조
```

### S - Secured (보안)
**기준**:
- OWASP Top 10 준수
- Input validation at boundaries
- No hardcoded secrets

**검증 방법**:
```bash
# 정적 분석
# SAST tools 사용

# Secrets 스캔
git secrets --scan

# 의존성 취약점 검사
dotnet list package --vulnerable
```

**주의사항**:
- .env 파일 절대 커밋 금지
- API key, token 등 secrets는 환경 변수 사용
- 의료 기기용 추가 보안 요구사항 준비

### T - Trackable (추적성)
**기준**:
- 요구사항 → 설계 → 테스트 추적성
- 명확한 커밋 메시지
- Issue tracking (Redmine)

**검증 방법**:
```bash
# 커밋 메시지 검사
git log --oneline

# 추적성 매트릭스
# 요구사항 ID → SPEC → 테스트 케이스 매핑
```

## Test Pyramid

### Layer 1: Static Analysis (지속적)
**도구**:
- RTL: Vivado Lint, Verilator
- C#: dotnet build warnings, Roslyn analyzers
- C/C++: cppcheck, clang-tidy

**실행 시점**: 모든 빌드

### Layer 2: Unit Testing (M2)
**범위**:
- FPGA: FV-01~FV-11 (개별 모듈)
- SW: 각 시뮬레이터 모듈

**실행 시점**: 모든 커밋

**테스트 작성 규칙**:
```csharp
// TDD: RED-GREEN-REFACTOR
[Fact]
public void PanelSimulator_GeneratesFrame_WithCorrectDimensions()
{
    // Arrange
    var simulator = new PanelSimulator(rows: 1024, cols: 1024);

    // Act
    var frame = simulator.CaptureFrame();

    // Assert
    Assert.Equal(1024, frame.Rows);
    Assert.Equal(1024, frame.Cols);
}
```

### Layer 3: Integration Testing (M3)
**범위**:
- IT-01~IT-10 시나리오
- SW-only full-chain 검증

**실행 시점**: Sprint 종료시, PR 전

### Layer 4: System V&V (M6)
**범위**:
- 실제 패널 통합
- 성능 KPI 검증
- 시뮬레이터 보정

**실행 시점**: 마일스톤 M6

## CI/CD Pipeline

### Git Push Trigger
```
Gitea Push
  ↓
n8n Webhook
  ↓
[RTL Pipeline]
  - RTL Lint
  - Simulation (FV-01~FV-11)
  - Coverage Report
  ↓
[SW Pipeline]
  - Build
  - Unit Test
  - Coverage
  - Integration Test
  ↓
[Config Pipeline]
  - Schema Validation
  - Conversion Check
  ↓
Redmine Ticket Update
Dashboard Update
```

### Build Status Badge
- RTL Build: [![RTL Build](badge-url)]()
- SW Build: [![SW Build](badge-url)]()
- Coverage: [![Coverage](badge-url)]()

## Quality Gates

### Pre-Commit Gate
```bash
# 로컬에서 실행
./scripts/pre-commit-checks.sh

# 체크 항목:
# 1. Lint 경고 없음
# 2. 단위 테스트 통과
# 3. Secrets 스캔 통과
```

### PR Merge Gate
- [ ] 모든 테스트 통과
- [ ] 커버리지 목표 달성
- [ ] 코드 리뷰 승인 (1명 이상)
- [ ] CI/CD 파이프라인 통과
- [ ] TRUST 5 체크리스트 완료

### Phase Milestone Gate
**M0 (W1)**: P0 결정 확정
**M0.5 (W6)**: CSI-2 PoC ≥70% 처리량
**M2 (W9)**: 모든 시뮬레이터 단위 테스트 통과
**M3 (W14)**: IT-01~IT-06 통합 시나리오 통과
**M4 (W18)**: HIL 패턴 A/B 통과
**M6 (W28)**: 실제 패널 프레임 획득, 시뮬레이터 보정 완료

## Performance KPI Monitoring

### 측정 항목
| 메트릭 | 목표 | 측정 시점 |
|--------|------|-----------|
| 프레임 드롭률 | <0.01% | M3, M4, M6 |
| 데이터 무결성 | 비트 정확도 (0 오류) | M3, M4, M6 |
| CSI-2 처리량 | ≥1 GB/s | M4 |
| End-to-end 지연 | ≤3 x frame_time | M6 |
| FPGA LUT 사용률 | <60% | M4 |

### 측정 방법
```bash
# 프레임 드롭률
./tools/measure-frame-drop.sh --duration 10m

# 데이터 무결성
./tools/verify-data-integrity.sh --frames 100

# CSI-2 처리량
./tools/measure-csi2-throughput.sh
```

## Process KPI Tracking

### CI Build Success Rate
**목표**: ≥95%
**측정**: Weekly
**대시보드**: Redmine CI 플러그인

### Code Review Completion
**목표**: 100% (모든 PR)
**측정**: Per PR
**추적**: Gitea PR status

### Critical Issue Resolution
**목표**: ≤5 business days
**측정**: Weekly
**추적**: Redmine issue tracking

### Milestone Adherence
**목표**: ≥80% (±1 week)
**측정**: Per milestone
**추적**: Gantt chart

## Documentation Standards

### 필수 문서화 항목
- [ ] 모든 public API에 XML 주석 (C#) 또는 Doxygen 주석 (C/C++)
- [ ] RTL 모듈 헤더에 기능 설명, I/O 신호 설명
- [ ] 복잡한 알고리즘에 설명 주석
- [ ] README.md 업데이트 (프로젝트 구조 변경시)

### 문서 생성 자동화
```bash
# C# API 문서
docfx build docfx.json

# RTL 문서
# Doxygen 또는 SphinxHDL

# 통합 문서 사이트
# Nextra or MkDocs
```

---

*Last Updated: 2026-02-16*
