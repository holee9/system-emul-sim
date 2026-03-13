# Phase 3 Plan: G7 Firmware TODOs - Agent-Implementable Tasks

## Context

G7 Firmware TODOs 중 **전문 에이전트가 구현 가능한 작업만** 추려 Phase 3 계획을 수립합니다.

### 분석 결과: 19개 TODO → 6개 남은 TODO → 4개 구현 가능

`HW_DEPENDENT_TODOS.md`에 따르면:
- **9개 TODO**: 이미 구현 완료 (SW logic only)
- **10개 TODO**: HW 의존적 (실제 하드웨어 필요)

현재 코드에 남은 **6개 TODO** 중 구현 가능 범위:

| TODO | 위치 | 구현 가능성 | 범위 |
|------|------|-----------|------|
| 1. UDP transmission | `main.c:407` | ✅ **Full 구현** | 표준 socket API, `eth_tx.h` 인터페이스 있음 |
| 2. UDP command listener | `main.c:429` | ✅ **Full 구현** | 표준 socket API, `command_protocol.h` 있음 |
| 3. FPGA temperature (SPI) | `health_monitor.c:300` | ✅ **Mock 구현** | Mock SPI 장치로 대체 |
| 4. FPGA ARM register (SPI) | `sequence_engine.c:109` | ✅ **Mock 구현** | Mock SPI 장치로 대체 |
| 5. V4L2 DQBUF | `main.c:385` | ❌ **제외** | 실제 V4L2 디바이스(/dev/video0) 필요 |

### 제외 사유 (V4L2 DQBUF)

V4L2는 실제 하드웨어가 필요합니다:
- MIPI CSI-2 receiver hardware
- V4L2 driver (rcar-csi2 등)
- Camera sensor

**대안**: IntegrationTests 시뮬레이터로 검증 (이미 완료됨)

---

## Implementation Strategy

### 범위: Agent-Implementable TODOs (4개)

**목표**: Mock HAL + 실제 UDP 네트워킹 코드로 펌웨어 TODO 해소

### 구현 가능 상세

#### 1. UDP Transmission (Full Implementation) ✅

**파일**: `fw/src/main.c:407` (`eth_tx_thread`)

**할 일**:
- `hal/eth_tx.h` 인터페이스 구현
- Linux socket API 사용한 UDP 전송
- Frame fragmentation (REQ-FW-040)
- CRC-16 header (REQ-FW-042)

**의존성**: 표준 Linux socket API (사용 가능)

#### 2. UDP Command Listener (Full Implementation) ✅

**파일**: `fw/src/main.c:429` (`command_thread`)

**할 일**:
- `protocol/command_protocol.h` 사용
- UDP port 8001 listen
- Command parsing (HMAC, replay detection)
- Response transmission

**의존성**: `command_protocol.c` 이미 구현됨

#### 3. FPGA Temperature (Mock Implementation) ✅

**파일**: `fw/src/health_monitor.c:300`

**할 일**:
- Mock SPI HAL 구현 (`tests/mock/mock_spidev.c`)
- 레지스터 매크로 정의 (FPGA_REG_TEMP = 0x30)
- mock 값을 반환 (테스트 가능)

**의존성**: Mock SPI 장치

#### 4. FPGA ARM Register (Mock Implementation) ✅

**파일**: `fw/src/sequence_engine.c:109`

**할 일**:
- Mock SPI HAL 사용
- 레지스터 매크로 정의 (FPGA_REG_CTRL = 0x10)
- ARM bit 설정 시뮬레이션

**의존성**: Mock SPI 장치

---

## Modified Files (6개)

| 파일 | 수정 내용 | 방법론 |
|------|----------|--------|
| `fw/src/main.c` | UDP transmission, command listener 구현 | TDD (신규 코드) |
| `fw/src/health_monitor.c` | FPGA temperature 읽기 (Mock SPI) | TDD |
| `fw/src/sequence_engine.c` | FPGA ARM register 쓰기 (Mock SPI) | TDD |
| `fw/src/hal/eth_tx.c` | HAL 구현 (신규) | TDD |
| `fw/tests/mock/mock_spidev.c` | Mock SPI HAL 확장 | TDD |
| `fw/tests/unit/test_*` | 관련 단위 테스트 | TDD |

---

## Testing Strategy

### Unit Tests (CMocka)

```bash
cd fw/build
cmake ..
make
ctest --verbose
```

**Target**: 85%+ coverage per quality.yaml

### Mock HAL Strategy

실제 하드웨어가 없으므로 Mock HAL을 사용:

1. **Mock SPI**: `tests/mock/mock_spidev.c`
   - FPGA 레지스터 시뮬레이션
   - temperature, control 레지스터 구현

2. **Mock V4L2**: `tests/mock/mock_v4l2.c`
   - DQBUF 시뮬레이션 (이미 존재)

3. **Ethernet TX**: 실제 socket 사용 (테스트용 loopback)
   - 127.0.0.1 로컬 테스트

---

## Verification

### 1. 빌드 확인

```bash
cd fw/build
cmake ..
make
```

### 2. 단위 테스트

```bash
cd fw/build
ctest --output-on-failure
```

### 3. 코드 커버리지

```bash
lcov --capture --directory . --output-file coverage.info
genhtml coverage.info --output-directory coverage_html
# Target: 85%+
```

### 4. 네트워크 기능 테스트 (로컬)

```bash
# UDP command listener 테스트
nc -u 127.0.0.1 8001
# Send test command, verify response
```

---

## Success Criteria

| 항목 | 조건 |
|------|------|
| TODO 해소 | 4개 TODO 구현 완료 |
| 테스트 통과 | 모든 단위 테스트 통과 |
| 커버리지 | 85%+ (quality.yaml 기준) |
| 빌드 | Cross-compilation 성공 |
| TRUST 5 | 코드 품질 게이트 통과 |

---

## Dependencies

### External Libraries

- **libcmocka**: 단위 테스트 (이미 사용 중)
- **libyaml**: YAML 설정 (이미 사용 중)

### Cross-Compilation

Yocto SDK for i.MX8M Plus (Cortex-A53):
- `aarch64-poky-linux-gcc`
- CMake toolchain file

---

## Execution Plan

### Phase 1: Mock HAL 확장
- Mock SPI HAL에 FPGA 레지스터 추가
- temperature, control 레지스터 구현

### Phase 2: Ethernet TX HAL 구현
- `hal/eth_tx.c` 구현
- Frame fragmentation, CRC-16

### Phase 3: main.c TODO 해소
- UDP transmission (eth_tx_thread)
- UDP command listener (command_thread)

### Phase 4: health_monitor.c TODO 해소
- FPGA temperature 읽기 (Mock SPI)

### Phase 5: sequence_engine.c TODO 해소
- FPGA ARM register 쓰기 (Mock SPI)

### Phase 6: 테스트 및 검증
- 단위 테스트 작성
- 커버리지 확인
- 빌드 검증

---

## Risk Assessment

| 리스크 | 확률 | 영향 | 완화 방안 |
|--------|------|------|----------|
| 실제 하드웨어 동작과 상이 | 중 | 중 | Mock HAL을 실제 동작과 유사하게 구현 |
| 네트워킹 코드 버그 | 낮 | 중 | 로컬 loopback 테스트로 검증 |
| 크로스 컴파일 문제 | 낮 | 낮 | Yocto SDK 표준 사용 |

---

## References

- `fw/HW_DEPENDENT_TODOS.md` - 전체 TODO 목록
- `fw/TEST_GUIDE.md` - 테스트 가이드
- `.moai/specs/SPEC-FW-001/spec.md` - 펌웨어 요구사항
- `fw/include/hal/eth_tx.h` - Ethernet TX HAL 인터페이스
- `fw/include/protocol/command_protocol.h` - Command Protocol
