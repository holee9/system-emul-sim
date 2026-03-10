# Hardware-Dependent TODOs

This document lists all TODO items that require actual hardware for implementation.

## Overview

Out of 19 total TODOs in the firmware:
- **9 TODOs** implemented (Category A+B: SW logic only)
- **10 TODOs** documented here (Category C: HW dependent)

---

## SPI / FPGA Communication (5 TODOs)

### 1. Write FPGA configuration registers via SPI
**File**: `fw/src/sequence_engine.c:74`
**Function**: `handle_configure_state()`

**Requirement**:
- Write FPGA configuration registers based on current scan mode
- Configure row/column count, bit depth, frame rate
- Set CSI-2 lane configuration

**Hardware Dependencies**:
- FPGA SPI slave interface
- SPI controller (Linux spidev)
- FPGA configuration register map

**Pseudo-code**:
```c
/* FPGA Configuration Register Map (example) */
#define FPGA_REG_CONFIG_ADDR     0x00
#define FPGA_REG_ROWS            0x01
#define FPGA_REG_COLS            0x02
#define FPGA_REG_BIT_DEPTH       0x03
#define FPGA_REG_FRAME_RATE      0x04
#define FPGA_REG_CSI2_LANES      0x05

static int handle_configure_state(void) {
    uint8_t config_data[6];

    /* Build configuration frame */
    config_data[0] = FPGA_REG_CONFIG_ADDR;
    config_data[1] = (current_config.rows >> 8) & 0xFF;
    config_data[2] = current_config.rows & 0xFF;
    config_data[3] = (current_config.cols >> 8) & 0xFF;
    config_data[4] = current_config.cols & 0xFF;
    config_data[5] = current_config.bit_depth;

    /* Write via SPI */
    return spi_master_write_reg(SPI_FPGA_CS, config_data, sizeof(config_data));
}
```

**Reference**: `fw/include/hal/spi_master.h`

---

### 2. Write FPGA ARM register via SPI
**File**: `fw/src/sequence_engine.c:85`
**Function**: `handle_arm_state()`

**Requirement**:
- Set FPGA ARM bit to enable scanning
- Configure trigger mode (auto vs. external)

**Hardware Dependencies**:
- FPGA control register
- SPI controller

**Pseudo-code**:
```c
#define FPGA_REG_CTRL        0x10
#define FPGA_CTRL_ARM_BIT    (1U << 0)

static int handle_arm_state(void) {
    uint8_t arm_cmd[2] = {
        FPGA_REG_CTRL,
        FPGA_CTRL_ARM_BIT
    };

    return spi_master_write_reg(SPI_FPGA_CS, arm_cmd, sizeof(arm_cmd));
}
```

---

### 3. Reset FPGA and retry
**File**: `fw/src/sequence_engine.c:145`
**Function**: `handle_error_state()`

**Requirement**:
- Pulse FPGA reset line
- Re-initialize FPGA configuration
- Retry the failed operation

**Hardware Dependencies**:
- FPGA reset GPIO line
- FPGA reset timing requirements

**Pseudo-code**:
```c
#define FPGA_RESET_GPIO_PIN  42

static int reset_fpga(void) {
    /* Pulse reset low for 10ms */
    gpio_set_value(FPGA_RESET_GPIO_PIN, 0);
    usleep(10000);
    gpio_set_value(FPGA_RESET_GPIO_PIN, 1);
    usleep(50000);  /* Wait for FPGA to boot */

    /* Re-configure */
    return handle_configure_state();
}
```

---

### 4. Send STOP command to FPGA via SPI
**File**: `fw/src/sequence_engine.c:228`
**Function**: `seq_stop_scan()`

**Requirement**:
- Clear FPGA ARM bit
- Stop current frame acquisition

**Hardware Dependencies**:
- FPGA control register
- SPI controller

**Pseudo-code**:
```c
#define FPGA_REG_CTRL        0x10
#define FPGA_CTRL_STOP_BIT   (1U << 1)

int seq_stop_scan(void) {
    if (!seq_ctx.initialized) {
        return -EINVAL;
    }

    uint8_t stop_cmd[2] = {
        FPGA_REG_CTRL,
        FPGA_CTRL_STOP_BIT
    };

    int rc = spi_master_write_reg(SPI_FPGA_CS, stop_cmd, sizeof(stop_cmd));
    if (rc != 0) {
        return rc;
    }

    /* Return to IDLE state */
    return transition_to(SEQ_STATE_IDLE);
}
```

---

### 5. SPI status polling
**File**: `fw/src/main.c:332`
**Function**: `spi_control_thread()`

**Requirement**:
- Poll FPGA STATUS register at 100us interval
- Check BUSY, ERROR, READY flags
- Generate events for state machine

**Hardware Dependencies**:
- FPGA status register
- SPI controller
- Real-time threading (SCHED_FIFO)

**Pseudo-code**:
```c
#define FPGA_REG_STATUS      0x20
#define FPGA_STATUS_BUSY     (1U << 0)
#define FPGA_STATUS_ERROR    (1U << 1)
#define FPGA_STATUS_READY    (1U << 2)

static void *spi_control_thread(void *arg) {
    daemon_context_t *ctx = (daemon_context_t *)arg;
    uint8_t status_cmd[2] = { FPGA_REG_STATUS, 0 };
    uint8_t status_data[1];

    prctl(PR_SET_NAME, "spi_ctrl", 0, 0, 0);

    while (ctx->running && !ctx->shutdown_requested) {
        /* Read FPGA STATUS register */
        int rc = spi_master_transfer(SPI_FPGA_CS, status_cmd, 2, status_data, 1);

        if (rc == 0) {
            uint8_t status = status_data[0];

            if (status & FPGA_STATUS_ERROR) {
                /* Trigger error event */
                seq_handle_event(EVT_ERROR, NULL);
            } else if (status & FPGA_STATUS_READY) {
                /* Trigger frame ready event */
                seq_handle_event(EVT_FRAME_READY, NULL);
            }
        }

        usleep(100);  /* 100us */
    }

    return NULL;
}
```

**Reference**: `fw/include/sequence_engine.h` for FPGA_STATUS_* macros

---

## V4L2 Camera Interface (1 TODO)

### 6. Implement V4L2 DQBUF
**File**: `fw/src/main.c:354`
**Function**: `csi2_rx_thread()`

**Requirement**:
- Dequeue frames from MIPI CSI-2 receiver
- Pass frames to frame manager
- Handle buffer errors

**Hardware Dependencies**:
- MIPI CSI-2 receiver hardware
- V4L2 driver (e.g., rcar-csi2)
- Camera sensor

**Pseudo-code**:
```c
#include <linux/videodev2.h>
#include <sys/ioctl.h>

static void *csi2_rx_thread(void *arg) {
    daemon_context_t *ctx = (daemon_context_t *)arg;
    struct v4l2_buffer buf;

    prctl(PR_SET_NAME, "csi2_rx", 0, 0, 0);

    while (ctx->running && !ctx->shutdown_requested) {
        memset(&buf, 0, sizeof(buf));
        buf.type = V4L2_BUF_TYPE_VIDEO_CAPTURE;
        buf.memory = V4L2_MEMORY_MMAP;

        /* Dequeue frame from V4L2 */
        if (ioctl(ctx->csi2_fd, VIDIOC_DQBUF, &buf) == 0) {
            /* Process frame */
            frame_manager_submit_frame(&ctx->frame_mgr,
                                      buf.m.userptr,
                                      buf.bytesused);

            /* Re-queue buffer */
            ioctl(ctx->csi2_fd, VIDIOC_QBUF, &buf);
        } else {
            if (errno == EAGAIN) {
                usleep(1000);  /* No frame available */
            } else {
                health_monitor_log(LOG_ERROR, "csi2_thread",
                                 "DQBUF failed: %s", strerror(errno));
                break;
            }
        }
    }

    return NULL;
}
```

**Reference**: `fw/include/hal/csi2_rx.h`

---

## UDP Networking (2 TODOs)

### 7. Implement UDP transmission
**File**: `fw/src/main.c:376`
**Function**: `eth_tx_thread()`

**Requirement**:
- Transmit frames via UDP to host
- Handle packet loss, reordering
- Rate limiting for network bandwidth

**Hardware Dependencies**:
- Ethernet controller (e.g., rAVB)
- Network interface

**Pseudo-code**:
```c
#include <sys/socket.h>
#include <netinet/udp.h>

static void *eth_tx_thread(void *arg) {
    daemon_context_t *ctx = (daemon_context_t *)arg;
    int sockfd;
    struct sockaddr_in dest_addr;

    /* Create UDP socket */
    sockfd = socket(AF_INET, SOCK_DGRAM, 0);
    if (sockfd < 0) {
        health_monitor_log(LOG_ERROR, "tx_thread", "socket() failed");
        return NULL;
    }

    /* Set destination address */
    memset(&dest_addr, 0, sizeof(dest_addr));
    dest_addr.sin_family = AF_INET;
    dest_addr.sin_port = htons(ctx->config.data_port);
    inet_pton(AF_INET, ctx->config.host_ip, &dest_addr.sin_addr);

    prctl(PR_SET_NAME, "eth_tx", 0, 0, 0);

    while (ctx->running && !ctx->shutdown_requested) {
        /* Get frame from queue */
        frame_t *frame = frame_manager_get_tx_frame(&ctx->frame_mgr);

        if (frame != NULL) {
            /* Transmit frame */
            ssize_t sent = sendto(sockfd, frame->data, frame->size, 0,
                                  (struct sockaddr*)&dest_addr, sizeof(dest_addr));

            if (sent < 0) {
                health_monitor_update_stat("errors", 1);
            } else {
                health_monitor_update_stat("bytes_sent", sent);
                health_monitor_update_stat("packets_sent", 1);
            }

            /* Free frame */
            frame_manager_release_frame(&ctx->frame_mgr, frame);
        } else {
            usleep(100);  /* 100us */
        }
    }

    close(sockfd);
    return NULL;
}
```

**Reference**: `fw/include/hal/eth_tx.h`

---

### 8. Implement UDP command listener
**File**: `fw/src/main.c:398`
**Function**: `command_thread()`

**Requirement**:
- Listen on UDP port 8001 for commands
- Parse and authenticate commands
- Dispatch to command protocol handler

**Hardware Dependencies**:
- Ethernet controller

**Pseudo-code**:
```c
static void *command_thread(void *arg) {
    daemon_context_t *ctx = (daemon_context_t *)arg;
    int sockfd;
    struct sockaddr_in serv_addr, client_addr;
    socklen_t client_len = sizeof(client_addr);
    uint8_t recv_buf[512];

    /* Create UDP socket */
    sockfd = socket(AF_INET, SOCK_DGRAM, 0);
    if (sockfd < 0) {
        health_monitor_log(LOG_ERROR, "cmd_thread", "socket() failed");
        return NULL;
    }

    /* Bind to control port */
    memset(&serv_addr, 0, sizeof(serv_addr));
    serv_addr.sin_family = AF_INET;
    serv_addr.sin_addr.s_addr = INADDR_ANY;
    serv_addr.sin_port = htons(ctx->config.control_port);

    if (bind(sockfd, (struct sockaddr*)&serv_addr, sizeof(serv_addr)) < 0) {
        health_monitor_log(LOG_ERROR, "cmd_thread", "bind() failed");
        close(sockfd);
        return NULL;
    }

    prctl(PR_SET_NAME, "command", 0, 0, 0);

    while (ctx->running && !ctx->shutdown_requested) {
        ssize_t recv_len = recvfrom(sockfd, recv_buf, sizeof(recv_buf), 0,
                                     (struct sockaddr*)&client_addr, &client_len);

        if (recv_len > 0) {
            /* Parse command */
            uint8_t resp_buf[512];
            size_t resp_len = sizeof(resp_buf);

            int rc = cmd_protocol_parse(recv_buf, recv_len,
                                       resp_buf, &resp_len,
                                       inet_ntoa(client_addr.sin_addr));

            if (rc == 0) {
                /* Send response */
                sendto(sockfd, resp_buf, resp_len, 0,
                       (struct sockaddr*)&client_addr, client_len);
            }
        } else if (recv_len < 0 && errno != EAGAIN) {
            health_monitor_log(LOG_ERROR, "cmd_thread", "recvfrom() failed");
            break;
        }
    }

    close(sockfd);
    return NULL;
}
```

**Reference**: `fw/include/protocol/command_protocol.h`

---

## I2C Sensors (2 TODOs)

### 9. Get battery metrics from BQ40z50 driver
**File**: `fw/src/health_monitor.c:268`
**Function**: `health_monitor_get_status()`

**Requirement**:
- Read battery state of charge (SoC)
- Read battery voltage
- Read current, temperature

**Hardware Dependencies**:
- BQ40z50 fuel gauge IC
- I2C bus (e.g., i2c-0)

**Pseudo-code**:
```c
#include <linux/i2c-dev.h>
#include <sys/ioctl.h>

#define BQ40Z50_I2C_ADDR     0x0B
#define BQ40Z50_REG_SOC      0x2C  /* State of Charge */
#define BQ40z50_REG_VOLTAGE  0x08  /* Voltage in mV */
#define BQ40z50_REG_CURRENT  0x0A  /* Current in mA */

static int bq40z50_read_reg(uint8_t reg, int16_t *value) {
    int i2c_fd = open("/dev/i2c-0", O_RDWR);
    if (i2c_fd < 0) return -1;

    ioctl(i2c_fd, I2C_SLAVE, BQ40Z50_I2C_ADDR);

    /* Write register address */
    write(i2c_fd, &reg, 1);

    /* Read 2 bytes (little-endian) */
    uint8_t buf[2];
    read(i2c_fd, buf, 2);
    *value = (int16_t)((buf[1] << 8) | buf[0]);

    close(i2c_fd);
    return 0;
}

/* In health_monitor_get_status(): */
int16_t soc, voltage;
bq40z50_read_reg(BQ40Z50_REG_SOC, &soc);
bq40z50_read_reg(BQ40Z50_REG_VOLTAGE, &voltage);

status->battery_soc = (uint8_t)(soc / 100);  /* Percentage */
status->battery_mv = (uint16_t)voltage;
```

**Reference**: `fw/include/hal/bq40z50_driver.h`

---

### 10. Get FPGA temperature from SPI registers
**File**: `fw/src/health_monitor.c:274`
**Function**: `health_monitor_get_status()`

**Requirement**:
- Read FPGA on-die temperature sensor
- Report in 0.1°C units

**Hardware Dependencies**:
- FPGA temperature sensor (Xilinx SYSMONE or similar)
- SPI controller

**Pseudo-code**:
```c
#define FPGA_REG_TEMP       0x30

/* In health_monitor_get_status(): */
uint8_t temp_cmd[2] = { FPGA_REG_TEMP, 0 };
uint8_t temp_data[2];

int rc = spi_master_transfer(SPI_FPGA_CS, temp_cmd, 2, temp_data, 2);
if (rc == 0) {
    /* Convert to 0.1°C units */
    int16_t temp_raw = (int16_t)((temp_data[1] << 8) | temp_data[0]);
    status->fpga_temp = (uint16_t)(temp_raw / 10);  /* Assume sensor reports in m°C */
} else {
    status->fpga_temp = 350;  /* Default: 35.0°C */
}
```

---

## Implementation Notes

### Testing Strategy
Since actual hardware may not be available for development:

1. **Mock Hardware Layer**: Create mock implementations in `fw/src/hal/mock_*.c`
2. **Unit Tests**: Use `TESTING` macro to enable mock functions
3. **Integration Tests**: Use simulator (C#) to validate firmware protocol

### Register Maps
The following register maps need to be defined based on actual FPGA design:
- FPGA Configuration Registers (0x00-0x0F)
- FPGA Control Registers (0x10-0x1F)
- FPGA Status Registers (0x20-0x2F)
- FPGA Sensor Registers (0x30-0x3F)

### Hardware Abstraction
All hardware access should go through HAL layer:
- `fw/include/hal/spi_master.h`
- `fw/include/hal/csi2_rx.h`
- `fw/include/hal/eth_tx.h`
- `fw/include/hal/bq40z50_driver.h`

This allows easy mocking for testing.

---

## References

- **SPEC-EMUL-001**: 168 verification scenarios
- **REQ-FW-030**: SPI communication with FPGA
- **REQ-FW-020**: MIPI CSI-2 reception
- **REQ-FW-040**: Ethernet transmission
- **REQ-FW-050**: Command protocol

---

**Last Updated**: 2026-03-10
**Status**: 9/19 TODOs implemented (47%), 10/19 documented (HW-dependent)
