# X-ray Detector Panel System

엑스레이 검출기 패널을 구동하고 제어하기 위한 통합 시스템입니다. FPGA 기반 하드웨어 제어와 소프트웨어 시뮬레이션 환경을 제공합니다.

## 현재 상태

> **Phase 1 문서화 완전 승인 ✅** (2026-02-17)
>
> SPEC/아키텍처/API 문서 31개 교차검증 완료 — Critical 10건 + Major 10건 수정 완료
> **Run Phase (W9+) 진입 가능**

## 프로젝트 개요

의료 영상 장비용 엑스레이 검출기 패널의 데이터 수집, 전송, 처리를 위한 계층형 시스템을 구축합니다.

### 핵심 목표

- **계층형 아키텍처**: FPGA → SoC Controller → Host PC 구조로 역할 분리
- **실시간 제어**: FPGA에서 패널 스캔 타이밍 및 고속 데이터 전송 담당
- **소프트웨어 시뮬레이터**: 하드웨어 없이 전체 시스템을 검증할 수 있는 환경
- **단일 설정 원천 (One Source of Truth)**: `detector_config.yaml` 파일로 모든 타겟 구성

### 주요 특징

| 특징 | 설명 |
|------|------|
| 최종 목표 해상도 | 3072 x 3072 픽셀 (16-bit) @ 15fps |
| 개발 기준선 | 2048 x 2048 픽셀 (16-bit) @ 15fps (400M 안정 검증) |
| 데이터 인터페이스 | CSI-2 MIPI 4-lane D-PHY (FPGA → SoC) |
| 네트워크 전송 | 10 GbE (SoC → Host PC, 권장) |
| 제어 채널 | SPI (최대 50 MHz) |

## 시스템 구조

```
[X-ray Panel] → [Gate IC + ROIC] → [FPGA: XC7A35T] → [SoC Controller] → [Host PC + SDK]
                                          |                    |                    |
                                     하드 실시간 전용         시퀀스/통신          프레임/디스플레이
                                     (타이밍 FSM,          (SPI 제어,           (재조립,
                                      라인 버퍼,            CSI-2 RX,            저장,
                                      CSI-2 TX)            이더넷 TX)           디스플레이)
```

### 계층별 역할

#### FPGA (Xilinx Artix-7 XC7A35T)
- **패널 스캔 타이밍 FSM**: 정밀한 타이밍 제어로 패널 스캔 시퀀스 실행
- **라인 버퍼**: Ping-Pong BRAM 구조로 데이터 손실 없이 라인 데이터 수집
- **CSI-2 TX**: 고속 데이터를 SoC로 전송 (4-lane D-PHY, ~4-5 Gbps)
- **보호 로직**: 타임아웃, 과노출 등 오류 감지 및 복구

#### SoC Controller (NXP i.MX8M Plus 권장)
- **CSI-2 RX**: FPGA로부터 영상 데이터 수신
- **시퀀스 엔진**: 프레임 스캔 시퀀스 제어
- **네트워크 스트리밍**: 이더넷을 통해 Host PC로 프레임 전송
- **SPI 마스터**: FPGA 레지스터 읽기/쓰기

#### Host PC
- **DetectorClient SDK**: 네트워크를 통한 검출기 제어 API
- **프레임 재조립**: 패킷에서 2D 이미지 복원
- **저장**: RAW, TIFF, (선택적) DICOM 형식 지원
- **실시간 디스플레이**: 영상 뷰어

## 핵심 기술 결정사항

### FPGA 디바이스 제약

**Xilinx Artix-7 XC7A35T-FGG484** (소형 FPGA)를 사용하며, 이로 인해 아키텍처가 결정되었습니다:

| 리소스 | 가용량 | 영향 |
|--------|--------|------|
| Logic Cells | 33,280 | USB 3.x 컨트롤러 IP는 단독으로 72-120% 소모 → 불가능 |
| LUTs | 20,800 | CSI-2 TX + FSM + SPI는 34-58% 예상 → 실현 가능 |
| Block RAM | 50 (225 KB) | 라인 버퍼는 ~5% 사용 (여유 충분) |

**결론**: USB 3.x는 불가능하며, **CSI-2가 유일한 고속 데이터 경로**입니다.

### 성능 계층

| 계층 | 해상도 | 비트 깊이 | FPS | 원시 데이터 속도 | D-PHY 요건 | 상태 |
|------|--------|-----------|-----|-----------------|-----------|------|
| 최소 (Minimum) | 1024 x 1024 | 14-bit | 15 fps | ~0.21 Gbps | 400M/lane | ✅ 검증 완료 |
| 중간-A (Mid-A) | 2048 x 2048 | 16-bit | 15 fps | ~1.01 Gbps | 400M/lane | ✅ 개발 기준선 |
| 중간-B (Mid-B) | 2048 x 2048 | 16-bit | 30 fps | ~2.01 Gbps | 800M/lane | ⚠️ 800M 디버깅 필요 |
| **목표 (Target Final Goal)** | **3072 x 3072** | **16-bit** | **15 fps** | **~2.26 Gbps** | **800M/lane** | ⚠️ 800M 디버깅 필요 |

> ❌ **제외**: 3072 x 3072 @ 30fps (~4.53 Gbps) — 4-lane 3.2 Gbps 한계 초과, 영구 제외

### 인터페이스 선택

| 인터페이스 | 대역폭 | 목표 계층 지원? | Artix-7 35T 실현 가능성 |
|-----------|--------|----------------|----------------------|
| CSI-2 4-lane | 1.6 Gbps (400M, 안정) / 3.2 Gbps (800M, 디버깅) | 목표 계층: 2.26 Gbps (800M 완료 시 29% 여유) | **가능 (선택됨)** |
| USB 3.x | ~5 Gbps | 이론적으로 가능 | **불가능** (LUT 72-120% 소모) |
| 10 GbE | ~10 Gbps | 모든 계층 지원 | SoC → Host 전용 |

### SoC 빌드 시스템 (최종 확정)

**빌드 시스템**: Yocto Project Scarthgap (5.0 LTS)
- **BSP**: Variscite imx-6.6.52-2.2.0-v1.3
- **Linux Kernel**: 6.6.52 (LTS until December 2026, Yocto LTS until April 2028)
- **마이그레이션**: Mickledore (4.2, EOL Nov 2024) → Scarthgap (W1-W2, 8일)

**확정 하드웨어 플랫폼** (2026-02-17 검증 완료):

| 구성요소 | 모델 | 인터페이스 | 드라이버 | Kernel 6.6 상태 |
|---------|------|-----------|---------|-----------------|
| SoM | Variscite VAR-SOM-MX8M-PLUS (DART) | - | - | ✅ Scarthgap BSP |
| WiFi/BT | Ezurio Sterling 60 (QCA6174A) | M.2 PCIe + USB | ath10k_pci + btusb | ✅ 포함 |
| Battery | TI BQ40z50 | SMBus (I2C addr 0x0b) | bq27xxx_battery | ⚠️ 포트 필요 (from 4.4) |
| IMU | Bosch BMI160 | I2C7 (addr 0x68) | bmi160_i2c (IIO) | ✅ 포함 |
| GPIO | NXP PCA9534 | I2C | gpio-pca953x | ✅ 포함 |
| 2.5GbE | TBD (on-board) | PCIe/RGMII | TBD | ⚠️ 칩 확인 필요 (lspci -nn) |

**신규 개발 대상**:
1. FPGA → i.MX8MP CSI-2 RX 드라이버 (V4L2, kernel 6.6)
2. FPGA-SoC 데이터 포맷 정의 (MIPI CSI-2 RAW16 or custom)
3. 2.5GbE 네트워크 드라이버 검증

**폐기된 레거시 드라이버**:
- ❌ dscam6.ko (CSI-2 카메라 → FPGA RX 드라이버로 대체)
- ❌ ax_usb_nic.ko (AX88279 USB Ethernet → 2.5GbE로 대체)
- ❌ imx8-media-dev.ko (V4L2 프레임워크로 대체)

### 개발 방법론

**프로젝트 접근법**: 문서 우선 (Document-First Waterfall)
- **Phase 1** (W1-W8): 모든 계획서, 사양서, SPEC 문서 작성 및 승인
- **Phase 2** (W9-W22): 시뮬레이터, 도구, RTL 구현 및 통합 테스트
- **Phase 3** (W23-W28): FPGA RTL, SoC 펌웨어 개발 및 HW 검증 (PoC, HIL)

**개발 방법론**: Hybrid (quality.yaml 설정)
- **신규 코드**: TDD (Test-Driven Development, RED-GREEN-REFACTOR)
- **기존 코드**: DDD (Domain-Driven Development, ANALYZE-PRESERVE-IMPROVE)
- **커버리지 목표**: 85%+ (RTL: Line ≥95%, Branch ≥90%, FSM 100%)

## 소프트웨어 구조

프로젝트는 10개의 모듈과 8개의 테스트 프로젝트로 구성됩니다:

```
Solution/
├── Common.Dto/              # 공통 인터페이스 (ISimulator, ICodeGenerator, DTOs)
├── PanelSimulator/          # 픽셀 매트릭스, 노이즈 모델, 결함 시뮬레이션
├── FpgaSimulator/           # SPI 레지스터, FSM, 라인 버퍼 (골든 참조)
├── McuSimulator/            # HAL 추상화 펌웨어 로직
├── HostSimulator/           # 패킷 재조립, 프레임 완성
├── ParameterExtractor/      # PDF 파싱, 규칙 엔진, GUI (C# WPF)
├── CodeGenerator/           # FPGA RTL / MCU / Host SDK 스켈레톤 생성
├── ConfigConverter/         # YAML → 타겟별 설정 변환
├── IntegrationRunner/       # IT-01~IT-10 시나리오 실행 CLI
└── GUI.Application/         # 통합 WPF GUI
```

### 의존성 규칙

모든 모듈은 `Common.Dto`에만 의존하며, 서로의 구현에 직접 의존하지 않습니다.

## 개발 일정

총 **28주** 계획:

```
W1-W8:   Phase 1 - 문서 우선 (SPEC, 아키텍처, API 문서) ← 현재 완료 ✅
W9-W14:  Phase 2 - 시뮬레이터 개발 (TDD)
W9-W18:  Phase 3 - FPGA RTL 개발
W11-W20: Phase 4 - SoC Controller 펌웨어
W12-W22: Phase 5 - Host SDK 개발
W16-W22: Phase 6 - 통합 테스트 (IT-01~IT-10)
W18-W22: Phase 7 - HIL 테스트
W23-W26: M0.5 - CSI-2 PoC (HW 검증, 구현 완료 후)
W22-W28: Phase 8 - 시스템 검증 및 확인
```

### 주요 마일스톤

| 마일스톤 | 주차 | 게이트 기준 | 상태 |
|---------|------|------------|------|
| **M0** | W1 | P0 결정 확정 (성능 목표, Host 링크, SoC 플랫폼) | ✅ 완료 |
| **M1-Doc** | W8 | 모든 SPEC/아키텍처/API 문서 완료 및 승인 | ✅ Phase 1 교차검증 완전 승인 (2026-02-17) |
| M2-Impl | W14 | 모든 시뮬레이터 단위 테스트 통과 | ⏳ 대기 |
| M3-Integ | W22 | IT-01~IT-10 통합 시나리오 모두 통과 | ⏳ 대기 |
| **M0.5-PoC** | W26 | CSI-2 PoC: 목표 처리량의 ≥70% 측정 완료 (구현 완료 후 수행) | ⏳ 연기 |
| M6-Final | W28 | 실제 패널 프레임 획득, 시뮬레이터 보정 완료 | ⏳ 대기 |

## 품질 전략

### 개발 방법론 (Hybrid)

프로젝트는 **Hybrid 개발 방법론**을 사용합니다 (`quality.yaml` 설정):

| 코드 유형 | 방법론 | 사이클 |
|----------|--------|--------|
| 신규 코드 (시뮬레이터, SDK, 도구) | TDD | RED-GREEN-REFACTOR |
| 기존 코드 수정 | DDD | ANALYZE-PRESERVE-IMPROVE |
| FPGA RTL | DDD 접근 | 특성화 테스트 → 점진적 RTL 개발 |

### 검증 피라미드

```
계층 4: 시스템 V&V           실제 패널 통합 (M6)
계층 3: 통합 테스트           IT-01~IT-10 시나리오 (M3)
계층 2: 단위 테스트           FV-01~FV-11 (RTL), xUnit/pytest (SW) (M2)
계층 1: 정적 분석            RTL lint, CDC 검사, 컴파일 경고 (지속적)
```

### 목표 KPI

| 메트릭 | 목표 |
|-------|------|
| RTL 코드 커버리지 | 라인 ≥95%, 브랜치 ≥90%, FSM 100% |
| SW 단위 테스트 커버리지 | 모듈당 80-90% |
| 프레임 드롭률 | <0.01% |
| 데이터 무결성 | 비트 정확도 (0 오류) |
| CSI-2 처리량 | ≥1 GB/s (4-lane) |

## 설정 관리

### 단일 설정 원천 (One Source of Truth)

모든 타겟 설정은 `detector_config.yaml` 파일에서 관리됩니다:

```yaml
panel:
  rows: 3072
  cols: 3072
  pixel_pitch_um: 150
  bit_depth: 16

fpga:
  timing: { gate_on_us, gate_off_us, roic_settle_us, adc_conv_us }
  line_buffer: { depth_lines, bram_width_bits }
  data_interface:
    primary: csi2
    csi2: { lane_count: 4, data_type: RAW16, virtual_channel: 0 }
  spi: { clock_hz: 50000000, mode: 0 }

controller:
  platform: imx8mp
  ethernet: { speed: 10gbe, protocol: udp, port: 8000 }

host:
  storage: { format: tiff, path: "./frames" }
  display: { fps: 30, color_map: gray }
```

### Git 저장소 구조

| 저장소 | 내용 | 주요 언어 |
|-------|------|----------|
| fpga | RTL 소스, 테스트벤치, 제약 파일 | SystemVerilog |
| fw | SoC 컨트롤러 펌웨어 | C/C++ |
| sdk | Host Detector SDK | C++/C# |
| tools | 시뮬레이터, GUI, 코드 생성기 | C# (.NET 8.0+) |
| config | detector_config.yaml, 스키마, 변환기 | YAML/JSON |
| docs | 아키텍처 문서, API 문서, 사용자 가이드 | Markdown |

## 위험 관리

### 주요 위험 요소

| ID | 위험 | 확률 | 영향 | 완화 방안 |
|----|------|------|------|----------|
| R-03 | FPGA 리소스 부족 | 낮음 | 높음 | 예상 사용률 34-58%, Artix-7 75T/100T 업그레이드 경로 확보 |
| R-04 | CSI-2 D-PHY 800M 디버깅 미완료 | 중간 | 높음 | 400M/lane 안정 검증 완료; 800M/lane 디버깅 중 (최종 목표 달성 필수) |
| R-12 | Host 링크 대역폭 부족 | 중간 | 높음 | P0 결정: 10 GbE 또는 목표 계층 축소 |

### FPGA 업그레이드 경로

XC7A35T가 개발 중 불충분할 경우 pin-compatible 업그레이드 가능:

| 디바이스 | LUTs | BRAMs | 패키지 호환 | 증가율 |
|---------|------|-------|-----------|-------|
| **XC7A35T** (현재) | 20,800 | 50 | FGG484 | 기준선 |
| XC7A50T | 32,600 | 75 | FGG484 | +57% LUTs |
| XC7A75T | 47,200 | 105 | FGG484 | +127% LUTs |
| XC7A100T | 63,400 | 135 | FGG484 | +205% LUTs |

## 기술 스택

### 하드웨어
- **FPGA**: Xilinx Artix-7 XC7A35T-FGG484
- **SoC SoM**: Variscite VAR-SOM-MX8M-PLUS (NXP i.MX8M Plus, Quad-core Cortex-A53)
- **인터페이스**: CSI-2 MIPI 4-lane D-PHY, SPI, 10 GbE / 2.5 GbE
- **WiFi/BT**: Ezurio Sterling 60 (QCA6174A, M.2)
- **Battery**: TI BQ40z50 (SMBus, I2C addr 0x0b)
- **IMU**: Bosch BMI160 (I2C7, addr 0x68)

### 소프트웨어
- **FPGA 개발**: AMD Vivado (synthesis + simulation)
- **시뮬레이션**: ModelSim / Questa
- **SoC 빌드**: Yocto Project Scarthgap (5.0 LTS), Variscite BSP imx-6.6.52-2.2.0-v1.3
- **SoC 커널**: Linux 6.6.52 (LTS)
- **SW 개발**: .NET 8.0+ C# (시뮬레이터, GUI), C/C++ (SoC 펌웨어)
- **버전 관리**: Gitea (6개 저장소)
- **CI/CD**: n8n webhooks + Gitea 통합
- **프로젝트 관리**: Redmine

### FPGA IP
- **AMD/Xilinx MIPI CSI-2 TX Subsystem IP** (Artix-7 호환, D-PHY via OSERDES+LVDS)

## 규정 및 보안 고려사항

의료 인증(FDA/CE)은 프로젝트 범위 밖이지만, 향후 비용 폭증 방지를 위한 기초 실천 사항:

| 실천 사항 | 조치 | 시기 |
|----------|------|------|
| 위험 등록부 | ISO 14971 호환 위험 등록부 프레임워크 수립 | M1 |
| 보안 SDLC | 기본 NIST SSDF 실천 적용 | M1 |
| 추적성 | 요구사항 → 설계 → 테스트 추적성 매트릭스 | M2+ |
| 데이터 무결성 | 모든 인터페이스 경계에서 비트 정확도 검증 | 지속적 |
| 감사 추적 | Git 기반 변경 추적 + 필수 코드 리뷰 | 지속적 |

## 시작하기

### 사전 요구사항

- .NET 8.0+ SDK
- AMD Vivado (FPGA 개발용)
- Git

### 저장소 클론

```bash
git clone <repository-url>
cd system-emul-sim
```

### 시뮬레이터 빌드 및 실행

```bash
cd tools
dotnet build
dotnet test
```

### 설정 파일 편집

```bash
cd config
# detector_config.yaml 파일을 편집하여 패널 및 시스템 파라미터 구성
```

## 문서

### 📚 핵심 문서
- **프로젝트 계획서**: [`X-ray_Detector_Optimal_Project_Plan.md`](X-ray_Detector_Optimal_Project_Plan.md) - 28주 전체 개발 계획
- **빠른 시작**: [`QUICKSTART.md`](QUICKSTART.md) - 빠른 시작 가이드
- **치트시트**: [`CHEATSHEET.md`](CHEATSHEET.md) - 초고속 참조

### 🏗️ 설계 문서
- **아키텍처**: [`docs/architecture/`](docs/architecture/) - 시스템/FPGA/SoC/Host SDK 설계
- **API 문서**: [`docs/api/`](docs/api/) - SPI/CSI-2/Ethernet/SDK API 레퍼런스
- **SPEC 문서**: [`.moai/specs/`](.moai/specs/) - EARS 포맷 요구사항 (FPGA/FW/SDK/SIM/TOOLS)
- **테스트 계획**: [`docs/testing/`](docs/testing/) - Unit/Integration/HIL/Verification 전략

### 📖 개발 가이드
- **개발 환경 설정**: [`docs/guides/development-setup.md`](docs/guides/development-setup.md)
- **FPGA 빌드 가이드**: [`docs/guides/fpga-build-guide.md`](docs/guides/fpga-build-guide.md)
- **펌웨어 빌드 가이드**: [`docs/guides/firmware-build-guide.md`](docs/guides/firmware-build-guide.md)
- **SDK 빌드 가이드**: [`docs/guides/sdk-build-guide.md`](docs/guides/sdk-build-guide.md)
- **시뮬레이터 빌드 가이드**: [`docs/guides/simulator-build-guide.md`](docs/guides/simulator-build-guide.md)
- **도구 사용 가이드**: [`docs/guides/tool-usage-guide.md`](docs/guides/tool-usage-guide.md)

### 🚀 배포 및 운영
- **설치 가이드**: [`docs/guides/installation-guide.md`](docs/guides/installation-guide.md)
- **배포 가이드**: [`docs/guides/deployment-guide.md`](docs/guides/deployment-guide.md)
- **사용자 매뉴얼**: [`docs/guides/user-manual.md`](docs/guides/user-manual.md)
- **문제 해결 가이드**: [`docs/guides/troubleshooting-guide.md`](docs/guides/troubleshooting-guide.md)

### 🎯 프로젝트 관리
- **WBS**: [`WBS.md`](WBS.md) - 작업 분류 체계 (8명 팀, W9-W28 Gantt, 리소스 매트릭스)
- **프로젝트 로드맵**: [`docs/project/roadmap.md`](docs/project/roadmap.md) - M0-M6 마일스톤, W1-W28 일정
- **용어집**: [`docs/project/glossary.md`](docs/project/glossary.md) - 기술 용어 정의
- **기여 가이드**: [`CONTRIBUTING.md`](CONTRIBUTING.md) - 개발 워크플로우 및 규칙
- **변경 이력**: [`CHANGELOG.md`](CHANGELOG.md) - 버전 히스토리

## 기여

본 프로젝트는 ABYZ-Lab-ADK 개발 방법론을 따릅니다:
- 코드 리뷰 필수
- TRUST 5 품질 프레임워크 준수
- TDD/DDD Hybrid 개발 방법론

자세한 내용은 [`CONTRIBUTING.md`](CONTRIBUTING.md)를 참조하세요.

## 라이선스

본 프로젝트는 독점 라이선스를 따릅니다. 자세한 내용은 [`LICENSE.md`](LICENSE.md)를 참조하세요.

## 연락처

프로젝트 문의: [연락처 정보 추가 필요]

---

*생성: ABYZ-Lab Agent Teams (researcher + analyst + architect)*
*기반 문서: X-ray_Detector_Dev_Plan_Final_v2.md + deep-research-report.md*
*FPGA 제약: Xilinx Artix-7 XC7A35T-FGG484*
*SoC: Variscite VAR-SOM-MX8M-PLUS | Yocto Scarthgap (5.0 LTS) | Linux 6.6.52*
*Phase 1 교차검증 완전 승인: 2026-02-17 (Critical 10건 + Major 10건 수정 완료)*
*업데이트: 2026-02-17 — 성능 계층 정규화, PoC 일정 수정, D-PHY 검증 상태 반영*
