# X-ray Detector Panel System - Technology Stack

**상태**: ✅ 실제 사용 중인 기술 스택 (v2.1 계획 기반, 2026-03-16 현실 감사)
**생성**: 2026-02-17
**마지막 업데이트**: 2026-03-16

---

## Table of Contents

1. [하드웨어 플랫폼](#하드웨어-플랫폼)
2. [FPGA 개발](#fpga-개발)
3. [SoC 펌웨어 개발](#soc-펌웨어-개발)
4. [Host SDK 개발](#host-sdk-개발)
5. [개발자 도구](#개발자-도구)
6. [테스트 프레임워크](#테스트-프레임워크)
7. [빌드 시스템](#빌드-시스템)
8. [NuGet 의존성](#nuget-의존성)
9. [개발 방법론](#개발-방법론)
10. [품질 게이트](#품질-게이트)
11. [제약 사항 및 알려진 문제](#제약-사항-및-알려진-문제)

---

## 하드웨어 플랫폼

### 센서 패널 사양

**선택지별 비교**:

| 항목 | AUO R1717AS01.3 | AUO R1714AS08.0 | AUO R1717GH01 | Innolux X239AW1-102 |
|------|-----------------|-----------------|----------------|-------------------|
| 해상도 | 3072×3072 | 3072×2500 | 3072×3072 | 3072×3072 |
| 패널 유형 | a-Si TFT/PIN | a-Si TFT/PIN | IGZO TFT | a-Si TFT/PIN |
| 픽셀 피치 | 140 μm | 140 μm | 140 μm | 140 μm |
| 채우기율(FF) | 65% | 65% | 65% | 65% |
| 유리 두께 | 0.7 mm | 0.7 mm | 0.7 mm | 0.7 mm |
| **픽셀 정전기** | 1.48 pF | 1.48 pF | 1.48 pF | 1.48 pF |
| **피셀 누설(max)** | ≤3 fA | ≤3 fA | N/A | ≤3 fA |
| **TFT 누설(a-Si)** | ≤80 fA | ≤80 fA | - | ≤80 fA |
| **TFT 누설(IGZO)** | - | - | ≤10 fA | - |
| **Lag 1차(a-Si)** | ≤3% | ≤3% | - | ≤3% |
| **Lag 1차(IGZO)** | - | - | ≤5% @ 5μs gate-on | - |

**a-Si 누설 전류 보정 공식**:
- 픽셀 누설: I = (CNT_70 - CNT_10) × (e⁻/LSB) × 1.6×10⁻¹⁹ / ΔT
- TFT 누설: I = (CNT_10 - CNT_20) × (e⁻/LSB) × 1.6×10⁻¹⁹ / ΔT
- Lag(율): Lag = [Median(Fn) - Offset] / [Median(F0) - Offset] × 100%

**결함 판정 기준**:
- AUO IIS: Point defect > ±6σ (32×32 ROI), Line defect ≥4개 연속 픽셀
- Innolux CAS: Point defect ±15% of Panel Median, Cluster ≤6 in 3×3 window

### ROIC (Read-Out IC) 사양

| 항목 | AD71143 (ADI) | AFE2256GR (TI) | DDC3256 (TI) |
|------|-------------|--------------|------------|
| **채널 수** | 256 | 256 | 256 |
| **ADC 해상도** | 16-bit | 16-bit | 24-bit |
| **최소 Line Time** | 60 μs | 51.2 μs | 50 μs |
| **최대 충전(Qmax)** | 16.0 pC | 9.6 pC | 320 pC |
| **노이즈(rms)** | 580 e⁻ | 240 e⁻ | 0.26 fC |
| **INL** | ±2.5 LSB | ±2 LSB | ±0.025% |
| **CDS(샘플링)** | Yes | Yes (내장) | No |
| **출력 인터페이스** | LVDS serial | LVDS DDR | LVDS serial |
| **패키지** | SOF (flex) | COF (flex) | BGA |

**v2.1 선택 근거**:
- **프라이머리**: AFE2256GR (TI) — Line Time 51.2 μs, 최소 노이즈 240 e⁻
- **백업**: AD71143 (ADI) — 안정성, 580 e⁻ 노이즈 수용 가능
- **Premium Tier**: DDC3256 (TI) — 낮은 노이즈지만 프로토콜 복잡도 높음

### Gate IC

**NT39522DH (Novatek)**:
- 채널: 512ch 기준 (541ch/513ch/385ch/361ch 가능)
- VGG 범위: VCC ~ VEE+40V
- 최대 Clock: 200 kHz
- 출력 Rise/Fall: ≤500/400 ns
- 패키지: COF (flex)
- **구성**: 6개 COF × 512ch = 3072 gate lines (3072×3072 패널 커버)

### FPGA

**Device**: Xilinx Artix-7 XC7A35T-FGG484 (확정, 변경 불가)

| 리소스 | 용량 | 설계 목표 사용률 |
|--------|------|----------------|
| Logic Cells | 33,280 | - |
| LUTs (6-input) | 20,800 | <60% (<12,480) |
| Flip-Flops | 41,600 | - |
| BRAMs (36Kbit) | 50 (총 1.8Mbit) | - |
| DSP Slices | 90 | - |

**구현된 RTL 모듈 (SystemVerilog)**:
- panel_scan_fsm, line_buffer, csi2_tx_wrapper, spi_slave, protection_logic
- Top-level: csi2_detector_top.sv

### SoC

**Device**: NXP i.MX8M Plus (확정)
- **CPU**: ARM Cortex-A53 quad-core (aarch64)
- **OS**: Linux 6.6.52 (Yocto Scarthgap 5.0 LTS)
- **CSI-2 RX**: V4L2 드라이버
- **SPI**: spidev 드라이버
- **10GbE**: 하드웨어 MAC/PHY (또는 2.5GbE)

---

## FPGA 개발

| 항목 | 기술 / 도구 |
|------|------------|
| HDL 언어 | SystemVerilog (IEEE 1800-2012) |
| 합성/구현 도구 | Xilinx Vivado 2023.x 이상 |
| 시뮬레이터 | ModelSim / Vivado Simulator |
| 테스트벤치 | SystemVerilog testbench |
| 제약 파일 | XDC (Xilinx Design Constraints) |
| CSI-2 IP | AMD/Xilinx MIPI CSI-2 TX Subsystem IP |
| 생성 도구 | CodeGenerator CLI (tools/ → generated/) |

**D-PHY 성능 파라미터**:
- Lane 속도: ~1.0-1.25 Gbps/lane (Artix-7 OSERDES 한계)
- 4-lane aggregate: ~4-5 Gbps raw
- CSI-2 프로토콜 오버헤드: ~20-30%

---

## SoC 펌웨어 개발

| 항목 | 기술 / 도구 |
|------|------------|
| 언어 | C11 |
| 컴파일러 | GCC aarch64-linux-gnu |
| 빌드 시스템 | CMake 3.20+ |
| Yocto 버전 | Scarthgap 5.0 LTS |
| Linux 커널 | 6.6.52 LTS |
| CSI-2 인터페이스 | V4L2 Media Subsystem |
| SPI 인터페이스 | spidev 커널 드라이버 |
| 이더넷 | 10GbE UDP (raw socket) 또는 2.5GbE |
| 인증 | HMAC-SHA256 (명령 프로토콜) |
| 테스트 프레임워크 | Unity Test Framework (C) |

**Yocto 레이어**: meta-detector
- collection: detector, priority: 10
- LAYERCOMPAT: scarthgap (5.0 LTS 호환)
- 레시피: detector-daemon v1.0.0 (CMake + systemd), detector-image (256MB rootfs)

---

## Host SDK 개발

| 항목 | 기술 / 도구 |
|------|------------|
| 언어 | C# 12.0 |
| 런타임 | .NET 8.0 LTS |
| 핵심 NuGet | System.IO.Pipelines (Microsoft) |
| DICOM 라이브러리 | fo-dicom 5.1.0 |
| 비동기 패턴 | IAsyncEnumerable, async/await |
| 스트리밍 API | IDetectorClient (event-driven) |
| 이미지 처리 | WindowLevelMapper (16-bit → 8-bit) |
| 인코딩 지원 | TIFF, RAW, DICOM (XRayAngiographicImageStorage) |
| CRC 검증 | CRC-16 (프레임 무결성) |

### DICOM 구현 상세 (fo-dicom 5.1.0)

| 항목 | 구현 내용 |
|------|---------|
| SOP Class | XRayAngiographicImageStorage |
| 픽셀 데이터 | 16-bit big-endian 그레이스케일 |
| DICOM 모듈 수 | 7개 (Patient, Study, Series, Equipment, Image Pixel, VOI LUT, SOP Common) |
| UID 생성 규칙 | `2.25.<timestamp>.<random>` |
| 테스트 케이스 | 12개 (기본값, 커스텀 메타데이터, 대용량 프레임, 경계 조건) |

---

## 개발자 도구

### WPF 도구 (net8.0-windows)

| 도구 | 타겟 프레임워크 | 주요 의존성 |
|------|---------------|------------|
| GUI.Application | net8.0-windows | CommunityToolkit.Mvvm, Serilog |
| ParameterExtractor.Wpf | net8.0-windows | iTextSharp (AGPL⚠️), YamlDotNet, Serilog |

> ⚠️ **라이선스**: iTextSharp는 AGPL 라이선스. 상업적 배포 시 라이선스 준수 필요.

### CLI 도구 (net8.0)

| 도구 | 주요 의존성 | 역할 |
|------|------------|------|
| CodeGenerator.Cli | System.CommandLine, YamlDotNet | YAML → RTL/C/C# 생성 |
| ConfigConverter.Cli | YamlDotNet | YAML → JSON/DTS/XDC |
| IntegrationRunner.Cli | System.CommandLine | HIL 시나리오 조율 |

### 파라미터 추출 도구 (v2.1)

**CalibrationFitter** (M8/W32 계획):
- 입력: Dark/Bright 이미지 (16-bit binary 또는 TIFF)
- 출력: 보정된 detector_config.yaml + calibration_report.json
- 피팅 목표: Dark current, readout offset, gain map, defect map, scintillator 비균일
- 목표: RMSE ≤ 2 LSB (보정 후)

### 시뮬레이터 (net8.0)

| 시뮬레이터 | 소스 파일 | 역할 |
|-----------|---------|------|
| FpgaSimulator.Core | 18개 | CSI-2 TX, SPI slave, 라인 버퍼 에뮬레이션 |
| PanelSimulator.Core | 7개 | 노이즈/게인/오프셋 X-ray 패널 모델 |
| McuSimulator.Core | 4개 | CSI-2 RX, 4-buffer ring, UDP endpoint |
| HostSimulator.Core | 8개 | SDK 통합 테스트 하네스 |
| Common.Dto | 6개 | 공유 DTO 허브 (의존성 없음) |

**PanelSimulator 업데이트 (v2.1)**:
- 기존 노이즈 모델 → 실측 파라미터 기반
  - Pixel Capacitance: 1.48 pF
  - Pixel Leakage: ≤3 fA/pixel
  - TFT Leakage: ≤80 fA (a-Si), ≤10 fA (IGZO)
  - Lag (1st frame): ≤3% (a-Si), ≤5% (IGZO @ 5μs gate-on)
  - ROIC Noise: 580~1000 e⁻ (AD71143), 240~1050 e⁻ (AFE2256GR)
  - ROIC INL: ±2~2.5 LSB
  - ROIC Crosstalk: 0.01~0.07%
- Dual TFT 모델 지원:
  - a-Si TFT/PIN diode: 표준 간접 FPD 모델
  - IGZO TFT: 낮은 누설 모델 (AUO R1717GH01)

---

## 테스트 프레임워크

### C# 테스트 스택

| 라이브러리 | 버전 | 역할 |
|-----------|------|------|
| xUnit | 2.9.0 | 테스트 프레임워크 |
| Moq | 4.20.70 | Mock 객체 생성 |
| FluentAssertions | 최신 | Assertion 가독성 향상 |
| coverlet | 최신 | 코드 커버리지 측정 |

> **주의**: IntegrationTests 프로젝트와 Sdk.Tests 프로젝트 간 테스트 프레임워크 버전 불일치 존재. 통일 권장.

### 테스트 현황

| 컴포넌트 | 테스트 파일 | 통과율 |
|---------|-----------|--------|
| XrayDetector.Sdk | 16개 | ✅ |
| DicomEncoder | 1개 (12 케이스) | ✅ |
| FpgaSimulator | 5개 | ✅ |
| PanelSimulator | 5개 | ✅ |
| McuSimulator | 4개 | ✅ |
| HostSimulator | 6개 | ✅ |
| Common.Dto | 6개 | ✅ |
| CodeGenerator | 9개 | ✅ |
| ConfigConverter | 42개 중 37개 | 🔶 (5개 미통과) |
| GUI.Application | 40개 | ✅ |
| ParameterExtractor | 41개 | ✅ |
| **합계** | **50+개 파일** | **대부분 통과** |

### RTL/FW 테스트

| 구분 | 도구 | 파일 수 |
|------|------|--------|
| FPGA 테스트벤치 | SystemVerilog + Vivado | 6개 (모듈별 + 통합) |
| 펌웨어 단위 테스트 | Unity (C) + V4L2/spidev mock | 10개 + 통합 |

---

## 빌드 시스템

### C# (.NET 8.0)

```bash
# 전체 빌드
dotnet build

# 전체 테스트
dotnet test --collect:"XPlat Code Coverage"

# 코드 커버리지 리포트
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coverage
```

### Yocto (SoC Firmware)

```bash
source poky/oe-init-build-env build-detector
bitbake detector-image                   # 전체 이미지 빌드
bitbake detector-daemon                  # 데몬만 빌드
bitbake -c devshell detector-daemon     # 개발 쉘 진입
```

**Yocto 빌드 출력물**:
- `detector-image-imx8mpevk.wic.zst` — eMMC/SD 이미지
- `detector-daemon_1.0.0-r0.aarch64.rpm` — 데몬 패키지

### FPGA (Vivado)

```bash
vivado -mode batch -source scripts/build.tcl

# 빌드 출력물:
# csi2_detector_top.bit  — FPGA 비트스트림
# csi2_detector_top.ltx  — ILA 디버그 프로브
# reports/utilization.rpt
# reports/timing.rpt
```

---

## NuGet 의존성

### 프로덕션 의존성

| 패키지 | 버전 | 사용 프로젝트 | 역할 |
|--------|------|-------------|------|
| System.IO.Pipelines | Microsoft.NETCore.App 포함 | XrayDetector.Sdk | 고성능 I/O 파이프라인 |
| fo-dicom | 5.1.0 | XrayDetector.Sdk | DICOM 인코딩/디코딩 |
| YamlDotNet | 최신 | CodeGenerator, ConfigConverter, ParameterExtractor | YAML 파싱 |
| System.CommandLine | 최신 | CodeGenerator, IntegrationRunner | CLI 파라미터 처리 |
| CommunityToolkit.Mvvm | 최신 | GUI.Application | MVVM 패턴 |
| iTextSharp | 최신 (AGPL) | ParameterExtractor | PDF 텍스트 추출 |
| Serilog | 최신 | GUI.Application, ParameterExtractor | 구조화 로깅 |

### 테스트 의존성

| 패키지 | 버전 | 역할 |
|--------|------|------|
| xunit | 2.9.0 | 테스트 프레임워크 |
| xunit.runner.visualstudio | 최신 | VS 통합 |
| Moq | 4.20.70 | Mock 객체 |
| FluentAssertions | 최신 | 가독성 높은 assertion |
| coverlet.collector | 최신 | 커버리지 수집 |

---

## 개발 방법론

**설정 파일**: `.moai/config/sections/quality.yaml`
**모드**: `hybrid` (Hybrid TDD + DDD)

### Hybrid 모드 규칙

| 코드 유형 | 방법론 | 사이클 |
|---------|--------|-------|
| 신규 모듈/기능 | TDD | RED → GREEN → REFACTOR |
| 레거시 코드 수정 | DDD | ANALYZE → PRESERVE → IMPROVE |
| 신규 파일 내 신규 함수 | TDD | 테스트 먼저 작성 |
| 기존 파일 함수 수정 | DDD | 특성화 테스트 먼저 |

### 커버리지 목표

| 구분 | 목표 |
|------|------|
| 신규 코드 | 85%+ |
| 레거시 코드 | 85%+ |
| RTL (라인) | ≥95% |
| RTL (브랜치) | ≥90% |
| RTL (FSM) | 100% |

---

## 품질 게이트

### TRUST 5 Framework

| 게이트 | 기준 | 도구 |
|--------|------|------|
| **Tested** | 85%+ 커버리지, xUnit 전체 통과, LSP 타입 에러 0 | xUnit, coverlet |
| **Readable** | 네이밍 규칙, 영문 주석, LSP lint 에러 0 | .editorconfig, Roslyn |
| **Unified** | 일관된 스타일, CommunityToolkit.Mvvm MVVM | .editorconfig |
| **Secured** | OWASP 준수, HMAC-SHA256 인증, 시크릿 미포함 | 코드 리뷰, SAST |
| **Trackable** | Conventional commits, SPEC 이슈 참조 | git log |

### LSP 품질 게이트 (C# Roslyn)

| 단계 | 기준 |
|------|------|
| Plan | LSP 기준선 캡처 |
| Run | 에러 0, 타입 에러 0, lint 에러 0 |
| Sync | 에러 0, 경고 최대 10, 깨끗한 LSP 상태 |

---

## 버전 관리

| 항목 | 내용 |
|------|------|
| VCS | Git (단일 저장소) |
| 브랜치 전략 | main 브랜치 + feature 브랜치 |
| 커밋 규칙 | Conventional Commits |
| SPEC 연동 | 커밋 메시지에 SPEC ID 참조 |

---

## 네트워크 대역폭 분석 (v2.1)

### 실제 데이터율 요구사항

| 해상도 | ROIC | FPS | 데이터율 | 권장 이더넷 |
|--------|------|-----|---------|-----------|
| 1024×1024 | 모두 | 19.1 | 0.32 Gbps | 1GbE ✅ |
| 2048×2048 | 모두 | 9.5 | 0.64 Gbps | 1GbE ✅ |
| 3072×3072 | AD71143 | 5.4 | 0.82 Gbps | 1GbE ✅ |
| 3072×3072 | AFE2256GR | 6.4 | 0.96 Gbps | 1GbE ⚠️ / 2.5GbE ✅ |

**v2.0 목표 재확정**:
- v2.0의 3072×3072@30fps 목표는 ROIC Line Time 물리적 한계로 불가능
- v2.1에서 ROIC 병목 기반 최대 6.4fps (AFE2256GR)로 재확정
- 대부분 애플리케이션에서 1GbE 충분 (3072×3072+AFE2256GR 제외)

---

## 제약 사항 및 알려진 문제

### 알려진 기술 부채

1. **IntegrationTests xUnit 버전 불일치**: Sdk.Tests와 다른 버전 사용 — 통일 필요
2. **ConfigConverter 미통과 테스트**: 42개 중 5개 실패 — M3-Integ 전 수정 필요
3. **iTextSharp AGPL 라이선스**: 상업적 배포 시 주의 필요
4. **패널 해상도 불일치**: ARCHITECTURE.md(3072×3072) vs detector_config.yaml(2048×2048) — 문서 동기화 필요
5. **펌웨어 레시피 버전 중복**: fw/deploy/detector-daemon_1.0.bb (구형) vs meta-detector/detector-daemon_1.0.0.bb (현재) — 구형 레시피 정리 필요

### FPGA 기술 제약

- **USB 3.x 불가**: IP 코어가 Artix-7 35T LUT 용량 72-120% 필요 — 구현 불가
- **D-PHY 속도 한계**: Artix-7 OSERDES 최대 1.25 Gbps/lane (D-PHY v2.5 최대값 아님)

### 이더넷 요구사항 수정 (v2.1)

- v2.0: "Maximum Tier 위험: 4.53 Gbps 요구" — **제거됨** (잘못된 30fps 가정)
- v2.1: 1GbE로 대부분 커버 가능 (3072×3072+AFE2256GR의 0.96Gbps 제외, 이 경우 2.5GbE 권장)

---

**문서 종료**

*마지막 업데이트: 2026-03-16. v2.1 계획 기반 기술 스택 반영.*
