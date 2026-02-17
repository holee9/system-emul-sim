# Wave 3 Test Execution Guide

## Test Structure

```
fw/tests/
├── unit/
│   ├── test_sequence_engine.c    (16 tests - TASK-007)
│   ├── test_command_protocol.c   (24 tests - TASK-008)
│   └── test_frame_header.c       (10 tests - TASK-009)
└── mock/
    ├── mock_sequence.c           (Mock functions)
    └── mock_sequence.h
```

## Building Tests

### Prerequisites

```bash
# Install dependencies (Ubuntu/Debian)
sudo apt-get update
sudo apt-get install build-essential cmake libcmocka-dev

# Or (Fedora/RHEL)
sudo dnf install gcc cmake libcmocka-devel
```

### Build Commands

```bash
cd fw/build
cmake ..
make

# Build individual tests
make test_sequence_engine
make test_command_protocol
make test_frame_header
```

## Running Tests

### Run All Tests

```bash
cd fw/build
ctest --verbose
```

### Run Individual Tests

```bash
# Sequence Engine Tests
./tests/unit/test_sequence_engine

# Command Protocol Tests
./tests/unit/test_command_protocol

# Frame Header Tests
./tests/unit/test_frame_header
```

### Run with Coverage

```bash
cd fw/build
cmake -DCMAKE_BUILD_TYPE=Coverage ..
make
ctest
lcov --capture --directory . --output-file coverage.info
genhtml coverage.info --output-directory coverage_html
```

## Test Descriptions

### test_sequence_engine.c (16 tests)

| Test ID | Description | Requirement |
|---------|-------------|-------------|
| FW_UT_05_001 | Initial state is IDLE | REQ-FW-030 |
| FW_UT_05_002 | IDLE → CONFIGURE on START_SCAN | REQ-FW-030 |
| FW_UT_05_003 | CONFIGURE → ARM on CONFIG_DONE | REQ-FW-030 |
| FW_UT_05_004 | ARM → SCANNING on ARM_DONE | REQ-FW-030 |
| FW_UT_05_005 | SCANNING → STREAMING on FRAME_READY | REQ-FW-030 |
| FW_UT_05_006 | STREAMING → COMPLETE (Single mode) | REQ-FW-030 |
| FW_UT_05_007 | COMPLETE → IDLE on cleanup | REQ-FW-030 |
| FW_UT_05_008 | Single scan mode | REQ-FW-033 |
| FW_UT_05_009 | Continuous scan mode | REQ-FW-033 |
| FW_UT_05_010 | Calibration mode | REQ-FW-033 |
| FW_UT_05_011 | Error during SCANNING | REQ-FW-032 |
| FW_UT_05_012 | Error recovery - retry success | REQ-FW-032 |
| FW_UT_05_013 | Error recovery - max retries | REQ-FW-032 |
| FW_UT_05_014 | Stop scan from any state | REQ-FW-031 |
| FW_UT_05_015 | Get status counters | REQ-FW-111 |
| FW_UT_05_016 | State to string conversion | - |

### test_command_protocol.c (24 tests)

| Test ID | Description | Requirement |
|---------|-------------|-------------|
| FW_UT_07_001 | Valid command magic | REQ-FW-025 |
| FW_UT_07_002 | Invalid magic number | REQ-FW-025 |
| FW_UT_07_003 | Response magic number | REQ-FW-026 |
| FW_UT_07_004 | Valid HMAC | REQ-FW-100 |
| FW_UT_07_005 | Invalid HMAC | REQ-FW-100 |
| FW_UT_07_006 | Valid sequence number | REQ-FW-028 |
| FW_UT_07_007 | Replay detection - duplicate | REQ-FW-028 |
| FW_UT_07_008 | Replay detection - old sequence | REQ-FW-028 |
| FW_UT_07_009 | Separate sources tracking | REQ-FW-028 |
| FW_UT_07_010 | Parse START_SCAN command | REQ-FW-027 |
| FW_UT_07_011 | Parse GET_STATUS command | REQ-FW-027 |
| FW_UT_07_012 | Parse SET_CONFIG with payload | REQ-FW-027 |
| FW_UT_07_013 | Handle START_SCAN success | - |
| FW_UT_07_014 | Handle STOP_SCAN | - |
| FW_UT_07_015 | Handle GET_STATUS | - |
| FW_UT_07_016 | Handle invalid command | - |
| FW_UT_07_017 | HMAC failure | REQ-FW-101 |
| FW_UT_07_018 | Replay response | REQ-FW-028 |
| FW_UT_07_019 | Busy state handling | - |
| FW_UT_07_020 | Response magic number | REQ-FW-026 |
| FW_UT_07_021 | Response sequence echo | REQ-FW-027 |
| FW_UT_07_022 | Minimum packet size | REQ-FW-027 |
| FW_UT_07_023 | Packet too small | REQ-FW-027 |
| FW_UT_07_024 | Maximum sequence number | REQ-FW-028 |

### test_frame_header.c (10 tests)

| Test ID | Description | Requirement |
|---------|-------------|-------------|
| FW_UT_02_001 | Basic frame header encoding | REQ-FW-040 |
| FW_UT_02_002 | CRC calculation | REQ-FW-042 |
| FW_UT_02_003 | Maximum values encoding | REQ-FW-040 |
| FW_UT_02_004 | Basic frame header decoding | REQ-FW-040 |
| FW_UT_02_005 | Decode with invalid CRC | REQ-FW-042 |
| FW_UT_02_006 | NULL buffer handling | - |
| FW_UT_02_007 | Buffer too small | - |
| FW_UT_02_008 | Invalid magic number | REQ-FW-040 |
| FW_UT_02_009 | Drop indicator flag | REQ-FW-040 |
| FW_UT_02_010 | First and last packet flags | REQ-FW-040 |

## Expected Output

### Successful Test Run

```
FW-UT-05: Sequence Engine Tests
[==========] Running 16 test(s).
[ RUN      ] FW_UT_05_001: Initial state is IDLE
[       OK ] FW_UT_05_001: Initial state is IDLE
...
[==========] 16 test(s) run.
[  PASSED  ] 16 test(s).

FW-UT-07: Command Protocol Tests
[==========] Running 24 test(s).
[ RUN      ] FW_UT_07_001: Valid command magic
[       OK ] FW_UT_07_001: Valid command magic
...
[==========] 24 test(s) run.
[  PASSED  ] 24 test(s).

FW-UT-02: Frame Header Tests
[==========] Running 10 test(s).
[ RUN      ] FW_UT_02_001: Basic frame header encoding
[       OK ] FW_UT_02_001: Basic frame header encoding
...
[==========] 10 test(s) run.
[  PASSED  ] 10 test(s).

[===========================] 50 test(s) run.
[  PASSED  ] 50 test(s).
```

## Coverage Report

Expected coverage: ≥85%

```bash
# Generate coverage report
lcov --capture --directory . --output-file coverage.info
genhtml coverage.info --output-directory coverage_html

# View report
xdg-open coverage_html/index.html
```

## Troubleshooting

### Build Errors

**Error**: `cmocka.h not found`
```bash
# Ubuntu/Debian
sudo apt-get install libcmocka-dev

# Fedora/RHEL
sudo dnf install libcmocka-devel
```

**Error**: `undefined reference to seq_init`
```bash
# Ensure all source files are linked
# Check CMakeLists.txt includes all .c files
```

### Test Failures

**Error**: Test hangs indefinitely
- Check for infinite loops in state machine
- Verify mock functions are properly implemented

**Error**: Segmentation fault
- Run with gdb: `gdb ./tests/unit/test_sequence_engine`
- Check for NULL pointer dereferences
- Verify buffer sizes

## Integration Testing

After unit tests pass, run integration tests:

```bash
cd fw/build
./tests/integration/test_integration
```

This tests:
- SPI Master HAL with real hardware (or mock)
- Frame Manager with Sequence Engine
- Command Protocol with Ethernet TX
- Frame Header with actual data

## Continuous Integration

Add to CI pipeline:

```yaml
# .github/workflows/firmware-tests.yml
name: Firmware Tests
on: [push, pull_request]
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v2
      - name: Install dependencies
        run: sudo apt-get install -y build-essential cmake libcmocka-dev
      - name: Build
        run: |
          cd fw/build
          cmake ..
          make
      - name: Run tests
        run: |
          cd fw/build
          ctest --output-on-failure
      - name: Coverage
        run: |
          cd fw/build
          lcov --capture --directory . --output-file coverage.info
          bash <(curl -s https://codecov.io/bash)
```

---
**Last Updated**: 2026-02-18
**TDD Agent**: manager-tdd
