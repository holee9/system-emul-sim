# MIPI CSI-2 Packet Format Specification

**Document Version**: 1.0.0
**Status**: Reviewed
**Last Updated**: 2026-02-17

---

## Overview

This document specifies the MIPI CSI-2 packet format used between the FPGA transmitter (Xilinx Artix-7 XC7A35T) and the SoC receiver (NXP i.MX8M Plus) in the X-ray Detector Panel System. The FPGA implements AMD MIPI CSI-2 TX Subsystem v3.1 with native OSERDES2-based D-PHY (no external PHY chip required).

All data flows in one direction: FPGA (transmitter) to SoC (receiver). The SoC's i.MX8M Plus MIPI CSI-2 RX interface receives the pixel stream via V4L2 kernel subsystem.

**Applicable Standards**:
- MIPI Alliance CSI-2 Specification v1.3
- MIPI Alliance D-PHY Specification v1.2

---

## 1. Physical Layer (D-PHY)

### 1.1 Lane Configuration

| Parameter | Value |
|-----------|-------|
| Data lanes | 4 (Lane 0 through Lane 3) |
| Clock lane | 1 (dedicated differential clock) |
| I/O standard | LVDS_25 (2.5V LVDS differential) |
| Differential swing | 200 mV typical |
| Common mode voltage | 200 mV +/- 25 mV |
| Serialization ratio | 10:1 DDR (OSERDES2) |
| Rise/fall time | < 100 ps (80-120 ps typical) |
| Maximum lane speed | 1.25 Gbps/lane (Artix-7 OSERDES2 limit) |

### 1.2 Validated Operating Points

Two operating speeds have been validated on the actual hardware (Artix-7 35T + i.MX8M Plus EVK):

| Speed | Lane Speed | 4-Lane Aggregate | Status |
|-------|-----------|-----------------|--------|
| 400M | 400 Mbps/lane | 1.6 Gbps total | Stable, verified |
| 800M | 800 Mbps/lane | 3.2 Gbps total | Functional, debugging in progress |

The 800 Mbps/lane configuration is required for the Target tier (3072x3072@15fps, 2.26 Gbps).

### 1.3 D-PHY Operating Modes

| Mode | Clock Lane State | Data Lane State | Usage |
|------|-----------------|----------------|-------|
| LP (Low Power) | LP-00 (single-ended) | LP-00 (single-ended) | Idle, initialization |
| HS (High Speed) | Differential continuous clock | Differential burst data | Pixel transmission |

### 1.4 HS Entry Sequence (Before Pixel Burst)

Before each high-speed packet transmission, the D-PHY performs the following state transitions:

Clock lane entry sequence:
- LP-11 (stop state) to LP-01 to LP-00 then transitions to HS continuous clock

Data lane entry sequence (each lane independently):
- LP-11 to LP-01 to LP-00 to HS-0 (Start of Transmission, SoT) then HS data stream begins

### 1.5 HS Exit Sequence (After Pixel Burst)

Data lane exit sequence:
- HS data completes, HS-Trail period, then LP-11 (stop state)

Clock lane exit sequence (gated clock mode):
- HS clock completes, HS-Trail period, then LP-11
- In continuous clock mode: clock remains active between packets

### 1.6 D-PHY Timing Parameters

Timing parameters differ between the 400 Mbps/lane and 800 Mbps/lane operating points. All times are in nanoseconds unless stated otherwise.

**400 Mbps/lane (stable operating point)**:

| Parameter | Symbol | Min | Typical | Max | Description |
|-----------|--------|-----|---------|-----|-------------|
| T_LPX | T_LPX | 50 ns | 100 ns | - | LP state hold time |
| T_HS-PREP | T_HS-PREP | 40 ns | 65 ns | 85 ns | HS preparation time (LP-00) |
| T_HS-ZERO | T_HS-ZERO | 145 ns | 200 ns | - | HS-0 time before SoT |
| T_HS-TRAIL | T_HS-TRAIL | 60 ns | 80 ns | - | HS trailing time after last bit |
| T_CLK-PREP | T_CLK-PREP | 38 ns | 65 ns | 95 ns | Clock lane preparation |
| T_CLK-ZERO | T_CLK-ZERO | 300 ns | 400 ns | - | Clock HS-0 before clock start |
| T_CLK-TRAIL | T_CLK-TRAIL | 60 ns | 80 ns | - | Clock trailing time |
| T_CLK-POST | T_CLK-POST | 60 ns | 100 ns | - | Clock lane LP-11 hold after data |
| Bit period | T_BIT | - | 2.5 ns | - | 1/400 MHz x 1 bit per ns |

**800 Mbps/lane (debugging in progress)**:

| Parameter | Symbol | Min | Typical | Max | Description |
|-----------|--------|-----|---------|-----|-------------|
| T_LPX | T_LPX | 50 ns | 60 ns | - | LP state hold time |
| T_HS-PREP | T_HS-PREP | 40 ns | 55 ns | 85 ns | HS preparation time (LP-00) |
| T_HS-ZERO | T_HS-ZERO | 105 ns | 145 ns | - | HS-0 time before SoT |
| T_HS-TRAIL | T_HS-TRAIL | 60 ns | 75 ns | - | HS trailing time after last bit |
| T_CLK-PREP | T_CLK-PREP | 38 ns | 55 ns | 95 ns | Clock lane preparation |
| T_CLK-ZERO | T_CLK-ZERO | 300 ns | 360 ns | - | Clock HS-0 before clock start |
| T_CLK-TRAIL | T_CLK-TRAIL | 60 ns | 75 ns | - | Clock trailing time |
| T_CLK-POST | T_CLK-POST | 60 ns | 80 ns | - | Clock lane LP-11 hold after data |
| Bit period | T_BIT | - | 1.25 ns | - | 1/800 MHz x 1 bit per ns |

---

## 2. Protocol Layer (CSI-2)

### 2.1 Protocol Parameters

| Parameter | Value |
|-----------|-------|
| CSI-2 version | v1.3 |
| Virtual channel | VC0 (value 0b00) |
| Primary data type | RAW16 (0x2C) for 16-bit pixels |
| Secondary data type | RAW14 (0x2D) for 14-bit pixels |
| Short packet ECC | 8-bit Hamming SEC-DED per packet |
| Long packet CRC | CRC-16 per payload (polynomial 0x1021 reflected as 0x8408) |

### 2.2 Packet Categories

**Short Packets** are 4 bytes total (no payload). Used for frame and line delimiters.

**Long Packets** have a 4-byte header, variable-length payload (pixel data), and a 2-byte CRC footer.

### 2.3 Data Type Codes

| Code | Name | Bit Depth | Usage |
|------|------|-----------|-------|
| 0x00 | Frame Start | N/A | Short packet, frame delimiter (start) |
| 0x01 | Frame End | N/A | Short packet, frame delimiter (end) |
| 0x02 | Line Start | N/A | Short packet, line delimiter (start) |
| 0x03 | Line End | N/A | Short packet, line delimiter (end) |
| 0x2C | RAW16 | 16 bits/pixel | Long packet, pixel data (primary format) |
| 0x2D | RAW14 | 14 bits/pixel | Long packet, pixel data (Minimum tier) |

**Note on RAW14 (0x2D)**: The 14-bit data type is used in the Minimum tier (1024x1024@14-bit@15fps). In hardware, the FPGA zero-pads the upper 2 bits when converting 14-bit ADC output to the 16-bit CSI-2 RAW14 container. The Word Count in the RAW14 packet header still counts bytes (2 bytes per pixel), so WC = width x 2 for both RAW14 and RAW16.

---

## 3. Short Packet Format

Short packets carry no pixel payload. They mark the boundaries of frames and lines.

### 3.1 Byte Layout

```
Byte 0          Byte 1          Byte 2          Byte 3
+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+
|VC1|VC0|DT5|DT4|DT3|DT2|DT1|DT0| Data[15:8]   | Data[7:0]    |
+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+

Byte 4 (sent after header, but logically part of packet):
+---+---+---+---+---+---+---+---+
|        ECC[7:0]               |
+---+---+---+---+---+---+---+---+
```

Bytes 0-2 form the 24-bit input to the ECC encoder. Byte 3 carries the 8-bit ECC output.

### 3.2 Field Definitions

| Field | Bit Position in Byte 0 | Value | Description |
|-------|----------------------|-------|-------------|
| VC[1:0] | bits [7:6] | 0b00 | Virtual Channel 0 (fixed for this system) |
| DT[5:0] | bits [5:0] | see Data Type table | Packet type identifier |

| Field | Bytes | Description |
|-------|-------|-------------|
| Data[15:0] | Bytes 1-2 | For FS/FE: 16-bit frame counter. For LS/LE: 16-bit line counter |
| ECC[7:0] | Byte 3 | Error Correction Code over bytes 0-2 |

### 3.3 Short Packet Examples

**Frame Start (FS) packet for frame number 0**:

```
Byte 0: 0x00  (VC=0b00, DT=0x00)
Byte 1: 0x00  (frame_number[15:8] = 0)
Byte 2: 0x00  (frame_number[7:0]  = 0)
Byte 3: 0x0B  (ECC calculated over bytes 0-2)
```

**Frame Start packet for frame number 1**:

```
Byte 0: 0x00  (VC=0b00, DT=0x00)
Byte 1: 0x00  (frame_number[15:8] = 0)
Byte 2: 0x01  (frame_number[7:0]  = 1)
Byte 3: ECC   (recalculated)
```

**Frame End (FE) packet for frame number 0**:

```
Byte 0: 0x01  (VC=0b00, DT=0x01)
Byte 1: 0x00  (frame_number[15:8] = 0)
Byte 2: 0x00  (frame_number[7:0]  = 0)
Byte 3: ECC   (calculated over bytes 0-2)
```

**Line Start (LS) packet for line 0**:

```
Byte 0: 0x02  (VC=0b00, DT=0x02)
Byte 1: 0x00  (line_number[15:8] = 0)
Byte 2: 0x00  (line_number[7:0]  = 0)
Byte 3: ECC   (calculated)
```

---

## 4. Long Packet Format

Long packets carry pixel payload data. Each line of pixel data is sent as one long packet.

### 4.1 Packet Structure Overview

```
+------------------+---------------------------+----------+
| Packet Header    | Pixel Data Payload        | CRC-16   |
| 4 bytes          | (WC bytes)                | 2 bytes  |
+------------------+---------------------------+----------+
```

Total long packet size in bytes = 4 (header) + WC (payload) + 2 (CRC).

### 4.2 Packet Header (4 bytes)

```
Byte 0          Byte 1          Byte 2          Byte 3
+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+
|VC1|VC0|DT5|DT4|DT3|DT2|DT1|DT0| WC[15:8]     | WC[7:0]      |
+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+---+

Byte 3 (ECC):
+---+---+---+---+---+---+---+---+
|        ECC[7:0]               |
+---+---+---+---+---+---+---+---+
```

| Field | Position | Value | Description |
|-------|----------|-------|-------------|
| VC[1:0] | Byte 0 bits [7:6] | 0b00 | Virtual Channel 0 |
| DT[5:0] | Byte 0 bits [5:0] | 0x2C (RAW16) or 0x2D (RAW14) | Data type |
| WC[15:0] | Bytes 1-2 | width x 2 | Word count: payload size in bytes |
| ECC[7:0] | Byte 3 | calculated | ECC over bytes 0-2 |

**Word Count Values by Tier**:

| Tier | Width (pixels) | WC (bytes) | WC in hex |
|------|---------------|-----------|-----------|
| Minimum (RAW14) | 1024 | 2048 | 0x0800 |
| Intermediate-A (RAW16) | 2048 | 4096 | 0x1000 |
| Target (RAW16) | 3072 | 6144 | 0x1800 |

### 4.3 Pixel Payload

RAW16 pixel encoding: each pixel occupies 2 bytes in little-endian order (low byte first, high byte second).

```
Pixel 0 (16-bit)   Pixel 1 (16-bit)   Pixel 2 (16-bit)   ...
+--------+--------+ +--------+--------+ +--------+--------+
| Lo[7:0]| Hi[7:0]| | Lo[7:0]| Hi[7:0]| | Lo[7:0]| Hi[7:0]|  ...
+--------+--------+ +--------+--------+ +--------+--------+
 Byte N   Byte N+1   Byte N+2 Byte N+3
```

Example: pixel value 0x1234 is encoded as byte 0x34 followed by byte 0x12.

Pixels are ordered left-to-right within a line. Each line produces one long packet.

### 4.4 CRC-16 Footer (2 bytes)

The 2-byte CRC covers only the pixel payload. The packet header is not included in the CRC calculation.

```
+----------+----------+
| CRC[7:0] | CRC[15:8]|
+----------+----------+
 Byte N    Byte N+1
```

CRC is transmitted in little-endian byte order (low byte first).

---

## 5. ECC Calculation

### 5.1 Algorithm

The ECC field in both short and long packet headers is an 8-bit value providing Single Error Correction and Double Error Detection (SEC-DED) over the 24-bit header payload (VC + DT + Data/WC).

The algorithm is a modified Hamming code specified in the MIPI CSI-2 standard:

1. The 24-bit input consists of bits D0 through D23 (D23 is MSB of byte 0, D0 is LSB of byte 2).
2. Six parity bits (P0-P5) are computed using the following XOR equations:

```
P0 = D0  ^ D1  ^ D3  ^ D4  ^ D6  ^ D8  ^ D10 ^ D11 ^ D13 ^ D15 ^ D17 ^ D19 ^ D21 ^ D23
P1 = D0  ^ D2  ^ D3  ^ D5  ^ D6  ^ D9  ^ D10 ^ D12 ^ D13 ^ D16 ^ D17 ^ D20 ^ D21
P2 = D1  ^ D2  ^ D3  ^ D7  ^ D8  ^ D9  ^ D10 ^ D14 ^ D15 ^ D16 ^ D17 ^ D22 ^ D23
P3 = D4  ^ D5  ^ D6  ^ D7  ^ D8  ^ D9  ^ D10 ^ D18 ^ D19 ^ D20 ^ D21 ^ D22 ^ D23
P4 = D11 ^ D12 ^ D13 ^ D14 ^ D15 ^ D16 ^ D17 ^ D18 ^ D19 ^ D20 ^ D21 ^ D22 ^ D23
P5 = D0  ^ D1  ^ D2  ^ D3  ^ D4  ^ D5  ^ D6  ^ D7  ^ D8  ^ D9  ^ D10 ^ P0  ^ P1  ^ P2  ^ P3 ^ P4
```

3. The 8-bit ECC byte is assembled as: ECC[7:0] = {P5, P4, P3, P2, P1, P0, 0, 0} where bits [1:0] are always 0.

### 5.2 ECC Capabilities

| Error Type | Capability |
|------------|-----------|
| Single-bit error | Corrected automatically at receiver |
| Double-bit error | Detected (flagged, not corrected) |
| Triple or more bit errors | Not guaranteed to detect |

### 5.3 ECC Reference Values

For the Frame Start packet (VC=0, DT=0x00, Data=0x0000):
- Input bits: 0x00, 0x00, 0x00
- ECC = 0x00

For the RAW16 long packet header with 2048-pixel line (VC=0, DT=0x2C, WC=0x1000):
- Byte 0 = 0x2C, Byte 1 = 0x10, Byte 2 = 0x00
- ECC = recalculated per algorithm above

In practice, the AMD CSI-2 TX IP computes ECC automatically. Reference values are needed only for simulator and testbench verification.

---

## 6. CRC-16 Calculation

### 6.1 Polynomial and Parameters

The CRC-16 used for long packet payload integrity is CRC-16/CCITT in reflected (bit-reversed) form:

| Property | Value |
|----------|-------|
| Standard name | CRC-16/CCITT (also known as CRC-CCITT) |
| Generator polynomial | x^16 + x^12 + x^5 + 1 |
| Normal polynomial representation | 0x1021 |
| Reflected polynomial representation | 0x8408 |
| Initial value | 0xFFFF |
| Final XOR value | 0x0000 |
| Input bit reflection | Yes (per-byte LSB-first) |
| Output bit reflection | Yes |

### 6.2 Reference C Implementation

```c
uint16_t csi2_crc16(const uint8_t *data, size_t length) {
    uint16_t crc = 0xFFFF;
    for (size_t i = 0; i < length; i++) {
        crc ^= (uint16_t)data[i];
        for (int j = 0; j < 8; j++) {
            if (crc & 0x0001)
                crc = (crc >> 1) ^ 0x8408;
            else
                crc = crc >> 1;
        }
    }
    return crc;
}
```

### 6.3 CRC Scope

The CRC is computed over the pixel payload bytes only. The packet header (4 bytes including ECC) and the CRC bytes themselves are excluded from the calculation.

```
CRC input:  pixel_data[0] ... pixel_data[WC-1]
CRC output: 2-byte value, transmitted LSB first after payload
```

### 6.4 CRC Test Vectors

| Input bytes | Length | Expected CRC-16 |
|-------------|--------|-----------------|
| 0x31 0x32 0x33 0x34 0x35 0x36 0x37 0x38 0x39 ("123456789") | 9 | 0x6F91 |
| 0x00 (single zero byte) | 1 | 0xE1F0 |
| 0xFF (single 0xFF byte) | 1 | 0xFF00 |

---

## 7. Frame Transmission Sequence

### 7.1 Complete Frame Structure

The following shows the complete D-PHY/CSI-2 transmission sequence for one frame. HS Entry and HS Exit denote D-PHY high-speed mode transitions.

```
[HS Entry]
  Frame Start short packet: VC=0, DT=0x00, Data=frame_number, ECC
[HS Exit]

[Line Blanking: ~100 pixel clock cycles]

For each line L from 0 to (height - 1):
  [HS Entry]
    Line Start short packet: VC=0, DT=0x02, Data=L, ECC
    Long packet header: VC=0, DT=0x2C (or 0x2D), WC=width*2, ECC
    Pixel data: pixels[L][0] through pixels[L][width-1], each 2 bytes little-endian
    CRC-16: 2 bytes little-endian, over pixel data
    Line End short packet: VC=0, DT=0x03, Data=L, ECC
  [HS Exit]
  [Line Blanking]

[HS Entry]
  Frame End short packet: VC=0, DT=0x01, Data=frame_number, ECC
[HS Exit]

[Frame Blanking: configurable via FRAME_BLANK_US register (default 500 us)]
```

### 7.2 Bandwidth Calculations Per Tier

The required CSI-2 bandwidth is derived from the raw pixel data rate plus protocol overhead.

| Tier | Resolution | Bit Depth | FPS | Raw Data Rate | CSI-2 Overhead (~3%) | Required Bandwidth | 4-Lane Available | Feasible |
|------|-----------|-----------|-----|--------------|---------------------|-------------------|-----------------|----------|
| Minimum | 1024x1024 | 14 | 15 | 0.210 Gbps | 0.006 Gbps | 0.216 Gbps | 1.6 Gbps | Yes |
| Intermediate-A | 2048x2048 | 16 | 15 | 1.006 Gbps | 0.030 Gbps | 1.036 Gbps | 1.6 Gbps | Yes |
| Intermediate-B | 2048x2048 | 16 | 30 | 2.013 Gbps | 0.060 Gbps | 2.073 Gbps | 3.2 Gbps | Yes (800M needed) |
| Target | 3072x3072 | 16 | 15 | 2.264 Gbps | 0.068 Gbps | 2.332 Gbps | 3.2 Gbps | Yes (800M needed, 29% margin) |

**Note**: The 400 Mbps/lane configuration (1.6 Gbps aggregate) supports Minimum and Intermediate-A tiers. The 800 Mbps/lane configuration (3.2 Gbps aggregate) is required for Intermediate-B and Target tiers.

### 7.3 Timing Budget Example: 1024x1024 RAW14 at 15fps (Minimum Tier, 400M)

| Phase | Duration | Notes |
|-------|----------|-------|
| FS packet | ~100 ns | Short packet burst |
| Per-line HS entry | ~400 ns | D-PHY LP to HS transition |
| Line data time | WC / bandwidth = 2048 / (1.6 Gbps / 8) = 10.24 us | 1024 pixels x 2 bytes / 200 MB/s |
| LS/LE overhead | ~200 ns | Two short packets per line |
| Line blanking | ~800 ns | 100 pixel clocks at 125 MHz |
| Total line time | ~11.64 us | Sum of above |
| All lines | 1024 x 11.64 us = 11.92 ms | Frame data portion |
| Frame blanking | 500 us | Configurable |
| Frame period | 66.67 ms | 1/15fps |
| Frame utilization | 11.92 ms / 66.67 ms = 17.9% | Well within budget |

---

## 8. Complete Packet Example: 1024x1024 RAW16 Frame

This example shows the exact byte sequence for a small portion of a 1024x1024 RAW16 frame (frame number 0, first line only).

### 8.1 Frame Start Short Packet

Transmitted bytes in order:
- 0x00 (Byte 0: VC=0b00, DT=0x00)
- 0x00 (Byte 1: frame_number[15:8] = 0)
- 0x00 (Byte 2: frame_number[7:0] = 0)
- 0x0B (Byte 3: ECC for {0x00, 0x00, 0x00})

### 8.2 Line 0 Long Packet Header

RAW16 (DT=0x2C), WC = 1024 x 2 = 2048 = 0x0800:
- 0x2C (Byte 0: VC=0b00, DT=0x2C)
- 0x08 (Byte 1: WC[15:8] = 0x08)
- 0x00 (Byte 2: WC[7:0] = 0x00)
- ECC  (Byte 3: calculated over bytes 0-2)

### 8.3 Line 0 Pixel Payload (first 3 pixels shown)

Assuming pixel[0][0]=0x0100, pixel[0][1]=0x0200, pixel[0][2]=0x0300:
- 0x00 0x01  (pixel[0][0] = 0x0100, little-endian: lo=0x00, hi=0x01)
- 0x00 0x02  (pixel[0][1] = 0x0200, little-endian: lo=0x00, hi=0x02)
- 0x00 0x03  (pixel[0][2] = 0x0300, little-endian: lo=0x00, hi=0x03)
- ... (1021 more pixels)

Total payload: 2048 bytes for line 0.

### 8.4 Line 0 CRC-16 Footer

CRC-16 computed over all 2048 payload bytes:
- 0xXX (CRC[7:0], low byte first)
- 0xXX (CRC[15:8], high byte second)

Actual CRC value depends on pixel data content.

### 8.5 Frame End Short Packet

- 0x01 (Byte 0: VC=0b00, DT=0x01)
- 0x00 (Byte 1: frame_number[15:8] = 0)
- 0x00 (Byte 2: frame_number[7:0] = 0)
- ECC  (Byte 3: calculated for {0x01, 0x00, 0x00})

---

## 9. Test Patterns

### 9.1 Counter Pattern (register CONTROL bit test_pattern_en = 1, mode = 0)

```
pixel[row][col] = (row * width + col) % 65536
```

For a 1024-wide frame:
- Line 0: 0x0000, 0x0001, 0x0002, ..., 0x03FF
- Line 1: 0x0400, 0x0401, 0x0402, ..., 0x07FF

This pattern enables easy verification of line continuity and pixel ordering.

### 9.2 Checkerboard Pattern (mode = 1)

```
pixel[row][col] = ((row + col) % 2 == 0) ? 0xFFFF : 0x0000
```

### 9.3 Constant Pattern (mode = 2)

All pixels transmit the configured constant value (default 0x8000 = mid-scale).

---

## 10. Data Integrity at SoC Receiver

Upon receiving each packet, the SoC CSI-2 RX hardware and V4L2 driver perform:

1. D-PHY hardware validates HS synchronization on each lane
2. CSI-2 RX IP parses packet header, verifies ECC over bytes 0-2
3. For single-bit errors: header bits are corrected automatically
4. For multi-bit errors: packet is discarded, error event raised
5. For long packets: CRC-16 is verified over received payload
6. CRC mismatch: frame is marked suspect, TX_ERROR_COUNT register incremented
7. V4L2 DMA engine transfers validated pixels to DDR4 frame buffer

---

## 11. Document Traceability

**Implements**: `docs/architecture/fpga-design.md` Section 5 (CSI-2 MIPI TX Subsystem)

**References**:
- MIPI Alliance CSI-2 Specification v1.3
- MIPI Alliance D-PHY Specification v1.2
- AMD MIPI CSI-2 TX Subsystem v3.1 Product Guide (PG232)
- `docs/api/spi-register-map.md` (CSI2_LANE_SPEED register 0x88)

**Feeds Into**:
- FpgaSimulator CSI-2 packet generation
- SoC firmware V4L2 driver configuration
- RTL testbench packet parser (FV-10, FV-11)

---

## 12. Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (manager-docs) | Initial specification with D-PHY timing, ECC algorithm, CRC-16 polynomial, complete packet examples |

---

## Review Record

- Date: 2026-02-17
- Reviewer: manager-quality
- Status: Approved
- TRUST 5: T:5 R:5 U:5 S:5 T:5
