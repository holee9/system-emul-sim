# 10 GbE UDP Frame Protocol Specification

**Document Version**: 1.0.0
**Status**: Reviewed - Approved
**Last Updated**: 2026-02-17

---

## Overview

This document specifies the UDP-based frame streaming protocol used between the SoC Controller (NXP i.MX8M Plus, Variscite VAR-SOM-MX8M-PLUS) and the Host PC (.NET 8.0 SDK). The SoC transmits assembled pixel frames as UDP datagrams over 10 GbE. The Host PC receives, reassembles, and processes the frames.

All pixel data flows in one direction: SoC (transmitter, source port dynamic) to Host (receiver, destination port 8000). Control commands flow in reverse on a separate port (8001).

---

## 1. Network Configuration

### 1.1 Physical Layer

| Parameter | Value |
|-----------|-------|
| Interface | 10GBASE-T (twisted pair) or 10GBASE-SR (fiber) |
| Connection | PCIe add-on NIC on SoC, native or PCIe on Host |
| Cable | Cat6a (up to 100m for 10GBASE-T) |
| Fallback | 1 GbE (Minimum tier only) |

### 1.2 Network Parameters

| Parameter | Default | Configurable | Description |
|-----------|---------|-------------|-------------|
| SoC IP | 192.168.1.100 | Yes | SoC controller IP address |
| Host IP | 192.168.1.1 | Yes | Host PC IP address |
| Data Port | 8000 | Yes | Frame data UDP port |
| Command Port | 8001 | Yes | Control command UDP port |
| Discovery Port | 8002 | No | Device discovery broadcast port |
| MTU | 9000 | Yes | Jumbo frame support recommended |
| UDP Payload | 8192 | Yes | Maximum pixel data per packet |

### 1.3 Bandwidth Budget

| Tier | Frame Data Rate | UDP Overhead | Total Rate | 10 GbE Utilization |
|------|----------------|-------------|-----------|-------------------|
| Minimum | 30 MB/s | 0.4% | 30.1 MB/s | 2.4% |
| Intermediate-A | 120 MB/s | 0.4% | 120.5 MB/s | 9.6% |
| Target | 270 MB/s | 0.4% | 271 MB/s | 21.7% |

---

## 2. Frame Data Protocol (Port 8000)

### 2.1 Frame Header Structure

Every UDP packet carrying pixel data begins with a 32-byte frame header in little-endian byte order.

```c
// 32 bytes, packed, little-endian
struct __attribute__((packed)) FrameHeader {
    uint32_t magic;           // Offset  0: 0xD7E01234 (synchronization marker)
    uint8_t  version;         // Offset  4: Protocol version (0x01)
    uint8_t  reserved0[3];    // Offset  5: Reserved (must be 0x00)
    uint32_t frame_id;        // Offset  8: Monotonic frame counter (0-based)
    uint16_t packet_seq;      // Offset 12: Packet index within frame (0-based)
    uint16_t total_packets;   // Offset 14: Total packets per frame
    uint64_t timestamp_ns;    // Offset 16: Nanoseconds since SoC boot epoch
    uint16_t rows;            // Offset 24: Image height in pixels
    uint16_t cols;            // Offset 26: Image width in pixels
    uint16_t crc16;           // Offset 28: CRC-16/CCITT over bytes 0-27 (all preceding fields)
    uint8_t  bit_depth;       // Offset 30: Pixel bit depth (14 or 16)
    uint8_t  flags;           // Offset 31: Status flags
};
```

**Total header size**: 32 bytes. No padding is added by the compiler due to explicit `packed` attribute and natural alignment.

### 2.2 Field Descriptions

| Field | Offset | Type | Description |
|-------|--------|------|-------------|
| magic | 0 | uint32 | Synchronization marker. Always 0xD7E01234. Used to identify valid frame packets and discard non-protocol UDP traffic. |
| version | 4 | uint8 | Protocol version. Currently 0x01. Receivers must check this before parsing remaining fields. |
| reserved0 | 5 | uint8[3] | Reserved, set to 0x00. Future use. |
| frame_id | 8 | uint32 | Monotonically increasing frame identifier, starting at 0. Wraps at 2^32-1. Each unique frame has a distinct frame_id. |
| packet_seq | 12 | uint16 | Zero-based index of this packet within the frame (0 to total_packets-1). Used for reassembly ordering. |
| total_packets | 14 | uint16 | Total number of UDP packets that comprise this frame. All packets for a given frame_id have the same total_packets value. |
| timestamp_ns | 16 | uint64 | SoC CLOCK_MONOTONIC timestamp in nanoseconds at frame capture start. Epoch is SoC boot time (not Unix epoch). |
| rows | 24 | uint16 | Frame height in pixels (1024, 2048, or 3072). |
| cols | 26 | uint16 | Frame width in pixels (1024, 2048, or 3072). |
| crc16 | 28 | uint16 | CRC-16/CCITT integrity check over header bytes 0-27 (all fields preceding this one). Receiver must verify before processing. See Section 7 for algorithm details. |
| bit_depth | 30 | uint8 | Pixel bit depth: 14 or 16. |
| flags | 31 | uint8 | Status flags (see Flags table). |

### 2.3 Flags Field (uint8, offset 31)

| Bit | Name | Description |
|-----|------|-------------|
| [0] | last_packet | 1 = This is the final packet of the frame |
| [1] | error_frame | 1 = Frame may contain errors (FPGA reported error during capture) |
| [2] | calibration | 1 = Calibration frame (dark frame, gate OFF during exposure) |
| [7:3] | reserved | Reserved, set to 0 |

**Flag combinations**:
- 0x00: Normal data frame, more packets follow
- 0x01: Normal data frame, last packet
- 0x03: Error frame, last packet (receiver may choose to discard)
- 0x05: Calibration frame, last packet

### 2.4 UDP Packet Layout

```
+------------------------------------------------------------------+
| Ethernet Header (14 bytes, added by NIC)                          |
+------------------------------------------------------------------+
| IP Header (20 bytes)                                              |
+------------------------------------------------------------------+
| UDP Header (8 bytes)                                              |
+------------------------------------------------------------------+
| Frame Header (32 bytes)                                           |
+------------------------------------------------------------------+
| Pixel Data Payload (up to 8192 bytes)                             |
| [Pixel 0 Lo] [Pixel 0 Hi] [Pixel 1 Lo] [Pixel 1 Hi] ...         |
+------------------------------------------------------------------+
```

**Total UDP Datagram Size**: 32 (header) + payload_size (up to 8192) = up to 8224 bytes

### 2.5 Pixel Data Layout

Pixels are packed as 16-bit unsigned integers in **little-endian** byte order, scanning left-to-right, top-to-bottom:

```
Packet 0: pixels[0] through pixels[4095]
  Byte 0: pixel[0] low byte
  Byte 1: pixel[0] high byte
  Byte 2: pixel[1] low byte
  Byte 3: pixel[1] high byte
  ...
  Byte 8190: pixel[4095] low byte
  Byte 8191: pixel[4095] high byte

Packet 1: pixels[4096] through pixels[8191]
  ...

Last Packet: remaining pixels (may be less than 8192 bytes)
```

### 2.6 Fragmentation Examples

| Tier | Frame Size | Payload/Packet | Packets/Frame | Last Packet Size |
|------|-----------|---------------|---------------|-----------------|
| Minimum (1024x1024) | 2,097,152 B | 8,192 B | 256 | 8,192 B |
| Intermediate-A (2048x2048) | 8,388,608 B | 8,192 B | 1,024 | 8,192 B |
| Target (3072x3072) | 18,874,368 B | 8,192 B | 2,304 | 2,048 B |

---

## 3. Frame Reassembly Algorithm

### 3.1 Overview

The Host SDK reassembles frames from out-of-order UDP packets:

```
Input:  Unordered UDP packets with FrameHeader
Output: Complete Frame (width x height x 2 bytes)

Algorithm:
  1. Parse FrameHeader, validate magic and CRC-16 (bytes 0-27)
  2. Look up or create reassembly slot for frame_seq
  3. Copy payload to correct offset: packet_index * max_payload
  4. Mark packet as received in bitmap
  5. When bitmap is full (all packets received): emit frame
  6. On timeout (2s): zero-fill missing regions or drop frame
```

### 3.2 Offset Calculation

```
pixel_offset = packet_index * (max_payload / bytes_per_pixel)
byte_offset  = packet_index * max_payload
payload_size = min(max_payload, frame_size - byte_offset)
```

### 3.3 Completeness Check

```
frame_complete = (received_packet_count == total_packets)

// Alternative: bitmap check
for (int i = 0; i < total_packets; i++) {
    if (!bitmap[i]) return false;
}
return true;
```

### 3.4 Missing Packet Recovery

| Strategy | Condition | Action |
|----------|-----------|--------|
| Wait | timeout not expired | Continue receiving packets |
| Zero-fill | timeout expired, < 10% missing | Fill missing regions with 0x0000 |
| Drop | timeout expired, >= 10% missing | Discard incomplete frame |

---

## 4. Control Protocol (Port 8001)

### 4.1 Command Packet

Host sends commands to SoC:

```c
struct __attribute__((packed)) CommandPacket {
    uint32_t magic;           // 0xBEEFCAFE
    uint16_t command_id;      // Command type (see table)
    uint16_t sequence;        // Command sequence number (for correlation)
    uint16_t payload_length;  // Payload size in bytes (0-256)
    uint8_t  payload[256];    // Command-specific data
    uint16_t crc16;           // CRC-16 over bytes 0 to (10 + payload_length - 1)
};
```

### 4.2 Response Packet

SoC responds to each command:

```c
struct __attribute__((packed)) ResponsePacket {
    uint32_t magic;           // 0xCAFEBEEF
    uint16_t command_id;      // Echoed command ID
    uint16_t sequence;        // Echoed command sequence
    uint16_t status;          // 0=OK, 1=ERROR, 2=BUSY, 3=INVALID
    uint16_t payload_length;  // Response data size (0-256)
    uint8_t  payload[256];    // Response data
    uint16_t crc16;           // CRC-16
};
```

### 4.3 Command Reference

| ID | Command | Payload | Response Payload | Description |
|----|---------|---------|-----------------|-------------|
| 0x0001 | START_SCAN | `{mode: u8, tier: u8}` | `{status: u8}` | Begin frame acquisition |
| 0x0002 | STOP_SCAN | (none) | `{frames_captured: u32}` | Stop acquisition |
| 0x0003 | GET_STATUS | (none) | `{StatusReport}` | Get current system status |
| 0x0004 | SET_CONFIG | `{key: str, value: str}` | `{status: u8}` | Update runtime config |
| 0x0005 | RESET | (none) | `{status: u8}` | Soft reset SoC + FPGA |
| 0x0006 | GET_DEVICE_INFO | (none) | `{DeviceInfo}` | Get device identification |
| 0x0007 | PING | `{echo: u32}` | `{echo: u32}` | Connection keep-alive |

### 4.4 StatusReport Structure

```c
struct __attribute__((packed)) StatusReport {
    uint8_t  is_scanning;       // 1 = scan active
    uint8_t  scan_mode;         // 0=single, 1=continuous, 2=calibration
    uint8_t  active_tier;       // 0=min, 1=intA, 2=intB, 3=target
    uint8_t  fpga_state;        // FSM state (0-5)
    uint32_t frame_count;       // Total frames captured
    uint32_t dropped_frames;    // Frames dropped due to buffer overrun
    uint32_t error_count;       // Total errors detected
    uint16_t fpga_error_flags;  // Current FPGA ERROR_FLAGS register
    uint16_t temperature_c;     // SoC temperature (degrees C x 10)
    uint64_t uptime_sec;        // SoC uptime in seconds
};
```

### 4.5 Scan Mode Values

| Value | Mode | Description |
|-------|------|-------------|
| 0 | Single | Capture one frame, return to idle |
| 1 | Continuous | Capture frames continuously until STOP_SCAN |
| 2 | Calibration | Dark frame (gate OFF during exposure) |

### 4.6 Tier Values

| Value | Tier | Resolution | FPS |
|-------|------|-----------|-----|
| 0 | Minimum | 1024x1024 @ 14-bit | 15 |
| 1 | Intermediate-A | 2048x2048 @ 16-bit | 15 |
| 2 | Intermediate-B | 2048x2048 @ 16-bit | 30 |
| 3 | Target | 3072x3072 @ 16-bit | 15 |

---

## 5. Discovery Protocol (UDP Broadcast Port 8002)

### 5.1 Discovery Overview

The Host SDK discovers detector devices on the local network using UDP broadcast. The discovery protocol uses JSON payloads for human readability and extensibility.

Discovery request is sent as a UDP broadcast to 255.255.255.255 on port 8002. Each SoC on the network responds with a unicast reply to the requester's IP address on port 8002.

### 5.2 Discovery Request (Broadcast)

The Host PC broadcasts a JSON discovery request:

```json
{
  "magic": "D7E0DISC",
  "version": 1,
  "host_ip": "192.168.1.1",
  "request_id": 12345
}
```

**Sent to**: UDP broadcast 255.255.255.255:8002

**Field descriptions**:
- magic: Fixed string "D7E0DISC" to identify discovery packets
- version: Discovery protocol version (currently 1)
- host_ip: IP address of the Host PC (used by SoC to send unicast reply)
- request_id: Random 32-bit value echoed in response for correlation

### 5.3 Discovery Response (Unicast)

The SoC responds with a JSON unicast reply:

```json
{
  "magic": "D7E0RESP",
  "version": 1,
  "request_id": 12345,
  "device_id": "XR-DET-001",
  "firmware_version": "1.0.0",
  "max_tier": "target",
  "ip_address": "192.168.1.100",
  "data_port": 8000,
  "command_port": 8001,
  "max_width": 3072,
  "max_height": 3072,
  "max_bit_depth": 16
}
```

**Sent to**: Unicast to host_ip:8002

**Tier values in max_tier**:
- "minimum": 1024x1024@14-bit@15fps
- "intermediate_a": 2048x2048@16-bit@15fps
- "intermediate_b": 2048x2048@16-bit@30fps
- "target": 3072x3072@16-bit@15fps

---

## 6. Wireshark Analysis

### 6.1 Capture Filter Expressions

Use these Wireshark capture filters to isolate X-ray detector traffic:

**Capture all detector protocol traffic (data + control + discovery)**:
```
udp and (port 8000 or port 8001)
```

**Capture frame data only**:
```
udp and dst port 8000
```

**Capture from specific SoC IP**:
```
udp and src host 192.168.1.100
```

**Capture all detector traffic from a specific SoC**:
```
host 192.168.1.100 and udp and (port 8000 or port 8001)
```

### 6.2 Display Filter Expressions

Use these Wireshark display filters after capture:

**Show only frame data packets (destination port 8000)**:
```
udp.dstport == 8000
```

**Show only control commands (destination port 8001)**:
```
udp.dstport == 8001
```

**Filter by magic number in payload (frame data, 0xD7E01234 little-endian = 34 12 E0 D7)**:
```
udp.payload[0:4] == 34:12:e0:d7
```

**Show packets for a specific frame_id (frame_id is at offset 8, 4 bytes)**:
```
udp.payload[8:4] == 00:00:00:01
```

**Show only last packets (flags byte at offset 31, bit 0 set)**:
```
(udp.payload[31] & 0x01) == 0x01
```

### 6.3 Bandwidth Verification

When capturing with Wireshark, the "Statistics > IO Graphs" feature can verify actual bandwidth:

| Tier | Expected Peak Rate | Expected Packet Rate |
|------|-------------------|---------------------|
| Minimum (1024x1024@15fps) | ~30 MB/s | ~3,840 packets/s |
| Intermediate-A (2048x2048@15fps) | ~122 MB/s | ~15,360 packets/s |
| Target (3072x3072@15fps) | ~273 MB/s | ~34,560 packets/s |

**Note**: Wireshark capture on the Host PC will reduce available NIC throughput. For Target tier analysis, use a network TAP or dedicated capture machine.

---

## 7. CRC-16 Specification

This section defines the CRC-16/CCITT algorithm used in two places:
1. **Frame Header** (`FrameHeader.crc16`, offset 28): covers header bytes 0-27 (all fields preceding `crc16`)
2. **Control Protocol** (`CommandPacket.crc16`, `ResponsePacket.crc16`): covers the respective command/response bytes

Both uses share the same algorithm and parameters below.

### 7.1 Algorithm

| Property | Value |
|----------|-------|
| Name | CRC-16/CCITT |
| Polynomial | 0x1021 (normal) / 0x8408 (reflected) |
| Initial Value | 0xFFFF |
| Final XOR | 0x0000 |
| Reflect Input | Yes |
| Reflect Output | Yes |

### 7.2 Frame Header CRC Scope

The `FrameHeader.crc16` field (offset 28, 2 bytes) is computed over the first 28 bytes of the frame header:

| Bytes | Fields Covered |
|-------|----------------|
| 0-3 | magic |
| 4 | version |
| 5-7 | reserved0 |
| 8-11 | frame_id |
| 12-13 | packet_seq |
| 14-15 | total_packets |
| 16-23 | timestamp_ns |
| 24-25 | rows |
| 26-27 | cols |

The `crc16` field itself (bytes 28-29) and subsequent fields (`bit_depth`, `flags`) are **not** included in the CRC calculation. The receiver computes CRC over bytes 0-27 and compares against `header.crc16`. On mismatch, the packet must be discarded (see Section 8.1).

**SoC transmitter pseudocode**:
```c
void frame_header_set_crc(FrameHeader *hdr) {
    hdr->crc16 = crc16_calculate((const uint8_t *)hdr, 28);
}
```

**Host receiver pseudocode**:
```c
bool frame_header_verify_crc(const FrameHeader *hdr) {
    uint16_t expected = crc16_calculate((const uint8_t *)hdr, 28);
    return expected == hdr->crc16;
}
```

### 7.3 Reference Implementation

```c
uint16_t crc16_calculate(const uint8_t *data, size_t length) {
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

### 7.4 Test Vectors

| Input (ASCII) | Length | CRC-16 |
|--------------|-------|--------|
| "123456789" | 9 | 0x6F91 |
| "" (empty) | 0 | 0xFFFF |
| "\x00" | 1 | 0xE1F0 |
| "\xFF" | 1 | 0xFF00 |

---

## 8. Error Handling

### 8.1 Packet-Level Errors

| Error | Detection | Response |
|-------|-----------|----------|
| Invalid magic | magic != 0xD7E01234 | Discard packet |
| CRC mismatch | Calculated CRC != header CRC | Discard packet, log warning |
| Out-of-range index | packet_index >= total_packets | Discard packet, log warning |
| Duplicate packet | Already received this index | Ignore (no action) |

### 8.2 Frame-Level Errors

| Error | Detection | Response |
|-------|-----------|----------|
| Missing packets | Timeout + incomplete bitmap | Zero-fill or drop frame |
| Sequence gap | frame_seq not monotonic | Log warning, continue |
| Size mismatch | Reassembled size != expected | Drop frame, log error |

---

## 9. Document Traceability

**Implements**: docs/architecture/soc-firmware-design.md Section 6 (Network Protocol)

**References**: docs/architecture/host-sdk-design.md Section 4-5 (Network Communication, Frame Reassembly)

---

## 10. Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (manager-docs) | Initial specification with UDP frame protocol, control protocol, discovery protocol, CRC-16, Wireshark filters |
| 1.0.1 | 2026-02-17 | manager-quality | Fixed: Section 7 heading numbering (6.x -> 7.x), invalid magic value in Section 8.1 (0xDEADBEEF -> 0xD7E01234), Discovery Protocol port (8001 -> 8002) |
| 1.0.2 | 2026-02-17 | manager-quality | Fixed CRIT-007: Added CRC-16 field to FrameHeader at offset 28; shifted bit_depth to offset 30, flags to offset 31; removed reserved1[2]; added Section 7.2 (frame header CRC scope and pseudocode); updated Wireshark flags filter offset (29 -> 31) |

---

## Review Record

- Date: 2026-02-17
- Reviewer: manager-quality
- Status: Approved (with corrections applied)
- TRUST 5: T:5 R:4 U:4 S:5 T:4
