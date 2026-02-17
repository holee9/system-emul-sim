# SPI Register Map API Reference

**Document Version**: 1.0.0
**Status**: Reviewed
**Last Updated**: 2026-02-17

---

## Overview

This document specifies the SPI slave register interface implemented in the FPGA (Xilinx Artix-7 XC7A35T). The SoC (NXP i.MX8M Plus) acts as the SPI master and uses this interface to configure the FPGA, start and stop panel scans, and monitor operational status.

The SPI interface is the sole control channel between the SoC and FPGA. Pixel data uses the separate CSI-2 D-PHY interface.

---

## 1. SPI Bus Configuration

### 1.1 Protocol Parameters

| Parameter | Value |
|-----------|-------|
| SPI mode | Mode 0 (CPOL=0, CPHA=0) |
| Bit order | MSB first |
| Clock speed | Up to 50 MHz |
| Word size | 16 bits per SPI word |
| Transaction size | 32 bits (2 x 16-bit words) |
| Chip select | Active low (CS_N) |
| Bus driver | spi-imx kernel driver on SoC |
| User-space device | /dev/spidev0.0 |

### 1.2 Clock Timing

| Parameter | Value |
|-----------|-------|
| Setup time (MOSI to SCLK rising) | minimum 2 ns |
| Hold time (MOSI after SCLK rising) | minimum 2 ns |
| CS_N to SCLK first edge | minimum 20 ns |
| SCLK last edge to CS_N release | minimum 20 ns |
| Inter-transaction gap (CS_N high) | minimum 40 ns |

---

## 2. Transaction Format

Every SPI transaction consists of exactly 32 bits transferred as two 16-bit words (bits_per_word=16) on MOSI and MISO.

### 2.1 Word Layout

```
Word 0 (16-bit)                  Word 1 (16-bit)
+----------------+----------------+---------------------------+
| ADDR[7:0]      | R/W flag[7:0]  | DATA[15:0]                |
+----------------+----------------+---------------------------+
  Register addr    0x00=Read         16-bit register data
                   0x01=Write
```

- **Word 0, Byte 0 (ADDR)**: 8-bit register address (0x00 to 0xFF)
- **Word 0, Byte 1 (R/W)**: 0x00 for read, 0x01 for write
- **Word 1 (DATA)**: 16-bit data field (MSB first on wire)

### 2.2 Write Transaction

The SoC sends 2 x 16-bit words. The FPGA ignores the MISO output during writes.

```
CS_N  _____|________________________________|_____
SCLK  _____|/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\|_____
MOSI  -----| Word0: ADDR | 0x01 | Word1: DATA[15:0] |-----
MISO  -----| (don't care)                          |-----
           |<--- 16 clk --->|<----- 16 clk ------->|
```

**Example: Write 0x0001 to CONTROL register (address 0x21)**

```c
uint16_t tx[2] = {(0x21 << 8) | 0x01, 0x0001};
// On wire: [0x21][0x01][0x00][0x01]
```

### 2.3 Read Transaction

The SoC sends Word 0 (address + read flag). The FPGA drives MISO with the 16-bit register data during Word 1.

```
CS_N  _____|________________________________|_____
SCLK  _____|/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\/\|_____
MOSI  -----| Word0: ADDR | 0x00 | Word1: 0x0000 |-----
MISO  -----| (don't care)      | Word1: DATA[15:0]|---
           |<--- 16 clk --->|<----- 16 clk ------->|
```

**Example: Read STATUS register (address 0x20)**

```c
uint16_t tx[2] = {(0x20 << 8) | 0x00, 0x0000};
uint16_t rx[2] = {0};
// On wire TX: [0x20][0x00][0x00][0x00]
// On wire RX: [xx][xx][STATUS_H][STATUS_L]
// Result: status = rx[1]
```

---

## 3. Register Map

### 3.1 Address Space Overview

| Address Range | Block | Access |
|--------------|-------|--------|
| 0x00 - 0x0F | Identification | Read-Only |
| 0x10 - 0x1F | ILA Capture | Read-Only |
| 0x20 - 0x2F | Control and Status | Mixed |
| 0x30 - 0x3F | Counters | Read-Only |
| 0x40 - 0x4F | Panel Configuration | Read/Write |
| 0x50 - 0x5F | Timing Configuration | Read/Write |
| 0x60 - 0x6F | CSI-2 Configuration | Read/Write |
| 0x70 - 0x7F | Data Interface Status | Read-Only |
| 0x80 - 0x8F | Error Flags | Read/Write-1-Clear |

---

### 3.2 Identification Registers (0x00 - 0x0F)

These registers are read-only and return fixed values burned into the firmware bitstream.

#### 0x00: DEVICE_ID (Read-Only)

| Bit | Name | Value | Description |
|-----|------|-------|-------------|
| [15:0] | device_id | 0xD7E0_0001 split as two 16-bit reads | Upper 16 bits at 0x00, lower 16 bits at 0x01 |

**Note**: Since each SPI register is 16 bits wide, the 32-bit DEVICE_ID occupies two consecutive addresses:
- Address 0x00: returns 0xD7E0 (upper 16 bits)
- Address 0x01: returns 0x0001 (lower 16 bits)

The full DEVICE_ID is 0xD7E0_0001. The upper 16 bits encode the product family (D7E0 = "Detector, 7-series FPGA, E0 variant"). The lower 16 bits encode the hardware revision (0x0001 = revision 1).

#### 0x01: DEVICE_ID_LO (Read-Only)

| Bit | Name | Value | Description |
|-----|------|-------|-------------|
| [15:0] | device_id_lo | 0x0001 | Lower 16 bits of device identifier |

#### 0x02: FW_VERSION (Read-Only)

| Bit | Name | Description |
|-----|------|-------------|
| [7:0] | fw_minor | Firmware minor version (0-255) |
| [15:8] | fw_major | Firmware major version (0-255) |

To read firmware version "1.0": fw_major=0x01, fw_minor=0x00, register value = 0x0100.

#### 0x03: BUILD_DATE (Read-Only)

| Bit | Name | Description |
|-----|------|-------------|
| [15:0] | build_date | Build date in BCD format (MMDD). Example: 0x0217 = February 17. |

---

### 3.3 ILA Capture Registers (0x10 - 0x1F)

These registers provide read-only access to Integrated Logic Analyzer (ILA) capture data for in-system debugging. They are used during the 800 Mbps/lane debugging phase to inspect D-PHY and CSI-2 internal signals without requiring a JTAG connection.

| Address | Name | Description |
|---------|------|-------------|
| 0x10 | ILA_CAPTURE_0 | ILA probe capture word 0 (bits [15:0] of internal probe bus) |
| 0x11 | ILA_CAPTURE_1 | ILA probe capture word 1 (bits [31:16]) |
| 0x12 | ILA_CAPTURE_2 | ILA probe capture word 2 (bits [47:32]) |
| 0x13 | ILA_CAPTURE_3 | ILA probe capture word 3 (bits [63:48]) |
| 0x14 | ILA_TRIGGER_COUNT | Number of ILA triggers fired since last reset |
| 0x15 | ILA_STATUS | bit[0]=capture_valid, bit[1]=armed, bit[2]=triggered |
| 0x16 - 0x1F | Reserved | Read as 0x0000 |

ILA captures are primarily used during post-silicon bring-up and are not needed during normal operation.

---

### 3.4 Control and Status Registers (0x20 - 0x2F)

#### 0x20: STATUS (Read-Only)

Current operational status of the FPGA. Polled by SoC firmware every 100 microseconds.

| Bit | Name | Description |
|-----|------|-------------|
| [0] | idle | 1 = FSM in IDLE state, ready to accept start_scan |
| [1] | scan_active | 1 = Scan in progress (INTEGRATE, READOUT, LINE_DONE, or FRAME_DONE state) |
| [2] | error | 1 = Error condition active (FSM in ERROR state) |
| [3] | frame_done | 1 = Frame completed since last STATUS read (self-clearing on read) |
| [7:4] | error_code | 4-bit error category (see ERROR_FLAGS register for details) |
| [10:8] | fsm_state | Current FSM state encoding (see table) |
| [11] | buffer_bank | Currently active write bank: 0=Bank A, 1=Bank B |
| [12] | csi2_phy_ready | 1 = D-PHY initialization complete and lanes in LP idle state |
| [13] | csi2_tx_active | 1 = CSI-2 HS packet transmission in progress |
| [15:14] | reserved | Read as 0 |

**FSM State Encoding**:

| Value | State | Description |
|-------|-------|-------------|
| 3'b000 | IDLE | Waiting for start_scan command |
| 3'b001 | INTEGRATE | Gate ON, X-ray exposure timer running |
| 3'b010 | READOUT | Gate OFF, ROIC ADC conversion and line readout |
| 3'b011 | LINE_DONE | Line written to buffer, incrementing line counter |
| 3'b100 | FRAME_DONE | Frame complete, updating frame counter |
| 3'b101 | ERROR | Error detected, all outputs in safe state |

#### 0x21: CONTROL (Read/Write)

Write-only fields below are self-clearing (return to 0 after one clock cycle after being written to 1).

| Bit | Name | Default | Description |
|-----|------|---------|-------------|
| [0] | scan_enable | 0 | Write 1 to begin scan sequence. Has no effect if FSM is not in IDLE. |
| [1] | scan_stop | 0 | Write 1 to abort active scan and return FSM to IDLE. |
| [2] | reset | 0 | Write 1 for soft reset: all registers return to power-on defaults. |
| [4:3] | scan_mode | 00 | 00=Single scan, 01=Continuous scan, 10=Calibration (dark frame), 11=Reserved |
| [5] | test_pattern_en | 0 | 1 = Enable internal test pattern generator instead of ROIC data |
| [7:6] | test_pattern_mode | 00 | 00=Counter, 01=Checkerboard, 10=Constant (0x8000), 11=Reserved |
| [8] | error_clear | 0 | Write 1 to clear all ERROR_FLAGS bits and return FSM from ERROR to IDLE |
| [15:9] | reserved | 0 | Must write 0 |

**Usage notes**:
- Set scan_mode bits before asserting scan_enable in the same or preceding transaction
- scan_enable and scan_stop must not be asserted simultaneously
- reset clears frame counters, timing registers return to defaults

---

### 3.5 Counter Registers (0x30 - 0x3F)

#### 0x30: FRAME_COUNT_LO (Read-Only)

| Bit | Name | Description |
|-----|------|-------------|
| [15:0] | frame_count[15:0] | Lower 16 bits of the 32-bit transmitted frame counter |

#### 0x31: FRAME_COUNT_HI (Read-Only)

| Bit | Name | Description |
|-----|------|-------------|
| [15:0] | frame_count[31:16] | Upper 16 bits of the 32-bit transmitted frame counter |

**Read Order**: Read FRAME_COUNT_HI first, then FRAME_COUNT_LO to obtain a consistent 32-bit value. The hardware latches the upper bits when FRAME_COUNT_LO is read to prevent a race condition.

#### 0x32: LINE_COUNT (Read-Only)

| Bit | Name | Description |
|-----|------|-------------|
| [11:0] | line_count | Current line index being processed (0 to rows-1) |
| [15:12] | reserved | Read as 0 |

#### 0x33: TX_FRAME_COUNT (Read-Only)

| Bit | Name | Description |
|-----|------|-------------|
| [15:0] | tx_frames | Frames successfully transmitted via CSI-2 (lower 16 bits) |

#### 0x34: TX_ERROR_COUNT (Read-Only)

| Bit | Name | Description |
|-----|------|-------------|
| [15:0] | tx_errors | Cumulative CSI-2 TX error count (CRC mismatches, FIFO overflows) |

---

### 3.6 Panel Configuration Registers (0x40 - 0x4F)

These registers define the panel resolution and pixel format. Must be written before asserting scan_enable.

#### 0x40: CONFIG_ROWS (Read/Write)

| Bit | Name | Default | Range | Description |
|-----|------|---------|-------|-------------|
| [13:0] | rows | 0x0800 (2048) | 1-3072 | Number of panel rows. Sets the frame height. |
| [15:14] | reserved | 0 | - | Must write 0 |

#### 0x41: CONFIG_COLS (Read/Write)

| Bit | Name | Default | Range | Description |
|-----|------|---------|-------|-------------|
| [13:0] | cols | 0x0800 (2048) | 1-3072 | Number of panel columns. Sets the frame width and CSI-2 WC. |
| [15:14] | reserved | 0 | - | Must write 0 |

#### 0x42: BIT_DEPTH (Read/Write)

| Bit | Name | Default | Valid values | Description |
|-----|------|---------|-------------|-------------|
| [4:0] | bit_depth | 16 | 14 or 16 | Pixel bit depth. 14 sets CSI-2 DT to RAW14 (0x2D). 16 sets DT to RAW16 (0x2C). |
| [15:5] | reserved | 0 | - | Must write 0 |

#### 0x43: PIXEL_FORMAT (Read-Only)

| Bit | Name | Description |
|-----|------|-------------|
| [7:0] | csi2_data_type | Current CSI-2 data type code (auto-set from BIT_DEPTH: 16->0x2C, 14->0x2D) |
| [15:8] | reserved | Read as 0 |

---

### 3.7 Timing Configuration Registers (0x50 - 0x5F)

Timing registers control the panel scan sequence. All times are in units of 10 nanoseconds (100 MHz system clock ticks). To convert microseconds to register value: value = us x 100.

#### 0x50: TIMING_GATE_ON (Read/Write)

| Bit | Name | Default | Range | Description |
|-----|------|---------|-------|-------------|
| [15:0] | gate_on | 0x186A (100000 = 1000 us) | 1-65535 | Gate ON duration in 10ns units. Sets X-ray exposure time. |

#### 0x51: TIMING_GATE_OFF (Read/Write)

| Bit | Name | Default | Range | Description |
|-----|------|---------|-------|-------------|
| [15:0] | gate_off | 0x2710 (10000 = 100 us) | 1-65535 | Gate OFF settling time in 10ns units |

#### 0x52: TIMING_ROIC_SETTLE (Read/Write)

| Bit | Name | Default | Range | Description |
|-----|------|---------|-------|-------------|
| [7:0] | roic_settle | 0x64 (100 = 1 us) | 1-255 | ROIC settling time in 10ns units after gate transition |
| [15:8] | reserved | 0 | - | Must write 0 |

#### 0x53: TIMING_ADC_CONV (Read/Write)

| Bit | Name | Default | Range | Description |
|-----|------|---------|-------|-------------|
| [7:0] | adc_conv | 0x32 (50 = 0.5 us) | 1-255 | ADC conversion time per line in 10ns units |
| [15:8] | reserved | 0 | - | Must write 0 |

#### 0x54: TIMING_LINE_PERIOD (Read/Write)

| Bit | Name | Default | Range | Description |
|-----|------|---------|-------|-------------|
| [15:0] | line_period | 0x0640 (1600 = 16 us) | 1-65535 | Total line period in 10ns units. Must be >= roic_settle + adc_conv. |

#### 0x55: TIMING_FRAME_BLANK (Read/Write)

| Bit | Name | Default | Range | Description |
|-----|------|---------|-------|-------------|
| [15:0] | frame_blank | 0xC350 (50000 = 500 us) | 1-65535 | Inter-frame blanking time in 10ns units |

---

### 3.8 CSI-2 Configuration Registers (0x60 - 0x6F)

#### 0x60: CSI2_LANE_SPEED (Read/Write)

Selects the D-PHY lane operating speed. Changing this register while a scan is active is not supported and may cause CSI-2 link failure.

| Bit | Name | Default | Description |
|-----|------|---------|-------------|
| [0] | speed_select | 0 | 0 = 400 Mbps/lane (1.6 Gbps aggregate, stable), 1 = 800 Mbps/lane (3.2 Gbps, debugging) |
| [15:1] | reserved | 0 | Must write 0 |

**Speed vs Tier Compatibility**:

| speed_select | Lane Speed | 4-Lane Aggregate | Supported Tiers |
|-------------|-----------|-----------------|-----------------|
| 0 (400M) | 400 Mbps/lane | 1.6 Gbps | Minimum, Intermediate-A |
| 1 (800M) | 800 Mbps/lane | 3.2 Gbps | Minimum, Intermediate-A, Intermediate-B, Target |

#### 0x61: CSI2_CONTROL (Read/Write)

| Bit | Name | Default | Description |
|-----|------|---------|-------------|
| [1:0] | lane_count | 10 | 00=1-lane, 01=2-lane, 10=4-lane (recommended), 11=Reserved |
| [2] | tx_enable | 0 | 1 = Enable CSI-2 TX. Must be set before asserting scan_enable. |
| [3] | continuous_clk | 0 | 1 = Keep HS clock running continuously. 0 = Gate clock off between packets (power save). |
| [7:4] | reserved | 0 | Must write 0 |

#### 0x62: CSI2_VIRTUAL_CHANNEL (Read/Write)

| Bit | Name | Default | Description |
|-----|------|---------|-------------|
| [1:0] | vc | 0 | CSI-2 virtual channel (0-3). Default 0, no multiplexing in this system. |
| [15:2] | reserved | 0 | Must write 0 |

---

### 3.9 Data Interface Status Registers (0x70 - 0x7F)

#### 0x70: CSI2_STATUS (Read-Only)

| Bit | Name | Description |
|-----|------|-------------|
| [0] | phy_ready | 1 = D-PHY initialization complete, lanes in LP-11 idle state |
| [1] | tx_active | 1 = HS packet transmission in progress |
| [2] | fifo_overflow | 1 = TX FIFO overflow (sticky flag, cleared by ERROR_FLAGS write-1-clear) |
| [3] | link_error | 1 = D-PHY link error detected (LP state error or sync loss) |
| [15:4] | reserved | Read as 0 |

#### 0x71 - 0x7F: Reserved

Read as 0x0000. Do not write.

---

### 3.10 Error Flags Register (0x80)

#### 0x80: ERROR_FLAGS (Read/Write-1-Clear)

All error flags are sticky: they remain set until explicitly cleared by the SoC. To clear a flag, write 1 to the corresponding bit position. Writing 0 to a bit has no effect.

| Bit | Name | Error Code | Description |
|-----|------|-----------|-------------|
| [0] | timeout | 0x01 | Readout exceeded TIMING_LINE_PERIOD x 2. Line did not complete in time. |
| [1] | overflow | 0x02 | Line buffer bank collision: write address overtook read address. |
| [2] | crc_error | 0x04 | CSI-2 self-check CRC mismatch (internal loopback path) |
| [3] | overexposure | 0x08 | One or more pixels reached saturation threshold (0xFFFC for 14-bit, 0xFFF8 for 16-bit) |
| [4] | roic_fault | 0x10 | No valid ROIC data received within TIMING_LINE_PERIOD |
| [5] | dphy_error | 0x20 | D-PHY initialization failed or link loss detected |
| [6] | config_error | 0x40 | Invalid register configuration: rows=0, cols=0, or bit_depth not in {14,16} |
| [7] | watchdog | 0x80 | System heartbeat timer expired (100 ms threshold). Indicates SoC lost communication. |

**Error flag behavior**: When any flag in ERROR_FLAGS is set, the STATUS register bit[2] (error) is also asserted and the FSM transitions to the ERROR state. The FPGA holds all outputs safe (gate OFF, CSI-2 TX disabled) while in ERROR state.

**Error recovery sequence**:
1. SoC reads ERROR_FLAGS to identify the error
2. SoC logs the error code
3. SoC writes 0xFF to ERROR_FLAGS to clear all flags
4. SoC reads STATUS and waits for idle=1
5. SoC optionally retries the scan

---

## 4. Complete Register Table

| Address | Name | Access | Default | Description |
|---------|------|--------|---------|-------------|
| 0x00 | DEVICE_ID | RO | 0xD7E0 | Device identifier, upper 16 bits |
| 0x01 | DEVICE_ID_LO | RO | 0x0001 | Device identifier, lower 16 bits |
| 0x02 | FW_VERSION | RO | - | Firmware version [major:minor] |
| 0x03 | BUILD_DATE | RO | - | Build date BCD (MMDD) |
| 0x10 | ILA_CAPTURE_0 | RO | - | ILA probe word 0 |
| 0x11 | ILA_CAPTURE_1 | RO | - | ILA probe word 1 |
| 0x12 | ILA_CAPTURE_2 | RO | - | ILA probe word 2 |
| 0x13 | ILA_CAPTURE_3 | RO | - | ILA probe word 3 |
| 0x14 | ILA_TRIGGER_COUNT | RO | 0x0000 | ILA trigger event count |
| 0x15 | ILA_STATUS | RO | 0x0000 | ILA state flags |
| 0x20 | STATUS | RO | 0x0001 | FSM and hardware status |
| 0x21 | CONTROL | RW | 0x0000 | Scan control and reset |
| 0x30 | FRAME_COUNT_LO | RO | 0x0000 | Frame counter lower 16 bits |
| 0x31 | FRAME_COUNT_HI | RO | 0x0000 | Frame counter upper 16 bits |
| 0x32 | LINE_COUNT | RO | 0x0000 | Current line index |
| 0x33 | TX_FRAME_COUNT | RO | 0x0000 | Transmitted frame count |
| 0x34 | TX_ERROR_COUNT | RO | 0x0000 | CSI-2 TX error count |
| 0x40 | CONFIG_ROWS | RW | 0x0800 | Panel row count (1-3072) |
| 0x41 | CONFIG_COLS | RW | 0x0800 | Panel column count (1-3072) |
| 0x42 | BIT_DEPTH | RW | 0x0010 | Pixel bit depth (14 or 16) |
| 0x43 | PIXEL_FORMAT | RO | 0x002C | CSI-2 data type (derived from BIT_DEPTH) |
| 0x50 | TIMING_GATE_ON | RW | 0x186A | Gate ON time (10ns units) |
| 0x51 | TIMING_GATE_OFF | RW | 0x2710 | Gate OFF time (10ns units) |
| 0x52 | TIMING_ROIC_SETTLE | RW | 0x0064 | ROIC settle time (10ns units) |
| 0x53 | TIMING_ADC_CONV | RW | 0x0032 | ADC conversion time (10ns units) |
| 0x54 | TIMING_LINE_PERIOD | RW | 0x0640 | Line period (10ns units) |
| 0x55 | TIMING_FRAME_BLANK | RW | 0xC350 | Frame blanking time (10ns units) |
| 0x60 | CSI2_LANE_SPEED | RW | 0x0000 | Lane speed select (0=400M, 1=800M) |
| 0x61 | CSI2_CONTROL | RW | 0x0004 | Lane count, TX enable, clock mode |
| 0x62 | CSI2_VIRTUAL_CHANNEL | RW | 0x0000 | Virtual channel (always 0) |
| 0x70 | CSI2_STATUS | RO | 0x0000 | CSI-2 link and TX status |
| 0x80 | ERROR_FLAGS | RW1C | 0x0000 | Error flag bits (write-1-clear) |

---

## 5. Programming Sequences

### 5.1 Initialization Sequence

Recommended initialization sequence after power-on or soft reset:

```c
// Step 1: Verify device identity
uint16_t id_hi, id_lo, fw_ver;
fpga_reg_read(spi, 0x00, &id_hi);    // Expect 0xD7E0
fpga_reg_read(spi, 0x01, &id_lo);    // Expect 0x0001
fpga_reg_read(spi, 0x02, &fw_ver);   // Log firmware version
assert(id_hi == 0xD7E0 && id_lo == 0x0001);

// Step 2: Configure panel resolution (Intermediate-A tier)
fpga_reg_write(spi, 0x40, 2048);     // CONFIG_ROWS = 2048
fpga_reg_write(spi, 0x41, 2048);     // CONFIG_COLS = 2048
fpga_reg_write(spi, 0x42, 16);       // BIT_DEPTH = 16 (RAW16)

// Step 3: Configure timing (in 10ns units)
fpga_reg_write(spi, 0x50, 100000);   // TIMING_GATE_ON = 1000 us
fpga_reg_write(spi, 0x51, 10000);    // TIMING_GATE_OFF = 100 us
fpga_reg_write(spi, 0x52, 100);      // TIMING_ROIC_SETTLE = 1 us
fpga_reg_write(spi, 0x53, 50);       // TIMING_ADC_CONV = 0.5 us
fpga_reg_write(spi, 0x54, 1600);     // TIMING_LINE_PERIOD = 16 us
fpga_reg_write(spi, 0x55, 50000);    // TIMING_FRAME_BLANK = 500 us

// Step 4: Configure CSI-2
fpga_reg_write(spi, 0x60, 0);        // CSI2_LANE_SPEED = 400M (stable)
fpga_reg_write(spi, 0x61, 0x06);     // CSI2_CONTROL: 4-lane (10b), tx_enable=1

// Step 5: Wait for D-PHY ready
uint16_t csi2_status;
do {
    fpga_reg_read(spi, 0x70, &csi2_status);
} while (!(csi2_status & 0x01));     // Wait for phy_ready

// Step 6: Verify IDLE state
uint16_t status;
fpga_reg_read(spi, 0x20, &status);
assert(status & 0x01);               // idle bit must be set
```

### 5.2 Single Scan Sequence

```c
// Ensure IDLE state
uint16_t status;
fpga_reg_read(spi, 0x20, &status);
assert(status & 0x01);  // Must be idle

// Set single scan mode and start
// CONTROL: scan_mode=00 (single), scan_enable=1
fpga_reg_write(spi, 0x21, 0x0001);

// Wait for scan to start (busy bit)
do {
    fpga_reg_read(spi, 0x20, &status);
} while (!(status & 0x02));

// Wait for scan to complete (busy clears, idle sets)
do {
    fpga_reg_read(spi, 0x20, &status);
} while (!(status & 0x01));

// Verify no errors
uint16_t errors;
fpga_reg_read(spi, 0x80, &errors);
assert(errors == 0);

// Read frame count to confirm transmission
uint16_t fc_hi, fc_lo;
fpga_reg_read(spi, 0x31, &fc_hi);
fpga_reg_read(spi, 0x30, &fc_lo);
uint32_t frame_count = ((uint32_t)fc_hi << 16) | fc_lo;
```

### 5.3 Continuous Scan Sequence

```c
// Set continuous scan mode and start
// CONTROL: scan_mode=01 (continuous), scan_enable=1
fpga_reg_write(spi, 0x21, 0x0009);

// Run until externally stopped
while (scan_running) {
    usleep(1000);  // 1 ms polling interval

    uint16_t status;
    fpga_reg_read(spi, 0x20, &status);

    if (status & 0x04) {  // error bit
        uint16_t error_flags;
        fpga_reg_read(spi, 0x80, &error_flags);
        handle_error(error_flags);
        fpga_reg_write(spi, 0x80, 0xFF);  // clear all errors
        break;
    }
}

// Stop scan
fpga_reg_write(spi, 0x21, 0x0002);  // scan_stop=1
```

### 5.4 Error Recovery Sequence

```c
// 1. Read status to confirm error
uint16_t status;
fpga_reg_read(spi, 0x20, &status);
assert(status & 0x04);  // error bit

// 2. Read error details
uint16_t error_flags;
fpga_reg_read(spi, 0x80, &error_flags);
log_error("FPGA error flags: 0x%04X", error_flags);

// 3. Clear all error flags
fpga_reg_write(spi, 0x80, 0x00FF);  // write-1-clear all bits

// 4. Wait for idle
do {
    fpga_reg_read(spi, 0x20, &status);
} while (!(status & 0x01));

// 5. Optionally retry scan
```

---

## 6. Timing Diagrams

### 6.1 Write Transaction Timing

```
         t_CSS                                    t_CSH
          |<-->|                                  |<-->|
CS_N  ____      ________________________________       ____
          |    |                                |    |
SCLK       __  _  _  _  _  _  _  _  _  _  _  _  __
         __  \/  \/  \/  \/  \/  \/  \/  \/  \/  \/__
         |<----------------32 clocks---------------->|
MOSI  ---| A7..A0 | R/W | D15..D8 | D7..D0          |---
          t_DS            t_DH
          |<->|           |<->|
```

Where:
- t_CSS = CS_N to first SCLK edge (min 20 ns)
- t_CSH = last SCLK edge to CS_N release (min 20 ns)
- t_DS = MOSI data setup before SCLK rising edge (min 2 ns)
- t_DH = MOSI data hold after SCLK rising edge (min 2 ns)

### 6.2 Read Transaction Timing

```
CS_N  ____      ________________________________       ____
          |    |                                |    |
SCLK       __  _  _  _  _  _  _  _  _  _  _  _  __
         __  \/  \/  \/  \/  \/  \/  \/  \/  \/  \/__

MOSI  ---| A7..A0 | 0x00 | 0x00     | 0x00          |---
                                |<----------------->|
MISO  ---( don't care )         | D15..D8 | D7..D0  |---
                                ^
                                FPGA drives MISO starting byte 2
```

---

## 7. Document Traceability

**Implements**: `docs/architecture/fpga-design.md` Section 6 (SPI Slave and Register Map)

**References**:
- SPEC-ARCH-001
- NXP i.MX8M Plus Reference Manual Section 67 (ECSPI)
- `docs/api/csi2-packet-format.md` (lane speed configuration)

**Feeds Into**:
- SoC firmware `spi_master.c` implementation
- FpgaSimulator SPI register model
- RTL testbench FV-01 (SPI register R/W)

---

## 8. Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (manager-docs) | Complete register map with all address blocks, C code examples, timing diagrams |

---

## Review Record

- Date: 2026-02-17
- Reviewer: manager-quality
- Status: Approved
- TRUST 5: T:5 R:5 U:5 S:5 T:5
