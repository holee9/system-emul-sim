# Work Breakdown Structure (WBS)
# X-ray Detector Panel System

| 항목 | 내용 |
|------|------|
| 문서 버전 | v1.0.0 |
| 작성일 | 2026-02-17 |
| 작성자 | ABYZ-Lab |
| 상태 | Phase 2 진입 준비 완료 |
| 대상 기간 | W1-W28 (28주) |

---

## 1. 프로젝트 개요

### 1.1 목적

X-ray 검출기 패널에서 발생하는 고해상도 의료 영상 데이터를 실시간으로 수집, 전송, 처리하는 계층형 임베디드 시스템을 설계 및 구현한다.

- **최종 목표**: 3072×3072, 16-bit, 15fps 영상 획득 (목표 처리량 2.26 Gbps)
- **개발 기준선**: 2048×2048, 16-bit, 15fps (400 Mbps/lane 안정 검증 완료)
- **검증된 인터페이스**: FPGA → SoC (CSI-2 MIPI 4-lane), SoC → Host (10 GbE)

### 1.2 개발 단계 요약

| 단계 | 기간 | 내용 | 상태 |
|------|------|------|------|
| Phase 1: 문서화 | W1-W8 | 모든 SPEC, 아키텍처, API 문서 완료 | ✅ COMPLETE |
| Phase 2: 구현 | W9-W22 | 시뮬레이터, SDK, FPGA RTL, FW 개발 | 진입 예정 |
| Phase 3: HW 검증 | W23-W28 | CSI-2 PoC, HIL 테스트, 최종 검증 | 대기 |

### 1.3 확정 기술 상수

```
프레임 헤더 매직:  0xD7E01234
RAW16 타입 코드:   0x2E
DEVICE_ID:        0xD7E0_0001
CRC 위치:         offset 28, CRC-16/CCITT
타임스탬프:       TimestampNs (나노초)
D-PHY:           400 Mbps/lane (안정) / 800 Mbps/lane (디버깅 중)
```

---

## 2. 팀 구성

### 2.1 역할 및 책임

| 역할 ID | 역할명 | 인원 | 담당 SPEC | 주요 기술 |
|---------|--------|------|-----------|----------|
| FPGA-ENG | FPGA 설계 엔지니어 | 1 | SPEC-FPGA-001 | SystemVerilog, Artix-7, Vivado |
| FW-ENG | iMX8MP 펌웨어 엔지니어 | 1 | SPEC-FW-001 | C/C++, Yocto Scarthgap, Linux 6.6 |
| SW1 | SW 개발자 1 | 1 | SPEC-SIM-001, SPEC-TOOLS-001(일부) | C# .NET 8.0 |
| SW2 | SW 개발자 2 | 1 | SPEC-SDK-001, SPEC-TOOLS-001(일부) | C# .NET 8.0 |
| GUI-ENG | GUI 디자인 엔지니어 | 1 | SPEC-TOOLS-001(GUI) | WPF, C# .NET 8.0 |
| TESTER | 테스터 | 1 | 전체 SPEC | xUnit, 통합 테스트 |
| RA1 | RA 검토자 1 | 1 | 게이트 리뷰 | 요구사항/아키텍처 검토 |
| RA2 | RA 검토자 2 | 1 | 게이트 리뷰 | 요구사항/아키텍처 검토 |

### 2.2 SW1/SW2 SPEC-TOOLS-001 분담

| 담당자 | 작업 범위 | 기간 |
|--------|----------|------|
| SW1 | ConfigConverter (YAML→XDC/DTS/JSON) + CodeGenerator (RTL/C 헤더/C# 템플릿) | W18-W21 |
| SW2 | ParameterExtractor (PDF 파싱, 규칙 엔진) + IntegrationRunner CLI | W18-W22 |
| GUI-ENG | GUI.Application WPF (프레임 미리보기, 상태 모니터링) | W19-W22 |

---

## 3. WBS 계층 구조

```
1. X-ray Detector Panel System
   1.1 Phase 1: 문서화 (W1-W8) [완료]
   1.2 Phase 2: 구현 (W9-W22)
       1.2.1 SPEC-FPGA-001: FPGA RTL 개발
       1.2.2 SPEC-FW-001: SoC 펌웨어 개발
       1.2.3 SPEC-SIM-001: 시뮬레이터 Suite
       1.2.4 SPEC-SDK-001: Host SDK
       1.2.5 SPEC-TOOLS-001: 도구 모음
       1.2.6 품질 보증 (테스트 + 코드 리뷰)
   1.3 Phase 3: HW 검증 (W23-W28)
       1.3.1 SPEC-POC-001: CSI-2 PoC
       1.3.2 HIL 테스트
       1.3.3 최종 시스템 검증
```

---

## 4. 단계별 상세 WBS 테이블

### 4.1 SPEC-FPGA-001 — FPGA RTL 개발 (FPGA-ENG)

| WBS ID | 작업명 | 담당자 | 기간 | 산출물 | 선행작업 | 상태 |
|--------|--------|--------|------|--------|----------|------|
| 2.1.1 | Clock/Reset 관리자 RTL | FPGA-ENG | W9 | clock_reset_manager.sv | M1-Doc | 대기 |
| 2.1.2 | SPI Slave 인터페이스 RTL | FPGA-ENG | W9-W10 | spi_slave_interface.sv | 2.1.1 | 대기 |
| 2.1.3 | RTL 검증 프레임워크 구성 | FPGA-ENG | W10-W11 | tb_framework.sv, sim_setup | 2.1.1 | 대기 |
| 2.1.4 | Panel Scan FSM 구현 | FPGA-ENG | W11-W13 | panel_scan_fsm.sv | 2.1.2 | 대기 |
| 2.1.5 | Line Buffer (BRAM 기반) | FPGA-ENG | W12-W14 | line_buffer.sv | 2.1.1 | 대기 |
| 2.1.6 | FSM-Buffer 통합 검증 | FPGA-ENG | W13-W14 | tb_integration.sv | 2.1.4, 2.1.5 | 대기 |
| 2.1.7 | CSI-2 TX Wrapper RTL | FPGA-ENG | W13-W16 | csi2_tx_wrapper.sv | 2.1.6 | 대기 |
| 2.1.8 | D-PHY 출력 드라이버 | FPGA-ENG | W15-W17 | dphy_output.sv | 2.1.7 | 대기 |
| 2.1.9 | 전체 파이프라인 통합 | FPGA-ENG | W16-W17 | pipeline_top.sv | 2.1.8 | 대기 |
| 2.1.10 | 보호 로직 (ECC, OVF) | FPGA-ENG | W16-W18 | protection_logic.sv | 2.1.9 | 대기 |
| 2.1.11 | 안전 상태 머신 | FPGA-ENG | W17-W19 | safe_state_fsm.sv | 2.1.10 | 대기 |
| 2.1.12 | 시스템 검증 (FV-01~11) | FPGA-ENG | W18-W19 | fv_report.md | 2.1.11 | 대기 |
| 2.1.13 | 리소스 최적화 (LUT <60%) | FPGA-ENG | W19-W21 | vivado_utilization.rpt | 2.1.12 | 대기 |
| 2.1.14 | 디버그 포트 구성 | FPGA-ENG | W20-W21 | debug_ila.xdc | 2.1.13 | 대기 |
| 2.1.15 | HIL 준비 (비트스트림) | FPGA-ENG | W21-W22 | top.bit, hil_testbench.sv | 2.1.14 | 대기 |

**게이트**: FV-01~FV-11 통과, Coverage Line≥95% / Branch≥90% / FSM 100%

---

### 4.2 SPEC-FW-001 — SoC 펌웨어 개발 (FW-ENG)

| WBS ID | 작업명 | 담당자 | 기간 | 산출물 | 선행작업 | 상태 |
|--------|--------|--------|------|--------|----------|------|
| 2.2.1 | HAL: SPI Master 드라이버 | FW-ENG | W9-W10 | spi_master.c/h | M1-Doc | 대기 |
| 2.2.2 | HAL: CSI-2 RX V4L2 드라이버 | FW-ENG | W9-W11 | csi2_rx_v4l2.c/h | M1-Doc | 대기 |
| 2.2.3 | HAL: Ethernet TX 드라이버 | FW-ENG | W10-W11 | ethernet_tx.c/h | M1-Doc | 대기 |
| 2.2.4 | CRC-16/CCITT 구현 | FW-ENG | W10 | crc16.c/h | M1-Doc | 대기 |
| 2.2.5 | Sequence Engine | FW-ENG | W11-W13 | sequence_engine.c/h | 2.2.1 | 대기 |
| 2.2.6 | Frame Manager (링 버퍼) | FW-ENG | W12-W14 | frame_manager.c/h | 2.2.2 | 대기 |
| 2.2.7 | 설정 관리자 (YAML 파서) | FW-ENG | W12-W13 | config_manager.c/h | 2.2.4 | 대기 |
| 2.2.8 | Command Protocol 파서 | FW-ENG | W13-W14 | cmd_protocol.c/h | 2.2.5 | 대기 |
| 2.2.9 | 데몬 통합 (systemd 서비스) | FW-ENG | W14-W16 | detector_daemon.c | 2.2.6, 2.2.8 | 대기 |
| 2.2.10 | Health Monitor | FW-ENG | W15-W17 | health_monitor.c/h | 2.2.9 | 대기 |
| 2.2.11 | Battery/IMU/GPIO 드라이버 | FW-ENG | W15-W17 | bq40z50.c, bmi160.c, pca9534.c | M1-Doc | 대기 |
| 2.2.12 | FW-IT-01: SPI 기능 검증 | FW-ENG | W18-W19 | fw_it_01_report.md | 2.2.8 | 대기 |
| 2.2.13 | FW-IT-02: CSI-2 RX 검증 | FW-ENG | W18-W20 | fw_it_02_report.md | 2.2.6 | 대기 |
| 2.2.14 | FW-IT-03~05: 통합 검증 | FW-ENG | W19-W21 | fw_it_03-05_report.md | 2.2.12, 2.2.13 | 대기 |
| 2.2.15 | Yocto 빌드 최종 검증 | FW-ENG | W21-W22 | bitbake_log.txt, image.wic | 2.2.14 | 대기 |

**게이트**: SPEC-FW-001 REQ-FW-001~152 전체 구현, Yocto bitbake 빌드 성공

---

### 4.3 SPEC-SIM-001 — 시뮬레이터 Suite (SW1)

| WBS ID | 작업명 | 담당자 | 기간 | 산출물 | 선행작업 | 상태 |
|--------|--------|--------|------|--------|----------|------|
| 2.3.1 | Common.Dto 공통 데이터 모델 | SW1 | W9 | Common.Dto.cs | M1-Doc | 대기 |
| 2.3.2 | ISimulator 인터페이스 정의 | SW1 | W9 | ISimulator.cs | 2.3.1 | 대기 |
| 2.3.3 | Config Loader (YAML) | SW1 | W9 | ConfigLoader.cs | 2.3.1 | 대기 |
| **G-DTO** | **[게이트] Common.Dto API 동결** | **RA1+RA2** | **W9말** | **API 동결 승인** | **2.3.1-3** | **대기** |
| 2.3.4 | PanelSimulator: 패턴 생성기 | SW1 | W9-W10 | PanelSimulator.Patterns.cs | G-DTO | 대기 |
| 2.3.5 | PanelSimulator: 노이즈/결함 모델 | SW1 | W10-W11 | PanelSimulator.Noise.cs | 2.3.4 | 대기 |
| 2.3.6 | FpgaSimulator 완성 (DDD 기반) | SW1 | W10-W13 | FpgaSimulator.cs | G-DTO | 대기 |
| 2.3.7 | McuSimulator: SPI Master | SW1 | W13-W14 | McuSimulator.Spi.cs | G-DTO | 대기 |
| 2.3.8 | McuSimulator: CSI-2 RX + UDP TX | SW1 | W14-W15 | McuSimulator.Csi2.cs | 2.3.7 | 대기 |
| 2.3.9 | HostSimulator: UDP RX + 재조립 | SW1 | W15-W16 | HostSimulator.cs | 2.3.8 | 대기 |
| 2.3.10 | HostSimulator: 파일 출력 | SW1 | W16-W17 | HostSimulator.FileOutput.cs | 2.3.9 | 대기 |
| 2.3.11 | 통합 성능 검증 (IT-01~10 지원) | SW1 | W17-W19 | sim_perf_report.md | 2.3.10 | 대기 |

**게이트**: IT-01~IT-10 지원 완료, xUnit Coverage ≥85%

---

### 4.4 SPEC-SDK-001 — Host SDK (SW2)

| WBS ID | 작업명 | 담당자 | 기간 | 산출물 | 선행작업 | 상태 |
|--------|--------|--------|------|--------|----------|------|
| 2.4.1 | PacketReceiver (UDP 수신) | SW2 | W9-W10 | PacketReceiver.cs | G-DTO | 대기 |
| 2.4.2 | FrameReassembler | SW2 | W10-W11 | FrameReassembler.cs | 2.4.1 | 대기 |
| 2.4.3 | Network 프로토콜 계층 | SW2 | W9-W11 | NetworkProtocol.cs | G-DTO | 대기 |
| **G-PROTO** | **[게이트] UDP 프로토콜 동결** | **FW-ENG+SW2+RA** | **W13** | **프로토콜 동결 승인** | **2.4.3** | **대기** |
| 2.4.4 | IDetectorClient API (28개) | SW2 | W11-W13 | IDetectorClient.cs | G-PROTO | 대기 |
| 2.4.5 | Connection Manager | SW2 | W12-W13 | ConnectionManager.cs | 2.4.4 | 대기 |
| 2.4.6 | Acquisition Controller | SW2 | W13-W14 | AcquisitionController.cs | 2.4.5 | 대기 |
| 2.4.7 | Storage: TIFF/RAW 출력 | SW2 | W13-W14 | StorageWriter.cs | 2.4.4 | 대기 |
| 2.4.8 | Display Utilities (헬퍼) | SW2 | W14-W15 | DisplayHelper.cs | 2.4.4 | 대기 |
| 2.4.9 | 품질 게이트 + 성능 최적화 | SW2 | W15-W16 | perf_benchmark.md | 2.4.6, 2.4.7 | 대기 |
| 2.4.10 | SDK 통합 검증 | SW2 | W16-W17 | sdk_integration_report.md | 2.4.9 | 대기 |

**게이트**: IDetectorClient API 28개 완성, xUnit Coverage ≥85%

---

### 4.5 SPEC-TOOLS-001 — 도구 모음 (SW1+SW2+GUI-ENG)

| WBS ID | 작업명 | 담당자 | 기간 | 산출물 | 선행작업 | 상태 |
|--------|--------|--------|------|--------|----------|------|
| 2.5.1 | ConfigConverter: YAML→XDC | SW1 | W18-W19 | ConfigConverter.Xdc.cs | 2.3.11 | 대기 |
| 2.5.2 | ConfigConverter: YAML→DTS | SW1 | W19-W20 | ConfigConverter.Dts.cs | 2.5.1 | 대기 |
| 2.5.3 | ConfigConverter: YAML→JSON | SW1 | W19-W20 | ConfigConverter.Json.cs | 2.5.1 | 대기 |
| 2.5.4 | CodeGenerator: RTL 헤더 생성 | SW1 | W19-W21 | CodeGenerator.Rtl.cs | 2.5.2 | 대기 |
| 2.5.5 | CodeGenerator: C 헤더 생성 | SW1 | W20-W21 | CodeGenerator.C.cs | 2.5.2 | 대기 |
| 2.5.6 | CodeGenerator: C# 클래스 생성 | SW1 | W20-W21 | CodeGenerator.Cs.cs | 2.5.3 | 대기 |
| 2.5.7 | ParameterExtractor: PDF 파서 | SW2 | W19-W20 | ParameterExtractor.Parser.cs | 2.4.10 | 대기 |
| 2.5.8 | ParameterExtractor: 규칙 엔진 | SW2 | W20-W21 | ParameterExtractor.Rules.cs | 2.5.7 | 대기 |
| 2.5.9 | GUI.Application: WPF 메인 창 | GUI-ENG | W19-W20 | MainWindow.xaml | M1-Doc | 대기 |
| 2.5.10 | GUI.Application: 프레임 미리보기 | GUI-ENG | W20-W21 | FramePreviewControl.xaml | 2.5.9 | 대기 |
| 2.5.11 | GUI.Application: 상태 패널 | GUI-ENG | W21-W22 | StatusPanel.xaml | 2.5.9 | 대기 |
| 2.5.12 | IntegrationRunner CLI (IT-01~10) | SW2 | W20-W22 | IntegrationRunner.cs | 2.5.8 | 대기 |

**게이트**: xUnit Coverage ≥85%, IT-01~IT-10 CLI 자동화 완료

---

### 4.6 품질 보증 활동 (TESTER + RA1 + RA2)

| WBS ID | 작업명 | 담당자 | 기간 | 산출물 | 선행작업 | 상태 |
|--------|--------|--------|------|--------|----------|------|
| 2.6.1 | 단위 테스트 프레임워크 구성 | TESTER | W9 | xunit.runner.json, test_setup | M1-Doc | 대기 |
| 2.6.2 | 공통 기반 설계 리뷰 (DTO/UDP) | RA1+RA2 | W9 (3일) | review_report_w9.md | 2.3.1-3 | 대기 |
| 2.6.3 | TDD 지원 (SW1/SW2 협업) | TESTER | W9-W14 | 단위 테스트 파일들 | 2.6.1 | 대기 |
| 2.6.4 | [게이트 리뷰] G-DTO 승인 | RA1+RA2 | W9말 | gate_dto_approval.md | 2.6.2 | 대기 |
| 2.6.5 | 지속적 코드 리뷰 (PR 리뷰) | RA1+RA2 | W9-W22 | PR 리뷰 기록 | 지속 | 대기 |
| 2.6.6 | [게이트 리뷰] G-PROTO 승인 | RA1+RA2 | W13 | gate_proto_approval.md | 2.4.3 | 대기 |
| 2.6.7 | IT-01~IT-06 작성 및 실행 | TESTER | W14-W18 | it_01-06_results.md | 2.3.6, 2.4.6 | 대기 |
| 2.6.8 | SIM+SDK 중간 완성도 검토 | RA1+RA2 | W16 | mid_review_w16.md | 2.3.9, 2.4.9 | 대기 |
| 2.6.9 | IT-07~IT-10 작성 및 실행 | TESTER | W18-W21 | it_07-10_results.md | 2.6.7 | 대기 |
| 2.6.10 | [게이트 리뷰] W19 시뮬레이터 완성 | RA1+RA2 | W19 | gate_sim_w19.md | 2.3.11 | 대기 |
| 2.6.11 | HIL 테스트 하네스 준비 | TESTER | W21-W23 | hil_harness.md | 2.6.9 | 대기 |
| 2.6.12 | [게이트 리뷰] M2-Impl 최종 승인 | RA1+RA2 | W22 | m2_impl_approval.md | 2.1.15, 2.2.15 | 대기 |

---

### 4.7 SPEC-POC-001 — CSI-2 PoC (Phase 3, W23-W26)

| WBS ID | 작업명 | 담당자 | 기간 | 산출물 | 선행작업 | 상태 |
|--------|--------|--------|------|--------|----------|------|
| 3.1.1 | HW 셋업 및 JTAG 연결 | FPGA-ENG+FW-ENG | W23 (3일) | hw_setup_checklist.md | M2-Impl | 대기 |
| 3.1.2 | SoC 부팅 및 SPI 기본 제어 검증 | FW-ENG | W23 | spi_basic_test.md | 3.1.1 | 대기 |
| 3.1.3 | FPGA CSI-2 TX IP 검증 | FPGA-ENG | W24 (4일) | csi2_tx_test.md | 3.1.1 | 대기 |
| 3.1.4 | 테스트 패턴 생성 (컬러바) | FPGA-ENG | W24 | test_pattern.sv | 3.1.3 | 대기 |
| 3.1.5 | SoC CSI-2 RX 수신 검증 | FW-ENG | W25 (4일) | csi2_rx_capture.md | 3.1.4 | 대기 |
| 3.1.6 | 프레임 캡처 및 데이터 검증 | FW-ENG+TESTER | W25 | frame_validation.md | 3.1.5 | 대기 |
| 3.1.7 | 처리량 측정 (400M/800M) | FPGA-ENG+FW-ENG | W25-W26 | throughput_report.md | 3.1.6 | 대기 |
| 3.1.8 | Lane Speed 특성화 | FPGA-ENG | W26 | lane_characterization.md | 3.1.7 | 대기 |
| 3.1.9 | 신호 무결성 검증 (오실로스코프) | FPGA-ENG | W26 (2일) | signal_integrity.md | 3.1.8 | 대기 |
| 3.1.10 | PoC 보고서 작성 | FPGA-ENG+RA1 | W26 (2일) | poc_report.md | 3.1.9 | 대기 |
| 3.1.11 | GO/NO-GO 결정 | RA1+RA2 | W26말 | go_nogo_decision.md | 3.1.10 | 대기 |

**게이트**: 목표 처리량 ≥70% 달성 (2.26 Gbps × 70% = 1.58 Gbps)

---

### 4.8 최종 시스템 검증 (Phase 3, W26-W28)

| WBS ID | 작업명 | 담당자 | 기간 | 산출물 | 선행작업 | 상태 |
|--------|--------|--------|------|--------|----------|------|
| 3.2.1 | HIL 테스트 실행 (전체) | TESTER | W26-W27 | hil_results.md | M0.5-PoC | 대기 |
| 3.2.2 | 실제 패널 연결 준비 | FPGA-ENG+FW-ENG | W27 | panel_integration.md | 3.2.1 | 대기 |
| 3.2.3 | 실제 패널 프레임 획득 시도 | 전체 팀 | W28 | real_frame_samples | 3.2.2 | 대기 |
| 3.2.4 | 최종 시스템 검증 보고서 | RA1+RA2 | W28 | final_report.md | 3.2.3 | 대기 |

---

## 5. 주간 Gantt 캘린더 (W9-W28)

```
역할        W9  W10 W11 W12 W13 W14 W15 W16 W17 W18 W19 W20 W21 W22 W23 W24 W25 W26 W27 W28
FPGA-ENG   [P1--][P1][P2--P2][P3------P3][P4----P4][P5--P5]              [POC-----------]
FW-ENG     [P1--P1][P2----------P2][P3--------P3][IT--------IT]          [POC-----------]
SW1        [Sim-Phase1][Sim-P2---][Sim-P3][Sim-P4][Sim-P5][Sim-P6][TOOL-P1--P2]
SW2        [SDK-Phase1---][SDK-P2][SDK-P3][SDK-P4]            [TOOL-P3--][TOOL-P5-]
GUI-ENG    [               (대기)               ][TOOL-GUI---------]
TESTER     [UT-Framework][TDD support    ][IT-01~06---][IT-07~10][HIL prep][HIL  ][Final]
RA1+RA2    [Review][PR review (continuous)    ][G-reviews ][M2][         PoC+Final      ]
```

**범례**: `[P1]`=Phase 1, `[IT]`=통합 테스트, `[HIL]`=HW in the Loop, `[POC]`=PoC 수행

---

## 6. 마일스톤 테이블

| ID | 마일스톤명 | 주차 | 날짜(예상) | 게이트 조건 | 상태 |
|----|-----------|------|-----------|-----------|------|
| M0 | P0 결정 확정 | W1 | 2025-09-22 | HW 제약 확정, 인터페이스 결정 | ✅ COMPLETE |
| M1-Doc | Phase 1 문서 완료 | W8 | 2026-02-17 | 7개 SPEC 승인 완료 | ✅ COMPLETE |
| G-DTO | Common.Dto API 동결 | W9말 | 2026-02-27 | SW1+SW2+RA 서명 승인 | 대기 |
| G-PROTO | UDP 프로토콜 동결 | W13 | 2026-03-27 | FW-ENG+SW2+RA 서명 승인 | 대기 |
| G-SIM-MID | SIM+SDK 중간 검토 | W16 | 2026-04-17 | 핵심 기능 ≥70% 완성 | 대기 |
| G-W19 | 시뮬레이터 완성 + RTL 검토 | W19 | 2026-05-08 | IT-01~06 통과, RTL Phase 4 완료 | 대기 |
| M2-Impl | 구현 완료 | W22 | 2026-05-29 | 227개 요구사항, Coverage ≥85%, IT-01~10 | 대기 |
| M0.5-PoC | CSI-2 PoC | W26 | 2026-06-26 | 목표 처리량 ≥70% (≥1.58 Gbps) 측정 | 대기 |
| M6-Final | 최종 시스템 검증 | W28 | 2026-07-10 | 실제 패널 프레임 획득 성공 | 대기 |

---

## 7. 리소스 할당 매트릭스

주간 각 역할 투입 현황 (● = 주담당, ○ = 부담당/지원, — = 미투입)

| 역할 | W9 | W10 | W11 | W12 | W13 | W14 | W15 | W16 | W17 | W18 | W19 | W20 | W21 | W22 | W23 | W24 | W25 | W26 | W27 | W28 |
|------|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|----|
| FPGA-ENG | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ○ |
| FW-ENG | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ○ |
| SW1 | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | — | — | — | — | — | — | — |
| SW2 | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | — | — | — | — | — | — |
| GUI-ENG | — | — | — | — | — | — | — | — | — | — | ● | ● | ● | ● | — | — | — | — | — | — |
| TESTER | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● | ● |
| RA1 | ● | ○ | ○ | ○ | ● | ○ | ○ | ● | ○ | ○ | ● | ○ | ○ | ● | ○ | ○ | ○ | ● | ○ | ● |
| RA2 | ● | ○ | ○ | ○ | ● | ○ | ○ | ● | ○ | ○ | ● | ○ | ○ | ● | — | — | — | ● | — | ● |

---

## 8. 병렬 구현 조건 및 게이트

### 8.1 병렬 구현 가능 조건

```
W9 시작 조건 (동시 병렬 시작):
  ✅ M1-Doc 완료 (2026-02-17 확인)
  ✅ G-DTO 사전 설계 완료 (Common.Dto 초안)

W9 동시 착수 가능 작업:
  FPGA-ENG → 2.1.1 Clock/Reset
  FW-ENG   → 2.2.1 HAL SPI + 2.2.2 HAL CSI-2
  SW1      → 2.3.1 Common.Dto (→ G-DTO 게이트 선행)
  SW2      → 2.4.1 PacketReceiver (G-DTO 후 진행)
  TESTER   → 2.6.1 테스트 프레임워크
  RA1+RA2  → 2.6.2 공통 기반 설계 리뷰
```

### 8.2 순차 의존성 체인

```
[G-DTO] ────→ SW2: SDK 개발 착수 (W9말)
                └→ FW-ENG: Command Protocol 확정
[G-PROTO] ──→ FW-ENG: 프로토콜 구현 확정 (W13)
                └→ TESTER: IT-01~06 시나리오 확정
[M2-Impl] ──→ SPEC-POC-001 착수 (W23)
[M0.5-PoC] → HIL 최종 실행 (W26~)
```

### 8.3 게이트 조건 상세

| 게이트 | 담당 | 통과 조건 | 실패 시 조치 |
|--------|------|----------|------------|
| G-DTO | RA1+RA2 | Common.Dto 전체 필드 정의, 마법 상수 포함, SW1+SW2 합의 | W10까지 재설계 |
| G-PROTO | RA1+FW-ENG | UDP 패킷 포맷 확정, CRC 위치(offset 28) 명시 | W14까지 프로토콜 수정 |
| G-SIM-MID | RA1+RA2 | SIM 핵심 모듈 ≥70%, SDK API 20개 이상 | 2주 연장 검토 |
| G-W19 | RA1+RA2 | IT-01~06 통과, RTL Phase 3 완료, Coverage ≥80% | RA+팀장 긴급 검토 |
| M2-Impl | RA1+RA2 | 모든 IT 통과, 전체 Coverage ≥85% | Phase 3 연기 |
| M0.5-PoC | RA1+전체 | 실측 처리량 ≥1.58 Gbps | NO-GO: 2048×2048 기준선으로 전환 |

---

## 9. 위험 및 의존성

### 9.1 주요 위험 요소

| 위험 ID | 위험 내용 | 확률 | 영향 | 완화 방안 |
|---------|----------|------|------|----------|
| R-800M | 800 Mbps/lane 디버깅 미완료 | 중 | 높음 | 400M(2048×2048@15fps)으로 최종 목표 하향 |
| R-BRAM | Line Buffer BRAM 50개 초과 | 중 | 높음 | 외부 SRAM 사용 또는 해상도 제한 |
| R-LUT | LUT 60% 초과 (목표 <60%) | 저 | 중간 | 기능 분할 또는 Artix-7 200T 대체 |
| R-BSP | Yocto BSP 패치 실패 | 저 | 중간 | Variscite 공식 지원 요청, 빌드 캐시 |
| R-GATE | G-DTO 합의 지연 | 저 | 높음 | W8에 사전 초안 작성 (M1-Doc 기간 중) |
| R-HIL | HIL 하네스 준비 부족 | 저 | 중간 | TESTER W21부터 조기 착수 |

### 9.2 핵심 외부 의존성

| 의존성 | 내용 | 확보 상태 |
|--------|------|----------|
| Xilinx Vivado | FPGA 합성/구현 도구 | ✅ 라이선스 보유 |
| Questa/ModelSim | RTL 시뮬레이션 | ✅ 라이선스 보유 |
| Artix-7 XC7A35T 평가보드 | FPGA PoC 하드웨어 | ✅ 보유 |
| NXP i.MX8M Plus EVK | SoC 검증 보드 | ✅ 보유 |
| Variscite VAR-SOM-MX8M-PLUS | 실 양산 SoM | ✅ BSP 지원 |
| .NET 8.0 SDK | SW/도구 개발 환경 | ✅ 무료 |
| 실제 X-ray 패널 | M6-Final 최종 검증 | W28 조달 필요 |

### 9.3 기술적 제약 사항 (변경 불가)

- **FPGA**: Artix-7 XC7A35T (LUT 20,800 / BRAM 50), Logic Cell 33,280
- **인터페이스**: FPGA → SoC = CSI-2 MIPI 4-lane D-PHY 전용
- **SoC → Host**: 10 GbE 권장 (1 GbE는 최소 계층만 지원)
- **제어**: SPI 최대 50 MHz
- **OS**: Yocto Project Scarthgap 5.0 LTS / Linux kernel 6.6.52

---

## 10. 커버리지 및 품질 목표

| 구성 요소 | Line | Branch | FSM | 비고 |
|-----------|------|--------|-----|------|
| FPGA RTL | ≥95% | ≥90% | 100% | ModelSim 기준 |
| SoC 펌웨어 | ≥85% | — | — | gcov/lcov 기준 |
| C# 시뮬레이터 | ≥85% | — | — | xUnit + Coverlet |
| C# SDK | ≥85% | — | — | xUnit + Coverlet |
| C# 도구 | ≥85% | — | — | xUnit + Coverlet |

**TRUST 5 게이트**: Tested / Readable / Unified / Secured / Trackable — 모든 커밋에 적용

---

*문서 끝 — WBS v1.0.0*
