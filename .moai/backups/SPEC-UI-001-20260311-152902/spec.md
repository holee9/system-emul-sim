---
id: SPEC-UI-001
version: 1.0.0
status: approved
created: 2026-03-11
updated: 2026-03-11
author: MoAI (manager-spec)
priority: high
milestone: M5
parent: SPEC-TOOLS-001
tags: [GUI, WPF, MVVM, Pipeline, Emulator, Integration]
---

## HISTORY

| Version | Date       | Author               | Description                          |
|---------|------------|----------------------|--------------------------------------|
| 1.0.0   | 2026-03-11 | MoAI (manager-spec)  | Initial SPEC creation                |

---

## Overview

### Scope

GUI.Application을 Host 전용 뷰어에서 4계층 통합 에뮬레이터 제어 센터로 확장한다.
현재 GUI는 SimulatedDetectorClient를 통해 독립적으로 생성된 프레임만 표시하지만,
이 SPEC은 실제 Panel -> FPGA/CSI-2 -> MCU/UDP -> NetworkChannel -> Host 파이프라인을
in-memory로 실행하고 제어할 수 있는 통합 GUI를 정의한다.

### Parent SPEC

SPEC-TOOLS-001 (기존 GUI.Application)

### New File/Project Locations

기존 `tools/GUI.Application/` 프로젝트 내에서 확장한다.
새 파일은 Services, ViewModels, Views 디렉터리에 추가된다.

### Traceability

- Parent: SPEC-TOOLS-001
- Related: SPEC-EMUL-003 (시나리오 검증), SPEC-INTSIM-001 (통합 시뮬레이션)

---

## Module 1: 에뮬레이터 모드 선택 (Emulator Mode Selection)

### REQ-UI-010: 모드 전환

**WHEN** 사용자가 에뮬레이터 모드를 선택하면
**THEN** GUI는 SimulatedDetectorClient(기본값)와 PipelineDetectorClient 간 전환을 수행한다.

- 기본 모드: SimulatedDetectorClient (기존 동작 유지)
- Pipeline 모드: PipelineDetectorClient (4계층 in-memory 파이프라인)
- 모드 전환은 파이프라인 중지 상태에서만 허용된다

### REQ-UI-011: PipelineDetectorClient

시스템은 **항상** PipelineDetectorClient가 IDetectorClient 인터페이스를 구현해야 한다.

- SimulatorPipeline을 in-memory로 래핑한다
- ConnectAsync, StartAcquisitionAsync, StopAcquisitionAsync, DisconnectAsync 구현
- FrameReceived 이벤트를 통해 처리된 프레임을 전달한다
- Background Task로 프레임 생성 루프를 실행한다
- CancellationToken을 통한 생명주기 관리를 제공한다
- Thread-safe하며 configurable fps를 지원한다

### REQ-UI-012: 모드 전환 시 기존 ViewModel 영향 없음

**WHEN** 에뮬레이터 모드가 전환되면
**THEN** 기존 FramePreviewViewModel 및 StatusViewModel은 IDetectorClient 인터페이스를 통해 동작하므로 변경 없이 정상 동작한다.

---

## Module 2: 시뮬레이터 제어판 (Simulator Control Panel -- Tab 3)

### REQ-UI-020: Panel 에뮬레이터 파라미터 제어

**IF** Pipeline Emulation 모드가 활성화된 상태에서
**THEN** 사용자는 다음 Panel 파라미터를 제어할 수 있다:

| Parameter    | Type     | Range/Options                                  |
|-------------|----------|-------------------------------------------------|
| kVp         | numeric  | 40-150                                          |
| mAs         | numeric  | positive float                                  |
| noise       | enum     | none / gaussian / composite                     |
| testPattern | enum     | Counter / Checkerboard / FlatField              |
| fidelity    | enum     | Low / Medium / High                             |
| seed        | integer  | any integer                                     |
| defectRate  | numeric  | 0.0-1.0                                        |

### REQ-UI-021: FPGA 에뮬레이터 구성

**IF** Pipeline Emulation 모드가 활성화된 상태에서
**THEN** 사용자는 다음 FPGA 파라미터를 구성할 수 있다:

| Parameter       | Type    | Description             |
|----------------|---------|--------------------------|
| csi2Lanes       | integer | CSI-2 레인 수            |
| csi2DataRateMbps| numeric | CSI-2 데이터 레이트 (Mbps)|
| lineBufferDepth | integer | 라인 버퍼 깊이           |

### REQ-UI-022: MCU 에뮬레이터 구성 및 버퍼 상태 표시

**IF** Pipeline Emulation 모드가 활성화된 상태에서
**THEN** 사용자는 다음 MCU 파라미터를 구성할 수 있다:

| Parameter       | Type    | Range    | Description      |
|----------------|---------|----------|-------------------|
| frameBufferCount| integer | 1-8      | 프레임 버퍼 수    |
| udpPort         | integer | 1024-65535| UDP 포트          |
| ethernetPort    | integer | 1024-65535| Ethernet 포트     |

**AND** MCU 섹션에서 각 버퍼의 상태(Free/Filling/Ready/Sending)를 실시간으로 표시한다.

### REQ-UI-023: 네트워크 채널 결함 주입

**IF** Pipeline Emulation 모드가 활성화된 상태에서
**THEN** 사용자는 다음 네트워크 채널 파라미터를 제어할 수 있다:

| Parameter      | Type    | Range    | Description        |
|---------------|---------|----------|--------------------|
| packetLossRate | numeric | 0.0-1.0  | 패킷 손실률        |
| reorderRate    | numeric | 0.0-1.0  | 패킷 재정렬률      |
| corruptionRate | numeric | 0.0-1.0  | 패킷 오염률        |
| minDelayMs     | integer | >= 0     | 최소 지연 (ms)     |
| maxDelayMs     | integer | >= minDelay | 최대 지연 (ms)  |

### REQ-UI-024: 파이프라인 Start/Stop/Reset

**WHEN** 사용자가 Start 버튼을 누르면 **THEN** 파이프라인이 현재 구성으로 실행을 시작한다.
**WHEN** 사용자가 Stop 버튼을 누르면 **THEN** 파이프라인이 정상 중지된다.
**WHEN** 사용자가 Reset 버튼을 누르면 **THEN** 모든 파라미터가 기본값으로 초기화되고 통계가 리셋된다.

---

## Module 3: 파이프라인 모니터 (Pipeline Monitor -- Tab 4)

### REQ-UI-030: 계층별 처리량 통계

**WHILE** 파이프라인이 실행 중인 상태에서
**THEN** 시스템은 각 계층(Panel, FPGA, MCU, Host)별로 다음 통계를 표시한다:

- 처리된 프레임 수 (FramesProcessed)
- 실패한 프레임 수 (FramesFailed)
- 평균 처리 시간 (AvgProcessingTimeMs)

### REQ-UI-031: 계층별 상태 표시

**WHILE** 파이프라인이 실행 중인 상태에서
**THEN** 시스템은 각 계층의 상태를 색상 인디케이터로 표시한다:

- Green: 정상 동작
- Yellow: 경고 (처리 지연 또는 경미한 오류)
- Red: 오류 (프레임 실패 또는 중단)

### REQ-UI-032: NetworkChannelStats 표시

**WHILE** 파이프라인이 실행 중인 상태에서
**THEN** 시스템은 NetworkChannel 통계를 표시한다:

- PacketsSent: 전송된 패킷 수
- PacketsLost: 손실된 패킷 수
- PacketsReordered: 재정렬된 패킷 수
- PacketsCorrupted: 오염된 패킷 수

### REQ-UI-033: 실시간 갱신

시스템은 **항상** 파이프라인 모니터 데이터를 2Hz(500ms 간격) 폴링으로 갱신해야 한다.

---

## Module 4: 시나리오 실행 (Scenario Runner -- Tab 5)

### REQ-UI-040: JSON 기반 시나리오 정의

시스템은 **항상** 다음 구조의 JSON 시나리오를 지원해야 한다:

```json
{
  "name": "string",
  "description": "string",
  "detectorConfig": { /* DetectorConfig fields */ },
  "networkConfig": { /* NetworkChannelConfig fields */ },
  "frameCount": 10,
  "assertions": [
    { "type": "minFrames", "value": 8 },
    { "type": "maxFailedFrames", "value": 2 }
  ]
}
```

### REQ-UI-041: 시나리오 목록 표시 및 선택 실행

**WHEN** Scenario Runner 탭이 표시되면
**THEN** 시스템은 사전 정의 및 사용자 정의 시나리오 목록을 표시하고 개별 선택 실행을 허용한다.

### REQ-UI-042: 실행 중 진행률 표시

**WHILE** 시나리오가 실행 중인 상태에서
**THEN** 시스템은 진행률(processed/total frames)을 프로그레스 바로 표시한다.

### REQ-UI-043: 실행 결과 표시

**WHEN** 시나리오 실행이 완료되면
**THEN** 시스템은 각 시나리오에 대해 PASS/FAIL 결과와 상세 메시지를 표시한다.

### REQ-UI-044: 사전 정의 시나리오 번들

시스템은 **항상** 기존 IT01-IT19 통합 테스트에 상응하는 사전 정의 시나리오를 번들로 포함해야 한다.

---

## Module 5: 구성 관리 (Configuration Management -- Tab 6)

### REQ-UI-050: detector_config.yaml 로드/저장

**WHEN** 사용자가 "Load Config" 버튼을 누르면
**THEN** 시스템은 detector_config.yaml 파일에서 구성을 로드하여 제어판에 반영한다.

**WHEN** 사용자가 "Save Config" 버튼을 누르면
**THEN** 시스템은 현재 제어판 설정을 detector_config.yaml 파일로 저장한다.

### REQ-UI-051: 구성 변경 시 파이프라인 즉시 반영

**WHEN** 구성이 로드되거나 파라미터가 변경되면
**THEN** 파이프라인이 실행 중이 아닌 경우 다음 Start 시 새 구성이 적용된다.
**AND** 파이프라인이 실행 중인 경우 hot-reload 가능한 파라미터(NetworkChannel)는 즉시 반영된다.

### REQ-UI-052: 파라미터 범위 유효성 검사

시스템은 **항상** 파라미터 입력 시 범위 유효성 검사를 수행해야 한다.

- kVp: 40-150
- packetLossRate: 0.0-1.0
- frameBufferCount: 1-8
- 유효하지 않은 값은 빨간색 테두리로 표시하고 Start 버튼을 비활성화한다

---

## Unwanted Requirements

### REQ-UI-060: 물리적 하드웨어 연결 제외

시스템은 물리적 하드웨어에 직접 연결하는 기능을 제공**하지 않아야 한다**.
DetectorClient를 통한 실제 하드웨어 연결은 이 SPEC 범위 외이며 별도 SPEC으로 다룬다.

### REQ-UI-061: 실시간 네트워크 소켓 통신 불필요

시스템은 실제 UDP 서버/클라이언트 포트 바인딩을 수행**하지 않아야 한다**.
모든 데이터 전달은 in-memory로만 이루어진다.

---

## Constraints

- C# 12 / .NET 8.0-windows / WPF
- 기존 MVVM 패턴 유지 (ObservableObject, RelayCommand)
- 기존 40개 단위 테스트 통과 유지 (하위 호환성)
- 새 NuGet 패키지 추가 없음 (System.Text.Json 내장 사용)
- IntegrationRunner.Core의 기존 file-based 메서드 유지 (backward compatibility)
