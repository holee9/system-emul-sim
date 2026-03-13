# Phase 2 Plan: X-ray Detector Panel System - Emulator Golden Reference

## Context

1차 구현(M0~M3)이 완료된 X-ray Detector Panel System의 2차 계획입니다.

**핵심 발견**: SPEC-EMUL-001(v3 Brainstormed) 5개 Phase 중 **Phase 1-3은 100% 구현 완료**, Phase 4-5는 **60-70% 구현 완료** 상태입니다. 따라서 2차 계획은 **잔여 갭 해소 + 품질 강화 + 검증 완성**에 집중합니다.

### 1차 완료 현황

| 영역 | 상태 | 근거 |
|------|------|------|
| MCU 에뮬레이터 (Phase 1) | 100% 완료 | SequenceEngine, FrameBufferManager, HealthMonitor, CommandProtocol, McuTopSimulator 전체 구현+테스트 |
| FPGA 강화 (Phase 2) | 100% 완료 | ProtectionLogic, Csi2BackpressureModel, ClockDomain, CdcSynchronizer 구현+테스트 |
| Panel 물리 모델 (Phase 3) | 100% 완료 | ScintillatorModel, CompositeNoise, GainOffsetMap, LagModel, RoicReadoutModel 등 10개 파일 구현+테스트 |
| 파이프라인 실체화 (Phase 4) | 70% 완료 | IntegrationTests/Helpers에 실제 파이프라인 존재, 하지만 **IntegrationRunner.Core/SimulatorPipeline.cs는 여전히 스텁** |
| CLI 독립 실행 (Phase 5) | 60% 완료 | 모든 모듈 CLI 존재(System.CommandLine 기반), 하지만 **모듈 간 라운드트립 미검증** |

### 핵심 갭 분석

| # | 갭 | 위치 | 영향 |
|---|-----|------|------|
| G1 | Production Pipeline 스텁 미제거 | `IntegrationRunner.Core/SimulatorPipeline.cs` | ProcessFrame()이 MCU/Host 건너뜀, InjectError/SetPacketLossRate 빈 메서드 |
| G2 | 파이프라인 이중화 | Test Helpers vs Core 별도 구현 | 동일 로직 2벌 유지보수, 불일치 위험 |
| G3 | 168 시나리오 검증 미완 | `.moai/specs/SPEC-EMUL-001/scenarios.md` | HW 검증 Golden Reference 자격 미확인 |
| G4 | CLI 라운드트립 미검증 | `*Cli/Program.cs` 5개 | 모듈 간 데이터 호환성 미확인 |
| G5 | ConfigConverter 테스트 실패 | 5개 테스트 | 88% → 100% 필요 |
| G6 | CI/CD 파이프라인 부재 | 없음 | 자동화 테스트/커버리지 리포트 미구축 |
| G7 | Firmware 19 TODO 미해결 | `fw/src/*.c` | Golden Reference로서의 완전성 부족 |

---

## 2차 계획: 3-SPEC 분할 전략

SPEC-EMUL-001의 잔여 갭을 3개의 집중된 SPEC으로 분할합니다.

### SPEC-EMUL-002: Pipeline Consolidation (우선순위 1)

**목표**: Production 파이프라인 실체화 - 스텁 제거, Test 파이프라인을 Core로 통합

**범위**:
- `IntegrationRunner.Core/SimulatorPipeline.cs` 리팩터링: 실제 4계층 데이터 플로우로 교체
- `NetworkChannel`, `NetworkChannelConfig`, `PipelineCheckpoint`를 `IntegrationRunner.Core/`로 이동
- 5개 스텁 메서드(`InjectError`, `SetPacketLossRate`, `SetPacketReorderRate`, `SetScanMode`, `ProcessFrame`) 실제 구현
- `IntegrationTests/Helpers/SimulatorPipeline.cs`를 Core 파이프라인 위임으로 리팩터링
- ConfigConverter 5개 실패 테스트 수정

**수정 파일** (8개):
- `tools/IntegrationRunner/src/IntegrationRunner.Core/SimulatorPipeline.cs` (주 대상)
- `tools/IntegrationRunner/src/IntegrationRunner.Core/Network/NetworkChannel.cs` (신규 위치)
- `tools/IntegrationRunner/src/IntegrationRunner.Core/Network/NetworkChannelConfig.cs` (신규 위치)
- `tools/IntegrationRunner/src/IntegrationRunner.Core/PipelineCheckpoint.cs` (신규 위치)
- `tools/IntegrationTests/Helpers/SimulatorPipeline.cs` (Core 위임으로 변경)
- `tools/IntegrationTests/Helpers/SimulatorPipelineBuilder.cs` (어댑터)
- `tools/ConfigConverter/src/ConfigConverter/` (테스트 수정)
- `IntegrationRunner.Core.csproj` (프로젝트 참조 추가)

**방법론**: DDD (ANALYZE-PRESERVE-IMPROVE) - 모든 기존 코드 수정
**예상 규모**: 1회 `/moai run` 세션 (180K 토큰 예산 내)
**위험도**: 낮음 - 테스트 인프라 완비 상태

**검증**:
```bash
cd tools/IntegrationTests && dotnet test --verbosity normal  # IT-01~IT-17 전체 통과
cd tools/ConfigConverter && dotnet test  # 42/42 통과
```

---

### SPEC-EMUL-003: Scenario Verification & CLI Hardening (우선순위 2)

**목표**: 168 시나리오 검증 매핑 + CLI 라운드트립 검증 + 시나리오 커버리지 50%+

**범위**:
1. **시나리오 커버리지 매핑**: 168개 시나리오 vs 기존 테스트 매핑 테이블 생성
2. **CLI 라운드트립 검증**: `panel-sim → fpga-sim → mcu-sim → host-sim` 데이터 체인 검증
3. **미커버 시나리오 테스트 추가**: 우선순위별로 50개+ 시나리오 자동 테스트화
4. **CLI 기능 완성**: Shell pipe 모드(stdin/stdout), `--benchmark`, `--inject-error` 플래그

**신규/수정 파일** (~15개):
- CLI Program.cs 5개 (기능 보완)
- 시나리오 테스트 파일 5~8개 (신규)
- `scenarios-coverage-matrix.md` (신규 문서)

**방법론**: Hybrid (TDD for 신규 테스트, DDD for CLI 수정)
**예상 규모**: 1~2회 `/moai run` 세션
**위험도**: 중간 - 시나리오 범위가 넓어 우선순위 선별 필요

**검증**:
```bash
# CLI 라운드트립
dotnet run --project tools/PanelSimulator/src/PanelSimulator.Cli -- --rows 256 --cols 256 -o frame.raw
dotnet run --project tools/FpgaSimulator/src/FpgaSimulator.Cli -- --input frame.raw -o packets.csi2
dotnet run --project tools/McuSimulator/src/McuSimulator.Cli -- --input packets.csi2 -o frames.udp
dotnet run --project tools/HostSimulator/src/HostSimulator.Cli -- --input frames.udp -o result.tiff
# result.tiff와 frame.raw의 픽셀 데이터 일치 확인
```

---

### SPEC-EMUL-004: Golden Reference Hardening (우선순위 3)

**목표**: HW 검증용 Golden Reference v1.0 인증 - CI/CD, 문서화, 품질 강화

**범위**:
1. **CI/CD 파이프라인**: GitHub Actions - 빌드, 테스트, 커버리지 리포트
2. **Firmware TODO 해결**: 19개 TODO 항목 구현 (SPI 통합, V4L2, UDP 전송)
3. **HW 검증 가이드**: G-01~G-15 시나리오 문서화
4. **API 문서**: 공개 인터페이스 레퍼런스
5. **커버리지 대시보드**: 모듈별 85%+ 확인 자동화

**신규/수정 파일** (~17개):
- `.github/workflows/ci.yml` (신규)
- `fw/src/*.c` 6~8개 (TODO 구현)
- `docs/hw-verification-guide.md` (신규)
- `docs/api-reference.md` (신규)
- 커버리지 설정 파일들

**방법론**: DDD for 펌웨어 수정, TDD for CI/CD 인프라
**예상 규모**: 2~3회 `/moai run` 세션
**위험도**: 중~상 - 펌웨어 TODO는 도메인 전문성 필요

**검증**:
```bash
dotnet test --collect:"XPlat Code Coverage"  # 전체 커버리지 85%+
# GitHub Actions PR 생성 시 자동 실행 확인
```

---

## 실행 순서 및 의존성

```
SPEC-EMUL-002 (Pipeline Consolidation)     ← 최우선, 독립 실행 가능
        │
        ▼
SPEC-EMUL-003 (Scenario Verification)      ← 002 완료 후 (통합 파이프라인 필요)
        │
        ▼
SPEC-EMUL-004 (Golden Reference)           ← 003과 부분 병렬 가능
```

**병렬 가능 영역**:
- EMUL-004의 CI/CD 구축은 002와 독립적으로 진행 가능
- EMUL-004의 문서 작업은 003 진행 중에 시작 가능
- EMUL-004의 펌웨어 TODO는 002/003과 완전 독립

---

## 마일스톤 산출물

| 마일스톤 | SPEC | 산출물 | 사용자가 얻는 것 |
|----------|------|--------|-----------------|
| M4a | EMUL-002 | 통합 파이프라인 | 단일 진입점으로 4계층 실제 시뮬레이션, 에러 주입, 네트워크 장애 테스트 |
| M4b | EMUL-003 | 검증된 CLI + 시나리오 매트릭스 | 모듈별 독립 실행, 라운드트립 검증, 168개 시나리오 중 50%+ 자동화 |
| M4c | EMUL-004 | Golden Reference v1.0 | CI/CD 자동화, HW 검증 가이드, API 문서, 85%+ 커버리지 인증 |

---

## 핵심 참조 파일

### 수정 대상 (Critical Path)
- `tools/IntegrationRunner/src/IntegrationRunner.Core/SimulatorPipeline.cs` - 프로덕션 파이프라인 스텁 제거
- `tools/IntegrationTests/Helpers/SimulatorPipeline.cs` - 실제 4계층 구현 (참조 원본)
- `tools/IntegrationTests/Helpers/NetworkChannel.cs` - 네트워크 장애 주입 (Core로 이동)
- `tools/IntegrationTests/Helpers/SimulatorPipelineBuilder.cs` - 빌더 어댑터

### 재사용 (Existing)
- `tools/McuSimulator/src/McuSimulator.Core/McuTopSimulator.cs` - MCU 통합 오케스트레이터
- `tools/Common/src/Common.Cli/CliFramework.cs` - CLI 공통 프레임워크
- `tools/Common/src/Common.Dto/Serialization/FrameDataSerializer.cs` - 데이터 직렬화
- `.moai/specs/SPEC-EMUL-001/scenarios.md` - 168개 검증 시나리오 정의

### 참조 (Read-only)
- `fw/include/sequence_engine.h` - 펌웨어 FSM 원본
- `fpga/rtl/protection_logic.sv` - 보호 로직 RTL 원본
- `.moai/specs/SPEC-INTEG-001/completion-report.md` - M3 완료 보고서

---

## 예상 소요

| SPEC | 신규 파일 | 수정 파일 | 신규 테스트 | Run 세션 |
|------|-----------|-----------|------------|---------|
| EMUL-002 | 3 (이동) | 5 | 2 | 1 |
| EMUL-003 | 8 | 7 | 6 | 1-2 |
| EMUL-004 | 5 | 8 | 3 | 2-3 |
| **합계** | **16** | **20** | **11** | **4-6** |
