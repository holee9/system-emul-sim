# X-ray Detector Panel System - Product Overview

**Status**: 🔄 SPEC-GUI-001 진행 중 (M2-Impl 완료, GUI 파이프라인 연결)
**Generated**: 2026-02-17
**Last Updated**: 2026-03-16
**Methodology**: Hybrid TDD/DDD (abyz-lab 개발 표준)

---

## Project Identity

**Name**: X-ray Detector Panel System
**Tagline**: Medical Imaging Grade Data Acquisition and Processing Platform
**Mission**: Deliver a production-grade, layered system for real-time X-ray detector panel control, data acquisition, and image processing for medical imaging equipment OEMs

**Project Type**: Research & Development System (Not a commercial product; platform for medical imaging equipment development)

**Development Timeline**: 34주 (8.5개월) — v2.1 분석 기반 재검토
**Current Phase**: SPEC-GUI-001 진행 중 — M2-Impl(SW 100%) 완료, GUI 파이프라인 연결 시작

---

## Current Implementation Status

### Milestone Progress

| Milestone | Status | 완료 내용 |
|-----------|--------|----------|
| M0 (Architecture) | ✅ 완료 | 아키텍처 확정, 성능 티어 결정, 3-tier 설계 |
| M0.5 (PoC) | ✅ 완료 | SPEC-POC-001, 시뮬레이터 프레임워크 구축 |
| M1 (Core Impl) | ✅ 완료 | SDK, FpgaSimulator, McuSimulator, PanelSimulator 구현 |
| M2-Impl (SW Complete) | ✅ 완료 | 18개 C# 프로젝트, 50+ 테스트 파일, 85%+ 커버리지 |
| M3-Integ (Integration) | 🔜 진행 예정 | 실 하드웨어 HIL 통합 테스트 |
| M4 (Performance) | ⬜ 미시작 | 성능 검증 — v2.1 업데이트된 목표값 |
| M5 (Validation) | ⬜ 미시작 | TRUST 5 완전 준수, 문서 완비 |
| M6 (Pilot) | ⬜ 미시작 | 파일럿 배포 |
| M7 (Param Extractor) | ⬜ 미시작 | 파라미터 추출기 미세 조정 (≥80% 자동 추출) |
| M8 (Real Measurement Cal) | ⬜ 미시작 | 실측 캘리브레이션 (RMSE ≤ 2 LSB) |
| M9 (Final Validation + RC) | ⬜ 미시작 | 최종 검증 및 RC 릴리스 |

### SW 구현 완료 현황 (M2-Impl)

**SDK (XrayDetector.Sdk)**:
- 소스 파일 21개 (Communication, Reassembly, Processing, Discovery, Implementation)
- 테스트 파일 16개 (xUnit + Moq + FluentAssertions)
- DICOM 인코딩 완료 (fo-dicom 5.1.0, 12개 테스트)
- IDetectorClient: async, event-driven, IAsyncEnumerable streaming

**시뮬레이터 (tools/)**:
- FpgaSimulator: 18개 소스 + 5개 테스트 (CSI-2 TX, SPI slave, line buffer 에뮬레이션)
- PanelSimulator: 7개 소스 + 5개 테스트 (노이즈/게인/오프셋 설정 가능)
- McuSimulator: 4개 소스 + 4개 테스트 (CSI-2 RX, 4-buffer ring, UDP fragmentation)
- HostSimulator: 8개 소스 + 6개 테스트 (SDK 통합 테스트 하네스)
- Common.Dto: 6개 소스 + 6개 테스트 (공유 DTO 허브)

**개발자 도구 (tools/)**:
- ParameterExtractor (WPF, net8.0-windows): 벤더 PDF 파라미터 추출 GUI (신규: 3-input 아키텍처)
- GUI.Application (WPF, net8.0-windows): SDK 통합 기본 GUI (SPEC-GUI-001 진행 중)
- CodeGenerator (CLI): detector_config.yaml → RTL/C header/C# 코드 생성
- ConfigConverter (CLI): 설정 포맷 변환 (YAML → JSON/DTS/XDC)
- IntegrationRunner (CLI): 멀티 시뮬레이터 HIL 테스트 조율

**펌웨어 (fw/)**:
- Yocto Scarthgap 5.0 LTS, Linux 6.6.52, NXP i.MX8M Plus (aarch64)
- meta-detector Yocto 레이어: detector-daemon v1.0.0, detector-image (256MB rootfs)
- TDD Wave 2~5 구현 완료: CSI-2 RX(V4L2), SPI Master(spidev), 10GbE UDP TX, HMAC-SHA256 커맨드 프로토콜, Sequence Engine(6-state FSM), Frame Manager(4-buffer ring), Health Monitor

**FPGA RTL (fpga/)**:
- SystemVerilog RTL 5개 모듈: panel_scan_fsm, line_buffer, csi2_tx_wrapper, spi_slave, protection_logic
- Top-level: csi2_detector_top.sv (Xilinx Artix-7 XC7A35T-FGG484)
- SPEC-FPGA-001 완전 구현 완료

**설정 및 생성 코드**:
- config/detector_config.yaml: 마스터 설정 (2048×2048, CSI-2 4-lane, SPI 50MHz, 10GbE UDP:8000)
- generated/: CodeGenerator 출력물 — fpga_registers.h, line_buffer.sv, panel_scan_fsm.sv, DetectorConfig.g.cs, FrameHeader.g.cs

---

## Core Purpose

The X-ray Detector Panel System is a comprehensive hardware and software platform designed to:

1. **Real-time Control**: Interface with X-ray detector panels via ROIC (Readout Integrated Circuit) for synchronized image capture
2. **High-Speed Data Acquisition**: Capture pixel data at rates up to 0.96 Gbps (Maximum tier: 3072×3072 + AFE2256GR) with deterministic latency
3. **Efficient Data Transport**: Stream image frames from FPGA → SoC → Host PC with minimal overhead
4. **Flexible Configuration**: Support multiple detector resolutions (1024×1024 to 3072×3072), bit depths (14-16 bit), and frame rates (6-19 fps depending on configuration)
5. **Development Acceleration**: Provide simulation environment and code generation tools to accelerate medical imaging device development
6. **DICOM Support**: Medical imaging standard compliance via fo-dicom 5.1.0 (XRayAngiographicImageStorage)

**Primary Use Cases**:
- Medical X-ray imaging systems (radiography, fluoroscopy, mammography)
- Detector panel characterization and testing
- Image processing algorithm development
- System integration for medical equipment OEMs

---

## System Architecture

### High-Level Data Flow

```
[X-ray Detector Panel] ──(Analog)──> [ROIC] ──(Parallel Digital)──> [FPGA Artix-7]
                                                                              │
                                                                              │ CSI-2 MIPI
                                                                              │ 4-lane D-PHY
                                                                              ↓
                                                                         [SoC i.MX8M Plus]
                                                                              │
                                                                              │ 1GbE/2.5GbE UDP (port 8000)
                                                                              ↓
                                                                         [Host PC / SDK]
                                                                              ↑
                                                                              │ HMAC-SHA256 Command (port 8001)
                                                                         [SoC i.MX8M Plus]
                                                                              ↑
                                                                              │ SPI Master (50MHz)
                                                                         [FPGA Artix-7]
```

### Component Roles

**FPGA (Xilinx Artix-7 XC7A35T-FGG484)** — *구현 완료*:
- Panel scan sequencing (panel_scan_fsm — 6-state FSM)
- Line buffering (line_buffer — dual-port BRAM)
- CSI-2 MIPI D-PHY TX 4-lane (csi2_tx_wrapper)
- SPI slave for Host control (spi_slave)
- Protection logic: 과열/타이밍 위반 감지 (protection_logic)

**SoC (NXP i.MX8M Plus, Linux 6.6.52 / Yocto Scarthgap 5.0 LTS)** — *알파 개발 중*:
- CSI-2 RX (V4L2 드라이버)
- Frame Manager (4-buffer ring)
- 1GbE/2.5GbE UDP TX (port 8000) — 프레임 데이터 스트리밍
- HMAC-SHA256 Command Protocol (port 8001) — 제어 명령
- Sequence Engine (6-state FSM)
- Health Monitor

**Host PC / SDK (.NET 8.0)** — *구현 완료*:
- UDP 패킷 수신 및 프레임 재조립 (CRC-16 검증)
- 이미지 처리: Window/Level 매핑, TIFF/RAW/DICOM 인코딩
- IDetectorClient: async, event-driven, IAsyncEnumerable streaming
- DICOM XRayAngiographicImageStorage (fo-dicom 5.1.0, 7 DICOM 모듈)

### Hardware Components (실제 사양)

**ROIC(Readout IC) 옵션** (프로젝트에 따라 선택):
- **AD71143** (ADI): 256ch, 16-bit, 60μs/line, 580 e⁻ RMS 노이즈
- **AFE2256GR** (TI): 256ch, 16-bit, 51.2μs/line, 240 e⁻ RMS 노이즈 (낮은 노이즈)
- **DDC3256** (TI): 256ch, 24-bit, 50μs/line, CT-grade

**Gate IC**: NT39522DH (Novatek), 541ch, VGG-VEE=40V, 200kHz max clock, COF 패키지
- 6개 칩 × 512ch 모드 = 3072 게이트 라인

**패널 벤더 지원**:
- **AUO**: R1717AS01.3 (3072×3072, a-Si), R1714AS08.0 (3072×2500, a-Si), R1717GH01 (IGZO)
- **Innolux**: X239AW1-102 (3072×3072, a-Si)
- 픽셀 피치: 140μm, Fill factor: 65%

### Key Architectural Decisions

1. **CSI-2 as Primary Data Path**: MIPI CSI-2 4-lane D-PHY chosen as FPGA↔SoC interface (FPGA resource constraint)
2. **USB 3.x Exclusion**: USB 3.x IP cores require 72-120% of Artix-7 35T LUT capacity — IMPOSSIBLE
3. **Ethernet for Host Link**: 1GbE는 대부분의 설정에 충분; 3072×3072+AFE2256GR (0.96Gbps)는 2.5GbE 권장
4. **Single Configuration Source**: `detector_config.yaml` → CodeGenerator → FPGA/SoC/Host 설정 파일 자동 생성
5. **HMAC-SHA256 Command Auth**: 명령 프로토콜 무결성 보장 (포트 8001)

---

## Performance Envelope (v2.1 수정됨)

| Configuration | Resolution | ROIC | Frame Rate | Data Rate | Network Req |
|--------------|------------|------|-----------|-----------|------------|
| **Minimum** | 1024×1024 | AFE2256GR | 19.1 fps | 0.32 Gbps | 1GbE |
| **Target-a** | 2048×2048 | AFE2256GR | 9.5 fps | 0.64 Gbps | 1GbE |
| **Target-b** | 3072×2500 | AFE2256GR | 6.4 fps | 0.78 Gbps | 1GbE |
| **Maximum-a** | 3072×3072 | AFE2256GR | 6.4 fps | 0.96 Gbps | 2.5GbE |
| **Maximum-b** | 3072×3072 | AD71143 | 5.4 fps | 0.82 Gbps | 1GbE |

**ROIC Line Time 병목**: 실제 프레임 레이트는 ROIC 라인 읽기 시간으로 제한됨 (AFE2256GR: 51.2μs/line, AD71143: 60μs/line)

**현재 설정** (detector_config.yaml): 2048×2048, AFE2256GR, CSI-2 4-lane 400Mbps, SPI 50MHz, 10GbE UDP port 8000 → 실제 성능: 약 9.5 fps, 0.64 Gbps

**주의**: 30fps는 ROIC 라인 타이밍 제약으로 물리적으로 불가능 (빈닝 또는 ROI 없이)

---

## Key Features

### 1. Layered Architecture
- **Hardware Abstraction**: FPGA RTL abstracts ROIC timing; SoC firmware abstracts CSI-2 and Ethernet
- **Clean Interfaces**: Well-defined API boundaries between FPGA/SoC/Host layers
- **Testability**: Each layer independently testable via C# simulators

### 2. Real-Time Panel Control
- **Deterministic Timing**: FPGA generates pixel-accurate scan sequences with <10 ns jitter
- **Synchronization**: Frame trigger, exposure control, and readout timing coordinated
- **Protection Logic**: 과열 모니터링, 타이밍 위반 감지, 비상 종료 경로

### 3. High-Speed Data Path
- **CSI-2 Streaming**: 4-lane MIPI D-PHY (Artix-7 OSERDES, ~1.0-1.25 Gbps/lane)
- **Zero-Copy Design**: SoC firmware DMA를 통한 CPU 오버헤드 최소화
- **Ethernet Offload**: 1GbE/2.5GbE 하드웨어 체크섬 및 scatter-gather DMA

### 4. Comprehensive Simulation Environment (구현 완료)
- **PanelSimulator**: X-ray 패널 아날로그 출력 모델 (dual TFT: a-Si 고 누설 vs IGZO 저 누설)
- **FpgaSimulator**: FPGA 로직 동작 모델 (C# .NET 8.0)
- **McuSimulator**: SoC 펌웨어 에뮬레이션 (CSI-2 RX, Ethernet 엔드포인트)
- **HostSimulator**: Host SDK 통합 테스트 하네스
- **IntegrationTests**: 4개 시뮬레이터 전체 통합 (HIL 패턴)

### 5. Single Configuration Source (구현 완료)
- **detector_config.yaml**: 패널 지오메트리, 타이밍, 인터페이스 파라미터
- **CodeGenerator**: YAML → RTL(.sv), C header(.h), C#(.g.cs), DTS, XDC 자동 생성
- **generated/** 검증: TestSdkCompilation.csproj로 컴파일 검증 완료

### 6. DICOM Medical Imaging Support (신규 구현)
- **DicomEncoder**: fo-dicom 5.1.0 기반, XRayAngiographicImageStorage
- **7 DICOM 모듈**: Patient, Study, Series, Equipment, Image Pixel 등
- **UID 생성**: DICOM 표준 준수 (2.25.\<timestamp\>.\<random\>)
- **16-bit Big-Endian 그레이스케일 인코딩**

### 7. Developer Tooling (구현 완료)
- **ParameterExtractor** (WPF): 3개 독립 입력 → 3개 파서 → 병합 & 검증 → detector_config.yaml
- **ConfigConverter** (CLI): YAML → JSON/DTS/XDC 변환
- **CodeGenerator** (CLI): 반복 RTL 블록 및 보일러플레이트 코드 생성
- **IntegrationRunner** (CLI): 멀티 레이어 HIL 시나리오 자동 테스트 조율
- **GUI.Application** (WPF): SDK 통합 기본 GUI (SPEC-GUI-001 진행 중)

### 8. Real Measurement Calibration Pipeline (신규 — M8)
- **Dark/Bright 실측 이미지 입력**: 통계 분석기로 처리
- **현재 Sim 파라미터**: Calibration Fitter와 병합
- **출력**: 캘리브레이션된 파라미터 + calibration_report.json (RMSE ≤ 2 LSB 목표)

---

## SPEC Document Status

| SPEC ID | 주제 | 상태 |
|---------|------|------|
| SPEC-ARCH-001 | System Architecture | ✅ 완료 |
| SPEC-FPGA-001 | FPGA RTL Design | ✅ 완료 |
| SPEC-FW-001 | SoC Firmware | ✅ 완료 |
| SPEC-POC-001 | Proof of Concept | ✅ 완료 |
| SPEC-SDK-001 | Host SDK | ✅ 완료 |
| SPEC-SIM-001 | Simulation Framework | ✅ 완료 |
| SPEC-TOOLS-001 | Developer Tools | ✅ 완료 |
| SPEC-GUI-001 | GUI Integration (MVP) | 🔄 진행 중 |

---

## Quality Strategy

### Development Methodology: Hybrid (TDD + DDD)

**New Code (TDD — RED-GREEN-REFACTOR)**:
- 신규 SDK 모듈, 시뮬레이터, 개발 도구

**Existing Code (DDD — ANALYZE-PRESERVE-IMPROVE)**:
- FPGA RTL, SoC 펌웨어 HAL 수정 시

### Coverage Targets (달성 현황)

- **SW 전체**: 85%+ 달성 (xUnit 2.9.0, coverlet)
- **SDK**: 16개 테스트 파일, DicomEncoder 12개 테스트
- **시뮬레이터**: 각 5~6개 테스트 파일
- **총 테스트 파일**: 50+개

### TRUST 5 Framework

- **Tested**: 85%+ coverage, characterization tests for existing code
- **Readable**: Clear naming, English comments
- **Unified**: 일관된 스타일, xUnit/Moq/FluentAssertions
- **Secured**: HMAC-SHA256 명령 인증, OWASP 준수
- **Trackable**: Conventional commits, SPEC 이슈 참조

---

## Core Constraints

### FPGA Resource Budget

**Device**: Xilinx Artix-7 XC7A35T-FGG484
**Resources**: LUTs 20,800 / FFs 41,600 / BRAMs 50 / DSP 90

**Target Utilization**: <60% LUTs (<12,480 LUTs)

**Implemented RTL Modules**:
- panel_scan_fsm, line_buffer, csi2_tx_wrapper, spi_slave, protection_logic
- Top-level: csi2_detector_top.sv

### D-PHY Bandwidth Ceiling
- Artix-7 OSERDES: ~1.0-1.25 Gbps/lane (하드웨어 한계)
- 4-lane aggregate: ~4-5 Gbps raw

---

## Target Users

### Primary Audience
1. **Medical Equipment OEMs**: X-ray 영상 시스템 개발 회사
2. **Detector Manufacturers**: 커스텀 패널 통합 벤더
3. **Research Institutions**: 의료 영상 알고리즘 연구 기관

### User Roles
- **System Architect**: 시스템 요구사항 정의, 컴포넌트 선택
- **FPGA Developer**: RTL 구현, 합성, 타이밍/리소스 검증
- **Firmware Developer**: SoC 펌웨어 (C/C++), CSI-2 및 Ethernet 드라이버
- **Software Developer**: Host SDK (C#), GUI 도구, 통합 테스트
- **Test Engineer**: HIL 테스트 시나리오, 성능 검증

---

## Development Timeline (34주 — v2.1 업데이트)

### Phase Overview

| Phase | 주차 | Milestone | Focus | Status |
|-------|-----|-----------|-------|--------|
| P0 | W1 | M0 | Requirements & Architecture | ✅ 완료 |
| P1 | W2-W6 | M0.5 | Foundation & PoC | ✅ 완료 |
| P2 | W7-W14 | M1-M2 | Core Implementation (SW) | ✅ 완료 (M2-Impl) |
| P3 | W15-W18 | M3 | Integration & HIL Testing | 🔜 진행 예정 |
| P4 | W19-W21 | M4 | Performance Optimization | ⬜ 미시작 |
| P5 | W22-W24 | M5 | Validation & Documentation | ⬜ 미시작 |
| P6 | W25-W27 | M6 | Pilot Deployment | ⬜ 미시작 |
| P7 | W28 | M6+ | Handoff & Transition | ⬜ 미시작 |
| P8 | W29-W30 | M7 | Parameter Extractor Fine-tuning | ⬜ 미시작 |
| P9 | W31-W32 | M8 | Real Measurement Calibration | ⬜ 미시작 |
| P10 | W33-W34 | M9 | Final Validation + RC | ⬜ 미시작 |

### Milestone Details (Extended)

| M7 | W30 | 파라미터 추출기 미세 조정 완료; 5개 spec PDF, ≥80% 자동 추출율 |
| M8 | W32 | 실측 캘리브레이션: Dark/Bright fitting, RMSE ≤ 2 LSB |
| M9 | W34 | 최종 검증 + Release Candidate |

---

## ParameterExtractor Architecture (v2.1 업데이트)

### 3-Input, 3-Parser, Merge & Validate Architecture

```
[Panel PDF]   → [Panel Parser]   → [Panel Template Matcher]   → panel_params.json
[Gate IC PDF] → [Gate Parser]    → [Gate Template Matcher]    → gate_ic_params.json
[ROIC PDF]    → [ROIC Parser]    → [ROIC Template Matcher]    → roic_params.json
                                                                      ↓
                                                           [Merge & Validate]
                                                                      ↓
                                                          detector_config.yaml
```

**미세 조정 사이클**: 5개 spec PDF 대상, ≥80% 자동 추출률 달성 목표 (M7, W30)

---

## PanelSimulator Enhanced Model (v2.1)

### Dual TFT Model
- **a-Si (아몰퍼스 실리콘)**: 고 누설 (≤80 fA/pixel), 1프레임 lag ≤3%
- **IGZO**: 저 누설 (≤10 fA/pixel), lag ≤5%

### Real Noise Parameters
- 픽셀 용량: 1.48pF
- 픽셀 누설: ≤3 fA
- TFT 누설 (a-Si): ≤80 fA

### ROIC Noise Model
- **AD71143**: 580-1000 e⁻ RMS
- **AFE2256GR**: 240-1050 e⁻ RMS

### Dual Defect Standard
- **AUO IIS**: ±6σ point defect
- **Innolux CAS**: ±15% of median

---

## Future Roadmap

### Next Steps (M3-Integ)
1. **실 하드웨어 HIL 테스트**: Artix-7 dev board + i.MX8M Plus eval board 연결
2. **Minimum Tier 검증**: 1024×1024@19.1fps end-to-end (<1% 프레임 손실)
3. **통합 테스트 시나리오**: IT-01~IT-10 실행
4. **SPEC-INTEG-001 작성**: 통합 테스트 명세 문서화

### SPEC-GUI-001: Real Pipeline Connection (현재)
1. **MVP-1 (Priority NOW)**: App.xaml.cs에서 SimulatedDetectorClient → PipelineDetectorClient 변경
2. **MVP-2**: PipelineStatusViewModel.OnPollingTick() 연결, 실시간 통계
3. **MVP-3**: 실제 Scenario 실행 (IT-01, IT-08, IT-09)
4. **MVP-4**: PDF 파라미터 추출 (6 → ≥15개 파라미터), Frame Export (TIFF/RAW)

### Potential Extensions (M9 이후)
1. **추가 패널 지원**: 다양한 해상도/비트뎁스/제조사 지원 확장
2. **실시간 전처리**: SoC에서 배드픽셀 보정, 게인/오프셋, 히스토그램 정규화
3. **AI 통합**: 실시간 이미지 분류 또는 이상 감지 추론 엔진
4. **멀티 패널 어레이**: 타일드 패널 배열(2×2, 3×3) 동기화 리드아웃
5. **FPGA 업그레이드**: Artix-7 100T 또는 Kintex UltraScale+ 마이그레이션
6. **Real Measurement 기반 자동 교정**: Dark/Bright 실측 이미지로 완전 자동 파라미터 최적화

---

## Glossary

**CSI-2**: Camera Serial Interface v2 (MIPI Alliance 카메라 데이터 전송 표준)
**D-PHY**: MIPI 물리층 사양 (CSI-2에서 사용하는 고속 시리얼 통신)
**DICOM**: Digital Imaging and Communications in Medicine (의료 영상 표준)
**FPGA**: Field-Programmable Gate Array (재구성 가능 논리 디바이스)
**HMAC-SHA256**: Hash-based Message Authentication Code (명령 무결성 인증)
**OSERDES**: Xilinx 출력 직렬화/역직렬화 프리미티브
**ROIC**: Readout Integrated Circuit (X-ray 검출기 아날로그→디지털 변환)
**SoC**: System-on-Chip (임베디드 프로세서 + 주변 장치 통합 칩)
**HIL**: Hardware-in-the-Loop (실제 하드웨어 포함 테스트)
**TRUST 5**: 품질 프레임워크 (Tested, Readable, Unified, Secured, Trackable)
**TFT**: Thin-Film Transistor (박막 트랜지스터 — 스캔/신호 라인 활성화)
**a-Si**: Amorphous Silicon (아몰퍼스 실리콘 — 전통적 X-ray 패널 물질)
**IGZO**: Indium-Gallium-Zinc Oxide (저누설 차세대 물질)

---

**Document End**

*Last updated: 2026-03-16. Reflects M2-Impl completion state (SW 100%), SPEC-GUI-001 active, v2.1 performance analysis integrated. Next update trigger: SPEC-GUI-001 완료 후.*
