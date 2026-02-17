# 10 GbE Ethernet Protocol API Reference

**Project**: X-ray Detector Panel System
**Interface**: SoC Controller (i.MX8M Plus) to Host PC
**Version**: 1.0.0
**Last Updated**: 2026-02-17

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

Every UDP packet carrying pixel data begins with a 32-byte frame header.

```c
// 32 bytes, packed, little-endian
struct __attribute__((packed)) FrameHeader {
    uint32_t magic;           // Offset 0:  0xDEADBEEF
    uint32_t frame_seq;       // Offset 4:  Frame sequence number
    uint64_t timestamp_us;    // Offset 8:  Microsecond timestamp
    uint16_t width;           // Offset 16: Image width (pixels)
    uint16_t height;          // Offset 18: Image height (pixels)
    uint16_t bit_depth;       // Offset 20: 14 or 16
    uint16_t packet_index;    // Offset 22: Packet index within frame
    uint16_t total_packets;   // Offset 24: Total packets per frame
    uint16_t flags;           // Offset 26: Status flags
    uint16_t crc16;           // Offset 28: CRC-16 over header bytes 0-27
    uint16_t reserved;        // Offset 30: Reserved (padding to 32 bytes)
};
```

### 2.2 Field Descriptions

| Field | Type | Description |
|-------|------|-------------|
| `magic` | uint32 | Synchronization marker. Always 0xDEADBEEF. Used to identify valid frame packets. |
| `frame_seq` | uint32 | Monotonically increasing frame counter (0-based). Wraps at 2^32. |
| `timestamp_us` | uint64 | SoC system clock timestamp in microseconds. Epoch: SoC boot time. |
| `width` | uint16 | Image width in pixels (1024, 2048, or 3072). |
| `height` | uint16 | Image height in pixels (1024, 2048, or 3072). |
| `bit_depth` | uint16 | Pixel bit depth (14 or 16). |
| `packet_index` | uint16 | Zero-based packet index within this frame (0 to total_packets-1). |
| `total_packets` | uint16 | Total number of packets comprising this frame. |
| `flags` | uint16 | Status flags (see table below). |
| `crc16` | uint16 | CRC-16/CCITT over header bytes 0-27 (excluding crc16 and reserved). |
| `reserved` | uint16 | Reserved for future use. Set to 0. |

### 2.3 Flags Field

| Bit | Name | Description |
|-----|------|-------------|
| [0] | last_packet | 1 = This is the final packet of the frame |
| [1] | error_frame | 1 = Frame may contain errors (FPGA reported error during capture) |
| [2] | calibration | 1 = Calibration frame (dark frame, gate OFF) |
| [15:3] | reserved | Reserved, set to 0 |

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
  1. Parse FrameHeader, validate magic and CRC
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

## 5. Discovery Protocol (Port 8002)

### 5.1 Discovery Request (Broadcast)

```c
struct __attribute__((packed)) DiscoveryRequest {
    uint32_t magic;           // 0xD15C0000
    uint8_t  version;         // Protocol version (1)
    uint8_t  reserved[3];     // Padding
};
```

**Sent to**: UDP broadcast 255.255.255.255:8002

### 5.2 Discovery Response (Unicast)

```c
struct __attribute__((packed)) DiscoveryResponse {
    uint32_t magic;           // 0xD15C0001
    uint8_t  version;         // Protocol version (1)
    char     device_id[16];   // Null-terminated device identifier
    char     firmware_ver[8]; // Null-terminated firmware version
    uint8_t  max_tier;        // Maximum supported tier (0-3)
    uint32_t ip_addr;         // SoC IP address (network byte order)
    uint16_t data_port;       // Frame data port (default 8000)
    uint16_t command_port;    // Command port (default 8001)
};
```

**Sent to**: Unicast to requester's IP address, port 8002

---

## 6. CRC-16 Specification

### 6.1 Algorithm

| Property | Value |
|----------|-------|
| Name | CRC-16/CCITT |
| Polynomial | 0x1021 (normal) / 0x8408 (reflected) |
| Initial Value | 0xFFFF |
| Final XOR | 0x0000 |
| Reflect Input | Yes |
| Reflect Output | Yes |

### 6.2 Reference Implementation

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

### 6.3 Test Vectors

| Input (ASCII) | Length | CRC-16 |
|--------------|-------|--------|
| "123456789" | 9 | 0x6F91 |
| "" (empty) | 0 | 0xFFFF |
| "\x00" | 1 | 0xE1F0 |
| "\xFF" | 1 | 0xFF00 |

---

## 7. Error Handling

### 7.1 Packet-Level Errors

| Error | Detection | Response |
|-------|-----------|----------|
| Invalid magic | magic != 0xDEADBEEF | Discard packet |
| CRC mismatch | Calculated CRC != header CRC | Discard packet, log warning |
| Out-of-range index | packet_index >= total_packets | Discard packet, log warning |
| Duplicate packet | Already received this index | Ignore (no action) |

### 7.2 Frame-Level Errors

| Error | Detection | Response |
|-------|-----------|----------|
| Missing packets | Timeout + incomplete bitmap | Zero-fill or drop frame |
| Sequence gap | frame_seq not monotonic | Log warning, continue |
| Size mismatch | Reassembled size != expected | Drop frame, log error |

---

## 8. Document Traceability

**Implements**: docs/architecture/soc-firmware-design.md Section 6 (Network Protocol)

**References**: docs/architecture/host-sdk-design.md Section 4-5 (Network Communication, Frame Reassembly)

---
