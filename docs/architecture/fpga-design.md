# FPGA Architecture Design

**Project**: X-ray Detector Panel System
**Target Device**: Xilinx Artix-7 XC7A35T-FGG484
**Document Version**: 1.0.0
**Status**: Draft
**Last Updated**: 2026-02-17

---

## 1. Overview

### 1.1 Purpose

This document describes the FPGA architecture for the X-ray Detector Panel System. The FPGA serves as the real-time data acquisition engine, responsible for panel scan timing, line-level pixel buffering, and high-speed CSI-2 MIPI transmission to the SoC controller.

### 1.2 Design Philosophy

The FPGA performs **hard real-time functions only**:
- Nanosecond-precision panel scan timing via FSM
- Deterministic line buffer management via Ping-Pong BRAM
- Continuous CSI-2 pixel streaming with zero frame drops
- Fault detection and safe shutdown via protection logic

All higher-level functions (frame sequencing, network streaming, storage) are delegated to the SoC Controller and Host PC.

### 1.3 Device Constraints

| Resource | Available | Budget (60%) | Estimated Usage | Margin |
|----------|-----------|-------------|-----------------|--------|
| LUTs | 20,800 | 12,480 | 7,000-12,000 | 34-58% |
| Flip-Flops | 41,600 | 24,960 | 5,000-8,000 | 12-19% |
| Block RAM (36Kb) | 50 | 30 | 3-6 | 6-12% |
| DSP48E1 | 90 | 54 | 0-2 | 0-2% |
| GTP Transceivers | 4 | - | 0 (reserved) | 0% |
| I/O Pins | ~250 | - | ~60 | ~24% |

**Critical Constraint**: USB 3.x is permanently excluded (LUT cost 72-120% of device).

---

## 2. Block Diagram

### 2.1 Top-Level Architecture

```
                          +--------------------------------------------------+
                          |           FPGA: Artix-7 XC7A35T                  |
                          |                                                   |
   ROIC LVDS Data  ------>|  +----------------+    +------------------+       |
   (Multi-channel)        |  | ROIC Interface |    | Panel Scan       |       |
                          |  | + Deserializer |--->| Timing FSM       |       |
                          |  +-------+--------+    +--------+---------+       |
                          |          |                      |                 |
                          |          | pixel_data            | timing_ctrl    |
                          |          v                      |                 |
                          |  +-------+--------+             |                 |
                          |  | Line Buffer    |<------------+                 |
                          |  | (Ping-Pong     |                               |
                          |  |  Dual BRAM)    |                               |
                          |  +-------+--------+                               |
                          |          |                                        |
                          |          | line_data                              |
                          |          v                                        |
   CSI-2 D-PHY     <-----|  +-------+--------+    +------------------+       |
   4-lane + Clock         |  | CSI-2 MIPI TX  |    | Protection       |       |
   (to SoC)               |  | Subsystem      |    | Logic            |       |
                          |  | (AMD IP v3.1)  |    | (Timeout/OVF/ERR)|       |
                          |  +----------------+    +--------+---------+       |
                          |                                 |                 |
   SPI (from SoC)  <---->|  +----------------+             |                 |
   SCLK/MOSI/MISO/CS     |  | SPI Slave      |<------------+                 |
                          |  | + Register Map |                               |
                          |  +----------------+                               |
                          |                                                   |
   Debug (JTAG/ILA) <--->|  +----------------+                               |
                          |  | Debug / ILA    |                               |
                          |  +----------------+                               |
                          +--------------------------------------------------+
```

### 2.2 Module Hierarchy

```
csi2_detector_top
  |-- clk_gen (MMCM/PLL)
  |     |-- clk_sys        (100 MHz, system clock)
  |     |-- clk_pixel      (125.83 MHz, pixel processing)
  |     |-- clk_csi2_byte  (125 MHz, CSI-2 byte clock)
  |     `-- clk_dphy_hs    (500 MHz, D-PHY high-speed DDR)
  |
  |-- spi_slave
  |     |-- spi_receiver    (SPI protocol engine)
  |     `-- register_file   (R/W registers, status, error flags)
  |
  |-- panel_scan_fsm
  |     |-- timing_gen      (gate/ROIC timing pulse generation)
  |     |-- mode_ctrl       (single/continuous/calibration mode)
  |     `-- frame_counter   (32-bit frame sequence counter)
  |
  |-- roic_interface
  |     |-- lvds_deserializer (ISERDES-based LVDS receiver)
  |     `-- pixel_formatter   (raw ADC data -> 16-bit pixel alignment)
  |
  |-- line_buffer
  |     |-- bram_bank_a     (True Dual-Port BRAM, write side)
  |     |-- bram_bank_b     (True Dual-Port BRAM, read side)
  |     `-- bank_ctrl       (ping-pong arbiter, address gen)
  |
  |-- csi2_tx_subsystem     (AMD MIPI CSI-2 TX Subsystem IP v3.1)
  |     |-- packet_builder  (FS/LS/pixel data/LE/FE framing)
  |     |-- crc_engine      (CRC-16 per line packet)
  |     `-- dphy_tx         (OSERDES + LVDS output, 4-lane)
  |
  |-- protection_logic
  |     |-- timeout_watchdog (readout timeout detection)
  |     |-- overflow_detect  (buffer overflow monitoring)
  |     `-- error_handler    (error code generation, safe shutdown)
  |
  `-- debug_infrastructure
        |-- ila_core         (Integrated Logic Analyzer probes)
        `-- vio_core         (Virtual I/O for runtime debug)
```

---

## 3. Panel Scan Timing FSM

### 3.1 State Machine Diagram

```
                    +-------+
           reset -->| IDLE  |<------ stop_scan OR error
                    +---+---+
                        |
                        | start_scan (via SPI CONTROL register)
                        v
                  +-----------+
                  | INTEGRATE |  Gate ON, exposure timer running
                  +-----+-----+
                        |
                        | exposure_done (gate_on_us elapsed)
                        v
                  +-----------+
                  | READOUT   |  Gate OFF, ROIC ADC conversion + readout
                  +-----+-----+
                        |
                        | line_data_valid (one line complete)
                        v
                  +-----------+
                  | LINE_DONE |  Write line to buffer, increment line counter
                  +-----+-----+
                        |
                 +------+-------+
                 |              |
                 | line < rows  | line == rows
                 v              v
            (READOUT)    +-----------+
                         | FRAME_DONE|  Increment frame counter
                         +-----+-----+
                               |
                        +------+-------+
                        |              |
                        | continuous   | single mode
                        v              v
                   (INTEGRATE)      (IDLE)

        [Any state] -- timeout/overflow --> +-------+
                                            | ERROR |
                                            +---+---+
                                                |
                                                | error_clear (via SPI)
                                                v
                                            (IDLE)
```

### 3.2 State Encoding

| State | Encoding (3-bit) | STATUS Register Bit | Description |
|-------|-----------------|---------------------|-------------|
| IDLE | 3'b000 | bit[0] = 1 | Waiting for start command |
| INTEGRATE | 3'b001 | bit[1] = 1 | Exposure in progress |
| READOUT | 3'b010 | bit[1] = 1 | Line readout active |
| LINE_DONE | 3'b011 | bit[1] = 1 | Line buffered, preparing next |
| FRAME_DONE | 3'b100 | bit[1] = 1 | Frame complete, updating counters |
| ERROR | 3'b101 | bit[2] = 1 | Error detected, safe state |

### 3.3 Timing Parameters (Configurable via SPI)

| Parameter | Register | Range | Default | Unit |
|-----------|----------|-------|---------|------|
| gate_on_us | 0x20 | 1-65535 | 1000 | microseconds |
| gate_off_us | 0x24 | 1-65535 | 100 | microseconds |
| roic_settle_us | 0x28 | 1-255 | 10 | microseconds |
| adc_conv_us | 0x2C | 1-255 | 5 | microseconds |
| line_time_us | 0x30 | 1-65535 | 16 | microseconds |
| frame_blanking_us | 0x34 | 1-65535 | 500 | microseconds |

### 3.4 Operating Modes

| Mode | CONTROL Bit | Behavior |
|------|------------|----------|
| Single Scan | bit[3:2] = 2'b00 | One frame capture, return to IDLE |
| Continuous Scan | bit[3:2] = 2'b01 | Repeat frames until stop_scan |
| Calibration | bit[3:2] = 2'b10 | Dark frame (gate OFF during INTEGRATE) |

### 3.5 Estimated Resources

| Component | LUTs | FFs | BRAMs | Notes |
|-----------|------|-----|-------|-------|
| FSM core | 200 | 150 | 0 | 6-state FSM with mode control |
| Timing counters | 200 | 128 | 0 | gate/line/frame timers |
| Frame counter | 50 | 32 | 0 | 32-bit counter |
| Mode control | 50 | 20 | 0 | Mode decode logic |
| **Subtotal** | **~500** | **~330** | **0** | |

---

## 4. Line Buffer Architecture

### 4.1 Ping-Pong BRAM Structure

```
                WRITE SIDE                        READ SIDE
              (clk_roic domain)               (clk_csi2_byte domain)

  pixel_data  +--+-----+-----+--+  line_data  +--+-----+-----+--+
  ----------->|WE| BRAM Bank A  |  ---------->|RD| BRAM Bank A  |--------->
              +--+-----+-----+--+             +--+-----+-----+--+
                       |                               |
  bank_sel    +--------+--------+   bank_sel  +--------+--------+
  (toggle)    |  Bank Controller |  (toggle)  |                 |
              +--------+--------+             +-----------------+
                       |                               |
  pixel_data  +--+-----+-----+--+  line_data  +--+-----+-----+--+
  ----------->|WE| BRAM Bank B  |  ---------->|RD| BRAM Bank B  |--------->
              +--+-----+-----+--+             +--+-----+-----+--+
                                                       |
                                                       v
                                              To CSI-2 TX Subsystem
```

### 4.2 BRAM Configuration

| Parameter | Value | Rationale |
|-----------|-------|-----------|
| BRAM Primitive | BRAM_TDP_36K | 36Kb True Dual-Port |
| Data Width | 16 bits | RAW16 pixel format |
| Depth per Bank | 3072 words | Maximum line width |
| Banks | 2 (A + B) | Ping-Pong isolation |
| BRAMs per Bank | 2 (cascaded) | 3072 x 16 = 49,152 bits > 36,864 bits |
| Total BRAMs | 4 | 2 banks x 2 BRAMs |
| Write Clock | clk_roic | ROIC readout domain |
| Read Clock | clk_csi2_byte | CSI-2 TX domain |

### 4.3 Ping-Pong Protocol

```
Time -->

Frame N:
  Line 0:  Write Bank A  |  Read Bank B (previous frame last line)
  Line 1:  Write Bank B  |  Read Bank A
  Line 2:  Write Bank A  |  Read Bank B
  ...
  Line N:  Write Bank X  |  Read Bank Y

Bank Toggle: Occurs on line_done signal
Write Side: Writes next line pixels sequentially (addr 0 to cols-1)
Read Side:  Reads previous line pixels sequentially for CSI-2 TX
```

### 4.4 Clock Domain Crossing

The line buffer operates across two clock domains:
- **Write side**: clk_roic (variable, depends on ROIC ADC rate)
- **Read side**: clk_csi2_byte (125 MHz for 1.0 Gbps lane speed)

**CDC Strategy**: Dual-port BRAM provides inherent clock domain isolation. Bank select toggle signal uses a 2-stage synchronizer.

```
clk_roic domain:                 clk_csi2_byte domain:

  bank_sel_wr  ---> [sync_ff_1] ---> [sync_ff_2] ---> bank_sel_rd
                     (posedge)        (posedge)
```

### 4.5 Estimated Resources

| Component | LUTs | FFs | BRAMs | Notes |
|-----------|------|-----|-------|-------|
| Address generators | 200 | 64 | 0 | Write/read address counters |
| Bank controller | 100 | 16 | 0 | Ping-pong arbiter |
| CDC synchronizers | 50 | 8 | 0 | 2-stage sync for bank_sel |
| BRAM instances | 100 | 0 | 4 | 2 banks x 2 BRAMs (cascaded) |
| **Subtotal** | **~450** | **~88** | **4** | |

---

## 5. CSI-2 MIPI TX Subsystem

### 5.1 IP Integration

**IP**: AMD MIPI CSI-2 TX Subsystem v3.1 (or later)
**License**: Vivado HL Design Edition required

### 5.2 IP Configuration

| Parameter | Setting | Rationale |
|-----------|---------|-----------|
| Number of Lanes | 4 data + 1 clock | Maximum bandwidth configuration |
| Lane Speed | 1.0 Gbps/lane (configurable to 1.25) | Conservative initial, sweepable |
| Data Type | RAW16 (0x2C) | 16-bit pixel format |
| Virtual Channel | VC0 | Single sensor, no multiplexing |
| Input Interface | AXI4-Stream | Standard streaming protocol |
| Line Blanking | 100 pixel clocks | Inter-line gap |
| Frame Blanking | 10 line times | Inter-frame gap |

### 5.3 AXI4-Stream Interface

```
Line Buffer --> AXI4-Stream --> CSI-2 TX IP --> D-PHY Output

Signals:
  s_axis_tdata  [15:0]   Pixel data (RAW16)
  s_axis_tvalid           Pixel valid
  s_axis_tready           TX ready (backpressure)
  s_axis_tlast            End of line marker
  s_axis_tuser  [0]       Start of frame marker
```

### 5.4 CSI-2 Packet Structure

**Frame Transmission Sequence**:
```
[SoT] [FS Packet] [SoT] [LS + Pixel Data + CRC] [EoT] ... [SoT] [FE Packet] [EoT]
  |                  |                                               |
  |-- Frame Start    |-- Per-line data packet                        |-- Frame End
      (4 bytes)          (4 + 2*width + 2 bytes)                         (4 bytes)
```

**Packet Format Detail**:

| Field | Size | Content |
|-------|------|---------|
| **Frame Start (FS)** | 4 bytes | DataID=0x00, WC=0x0000, ECC |
| **Line Start (LS)** | 4 bytes | DataID=0x02, WC=0x0000, ECC |
| **Pixel Data** | 2 x width bytes | RAW16 pixels, MSB first |
| **CRC-16** | 2 bytes | CRC over pixel payload |
| **Line End (LE)** | 4 bytes | DataID=0x03, WC=0x0000, ECC |
| **Frame End (FE)** | 4 bytes | DataID=0x01, WC=0x0000, ECC |

### 5.5 D-PHY Physical Layer

**Implementation**: OSERDES2 + LVDS I/O buffers (native Artix-7, no external PHY)

| Parameter | Value | Specification |
|-----------|-------|---------------|
| Serialization Ratio | 10:1 DDR | OSERDES2 configuration |
| I/O Standard | LVDS_25 | 2.5V LVDS differential |
| Differential Swing | 200 mV typical | D-PHY v1.2 specification |
| Common Mode | 200 mV +/- 25 mV | D-PHY v1.2 specification |
| Rise/Fall Time | < 100 ps | 80-120 ps typical |
| Max Lane Speed | ~1.25 Gbps | Artix-7 OSERDES limit |

**D-PHY Modes**:

| Mode | Clock Lane | Data Lanes | When Used |
|------|-----------|------------|-----------|
| HS (High Speed) | Differential clock | Differential data | Pixel transmission |
| LP (Low Power) | Single-ended LP-00 | Single-ended LP-00 | Idle, init, escape |
| HS Entry | LP-00 -> LP-01 -> LP-00 -> HS-0 | LP-00 -> LP-01 -> LP-00 -> HS-Sync | Before pixel burst |
| HS Exit | HS-0 -> LP-11 | HS-Trail -> LP-11 | After pixel burst |

### 5.6 Estimated Resources

| Component | LUTs | FFs | BRAMs | Notes |
|-----------|------|-----|-------|-------|
| CSI-2 TX IP core | 3,000-5,000 | 2,000-3,000 | 0-1 | AMD IP estimate |
| Packet builder wrapper | 300 | 200 | 0 | Frame/line control logic |
| AXI4-Stream adapter | 200 | 100 | 0 | Line buffer to AXI-S |
| **Subtotal** | **~3,500-5,500** | **~2,300-3,300** | **0-1** | |

---

## 6. SPI Slave and Register Map

### 6.1 SPI Protocol

| Parameter | Value |
|-----------|-------|
| Mode | SPI Mode 0 (CPOL=0, CPHA=0) |
| Clock | Up to 50 MHz (from SoC master) |
| Data Width | 8-bit (byte-oriented) |
| Transaction | 32-bit: 8-bit address + 8-bit R/W + 16-bit data |
| Chip Select | Active low (directly from SoC GPIO) |

### 6.2 Transaction Protocol

```
     CS_N  ______|___________________________________|______
                  |                                   |
     SCLK  ______|/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\|______
                  |<-- 8-bit addr -->|<- R/W ->|<- 16-bit data ->|
     MOSI  ------|  ADDRESS[7:0]    |  0=R/1=W |  WDATA[15:0]   |------
     MISO  ------|  XXXXXXXX        | XXXXXXXX  |  RDATA[15:0]   |------
```

**Write Transaction**: SoC sends address + W flag + data. FPGA latches on final SCLK edge.
**Read Transaction**: SoC sends address + R flag. FPGA returns data on MISO during data phase.

### 6.3 Detailed Register Map

#### Control Registers (0x00 - 0x0F)

| Address | Name | Access | Bits | Description |
|---------|------|--------|------|-------------|
| 0x00 | CONTROL | W | [0] start_scan | Write 1 to begin scan sequence |
| | | | [1] stop_scan | Write 1 to abort scan |
| | | | [2] reset | Write 1 for soft reset |
| | | | [3:2] scan_mode | 00=single, 01=continuous, 10=calibration |
| | | | [4] error_clear | Write 1 to clear error flags |
| | | | [15:5] reserved | Must be 0 |
| 0x04 | STATUS | R | [0] idle | 1 = FSM in IDLE state |
| | | | [1] busy | 1 = Scan in progress |
| | | | [2] error | 1 = Error condition active |
| | | | [7:3] error_code | See error code table |
| | | | [10:8] fsm_state | Current FSM state encoding |
| | | | [11] buffer_bank | Current active write bank (0=A, 1=B) |
| | | | [15:12] reserved | Read as 0 |
| 0x08 | FRAME_COUNTER | R | [15:0] frame_count | Lower 16 bits of frame counter |
| 0x0A | FRAME_COUNTER_H | R | [15:0] frame_count_h | Upper 16 bits of frame counter |
| 0x0C | LINE_COUNTER | R | [11:0] line_count | Current line being processed (0-3071) |
| | | | [15:12] reserved | Read as 0 |

#### Timing Configuration Registers (0x20 - 0x3F)

| Address | Name | Access | Bits | Description |
|---------|------|--------|------|-------------|
| 0x20 | GATE_ON_US | R/W | [15:0] gate_on | Gate ON duration in microseconds |
| 0x24 | GATE_OFF_US | R/W | [15:0] gate_off | Gate OFF duration in microseconds |
| 0x28 | ROIC_SETTLE_US | R/W | [7:0] settle | ROIC settling time in microseconds |
| 0x2C | ADC_CONV_US | R/W | [7:0] conv | ADC conversion time in microseconds |
| 0x30 | LINE_TIME_US | R/W | [15:0] line_time | Total line period in microseconds |
| 0x34 | FRAME_BLANK_US | R/W | [15:0] blank | Inter-frame blanking in microseconds |

#### Panel Configuration Registers (0x40 - 0x5F)

| Address | Name | Access | Bits | Description |
|---------|------|--------|------|-------------|
| 0x40 | PANEL_ROWS | R/W | [11:0] rows | Number of panel rows (max 3072) |
| 0x44 | PANEL_COLS | R/W | [11:0] cols | Number of panel columns (max 3072) |
| 0x48 | BIT_DEPTH | R/W | [4:0] depth | Pixel bit depth (14 or 16) |
| 0x4C | PIXEL_FORMAT | R/W | [7:0] format | CSI-2 data type (0x2C = RAW16) |

#### CSI-2 Configuration Registers (0x80 - 0x8F)

| Address | Name | Access | Bits | Description |
|---------|------|--------|------|-------------|
| 0x80 | CSI2_CONTROL | R/W | [1:0] lane_count | 00=1-lane, 01=2-lane, 10=4-lane |
| | | | [2] tx_enable | 1 = Enable CSI-2 TX |
| | | | [3] continuous_clk | 1 = Continuous HS clock |
| | | | [7:4] reserved | Must be 0 |
| 0x84 | CSI2_STATUS | R | [0] phy_ready | D-PHY initialization complete |
| | | | [1] tx_active | Packet transmission in progress |
| | | | [2] fifo_overflow | TX FIFO overflow detected |
| | | | [15:3] reserved | Read as 0 |
| 0x88 | CSI2_LANE_SPEED | R/W | [7:0] speed_code | Lane speed: 0x64=1.0G, 0x6E=1.1G, 0x78=1.2G, 0x7D=1.25G |

#### Data Interface Status Registers (0x90 - 0x9F)

| Address | Name | Access | Bits | Description |
|---------|------|--------|------|-------------|
| 0x90 | DATA_IF_STATUS | R | [0] csi2_link_up | CSI-2 D-PHY link established |
| | | | [1] csi2_tx_ok | Last CSI-2 TX successful |
| | | | [7:2] reserved | Read as 0 |
| 0x94 | TX_FRAME_COUNT | R | [15:0] tx_frames | CSI-2 transmitted frame count |
| 0x98 | TX_ERROR_COUNT | R | [15:0] tx_errors | CSI-2 TX error count |

#### Error Flag Registers (0xA0 - 0xAF)

| Address | Name | Access | Bits | Description |
|---------|------|--------|------|-------------|
| 0xA0 | ERROR_FLAGS | R | [0] timeout | Readout timeout exceeded |
| | | | [1] overflow | Line buffer overflow |
| | | | [2] crc_error | CSI-2 CRC mismatch (self-check) |
| | | | [3] overexposure | Pixel saturation detected |
| | | | [4] roic_fault | ROIC interface error |
| | | | [5] dphy_error | D-PHY initialization failure |
| | | | [6] config_error | Invalid configuration detected |
| | | | [7] watchdog | System watchdog timeout |

#### Identification Registers (0xF0 - 0xFF)

| Address | Name | Access | Bits | Description |
|---------|------|--------|------|-------------|
| 0xF0 | DEVICE_ID | R | [15:0] id | Fixed: 0xA735 (Artix-7 35T) |
| 0xF4 | VERSION | R | [7:0] minor | Firmware minor version |
| | | | [15:8] major | Firmware major version |
| 0xF8 | BUILD_DATE | R | [15:0] date | Build date (BCD: MMDD) |

### 6.4 Estimated Resources

| Component | LUTs | FFs | BRAMs | Notes |
|-----------|------|-----|-------|-------|
| SPI protocol engine | 150 | 80 | 0 | Shift register, state machine |
| Register file | 400 | 256 | 0 | All R/W registers |
| Address decoder | 100 | 0 | 0 | Combinational decode |
| Read MUX | 150 | 16 | 0 | Multi-register read select |
| **Subtotal** | **~800** | **~352** | **0** | |

---

## 7. ROIC Interface and Deserializer

### 7.1 ROIC Data Path

```
ROIC Analog Output --> [ADC] --> LVDS Serialized Data --> FPGA ISERDES --> Pixel Formatter
                                                           |
                                                           v
                                                      Line Buffer
```

### 7.2 LVDS Receiver Configuration

| Parameter | Value |
|-----------|-------|
| I/O Standard | LVDS_25 |
| ISERDES Primitive | ISERDESE2 |
| Deserialization Ratio | 8:1 or 10:1 (ROIC-dependent) |
| Channels | 1-8 (depending on ROIC output configuration) |
| Data Rate | Up to 1.0 Gbps/channel |
| Reference Clock | clk_roic (derived from ROIC master clock) |

### 7.3 Pixel Formatter

The pixel formatter aligns raw deserialized ROIC data into 16-bit pixel values:

| Input Format | Output Format | Operation |
|-------------|---------------|-----------|
| 14-bit ADC raw | 16-bit RAW16 | Zero-pad upper 2 bits |
| 16-bit ADC raw | 16-bit RAW16 | Direct pass-through |

### 7.4 Estimated Resources

| Component | LUTs | FFs | BRAMs | Notes |
|-----------|------|-----|-------|-------|
| ISERDES instances | 200 | 64 | 0 | Per-channel deserializer |
| Bit alignment | 300 | 128 | 0 | Word boundary detection |
| Pixel formatter | 100 | 32 | 0 | Bit-width conversion |
| Channel bonding | 200 | 64 | 0 | Multi-channel sync |
| **Subtotal** | **~800** | **~288** | **0** | |

---

## 8. Protection Logic

### 8.1 Error Detection Mechanisms

| Detector | Trigger Condition | Error Code | Action |
|----------|------------------|------------|--------|
| Readout Timeout | Line readout exceeds line_time_us x 2 | 0x01 | Abort scan, ERROR state |
| Buffer Overflow | Write catches read in same bank | 0x02 | Abort scan, ERROR state |
| CRC Self-Check | CSI-2 TX CRC mismatch (loopback) | 0x04 | Log error, continue |
| Overexposure | Pixel value >= saturation threshold | 0x08 | Log warning, continue |
| ROIC Fault | No valid data from ROIC for N cycles | 0x10 | Abort scan, ERROR state |
| D-PHY Error | D-PHY init timeout or link failure | 0x20 | Retry init, then ERROR |
| Config Error | Invalid register value detected | 0x40 | Reject config, log error |
| Watchdog | System heartbeat timeout (100 ms) | 0x80 | Full reset, ERROR state |

### 8.2 Error Recovery

```
Normal Operation:
  [Any Error Detected] --> Set ERROR_FLAGS register
                      --> Transition FSM to ERROR state
                      --> Assert error interrupt (active-low output pin)
                      --> Hold all outputs in safe state

Recovery:
  [SoC writes error_clear to CONTROL] --> Clear ERROR_FLAGS
                                      --> Return FSM to IDLE
                                      --> De-assert error interrupt
```

### 8.3 Safe State Definition

When in ERROR state, the FPGA ensures:
- Gate control outputs held LOW (no X-ray exposure)
- CSI-2 TX disabled (D-PHY enters LP mode)
- Line buffer write disabled (data preserved for debug readout)
- SPI remains operational (SoC can read status and clear errors)

### 8.4 Estimated Resources

| Component | LUTs | FFs | BRAMs | Notes |
|-----------|------|-----|-------|-------|
| Timeout watchdog | 100 | 48 | 0 | Counter-based timer |
| Overflow detector | 50 | 16 | 0 | Address comparator |
| Error aggregator | 100 | 32 | 0 | Flag register + priority |
| Safe state logic | 50 | 16 | 0 | Output gating |
| **Subtotal** | **~300** | **~112** | **0** | |

---

## 9. Clock Architecture

### 9.1 Clock Tree

```
External Input:
  clk_100mhz (100 MHz board oscillator)
     |
     v
  +--------+
  | MMCM   |---> clk_sys          100 MHz    System logic, SPI, FSM
  | (PLL)  |---> clk_pixel        125.83 MHz Pixel processing (2048x2048@30fps)
  |        |---> clk_csi2_byte    125 MHz    CSI-2 byte clock (1.0 Gbps / 8)
  |        |---> clk_dphy_hs      500 MHz    D-PHY HS serialization (DDR)
  +--------+

External Input:
  clk_roic (from ROIC master clock, variable frequency)
     |
     v
  Used directly for ROIC interface and line buffer write side
```

### 9.2 Clock Domain Summary

| Clock | Frequency | Domain | Modules |
|-------|-----------|--------|---------|
| clk_sys | 100 MHz | System | SPI slave, FSM, protection logic, register file |
| clk_pixel | 125.83 MHz | Pixel | Pixel formatter (optional, may use clk_csi2_byte) |
| clk_csi2_byte | 125 MHz | CSI-2 | CSI-2 TX IP, line buffer read side |
| clk_dphy_hs | 500 MHz | D-PHY | OSERDES high-speed serialization |
| clk_roic | Variable | ROIC | ISERDES, ROIC interface, line buffer write side |

### 9.3 Clock Domain Crossings (CDC)

| Source Domain | Destination Domain | Crossing Type | Method |
|--------------|-------------------|---------------|--------|
| clk_sys -> clk_csi2_byte | Control signals | Single-bit | 2-stage FF synchronizer |
| clk_roic -> clk_csi2_byte | Bank select | Single-bit | 2-stage FF synchronizer |
| clk_roic -> clk_csi2_byte | Line data | Multi-bit | Dual-port BRAM (inherent) |
| clk_sys -> clk_roic | Timing parameters | Multi-bit | Gray-coded register sync |

---

## 10. Resource Utilization Summary

### 10.1 Module-Level Breakdown

| Module | LUTs | FFs | BRAMs | DSPs |
|--------|------|-----|-------|------|
| Panel Scan FSM | 500 | 330 | 0 | 0 |
| Line Buffer | 450 | 88 | 4 | 0 |
| CSI-2 TX Subsystem | 3,500-5,500 | 2,300-3,300 | 0-1 | 0 |
| SPI Slave + Registers | 800 | 352 | 0 | 0 |
| ROIC Interface | 800 | 288 | 0 | 0 |
| Protection Logic | 300 | 112 | 0 | 0 |
| Clock Gen (MMCM) | 50 | 16 | 0 | 0 |
| Debug (ILA/VIO) | 500-1,000 | 500-1,000 | 1-2 | 0 |
| Interconnect/glue | 200 | 100 | 0 | 0 |
| **Total Estimate** | **7,100-9,600** | **4,086-5,586** | **5-7** | **0** |
| **Device Capacity** | **20,800** | **41,600** | **50** | **90** |
| **Utilization %** | **34-46%** | **10-13%** | **10-14%** | **0%** |

### 10.2 Resource Budget Compliance

| Metric | Budget | Estimated | Status |
|--------|--------|-----------|--------|
| LUT Utilization | < 60% (12,480) | 34-46% (7,100-9,600) | PASS (14-26% margin) |
| BRAM Utilization | < 60% (30) | 10-14% (5-7) | PASS (46-50% margin) |
| Timing Closure | WNS >= 0 ns | Expected positive | Validation at synthesis |
| Power | < 2W total | ~1.0-1.5W estimated | Validation at implementation |

### 10.3 Upgrade Path

If utilization exceeds budget during development:

| Device | LUTs | BRAMs | Package | Notes |
|--------|------|-------|---------|-------|
| **XC7A35T** (current) | 20,800 | 50 | FGG484 | Baseline |
| XC7A50T | 32,600 | 75 | FGG484 | Pin-compatible, +57% LUTs |
| XC7A75T | 47,200 | 105 | FGG484 | Pin-compatible, +127% LUTs |
| XC7A100T | 63,400 | 135 | FGG484 | Pin-compatible, +205% LUTs |

---

## 11. Pin Assignments

### 11.1 D-PHY Lanes (CSI-2 Output)

| Signal | Direction | Pin (Example) | I/O Standard | Notes |
|--------|-----------|--------------|-------------|-------|
| dphy_clk_p | Output | AB12 | LVDS_25 | D-PHY continuous clock (+) |
| dphy_clk_n | Output | AB13 | LVDS_25 | D-PHY continuous clock (-) |
| dphy_data_p[0] | Output | AA10 | LVDS_25 | Data lane 0 (+) |
| dphy_data_n[0] | Output | AA11 | LVDS_25 | Data lane 0 (-) |
| dphy_data_p[1] | Output | Y10 | LVDS_25 | Data lane 1 (+) |
| dphy_data_n[1] | Output | Y11 | LVDS_25 | Data lane 1 (-) |
| dphy_data_p[2] | Output | W12 | LVDS_25 | Data lane 2 (+) |
| dphy_data_n[2] | Output | W13 | LVDS_25 | Data lane 2 (-) |
| dphy_data_p[3] | Output | V12 | LVDS_25 | Data lane 3 (+) |
| dphy_data_n[3] | Output | V13 | LVDS_25 | Data lane 3 (-) |

### 11.2 SPI Interface

| Signal | Direction | Pin (Example) | I/O Standard | Notes |
|--------|-----------|--------------|-------------|-------|
| spi_sclk | Input | T14 | LVCMOS33 | SPI clock from SoC |
| spi_mosi | Input | T15 | LVCMOS33 | Master Out Slave In |
| spi_miso | Output | U14 | LVCMOS33 | Master In Slave Out |
| spi_cs_n | Input | U15 | LVCMOS33 | Chip Select (active low) |

### 11.3 Panel Control (Gate IC)

| Signal | Direction | Pin (Example) | I/O Standard | Notes |
|--------|-----------|--------------|-------------|-------|
| gate_on | Output | P16 | LVCMOS33 | Gate IC enable |
| gate_pulse | Output | P17 | LVCMOS33 | Gate pulse output |
| roic_clk | Output | N16 | LVCMOS33 | ROIC master clock |
| roic_sync | Output | N17 | LVCMOS33 | ROIC sync/trigger |

### 11.4 ROIC Data Input (LVDS)

| Signal | Direction | Pin (Example) | I/O Standard | Notes |
|--------|-----------|--------------|-------------|-------|
| roic_data_p[0] | Input | H14 | LVDS_25 | ROIC channel 0 (+) |
| roic_data_n[0] | Input | H15 | LVDS_25 | ROIC channel 0 (-) |
| roic_data_p[1] | Input | G14 | LVDS_25 | ROIC channel 1 (+) |
| roic_data_n[1] | Input | G15 | LVDS_25 | ROIC channel 1 (-) |
| roic_frame_valid | Input | F14 | LVCMOS33 | ROIC frame valid |
| roic_line_valid | Input | F15 | LVCMOS33 | ROIC line valid |

### 11.5 Debug and Status

| Signal | Direction | Pin (Example) | I/O Standard | Notes |
|--------|-----------|--------------|-------------|-------|
| led[3:0] | Output | M14-M17 | LVCMOS33 | Status LEDs |
| error_n | Output | L14 | LVCMOS33 | Error interrupt (active low) |
| heartbeat | Output | L15 | LVCMOS33 | System alive indicator |

**Note**: Pin assignments are examples based on FGG484 package. Final assignments depend on the specific evaluation board schematic and PCB layout constraints.

---

## 12. Timing Constraints

### 12.1 Primary Clock Constraints

```tcl
# Input clock (board oscillator)
create_clock -period 10.000 -name clk_100mhz [get_ports clk_100mhz]

# MMCM-generated clocks (automatically derived by Vivado)
# clk_sys:       100.000 MHz (period = 10.000 ns)
# clk_pixel:     125.830 MHz (period =  7.948 ns)
# clk_csi2_byte: 125.000 MHz (period =  8.000 ns)
# clk_dphy_hs:   500.000 MHz (period =  2.000 ns)

# ROIC input clock (variable, example: 80 MHz)
create_clock -period 12.500 -name clk_roic [get_ports roic_clk_in]

# SPI clock (from SoC, max 50 MHz)
create_clock -period 20.000 -name spi_sclk [get_ports spi_sclk]
```

### 12.2 Clock Domain Crossing Constraints

```tcl
# Declare asynchronous clock groups
set_clock_groups -asynchronous \
    -group [get_clocks clk_sys] \
    -group [get_clocks clk_roic]

set_clock_groups -asynchronous \
    -group [get_clocks clk_roic] \
    -group [get_clocks clk_csi2_byte]

set_clock_groups -asynchronous \
    -group [get_clocks spi_sclk] \
    -group [get_clocks clk_sys]

# False path for CDC synchronizer chains
set_false_path -from [get_cells */sync_ff_1_reg] -to [get_cells */sync_ff_2_reg]
```

### 12.3 I/O Timing Constraints

```tcl
# SPI input timing (relative to spi_sclk)
set_input_delay -clock spi_sclk -max 5.000 [get_ports spi_mosi]
set_input_delay -clock spi_sclk -min 1.000 [get_ports spi_mosi]
set_input_delay -clock spi_sclk -max 5.000 [get_ports spi_cs_n]

# SPI output timing
set_output_delay -clock spi_sclk -max 5.000 [get_ports spi_miso]
set_output_delay -clock spi_sclk -min 1.000 [get_ports spi_miso]

# D-PHY output timing (high-speed, managed by OSERDES primitives)
# OSERDES output timing is handled internally by Vivado MIPI IP
```

---

## 13. Verification Strategy

### 13.1 RTL Verification Test Cases

| ID | Test | Module | Coverage Target |
|----|------|--------|----------------|
| FV-01 | SPI register R/W | spi_slave | All registers accessible |
| FV-02 | Timing FSM single scan | panel_scan_fsm | All state transitions |
| FV-03 | Timing FSM continuous scan | panel_scan_fsm | Multi-frame operation |
| FV-04 | Line buffer ping-pong | line_buffer | Zero data loss across banks |
| FV-05 | Error injection and recovery | protection_logic | All 8 error codes |
| FV-06 | SPI + data concurrent | spi_slave, csi2_tx | Dual channel isolation |
| FV-07 | Full frame (multi-line) | All | End-to-end data path |
| FV-08 | CSI-2 single lane TX | csi2_tx_subsystem | D-PHY output via OSERDES |
| FV-09 | CSI-2 multi-lane (2/4) | csi2_tx_subsystem | Lane scaling, throughput |
| FV-10 | CSI-2 long packet + CRC | csi2_tx_subsystem | Data integrity, framing |
| FV-11 | CSI-2 max throughput stress | csi2_tx_subsystem | Sustained target bandwidth |

### 13.2 Coverage Requirements

| Metric | Target | Measurement Tool |
|--------|--------|-----------------|
| Line Coverage | >= 95% | Vivado xsim / Questa |
| Branch Coverage | >= 90% | Vivado xsim / Questa |
| FSM Coverage | 100% | All states + transitions |
| Toggle Coverage | >= 80% | Signal activity analysis |

### 13.3 Cross-Verification with Simulator

The FpgaSimulator (C# .NET) serves as the golden reference model. RTL outputs are compared bit-by-bit against simulator outputs using `rtl_vs_sim_checker`:

```
FpgaSimulator (C#)                  FPGA RTL (SystemVerilog)
       |                                    |
       v                                    v
  Expected Output                      Actual Output
       |                                    |
       +----------> rtl_vs_sim_checker <----+
                          |
                    PASS / FAIL
                    (bit-accurate comparison)
```

---

## 14. Design Decisions Log

### 14.1 Key Decisions

| ID | Decision | Rationale | Date |
|----|----------|-----------|------|
| DD-01 | CSI-2 as sole data interface | USB 3.x infeasible on XC7A35T (72-120% LUT) | 2026-02-16 |
| DD-02 | 4-lane D-PHY configuration | Maximum bandwidth (4-5 Gbps) for Target tier | 2026-02-16 |
| DD-03 | Ping-Pong BRAM for line buffer | Zero-copy, clock-domain isolation | 2026-02-17 |
| DD-04 | OSERDES-based D-PHY (no external PHY) | Artix-7 native support, no BOM cost | 2026-02-17 |
| DD-05 | 4 clock domains (reduced from 6) | USB removal eliminated clk_usb, clk_csi2_esc | 2026-02-17 |
| DD-06 | SPI Mode 0, 50 MHz max | i.MX8M Plus SPI master compatibility | 2026-02-17 |
| DD-07 | 32-bit SPI transaction format | Simple protocol, sufficient for register map | 2026-02-17 |

### 14.2 Rejected Alternatives

| Alternative | Reason for Rejection |
|-------------|---------------------|
| USB 3.x interface | FPGA LUT budget exceeded (72-120%) |
| External D-PHY PHY IC | Adds BOM cost and PCB complexity; native OSERDES sufficient for Target tier |
| FIFO-based line buffer | Ping-Pong BRAM provides deterministic timing and CDC isolation |
| AXI-Lite for SPI register access | Overhead too high for simple register map; direct SPI is sufficient |
| 6 clock domains | USB and CSI-2 escape mode clocks unnecessary with CSI-2-only design |

---

## 15. Document Traceability

**Implements**:
- SPEC-ARCH-001 (REQ-ARCH-001 through REQ-ARCH-012)
- SPEC-POC-001 (CSI-2 TX IP configuration, D-PHY constraints)
- X-ray_Detector_Optimal_Project_Plan.md Section 2.3 (FPGA Internal Architecture)

**References**:
- docs/architecture/system-architecture.md (system-level context)
- MEMORY.md (FPGA constraints, verified interfaces)
- detector_config.yaml schema (configuration parameters)

**Feeds Into**:
- SPEC-FPGA-001 (FPGA RTL detailed requirements)
- FpgaSimulator golden reference model
- RTL testbench development (FV-01 through FV-11)
- PCB layout (pin assignments, I/O planning)

---

## 16. Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (architect) | Initial FPGA architecture design document |

---

**Approval**:
- [ ] FPGA Lead
- [ ] System Architect
- [ ] Project Manager

---
