# X-ray Detector Panel System - Product Overview

**Status**: ✅ M2-Impl 완료 (SW 100% 구현)
**Generated**: 2026-02-17
**Last Updated**: 2026-02-27
**Methodology**: Hybrid TDD/DDD (abyz-lab 개발 표준)

---

## Project Identity

**Name**: X-ray Detector Panel System
**Tagline**: Medical Imaging Grade Data Acquisition and Processing Platform
**Mission**: Deliver a production-grade, layered system for real-time X-ray detector panel control, data acquisition, and image processing for medical imaging equipment OEMs

**Project Type**: Research & Development System (Not a commercial product; platform for medical imaging equipment development)

**Development Timeline**: 28 weeks (7 months)
**Current Phase**: M3-Integ 준비 단계 — SW 구현(M2-Impl) 완료, 통합 테스트 단계 진입

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
| M4 (Performance) | ⬜ 미시작 | Target tier 2048×2048@30fps 성능 검증 |
| M5 (Validation) | ⬜ 미시작 | TRUST 5 완전 준수, 문서 완비 |
| M6 (Pilot) | ⬜ 미시작 | 파일럿 배포 |

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
- ParameterExtractor (WPF, net8.0-windows): 벤더 PDF 파라미터 추출 GUI
- GUI.Application (WPF, net8.0-windows): SDK 통합 기본 GUI
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
2. **High-Speed Data Acquisition**: Capture pixel data at rates up to 4.53 Gbps (Maximum tier) with deterministic latency
3. **Efficient Data Transport**: Stream image frames from FPGA → SoC → Host PC with minimal overhead
4. **Flexible Configuration**: Support multiple detector resolutions (1024×1024 to 3072×3072), bit depths (14-16 bit), and frame rates (15-30 fps)
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
                                                                              │ 10GbE UDP (port 8000)
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
- 10GbE UDP TX (port 8000) — 프레임 데이터 스트리밍
- HMAC-SHA256 Command Protocol (port 8001) — 제어 명령
- Sequence Engine (6-state FSM)
- Health Monitor

**Host PC / SDK (.NET 8.0)** — *구현 완료*:
- UDP 패킷 수신 및 프레임 재조립 (CRC-16 검증)
- 이미지 처리: Window/Level 매핑, TIFF/RAW/DICOM 인코딩
- IDetectorClient: async, event-driven, IAsyncEnumerable streaming
- DICOM XRayAngiographicImageStorage (fo-dicom 5.1.0, 7 DICOM 모듈)

### Key Architectural Decisions

1. **CSI-2 as Primary Data Path**: MIPI CSI-2 4-lane D-PHY chosen as FPGA↔SoC interface (FPGA resource constraint)
2. **USB 3.x Exclusion**: USB 3.x IP cores require 72-120% of Artix-7 35T LUT capacity — IMPOSSIBLE
3. **10 GbE for Host Link**: Required for Target/Maximum performance tiers (>1 Gbps sustained)
4. **Single Configuration Source**: `detector_config.yaml` → CodeGenerator → FPGA/SoC/Host 설정 파일 자동 생성
5. **HMAC-SHA256 Command Auth**: 명령 프로토콜 무결성 보장 (포트 8001)

---

## Performance Envelope

| Performance Tier | Resolution | Bit Depth | Frame Rate | Data Rate | Target Use Case |
|-----------------|------------|-----------|------------|-----------|----------------|
| **Minimum** | 1024×1024 | 14-bit | 15 fps | ~0.21 Gbps | 개발/단위 테스트 |
| **Target** | 2048×2048 | 16-bit | 30 fps | ~2.01 Gbps | 표준 임상 영상 |
| **Maximum** | 3072×3072 | 16-bit | 30 fps | ~4.53 Gbps | 고해상도 연구 영상 |

**현재 설정** (detector_config.yaml): 2048×2048, CSI-2 4-lane 400Mbps, SPI 50MHz, 10GbE UDP port 8000

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
- **Ethernet Offload**: 10GbE 하드웨어 체크섬 및 scatter-gather DMA

### 4. Comprehensive Simulation Environment (구현 완료)
- **PanelSimulator**: X-ray 패널 아날로그 출력 모델 (노이즈/게인/오프셋)
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
- **ParameterExtractor** (WPF): 벤더 PDF에서 타이밍/전기 파라미터 추출
- **ConfigConverter** (CLI): YAML → JSON/DTS/XDC 변환
- **CodeGenerator** (CLI): 반복 RTL 블록 및 보일러플레이트 코드 생성
- **IntegrationRunner** (CLI): 멀티 레이어 HIL 시나리오 자동 테스트 조율
- **GUI.Application** (WPF): SDK 통합 기본 GUI

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

## Development Timeline

### Phase Overview

| Phase | Milestone | Focus | Status |
|-------|-----------|-------|--------|
| P0 (W1) | M0 | Requirements & Architecture | ✅ 완료 |
| P1 (W2-W6) | M0.5 | Foundation & PoC | ✅ 완료 |
| P2 (W7-W14) | M1-M2 | Core Implementation (SW) | ✅ 완료 (M2-Impl) |
| P3 (W15-W18) | M3 | Integration & HIL Testing | 🔜 진행 예정 |
| P4 (W19-W21) | M4 | Performance Optimization | ⬜ 미시작 |
| P5 (W22-W24) | M5 | Validation & Documentation | ⬜ 미시작 |
| P6 (W25-W27) | M6 | Pilot Deployment | ⬜ 미시작 |
| P7 (W28) | M6+ | Handoff & Transition | ⬜ 미시작 |

---

## Future Roadmap

### Next Steps (M3-Integ)
1. **실 하드웨어 HIL 테스트**: Artix-7 dev board + i.MX8M Plus eval board 연결
2. **Minimum Tier 검증**: 1024×1024@15fps end-to-end (<1% 프레임 손실)
3. **통합 테스트 시나리오**: IT-01~IT-10 실행
4. **SPEC-INTEG-001 작성**: 통합 테스트 명세 문서화

### Potential Extensions
1. **추가 패널 지원**: 다양한 해상도/비트뎁스/제조사 지원 확장
2. **실시간 전처리**: SoC에서 배드픽셀 보정, 게인/오프셋, 히스토그램 정규화
3. **AI 통합**: 실시간 이미지 분류 또는 이상 감지 추론 엔진
4. **멀티 패널 어레이**: 타일드 패널 배열(2×2, 3×3) 동기화 리드아웃
5. **FPGA 업그레이드**: Artix-7 100T 또는 Kintex UltraScale+ 마이그레이션

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

---

**Document End**

*Last updated: 2026-02-27. Reflects M2-Impl completion state (SW 100%). Next update trigger: M3-Integ 완료 후.*
