# 프로젝트 문서 전면 갱신 계획

## Context

`.moai/project/` 문서들(product.md, structure.md, tech.md)은 2026-02-17에 "사전 구현 기준선 (M0 준비, Week 1)"으로 생성되었으나, 현재 프로젝트는 SW 구현 100% 완료(M2-Impl 마일스톤) 상태에 도달했다. 문서에는 "구현 예정"으로 표시된 내용들이 실제로는 모두 구현 완료된 상태이며, 새로운 구성요소(DicomEncoder, FPGA RTL, Yocto 메타레이어, 생성된 설정 파일 등)가 추가되어 있다.

사용자 요청: `moai project "현재 프로젝트 재점검해서 계획 업데이트 해줘" --team worktree`

---

## 문서 브랜딩 규칙

> **[HARD]** 생성되는 모든 문서에서 "claude", "moai", "moai-adk" 표기를 **금지**한다.
> 대신 **"abyz-lab"** 으로 표기한다. (product.md, structure.md, tech.md, README 등 전체 적용)

---

## 현재 상태 vs. 문서 상태 Gap

| 항목 | 문서 상태 (2026-02-17) | 실제 현재 상태 |
|------|----------------------|--------------|
| 전체 진행률 | M0 준비 (Week 1) | M2-Impl 완료 (SW 100%) |
| sdk/ | 미구현 (계획) | 구현 완료 (242/242 테스트 통과) |
| tools/ParameterExtractor | 미구현 (계획) | WPF 완료 (41/41 테스트) |
| tools/GUI.Application | 미구현 (계획) | WPF 완료 (40/40 테스트) |
| tools/CodeGenerator | 미구현 (계획) | 구현 완료 (9/9 테스트) |
| tools/ConfigConverter | 미구현 (계획) | 부분 완료 (37/42 테스트) |
| tools/IntegrationRunner | 미구현 (계획) | CLI 빌드 완료 |
| fw/ | 미구현 (계획) | meta-detector Yocto 레이어 추가, 알파 개발 중 |
| fpga/ | 미구현 (계획) | RTL 파일 존재 (untracked) |
| DicomEncoder | 없음 | 신규 추가 (DicomEncoder.cs + Tests) |
| config/ | 계획 중 | detector_config.yaml/json/dts/xdc 생성 완료 |
| generated/ | 없음 | 자동 생성 코드 존재 |

---

## 실행 계획

### Phase 1: Git Worktree 생성
- `EnterWorktree` 도구를 사용하여 격리된 작업 환경 생성
- 브랜치명: `docs/project-refresh-2026-02-27`
- 목적: 메인 브랜치를 보호하면서 문서 업데이트 작업

### Phase 2: 팀 모드 병렬 탐색 (--team 플래그)
- `TeamCreate`: `moai-project-refresh` 팀 생성
- 3개 에이전트 병렬 실행:
  - **team-researcher**: 전체 코드베이스 현황 상세 분석 (SDK, Tools, FW, FPGA)
  - **team-analyst**: 각 컴포넌트 구현 완료 상태 및 테스트 커버리지 분석
  - **team-architect**: 아키텍처 변화 및 새로운 구성요소 분석
- 탐색 완료 후 팀 종료

### Phase 3: 문서 갱신 (manager-docs 서브에이전트)
팀 탐색 결과를 바탕으로 3개 문서 전면 갱신:

**product.md 업데이트 내용**:
- Status: "Pre-implementation Baseline" → "M2-Impl 완료 (SW 100%)"
- Current Phase: "M0 준비" → "M3-Integ 준비 (M2-Impl 완료)"
- 구현 완료된 기능 목록 추가 (실제 구현 기반)
- DICOM 인코딩 지원 추가 (DicomEncoder 신규)
- 마일스톤 진행 상황 업데이트

**structure.md 업데이트 내용**:
- Status: "Planned Structure (Not Yet Implemented)" → "실제 구현된 구조"
- 실제 디렉토리 트리 반영 (fpga/, generated/, fw/meta-detector/, config/ 실제 파일)
- 신규 구성요소 추가: DicomEncoder, ConfigConverter, CodeGenerator, IntegrationRunner
- 테스트 파일 구조 업데이트 (~55 테스트 파일)
- 생성된 파일 (generated/) 설명 추가

**tech.md 업데이트 내용**:
- 실제 사용 중인 기술 스택 반영
- xUnit 2.9.0, Moq 4.20.70, FluentAssertions, coverlet 버전 업데이트
- DICOM 인코딩 관련 기술 추가
- Yocto Scarthgap 5.0 LTS 메타레이어 구체화
- 27개 .csproj 프로젝트 의존성 업데이트

### Phase 4: 개발 방법론 자동 설정 (Phase 3.7)
- 현재 테스트 파일 비율: ~55 테스트파일 / 전체 소스 → 충분한 커버리지
- development_mode 확인: 현재 `hybrid` → 유지 (변경 불필요)

### Phase 5: LSP 서버 확인 (Phase 3.5)
- C# 프로젝트 → OmniSharp 또는 csharp-ls 설치 여부 확인

### Phase 6: 완료 보고
- 갱신된 파일 목록 제시
- 다음 단계 옵션: SPEC-INTEG-001 작성 (M3 통합 테스트 단계) 또는 검토 후 머지

---

## 수정 대상 파일

| 파일 | 작업 | 에이전트 |
|------|------|---------|
| `.moai/project/product.md` | 전면 재작성 | manager-docs |
| `.moai/project/structure.md` | 전면 재작성 | manager-docs |
| `.moai/project/tech.md` | 전면 재작성 | manager-docs |
| `.moai/config/sections/quality.yaml` | development_mode 확인/유지 | MoAI 자동 |

---

## 검증 방법

1. 갱신된 product.md에서 "Pre-implementation" 표현이 없는지 확인
2. structure.md가 실제 `ls` 결과와 일치하는지 확인
3. tech.md에 DicomEncoder, xUnit 2.9.0, Yocto 5.0 반영 확인
4. 현재 구현 완료 상태(SW 100%)가 모든 문서에 정확히 반영되어 있는지 확인
5. Worktree 브랜치에서 변경 사항 검토 후 main에 머지

---

## 실행 모드

- `--team`: TeamCreate 사용하여 3개 에이전트 병렬 탐색
- `worktree`: EnterWorktree로 격리된 작업 환경 사용
- 팀 완료 후: TeamDelete로 정리, worktree에서 작업 완료 후 브랜치 머지

---

## 참고 파일

- `.moai/project/product.md` (현재 문서, 2026-02-17)
- `.moai/project/structure.md` (현재 문서, 2026-02-17)
- `.moai/project/tech.md` (현재 문서, 2026-02-17)
- `README.md` (최신 구현 현황 포함)
- `fw/ARCHITECTURE.md` (710줄, 펌웨어 아키텍처)
- `fw/README.md` (Yocto 빌드 가이드)
- `.moai/specs/SPEC-*/spec.md` (7개 완료된 SPEC)
