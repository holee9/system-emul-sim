# X-ray Detector Panel System - Technology Stack

**Status**: ✅ 실제 사용 중인 기술 스택 (M2-Impl 완료 기준)
**Generated**: 2026-02-17
**Last Updated**: 2026-02-27

---

## Table of Contents

1. [Hardware Platform](#hardware-platform)
2. [FPGA Development](#fpga-development)
3. [SoC Firmware Development](#soc-firmware-development)
4. [Host SDK Development](#host-sdk-development)
5. [Developer Tools](#developer-tools)
6. [Testing Framework](#testing-framework)
7. [Build System](#build-system)
8. [NuGet Dependencies](#nuget-dependencies)
9. [Development Methodology](#development-methodology)
10. [Quality Gates](#quality-gates)

---

## Hardware Platform

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
- **10GbE**: 하드웨어 MAC/PHY

---

## FPGA Development

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

## SoC Firmware Development

| 항목 | 기술 / 도구 |
|------|------------|
| 언어 | C11 |
| 컴파일러 | GCC aarch64-linux-gnu |
| 빌드 시스템 | CMake 3.20+ |
| Yocto 버전 | Scarthgap 5.0 LTS |
| Linux 커널 | 6.6.52 LTS |
| CSI-2 인터페이스 | V4L2 Media Subsystem |
| SPI 인터페이스 | spidev 커널 드라이버 |
| 이더넷 | 10GbE UDP (raw socket) |
| 인증 | HMAC-SHA256 (명령 프로토콜) |
| 테스트 프레임워크 | Unity Test Framework (C) |

**Yocto 레이어**: meta-detector
- collection: detector, priority: 10
- LAYERCOMPAT: scarthgap (5.0 LTS 호환)
- 레시피: detector-daemon v1.0.0 (CMake + systemd), detector-image (256MB rootfs)

---

## Host SDK Development

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

## Developer Tools

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

### 시뮬레이터 (net8.0)

| 시뮬레이터 | 소스 파일 | 역할 |
|-----------|---------|------|
| FpgaSimulator.Core | 18개 | CSI-2 TX, SPI slave, 라인 버퍼 에뮬레이션 |
| PanelSimulator.Core | 7개 | 노이즈/게인/오프셋 X-ray 패널 모델 |
| McuSimulator.Core | 4개 | CSI-2 RX, 4-buffer ring, UDP endpoint |
| HostSimulator.Core | 8개 | SDK 통합 테스트 하네스 |
| Common.Dto | 6개 | 공유 DTO 허브 (의존성 없음) |

---

## Testing Framework

### C# 테스트 스택

| 라이브러리 | 버전 | 역할 |
|-----------|------|------|
| xUnit | 2.9.0 | 테스트 프레임워크 |
| Moq | 4.20.70 | Mock 객체 생성 |
| FluentAssertions | 최신 | Assertion 가독성 향상 |
| coverlet | 최신 | 코드 커버리지 측정 |

> **Note**: IntegrationTests 프로젝트와 Sdk.Tests 프로젝트 간 테스트 프레임워크 버전 불일치 존재. 통일 권장.

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

## Build System

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

## NuGet Dependencies

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

## Development Methodology

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

## Quality Gates

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

## Version Control

| 항목 | 내용 |
|------|------|
| VCS | Git (단일 저장소) |
| 브랜치 전략 | main 브랜치 + feature 브랜치 |
| 커밋 규칙 | Conventional Commits |
| SPEC 연동 | 커밋 메시지에 SPEC ID 참조 |

---

## Constraints & Known Issues

### 알려진 기술 부채

1. **IntegrationTests xUnit 버전 불일치**: Sdk.Tests와 다른 버전 사용 — 통일 필요
2. **ConfigConverter 미통과 테스트**: 42개 중 5개 실패 — M3-Integ 전 수정 필요
3. **iTextSharp AGPL 라이선스**: 상업적 배포 시 주의 필요
4. **패널 해상도 불일치**: ARCHITECTURE.md(3072×3072) vs detector_config.yaml(2048×2048) — 문서 동기화 필요
5. **펌웨어 레시피 버전 중복**: fw/deploy/detector-daemon_1.0.bb (구형) vs meta-detector/detector-daemon_1.0.0.bb (현재) — 구형 레시피 정리 필요

### FPGA 기술 제약

- **USB 3.x 불가**: IP 코어가 Artix-7 35T LUT 용량 72-120% 필요 — 구현 불가
- **D-PHY 속도 한계**: Artix-7 OSERDES 최대 1.25 Gbps/lane (D-PHY v2.5 최대값 아님)
- **Maximum Tier 위험**: 4.53 Gbps 요구 → 유효 CSI-2 대역폭(~3.2-3.5 Gbps) 초과 가능성

---

**Document End**

*Last updated: 2026-02-27. Reflects actual technology stack at M2-Impl completion.*
