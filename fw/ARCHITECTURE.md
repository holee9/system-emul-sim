# SoC Firmware Architecture

## System Architecture

The SoC firmware implements a real-time data acquisition and streaming system running on NXP i.MX8M Plus.

### Hardware Context

```
┌─────────────────────────────────────────────────────────────┐
│                     Host PC (Frame Processing)               │
└───────────────────────────────┬─────────────────────────────┘
                                │ 10 GbE UDP (port 8000/8001)
┌───────────────────────────────▼─────────────────────────────┐
│              NXP i.MX8M Plus SoC (Variscite DART)            │
│  ┌───────────────┐  ┌──────────────┐  ┌─────────────────┐   │
│  │ CSI-2 D-PHY   │  │ SPI Master   │  │ 10 GbE MAC     │   │
│  │ 4-lane RX     │  │ 50 MHz       │  │ ixgbe/mlx5     │   │
│  └───────┬───────┘  └──────┬───────┘  └────────┬────────┘   │
│          │                  │                   │            │
│  ┌───────▼──────────────────▼───────────────────▼────┐      │
│  │         detector_daemon (User Space)              │      │
│  │  ┌───────────┐ ┌─────────┐ ┌────────────────┐     │      │
│  │  │CSI-2 RX   │ │SPI Ctrl │ │Ethernet TX     │     │      │
│  │  │(V4L2)     │ │Thread   │ │Frame Fragment │     │      │
│  │  └─────┬─────┘ └────┬────┘ └───────┬────────┘     │      │
│  │        │            │              │              │      │
│  │  ┌─────▼────────────▼──────────────▼────┐        │      │
│  │  │   Sequence Engine (State Machine)     │        │      │
│  │  │   Frame Manager (4-Buffer Ring)      │        │      │
│  │  │   Command Protocol (HMAC-SHA256)     │        │      │
│  │  │   Health Monitor (Watchdog)          │        │      │
│  │  └──────────────────────────────────────┘        │      │
│  └──────────────────────────────────────────────────┘      │
│  ┌──────────────────────────────────────────────────┐      │
│  │ Linux 6.6.52 Kernel + BSP Drivers               │      │
│  │ - V4L2 (CSI-2 RX)                               │      │
│  │ - spidev (SPI Master)                           │      │
│  │ - bmi160_i2c (IMU IIO)                          │      │
│  │ - gpio-pca953x (GPIO Expander)                  │      │
│  │ - ixgbe/mlx5 (10 GbE)                           │      │
│  └──────────────────────────────────────────────────┘      │
└───────────────────────────────┬─────────────────────────────┘
                                │ SPI Control
┌───────────────────────────────▼─────────────────────────────┐
│                 FPGA (Artix-7 XC7A35T)                       │
│  ┌───────────────┐  ┌──────────────┐  ┌─────────────────┐   │
│  │ CSI-2 TX      │  │ SPI Slave    │  │ Panel Scan     │   │
│  │ 4-lane D-PHY  │  │ 32-bit Regs  │  │ Logic          │   │
│  └───────────────┘  └──────────────┘  └─────────────────┘   │
└───────────────────────────────┬─────────────────────────────┘
                                │
┌───────────────────────────────▼─────────────────────────────┐
│              X-ray Detector Panel (3072×3072)                │
└─────────────────────────────────────────────────────────────┘
```

## Module Architecture

### 1. HAL Layer (Hardware Abstraction)

#### CSI-2 RX Driver (`hal/csi2_rx.c`)

**Purpose**: Receive RAW16 pixel frames from FPGA via MIPI CSI-2 D-PHY.

**Interface**: V4L2 (`/dev/video0`)

**Key Operations**:
- Configure V4L2 device for `V4L2_PIX_FMT_Y16` (16-bit grayscale)
- Request 4 MMAP DMA buffers (zero-copy)
- Queue buffers and start streaming with `VIDIOC_STREAMON`
- Dequeue frames via `VIDIOC_DQBUF` (delivers to Frame Manager within 1 ms per REQ-FW-012)
- Bypass ISP pipeline (raw pixel pass-through)

**Data Structure**:
```c
typedef struct {
    int fd;                     // V4L2 device file descriptor
    struct v4l2_buffer *buffers; // DMA buffer pointers
    uint32_t num_buffers;       // Buffer count (4)
    uint32_t width, height;     // Frame resolution
} csi2_rx_t;
```

**Error Handling**: V4L2 pipeline restart on EAGAIN/EIO (REQ-FW-061)

#### SPI Master (`hal/spi_master.c`)

**Purpose**: Control FPGA registers via 32-bit SPI transaction format.

**Interface**: Linux spidev (`/dev/spidev0.0`)

**Transaction Format** (matches FPGA SPI Slave):
```
[8-bit addr] [8-bit R/W] [16-bit data] = 32 bits total
```

**Key Operations**:
- Register write: Write 32-bit transaction, read back to verify (REQ-FW-021)
- Register read: Send addr + read command, receive 16-bit data
- Poll FPGA STATUS register at 100 us intervals (REQ-FW-022)
- Round-trip latency < 10 ms (REQ-FW-023)

**Data Structure**:
```c
typedef struct {
    int fd;                     // spidev file descriptor
    uint32_t speed_hz;          // SPI speed (50 MHz max)
    uint8_t mode;               // SPI Mode 0
} spi_master_t;

typedef struct {
    uint8_t addr;               // Register address
    uint8_t rw;                 // 0x01 = write, 0x00 = read
    uint16_t data;              // Register value
} spi_transaction_t;
```

**Scheduling**: `spi_control` thread uses `SCHED_FIFO` for real-time polling (REQ-FW-001a)

#### Battery Driver (`hal/bq40z50_driver.c`)

**Purpose**: Port TI BQ40z50 SMBus driver from Linux 4.4 to 6.6.

**Interface**: SMBus via i2c-dev (I2C addr 0x0b)

**Port Strategy** (DDD per REQ-FW-090):
- **ANALYZE**: Existing kernel 4.4 driver SMBus API calls
- **PRESERVE**: BQ40z50 register-level behavior and SBS compliance
- **IMPROVE**: Update to Linux 6.6 SMBus API (`i2c_smbus_read_word_data`, Power Supply class)

**Metrics Reported** (REQ-FW-091):
- State of Charge (%)
- Voltage (mV)
- Current (mA)
- Temperature (0.1 K)
- Remaining Capacity (mAh)
- Full Charge Capacity (mAh)

**Thresholds**:
- 15% SOC: Low battery warning
- 10% SOC: Graceful scan termination (REQ-FW-092)
- 5% SOC: Emergency shutdown

#### IMU HAL (`hal/imu_hal.c`) - Phase 3 (W23-W28)

**Purpose**: Read Bosch BMI160 accelerometer and gyroscope via IIO subsystem.

**Interface**: IIO sysfs (`/sys/bus/iio/devices/iio:device0`)

**Kernel Driver**: `bmi160_i2c` (included in Linux 6.6, no porting required)

**API** (REQ-FW-141):
```c
typedef struct {
    int16_t x, y, z;            // Raw IIO values
} imu_3axis_t;

int imu_get_accel(imu_3axis_t *accel_mg);      // Accelerometer in milli-g
int imu_get_gyro(imu_3axis_t *gyro_mdps);      // Gyroscope in milli-dps
bool imu_is_stationary(uint32_t threshold_mg); // Motion detection
```

**Use Case**: Motion blur prevention - Sequence Engine defers ARM if device is moving (REQ-FW-142)

#### GPIO HAL (`hal/gpio_hal.c`) - Phase 2 (W9-W22)

**Purpose**: Control NXP PCA9534 GPIO expander via sysfs GPIO interface.

**Interface**: `/sys/class/gpio` (kernel driver: `gpio-pca953x`)

**API** (REQ-FW-151):
```c
int gpio_set_panel_power(bool enable);          // Panel power control
int gpio_set_led(uint8_t id, bool state);       // LED0-3 control
int gpio_get_status_input(uint8_t pin, bool *state); // Read inputs
```

**Safe Initialization** (REQ-FW-152): All outputs set to safe defaults at startup (panel power OFF, LEDs OFF)

### 2. Core Logic Layer

#### Sequence Engine (`sequence_engine.c`)

**Purpose**: Coordinate FPGA control, CSI-2 reception, and network streaming via state machine.

**State Machine** (REQ-FW-030):
```
IDLE -> CONFIGURE -> ARM -> SCANNING -> STREAMING -> COMPLETE
  ^                                      |
  |                                      v
  +------------------------- ERROR <------+
```

**State Transitions**:
- **IDLE**: Daemon startup, waiting for commands
- **CONFIGURE**: Write FPGA timing/panel/CSI-2 registers via SPI
- **ARM**: Check IMU for motion (REQ-FW-142), write FPGA start_scan bit
- **SCANNING**: Poll FPGA STATUS, receive CSI-2 frames
- **STREAMING**: Transmit frames to Host via UDP
- **COMPLETE**: Scan finished, return to IDLE
- **ERROR**: FPGA error detected, attempt recovery (max 3 retries per REQ-FW-032)

**Scan Modes** (REQ-FW-033):
- Single Scan: Acquire one frame, stop
- Continuous Scan: Acquire frames until STOP command
- Calibration: Factory calibration mode

**Inputs**:
- Host commands (via Command Protocol)
- FPGA STATUS (via SPI polling)
- IMU motion status (via IMU HAL)
- CSI-2 frame events (via CSI-2 RX)

**Outputs**:
- SPI register writes to FPGA
- CSI-2 stream start/stop
- Frame transmission enable/disable
- Status reports to Host

#### Frame Manager (`frame_manager.c`)

**Purpose**: Manage 4-frame DDR4 buffer ring with zero-copy handoff.

**Buffer States**:
```
FREE -> DMA -> FILLED -> SENDING -> FREE
```

**Buffer Allocation** (REQ-FW-050):
- 4 buffers in DDR4
- Buffer size = `rows × cols × 2 bytes` (RAW16)
- Example: 3072×3072×2 = 18 MB per buffer, 72 MB total

**Zero-Copy Flow**:
1. CSI-2 RX acquires buffer via MMAP (FREE -> DMA)
2. V4L2 DQBUF delivers filled buffer (DMA -> FILLED)
3. Frame Manager hands off to Ethernet TX (FILLED -> SENDING)
4. Ethernet TX releases after transmission (SENDING -> FREE)

**Drop Policy** (REQ-FW-051): Oldest-drop if all buffers in SENDING state (prevents CSI-2 RX stall)

**Performance Target**: < 0.01% frame drop rate (REQ-FW-052)

### 3. Protocol Layer

#### Command Protocol (`protocol/command_protocol.c`)

**Purpose**: Handle Host commands with authentication and replay protection.

**Transport**: UDP port 8001 (separate from data port 8000 per REQ-FW-043)

**Frame Format** (REQ-FW-027):
```
Offset | Size | Field | Description
-------|------|-------|-------------
0      | 4    | magic | 0xBEEFCAFE (cmd) or 0xCAFEBEEF (resp)
4      | 4    | sequence | Monotonic seq number (replay protection)
8      | 2    | command_id | Opcode (START_SCAN=0x01, STOP_SCAN=0x02, etc.)
10     | 2    | payload_len | Payload length in bytes
12     | 32   | hmac | HMAC-SHA256 over bytes 0-11 + payload
44     | var  | payload | Command-specific data
```

**Authentication** (REQ-FW-100):
- Pre-shared HMAC key in Phase 1 (TLS mutual auth deferred to production)
- HMAC-SHA256 over header + payload
- Silent discard on auth failure, increment counter (REQ-FW-101)

**Replay Protection** (REQ-FW-028):
- Monotonic sequence numbers
- Discard packets with seq <= last accepted seq

**Command IDs**:
- `0x01`: START_SCAN (payload: mode, config_id)
- `0x02`: STOP_SCAN
- `0x10`: GET_STATUS (response: state, counters, battery, FPGA)
- `0x20`: SET_CONFIG (payload: param_id, value)

### 4. System Layer

#### Health Monitor (`health_monitor.c`)

**Purpose**: Detect firmware hangs and unresponsive states.

**Watchdog Timer** (REQ-FW-060):
- Pet interval: 1 second
- Timeout: 5 seconds
- Action: Systemd restart via `WatchdogSec=`

**Counters Tracked** (REQ-FW-111):
```c
typedef struct {
    uint64_t frames_received;
    uint64_t frames_sent;
    uint64_t frames_dropped;
    uint64_t spi_errors;
    uint64_t csi2_errors;
    uint64_t packets_sent;
    uint64_t bytes_sent;
    uint64_t auth_failures;
} runtime_stats_t;
```

**Status Response** (REQ-FW-112): Assemble and send within 50 ms

#### Main Daemon (`main.c`)

**Purpose**: Daemon lifecycle management and initialization.

**Startup Sequence**:
1. Load `detector_config.yaml` (validate per REQ-FW-130)
2. Initialize GPIO HAL (safe defaults per REQ-FW-152)
3. Initialize IMU HAL
4. Initialize Battery Driver
5. Initialize CSI-2 RX (V4L2 setup)
6. Initialize SPI Master (open spidev)
7. Initialize Ethernet TX (bind UDP socket)
8. Spawn threads: spi_control, csi2_rx, eth_tx, health_monitor
9. Start Sequence Engine in IDLE state

**Shutdown Sequence** (REQ-FW-121):
1. Set shutdown flag
2. Stop CSI-2 streaming
3. Complete in-progress frame transmission
4. Turn off panel power (GPIO HAL)
5. Close SPI and network sockets
6. Exit within 5 seconds

**Systemd Integration**: `Restart=on-failure`, `RestartSec=5` (REQ-FW-120)

### 5. Network Layer

#### Ethernet TX (`hal/eth_tx.c`)

**Purpose**: Fragment frames into UDP packets and transmit on port 8000.

**Frame Header Format** (per `soc-firmware-design.md` Section 6.1):
```
Offset | Size | Field | Description
-------|------|-------|-------------
0      | 4    | magic | 0xD7E01234 (frame identifier)
4      | 4    | frame_id | Monotonic frame number
8      | 4    | packet_index | Packet index in frame (0-based)
12     | 4    | total_packets | Total packets in frame
16     | 8    | timestamp_ns | Frame acquisition timestamp (ns)
24     | 4    | rows | Frame height
28     | 4    | cols | Frame width
32     | 2    | crc16 | CRC-16/CCITT over bytes 0-31
```

**Fragmentation** (REQ-FW-040):
- Max payload per packet: 8192 bytes
- 32-byte header + up to 8160 bytes pixel data
- Target tier example: 18 MB frame / 8160 = 1024 packets

**Performance** (REQ-FW-041):
- All packets sent within 1 frame period (66.7 ms at 15 fps)
- At 10 Gbps: ~7 ms per frame transmission time

**CRC-16** (REQ-FW-042): Computed over header bytes 0-31 (excluding CRC field itself)

## Data Flow

### Frame Acquisition Flow

```
1. FPGA completes frame acquisition -> CSI-2 TX sends MIPI packets
2. CSI-2 D-PHY receives -> DMA writes to V4L2 MMAP buffer
3. CSI-2 RX thread calls DQBUF -> receives filled buffer pointer
4. Frame Manager transitions buffer: DMA -> FILLED
5. Frame Manager hands off to Ethernet TX -> FILLED -> SENDING
6. Ethernet TX fragments frame -> sends UDP packets to Host
7. Ethernet TX releases buffer -> SENDING -> FREE
8. CSI-2 RX re-queues buffer for next frame -> FREE -> DMA
```

### Command Processing Flow

```
1. Host SDK sends UDP packet to port 8001
2. Command Protocol receives packet
3. Validate magic (0xBEEFCAFE)
4. Verify HMAC-SHA256
5. Check sequence number > last_seq (replay protection)
6. Parse command_id and payload
7. Route to Sequence Engine
8. Sequence Engine executes command (e.g., START_SCAN)
9. Sequence Engine sends response (magic=0xCAFEBEEF)
10. Command Protocol transmits response to Host
```

### Error Recovery Flow

```
1. CSI-2 RX returns EAGAIN/EIO error
2. CSI-2 RX thread detects error
3. Close V4L2 device, stop streaming
4. Re-initialize V4L2 (VIDIOC_REQBUFS, VIDIOC_STREAMON)
5. Resume frame capture
6. Log error at WARNING level
7. Report error to Host via GET_STATUS
8. If continuous mode, auto-resume scanning
```

## Thread Architecture

| Thread | Priority | Function | Scheduling |
|--------|----------|----------|------------|
| spi_control | High | Poll FPGA STATUS @ 100 us | SCHED_FIFO |
| csi2_rx | High | Dequeue V4L2 frames | SCHED_FIFO |
| eth_tx | Medium | Fragment and send UDP | SCHED_OTHER |
| command_handler | Medium | Process Host commands | SCHED_OTHER |
| health_monitor | Low | Watchdog pet, stats aggregation | SCHED_OTHER |
| battery_monitor | Low | Poll BQ40z50 @ 1 Hz | SCHED_OTHER |
| main | Medium | Initialization and coordination | SCHED_OTHER |

## Security Architecture

### Authentication (REQ-FW-100)

**Phase 1**: Pre-shared HMAC-SHA256 key
- Key stored in `/etc/detector/hmac_key` (root:detector 0400)
- Commands without valid HMAC silently discarded
- Auth failure counter exposed via GET_STATUS

**Phase 2 (Production)**: TLS mutual authentication
- X.509 certificates for Host and SoC
- Encrypted command channel

### Least Privilege (REQ-FW-102)

**Service Account**: `detector`
- Non-root user
- Minimal Linux capabilities:
  - `CAP_NET_BIND_SERVICE`: Bind to ports 8000/8001
  - `CAP_SYS_NICE`: Set SCHED_FIFO for real-time threads

**Systemd Hardening**:
```
User=detector
NoNewPrivileges=true
ProtectSystem=strict
ProtectHome=yes
ReadWritePaths=/var/log/detector
AmbientCapabilities=CAP_NET_BIND_SERVICE CAP_SYS_NICE
```

**Device Permissions**: udev rules configure `/dev/video0`, `/dev/spidev0.0` ownership

## Performance Characteristics

### Latency Budget

| Stage | Target | Notes |
|-------|--------|-------|
| CSI-2 RX → Frame Manager | < 1 ms | Zero-copy pointer handoff (REQ-FW-012) |
| SPI round-trip | < 10 ms | Command to response (REQ-FW-023) |
| GET_STATUS response | < 50 ms | Cached status assembly (REQ-FW-112) |
| Frame TX time | < 66.7 ms | 1 frame period at 15 fps (REQ-FW-041) |

### Throughput Requirements

| Tier | Resolution | Bit Depth | FPS | Data Rate | CSI-2 Speed |
|------|------------|-----------|-----|-----------|-------------|
| Minimum | 1024×1024 | 14-bit | 15 | 0.21 Gbps | 400 Mbps/lane |
| Intermediate-A | 2048×2048 | 16-bit | 15 | 1.01 Gbps | 400 Mbps/lane |
| Target | 3072×3072 | 16-bit | 15 | 2.26 Gbps | 800 Mbps/lane (debugging) |

### Memory Requirements

**DDR4 Buffer Ring**:
- 4 buffers × rows × cols × 2 bytes
- Target tier: 4 × 3072 × 3072 × 2 = 72 MB
- Intermediate-A: 4 × 2048 × 2048 × 2 = 32 MB

**Additional Memory**:
- Network buffers: 16 MB (kernel socket buffers)
- Configuration: 1 MB
- Runtime counters: < 1 MB
- **Total**: ~90 MB at target tier

## Configuration Schema

The firmware loads configuration from `detector_config.yaml` (single source of truth).

### Hot-Swappable Parameters

- `frame_rate`: 1-60 fps
- `network.destination_ip`: Host PC IP address
- `network.port_udp_data`: 8000 (default)
- `network.port_udp_cmd`: 8001 (default)
- `log.level`: DEBUG, INFO, WARNING, ERROR, CRITICAL

### Cold Parameters

- `panel.resolution`: rows × cols (128-4096)
- `panel.bit_depth`: 14 or 16
- `csi2.lane_speed`: 400 or 800 Mbps/lane
- `spi.speed_hz`: 1-50 MHz

## Dependencies

### Kernel Drivers (Linux 6.6.52)

| Driver | Module | Device | Status |
|--------|--------|--------|--------|
| V4L2 | `viv-imx8mp` | `/dev/video0` | Included in BSP |
| spidev | `spidev` | `/dev/spidev0.0` | Included in BSP |
| 10 GbE | `ixgbe` or `mlx5` | `eth0` | To be verified |
| IIO BMI160 | `bmi160_i2c` | `iio:device0` | Included in kernel |
| GPIO PCA9534 | `gpio-pca953x` | `gpiochipN` | Included in kernel |

### Userspace Libraries

- `libyaml`: YAML configuration parsing
- `openssl`: HMAC-SHA256 computation
- `systemd`: daemon notification and watchdog

## Risks and Mitigations

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| V4L2 driver instability | Medium | High | Pipeline restart (REQ-FW-061) |
| SPI polling jitter | Low | Medium | SCHED_FIFO scheduling |
| BQ40z50 port breakage | Medium | Low | DDD approach, fallback to userspace SMBus |
| 10 GbE driver missing | Low | High | Verify with `lspci -nn` early |
| IMU IIO path instability | Low | Low | Probe by device name, not fixed path |

## References

- [SPEC-FW-001](../.moai/specs/SPEC-FW-001/spec.md): Requirements Specification
- [../docs/architecture/soc-firmware-design.md](../docs/architecture/soc-firmware-design.md): Detailed Design Document
- [NXP i.MX8M Plus Reference Manual](https://www.nxp.com/docs/en/reference-manual/IMX8MPRM.pdf)
- [Variscite BSP Documentation](https://variwiki.com/index.php?title=VAR-SOM-MX8M-PLUS)
- [V4L2 API Specification](https://www.kernel.org/doc/html/latest/userspace-api/media/v4l/v4l2.html)
- [Linux IIO Subsystem](https://www.kernel.org/doc/html/latest/driver-api/iio/index.html)

---

**Version**: 1.0.0
**Last Updated**: 2026-02-18
**Author**: ABYZ-Lab Agent (architect)
**Status**: SPEC-FW-001 approved, architecture frozen for implementation
