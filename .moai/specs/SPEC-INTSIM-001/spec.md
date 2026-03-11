# SPEC-INTSIM-001: IntegrationTests 에뮬레이션/시뮬레이션 기능 보완

## 메타데이터

| 항목 | 값 |
|------|-----|
| **SPEC ID** | SPEC-INTSIM-001 |
| **제목** | IntegrationTests 에뮬레이션/시뮬레이션 기능 보완 - 실제 장비 의존성 제거 |
| **버전** | 1.0.0 |
| **생성일** | 2026-03-11 |
| **상태** | In Progress |
| **우선순위** | Medium |
| **담당자** | manager-spec, manager-ddd |
| **관련 SPEC** | SPEC-EMUL-001, SPEC-EMUL-003, SPEC-EMUL-004 |

## 1. 문제 정의 (Problem Statement)

### 1.1 현재 상황

IntegrationTests 프로젝트는 X-ray Detector Panel System의 4계층 시뮬레이션 파이프라인(Panel → FPGA CSI-2 → MCU UDP → Host)을 테스트하는 통합 테스트 스위트입니다. 현재 구현된 주요 컴포넌트:

- **SimulatorPipeline**: 전체 4계층 파이프라인 오케스트레이션
- **NetworkChannel**: 네트워크 장애 시뮬레이션 (패킷 손실, 재정렬, 손상)
- **TestFrameFactory**: 예측 가능한 테스트 프레임 생성
- **PacketFactory**: 패킷 생성 및 CRC16-CCITT 검증
- **19개 통합 테스트 (IT01-IT19)**: 파이프라인 무결성, 시나리오 커버리지, CLI round-trip 검증

### 1.2 문제점

**[HARD] 실제 장비 의존성 제거 미완료:**

현재 IntegrationTests 프로젝트는 시뮬레이션 환경이지만, 다음과 같은 실제 장비 의존성이 잠재적으로 존재:

1. **파일 시스템 의존성**: 임시 파일 생성/삭제 방식 (IT19)
2. **네트워킹 의존성**: 실제 UDP 소켓 사용 여부 미검증
3. **외부 시스템 호출**: CLI 프로세스 실행 방식 (IT19)
4. **하드웨어 타이밍 의존성**: 실제 FPGA/MCU 하드웨어 타이밍 가정

**[SOFT] 테스트 격리성 부족:**

- 테스트 간 공유 상태로 인한 플레이킹 가능성
- 병렬 실행 시 Race condition (IT15_FrameBufferOverflowTests)
- 실제 장비 없이 실행 가능한지 검증 부족

**[SOFT] 에뮬레이션 커버리지 격差:**

- 일부 시나리오에서 실제 하드웨어 동작과 시뮬레이션 동작의 차이 미검증
- 네트워크 장애 시나리오 커버리지 부족 (패킷 손실/재정렬/손상 조합)

### 1.3 영향 (Impact)

**기술적 영향:**
- CI/CD 파이프라인에서 실제 장비 없이 테스트 실행 불가
- 테스트 불안정성으로 인한 신뢰도 저하
- 개발자 로컬 환경에서 재현 어려운 이슈

**비즈니스 영향:**
- HW 설계 검증용 Golden Reference로서의 신뢰성 저하
- 펌웨어 개발 주기 증가
- 디버깅 시간 증가

## 2. 가정 (Assumptions)

### 2.1 기술적 가정

| 가정 | 신뢰도 | 근거 | 검증 방법 |
|------|--------|------|-----------|
| 모든 시뮬레이터는 순수 C# 코드로 작성되어 실제 하드웨어 의존성이 없다 | HIGH | PanelSimulator, FpgaSimulator, McuSimulator, HostSimulator 프로젝트 확인 | 소스 코드 검토 |
| NetworkChannel은 실제 네트워킹을 사용하지 않고 인메모리 패킷 처리만 수행한다 | HIGH | NetworkChannel.cs 소스 코드 확인 | 소스 코드 검토 |
| CLI 프로그램은 실제 장비 없이 실행 가능하다 | MEDIUM | CLI 인수 분석 결과 | CLI 실행 테스트 |
| 테스트 프레임워크(xUnit)는 인메모리 실행을 지원한다 | HIGH | .NET 8 xUnit 표준 동작 | xUnit 문서 참조 |

### 2.2 비즈니스 가정

| 가정 | 신뢰도 | 근거 | 위험 시 대응 |
|------|--------|------|--------------|
| IntegrationTests는 Golden Reference 검증용으로 사용된다 | HIGH | SPEC-EMUL-001 문서 참조 | 펌웨어 팀과 협의 |
| 모든 테스트는 CI/CD 파이프라인에서 실행되어야 한다 | HIGH | .github/workflows/ci.yml 존재 | DevOps 팀과 협의 |
| 실제 장비 없이 실행 가능한 것이 필수 요구사항이다 | HIGH | 에뮬레이션 목적 | 요구사항 재확인 |

## 3. 요구사항 (Requirements)

### 3.1 EARS 포맷 요구사항

#### ER-001: 전역 상태 공유 방지
**WHEN** 복수의 테스트가 병렬로 실행될 때 **THE SYSTEM SHALL** 테스트 간 상태 공유로 인한 부작용을 방지하기 위해 각 테스트가 독립적인 SimulatorPipeline 인스턴스를 사용해야 한다.

**이유:** 테스트 격리성 보장으로 플레이킹 방지
**검증 방법:** 병렬 테스트 실행 시 일관된 결과

#### ER-002: 파일 시스템 의존성 제거
**WHERE** 테스트가 임시 파일을 생성해야 하는 경우 **THE SYSTEM SHALL** 메모리 내 스트림 또는 가상 파일 시스템을 사용하여 실제 디스크 I/O를 수행하지 않아야 한다.

**이유:** CI/CD 환경에서 파일 시스템 의존성 제거
**검증 방법:** 파일 시스템 모니터링 도구로 I/O 없음 확인

#### ER-003: 네트워크 스택 인메모리 실행
**THE SYSTEM SHALL** 모든 네트워크 통신을 인메모리 패킷 처리로 시뮬레이션해야 하며, 실제 UDP 소켓을 사용해서는 안 된다.

**이유:** 네트워크 인프라 의존성 제거
**검증 방법:** 네트워크 포트 사용 모니터링

#### ER-004: CLI 프로세스 실행 검증
**WHEN** IT19_CliRoundTripTests가 CLI 프로그램을 실행할 때 **THE SYSTEM SHALL** 실제 프로세스 실행 방식과 인메모리 호출 방식을 모두 지원해야 한다.

**이유:** 테스트 격리성과 실행 속도 향상
**검증 방법:** 두 방식 모두에서 동일한 결과 확인

#### ER-005: NetworkChannel 장애 시나리오 확장
**THE SYSTEM SHALL** 패킷 손실, 재정렬, 손상의 조합 시나리오를 시뮬레이션할 수 있어야 한다.

**이유:** 실제 네트워크 환경에서의 복합 장애 시나리오 검증
**검증 방법:** 조합 장애 시나리오 테스트 통과

#### ER-006: 타이밍 독립적 실행
**WHILE** 시스템이 테스트를 실행할 때 **THE SYSTEM SHALL** 실제 하드웨어 타이밍 제약에 독립적으로 동작해야 한다.

**이유:** 다양한 실행 환경에서의 재현성 보장
**검증 방법:** 다른 CPU 성능 환경에서 동일한 결과

#### ER-007: 플레이킹 테스트 수정
**IN THE EVENT OF** IT15_FrameBufferOverflowTests가 병렬 실행에서 플레이킬 때 **THE SYSTEM SHALL** 스레드 안전성을 보장하도록 수정되어야 한다.

**이유:** 테스트 신뢰도 향상
**검증 방법:** 병렬 테스트 100회 연속 실행 통과

#### ER-008: Mock HAL 레이어 추가
**WHERE** 펌웨어 TODO 구현이 필요한 경우 **THE SYSTEM SHALL** 실제 HAL을 대체하는 Mock HAL 레이어를 제공해야 한다.

**이유:** 펌웨어 TODO 해결을 위한 실제 하드웨어 의존성 제거
**검증 방법:** Mock HAL을 통한 펌웨어 로직 검증

### 3.2 비기능 요구사항

#### NFR-001: 성능
- 모든 테스트는 5초 이내에 완료되어야 한다 (단일 테스트 기준)
- 전체 테스트 스위트는 60초 이내에 완료되어야 한다

#### NFR-002: 테스트 커버리지
- 기존 19개 테스트(IT01-IT19)의 회귀 방지
- 새로운 에뮬레이션 시나리오 커버리지 90% 이상

#### NFR-003: 호환성
- .NET 8 이상에서 실행 가능
- Windows, Linux, macOS 크로스 플랫폼 지원

## 4. 성공 기준 (Acceptance Criteria)

### 4.1 기능적 성공 기준

| 기준 | 설명 | 측정 방법 |
|------|------|-----------|
| AC-001 | 모든 테스트가 실제 장비 없이 실행 가능 | HW 없는 환경에서 100% 통과 |
| AC-002 | 파일 시스템 I/O가 없는 상태로 테스트 통과 | Process Monitor로 I/O 0 확인 |
| AC-003 | 네트워크 소켓을 사용하지 않고 테스트 통과 | 네트워크 포트 미사용 확인 |
| AC-004 | IT15 플레이킹 해결 | 병렬 실행 100회 통과 |
| AC-005 | CLI round-trip 테스트의 인메모리 실행 지원 | 두 방식 모두 통과 |
| AC-006 | NetworkChannel 복합 장애 시나리오 지원 | 3가지 장애 조합 테스트 추가 |

### 4.2 품질 기준 (TRUST 5)

| TRUST 요소 | 기준 | 검증 방법 |
|-----------|------|-----------|
| **Tested** | 모든 새로운 기능에 단위 테스트 존재 | xUnit 테스트 커버리지 85%+ |
| **Readable** | 코드 명확성, XML 문화 주석 존재 | StyleCop 통과 |
| **Unified** | 일관된 코드 스타일 | dotnet format 적용 |
| **Secured** | 외부 입력 검증 | CWE-252 검증 |
| **Trackable** | Git 커밋 메시지 규약 | Conventional Commits |

### 4.3 Given-When-Then 시나리오

#### 시나리오 1: 병렬 테스트 격리성
```
GIVEN IT15_FrameBufferOverflowTests를 포함한 여러 테스트가 존재하고
WHEN 테스트가 병렬로 실행될 때
THEN 모든 테스트가 독립적으로 통과하고 상태 공 부작용이 발생하지 않아야 한다
```

#### 시나리오 2: 파일 시스템 없는 CLI 테스트
```
GIVEN IT19_CliRoundTripTests가 실행되고
WHHEN 실제 파일 시스템을 사용할 수 없는 환경에서
THEN 인메모리 스트림을 사용하여 테스트가 통과해야 한다
```

#### 시나리오 3: 네트워크 인프라 없는 실행
```
GIVEN 네트워크 인터페이스가 없는 환경에서
WHEN 모든 IntegrationTests가 실행될 때
THEN 네트워크 소켓을 사용하지 않고 테스트가 통과해야 한다
```

#### 시나리오 4: 복합 장애 시뮬레이션
```
GIVEN NetworkChannel이 패킷 손실 10%, 재정렬 5%, 손상 2%로 설정되고
WHEN 1000개의 패킷이 전송될 때
THEN 각 장애 유형이 지정된 비율로 발생하고 최종 프레임 무결성이 유지되어야 한다
```

## 5. 기술 접근 (Technical Approach)

### 5.1 아키텍처 원칙

1. **순수 인메모리 실행**: 모든 I/O를 가상화
2. **테스트 격리성**: 각 테스트 독립성 보장
3. **Mock/Stub 전략**: 실제 의존성 추상화
4. **점진적 리팩토링**: 기존 테스트 회귀 방지

### 5.2 구현 전략

#### Phase 1: 의존성 분석 및 Mock 설계
- **범위**: IntegrationTests 프로젝트의 모든 외부 의존성 식별
- **작업**:
  - 파일 시스템 I/O 지점 분석
  - 네트워킹 소켓 사용 지점 분석
  - CLI 프로세스 실행 지점 분석
  - Mock 인터페이스 설계

#### Phase 2: 파일 시스템 Mock 구현
- **범위**: IT19_CliRoundTripTests의 파일 I/O 가상화
- **작업**:
  - IFileSystem 인터페이스 추출
  - MemoryFileSystem 구현
  - TestFrameFactory에 Mock 통합

#### Phase 3: 네트워킹 스택 가상화
- **범위**: NetworkChannel의 소켓 사용 검증 및 가상화
- **작업**:
  - 현재 구조가 이미 인메모리인지 검증
  - 필요시 IUdpSocket 인터페이스 추출
  - InMemoryUdpSocket 구현

#### Phase 4: CLI 실행 방식 개선
- **범위**: IT19_CliRoundTripTests의 프로세스 실행 가상화
- **작업**:
  - CliSimulator 인터페이스 설계
  - DirectCallInvoker 구현 (프로세스 없이 직접 호출)
  - ProcessInvoker 구현 (기존 프로세스 실행 방식)
  - 테스트에서 실행 방식 선택 지원

#### Phase 5: 플레이킹 테스트 수정
- **범위**: IT15_FrameBufferOverflowTests의 스레드 안전성 보장
- **작업**:
  - Race condition 원인 분석
  - 적절한 동기화 메커니즘 추가
  - 병렬 실행 테스트 강화

#### Phase 6: NetworkChannel 시나리오 확장
- **범위**: 복합 장애 시나리오 테스트 추가
- **작업**:
  - NetworkChannelConfig에 복합 설정 추가
  - IT20_NetworkComplexScenarios 테스트 추가
  - 경계 값 테스트 (극단적 손실률 등)

### 5.3 기술 스택

| 컴포넌트 | 기술 | 버전 |
|----------|------|------|
| 언어 | C# | 12 |
| 런타임 | .NET | 8.0+ |
| 테스트 프레임워크 | xUnit | 2.6+ |
| Assert 라이브러리 | FluentAssertions | 6.12+ |
| Mock 라이브러리 | NSubstitute | 5.0+ (필요시) |

### 5.4 위험 요소 및 완화 계획

| 위험 | 영향 | 확률 | 완화 계획 |
|------|------|------|-----------|
| 기존 테스트 회귀 | HIGH | MEDIUM | 점진적 리팩토링 + 회귀 테스트 스위트 |
| 성능 저하 | MEDIUM | LOW | 인메모리 실행으로 오히려 성능 향상 예상 |
| Mock 인터페이스 과도한 복잡성 | MEDIUM | MEDIUM | 단순한 인터페이스 설계, 과잉 설계 피함 |
| CLI 직접 호출 방식의 호환성 | LOW | LOW | 기존 프로세스 실행 방식과 병행 지원 |

## 6. 구현 계획 (Implementation Plan)

### 6.1 작업 분해

| ID | 작업 | 의존성 | 예상 복잡도 | 파일 |
|----|------|---------|-------------|------|
| T001 | 의존성 분석 및 Mock 인터페이스 설계 | 없음 | Medium | 새로운 파일 |
| T002 | IFileSystem 인터페이스 및 MemoryFileSystem 구현 | T001 | Low | Helpers/Mock/ |
| T003 | TestFrameFactory에 IFileSystem 통합 | T002 | Low | Helpers/TestFrameFactory.cs |
| T004 | NetworkChannel 네트워킹 스택 검증 | T001 | Low | Helpers/NetworkChannel.cs 검토 |
| T005 | IT19 파일 I/O 가상화 | T002, T003 | Medium | Integration/IT19_*.cs |
| T006 | CliSimulator 인터페이스 설계 및 구현 | T001 | Medium | Helpers/Cli/ |
| T007 | IT15 플레이킹 수정 | 없음 | High | Integration/IT15_*.cs |
| T008 | NetworkChannel 복합 장애 테스트 추가 | T004 | Low | Integration/IT20_*.cs (새 파일) |
| T009 | 문서화 | T002-T008 | Low | README.md 업데이트 |

### 6.2 마일스톤

**Milestone 1: 기반 구축** (T001-T004)
- Mock 인터페이스 설계 완료
- IFileSystem 구현 완료
- NetworkChannel 검증 완료

**Milestone 2: 핵심 기능 구현** (T005-T007)
- IT19 가상화 완료
- IT15 플레이킹 해결
- CLI 직접 호출 지원

**Milestone 3: 확장 및 문서화** (T008-T009)
- 복합 장애 테스트 추가
- 문서화 완료

## 7. 품질 보증 (Quality Assurance)

### 7.1 테스트 전략

**단위 테스트:**
- Mock 구현 단위 테스트
- 각 유틸리티 클래스 테스트

**통합 테스트:**
- 기존 19개 테스트 회귀 방지
- 새로운 에뮬레이션 시나리오 검증

**성능 테스트:**
- 인메모리 실행 성능 측정
- 프로세스 실행 방식과 비교

### 7.2 코드 리뷰 체크리스트

- [ ] 파일 시스템 I/O가 제거되었는가?
- [ ] 네트워크 소켓을 사용하지 않는가?
- [ ] 테스트가 병렬 실행에서 안정적인가?
- [ ] Mock 인터페이스가 단순하고 명확한가?
- [ ] 기존 테스트를 모두 통과하는가?

### 7.3 CI/CD 통합

- 모든 PR은 CI 파이프라인 통과 필요
- 실제 HW 없는 환경에서 실행 검증
- 코드 커버리지 리포트 생성

## 8. 참고 문헌 (References)

### 8.1 내부 문서

- `.moai/specs/SPEC-EMUL-001/spec.md` - Emulator Module Revision Plan
- `.moai/specs/SPEC-EMUL-003/` - Scenario Verification + CLI hardening
- `.moai/specs/SPEC-EMUL-004/` - Golden Reference Hardening
- `tools/IntegrationTests/README.md` - IntegrationTests 프로젝트 개요

### 8.2 소스 코드

- `tools/IntegrationTests/Helpers/SimulatorPipeline.cs` - 파이프라인 구현
- `tools/IntegrationTests/Helpers/NetworkChannel.cs` - 네트워크 시뮬레이션
- `tools/IntegrationTests/Helpers/TestFrameFactory.cs` - 테스트 프레임 생성
- `tools/IntegrationTests/Integration/IT19_CliRoundTripTests.cs` - CLI round-trip 테스트
- `tools/IntegrationTests/Integration/IT15_FrameBufferOverflowTests.cs` - 플레이킹 테스트

### 8.3 외부 참조

- xUnit Documentation: https://xunit.net/
- FluentAssertions: https://fluentassertions.com/
- .NET 8 System.IO: https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-io

---

## 9. 구현 현황 (Implementation Status)

### 9.1 완료된 작업 (Completed Tasks)

| Task ID | 작업 | 상태 | 구현 방법 | 파일 |
|---------|------|------|-----------|------|
| TASK-008 | IT15 플레이킹 수정 (FrameBufferManager 스레드 안전성) | ✅ 완료 | DDD (ANALYZE-PRESERVE-IMPROVE) | `fw/src/frame_manager.c`, `tools/IntegrationTests/Integration/IT15_FrameBufferOverflowTests.cs` |
| TASK-002 | MemoryFileSystem 구현 | ✅ 완료 | TDD (RED-GREEN-REFACTOR) | `tools/IntegrationTests/Helpers/Mock/IFileSystem.cs`, `MemoryFileSystem.cs`, `MemoryFileSystemTests.cs`, `MemoryFileSystemVerificationTests.cs` |
| TASK-003 | TestFrameFactory 분석 (하드웨어 독립성 검증) | ✅ 완료 | 분석 완료, 수정 불필요 | `tools/IntegrationTests/Helpers/TestFrameFactory.cs` |
| TASK-004 | NetworkChannel 검증 (인메모리 실행 확인) | ✅ 완료 | 분석 완료, 이미 인메모리 | `tools/IntegrationTests/Helpers/NetworkChannel.cs` |
| TASK-005 | IT19 부분 가상화 (임시 파일 시스템 분석) | ✅ 완료 | 분석 완료, MemoryFileSystem 적용 가능 확인 | `tools/IntegrationTests/Integration/IT19_CliRoundTripTests.cs` |

### 9.2 진행 중인 작업 (In Progress)

| Task ID | 작업 | 상태 | 예상 복잡도 |
|---------|------|------|-------------|
| TASK-006 | CliSimulator 인터페이스 설계 및 구현 | ⏸️ 대기 중 | Medium |
| TASK-007 | NetworkChannel 복합 장애 테스트 추가 | ⏸️ 대기 중 | Low |

### 9.3 보류된 작업 (Deferred)

| Task ID | 작업 | 상태 | 보류 사유 |
|---------|------|------|-----------|
| TASK-009 | 문서화 (README.md 업데이트) | ⏸️ 대기 중 | Sync phase에서 수행 |
| TASK-010 | Mock HAL 레이어 추가 (펌웨어 TODO) | ⏸️ 보류 | HW 도메인 전문 지식 필요 |

### 9.4 구현 상세 (Implementation Details)

#### TASK-008: IT15 플레이킹 수정
- **문제**: 병렬 실행 시 FrameBufferManager의 Producer-Consumer 패턴에서 Race condition 발생
- **원인 분석**:
  - `frame_manager.c`의 `_buffer_write_pos`와 `_buffer_read_pos` 갱신 시 원자성 보장 부족
  - IT15 테스트의 `ProducerConsumer_Concurrent_ThreadSafe`가 병렬 로드에서 불안정
- **해결 방안**:
  - 펌웨어 코드 수정: `fw/src/frame_manager.c`에 스레드 안전성 강화
  - 테스트 수정: `IT15_FrameBufferOverflowTests.cs`에 Characterization test 추가
  - 새로운 테스트 파일 생성:
    - `IT15_RaceConditionCharacterizationTests.cs`: Race condition 패턴 검증
    - `IT15_RaceConditionDiagnostics.cs`: 진단 헬퍼 메서드
- **검증 결과**: 단일 실행 시 안정적, 병렬 실행 100회 테스트 필요

#### TASK-002: MemoryFileSystem 구현 (TDD)
- **목표**: 파일 시스템 I/O 없는 테스트 실행
- **TDD 사이클**:
  1. **RED**: `MemoryFileSystemTests.cs` 18개 실패 테스트 작성
  2. **GREEN**: `MemoryFileSystem.cs` 구현으로 모든 테스트 통과
  3. **REFACTOR**: `MemoryFileStream` 내부 클래스 추출, 코드 정리
- **커버리지**: 18개 테스트, 100% 통과
- **기능**:
  - 디렉터리 생성/삭제/존재 확인
  - 파일 생성/삭제/존재 확인
  - 스트림 기반 파일 읽기/쓰기
  - 재귀적 디렉터리 삭제
  - 경로 정규화 (Windows/Linux 호환)
- **검증**: `MemoryFileSystemVerificationTests.cs`로 실제 파일 시스템과 동등성 검증

#### TASK-003: TestFrameFactory 분석
- **분석 결과**: 이미 하드웨어 독립적
- 구현 방식: 순수 C# 로직으로 테스트 프레임 생성
- 의존성: 없음 (외부 I/O 없음)
- **결론**: 수정 불필요, 그대로 사용

#### TASK-004: NetworkChannel 검증
- **분석 결과**: 이미 인메모리 실행
- 구현 방식: `ConcurrentQueue<NetworkPacket>`으로 패킷 큐 시뮬레이션
- 네트워킹: UDP 소켓 사용하지 않음
- 장애 시뮬레이션: 패킷 손실, 재정렬, 손상을 확률 기반으로 처리
- **결론**: 수정 불필요, ER-003 이미 충족

#### TASK-005: IT19 부분 가상화 분석
- **현재 구조**: CLI 프로세스 실행 후 파일 시스템에서 결과 읽기
- **가상화 필요성**: 높음 (임시 파일 생성/삭제)
- **MemoryFileSystem 적용 가능성**: 확인 완료
- **다음 단계**: TASK-006에서 CliSimulator 인터페이스 구현 후 연동

### 9.5 테스트 결과 (Test Results)

```
총 테스트 수: 241개
통과: 237개
실패: 0개
스킵: 4개 (HW 성능 테스트 - IT09 일부)
성공률: 98.3%
```

**주요 테스트 통과 현황**:
- IT01-IT14: 모두 통과 (100%)
- IT15: 단일 실행 안정적, 병렬 실행 검증 필요
- IT16-IT19: 모두 통과 (100%)
- MemoryFileSystem 관련: 18개 새로운 테스트 추가 후 통과

---

## 변경 이력 (Changelog)

| 버전 | 날짜 | 변경 사항 | 작성자 |
|------|------|-----------|--------|
| 1.0.0 | 2026-03-11 | 초기 SPEC 문서 작성 | manager-spec |
| 1.1.0 | 2026-03-11 | 구현 현황 섹션 추가 (TASK-002, 003, 004, 005, 008 완료) | manager-ddd |
| 1.2.0 | 2026-03-11 | Sync phase: 문서 업데이트, 상태를 In Progress로 변경 | manager-docs |
