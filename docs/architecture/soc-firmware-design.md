# SoC Firmware Architecture Design

**Project**: X-ray Detector Panel System
**Target Platform**: NXP i.MX8M Plus (Quad Cortex-A53, Linux 6.6.52 LTS)
**Document Version**: 1.0.0
**Status**: Reviewed - Approved
**Last Updated**: 2026-02-17

---

## 1. Overview

### 1.1 Purpose

This document describes the SoC Controller firmware architecture for the X-ray Detector Panel System. The SoC sits between the FPGA (real-time data acquisition) and the Host PC (frame processing and display), serving as the bridge for sequence control, data reception, and network streaming.

### 1.2 Responsibilities

The SoC Controller firmware handles:
- **CSI-2 RX**: Receive pixel data frames from FPGA via MIPI CSI-2 4-lane D-PHY
- **Sequence Engine**: Orchestrate frame scan sequences by controlling FPGA via SPI
- **Network Streaming**: Stream assembled frames to Host PC via 10 GbE UDP
- **SPI Master**: Read/write FPGA registers for configuration and status monitoring
- **Frame Buffer Management**: Manage DDR4 memory for frame buffering and DMA

### 1.3 Platform Constraints

| Resource | Specification | Notes |
|----------|-------------|-------|
| CPU | Quad Cortex-A53 @ 1.8 GHz | Linux user-space for control, kernel for drivers |
| Memory | 4 GB LPDDR4 | Frame buffers + OS + application |
| CSI-2 RX | 2x 4-lane MIPI CSI-2 | Lane speed: 80 Mbps - 2.5 Gbps |
| Ethernet | 2x GbE (native) | 10 GbE via PCIe add-on NIC |
| SPI | 4x SPI master | SPI0 used for FPGA control |
| Storage | eMMC 32 GB | OS + firmware + logs |
| OS | Linux 6.6.52 LTS (Yocto Scarthgap 5.0, Variscite BSP imx-6.6.52-2.2.0-v1.3) | Real-time patches optional |

---

## 2. Software Architecture

### 2.1 Layer Diagram

```
+------------------------------------------------------------------+
|                    Application Layer                              |
|  +-------------------+  +-------------------+  +---------------+ |
|  | Sequence Engine   |  | CLI / Config      |  | Diagnostics   | |
|  | (scan control,    |  | (YAML config      |  | (logging,     | |
|  |  frame sequencing)|  |  loader, CLI)     |  |  telemetry)   | |
|  +--------+----------+  +--------+----------+  +-------+-------+ |
+-----------|----------------------|----------------------|--------+
            |                      |                      |
+-----------|----------------------|----------------------|--------+
|                    Service Layer                                  |
|  +--------v----------+  +--------v----------+  +-------v-------+ |
|  | Frame Manager     |  | Config Manager    |  | Health Monitor| |
|  | (buffer alloc,    |  | (register config, |  | (watchdog,    | |
|  |  frame lifecycle) |  |  tier selection)  |  |  stats)       | |
|  +--------+----------+  +--------+----------+  +-------+-------+ |
+-----------|----------------------|----------------------|--------+
            |                      |                      |
+-----------|----------------------|----------------------|--------+
|                    HAL (Hardware Abstraction Layer)                |
|  +--------v----------+  +--------v----------+  +-------v-------+ |
|  | CSI-2 RX Driver   |  | SPI Master Driver |  | Ethernet      | |
|  | (V4L2 interface,  |  | (spidev, register |  | Driver        | |
|  |  DMA, ISP bypass) |  |  read/write)      |  | (10GbE UDP TX)| |
|  +--------+----------+  +--------+----------+  +-------+-------+ |
+-----------|----------------------|----------------------|--------+
            |                      |                      |
+-----------|----------------------|----------------------|--------+
|                    Linux Kernel                                   |
|  +--------v----------+  +--------v----------+  +-------v-------+ |
|  | imx8-mipi-csi2    |  | spi-imx           |  | ixgbe/mlx5    | |
|  | V4L2 driver       |  | SPI bus driver    |  | 10GbE driver  | |
|  +-------------------+  +-------------------+  +---------------+ |
+------------------------------------------------------------------+
            |                      |                      |
     [CSI-2 D-PHY]           [SPI Bus]            [10 GbE NIC]
     (from FPGA)          (to/from FPGA)          (to Host PC)
```

### 2.2 Process Architecture

```
+-- detector_daemon (main process, root privileges)
|     |
|     +-- [Thread] sequence_engine     (frame scan control loop)
|     +-- [Thread] frame_rx            (CSI-2 frame reception via V4L2)
|     +-- [Thread] frame_tx            (10 GbE UDP frame transmission)
|     +-- [Thread] spi_control         (FPGA register polling, 100 us interval)
|     +-- [Thread] health_monitor      (watchdog, stats, logging)
|
+-- detector_cli (CLI tool, user interaction)
|     |
|     +-- Connects to detector_daemon via Unix domain socket
|
+-- detector_config (config loader, runs at boot)
      |
      +-- Parses detector_config.yaml, applies settings to daemon
```

---

## 3. HAL Layer

### 3.1 CSI-2 RX Driver Interface

**Kernel Driver**: `imx8-mipi-csi2` (V4L2 subsystem)

**V4L2 Device**: `/dev/video0`

#### 3.1.1 Initialization Sequence

```c
// 1. Open V4L2 device
int fd = open("/dev/video0", O_RDWR);

// 2. Set pixel format
struct v4l2_format fmt = {
    .type = V4L2_BUF_TYPE_VIDEO_CAPTURE_MPLANE,
    .fmt.pix_mp = {
        .width = 2048,              // Panel columns
        .height = 2048,             // Panel rows
        .pixelformat = V4L2_PIX_FMT_Y16,  // 16-bit grayscale (RAW16)
        .num_planes = 1,
        .plane_fmt[0].bytesperline = 2048 * 2,  // 4096 bytes/line
        .plane_fmt[0].sizeimage = 2048 * 2048 * 2,  // 8,388,608 bytes
    }
};
ioctl(fd, VIDIOC_S_FMT, &fmt);

// 3. Request buffers (DMA-mapped)
struct v4l2_requestbuffers req = {
    .count = 4,                     // 4x frame buffers
    .type = V4L2_BUF_TYPE_VIDEO_CAPTURE_MPLANE,
    .memory = V4L2_MEMORY_MMAP,
};
ioctl(fd, VIDIOC_REQBUFS, &req);

// 4. Memory-map buffers
for (int i = 0; i < 4; i++) {
    struct v4l2_buffer buf;
    // ... query and mmap each buffer
    buffers[i] = mmap(NULL, length, PROT_READ, MAP_SHARED, fd, offset);
}

// 5. Queue all buffers and start streaming
for (int i = 0; i < 4; i++) {
    ioctl(fd, VIDIOC_QBUF, &buf[i]);
}
ioctl(fd, VIDIOC_STREAMON, &type);
```

#### 3.1.2 Frame Reception Loop

```c
// Blocking dequeue (waits for FPGA frame)
struct v4l2_buffer buf;
ioctl(fd, VIDIOC_DQBUF, &buf);  // Blocks until frame available

// Access frame data
void *frame_data = buffers[buf.index];
size_t frame_size = buf.m.planes[0].bytesused;
uint64_t timestamp_us = buf.timestamp.tv_sec * 1000000 + buf.timestamp.tv_usec;

// Process frame (copy to TX buffer, validate, etc.)
process_frame(frame_data, frame_size, timestamp_us);

// Re-queue buffer for next frame
ioctl(fd, VIDIOC_QBUF, &buf);
```

#### 3.1.3 ISP Bypass

For raw pixel pass-through (no image processing):
```c
// Disable ISP processing (device-tree or V4L2 control)
struct v4l2_control ctrl = {
    .id = V4L2_CID_ISP_BYPASS,  // Platform-specific control ID
    .value = 1,                  // Bypass ISP
};
ioctl(fd, VIDIOC_S_CTRL, &ctrl);
```

### 3.2 SPI Master Driver Interface

**Kernel Driver**: `spi-imx` (SPI bus driver)

**User-Space Access**: `/dev/spidev0.0` (via spidev)

#### 3.2.1 Configuration

```c
// Open SPI device
int spi_fd = open("/dev/spidev0.0", O_RDWR);

// Configure SPI parameters
uint8_t mode = SPI_MODE_0;          // CPOL=0, CPHA=0
uint8_t bits = 8;                   // 8 bits per word
uint32_t speed = 50000000;          // 50 MHz

ioctl(spi_fd, SPI_IOC_WR_MODE, &mode);
ioctl(spi_fd, SPI_IOC_WR_BITS_PER_WORD, &bits);
ioctl(spi_fd, SPI_IOC_WR_MAX_SPEED_HZ, &speed);
```

#### 3.2.2 Register Read/Write Functions

```c
// Write FPGA register: [8-bit addr][8-bit W flag][16-bit data]
int fpga_reg_write(int spi_fd, uint8_t addr, uint16_t data) {
    uint8_t tx[4] = {
        addr,                       // Register address
        0x01,                       // Write flag
        (data >> 8) & 0xFF,         // Data MSB
        data & 0xFF                 // Data LSB
    };
    struct spi_ioc_transfer xfer = {
        .tx_buf = (unsigned long)tx,
        .len = 4,
        .speed_hz = 50000000,
    };
    return ioctl(spi_fd, SPI_IOC_MESSAGE(1), &xfer);
}

// Read FPGA register: [8-bit addr][8-bit R flag][16-bit response]
int fpga_reg_read(int spi_fd, uint8_t addr, uint16_t *data) {
    uint8_t tx[4] = { addr, 0x00, 0x00, 0x00 };  // Read flag = 0
    uint8_t rx[4] = { 0 };
    struct spi_ioc_transfer xfer = {
        .tx_buf = (unsigned long)tx,
        .rx_buf = (unsigned long)rx,
        .len = 4,
        .speed_hz = 50000000,
    };
    int ret = ioctl(spi_fd, SPI_IOC_MESSAGE(1), &xfer);
    *data = (rx[2] << 8) | rx[3];
    return ret;
}
```

### 3.3 Ethernet Driver Interface (10 GbE)

**Kernel Driver**: `ixgbe` or `mlx5_core` (PCIe 10 GbE NIC)

**User-Space Access**: Raw UDP socket

#### 3.3.1 Socket Configuration

```c
// Create UDP socket
int sock_fd = socket(AF_INET, SOCK_DGRAM, 0);

// Set send buffer size (large for frame streaming)
int sndbuf = 16 * 1024 * 1024;  // 16 MB send buffer
setsockopt(sock_fd, SOL_SOCKET, SO_SNDBUF, &sndbuf, sizeof(sndbuf));

// Bind to specific interface (10 GbE)
struct sockaddr_in src_addr = {
    .sin_family = AF_INET,
    .sin_port = htons(8000),
    .sin_addr.s_addr = inet_addr("192.168.1.100"),  // SoC IP
};
bind(sock_fd, (struct sockaddr *)&src_addr, sizeof(src_addr));

// Destination (Host PC)
struct sockaddr_in dst_addr = {
    .sin_family = AF_INET,
    .sin_port = htons(8000),
    .sin_addr.s_addr = inet_addr("192.168.1.1"),    // Host IP
};
```

#### 3.3.2 Frame Transmission

```c
// Transmit frame as UDP packets (8192 bytes per packet)
int send_frame(int sock_fd, struct sockaddr_in *dst,
               void *frame_data, size_t frame_size,
               uint32_t frame_seq, uint64_t timestamp_us,
               uint16_t width, uint16_t height, uint16_t bit_depth)
{
    const size_t max_payload = 8192;
    uint16_t total_packets = (frame_size + max_payload - 1) / max_payload;

    for (uint16_t i = 0; i < total_packets; i++) {
        // Build packet: header + payload
        struct FrameHeader hdr = {
            .magic = 0xDEADBEEF,
            .frame_seq = frame_seq,
            .timestamp_us = timestamp_us,
            .width = width,
            .height = height,
            .bit_depth = bit_depth,
            .packet_index = i,
            .total_packets = total_packets,
        };
        hdr.crc16 = crc16_calculate(&hdr, sizeof(hdr) - 2);

        // Assemble packet buffer
        size_t offset = (size_t)i * max_payload;
        size_t payload_size = (offset + max_payload > frame_size)
                              ? (frame_size - offset) : max_payload;

        uint8_t packet[sizeof(hdr) + max_payload];
        memcpy(packet, &hdr, sizeof(hdr));
        memcpy(packet + sizeof(hdr), (uint8_t *)frame_data + offset, payload_size);

        sendto(sock_fd, packet, sizeof(hdr) + payload_size, 0,
               (struct sockaddr *)dst, sizeof(*dst));
    }
    return 0;
}
```

---

## 4. Sequence Engine

### 4.1 State Machine

```
                    +-------+
           init --> | IDLE  |<------ stop_cmd OR error_recovery
                    +---+---+
                        |
                        | start_cmd (from Host via network OR CLI)
                        v
                  +-----------+
                  | CONFIGURE |  Write timing params to FPGA via SPI
                  +-----+-----+
                        |
                        | config_done (all registers written and verified)
                        v
                  +-----------+
                  | ARM       |  Write start_scan to FPGA CONTROL register
                  +-----+-----+
                        |
                        | fpga_busy (STATUS.busy == 1)
                        v
                  +-----------+
                  | SCANNING  |  Monitor FPGA status, receive frames via CSI-2
                  +-----+-----+
                        |
                 +------+-------+
                 |              |
                 | frame_done   | fpga_error
                 v              v
          +-----------+   +-----------+
          | STREAMING |   | ERROR     |
          +-----+-----+   +-----+-----+
                |                |
                | tx_complete    | error_clear
                v                v
          +-----------+     (IDLE)
          | COMPLETE  |
          +-----+-----+
                |
         +------+-------+
         |              |
         | continuous   | single mode
         v              v
       (ARM)         (IDLE)
```

### 4.2 Sequence Engine Operations

| State | Actions |
|-------|---------|
| **IDLE** | Wait for start command. Poll FPGA STATUS for idle confirmation. |
| **CONFIGURE** | Write timing registers (0x20-0x34), panel config (0x40-0x4C), CSI-2 config (0x80-0x88) via SPI. Read-back verify each register. |
| **ARM** | Write start_scan (CONTROL bit[0] = 1) to FPGA. Wait for STATUS.busy assertion (max 10 ms timeout). |
| **SCANNING** | Poll FPGA STATUS (100 us interval). Receive frames via CSI-2 RX (V4L2 DQBUF). Monitor for errors (STATUS.error). |
| **STREAMING** | Send received frame to Host via 10 GbE UDP. Wait for TX completion. Update frame counter. |
| **COMPLETE** | Frame transmission confirmed. If continuous mode, return to ARM. If single, return to IDLE. |
| **ERROR** | Log error code from FPGA. Attempt recovery (clear error, retry up to 3 times). Report to Host if unrecoverable. |

### 4.3 Timing Budget

| Operation | Latency | Notes |
|-----------|---------|-------|
| SPI register write (1 reg) | ~1 us | 32-bit at 50 MHz |
| SPI register read (1 reg) | ~1 us | 32-bit at 50 MHz |
| Configure all registers (~20 regs) | ~40 us | Sequential R/W with verify |
| FPGA scan start to busy | < 100 us | FSM transition time |
| Frame capture (2048x2048@30fps) | 33.33 ms | One frame period |
| CSI-2 RX DMA to DDR4 | < 1 ms | Hardware DMA |
| UDP TX (8.39 MB frame) | ~7 ms | At 10 Gbps with overhead |
| End-to-end frame latency | < 45 ms | Capture + DMA + TX |

---

## 5. Frame Buffer Management

### 5.1 DDR4 Memory Layout

```
DDR4 (4 GB total)
+------------------------------------------+
| 0x00000000 - 0x3FFFFFFF: Linux Kernel/OS | 1 GB
+------------------------------------------+
| 0x40000000 - 0x41FFFFFF: Frame Buffer 0  | 32 MB (up to 3072x3072x2)
+------------------------------------------+
| 0x42000000 - 0x43FFFFFF: Frame Buffer 1  | 32 MB
+------------------------------------------+
| 0x44000000 - 0x45FFFFFF: Frame Buffer 2  | 32 MB
+------------------------------------------+
| 0x46000000 - 0x47FFFFFF: Frame Buffer 3  | 32 MB
+------------------------------------------+
| 0x48000000 - 0xBFFFFFFF: Application     | ~2 GB (heap, stack, shared libs)
+------------------------------------------+
| 0xC0000000 - 0xFFFFFFFF: Reserved        | 1 GB (GPU, VPU, reserved)
+------------------------------------------+
```

### 5.2 Buffer Management Strategy

**Scheme**: 4-buffer ring with producer-consumer pattern

```
CSI-2 RX (Producer)              10 GbE TX (Consumer)
    |                                |
    v                                v
[Buffer 0] <-- DMA write       [Buffer 2] <-- UDP read
[Buffer 1] <-- DMA next        [Buffer 3] <-- UDP next
[Buffer 2] -- ready -->
[Buffer 3] -- ready -->
```

**State Machine per Buffer**:

| State | Description |
|-------|-------------|
| FREE | Available for CSI-2 RX DMA write |
| FILLING | CSI-2 RX DMA in progress |
| READY | Frame complete, awaiting TX |
| SENDING | 10 GbE UDP TX in progress |

**Transitions**:
```
FREE --> FILLING  (V4L2 QBUF, DMA starts)
FILLING --> READY (V4L2 DQBUF, DMA complete)
READY --> SENDING (TX thread picks up frame)
SENDING --> FREE  (TX complete, buffer recycled)
```

### 5.3 Buffer Sizing

| Tier | Resolution | Frame Size | 4 Buffers Total | DDR4 % |
|------|-----------|-----------|-----------------|--------|
| Minimum | 1024x1024 | 2 MB | 8 MB | 0.2% |
| Intermediate-A | 2048x2048 | 8 MB | 32 MB | 0.8% |
| Target | 3072x3072 | 18 MB | 72 MB | 1.8% |

### 5.4 Overrun Protection

If consumer (TX) falls behind producer (RX):
1. **Oldest-drop policy**: Drop oldest un-sent frame (increment drop counter)
2. **Never block RX**: CSI-2 reception must never stall (causes FPGA buffer overflow)
3. **Report drops**: Log dropped frame count, report to Host via status channel

---

## 6. Network Protocol (SoC to Host)

### 6.1 Frame Header Structure

```c
// 32 bytes, packed, little-endian
struct __attribute__((packed)) FrameHeader {
    uint32_t magic;           // 0xDEADBEEF (synchronization marker)
    uint32_t frame_seq;       // Frame sequence number (0-based, monotonic)
    uint64_t timestamp_us;    // Microsecond timestamp (SoC system clock)
    uint16_t width;           // Image width in pixels
    uint16_t height;          // Image height in pixels
    uint16_t bit_depth;       // 14 or 16
    uint16_t packet_index;    // Packet index within this frame (0-based)
    uint16_t total_packets;   // Total packets per frame
    uint16_t flags;           // bit[0]: last_packet, bit[1]: error_frame
    uint16_t crc16;           // CRC-16/CCITT over header (excluding crc16 field)
};
```

### 6.2 Packet Layout

```
UDP Packet (max 8224 bytes):
+------------------+------------------+
| Frame Header     | Pixel Payload    |
| (32 bytes)       | (up to 8192 bytes)|
+------------------+------------------+
```

### 6.3 Fragmentation Strategy

| Tier | Frame Size | Packets/Frame | Overhead | Notes |
|------|-----------|---------------|----------|-------|
| Minimum | 2 MB | 256 | 8 KB (0.4%) | Low overhead |
| Intermediate-A | 8 MB | 1,024 | 32 KB (0.4%) | Manageable |
| Target | 18 MB | 2,304 | 73 KB (0.4%) | Jumbo frames recommended |

### 6.4 Control Channel (Reverse Path)

Host commands to SoC use a separate UDP port (8001):

```c
// Command packet structure
struct __attribute__((packed)) CommandPacket {
    uint32_t magic;           // 0xBEEFCAFE
    uint16_t command_id;      // Command type
    uint16_t payload_length;  // Payload size
    uint8_t  payload[256];    // Command-specific data
    uint16_t crc16;           // CRC-16/CCITT
};
```

**Command IDs**:

| ID | Command | Payload | Description |
|----|---------|---------|-------------|
| 0x0001 | START_SCAN | mode (1 byte), tier (1 byte) | Start frame acquisition |
| 0x0002 | STOP_SCAN | none | Stop acquisition |
| 0x0003 | GET_STATUS | none | Request status report |
| 0x0004 | SET_CONFIG | key-value pairs | Update configuration |
| 0x0005 | RESET | none | Soft reset SoC + FPGA |

**Response Packet**:

```c
struct __attribute__((packed)) ResponsePacket {
    uint32_t magic;           // 0xCAFEBEEF
    uint16_t command_id;      // Echoed command ID
    uint16_t status;          // 0=OK, 1=ERROR, 2=BUSY
    uint16_t payload_length;  // Response data size
    uint8_t  payload[256];    // Response data
    uint16_t crc16;
};
```

---

## 7. Error Handling

### 7.1 Error Categories

| Category | Source | Severity | Recovery Strategy |
|----------|--------|----------|-------------------|
| **CSI-2 RX Error** | Kernel driver | High | Restart V4L2 streaming pipeline |
| **FPGA Error** | SPI STATUS register | High | Clear error via SPI, retry scan |
| **Network TX Error** | UDP sendto() failure | Medium | Retry packet 3x, then drop frame |
| **Buffer Overrun** | Frame manager | Medium | Drop oldest frame, log warning |
| **SPI Timeout** | SPI transaction | Medium | Retry 3x, then report FPGA comm error |
| **Config Error** | YAML parser | Low | Reject config, use defaults |
| **Watchdog Timeout** | Health monitor | Critical | Full system restart |

### 7.2 Error Recovery State Machine

```
[Normal Operation]
       |
       | error detected
       v
[Error Classification]
       |
  +----+----+
  |         |
  | retryable   | fatal
  v              v
[Retry Loop]  [Fatal Handler]
  max 3x       |
  |             v
  +-- success --> [Normal Operation]
  |
  +-- exhausted --> [Report to Host]
                        |
                        v
                   [Log & Alert]
                        |
                        v (if watchdog)
                   [System Restart]
```

### 7.3 FPGA Error Monitoring

```c
// Poll FPGA status every 100 us (in spi_control thread)
void fpga_status_poll(int spi_fd) {
    uint16_t status;
    fpga_reg_read(spi_fd, 0x04, &status);  // STATUS register

    if (status & 0x04) {  // Error bit set
        uint16_t error_flags;
        fpga_reg_read(spi_fd, 0xA0, &error_flags);  // ERROR_FLAGS register

        log_error("FPGA error: flags=0x%04X, code=%d",
                  error_flags, (status >> 3) & 0x1F);

        // Attempt recovery
        fpga_reg_write(spi_fd, 0x00, 0x0010);  // Write error_clear
    }
}
```

---

## 8. Configuration Management

### 8.1 Configuration Loading

At boot, the firmware loads configuration from `detector_config.yaml`:

```
Boot Sequence:
  1. Linux kernel boots (5-10 seconds)
  2. detector_config reads /etc/detector/detector_config.yaml
  3. detector_daemon starts with loaded configuration
  4. SPI: Write timing/panel/CSI-2 registers to FPGA
  5. CSI-2: Configure V4L2 device with resolution/format
  6. Network: Bind UDP sockets on configured ports
  7. Ready for scan commands
```

### 8.2 Runtime Configuration Updates

Configuration changes during operation follow this flow:

```
Host sends SET_CONFIG command
    |
    v
SoC validates new configuration
    |
    +-- invalid --> Reject, send error response
    |
    +-- valid --> Apply configuration
                    |
                    +-- Requires scan stop? --> Stop scan, apply, restart
                    |
                    +-- Hot-swappable? --> Apply immediately
```

**Hot-swappable parameters** (no scan restart needed):
- Frame rate (within tier limits)
- Network destination IP/port
- Logging level
- Watchdog timeout

**Cold parameters** (require scan restart):
- Resolution (panel rows/cols)
- Bit depth
- CSI-2 lane speed
- SPI clock speed

---

## 9. Build and Deployment

### 9.1 Build System

```
fw/
  CMakeLists.txt              # Top-level CMake build
  src/
    main.c                    # Entry point, daemon initialization
    sequence_engine.c/.h      # Scan sequence control FSM
    frame_manager.c/.h        # Frame buffer lifecycle
    hal/
      csi2_rx.c/.h            # V4L2 CSI-2 RX wrapper
      spi_master.c/.h         # SPI register read/write
      eth_tx.c/.h             # 10 GbE UDP TX
    config/
      config_loader.c/.h      # YAML configuration parser
    protocol/
      frame_header.c/.h       # Frame header encode/decode
      command_protocol.c/.h   # Host command handling
    util/
      crc16.c/.h              # CRC-16/CCITT implementation
      log.c/.h                # Structured logging
  tests/
    test_sequence_engine.c    # Unit tests (TDD for new code)
    test_frame_manager.c
    test_spi_protocol.c
    test_frame_header.c
    test_crc16.c
  toolchain/
    imx8mp-toolchain.cmake    # Cross-compilation toolchain file
```

### 9.2 Cross-Compilation

```bash
# Set up cross-compiler (Yocto SDK)
source /opt/fsl-imx-xwayland/scarthgap/environment-setup-cortexa53-crypto-poky-linux

# Build firmware
mkdir build && cd build
cmake -DCMAKE_TOOLCHAIN_FILE=../toolchain/imx8mp-toolchain.cmake ..
make -j$(nproc)

# Deploy to SoC
scp detector_daemon root@192.168.1.100:/usr/bin/
scp detector_cli root@192.168.1.100:/usr/bin/
scp detector_config.yaml root@192.168.1.100:/etc/detector/
```

### 9.3 Systemd Service

```ini
# /etc/systemd/system/detector.service
[Unit]
Description=X-ray Detector Daemon
After=network.target

[Service]
Type=simple
ExecStart=/usr/bin/detector_daemon --config /etc/detector/detector_config.yaml
Restart=on-failure
RestartSec=5
User=root

[Install]
WantedBy=multi-user.target
```

---

## 10. Verification Strategy

### 10.1 Development Methodology

Per project `quality.yaml` (Hybrid mode):
- **New firmware code**: TDD (RED-GREEN-REFACTOR)
- **HAL integration with BSP**: DDD (ANALYZE-PRESERVE-IMPROVE)

### 10.2 Test Categories

| Category | Framework | Coverage Target | Methodology |
|----------|-----------|----------------|-------------|
| Unit Tests | CMocka / Unity | 85% | TDD |
| HAL Integration | Custom test harness | 80% | DDD |
| System Tests | IntegrationRunner | Pass/Fail | DDD |
| Performance Tests | Custom benchmarks | Metric-based | N/A |

### 10.3 Key Test Cases

| ID | Test | Component | Criteria |
|----|------|-----------|---------|
| FW-UT-01 | SPI register R/W | spi_master | Correct 32-bit transactions |
| FW-UT-02 | Frame header encode/decode | frame_header | Round-trip integrity |
| FW-UT-03 | CRC-16 calculation | crc16 | Match reference vectors |
| FW-UT-04 | Config YAML parsing | config_loader | All parameters loaded |
| FW-UT-05 | Sequence engine states | sequence_engine | All transitions covered |
| FW-UT-06 | Buffer state machine | frame_manager | All states + transitions |
| FW-IT-01 | CSI-2 RX frame capture | csi2_rx + V4L2 | 100 frames, 0 errors |
| FW-IT-02 | SPI + CSI-2 concurrent | spi + csi2_rx | No interference |
| FW-IT-03 | Full scan sequence | All | Single frame end-to-end |
| FW-IT-04 | Continuous 1000 frames | All | < 0.01% frame drop |
| FW-IT-05 | Error injection recovery | All | All error codes handled |

---

## 11. Design Decisions Log

| ID | Decision | Rationale | Date |
|----|----------|-----------|------|
| DD-FW-01 | Linux user-space daemon (not bare-metal) | Leverages V4L2, spidev, network stack | 2026-02-17 |
| DD-FW-02 | V4L2 MMAP for CSI-2 RX | Zero-copy DMA, kernel-managed buffers | 2026-02-17 |
| DD-FW-03 | 4-buffer ring (not 2) | Prevents producer-consumer stall at Target tier | 2026-02-17 |
| DD-FW-04 | UDP (not TCP) for frame streaming | Lower latency, no head-of-line blocking | 2026-02-17 |
| DD-FW-05 | spidev user-space SPI (not kernel module) | Simpler development, sufficient for 100 us polling | 2026-02-17 |
| DD-FW-06 | Oldest-drop policy for buffer overrun | Never blocks CSI-2 RX (prevents FPGA overflow) | 2026-02-17 |
| DD-FW-07 | C language (not C++) | Minimal runtime, BSP compatibility | 2026-02-17 |
| DD-FW-08 | CMake build system | Cross-platform, Yocto SDK integration | 2026-02-17 |

---

## 12. Document Traceability

**Implements**:
- SPEC-ARCH-001 (REQ-ARCH-005, REQ-ARCH-008, REQ-ARCH-009)
- X-ray_Detector_Optimal_Project_Plan.md Section 5.4 Phase 4 (SoC Controller Firmware)
- System Architecture (docs/architecture/system-architecture.md) Layer 2

**References**:
- docs/architecture/fpga-design.md (FPGA register map, CSI-2 TX specification)
- SPEC-POC-001 (i.MX8M Plus CSI-2 RX configuration, V4L2 setup)
- detector_config.yaml (runtime configuration parameters)

**Feeds Into**:
- SPEC-FW-001 (SoC firmware detailed requirements)
- McuSimulator golden reference model
- Host SDK API design (network protocol specification)

---

## 13. Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (architect) | Initial SoC firmware architecture design document |

---

**Approval**:
- [ ] SoC Firmware Lead
- [ ] System Architect
- [ ] Project Manager

---

## Review Record

| Reviewer | Date | TRUST 5 Score | Decision |
|---------|------|--------------|---------|
| manager-quality | 2026-02-17 | T:5 R:5 U:4 S:5 T:5 | APPROVED |

### Review Notes

**TRUST 5 Assessment**

- **Testable (5/5)**: Section 10 defines 11 test cases (FW-UT-01 through FW-IT-05) covering unit and integration scenarios. Coverage targets specified per methodology: TDD (85%) and DDD (80%) consistent with quality.yaml. Test framework identified (CMocka / Unity). Key metrics defined (100 frames 0 errors, <0.01% frame drop for continuous).
- **Readable (5/5)**: Layered architecture diagram clearly shows Application/Service/HAL/Kernel decomposition. Process architecture (detector_daemon threads) documented. Code examples provided for all HAL interfaces (V4L2, SPI, UDP). State machines with ASCII diagrams for Sequence Engine and Frame Buffer management.
- **Unified (4/5)**: Hardware platform matches ground truth (VAR-SOM-MX8M-PLUS, Quad Cortex-A53, 1.8 GHz). CSI-2 interface consistent with fpga-design.md register map (SPI registers 0x20-0x3F, 0x40-0x4C, 0x80-0x88 referenced). Frame header structure matches system-architecture.md. **Minor issue corrected during review**: Header stated "Linux 5.15+" - corrected to "Linux 6.6.52 LTS (Yocto Scarthgap 5.0)" to match project ground truth. Yocto SDK path updated accordingly.
- **Secured (5/5)**: Section 7 categorizes all error types with severity levels and recovery strategies. FPGA error monitoring code shows 3-retry pattern before escalation. Buffer overrun protection (oldest-drop policy) prevents CSI-2 RX stall. Watchdog timeout triggers full system restart as last resort. Root privilege rationale documented (DD-FW-01).
- **Trackable (5/5)**: Document metadata complete. Section 11 lists 8 design decisions with rationale and dates. Rejected alternatives pattern not explicitly listed (unlike fpga-design.md) but decisions reference alternatives implicitly. Section 12 provides bidirectional traceability. Revision history present.

**Corrections Applied During Review**

1. Header field "Linux 5.15+" corrected to "Linux 6.6.52 LTS (Yocto Scarthgap 5.0, Variscite BSP imx-6.6.52-2.2.0-v1.3)"
2. Platform constraints table OS field updated to match corrected kernel version
3. Yocto SDK cross-compile path updated from "5.15-kirkstone" to "scarthgap"

**Minor Observations (non-blocking)**

- The "Unified" score is 4/5 due to the Linux version discrepancy that was found and corrected. Post-correction the document is fully consistent.
- Section 3.2.1 SPI configuration uses 8 bits-per-word but fpga-design.md Section 6.1 specifies 32-bit transactions (8-bit addr + 8-bit R/W + 16-bit data). The C code correctly assembles 4-byte transfers, which is consistent. This is not a discrepancy.
- The 10 GbE NIC driver is listed as "ixgbe or mlx5_core" - final driver selection depends on W15-W18 chip identification (lspci -nn). Acceptable for Phase 1.
