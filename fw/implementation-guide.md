# 펌웨어 TODO 구현 가이드

## 개요
이 문서는 X-ray Detector Panel System 펌웨어의 TODO 아이템에 대한 상세 구현 계획을 제공합니다.

## 분류 기준
- **Category A**: 순수 SW 로직 - 실제 하드웨어 없이 구현 가능
- **Category B**: Mock/Stub - 가짜 하드웨어 인터페이스로 구현
- **Category C**: 실제 하드웨어 의존 - HW 전문가 필요

## Category A: 순수 SW 로직 구현

### 1. Command Protocol (command_protocol.c)

#### 1.1 Start Scan Sequence (Line 401)
**API 필요**: `seq_start_scan(scan_mode_t mode)`
**구현 방식**:
```c
case CMD_START_SCAN:
    // 시퀀스 엔진에서 스캔 시작
    int seq_result = seq_start_scan(SCAN_MODE_SINGLE); // 기본값
    if (seq_result != 0) {
        status = STATUS_ERROR;
        payload_len = 4; // 오류 코드 크기
        memcpy(payload, &seq_result, 4);
    } else {
        status = STATUS_OK;
    }
    break;
```

#### 1.2 Stop Scan Sequence (Line 406)
**API 필요**: `seq_stop_scan()`
**구현 방식**:
```c
case CMD_STOP_SCAN:
    // 시퀀스 엔진에서 스캔 중지
    int seq_result = seq_stop_scan();
    if (seq_result != 0) {
        status = STATUS_ERROR;
        payload_len = 4;
        memcpy(payload, &seq_result, 4);
    } else {
        status = STATUS_OK;
    }
    break;
```

#### 1.3 Get Status (Line 411)
**API 필요**: `health_monitor_get_status(system_status_t *status)`
**구현 방식**:
```c
case CMD_GET_STATUS:
    system_status_t status_info;
    int health_result = health_monitor_get_status(&status_info);
    if (health_result != 0) {
        status = STATUS_ERROR;
    } else {
        status = STATUS_OK;
        // 상정보를 payload로 변환
        payload_len = sizeof(status_info);
        memcpy(payload, &status_info, payload_len);
    }
    break;
```

#### 1.4 Set Configuration (Line 417)
**구현 방식**:
```c
case CMD_SET_CONFIG:
    // 설정 파싱 및 적용
    // config_t *config = (config_t *)cmd->payload;
    // if (validate_config(config)) {
    //     apply_config(config);
    //     status = STATUS_OK;
    // } else {
    //     status = STATUS_INVALID_CMD;
    // }
    status = STATUS_OK; // 임시 구현
    break;
```

#### 1.5 Reset System (Line 422)
**구현 방식**:
```c
case CMD_RESET:
    // 시스템 리셋 로직
    // health_monitor_log(LOG_INFO, "cmd", "System reset requested");
    // seq_deinit();
    // seq_init();
    status = STATUS_OK; // 임시 구현
    break;
```

### 2. Main Daemon (main.c)

#### 2.1 SPI Status Polling (Line 332)
**구현 방식**:
```c
while (ctx->running && !ctx->shutdown_requested) {
    // FPGA 상태 레지스터 읽기 (가짜 구현)
    uint8_t fpga_status = 0x04; // FPGA_STATUS_READY

    if (fpga_status & FPGA_STATUS_ERROR) {
        health_monitor_log(LOG_ERROR, "spi", "FPGA error detected");
        // 에러 처리 로직
    }

    if (fpga_status & FPGA_STATUS_BUSY) {
        // FPGA 작업 중
    } else {
        // FPGA 준비됨
    }

    usleep(100);  // 100us
}
```

#### 2.2 Statistics Aggregation (Line 423)
**API 필요**: `health_monitor_update_stat()`
**구현 방식**:
```c
// 각 스레드에서 통계 업데이트
health_monitor_update_stat("frames_received", 1);
health_monitor_update_stat("frames_sent", 1);
health_monitor_update_stat("spi_errors", error_count);
health_monitor_update_stat("csi2_errors", csi2_error_count);
```

#### 2.3 Configuration Reload (Line 676)
**구현 방식**:
```c
else if (g_signal_received == SIGHUP) {
    health_monitor_log(LOG_INFO, "main", "Reloading configuration");
    detector_config_t new_config;
    if (config_loader_reload(config_path, &new_config) == 0) {
        // 새로운 설정 적용
        memcpy(&ctx->config, &new_config, sizeof(detector_config_t));
        health_monitor_log(LOG_INFO, "main", "Configuration reloaded");
    } else {
        health_monitor_log(LOG_ERROR, "main", "Failed to reload configuration");
    }
    g_signal_received = 0;
}
```

### 3. Health Monitor (health_monitor.c)

#### 3.1 Get Sequence Engine State (Line 265)
**API 필요**: `seq_get_state()`
**구현 방식**:
```c
// 시퀀스 엔진 상태 가져오기
if (g_health_ctx.seq_engine != NULL) {
    seq_state_t *seq_state = (seq_state_t *)g_health_ctx.seq_engine;
    status->state = *seq_state;
} else {
    status->state = SEQ_STATE_IDLE; // 기본값
}
```

#### 3.2 Battery Metrics (Line 268)
**구현 방식**:
```c
// 배터리 상태 가져오기 (가짜 구현)
if (ctx->battery_ctx.initialized) {
    // 실제 구현에서는 BQ40z50 드라이버에서 읽음
    // bq40z50_get_soc(&ctx->battery_ctx, &status->battery_soc);
    // bq40z50_get_voltage(&ctx->battery_ctx, &status->battery_mv);
} else {
    status->battery_soc = 100; // 100%
    status->battery_mv = 3700; // 3.7V
}
```

## Category B: Mock/Stub 구현

### 1. V4L2 DQBUF 구현 (main.c:354)
```c
static void *csi2_rx_thread(void *arg) {
    daemon_context_t *ctx = (daemon_context_t *)arg;
    int fd = open("/dev/video0", O_RDWR);

    while (ctx->running && !ctx->shutdown_requested) {
        // V4L2 DQBUF 시뮬레이션
        struct v4l2_buffer buffer;
        memset(&buffer, 0, sizeof(buffer));

        // 가짜 프레임 생성
        if (rand() % 100 < 95) { // 95% 성공률
            buffer.type = V4L2_BUF_TYPE_VIDEO_CAPTURE;
            buffer.memory = V4L2_MEMORY_MMAP;
            buffer.length = FRAME_SIZE;
            buffer.bytesused = FRAME_SIZE;

            // 프레임을 프레임 매니저로 전달
            frame_manager_push_frame(&ctx->frame_mgr, &buffer);

            health_monitor_update_stat("frames_received", 1);
        }

        usleep(1000);  // 1ms
    }

    close(fd);
    return NULL;
}
```

### 2. UDP Transmission 구현 (main.c:376)
```c
static void *eth_tx_thread(void *arg) {
    daemon_context_t *ctx = (daemon_context_t *)arg;
    int sockfd = socket(AF_INET, SOCK_DGRAM, 0);
    struct sockaddr_in servaddr;

    memset(&servaddr, 0, sizeof(servaddr));
    servaddr.sin_family = AF_INET;
    servaddr.sin_port = htons(8000);
    inet_pton(AF_INET, "192.168.1.100", &servaddr.sin_addr);

    while (ctx->running && !ctx->shutdown_requested) {
        // 프레임 큐에서 가져오기
        frame_queue_t *frame = frame_manager_pop_frame(&ctx->frame_mgr);
        if (frame != NULL) {
            // UDP로 전송
            sendto(sockfd, frame->data, frame->size, 0,
                  (struct sockaddr *)&servaddr, sizeof(servaddr));

            health_monitor_update_stat("frames_sent", 1);
            health_monitor_update_stat("bytes_sent", frame->size);

            free(frame);
        }

        usleep(100);  // 100us
    }

    close(sockfd);
    return NULL;
}
```

### 3. UDP Command Listener (main.c:398)
```c
static void *command_thread(void *arg) {
    daemon_context_t *ctx = (daemon_context_t *)arg;
    int sockfd = socket(AF_INET, SOCK_DGRAM, 0);
    struct sockaddr_in servaddr;

    memset(&servaddr, 0, sizeof(servaddr));
    servaddr.sin_family = AF_INET;
    servaddr.sin_port = htons(8001);
    servaddr.sin_addr.s_addr = INADDR_ANY;

    bind(sockfd, (struct sockaddr *)&servaddr, sizeof(servaddr));

    while (ctx->running && !ctx->shutdown_requested) {
        uint8_t buffer[1024];
        struct sockaddr_in cliaddr;
        socklen_t len = sizeof(cliaddr);

        int n = recvfrom(sockfd, buffer, sizeof(buffer), 0,
                         (struct sockaddr *)&cliaddr, &len);

        if (n > 0) {
            // 명령 파싱 및 처리
            command_frame_t *cmd = (command_frame_t *)buffer;
            uint8_t response[256];
            size_t response_len = sizeof(response);

            int result = cmd_handle_command(cmd, response, &response_len);

            // 응답 전송
            sendto(sockfd, response, response_len, 0,
                  (struct sockaddr *)&cliaddr, len);
        }

        usleep(10000);  // 10ms
    }

    close(sockfd);
    return NULL;
}
```

### 4. Debug Info Dump (main.c:680)
```c
else if (g_signal_received == SIGUSR1) {
    health_monitor_log(LOG_INFO, "main", "Debug info requested");

    // 시스템 상태 출력
    runtime_stats_t stats;
    health_monitor_get_stats(&stats);

    printf("=== System Debug Info ===\n");
    printf("Uptime: %u seconds\n", g_daemon_ctx.uptime_sec);
    printf("Frames received: %lu\n", stats.frames_received);
    printf("Frames sent: %lu\n", stats.frames_sent);
    printf("SPI errors: %lu\n", stats.spi_errors);
    printf("CSI2 errors: %lu\n", stats.csi2_errors);
    printf("Auth failures: %lu\n", stats.auth_failures);
    printf("=========================\n");

    g_signal_received = 0;
}
```

### 5. FPGA SPI Operations (sequence_engine.c)

#### 5.1 FPGA Configuration (Line 74)
```c
static int handle_configure_state(void) {
    // SPI를 통해 FPGA 설정 레지스터 쓰기 (가짜 구현)
    uint8_t config_data[8] = {
        0x01, // CONFIG register
        0x00, // Clear
        0x02, // Mode register
        0x01, // Single mode
        0x03, // Timing register
        0x00, // Default timing
        0x04, // Gain register
        0x80  // Default gain
    };

    // spi_master_write(&spi_ctx, config_data, sizeof(config_data));

    health_monitor_log(LOG_INFO, "seq", "FPGA configured");
    return transition_to(SEQ_STATE_ARM);
}
```

#### 5.2 FPGA ARM Command (Line 85)
```c
static int handle_arm_state(void) {
    // SPI를 통해 FPGA ARM 레지스터 쓰기
    uint8_t arm_data[4] = {
        0x05, // ARM register
        0x01, // ARM command
        0x00, // Padding
        0x00  // Padding
    };

    // spi_master_write(&spi_ctx, arm_data, sizeof(arm_data));

    health_monitor_log(LOG_INFO, "seq", "FPGA armed");
    return transition_to(SEQ_STATE_SCANNING);
}
```

#### 5.3 FPGA Reset (Line 145)
```static int handle_error_state(void) {
    if (seq_ctx.retry_count >= MAX_RETRY_COUNT) {
        return -ETIMEDOUT;
    }

    seq_ctx.retry_count++;
    seq_ctx.stats.retries++;

    // FPGA 리셋 (가짜 구현)
    uint8_t reset_data[4] = {
        0x00, // RESET register
        0x01, // Reset command
        0x00, // Padding
        0x00  // Padding
    };

    // spi_master_write(&spi_ctx, reset_data, sizeof(reset_data));

    health_monitor_log(LOG_WARNING, "seq", "FPGA reset attempt %u", seq_ctx.retry_count);
    return transition_to(SEQ_STATE_SCANNING);
}
```

#### 5.4 FPGA Stop Command (Line 228)
```c
int seq_stop_scan(void) {
    if (!seq_ctx.initialized) {
        return -EINVAL;
    }

    // FPGA STOP 명령 전송
    uint8_t stop_data[4] = {
        0x01, // CONTROL register
        0x02, // STOP command
        0x00, // Padding
        0x00  // Padding
    };

    // spi_master_write(&spi_ctx, stop_data, sizeof(stop_data));

    health_monitor_log(LOG_INFO, "seq", "Scan stopped");
    return transition_to(SEQ_STATE_IDLE);
}
```

## Category C: 실제 하드웨어 의존 항목

### FPGA Temperature (health_monitor.c:274)
- **의존**: 실제 SPI 통신 및 FPGA 온도 센서
- **구현**: `/dev/spidev0.0`을 통해 온도 레지스터 읽기
- **전문가**: FPGA/HW 엔지니어 필요

## 구현 우선순위

### Phase 1: Core SW Logic (Category A)
1. Command Protocol 모든 함수
2. Statistics aggregation
3. Configuration reload
4. Sequence engine state integration

### Phase 2: Mock Implementation (Category B)
1. V4L2 DQBUF 시뮬레이션
2. UDP transmission
3. UDP command listener
4. Debug info dump
5. FPGA SPI operations

### Phase 3: HW Integration (Category C)
1. FPGA temperature (전문가 협업 필요)

## 테스트 전략

### Unit Tests
- 각 TODO 함수별로 단위 테스트 작성
- Mock 객체를 사용한 독립 테스트
- Edge case 테스트 (에러 처리, 경계 값)

### Integration Tests
- 시스템 통합 테스트 (5개 스레드 상호작용)
- End-to-end 테스트 (명령 → 처리 → 응답)
- 부하 테스트 (초당 1000 프레임 처리)

### Test Coverage
- 최소 85% 코드 커버리지
- 메모리 누수 테스트 (Valgrind)
- 스레드 안전성 테스트

## 의존성 관리

### 외부 라이브러리
- OpenSSL (HMAC-SHA256)
- libv4l2 (V4L2)
- libsystemd (systemd 통합)

### 내부 모듈
- `health_monitor` - 모든 모듈에서 사용
- `sequence_engine` - 상태 관리
- `frame_manager` - 프레임 버퍼 관리
- `spi_master` - SPI 통합 (가짜 구현)

## 배포 점검리스트

- [ ] 모든 Category A TODO 구현 완료
- [ ] 모든 Category B TODO Mock 구현 완료
- [ ] Unit tests 통과 (85%+ coverage)
- [ ] Integration tests 통과
- [ ] 메모리 누수 없음
- [ ] 스레드 안전성 검증
- [ ] SIGTERM/SIGINT 그레이스ful shutdown 확인
- [ ] systemd 서비스로 등록 가능