# X-ray Detector Panel System - 2차 업그레이드 계획서 (v2.1)

## Document Info

| 항목 | 내용 |
|------|------|
| 문서 ID | XDPS-PLAN-2026-002 |
| 버전 | v2.1 (사양서 반영판) |
| 기준 문서 | v2.0 + 5종 Panel PDF + Gate IC PDF + ROIC PDF |
| 작성일 | 2026-03-16 |
| 상태 | Draft |

---

## 1. 사양서 교차 분석 결과

### 1.1 입수 사양서 목록

| # | 문서명 | 유형 | 공급사 | 핵심 정보 |
|---|------|------|------|----------|
| 1 | R1717AS01.3 Product Spec | Panel (a-Si) | AUO | 3072×3072, 140μm, 65% FF |
| 2 | R1714AS08.0 Product Spec | Panel (a-Si) | AUO | 3072×2500, 140μm, 65% FF |
| 3 | R1717GH01 IIS | Panel (IGZO) | AUO | 검사 규격, 결함 분류, Driving 조건 |
| 4 | X239AW1-102 CAS | Panel (a-Si) | Innolux | 3072×3072, 140μm, 결함 규격 |
| 5 | 1717 Panel Spec (preliminary) | Panel (a-Si) | Innolux | 3072×3072, 140μm, TEG 사양 |
| 6 | NT39522DH | Gate IC | Novatek | 541ch, VGG-VEE=40V, 200kHz |
| 7 | AD71143 | ROIC (AFE) | ADI | 256ch, 16-bit, 60μs line, 580e⁻ |
| 8 | AFE2256GR | ROIC (AFE) | TI | 256ch, 16-bit, 51.2μs scan, 240e⁻ |
| 9 | DDC3256 | ROIC (CT급) | TI | 256ch, 24-bit, 50μs, 320pC |

### 1.2 Panel 실측 파라미터 추출 결과

| 파라미터 | R1717AS01.3 (AUO) | R1714AS08.0 (AUO) | X239AW1-102 (Innolux) |
|---------|-------------------|-------------------|----------------------|
| Technology | a-Si TFT / PIN diode | a-Si TFT / PIN diode | a-Si TFT / PD |
| 해상도 (Gate × Data) | 3072 × 3072 | 3072 × 2500 | 3072 × 3072 |
| Pixel Pitch | 140 μm | 140 μm | 140 μm |
| Panel Size | 445 × 445 mm | 443 × 364.92 mm | 445 × 445 mm |
| Active Area | 430.08 × 430.08 mm | 430.08 × 350 mm | 430.08 × 430.08 mm |
| Fill Factor | 65% | 65% | - |
| Glass Thickness | 0.7 mm | 0.7 mm | 0.7 mm |
| TFT Ron | 2.2 MΩ | 2.2 MΩ | - |
| PIN Pixel Capacitance | 1.48 pF | 1.48 pF | - |
| Pixel Leakage (Max) | 3 fA/pixel | 3 fA/pixel | - |
| TFT Leakage (Max) | 80 fA/pixel | 80 fA/pixel | - |
| Lag (1st Frame) | 3% | 3% | - |
| Gate Pad Blocks | 6 (562 pads/block) | 6 (562 pads/block) | 6 (562 pads/block) |
| Data Pad Blocks | 12 (270 pads/block) | 10 (270 pads/block) | 12 (270 pads/block) |
| Gate Pad Pitch | 72 μm | 72 μm | 72 μm |
| Data Pad Pitch | 100 μm | 100 μm | 100 μm |

**Panel 구동 조건 (R1717GH01 IIS 기준):**

| 항목 | 값 |
|------|---|
| VGH | +15V |
| VGL | -15V |
| Vcom (Bias) | -5V |
| Vref | +1V |
| IFS (Integrator Feedback Capacitance) | 31 |
| Gate-On Time | 5μs |

**Innolux Panel 구동 전압 범위 (X239AW1-102, 1717):**

| 항목 | Min | Typ | Max |
|------|-----|-----|-----|
| VGH (Gate-on) | 5V | 15V | 23V |
| VGL (Gate-off) | -12V | -5V | -3V |
| Photodiode Bias | -10V | -5V | -3V |

### 1.3 Gate IC 실측 파라미터 (NT39522DH)

| 파라미터 | 값 | 단위 |
|---------|---|------|
| Channel Selection | 541/513/385/361 |  ch |
| VGG Range | VCC ~ VEE+40 | V |
| VEE Range | -15 ~ -5 | V |
| VCC | 2.3 ~ 3.6 | V |
| Max Clock Frequency (FCPV) | 200 | kHz |
| Clock Pulse Width (min) | 500 | ns |
| STV Setup Time | 200 | ns |
| STV Hold Time | 300 | ns |
| Output Rise Time | ≤ 500 | ns |
| Output Fall Time | ≤ 400 | ns |
| Output Delay (Tdo) | ≤ 500 | ns |
| OE to Output Delay (Toe) | ≤ 500 | ns |
| Output Enabled Pulse Width (min) | 0.5 | μs |
| Operating Current (IGG) | 50 typ, 250 max | μA |
| Shift Direction | Bidirectional (LR pin) | - |
| Chip Select Mode | Normal / 2G | - |
| Package | COF | - |

**Gate IC당 유효 채널**: 6개 COF × 512ch = 3072 gate lines (512ch 모드 사용)

### 1.4 ROIC 실측 파라미터 비교

| 파라미터 | AD71143 (ADI) | AFE2256GR (TI) | DDC3256 (TI) |
|---------|-------------|--------------|------------|
| Channels | 256 | 256 | 256 |
| ADC Resolution | 16-bit | 16-bit | 24-bit |
| Min Line Time | 60 μs | 51.2 μs | 50 μs |
| Max Charge Range | 16.0 pC | 9.6 pC | 320 pC |
| Noise (best) | 580 e⁻ rms | 240 e⁻ rms | 0.26 fC rms |
| INL | ±2.5 LSB | ±2 LSB | ±0.025% |
| CDS | Yes | Yes (built-in) | No (continuous) |
| Power/channel | 1.4 mW (normal) | 1.3~1.4 mW | 1.2 mW |
| Output Interface | LVDS serial | LVDS DDR | LVDS serial |
| SPI Config | Yes (daisy-chain) | Yes | Yes |
| Supply | 5V + 2.5V multi-rail | 1.85V + 3.3V | 1.85V single |
| Package | SOF (flex) | COF (flex) | BGA |
| Panel Interface | 256 inputs (flex to glass) | 256 inputs (glass) | 256 inputs (BGA) |

**ROIC 선정**: Data line 12 blocks × 256ch = 3072 → AFE2256GR 12개 또는 AD71143 12개

---

## 2. FPS 재계산 (병목 기준)

### 2.1 FPS 산출 공식

```
Frame_Time = Gate_On_Time × N_rows + Readout_Overhead
FPS = 1 / Frame_Time
```

**병목 요소 2가지:**
1. **Gate IC 병목**: Gate-On Time per line (ROIC readout 포함)
2. **ROIC 병목**: Line Time (integrator reset + CDS + ADC + readout)

실제 Line Time = max(Gate_On_Time, ROIC_Line_Time) + Inter-line overhead

### 2.2 실측 기반 FPS 계산

| 조건 | Gate-On (IIS) | ROIC Line Time | Line Time (병목) | 비고 |
|------|-------------|---------------|----------------|------|
| R1717GH01 기준 | 5 μs | - | - | Gate-On만 명시 |
| AD71143 min | - | 60 μs | 60 μs | ROIC 병목 |
| AFE2256GR STR=2 | - | 51.2 μs | 51.2 μs | ROIC 병목 |
| AFE2256GR STR=3 | - | 102.4 μs | 102.4 μs | 고정밀 모드 |

**참고**: Gate-On Time(5μs)은 ROIC Line Time(51.2~60μs) 내에 포함됨 → **ROIC가 병목**

### 2.3 Panel별 실현 가능 FPS

#### AFE2256GR (51.2 μs/line) 기준

| Panel | Rows | Frame Time | Max FPS | Raw Data Rate (16-bit) |
|-------|------|------------|---------|----------------------|
| R1717AS01.3 | 3072 | 157.3 ms | **6.36 fps** | 0.96 Gbps |
| R1714AS08.0 | 3072 | 157.3 ms | **6.36 fps** | 0.78 Gbps |
| 2048×2048 (가상) | 2048 | 104.9 ms | **9.54 fps** | 0.64 Gbps |
| 1024×1024 (가상) | 1024 | 52.4 ms | **19.08 fps** | 0.32 Gbps |

#### AD71143 (60 μs/line) 기준

| Panel | Rows | Frame Time | Max FPS | Raw Data Rate (16-bit) |
|-------|------|------------|---------|----------------------|
| R1717AS01.3 | 3072 | 184.3 ms | **5.43 fps** | 0.82 Gbps |
| R1714AS08.0 | 3072 | 184.3 ms | **5.43 fps** | 0.67 Gbps |
| 2048×2048 (가상) | 2048 | 122.9 ms | **8.14 fps** | 0.55 Gbps |

#### DDC3256 (50 μs/line, CT급) 기준

| Panel | Rows | Frame Time | Max FPS | Raw Data Rate (16-bit) |
|-------|------|------------|---------|----------------------|
| 3072×3072 | 3072 | 153.6 ms | **6.51 fps** | 0.98 Gbps |

**결론**: 3072×3072 패널은 ROIC Line Time 병목으로 **최대 약 5~7 fps**. Fluoroscopy(동영상) 30fps는 별도 binning/ROI 없이는 불가능하며, 이는 radiography(정지 영상) 용도의 일반적 범위.

### 2.4 수정된 대역폭 분석

| 해상도 | Rows | ROIC | FPS | Data Rate | 1GbE | 2.5GbE | 5GbE |
|--------|------|------|-----|-----------|------|--------|------|
| 1024×1024 | 1024 | AFE2256 | 19.1 | 0.32 Gbps | ✅ | ✅ | ✅ |
| 2048×2048 | 2048 | AFE2256 | 9.5 | 0.64 Gbps | ✅ | ✅ | ✅ |
| 3072×3072 | 3072 | AFE2256 | 6.4 | 0.96 Gbps | ⚠️ | ✅ | ✅ |
| 3072×3072 | 3072 | AD71143 | 5.4 | 0.82 Gbps | ✅ | ✅ | ✅ |
| 3072×2500 | 3072 | AFE2256 | 6.4 | 0.78 Gbps | ✅ | ✅ | ✅ |

**결론**: 실제 사양 기반으로 **1GbE로 대부분 커버 가능**. 2.5GbE/5GbE는 향후 고속 모드(binning, ROI)나 여유 확보용.

---

## 3. 파라미터 추출기 구조 (v2.1 개선)

### 3.1 3종 독립 입력 → 개별 추출 → 파인튜닝 아키텍처

```
[Panel PDF] ──→ [Panel Parser] ──→ [Panel Template Matcher] ──→ panel_params.json
[Gate IC PDF] ─→ [Gate Parser] ──→ [Gate Template Matcher]  ──→ gate_ic_params.json
[ROIC PDF] ───→ [ROIC Parser] ──→ [ROIC Template Matcher]  ──→ roic_params.json
                                                                    │
                                                    ┌───────────────┘
                                                    ▼
                                           [Merge & Validate]
                                                    │
                                                    ▼
                                          detector_config.yaml
                                          (YAML + JSON 출력)
```

### 3.2 추출 파인튜닝 사이클

```
Phase 1: 초기 구현
  ├─ 범용 PDF 텍스트/테이블 파싱 (PdfPig/iText7)
  ├─ 키워드 기반 파라미터 매칭
  └─ 수동 보정 UI

Phase 2: 사양서 투입 후 규칙 최적화
  ├─ AUO 사양서 포맷 분석 → AUO 전용 Rule 추가
  ├─ Innolux 사양서 포맷 분석 → Innolux 전용 Rule 추가
  ├─ ADI/TI ROIC 포맷 분석 → IC 벤더별 Rule 추가
  └─ Novatek Gate IC 포맷 분석 → Gate IC Rule 추가

Phase 3: 파인튜닝
  ├─ 추출 정확도 측정 (수동 입력 vs 자동 추출 비교)
  ├─ Rule 가중치 조정
  ├─ 미검출 파라미터 패턴 추가
  └─ 목표: 핵심 파라미터 자동 추출률 ≥ 80%
```

### 3.3 사양서별 추출 대상 (실제 사양서 기반 확정)

#### Panel 추출 대상 (AUO R1717AS01.3 기준)

| 카테고리 | 파라미터 | 사양서 내 위치 | 추출 난이도 |
|---------|---------|-----------|---------|
| General | Technology (a-Si/IGZO) | Section 2.1 | 쉬움 |
| General | Pixel Pitch (140μm) | Section 2.1 | 쉬움 |
| General | Number of Pixels (3072×3072) | Section 2.1 | 쉬움 |
| General | Active Area (430.08×430.08) | Section 2.1/2.2 | 쉬움 |
| General | Fill Factor (65%) | Section 2.3 | 쉬움 |
| Electrical | TFT Ron (2.2 MΩ) | Section 2.3 | 중간 |
| Electrical | Pixel Capacitance (1.48 pF) | Section 2.3 | 중간 |
| Electrical | Pixel Leakage (3 fA) | Section 2.3 Note1 | 중간 |
| Electrical | TFT Leakage (80 fA) | Section 2.3 Note3 | 중간 |
| Performance | Lag 1st Frame (3%) | Section 2.3 Note2 | 어려움 |
| Mechanical | Gate Pad Blocks/Pitch | Section 2.2 | 쉬움 |
| Mechanical | Data Pad Blocks/Pitch | Section 2.2 | 쉬움 |
| Line Resistance | Gate/Data/Bias (K ohm) | Section 2.3 | 중간 |
| Driving | VGH/VGL/Vcom/Vref | IIS Section 3.1 | 쉬움 |
| Defect | Cluster class 규격 | IIS Section 5.1 | 어려움 |

#### Gate IC 추출 대상 (NT39522DH 기준)

| 파라미터 | 사양서 내 위치 | 추출 난이도 |
|---------|-----------|---------|
| Output Channels (541/513/385/361) | Section 2 Features | 쉬움 |
| VGG Range (VCC ~ VEE+40V) | Section 6 DC Electrical | 중간 |
| VEE Range (-15V ~ -5V) | Section 6 DC Electrical | 중간 |
| VCC (2.3~3.6V) | Section 6 DC Electrical | 쉬움 |
| Max Clock (200kHz) | Section 7 AC Electrical | 중간 |
| Clock Pulse Width (500ns min) | Section 7 AC Electrical | 중간 |
| Output Rise/Fall Time | Section 7 AC Electrical | 중간 |
| STV Setup/Hold Time | Section 7 AC Electrical | 중간 |
| Shift Direction (LR pin) | Section 4 Pin Description | 중간 |
| Power-on Sequence | Section 5-1 | 어려움 |

#### ROIC 추출 대상 (AD71143 기준)

| 파라미터 | 사양서 내 위치 | 추출 난이도 |
|---------|-----------|---------|
| Channels (256) | Features / Table 1 | 쉬움 |
| ADC Resolution (16-bit) | Table 1 | 쉬움 |
| Forward Charge Range (0.5~16.0 pC) | Table 1 Analog Input | 중간 |
| Min Line Time (60μs) | Table 1 Throughput | 중간 |
| LPF Time Constant (1.3~11.7μs) | Table 1 Throughput | 중간 |
| Noise (580~880 e⁻ rms) | Table 1 Accuracy | 중간 |
| INL (±2.5 LSB) | Table 1 Accuracy | 쉬움 |
| Crosstalk (0.07% adjacent) | Table 1 Accuracy | 중간 |
| Power per channel (1.4 mW) | Table 1 Supply Current | 중간 |
| LVDS Output Format | Table 1 Digital Outputs | 쉬움 |
| AFE Sequencer Timing Registers | Table 5 | 어려움 |
| Transfer Function Formula | Theory of Operation | 어려움 |

---

## 4. 실측 영상 캘리브레이션 파이프라인 (신규)

### 4.1 개요

실제 Panel에서 취득한 Dark/Bright 영상을 시뮬레이터에 입력하여 모델 파라미터를 피팅하고 시뮬레이션-실측 오차를 최소화한다.

### 4.2 파이프라인

```
[실측 Dark Image (Raw)] ──→ [Statistics Analyzer] ──→ dark_stats.json
[실측 Bright Image (Raw)] ─→ [Statistics Analyzer] ──→ bright_stats.json
                                                           │
                                                           ▼
[현재 Sim 파라미터] ──→ [Calibration Fitter] ←── [실측 stats]
                              │
                              ▼
                    [Calibrated Parameters]
                              │
                    ┌─────────┴─────────┐
                    ▼                   ▼
            detector_config.yaml    calibration_report.json
            (파라미터 업데이트)      (피팅 결과/오차 리포트)
```

### 4.3 피팅 대상 파라미터

| 파라미터 | Dark에서 추출 | Bright에서 추출 |
|---------|-----------|------------|
| Dark Current Mean/Sigma | ✅ | - |
| Readout Offset (채널별) | ✅ | - |
| Electronic Noise (σ_read) | ✅ | - |
| Gain Map (픽셀별 감도) | - | ✅ |
| Gain Non-uniformity (σ_gain) | - | ✅ |
| Defect Map (자동 검출) | ✅ | ✅ |
| Scintillator Non-uniformity | - | ✅ |

### 4.4 입력 포맷

| 형식 | 설명 |
|------|------|
| Raw binary (16-bit unsigned) | ROIC 직출력 데이터 |
| TIFF (16-bit grayscale) | 표준 영상 포맷 |
| CSV (optional) | 디버깅/소량 데이터용 |

### 4.5 캘리브레이션 수식

AUO 사양서에 명시된 leakage 측정 공식 기반:

```
Pixel Leakage:
  I = (CNT_70 - CNT_10) × (e⁻/LSB) × 1.6×10⁻¹⁹ / ΔT

TFT Leakage:
  I = (CNT_10 - CNT_20) × (e⁻/LSB) × 1.6×10⁻¹⁹ / ΔT

Lag:
  Lag(%) = [Median(Fn) - Offset] / [Median(F0) - Offset] × 100%
```

이 공식들을 시뮬레이터 Dark/Bright 모델에 역으로 적용하여 파라미터를 피팅한다.

### 4.6 결함 검출 기준 (사양서 기반)

**AUO IIS (R1717GH01) 기준:**

| 검사 항목 | Dark Image 조건 | Bright Image 조건 |
|---------|-------------|--------------|
| Point Defect | > ±6σ (32×32 ROI) | > ±25% (100×100 ROI) |
| Line Defect | ≥4 연속 결함 픽셀 | ≥5% bad pixels on line |
| Localized High Density | ≤40 (0.1%) in 64×64 ROI | - |

**Innolux (X239AW1-102) 기준:**

| 검사 항목 | 기준 |
|---------|------|
| Point Defect | Charge value ±15% of Panel Median |
| Line Defect | Row/Column with >5% defective pixels |
| Cluster | 3×3 window, max 6 defects |

→ 시뮬레이터 DefectEngine에 **양사 기준 모두 구현**하여 선택 가능하게 함.

---

## 5. 수정된 Panel 영상 시뮬레이션 모델

### 5.1 Panel Technology 지원

| Technology | 대상 Panel | 특이사항 |
|-----------|----------|---------|
| a-Si TFT / PIN diode | R1717AS01.3, R1714AS08.0, X239AW1-102 | 표준 indirect FPD |
| IGZO TFT | R1717GH01 | 저 leakage, AUO 신제품 |

→ 시뮬레이터에서 a-Si / IGZO TFT 모델 선택 기능 필요 (leakage 특성 차이)

### 5.2 수정된 노이즈 모델 파라미터 (실측 기반)

| 파라미터 | 값 (사양서 기준) | 소스 |
|---------|------------|------|
| Pixel Capacitance | 1.48 pF | AUO R1717AS01.3 |
| Pixel Leakage | ≤ 3 fA/pixel | AUO Product Spec |
| TFT Leakage | ≤ 80 fA (a-Si), ≤ 10 fA (IGZO) | AUO IIS |
| Diode Leakage | ≤ 3 fA | AUO IIS (R1717GH01) |
| Lag (1st frame) | ≤ 3% (a-Si), ≤ 5% (IGZO @ Gon=5μs) | AUO Product Spec / IIS |
| ROIC Noise | 580~1000 e⁻ rms (AD71143), 240~1050 e⁻ (AFE2256) | ROIC Spec |
| ROIC INL | ±2~2.5 LSB | ROIC Spec |
| ROIC Crosstalk | 0.01~0.07% | ROIC Spec |

---

## 6. 수정된 개발 일정 (v2.1)

v2.0 대비 변경: 파라미터 추출기 파인튜닝 사이클 + 실측 캘리브레이션 단계 추가

```
W1  W2  W3  W4  W5  W6  W7  W8  W9  W10 W11 W12 W13 W14 ~  W28 W29 W30 W31 W32 W33 W34
|===P0: 사양 확정===|
    |===P1: 추출기 v1======|
         |===P2: Panel Sim======|
              |===P3: Gate/ROIC Emu=====|
                   |===P4: FPGA Emu===============|
                             |===P5: SoC+Host Sim========|
                                  |===P6: GUI 통합==================|
                                            |===P7: 통합 DataPath==========|
|                                                     |===P8: 추출기 파인튜닝====|  ← 신규
                                                           |===P9: 실측 캘리브레이션==| ← 신규
                                                                     |===P10: 최종 검증===|
```

### Milestones (v2.1)

| Milestone | Week | Gate Criteria |
|-----------|------|--------------|
| M0 | W3 | 사양 확정, 3종 사양서 파라미터 수동 추출 완료 |
| M1 | W7 | 추출기 v1: AUO R1717AS01.3 PDF → YAML 변환 동작 |
| M2 | W11 | Panel Sim: Dark/Bright + Noise + Defect 영상 (실측 파라미터 기반) |
| M3 | W15 | Gate IC + ROIC Emulator 단위 테스트 PASS |
| M4 | W19 | FPGA Emulator + CSI-2 패킷 생성 완료 |
| M5 | W23 | 전체 Data Path 시뮬레이션 동작 |
| M6 | W27 | GUI 통합 완료 |
| M7 | W30 | 추출기 파인튜닝: 5종 사양서 핵심 파라미터 자동 추출률 ≥ 80% |
| M8 | W32 | 실측 캘리브레이션: Dark/Bright 피팅, RMSE ≤ 2 LSB |
| M9 | W34 | 최종 검증 + Release Candidate |

---

## 7. v2.0 대비 v2.1 변경 이력

| # | 변경 사항 | 근거 |
|---|---------|------|
| 1 | FPS 재계산: 3072×3072 → 5.4~6.4fps (ROIC 병목) | AD71143 60μs, AFE2256 51.2μs line time |
| 2 | v2.0의 4096×4096@30fps 삭제 → 물리적 불가능 | Line Time × Rows 기반 계산 |
| 3 | 대역폭 표 수정: 실제 FPS 기준, 1GbE로 대부분 커버 | 데이터율 재계산 |
| 4 | Panel 파라미터를 실측 사양서 값으로 교체 | AUO/Innolux 5종 사양서 |
| 5 | Gate IC 파라미터를 NT39522DH 실측값으로 교체 | Novatek 사양서 |
| 6 | ROIC 파라미터를 AD71143/AFE2256GR/DDC3256 비교표로 교체 | ADI/TI 사양서 |
| 7 | 파라미터 추출기: 3종 독립 입력 + 파인튜닝 사이클 구조 | 사용자 요구사항 |
| 8 | 실측 캘리브레이션 파이프라인 신규 추가 | 사용자 요구사항 |
| 9 | 결함 검출 기준: AUO IIS + Innolux CAS 이중 기준 | 양사 사양서 |
| 10 | a-Si / IGZO 이중 TFT 모델 지원 | R1717GH01 (IGZO) 사양서 |
| 11 | AUO leakage 공식 (Pixel/TFT/Lag) 시뮬레이터 반영 | AUO Product Spec Note1~3 |
| 12 | 일정 32주→34주 (추출기 파인튜닝 + 실측 캘리브레이션 추가) | 공정 추가 |
| 13 | Panel 구동 전압 실측값 반영 (VGH/VGL/Vcom/Vref) | AUO IIS + Innolux CAS |

---

*Generated based on: 5종 Panel PDF + NT39522DH Gate IC + AD71143/AFE2256GR/DDC3256 ROIC 교차분석*
*Date: 2026-03-16*
