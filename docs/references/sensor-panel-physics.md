# Sensor Panel Physics Reference

**Document**: X-ray FPD 물리 모델 참고 자료
**Version**: 1.0.0
**Created**: 2026-03-18
**Source**: UltraThink 딥 리서치 (ROIC 벤더 + TFT 전압 + 노이즈 모델)

---

## 1. X-ray 평판 검출기(FPD) 구조

```
X-ray → [CsI(Tl) Scintillator] → Visible Light
       → [a-Si Photodiode]      → Electrons
       → [TFT Switch]           → Charge Transfer
       → [ROIC CSA]             → Voltage
       → [ADC]                  → Digital Number (DN)
```

### 대표 벤더

| 벤더 | 제품 계열 | 스킨틸레이터 | 픽셀 피치 |
|-----|----------|-----------|---------|
| Varex (Varian) | PaxScan 4030, 3030 | CsI(Tl) 600 μm | 200–400 μm |
| Canon | CXDI Series | CsI | 125–160 μm |
| Teledyne DALSA | Rad-icon 1520/3030 | GOS | 99 μm |
| Carestream | DRX Series | CsI | 139–148 μm |
| iRay | Various | CsI/GOS | 100–200 μm |

---

## 2. 노이즈 모델

### 2.1 전체 노이즈 공식

```
σ²_total = σ²_shot + σ²_dark + σ²_readout + σ²_FPN + σ²_1/f

where:
  σ²_shot    = signal_electrons        (Poisson, signal-dependent)
  σ²_dark    = Idark × t_frame         (dark current shot noise)
  σ²_readout = readout_noise²          (signal-independent, Gaussian)
  σ²_FPN     = (fpn_fraction × signal)² (fixed, per-pixel gain variation)
  σ²_1/f     = spatial low-frequency   (column/row correlation)
```

### 2.2 대표 파라미터 (a-Si TFT + CsI, 의료용)

| 파라미터 | 값 | 단위 |
|---------|-----|------|
| Readout noise (low gain) | 5,948 | e⁻ σ |
| Readout noise (high gain) | 1,716 | e⁻ σ |
| Detector gain (low mode) | 1,525 | e⁻/ADU |
| CsI 광결합 효율 | 64 | % |
| CsI light yield | 54,000 | photons/MeV |
| 암전류 요구사항 | ≤ 1.0 | nA/cm² |
| FPN 진폭 | 1–3 | % of signal |
| PRNU (게인 불균일) | ~3 | % σ |

### 2.3 온도 의존 암전류

```
Idark(T) = Idark(T0) × 2^((T − T0) / T_double)

T0 = 25°C (reference)
T_double ≈ 8°C (암전류 2배 온도 간격, a-Si p-i-n 기준)
```

### 2.4 Vback 의존 암전류

```
Idark(Vback) = I0 × exp(q × |Vback| / (n × k × T))

q = 1.602 × 10⁻¹⁹ C
k = 1.381 × 10⁻²³ J/K
T = 298 K (25°C)
n = 1.5 (이상 인자, a-Si p-i-n)
I0 = reference dark current at Vback = -5V
```

---

## 3. TFT 전압 파라미터

### 3.1 a-Si TFT X-ray FPD 전압 정의

| 전압 | 정의 | 범위 | 기본값 | 영상 영향 |
|-----|------|------|--------|---------|
| **VGH** | Gate High — TFT ON | 28–32 V | 30 V | 낮으면 신호 전달 불완전 → 수평 스트라이프 |
| **VGL** | Gate Low — TFT OFF | -5 to -15 V | -10 V | 부족하면 누설 전류 → 수직 스트라이프 |
| **Vback** | Back-bias (포토다이오드 역바이어스) | -15 to -30 V | -15 V | 암전류 지수 증가, 전하 수집 효율 개선 |
| **Vcom** | Common voltage (ADC 가상 접지) | 2.0–3.0 V | 2.5 V | ADC 입력 범위 기준점 |
| **Vreset** | Pixel reset voltage | 0–5 V | 0 V | 프레임 시작 전 픽셀 초기화 |

### 3.2 전압 이탈 시 아티팩트

```
VGL 이탈 (덜 음의 값):
  TFT 채널 완전 차단 불가
  → 픽셀 누설 전류 발생
  → 열(column) 방향 밝은 스트라이프
  → FPN 증가

VGH 이탈 (낮은 값):
  TFT 완전 개방 불가
  → 신호 전달 불완전
  → 행(row) 방향 어두운 스트라이프

Vback 과다 (더 음의 값):
  포토다이오드 과역바이어스
  → 암전류 지수 증가
  → 전체 신호 기저(background) 상승
  → SNR 악화

Vback 부족 (덜 음의 값):
  고갈 영역 축소
  → 전하 수집 효율 감소
  → 신호 감도 저하
```

### 3.3 이미지 랙(Image Lag)

```
원인: a-Si 트랩 상태(defect states)에 전하 포획

모델:
  output[n] = signal[n] + lag_fraction × signal[n-1] × exp(-dt/tau_trap)

전형값:
  lag_fraction: 0.02–0.10 (2–10% 첫 프레임)
  tau_trap: 10–100 ms
  보정 방법: Forward bias technique (행당 ≤8개, Vfwd ≥ 2.9V)
```

---

## 4. Fixed Pattern Noise (FPN) 구조

### 4.1 FPN 구성 요소

```
FPN_total[r, c] = FPN_pixel[r, c] + FPN_column[c] + FPN_row[r]

FPN_pixel[r, c] : 픽셀별 감도 차이 (Gaussian, σ = 1–2% of signal)
                  → 원인: 포토다이오드 특성 분산
FPN_column[c]   : 열별 오프셋 (ROIC 채널 불균일, 저주파 패턴)
                  → 원인: ADC 채널간 오프셋 차이
FPN_row[r]      : 행별 오프셋 (게이트 라인 딜레이)
                  → 원인: 게이트 라인 저항-용량(RC) 딜레이
```

### 4.2 FPN 시뮬레이션 알고리즘

```csharp
// Column FPN: 저주파 사인파 + 무작위 성분
double[] columnFpn = new double[cols];
for (int c = 0; c < cols; c++) {
    columnFpn[c] = A_col * Math.Sin(2 * Math.PI * c / (cols / 3.0))
                 + B_col * (rng.NextDouble() - 0.5) * 2;
}
// A_col = signal * 0.01 (1% 진폭)
// B_col = signal * 0.005 (0.5% 랜덤 성분)

// Row FPN: 유사 (저주파 + 랜덤)
// Pixel FPN: Gaussian(0, signal * 0.01) — seed 고정

// 적용: pixel[r,c] *= (1 + columnFpn[c] + rowFpn[r] + pixelFpn[r,c])
```

---

## 5. Dark Frame vs Bright Frame 특성

### 5.1 Dark Frame (암영상)

특징:
- 전체적으로 낮은 신호 (dark current level)
- 열 스트라이프 구조 (FPN_column)
- 온도에 따른 배경 기울기 (gradient)
- 핫 픽셀 / 데드 픽셀 (DefectMap)
- Gaussian 분포 (readout noise 지배)

시뮬레이션 파라미터:
```yaml
dark_frame:
  baseline_dn: 200            # 14-bit 기준 중간 오프셋
  readout_noise_dn: 5.0       # ≈ 5948 e⁻ / 1525 e⁻/ADU ≈ 3.9 ADU
  dark_current_dn_per_ms: 0.5 # 100ms frame → +50 DN
  temperature_celsius: 25.0
```

### 5.2 Bright Frame (밝은 영상 — 균일 조사)

특징:
- 높은 평균 신호 (dose-dependent)
- 픽셀간 게인 불균일 (PRNU)
- 신호 의존 Poisson 노이즈 (분산 = 평균 신호)
- 포화 픽셀 → 최대값 클램핑
- FPN 잔여분 (보정 후 ~0.3%)

시뮬레이션 파라미터:
```yaml
bright_frame:
  target_fill_fraction: 0.5   # 동적 범위의 50% 목표
  prnu_stddev_pct: 3.0        # ±3% 픽셀간 게인 불균일
  shot_noise: true             # Poisson 노이즈 활성화
  saturation_clamp: true       # bit-depth max 클램핑
```

---

## 6. PDF에서 추출 가능한 파라미터

### 6.1 패널 데이터시트 (예: AUO R1717AS01.3)

| 파라미터 | 위치 | 추출 가능성 |
|---------|------|-----------|
| Pixel pitch (μm) | "Pixel Size", "Pixel Pitch" | 높음 (텍스트) |
| Gate rows (lines) | "Gate Lines", "Active Rows" | 높음 |
| Source cols (lines) | "Source Lines", "Active Cols" | 중간 |
| Bit depth | "Output Format", "Bit Depth" | 중간 |
| VGL | "Gate Low Voltage", "VGL" | 낮음 (ROIC 스펙에 있음) |
| VGH | "Gate High Voltage", "VGH" | 낮음 |
| Vback | "Back Bias", "Vback", "Reverse Bias" | 낮음 |
| Dark current | "Leakage Current", "Dark Current" | 중간 |
| Frame rate | "Frame Rate", "fps" | 높음 |

### 6.2 ROIC 데이터시트에서 추출

전압 파라미터(VGL/VGH/Vback)는 주로 ROIC(예: Flare, OmniVision, IGNIS) 데이터시트에 있으며, 패널 스펙 PDF에는 미포함인 경우가 많다. 수동 입력 또는 ROIC 스펙 별도 참조 필요.

---

## 7. 구현 우선순위 및 복잡도

| 기능 | 우선순위 | 복잡도 | 기존 코드 재사용 |
|------|---------|--------|----------------|
| CompositeNoiseGenerator 통합 | 1순위 | 낮음 | CompositeNoiseGenerator.cs 존재 |
| FPN Map 생성 | 2순위 | 중간 | 신규 구현 필요 |
| Vback 의존 암전류 | 3순위 | 중간 | GateResponseModel 수정 |
| LagModel 통합 | 3순위 | 낮음 | LagModel.cs 존재 |
| 온도 의존 암전류 | 4순위 | 낮음 | 수식 구현 |
| VGL/VGH 아티팩트 | 5순위 | 중간 | 신규 ArtifactModel 필요 |
| PDF VGL/VGH 추출 | 6순위 | 낮음 | PdfParser regex 추가 |

---

## 8. 참고 문헌

1. Siewerdsen JH, Jaffray DA. "Noise variance analysis using a flat panel x-ray detector." Med Phys. 2000 Mar;27(3):542-54. PMC2902539
2. Zhao W et al. "A semiempirical linear model of indirect, flat-panel x-ray detectors." Med Phys. 2012. PMC3326070
3. Schmitt CA et al. "Compound Poisson noise verification for X-ray flat panel imager." IEEE NSS/MIC 2015. DOI:10.1109/NSSMIC.2015.7294576
4. Zhao W, Rowlands JA. "A forward bias method for lag correction of an a-Si flat panel detector." Med Phys. 2012. PMC3257750
5. Siewerdsen JH et al. "A nonlinear lag correction algorithm for a-Si flat-panel x-ray detectors." Med Phys. 2012. PMC3465354
6. RP Photonics Encyclopedia. "Dark Current." https://www.rp-photonics.com/dark_current.html
7. Radiologykey.com. "TFT Flat-Panel Array Image Acquisition." https://radiologykey.com/tft-flat-panel-array-image-acquisition/
8. Varex Imaging. "PaxScan 4030CB Product Specifications." (Vendor documentation)
9. Teledyne DALSA. "Rad-icon 3030 Datasheet." https://www.teledynevisionsolutions.com/products/rad-icon

---

*Generated by UltraThink deep research — 2026-03-18*
