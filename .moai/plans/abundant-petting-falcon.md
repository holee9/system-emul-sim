# Phase 1 문서 교차검증 - 병렬 팀 리뷰 계획

## Context

이전 분석에서 31개 문서(SPEC 7개 + docs 28개)가 모두 "Approved" 상태임을 확인.
이제 전문팀별 병렬 교차검증 → 총괄 아키텍트 최종 승인을 수행한다.

**목표**: 각 전문팀이 해당 도메인 문서의 내용적 정합성·기술적 정확성을 독립 검증.
최종 결과는 총괄 아키텍트(Chief Architect) 에이전트가 통합 승인.

---

## 병렬 dispatch 계획 (5개 팀 동시 실행)

### 팀 1 — FPGA 리뷰팀
**검토 대상:**
- `.moai/specs/SPEC-FPGA-001/spec.md`
- `docs/architecture/fpga-design.md`
- `docs/api/csi2-packet-format.md`
- `docs/api/spi-register-map.md`

**검증 포인트:**
- Artix-7 XC7A35T LUT/BRAM 리소스 제약 준수 여부
- D-PHY 400/800 Mbps 대역폭 계산 정확성
- CSI-2 패킷 포맷과 FPGA 설계서 간 정합성
- SPI 레지스터 맵과 FW SPEC 간 인터페이스 일치

### 팀 2 — FW 리뷰팀
**검토 대상:**
- `.moai/specs/SPEC-FW-001/spec.md`
- `docs/architecture/soc-firmware-design.md`
- `docs/api/spi-register-map.md`

**검증 포인트:**
- i.MX8M Plus (Yocto Scarthgap) 환경 적합성
- V4L2 CSI-2 RX 드라이버 요구사항 완결성
- SPI 드라이버와 레지스터 맵 일치
- BQ40z50 Battery / BMI160 IMU 드라이버 포팅 계획 포함 여부

### 팀 3 — SDK 리뷰팀
**검토 대상:**
- `.moai/specs/SPEC-SDK-001/spec.md`
- `docs/architecture/host-sdk-design.md`
- `docs/api/host-sdk-api.md`
- `docs/api/ethernet-protocol.md`

**검증 포인트:**
- DetectorClient API 완결성 및 일관성
- 10 GbE 프로토콜 설계 정확성
- C#/.NET 8.0 구현 실현 가능성
- Frame 처리 파이프라인 설계 정합성

### 팀 4 — SIM/Test 리뷰팀
**검토 대상:**
- `.moai/specs/SPEC-SIM-001/spec.md`
- `.moai/specs/SPEC-POC-001/spec.md`
- `.moai/specs/SPEC-TOOLS-001/spec.md`
- `docs/testing/unit-test-plan.md`
- `docs/testing/integration-test-plan.md`
- `docs/testing/verification-strategy.md`

**검증 포인트:**
- FpgaSimulator가 FPGA SPEC 요구사항 모두 커버하는지
- IT-01~IT-10 통합 테스트와 SPEC 수락 조건 일치
- PoC 성공 조건이 실현 가능한지 (400M vs 800M)
- Tools SPEC 요구사항 완결성

### 팀 5 — ARCH 교차검증팀
**검토 대상:**
- `.moai/specs/SPEC-ARCH-001/spec.md`
- `docs/architecture/system-architecture.md`
- 모든 SPEC 요약 (인터페이스 경계 중심)

**검증 포인트:**
- 전체 아키텍처 일관성 (FPGA↔FW↔SDK 인터페이스 체인)
- 성능 계층 정의 일관성 (최소/중간-A/중간-B/목표)
- SPEC 간 인터페이스 계약 일치 여부
- 누락된 요구사항 또는 모순 식별

---

## 최종 단계 — 총괄 아키텍트 승인

5개 팀 결과를 통합하여:
- 이슈 없음 → "Phase 1 교차검증 통과" 확인
- 이슈 발견 → 이슈 목록 + 심각도 분류 + 수정 권고안

---

## 실행 순서

1. **Phase A**: 5개 팀 병렬 dispatch (단일 메시지 동시 실행)
2. **Phase B**: 각 팀 결과 수집 후 총괄 아키텍트 에이전트 실행
3. **Phase C**: 최종 보고서 사용자에게 제시 → 세션 완료

---

## 수정 정책 (--auto)

- 이슈 발견 시 자동 문서 수정 없음 (문서는 이미 Approved 상태)
- 이슈는 목록화하여 보고만 함
- 총괄 아키텍트가 "승인" 또는 "수정 필요" 결론 도출
