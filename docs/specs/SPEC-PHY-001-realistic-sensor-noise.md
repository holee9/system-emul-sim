# SPEC-PHY-001: Realistic Sensor Panel Noise Model

**SPEC-ID**: SPEC-PHY-001
**Status**: Planned
**Version**: 1.0.0
**Created**: 2026-03-18
**Author**: ABYZ-Lab (UltraThink Research)
**Priority**: High

---

## 1. Overview

현재 PanelSimulator의 mock 영상은 고정 패턴(Gaussian noise only)으로 생성되어 실제 X-ray 평판 검출기(FPD)의 특성을 충분히 모사하지 못한다. 본 SPEC은 ROIC 벤더별 dark/bright 영상 특성 및 TFT 전압 파라미터(VGL/VGH/Vback)를 반영한 물리 기반 노이즈 모델을 정의한다.

## 2. Background: Research Findings

### 2.1 ROIC 벤더별 노이즈 특성 (검증된 값)

| 파라미터 | 값 | 단위 | 출처 |
|---------|-----|------|------|
| 암전류 요구사항 | ≤ 10⁻⁹ | A/cm² | Industry spec |
| Readout noise (low gain) | 5,948 | e⁻ σ | PaxScan 4030CB 실측 |
| Readout noise (high gain) | 1,716 | e⁻ σ | PaxScan 4030CB 실측 |
| CsI 광결합 효율 | 64 | % | 선형 모델 연구 |
| FPN 진폭 | 1–3 | % of signal | 산업 표준 |
| 픽셀간 게인 변동 | ~10 | % | 실측 |
| 온도 계수 | 0.002–0.004 | per °C | 물리 모델 |

### 2.2 TFT 전압 파라미터 (a-Si TFT X-ray FPD)

| 파라미터 | 범위 | 기본값 | 영상 영향 |
|---------|------|--------|---------|
| VGH | 28–32 V | 30 V | TFT 완전 개방; 낮으면 신호 손실 |
| VGL | -5 to -15 V | -10 V | TFT 차단; 부족하면 수직 스트라이프 |
| Vback | -15 to -30 V | -15 V | 암전류 지수 증가; 전하 수집 효율 |
| Vcom | 2.0–3.0 V | 2.5 V | ADC 기준점 |
| Vreset | 0–5 V | 0 V | 프레임 간 잔존 신호 |

### 2.3 현재 구현 상태 (Gap Analysis)

| 기능 | 상태 | 비고 |
|------|------|------|
| GaussianNoiseGenerator | 통합됨 | PhysicsBased 파이프라인 활성 |
| CompositeNoiseGenerator | **미통합** | 코드 존재, 파이프라인 연결 없음 |
| PoissonNoiseGenerator | **미통합** | 코드 존재, config 미노출 |
| LagModel | **미통합** | 코드 존재, 파이프라인 연결 없음 |
| DriftModel | **미통합** | 코드 존재, 파이프라인 연결 없음 |
| VGL/VGH/Vback 파라미터 | **없음** | GateResponseModel에 전압 물리 없음 |
| FPN (Fixed Pattern Noise) | **없음** | 열/행 구조 노이즈 미구현 |
| 온도 의존 암전류 | **없음** | DarkCurrentRatePerSec 상수만 존재 |

---

## 3. Requirements (EARS Format)

### REQ-PHY-001: Composite Noise Integration
**WHEN** TestPattern == PhysicsBased,
**THE SYSTEM SHALL** apply CompositeNoiseGenerator (dark current + photon shot noise + readout noise + FPN + 1/f) instead of Gaussian-only noise.

### REQ-PHY-002: Poisson Shot Noise
**THE SYSTEM SHALL** model photon shot noise as Poisson distribution: σ²_shot = signal_electrons,
using Gaussian approximation (σ = √signal) when signal > 30 electrons.

### REQ-PHY-003: Fixed Pattern Noise (FPN)
**THE SYSTEM SHALL** generate per-frame FPN as:
- Column FPN: amplitude 1–2% of signal, low spatial frequency (column-wise correlation)
- Row FPN: amplitude 0.5–1% of signal
- Per-pixel FPN: random map seeded once per simulation session
**THE SYSTEM SHALL** persist FPN map across frames (frame-invariant fixed pattern).

### REQ-PHY-004: ROIC Voltage Parameters
**WHEN** detector_config.yaml contains `roic:` section,
**THE SYSTEM SHALL** apply voltage-dependent effects:
- VGL out of nominal range → vertical stripe artifact (column amplitude ∝ |VGL − VGL_nominal|)
- VGH out of nominal range → horizontal stripe artifact
- Vback → dark current: `Idark = Idark_0 × exp(−q × |Vback| / (n × k × T))`

### REQ-PHY-005: Temperature-Dependent Dark Current
**THE SYSTEM SHALL** compute dark current as:
`Idark(T) = Idark(25°C) × 2^((T − 25) / 8)`
where T is detector temperature in °C (default 25°C).

### REQ-PHY-006: Image Lag Model Integration
**THE SYSTEM SHALL** integrate LagModel into PhysicsBased pipeline:
- First frame lag fraction: configurable (default 5%)
- Decay: exponential with tau_trap_ms (default 50 ms)
- Formula: `output[n] = current[n] + lag_coeff × frame_history[n-1]`

### REQ-PHY-007: Dark Frame Appearance
**THE SYSTEM SHALL** generate realistic dark frames with:
- Non-uniform dark level gradient (radial or linear, ±5% of mean)
- Column stripe structure (FPN)
- Salt-and-pepper noise (hot/dead pixel simulation via DefectMap)
- Temperature-dependent baseline shift

### REQ-PHY-008: Bright Frame Appearance
**THE SYSTEM SHALL** generate realistic bright frames with:
- Signal-dependent Poisson noise (shot noise)
- Gain non-uniformity map (PRNU, ±3% per pixel)
- Saturation behavior at bit-depth maximum
- Residual FPN after "calibration" subtraction (0.3% residual)

### REQ-PHY-009: detector_config.yaml Schema Extension
**THE SYSTEM SHALL** extend detector_config.yaml with:
```yaml
roic:
  vgh_volts: 30.0
  vgl_volts: -10.0
  vback_volts: -15.0
  vcom_volts: 2.5
  vreset_volts: 0.0
  readout_noise_electrons: 5948
  gain_mode: low  # low | high

panel:
  temperature_celsius: 25.0
  dark_current_pa_cm2: 1.0  # pA/cm²
  image_lag_fraction: 0.05
  tau_trap_ms: 50.0

simulation:
  noise_model: composite  # none | gaussian | composite
  fpn_amplitude_pct: 1.5
  prnu_stddev_pct: 3.0
```

### REQ-PHY-010: PDF Parameter Extraction — VGL/VGH/Vback
**WHEN** ParameterExtractor parses a panel datasheet PDF,
**THE SYSTEM SHALL** attempt to extract:
- VGH from "Gate High", "VGH", "gate high voltage" text patterns
- VGL from "Gate Low", "VGL", "gate low voltage" text patterns
- Vback from "Back bias", "Vback", "substrate bias" text patterns
**THE SYSTEM SHALL** report "Not found — using default" for missing voltage parameters.

---

## 4. Implementation Plan

### Phase 1: Noise Integration (핵심 — 먼저 구현)

**목표**: CompositeNoiseGenerator를 PhysicsBased 파이프라인에 통합

**변경 파일**:
1. `PanelSimulator.cs` — PhysicsBased 분기에서 CompositeNoiseGenerator 호출
2. `PanelConfig.cs` — NoiseModel enum에 `Composite` 추가, FPN 파라미터 추가
3. `CompositeNoiseGenerator.cs` — 기존 구현 검증 및 FPN 추가
4. `detector_config.yaml` — `simulation.noise_model: composite` 추가

**검증**: 영상 시각적 검증 — 픽셀값이 프레임마다 달라야 함

### Phase 2: ROIC Voltage Model

**목표**: VGL/VGH/Vback 전압 파라미터 → GateResponseModel 반영

**변경 파일**:
1. `PanelConfig.cs` — RoicConfig 클래스 신설 (VGL, VGH, Vback, Vcom, Vreset)
2. `GateResponseModel.cs` — Vback 의존 암전류 지수 모델 추가
3. `DetectorConfig.cs` — RoicConfig 섹션 추가
4. YAML schema 확장

**수식**:
```
dark_current_factor = exp(-q * |Vback| / (n * k * T))  ; n=1.5, T=298K
Idark_effective = Idark_nominal * dark_current_factor * 2^((T-25)/8)
```

### Phase 3: Fixed Pattern Noise

**목표**: 프레임 고정 FPN 맵 생성 (열/행/픽셀 3단계)

**신규 파일**: `Models/Noise/FixedPatternNoiseMap.cs`

**알고리즘**:
```
column_fpn[c] = A_col * sin(2π * c / (Cols/3)) + B_col * random(seed)
row_fpn[r] = A_row * sin(2π * r / (Rows/5)) + B_row * random(seed+1)
pixel_fpn[r,c] = gaussian(0, A_pixel)  ; fixed per seed
total_fpn[r,c] = (1 + column_fpn[c] + row_fpn[r] + pixel_fpn[r,c]) * signal
```

### Phase 4: Image Lag Integration

**목표**: LagModel을 PhysicsBased 파이프라인에 연결

**변경 파일**:
1. `PanelSimulator.cs` — LagModel 인스턴스 유지, 프레임마다 적용
2. `PanelConfig.cs` — ImageLagFraction, TauTrapMs 파라미터 추가

### Phase 5: PDF Extraction — VGL/VGH/Vback

**목표**: ParameterExtractor에서 전압 파라미터 추출 시도

**변경 파일**:
1. `PdfParser.cs` — voltage pattern regex 추가
2. `ConfigExporter.cs` — roic 섹션 매핑 추가

---

## 5. Test Plan

| 테스트 ID | 설명 | 기대 결과 |
|----------|------|---------|
| PHY-T01 | 연속 2프레임 픽셀값 비교 | 프레임마다 달라야 함 (noise randomness) |
| PHY-T02 | dark frame 픽셀 분포 | N(μ_dark, σ_readout) 분포 확인 |
| PHY-T03 | bright frame 분산 vs 신호 | Var ∝ Signal (Poisson) 검증 |
| PHY-T04 | FPN map 프레임 불변성 | FPN map = frame[0] - frame[1]의 잔차가 일정 |
| PHY-T05 | Vback 증가 → 암전류 증가 | Vback=-30 > Vback=-15 암전류 지수 관계 |
| PHY-T06 | 온도 +10°C → 암전류 2배 | Idark(35°C) ≈ 2 × Idark(25°C) |
| PHY-T07 | Image lag 검증 | frame[n] += lag_coeff × frame[n-1] |

---

## 6. Acceptance Criteria

- [ ] PhysicsBased 패턴: 연속 프레임에서 픽셀값 분산 > 0 (랜덤 노이즈 확인)
- [ ] Dark frame 히스토그램: Gaussian 분포 (readout noise)
- [ ] Bright frame: 신호-분산 선형 관계 (Poisson 통계)
- [ ] FPN map이 seed 기반으로 재현 가능
- [ ] Vback -30V: 암전류가 -15V 대비 ≥ 4배 증가
- [ ] VGL 이탈 시 수직 스트라이프 아티팩트 시각적 확인
- [ ] detector_config.yaml `roic:` 섹션 로드 정상
- [ ] ParameterExtractor: VGL/VGH 추출 시도 후 결과 보고

---

## 7. References

- Siewerdsen JH et al., "Noise variance analysis using a flat panel x-ray detector" (PMC2902539)
- Zhao W et al., "A semiempirical linear model of indirect, flat-panel x-ray detectors" (PMC3326070)
- Schmitt CA et al., "Compound Poisson noise verification for X-ray flat panel imager" (IEEE 7294576)
- "A forward bias method for lag correction of an a-Si flat panel detector" (PMC3257750)
- "A nonlinear lag correction algorithm for a-Si flat-panel x-ray detectors" (PMC3465354)
- RP Photonics: Dark Current — https://www.rp-photonics.com/dark_current.html
- Radiology Key: TFT Flat-Panel Array Image Acquisition

---

## 8. Revision History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2026-03-18 | Initial — UltraThink deep research 결과 기반 |
