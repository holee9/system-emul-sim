# X-ray Detector Panel System - Project Structure

**Status**: ✅ 실제 구현된 구조 (M2-Impl 완료)
**Generated**: 2026-02-17
**Last Updated**: 2026-02-27

---

## Table of Contents

1. [Repository Overview](#repository-overview)
2. [실제 디렉토리 구조](#실제-디렉토리-구조)
3. [SDK 모듈 구성](#sdk-모듈-구성)
4. [Tools 모듈 구성](#tools-모듈-구성)
5. [Firmware 구성](#firmware-구성)
6. [FPGA RTL 구성](#fpga-rtl-구성)
7. [설정 및 생성 코드](#설정-및-생성-코드)
8. [모듈 의존성 그래프](#모듈-의존성-그래프)
9. [테스트 구성](#테스트-구성)
10. [빌드 시스템](#빌드-시스템)

---

## Repository Overview

단일 Git 저장소에 전체 프로젝트가 통합되어 있습니다.

| 디렉토리 | 기술 | 내용 | 상태 |
|---------|------|------|------|
| **fpga/** | SystemVerilog | RTL 모듈, 테스트벤치, 제약 파일 | ✅ SPEC-FPGA-001 완료 |
| **fw/** | C11 / Yocto | SoC 펌웨어, meta-detector Yocto 레이어 | 🔶 알파 개발 중 |
| **sdk/** | C# .NET 8.0 | Host SDK 라이브러리 | ✅ SPEC-SDK-001 완료 |
| **tools/** | C# .NET 8.0 | 시뮬레이터, GUI 도구, CLI 유틸리티 | ✅ SPEC-TOOLS-001 완료 |
| **config/** | YAML/JSON/DTS/XDC | 단일 소스 설정 파일 | ✅ 생성 완료 |
| **generated/** | C#/C/SV | CodeGenerator 자동 출력물 | ✅ 컴파일 검증 완료 |
| **.moai/** | Markdown/YAML | 프로젝트 문서, SPEC, 설정 | ✅ 7개 SPEC 완료 |

**총 .csproj 파일**: 18개 (no solution file)
**총 테스트 파일**: 50+개
**코드 커버리지**: 85%+

---

## 실제 디렉토리 구조

```
system-emul-sim/
├── sdk/
│   ├── XrayDetector.Sdk/                         # Host SDK 핵심 라이브러리
│   │   ├── XrayDetector.Sdk.csproj               # net8.0, System.IO.Pipelines, fo-dicom
│   │   ├── Core/
│   │   │   ├── Communication/                    # UDP 통신 레이어
│   │   │   ├── Reassembly/                       # 프레임 재조립 (CRC-16 검증)
│   │   │   └── Processing/
│   │   │       ├── ImageEncoder.cs               # TIFF/RAW 인코딩
│   │   │       ├── WindowLevelMapper.cs          # 윈도우/레벨 매핑
│   │   │       └── DicomEncoder.cs               # DICOM XRayAngiographicImageStorage (신규)
│   │   ├── Discovery/                            # 디바이스 검색
│   │   ├── Implementation/
│   │   │   └── IDetectorClient.cs                # async, IAsyncEnumerable streaming
│   │   └── Models/
│   │       └── Frame.cs                          # 프레임 데이터 모델
│   └── XrayDetector.Sdk.Tests/
│       ├── XrayDetector.Sdk.Tests.csproj         # xUnit 2.9.0, Moq 4.20.70, FluentAssertions
│       ├── Core/Processing/
│       │   ├── ImageEncoderTests.cs
│       │   ├── WindowLevelMapperTests.cs
│       │   └── DicomEncoderTests.cs              # 12개 테스트 케이스 (신규)
│       └── Models/
│           └── FrameTests.cs
│
├── tools/
│   ├── Common.Dto/                               # 공유 DTO 허브 (의존성 없음)
│   │   ├── Common.Dto.csproj
│   │   ├── FrameData.cs
│   │   ├── ConfigurationDto.cs
│   │   ├── DiagnosticsDto.cs
│   │   └── Common.Dto.Tests/
│   │       └── (6개 테스트 파일)
│   │
│   ├── FpgaSimulator/
│   │   ├── FpgaSimulator.Core/                   # FPGA 동작 모델
│   │   │   ├── FpgaSimulator.Core.csproj         # 18개 소스 파일
│   │   │   ├── Csi2Transmitter.cs                # CSI-2 TX 에뮬레이션
│   │   │   ├── SpiSlave.cs                       # SPI slave 에뮬레이션
│   │   │   └── LineBuffer.cs                     # 라인 버퍼 에뮬레이션
│   │   └── FpgaSimulator.Tests/
│   │       └── (5개 테스트 파일)
│   │
│   ├── PanelSimulator/                           # X-ray 패널 아날로그 모델
│   │   ├── PanelSimulator.Core/
│   │   │   ├── PanelSimulator.Core.csproj        # 7개 소스 파일
│   │   │   └── NoiseGenerator.cs                 # 노이즈/게인/오프셋 주입
│   │   └── PanelSimulator.Tests/
│   │       └── (5개 테스트 파일)
│   │
│   ├── McuSimulator/                             # SoC 펌웨어 에뮬레이션
│   │   ├── McuSimulator.Core/
│   │   │   ├── McuSimulator.Core.csproj          # 4개 소스 파일
│   │   │   ├── Csi2Receiver.cs                   # CSI-2 RX 에뮬레이션
│   │   │   └── EthernetEndpoint.cs               # UDP 엔드포인트 에뮬레이션
│   │   └── McuSimulator.Tests/
│   │       └── (4개 테스트 파일)
│   │
│   ├── HostSimulator/                            # Host SDK 통합 테스트 하네스
│   │   ├── HostSimulator.Core/
│   │   │   ├── HostSimulator.Core.csproj         # 8개 소스 파일
│   │   │   └── ImageValidator.cs                 # 프레임 무결성 검증
│   │   └── HostSimulator.Tests/
│   │       └── (6개 테스트 파일)
│   │
│   ├── IntegrationTests/                         # 전체 통합 테스트 (4개 시뮬레이터 통합)
│   │   └── IntegrationTests.csproj
│   │
│   ├── GUI.Application/                          # WPF 기본 GUI (net8.0-windows)
│   │   └── src/GUI.Application/
│   │       ├── GUI.Application.csproj            # CommunityToolkit.Mvvm, Serilog
│   │       ├── App.xaml.cs
│   │       ├── Views/MainWindow.xaml
│   │       └── ViewModels/MainViewModel.cs
│   │
│   ├── ParameterExtractor/                       # WPF 파라미터 추출 도구 (net8.0-windows)
│   │   └── src/ParameterExtractor.Wpf/
│   │       ├── ParameterExtractor.Wpf.csproj     # iTextSharp(AGPL), YamlDotNet, Serilog
│   │       ├── App.xaml.cs
│   │       ├── Views/MainWindow.xaml
│   │       └── ViewModels/MainWindowViewModel.cs
│   │
│   ├── CodeGenerator/                            # CLI 코드 생성기
│   │   └── src/CodeGenerator.Cli/
│   │       ├── CodeGenerator.Cli.csproj          # System.CommandLine, YamlDotNet
│   │       └── (9개 테스트)
│   │
│   ├── ConfigConverter/                          # CLI 설정 포맷 변환기
│   │   └── src/ConfigConverter.Cli/
│   │       ├── ConfigConverter.Cli.csproj        # YamlDotNet
│   │       └── (37/42 테스트 통과)
│   │
│   └── IntegrationRunner/                        # CLI 통합 테스트 조율기
│       └── src/IntegrationRunner.Cli/
│           └── IntegrationRunner.Cli.csproj      # System.CommandLine
│
├── fw/                                           # SoC 펌웨어 (C11)
│   ├── ARCHITECTURE.md                           # 710줄 아키텍처 문서
│   ├── README.md                                 # Yocto 빌드 가이드
│   ├── src/
│   │   ├── main.c
│   │   ├── csi2_rx.c                             # V4L2 CSI-2 RX
│   │   ├── spi_master.c                          # spidev SPI Master
│   │   ├── udp_tx.c                              # 10GbE UDP TX (port 8000)
│   │   ├── cmd_protocol.c                        # HMAC-SHA256 Command (port 8001)
│   │   ├── sequence_engine.c                     # 6-state FSM
│   │   ├── frame_manager.c                       # 4-buffer ring
│   │   └── health_monitor.c
│   ├── tests/
│   │   ├── (10개 단위 테스트 파일)
│   │   ├── mocks/                                # V4L2/spidev/YAML mock
│   │   └── integration/
│   ├── deploy/
│   │   └── detector-daemon_1.0.bb               # 구형 레시피 (레거시)
│   └── meta-detector/                            # Yocto 레이어
│       ├── conf/
│       │   └── layer.conf                        # collection: detector, priority 10
│       ├── recipes-detector/
│       │   ├── detector-daemon/
│       │   │   └── detector-daemon_1.0.0.bb      # CMake + systemd inherit
│       │   └── packagegroup-detector/
│       │       └── packagegroup-detector.bb
│       └── recipes-core/
│           └── images/
│               └── detector-image.bb             # core-image-minimal + 256MB rootfs
│
├── fpga/                                         # FPGA RTL (SystemVerilog)
│   ├── csi2_detector_top.sv                      # Top-level 모듈
│   ├── panel_scan_fsm.sv                         # 패널 시퀀싱 6-state FSM
│   ├── line_buffer.sv                            # Dual-port BRAM 라인 버퍼
│   ├── csi2_tx_wrapper.sv                        # MIPI CSI-2 TX wrapper
│   ├── spi_slave.sv                              # SPI control interface
│   ├── protection_logic.sv                       # 과열/타이밍 보호 로직
│   ├── tb/
│   │   ├── panel_scan_fsm_tb.sv
│   │   ├── line_buffer_tb.sv
│   │   ├── csi2_tx_wrapper_tb.sv
│   │   ├── spi_slave_tb.sv
│   │   ├── protection_logic_tb.sv
│   │   └── integration_tb.sv
│   └── constraints/
│       └── (XDC 제약 파일)
│
├── config/                                       # 단일 소스 설정 파일
│   ├── detector_config.yaml                      # 마스터 설정 (2048×2048, CSI-2 4-lane)
│   ├── detector_config.json                      # Host SDK용 JSON 버전
│   ├── detector_config.dts                       # Auto-generated (2026-02-18)
│   └── detector_config.xdc                       # Auto-generated (2026-02-18)
│
├── generated/                                    # CodeGenerator 자동 출력물
│   ├── fpga_registers.h                          # C header (FPGA 레지스터 맵)
│   ├── line_buffer.sv                            # RTL 파라미터 모듈
│   ├── panel_scan_fsm.sv                         # RTL 파라미터 모듈
│   ├── DetectorConfig.g.cs                       # C# 설정 클래스 (SystemEmulSim.Sdk)
│   ├── FrameHeader.g.cs                          # C# 프레임 헤더 클래스
│   └── TestSdkCompilation/
│       └── TestSdkCompilation.csproj             # 컴파일 검증 프로젝트
│
└── .moai/
    ├── project/
    │   ├── product.md                            # 프로젝트 개요 (이 문서의 형제)
    │   ├── structure.md                          # 이 문서
    │   └── tech.md                               # 기술 스택
    ├── specs/
    │   ├── SPEC-ARCH-001/                        # plan.md + spec.md + acceptance.md
    │   ├── SPEC-FPGA-001/
    │   ├── SPEC-FW-001/
    │   ├── SPEC-POC-001/
    │   ├── SPEC-SDK-001/
    │   ├── SPEC-SIM-001/
    │   └── SPEC-TOOLS-001/
    └── config/sections/
        ├── quality.yaml                          # development_mode: hybrid
        ├── language.yaml                         # conversation_language: ko
        └── user.yaml
```

---

## SDK 모듈 구성

### XrayDetector.Sdk (21개 소스 파일)

```
Core/Communication/
  ├── UdpReceiver.cs              # UDP 패킷 수신 (port 8000)
  └── CommandClient.cs            # HMAC-SHA256 명령 클라이언트 (port 8001)

Core/Reassembly/
  ├── FrameReassembler.cs         # 패킷 → 프레임 재조립
  └── CrcValidator.cs             # CRC-16 검증

Core/Processing/
  ├── ImageEncoder.cs             # TIFF/RAW 인코딩
  ├── WindowLevelMapper.cs        # 16-bit → 8-bit W/L 매핑
  └── DicomEncoder.cs             # DICOM 인코딩 (fo-dicom 5.1.0) [신규]

Discovery/
  └── DetectorDiscovery.cs        # 디바이스 자동 검색

Implementation/
  └── IDetectorClient.cs          # 비동기 인터페이스 (IAsyncEnumerable)

Models/
  └── Frame.cs                    # 프레임 데이터 모델
```

### DicomEncoder 상세

- **표준**: DICOM XRayAngiographicImageStorage
- **구현**: fo-dicom 5.1.0
- **DICOM 모듈**: Patient, Study, Series, Equipment, Image Pixel, VOI LUT, SOP Common
- **UID 생성**: `2.25.<timestamp>.<random>` (DICOM 표준)
- **인코딩**: 16-bit big-endian 그레이스케일
- **테스트**: 12개 케이스 (기본값, 커스텀 메타데이터, 대용량 프레임, 경계 조건)

---

## Tools 모듈 구성

### 시뮬레이터 의존성

```
Common.Dto (의존성 없음 — 허브)
    ├── PanelSimulator.Core
    ├── FpgaSimulator.Core
    ├── McuSimulator.Core (+ FpgaSimulator.Core 의존: 실제 HW 토폴로지 미러링)
    └── HostSimulator.Core
        └── IntegrationTests (4개 시뮬레이터 전체 통합)
```

### GUI 도구

| 도구 | 타겟 | 주요 의존성 | 역할 |
|------|------|------------|------|
| GUI.Application | net8.0-windows | CommunityToolkit.Mvvm, Serilog | SDK 통합 기본 GUI |
| ParameterExtractor.Wpf | net8.0-windows | iTextSharp(AGPL⚠️), YamlDotNet, Serilog | 벤더 PDF 파라미터 추출 |

> ⚠️ **라이선스 주의**: ParameterExtractor의 iTextSharp는 AGPL 라이선스입니다.

### CLI 도구

| 도구 | 주요 의존성 | 역할 |
|------|------------|------|
| CodeGenerator.Cli | System.CommandLine, YamlDotNet | YAML → RTL/C/C# 코드 생성 |
| ConfigConverter.Cli | YamlDotNet | YAML → JSON/DTS/XDC 변환 |
| IntegrationRunner.Cli | System.CommandLine | HIL 테스트 시나리오 조율 |

---

## Firmware 구성

### 핵심 모듈 (C11, NXP i.MX8M Plus aarch64)

| 모듈 | 파일 | 역할 |
|------|------|------|
| CSI-2 RX | csi2_rx.c | V4L2 드라이버 인터페이스 |
| SPI Master | spi_master.c | spidev를 통한 FPGA 제어 |
| 10GbE UDP TX | udp_tx.c | 프레임 UDP 스트리밍 (port 8000) |
| Command Protocol | cmd_protocol.c | HMAC-SHA256 명령 인증 (port 8001) |
| Sequence Engine | sequence_engine.c | 6-state FSM (IDLE→INIT→READY→CAPTURE→TRANSFER→ERROR) |
| Frame Manager | frame_manager.c | 4-buffer ring (zero-copy DMA) |
| Health Monitor | health_monitor.c | 시스템 상태 모니터링 |

### Yocto 레이어 (meta-detector)

```
meta-detector/
├── conf/layer.conf               collection: detector, priority 10
│                                 LAYERCOMPAT: scarthgap
├── recipes-detector/
│   ├── detector-daemon_1.0.0.bb  CMake + systemd
│   └── packagegroup-detector.bb
└── recipes-core/images/
    └── detector-image.bb         core-image-minimal 기반, 256MB rootfs
```

**빌드 환경**: Yocto Scarthgap 5.0 LTS, Linux 6.6.52, GCC aarch64-linux-gnu

---

## FPGA RTL 구성

### 모듈 목록 (SystemVerilog, Xilinx Artix-7 XC7A35T-FGG484)

| 모듈 | 파일 | 역할 | 추정 LUT |
|------|------|------|---------|
| csi2_detector_top | csi2_detector_top.sv | Top-level 통합 | ~1,000 |
| panel_scan_fsm | panel_scan_fsm.sv | 패널 시퀀싱 6-state FSM | ~800 |
| line_buffer | line_buffer.sv | Dual-port BRAM 라인 버퍼 | ~400 |
| csi2_tx_wrapper | csi2_tx_wrapper.sv | MIPI CSI-2 TX subsystem | ~2,500 |
| spi_slave | spi_slave.sv | SPI 제어 인터페이스 | ~300 |
| protection_logic | protection_logic.sv | 과열/타이밍 보호 | ~350 |

**목표 LUT 사용률**: <60% (<12,480 LUTs) — 현재 설계 기준 ~26% (application logic only)

### 클록 도메인

1. **clk_panel** (~50 MHz): 패널 스캔 타이밍
2. **clk_csi2** (~250 MHz): CSI-2 패킷 생성
3. **clk_dphy** (~1.0-1.25 GHz): D-PHY 직렬화 (OSERDES DDR)
4. **clk_spi** (~50 MHz): SPI slave 인터페이스

---

## 설정 및 생성 코드

### 단일 소스 패턴

```
detector_config.yaml (마스터 설정)
    │
    ├──> CodeGenerator CLI ──> generated/
    │        │                   ├── fpga_registers.h    (C header)
    │        │                   ├── line_buffer.sv      (RTL 파라미터)
    │        │                   ├── panel_scan_fsm.sv   (RTL 파라미터)
    │        │                   ├── DetectorConfig.g.cs (C# 클래스)
    │        │                   └── FrameHeader.g.cs    (C# 클래스)
    │        │
    └──> ConfigConverter CLI ──> config/
                                  ├── detector_config.json (Host SDK용)
                                  ├── detector_config.dts  (Auto-generated)
                                  └── detector_config.xdc  (Auto-generated)
```

**현재 설정값** (detector_config.yaml):
- 패널: 2048×2048, 16-bit, 30fps
- CSI-2: 4-lane, 400Mbps
- SPI: 50MHz
- 10GbE: UDP port 8000 (데이터), port 8001 (명령)

> **Note**: ARCHITECTURE.md 다이어그램에는 3072×3072가 표기되어 있으나, 실제 구현 기준(detector_config.yaml)은 2048×2048입니다. 최종 결정 시 문서 동기화 필요.

---

## 모듈 의존성 그래프

```
XrayDetector.Sdk
    │  (System.IO.Pipelines, fo-dicom 5.1.0)
    └──> GUI.Application (SDK 통합 브릿지)

Common.Dto (의존성 없음)
    ├──> PanelSimulator.Core
    ├──> FpgaSimulator.Core
    ├──> McuSimulator.Core ──> FpgaSimulator.Core
    ├──> HostSimulator.Core
    └──> IntegrationTests (4개 시뮬레이터 전체)

CodeGenerator.Cli ──> generated/ (자동 생성 코드)
ConfigConverter.Cli ──> config/ (JSON/DTS/XDC)
```

---

## 테스트 구성

### 테스트 계층

**Level 1: 단위 테스트 (Unit Tests)**
- C# xUnit 2.9.0 + Moq 4.20.70 + FluentAssertions
- SDK: 16개 테스트 파일 (DicomEncoderTests 포함)
- 시뮬레이터: 각 4~6개 테스트 파일
- CLI 도구: 각 9~42개 테스트

**Level 2: 통합 테스트 (Integration Tests)**
- IntegrationTests 프로젝트: 4개 시뮬레이터 전체 통합
- 시나리오: IT-01~IT-10 (단일 프레임, 연속 캡처, SPI 구성, 버퍼 오버플로, 타임아웃 등)

**Level 3: HIL 테스트 (Hardware-in-the-Loop)**
- M3-Integ 단계에서 실제 하드웨어 연결 예정

### RTL 테스트벤치 (SystemVerilog)
- 모듈별 단위 테스트벤치 (5개)
- 통합 테스트벤치 (integration_tb.sv)

### 펌웨어 테스트 (C)
- 10개 단위 테스트 파일
- V4L2/spidev/YAML mock 지원
- 통합 테스트

### 테스트 커버리지 목표
- SW 전체: 85%+ (달성)
- RTL: 라인 커버리지 ≥95%, 브랜치 ≥90%, FSM 100%

---

## 빌드 시스템

### C# 프로젝트 빌드

```bash
# SDK 빌드 및 테스트
cd sdk/
dotnet build
dotnet test

# Tools 빌드 및 테스트
cd tools/
dotnet build
dotnet test

# 개별 CLI 도구 실행
dotnet run --project tools/CodeGenerator/src/CodeGenerator.Cli -- --config config/detector_config.yaml
dotnet run --project tools/ConfigConverter/src/ConfigConverter.Cli -- --input config/detector_config.yaml
```

### Yocto 빌드 (SoC 펌웨어)

```bash
# meta-detector 레이어 빌드
source poky/oe-init-build-env build-detector
bitbake detector-image
```

**참고**: fw/README.md에 상세 빌드 가이드 포함

### FPGA 빌드 (Vivado)

```bash
cd fpga/
vivado -mode batch -source scripts/build.tcl
```

**타겟 디바이스**: xc7a35tfgg484-1 (Xilinx Artix-7)

---

## SPEC 문서 참조

각 구성 요소의 상세 명세:

| SPEC | 관련 디렉토리 | 핵심 내용 |
|------|-------------|---------|
| SPEC-ARCH-001 | 전체 | 시스템 아키텍처, 인터페이스 정의 |
| SPEC-FPGA-001 | fpga/ | RTL 모듈 명세, 타이밍 제약 |
| SPEC-FW-001 | fw/ | 펌웨어 모듈 명세, Yocto 레이어 |
| SPEC-POC-001 | tools/IntegrationTests | PoC 시나리오, 시뮬레이터 프레임워크 |
| SPEC-SDK-001 | sdk/ | Host SDK API, IDetectorClient |
| SPEC-SIM-001 | tools/*Simulator | 시뮬레이터 동작 명세 |
| SPEC-TOOLS-001 | tools/GUI, tools/ParameterExtractor 등 | 개발자 도구 명세 |

---

**Document End**

*Last updated: 2026-02-27. Reflects actual implemented structure at M2-Impl completion.*
