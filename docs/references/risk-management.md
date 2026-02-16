# Risk Management

프로젝트 위험 요소, 완화 전략, 대응 프로토콜을 기록합니다.

## 위험 등록부

### R-03: FPGA 리소스 부족
**확률**: 낮음
**영향**: 높음
**상태**: 모니터링 중

**증상**:
- Synthesis 실패 (LUT 사용률 >60%)
- Timing closure 실패
- 기능 추가 불가

**완화 방안**:
```
1. 예방적 리소스 모니터링
   - 매 빌드시 리소스 사용률 추적
   - 50% 도달시 경고
   - 55% 도달시 최적화 시작

2. 최적화 전략
   - DSP48E1 활용 (곱셈/누산)
   - BRAM 활용 (대용량 메모리)
   - Logic optimization (retiming, resource sharing)

3. 업그레이드 경로 (Pin-Compatible)
   - XC7A35T → XC7A50T (+57% LUTs)
   - XC7A50T → XC7A75T (+127% LUTs)
   - XC7A75T → XC7A100T (+205% LUTs)
```

**대응 프로토콜**:
```bash
# 1. 리소스 사용률 확인
vivado -mode batch -source report_utilization.tcl

# 2. Critical path 분석
vivado -mode batch -source report_timing.tcl

# 3. 최적화 우선순위
#    a) Remove unused features
#    b) Optimize combinational logic
#    c) Share resources
#    d) Upgrade FPGA (last resort)
```

**게이트**: M0.5, M4

---

### R-04: CSI-2 D-PHY 처리량 부족
**확률**: 중간
**영향**: 높음
**상태**: PoC 검증 필요

**증상**:
- 프레임 드롭 발생
- CSI-2 TX 타임아웃
- 목표 FPS 미달

**완화 방안**:
```
1. Artix-7 OSERDES 제약 인지
   - 실제 달성 가능: ~1.0-1.25 Gbps/lane
   - 4-lane total: ~4-5 Gbps effective
   - Protocol overhead: ~20%

2. 성능 계층 조정
   - 목표 계층 (2048x2048@30fps): 실현 가능
   - 최대 계층 (3072x3072@30fps): 여유 부족

3. 외부 D-PHY PHY IC 고려
   - 2.5 Gbps/lane 지원
   - 추가 비용/복잡도
```

**대응 프로토콜**:
```bash
# 1. PoC에서 실제 처리량 측정 (W3-W6)
./tools/measure-csi2-throughput.sh

# 2. Bandwidth 재계산
# Effective BW = (lane_speed x lane_count x 0.8)

# 3. 의사결정
#    Option A: 목표 계층으로 축소
#    Option B: 외부 D-PHY IC 추가
#    Option C: FPS 감소 (30fps → 15fps)
```

**게이트**: M0.5 (PoC Gate)

---

### R-05: 시뮬레이터 vs 실제 HW 불일치
**확률**: 중간
**영향**: 중간
**상태**: M6 보정 필요

**증상**:
- FpgaSimulator와 RTL 출력 불일치
- PanelSimulator 노이즈 모델 부정확
- 타이밍 편차 >5%

**완화 방안**:
```
1. Phase 8 보정 프로세스
   - 실제 패널 데이터 수집
   - 시뮬레이터 파라미터 튜닝
   - RMSE ≤2 LSB 목표

2. 골든 모델 검증
   - FpgaSimulator를 RTL 참조 모델로 사용
   - rtl_vs_sim_checker 자동 비교

3. 특성화 테스트
   - 실제 HW 동작을 특성화 테스트로 캡처
   - 시뮬레이터 동작과 비교
```

**대응 프로토콜**:
```bash
# 1. 불일치 검출
./tools/rtl_vs_sim_checker.sh --frame 100

# 2. 원인 분석
#    - Timing assumption 검증
#    - Clock domain crossing 검증
#    - Analog behavior 모델링 검증

# 3. 보정
#    - 시뮬레이터 파라미터 업데이트
#    - detector_config.yaml 수정
```

**게이트**: M6

---

### R-12: Host 링크 대역폭 부족
**확률**: 중간
**영향**: 높음
**상태**: P0 결정 필요

**증상**:
- Host PC로 프레임 전송 지연
- 네트워크 병목
- 1 GbE 대역폭 부족

**완화 방안**:
```
1. P0 결정 (W1-W3)
   - 10 GbE 선택 (권장)
   - 또는 목표 계층 축소 (1 GbE 유지)

2. 압축 옵션 (Fallback)
   - Lossless: ~2:1 압축률
   - Lossy (JPEG): ~10:1 압축률
   - SW 복잡도 증가

3. 대역폭 요구사항
   - 최소: 0.21 Gbps (1 GbE 충분)
   - 목표: 2.01 Gbps (10 GbE 필요)
   - 최대: 4.53 Gbps (10 GbE 필요)
```

**대응 프로토콜**:
```
# P0 결정 필요:
Option A: 10 GbE 선택
  - Pros: 모든 계층 지원, 여유 충분
  - Cons: 비용 증가, PCIe add-on 필요

Option B: 압축 + 1 GbE
  - Pros: 비용 절감
  - Cons: SW 복잡도, 지연 증가, 압축률 불확실

Option C: 목표 계층 축소
  - Pros: 1 GbE 충분
  - Cons: 성능 제한
```

**게이트**: M0

---

### R-13: 의료 규제 격차
**확률**: 낮음
**영향**: 높음
**상태**: 기초 준비 시작

**증상**:
- FDA/CE 인증 준비 부족
- 추적성 부족
- 보안 기준 미달

**완화 방안**:
```
1. 기초 프레임워크 수립 (M1)
   - ISO 14971 위험 등록부
   - NIST SSDF 보안 SDLC
   - 추적성 매트릭스

2. 지속적 실천
   - Git 기반 변경 추적
   - 필수 코드 리뷰
   - 비트 정확도 검증

3. 문서화
   - Design History File (DHF) 준비
   - 검증 계획 (Verification Plan)
   - 위험 분석 문서
```

**대응 프로토콜**:
```
# 인증 준비 체크리스트
- [ ] ISO 14971 위험 분석 프레임워크
- [ ] ISO 62304 SW 생명주기 프로세스
- [ ] IEC 60601 전기 안전 기준
- [ ] DICOM 표준 호환성
- [ ] Cybersecurity 기준 (FDA guidance)
```

**게이트**: M1 (기초), M6 (완성도)

---

### R-14: Artix-7 OSERDES D-PHY Lane Speed 한계
**확률**: 중간
**영향**: 중간
**상태**: PoC 검증 필요

**증상**:
- D-PHY lane 속도 < 1.0 Gbps
- 최대 계층 전송 불가
- SI 문제 (eye diagram)

**완화 방안**:
```
1. OSERDES 한계 인지
   - Artix-7 OSERDES: ~1.0-1.25 Gbps/lane
   - D-PHY spec: 최대 2.5 Gbps/lane
   - 차이: Hardware limitation

2. 대안
   - 목표 계층 (2048x2048@30fps): 실현 가능
   - 최대 계층: 외부 D-PHY IC 필요

3. SI 최적화
   - PCB stackup 최적화
   - Impedance matching
   - Eye diagram 측정
```

**대응 프로토콜**:
```bash
# 1. PoC에서 실제 lane 속도 측정
./tools/measure-dphy-lane-speed.sh

# 2. Eye diagram 측정
# Logic analyzer with MIPI D-PHY decode

# 3. 의사결정
#    - 목표 계층 달성: PoC PASS
#    - 최대 계층 필요: 외부 D-PHY IC 추가
```

**게이트**: M0.5 (PoC Gate)

---

## 위험 대응 매트릭스

| 위험 ID | 확률 | 영향 | 우선순위 | 대응 전략 | 책임자 |
|--------|------|------|---------|---------|-------|
| R-03 | 낮음 | 높음 | 중간 | Mitigate (모니터링 + 업그레이드 경로) | FPGA Lead |
| R-04 | 중간 | 높음 | 높음 | Mitigate (PoC 검증 + 외부 IC) | Architect |
| R-05 | 중간 | 중간 | 중간 | Accept (M6 보정) | SW Lead |
| R-12 | 중간 | 높음 | 높음 | Mitigate (P0 결정) | Architect |
| R-13 | 낮음 | 높음 | 낮음 | Mitigate (기초 준비) | PM |
| R-14 | 중간 | 중간 | 중간 | Mitigate (PoC 검증) | FPGA Lead |

## 위험 모니터링 일정

### W1-W3: P0 결정
- [ ] R-12 해결 (Host 링크 선택)
- [ ] R-04 계획 확정 (PoC 준비)

### W3-W6: PoC Gate
- [ ] R-04 검증 (CSI-2 처리량)
- [ ] R-14 검증 (D-PHY lane 속도)
- [ ] R-03 초기 확인 (리소스 사용률)

### W14+: 통합 단계
- [ ] R-03 지속 모니터링
- [ ] R-05 준비 (HW 도착시)

### M6: 시스템 V&V
- [ ] R-05 보정 완료
- [ ] R-13 기초 프레임워크 완성

## Escalation Protocol

### Level 1: 팀 내 해결
- 위험 발생 감지
- 팀 리드에게 보고
- 24시간 내 초기 대응

### Level 2: PM 개입
- Level 1 해결 실패
- 일정 영향 >1주
- PM 의사결정 필요

### Level 3: Stakeholder 결정
- 아키텍처 변경 필요
- 성능 계층 변경
- 예산 영향

## 위험 리뷰 주기

- **Daily**: 활성 위험 상태 확인 (Standup)
- **Weekly**: 위험 등록부 업데이트 (Sprint Review)
- **Milestone**: 전체 위험 재평가 (Milestone Gate)

---

*Last Updated: 2026-02-16*
