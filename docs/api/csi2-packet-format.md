# CSI-2 Packet Format API Reference

**Project**: X-ray Detector Panel System
**Interface**: FPGA CSI-2 MIPI TX (Artix-7 XC7A35T) to SoC CSI-2 RX (i.MX8M Plus)
**Version**: 1.0.0
**Last Updated**: 2026-02-17

---

## 1. Physical Layer (D-PHY)

### 1.1 Configuration

| Parameter | Value |
|-----------|-------|
| Lanes | 4 data lanes + 1 clock lane |
| Lane Speed | 1.0 - 1.25 Gbps/lane (Artix-7 OSERDES limit) |
| Aggregate Bandwidth | 4.0 - 5.0 Gbps |
| I/O Standard | LVDS_25 (2.5V LVDS differential) |
| Differential Swing | 200 mV typical |
| Serialization | 10:1 DDR (OSERDES2) |

### 1.2 D-PHY Operating Modes

| Mode | Clock Lane | Data Lanes | Purpose |
|------|-----------|------------|---------|
| LP (Low Power) | LP-00 | LP-00 | Idle, initialization, escape sequences |
| HS (High Speed) | Differential clock | Differential data | Pixel data transmission |

### 1.3 HS Entry/Exit Sequences

**HS Entry** (before each packet burst):
```
Data Lane:  LP-11 -> LP-01 -> LP-00 -> HS-0 (SoT) -> HS data...
Clock Lane: LP-11 -> LP-01 -> LP-00 -> HS continuous clock...
```

**HS Exit** (after each packet burst):
```
Data Lane:  ...HS data -> HS-Trail -> LP-11
Clock Lane: ...continuous clock (or LP-11 if gated)
```

---

## 2. Protocol Layer (CSI-2)

### 2.1 CSI-2 Specification

| Parameter | Value |
|-----------|-------|
| CSI-2 Version | v1.3 |
| Data Type | RAW16 (0x2C) or RAW14 (0x2B) |
| Virtual Channel | VC0 (0x00) |
| ECC | 1-byte Error Correction Code per short packet |
| CRC | CRC-16 per long packet payload |

### 2.2 Packet Types

| Type | Category | Data ID | Purpose |
|------|----------|---------|---------|
| Frame Start (FS) | Short Packet | 0x00 | Marks beginning of a frame |
| Frame End (FE) | Short Packet | 0x01 | Marks end of a frame |
| Line Start (LS) | Short Packet | 0x02 | Marks beginning of a line |
| Line End (LE) | Short Packet | 0x03 | Marks end of a line |
| RAW16 Data | Long Packet | 0x2C | Pixel data payload |

---

## 3. Packet Structures

### 3.1 Short Packet Format (4 bytes)

Used for Frame Start, Frame End, Line Start, Line End.

```
Byte 0            Byte 1           Byte 2           Byte 3
+--------+-------+----------------+----------------+--------+
| VC[1:0]| DT[5:0]| Data[15:8]    | Data[7:0]     | ECC    |
+--------+-------+----------------+----------------+--------+
  [7:6]    [5:0]     [15:8]          [7:0]           [7:0]
```

| Field | Bits | Value (FS) | Value (FE) | Description |
|-------|------|-----------|-----------|-------------|
| VC | [7:6] | 0b00 | 0b00 | Virtual Channel 0 |
| Data Type | [5:0] | 0x00 | 0x01 | Frame Start / End |
| Data | [15:0] | frame_number | frame_number | 16-bit frame counter |
| ECC | [7:0] | calculated | calculated | Hamming (6,4) + parity |

**Frame Start Packet Example** (frame #0):
```
Byte 0: 0x00 (VC=0, DT=0x00)
Byte 1: 0x00 (Frame number MSB)
Byte 2: 0x00 (Frame number LSB)
Byte 3: 0x0B (ECC)
```

### 3.2 Long Packet Format (Variable Length)

Used for RAW16 pixel data.

```
+------------------+------------------+---...---+----------+
| Packet Header    | Pixel Data       |         | CRC-16   |
| (4 bytes)        | (WC bytes)       |         | (2 bytes)|
+------------------+------------------+---...---+----------+
```

#### Packet Header (4 bytes)

```
Byte 0            Byte 1           Byte 2           Byte 3
+--------+-------+----------------+----------------+--------+
| VC[1:0]| DT[5:0]| WC[15:8]      | WC[7:0]       | ECC    |
+--------+-------+----------------+----------------+--------+
```

| Field | Bits | Value | Description |
|-------|------|-------|-------------|
| VC | [7:6] | 0b00 | Virtual Channel 0 |
| Data Type | [5:0] | 0x2C | RAW16 |
| WC (Word Count) | [15:0] | width x 2 | Payload size in bytes |
| ECC | [7:0] | calculated | Error Correction Code |

**Word Count Calculation**:
- 1024-pixel line: WC = 1024 x 2 = 2048 bytes
- 2048-pixel line: WC = 2048 x 2 = 4096 bytes
- 3072-pixel line: WC = 3072 x 2 = 6144 bytes

#### Pixel Data Payload

RAW16 pixels, little-endian byte order, left to right:

```
Pixel 0      Pixel 1      Pixel 2      ...     Pixel N-1
+----+----+ +----+----+ +----+----+          +----+----+
| L  | H  | | L  | H  | | L  | H  |    ...  | L  | H  |
+----+----+ +----+----+ +----+----+          +----+----+

L = Low byte (bits [7:0])
H = High byte (bits [15:8])
```

**Example**: Pixel value 0x1234
```
Byte N:   0x34 (Low byte)
Byte N+1: 0x12 (High byte)
```

#### CRC-16 (2 bytes)

- **Algorithm**: CRC-16/CCITT (polynomial 0x8408, reflected)
- **Initial Value**: 0xFFFF
- **Scope**: Calculated over pixel data payload only (not packet header)
- **Byte Order**: Little-endian (CRC_L first, CRC_H second)

```
CRC Input:  [Pixel 0 L][Pixel 0 H][Pixel 1 L][Pixel 1 H]...[Pixel N-1 H]
CRC Output: [CRC_L][CRC_H]
```

---

## 4. Frame Transmission Sequence

### 4.1 Complete Frame

```
Time -->

[D-PHY HS Entry]
  |-- Frame Start (FS): VC=0, DT=0x00, Data=frame_number
  |
  |-- [D-PHY HS Entry]
  |     |-- Line 0 Data: VC=0, DT=0x2C, WC=width*2, Pixels[0..width-1], CRC
  |-- [D-PHY HS Exit]
  |
  |-- [Line Blanking: 100 pixel clocks]
  |
  |-- [D-PHY HS Entry]
  |     |-- Line 1 Data: VC=0, DT=0x2C, WC=width*2, Pixels[0..width-1], CRC
  |-- [D-PHY HS Exit]
  |
  |   ... (repeat for all rows)
  |
  |-- [D-PHY HS Entry]
  |     |-- Line N-1 Data: VC=0, DT=0x2C, WC=width*2, Pixels[0..width-1], CRC
  |-- [D-PHY HS Exit]
  |
  |-- Frame End (FE): VC=0, DT=0x01, Data=frame_number
[D-PHY HS Exit]

[Frame Blanking: frame_blank_us microseconds]

[Next Frame...]
```

### 4.2 Timing Parameters

| Parameter | Formula | 2048x2048@30fps | 3072x3072@15fps |
|-----------|---------|-----------------|-----------------|
| Line data time | WC / (lane_speed x lanes / 8) | 4096 / 500 = 8.19 us | 6144 / 500 = 12.29 us |
| Line blanking | Configurable (100 pixel clocks) | ~0.8 us | ~0.8 us |
| Total line time | line_data + blanking | ~9.0 us | ~13.1 us |
| Frame data time | rows x total_line | 18.43 ms | 40.18 ms |
| Frame blanking | frame_blank_us | 0.5 ms | 0.5 ms |
| Frame period | 1 / fps | 33.33 ms | 66.67 ms |
| Frame utilization | frame_data / frame_period | 55% | 60% |

---

## 5. Data Integrity

### 5.1 ECC (Error Correction Code)

Short packet ECC provides:
- **Single-bit error correction** (automatic)
- **Multi-bit error detection** (report to host)

Algorithm: Modified Hamming code (SEC-DED)
- Input: 24 bits (VC + DT + Data)
- Output: 8-bit ECC

### 5.2 CRC-16

Long packet CRC provides:
- **Error detection** for payload data corruption
- **No error correction** (retransmission not available in streaming mode)

| Property | Value |
|----------|-------|
| Polynomial | 0x8408 (CRC-CCITT reflected) |
| Initial Value | 0xFFFF |
| Final XOR | 0x0000 |
| Bit Error Detection | Up to 3 bit errors guaranteed |

### 5.3 Verification

At the SoC receiver side:
1. Parse packet header, verify ECC
2. Extract pixel payload (WC bytes)
3. Calculate CRC-16 over payload
4. Compare calculated CRC with received CRC
5. If mismatch: increment TX_ERROR_COUNT, mark frame as suspect

---

## 6. Test Patterns

### 6.1 Counter Pattern (Mode 0x00)

```
Pixel[row][col] = (row * width + col) % 65536

Line 0:  0x0000, 0x0001, 0x0002, ..., 0x07FF (for 2048-width)
Line 1:  0x0800, 0x0801, 0x0802, ..., 0x0FFF
...
```

### 6.2 Checkerboard Pattern (Mode 0x01)

```
Pixel[row][col] = ((row + col) % 2 == 0) ? 0xFFFF : 0x0000

Line 0:  0xFFFF, 0x0000, 0xFFFF, 0x0000, ...
Line 1:  0x0000, 0xFFFF, 0x0000, 0xFFFF, ...
```

### 6.3 Constant Pattern (Mode 0x02)

```
All pixels = configured constant value (default: 0x8000)
```

---

## 7. Bandwidth Calculations

### 7.1 Raw Pixel Data Rate

```
Raw data rate = width x height x bit_depth x fps
```

| Tier | Calculation | Raw Rate |
|------|------------|----------|
| Minimum | 1024 x 1024 x 14 x 15 | 0.21 Gbps |
| Intermediate-A | 2048 x 2048 x 16 x 15 | 1.01 Gbps |
| Intermediate-B | 2048 x 2048 x 16 x 30 | 2.01 Gbps |
| Target | 3072 x 3072 x 16 x 15 | 2.26 Gbps |

### 7.2 CSI-2 Protocol Overhead

| Component | Bytes/Frame (2048x2048) | Overhead % |
|-----------|------------------------|-----------|
| Frame Start/End | 8 | < 0.01% |
| Packet Headers | rows x 4 = 8,192 | 0.10% |
| CRC-16 | rows x 2 = 4,096 | 0.05% |
| Line Blanking | rows x ~100 bytes | 2.4% |
| D-PHY HS Entry/Exit | rows x ~20 bytes | 0.5% |
| **Total Overhead** | | **~3%** |

### 7.3 Required CSI-2 Bandwidth

```
Required bandwidth = raw_rate x (1 + overhead)
```

| Tier | Raw Rate | With Overhead | CSI-2 Available (4-lane) | Feasible |
|------|----------|--------------|------------------------|----------|
| Minimum | 0.21 Gbps | 0.22 Gbps | 4.0 Gbps | Yes |
| Intermediate-A | 1.01 Gbps | 1.04 Gbps | 4.0 Gbps | Yes |
| Intermediate-B | 2.01 Gbps | 2.07 Gbps | 4.0 Gbps | Yes |
| Target | 2.26 Gbps | 2.33 Gbps | 4.0 Gbps | Yes |

---

## 8. Document Traceability

**Implements**: docs/architecture/fpga-design.md Section 5 (CSI-2 MIPI TX Subsystem)

**References**: MIPI Alliance CSI-2 v1.3 Specification, MIPI Alliance D-PHY v1.2 Specification

---
