# SPI Register Map API Reference

**Project**: X-ray Detector Panel System
**Interface**: FPGA SPI Slave (Artix-7 XC7A35T)
**Version**: 1.0.0
**Last Updated**: 2026-02-17

---

## 1. SPI Bus Configuration

| Parameter | Value |
|-----------|-------|
| SPI Mode | Mode 0 (CPOL=0, CPHA=0) |
| Clock Speed | Up to 50 MHz |
| Data Order | MSB first |
| Word Size | 8-bit |
| Chip Select | Active low |

---

## 2. Transaction Format

Each SPI transaction is 32 bits (4 bytes):

```
Byte 0: Register Address [7:0]
Byte 1: R/W Flag (0x00 = Read, 0x01 = Write)
Byte 2: Data MSB [15:8]
Byte 3: Data LSB [7:0]
```

### Write Transaction

```
CS_N  __|________________________________|__
SCLK  __|/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\|__
MOSI  --| ADDR[7:0] | 0x01 | DATA[15:8] | DATA[7:0] |--
MISO  --| XXXXXXXX  | XXXX | XXXXXXXXXX | XXXXXXXXX |--
```

**Example: Write 0x0001 to CONTROL register (0x00)**
```
TX: [0x00] [0x01] [0x00] [0x01]
RX: (ignored)
```

### Read Transaction

```
CS_N  __|________________________________|__
SCLK  __|/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\|__
MOSI  --| ADDR[7:0] | 0x00 | 0x00       | 0x00      |--
MISO  --| XXXXXXXX  | XXXX | RDATA[15:8]| RDATA[7:0]|--
```

**Example: Read STATUS register (0x04)**
```
TX: [0x04] [0x00] [0x00] [0x00]
RX: [XX]   [XX]   [STATUS_H] [STATUS_L]
```

---

## 3. Register Map

### 3.1 Address Space Overview

| Address Range | Block | Description |
|--------------|-------|-------------|
| 0x00 - 0x0F | Control & Status | Scan control, FSM status, counters |
| 0x20 - 0x3F | Timing Configuration | Gate timing, line/frame parameters |
| 0x40 - 0x5F | Panel Configuration | Resolution, bit depth, pixel format |
| 0x80 - 0x8F | CSI-2 Configuration | Lane count, speed, TX enable |
| 0x90 - 0x9F | Data Interface Status | Link status, TX counters |
| 0xA0 - 0xAF | Error Flags | Detailed error flag register |
| 0xF0 - 0xFF | Identification | Device ID, firmware version |

---

### 3.2 Control & Status Registers (0x00 - 0x0F)

#### 0x00: CONTROL (Write-Only)

| Bit | Name | Default | Description |
|-----|------|---------|-------------|
| [0] | start_scan | 0 | Write 1 to begin scan sequence. Self-clearing. |
| [1] | stop_scan | 0 | Write 1 to abort active scan. Self-clearing. |
| [2] | reset | 0 | Write 1 for soft reset (all registers to defaults). Self-clearing. |
| [3:2] | scan_mode | 00 | 00=Single, 01=Continuous, 10=Calibration, 11=Reserved |
| [4] | error_clear | 0 | Write 1 to clear all error flags. Self-clearing. |
| [15:5] | reserved | 0 | Must write 0 |

**Usage Notes**:
- `start_scan` has no effect if FSM is not in IDLE state
- `stop_scan` transitions FSM to IDLE from any state except ERROR
- `reset` returns all registers to power-on defaults
- `scan_mode` must be set before `start_scan`

#### 0x04: STATUS (Read-Only)

| Bit | Name | Default | Description |
|-----|------|---------|-------------|
| [0] | idle | 1 | 1 = FSM in IDLE state |
| [1] | busy | 0 | 1 = Scan in progress (INTEGRATE, READOUT, LINE_DONE, FRAME_DONE) |
| [2] | error | 0 | 1 = Error condition active (FSM in ERROR state) |
| [7:3] | error_code | 00000 | Active error code (see Error Flags section) |
| [10:8] | fsm_state | 000 | Current FSM state encoding (see table below) |
| [11] | buffer_bank | 0 | Currently active write bank (0=A, 1=B) |
| [15:12] | reserved | 0 | Read as 0 |

**FSM State Encoding**:
| Value | State |
|-------|-------|
| 3'b000 | IDLE |
| 3'b001 | INTEGRATE |
| 3'b010 | READOUT |
| 3'b011 | LINE_DONE |
| 3'b100 | FRAME_DONE |
| 3'b101 | ERROR |

#### 0x08: FRAME_COUNTER (Read-Only)

| Bit | Name | Description |
|-----|------|-------------|
| [15:0] | frame_count_lo | Lower 16 bits of 32-bit frame counter |

#### 0x0A: FRAME_COUNTER_H (Read-Only)

| Bit | Name | Description |
|-----|------|-------------|
| [15:0] | frame_count_hi | Upper 16 bits of 32-bit frame counter |

**Note**: Read FRAME_COUNTER_H first, then FRAME_COUNTER to get consistent 32-bit value. Counter resets on soft reset.

#### 0x0C: LINE_COUNTER (Read-Only)

| Bit | Name | Description |
|-----|------|-------------|
| [11:0] | line_count | Current line being processed (0 to rows-1) |
| [15:12] | reserved | Read as 0 |

---

### 3.3 Timing Configuration Registers (0x20 - 0x3F)

#### 0x20: GATE_ON_US (Read/Write)

| Bit | Name | Default | Range | Description |
|-----|------|---------|-------|-------------|
| [15:0] | gate_on | 1000 | 1-65535 | Gate ON duration in microseconds (X-ray exposure time) |

#### 0x24: GATE_OFF_US (Read/Write)

| Bit | Name | Default | Range | Description |
|-----|------|---------|-------|-------------|
| [15:0] | gate_off | 100 | 1-65535 | Gate OFF settling time in microseconds |

#### 0x28: ROIC_SETTLE_US (Read/Write)

| Bit | Name | Default | Range | Description |
|-----|------|---------|-------|-------------|
| [7:0] | settle | 10 | 1-255 | ROIC settling time after gate transition (microseconds) |
| [15:8] | reserved | 0 | - | Must write 0 |

#### 0x2C: ADC_CONV_US (Read/Write)

| Bit | Name | Default | Range | Description |
|-----|------|---------|-------|-------------|
| [7:0] | conv | 5 | 1-255 | ADC conversion time per line (microseconds) |
| [15:8] | reserved | 0 | - | Must write 0 |

#### 0x30: LINE_TIME_US (Read/Write)

| Bit | Name | Default | Range | Description |
|-----|------|---------|-------|-------------|
| [15:0] | line_time | 16 | 1-65535 | Total line period (microseconds). Must be > roic_settle + adc_conv. |

#### 0x34: FRAME_BLANK_US (Read/Write)

| Bit | Name | Default | Range | Description |
|-----|------|---------|-------|-------------|
| [15:0] | blank | 500 | 1-65535 | Inter-frame blanking time (microseconds) |

---

### 3.4 Panel Configuration Registers (0x40 - 0x5F)

#### 0x40: PANEL_ROWS (Read/Write)

| Bit | Name | Default | Range | Description |
|-----|------|---------|-------|-------------|
| [11:0] | rows | 2048 | 1-3072 | Number of panel rows |
| [15:12] | reserved | 0 | - | Must write 0 |

#### 0x44: PANEL_COLS (Read/Write)

| Bit | Name | Default | Range | Description |
|-----|------|---------|-------|-------------|
| [11:0] | cols | 2048 | 1-3072 | Number of panel columns |
| [15:12] | reserved | 0 | - | Must write 0 |

#### 0x48: BIT_DEPTH (Read/Write)

| Bit | Name | Default | Range | Description |
|-----|------|---------|-------|-------------|
| [4:0] | depth | 16 | 14 or 16 | Pixel bit depth |
| [15:5] | reserved | 0 | - | Must write 0 |

#### 0x4C: PIXEL_FORMAT (Read/Write)

| Bit | Name | Default | Range | Description |
|-----|------|---------|-------|-------------|
| [7:0] | format | 0x2C | See table | CSI-2 data type code |
| [15:8] | reserved | 0 | - | Must write 0 |

**Supported Data Types**:
| Code | Format | Description |
|------|--------|-------------|
| 0x2B | RAW14 | 14-bit raw pixel data |
| 0x2C | RAW16 | 16-bit raw pixel data (default) |

---

### 3.5 CSI-2 Configuration Registers (0x80 - 0x8F)

#### 0x80: CSI2_CONTROL (Read/Write)

| Bit | Name | Default | Description |
|-----|------|---------|-------------|
| [1:0] | lane_count | 10 | 00=1-lane, 01=2-lane, 10=4-lane |
| [2] | tx_enable | 0 | 1=Enable CSI-2 TX. Must be set before start_scan. |
| [3] | continuous_clk | 0 | 1=Continuous HS clock (always on), 0=Gated clock (power save) |
| [7:4] | reserved | 0 | Must write 0 |

#### 0x84: CSI2_STATUS (Read-Only)

| Bit | Name | Description |
|-----|------|-------------|
| [0] | phy_ready | 1=D-PHY initialization complete, lanes in LP state |
| [1] | tx_active | 1=HS packet transmission in progress |
| [2] | fifo_overflow | 1=TX FIFO overflow detected (sticky, clear via error_clear) |
| [15:3] | reserved | Read as 0 |

#### 0x88: CSI2_LANE_SPEED (Read/Write)

| Bit | Name | Default | Description |
|-----|------|---------|-------------|
| [7:0] | speed_code | 0x64 | Lane speed configuration |
| [15:8] | reserved | 0 | Must write 0 |

**Speed Codes**:
| Code | Lane Speed | 4-lane Aggregate | Notes |
|------|-----------|-----------------|-------|
| 0x64 | 1.0 Gbps | 4.0 Gbps | Default, conservative |
| 0x6E | 1.1 Gbps | 4.4 Gbps | Intermediate |
| 0x78 | 1.2 Gbps | 4.8 Gbps | Intermediate |
| 0x7D | 1.25 Gbps | 5.0 Gbps | Maximum (Artix-7 OSERDES limit) |

---

### 3.6 Data Interface Status Registers (0x90 - 0x9F)

#### 0x90: DATA_IF_STATUS (Read-Only)

| Bit | Name | Description |
|-----|------|-------------|
| [0] | csi2_link_up | 1=CSI-2 D-PHY link established (lanes initialized) |
| [1] | csi2_tx_ok | 1=Last CSI-2 TX completed without error |
| [7:2] | reserved | Read as 0 |

#### 0x94: TX_FRAME_COUNT (Read-Only)

| Bit | Name | Description |
|-----|------|-------------|
| [15:0] | tx_frames | Number of frames successfully transmitted via CSI-2 |

#### 0x98: TX_ERROR_COUNT (Read-Only)

| Bit | Name | Description |
|-----|------|-------------|
| [15:0] | tx_errors | Number of CSI-2 TX errors (CRC, FIFO overflow, etc.) |

---

### 3.7 Error Flag Registers (0xA0 - 0xAF)

#### 0xA0: ERROR_FLAGS (Read-Only)

All flags are sticky (remain set until cleared by writing `error_clear` to CONTROL register).

| Bit | Name | Code | Description |
|-----|------|------|-------------|
| [0] | timeout | 0x01 | Readout exceeded line_time_us x 2 |
| [1] | overflow | 0x02 | Line buffer write caught read (bank collision) |
| [2] | crc_error | 0x04 | CSI-2 CRC self-check mismatch |
| [3] | overexposure | 0x08 | Pixel value at saturation threshold |
| [4] | roic_fault | 0x10 | No valid ROIC data for timeout period |
| [5] | dphy_error | 0x20 | D-PHY initialization or link failure |
| [6] | config_error | 0x40 | Invalid register configuration detected |
| [7] | watchdog | 0x80 | System heartbeat timeout (100 ms) |

---

### 3.8 Identification Registers (0xF0 - 0xFF)

#### 0xF0: DEVICE_ID (Read-Only)

| Bit | Name | Value | Description |
|-----|------|-------|-------------|
| [15:0] | id | 0xA735 | Fixed device identifier (Artix-7 35T) |

#### 0xF4: VERSION (Read-Only)

| Bit | Name | Description |
|-----|------|-------------|
| [7:0] | minor | Firmware minor version (0-255) |
| [15:8] | major | Firmware major version (0-255) |

#### 0xF8: BUILD_DATE (Read-Only)

| Bit | Name | Description |
|-----|------|-------------|
| [15:0] | date | Build date in BCD format (MMDD) |

---

## 4. Programming Examples

### 4.1 Initialization Sequence

```
1. Read DEVICE_ID (0xF0) → Verify 0xA735
2. Read VERSION (0xF4)   → Log firmware version
3. Write PANEL_ROWS (0x40) = target rows (e.g., 2048)
4. Write PANEL_COLS (0x44) = target cols (e.g., 2048)
5. Write BIT_DEPTH (0x48)  = 16
6. Write PIXEL_FORMAT (0x4C) = 0x2C (RAW16)
7. Write GATE_ON_US (0x20) = exposure time
8. Write GATE_OFF_US (0x24) = gate off time
9. Write ROIC_SETTLE_US (0x28) = settling time
10. Write ADC_CONV_US (0x2C) = ADC conversion time
11. Write LINE_TIME_US (0x30) = total line period
12. Write FRAME_BLANK_US (0x34) = blanking time
13. Write CSI2_LANE_SPEED (0x88) = 0x64 (1.0 Gbps)
14. Write CSI2_CONTROL (0x80) = 0x06 (4-lane, TX enable)
15. Poll CSI2_STATUS (0x84) until phy_ready = 1
16. Read STATUS (0x04) → Verify idle = 1
```

### 4.2 Single Scan Sequence

```
1. Write CONTROL (0x00) = 0x01 (start_scan, single mode)
2. Poll STATUS (0x04) until busy = 1 (scan started)
3. Poll STATUS (0x04) until busy = 0 (scan complete)
4. Read FRAME_COUNTER (0x08, 0x0A) → Verify incremented
5. Check ERROR_FLAGS (0xA0) → Verify 0x00
```

### 4.3 Error Recovery Sequence

```
1. Read STATUS (0x04) → error = 1
2. Read ERROR_FLAGS (0xA0) → Identify error type
3. Log error code for diagnostics
4. Write CONTROL (0x00) = 0x10 (error_clear)
5. Poll STATUS (0x04) until idle = 1
6. Optionally retry scan
```

---

## 5. Document Traceability

**Implements**: docs/architecture/fpga-design.md Section 6 (SPI Slave and Register Map)

**References**: SPEC-ARCH-001, SPEC-POC-001

---
