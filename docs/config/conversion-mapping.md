# Configuration Conversion Mapping

**Project**: X-ray Detector Panel System
**Source**: `config/detector_config.yaml` (Single Source of Truth)
**Schema**: `config/schema/detector-config-schema.json`
**Last Updated**: 2026-02-17

---

## Overview

All target-specific configuration files are generated from `detector_config.yaml`. This document defines the mapping rules for each conversion target.

---

## 1. YAML to FPGA Constraints (.xdc)

**Tool**: `ConfigConverter` (C# .NET 8.0+)
**Output**: `fpga/constraints/detector_generated.xdc`

### Mapping Rules

| YAML Path | XDC Output | Notes |
|-----------|-----------|-------|
| `fpga.data_interface.csi2.lane_count` | `# D-PHY lane configuration: N lanes` | Comment header |
| `fpga.data_interface.csi2.lane_speed_mbps` | Clock constraint for byte clock | `lane_speed / 8` = byte clock freq |
| `fpga.spi.clock_hz` | `create_clock -period T [get_ports spi_sclk]` | T = 1e9 / clock_hz (ns) |
| `panel.rows` | Parameter propagation to RTL generics | Via `defparam` or Verilog `parameter` |
| `panel.cols` | Parameter propagation to RTL generics | Line buffer depth sizing |

### Generated XDC Template

```tcl
# Auto-generated from detector_config.yaml
# DO NOT EDIT MANUALLY - use ConfigConverter tool

# System clock (100 MHz input)
create_clock -period 10.0 [get_ports clk_100mhz]

# SPI clock constraint
# Source: fpga.spi.clock_hz = {spi_clock_hz}
create_clock -period {1e9/spi_clock_hz} [get_ports spi_sclk]

# CSI-2 byte clock constraint
# Source: fpga.data_interface.csi2.lane_speed_mbps = {lane_speed}
# Byte clock = lane_speed / 8
create_clock -period {8e3/lane_speed} [get_ports csi2_byte_clk]

# D-PHY lane assignments
# Source: fpga.data_interface.csi2.lane_count = {lane_count}
# Pin assignments are board-specific (manual in board.xdc)
```

### Validation Rules

- `fpga.spi.clock_hz` <= 50 MHz (SPI specification limit)
- `fpga.data_interface.csi2.lane_speed_mbps` in [400, 1250] (Artix-7 OSERDES limit)
- `panel.cols` * `panel.bit_depth` / 8 <= BRAM width (line buffer sizing)

---

## 2. YAML to SoC Device Tree (.dts)

**Tool**: `ConfigConverter` (C# .NET 8.0+)
**Output**: `fw/dts/detector-overlay.dts`

### Mapping Rules

| YAML Path | DTS Node | Property |
|-----------|----------|----------|
| `controller.platform` | Root compatible string | `compatible = "moai,{platform}-detector"` |
| `fpga.data_interface.csi2.lane_count` | `&mipi_csi_0 / port@0 / endpoint` | `data-lanes = <1 2 ... N>` |
| `fpga.data_interface.csi2.lane_speed_mbps` | `&mipi_csi_0 / port@0 / endpoint` | `link-frequencies = /bits/ 64 <speed*1e6>` |
| `controller.ethernet.speed` | `&fec1` or `&eqos` | `max-speed = <speed_value>` |
| `controller.csi2_rx.interface_index` | `&mipi_csi_{index}` | Node selection (0 or 1) |

### Generated DTS Template

```dts
/* Auto-generated from detector_config.yaml */
/* DO NOT EDIT MANUALLY - use ConfigConverter tool */

/dts-v1/;
/plugin/;

/ {
    compatible = "moai,{platform}-detector";
};

&mipi_csi_{csi2_rx_index} {
    status = "okay";
    ports {
        port@0 {
            reg = <0>;
            mipi_csi_in: endpoint {
                remote-endpoint = <&fpga_csi2_out>;
                data-lanes = <{lane_list}>;
                clock-lanes = <0>;
                link-frequencies = /bits/ 64 <{lane_speed_hz}>;
            };
        };
    };
};
```

### Validation Rules

- `controller.platform` must be a supported SoC with CSI-2 receiver
- `fpga.data_interface.csi2.lane_count` <= SoC max lanes (4 for i.MX8MP)
- `controller.ethernet.speed` = "10gbe" required if target/max tier

---

## 3. YAML to Host SDK Config (.json)

**Tool**: `ConfigConverter` (C# .NET 8.0+)
**Output**: `sdk/config/detector-sdk-config.json`

### Mapping Rules

| YAML Path | JSON Path | Notes |
|-----------|----------|-------|
| `panel.rows` | `$.image.height` | Frame height in pixels |
| `panel.cols` | `$.image.width` | Frame width in pixels |
| `panel.bit_depth` | `$.image.bitDepth` | Pixel bit depth |
| `controller.ethernet.port` | `$.network.port` | UDP listen port |
| `controller.ethernet.protocol` | `$.network.protocol` | Transport protocol |
| `controller.ethernet.payload_size` | `$.network.payloadSize` | Bytes per UDP packet |
| `controller.ethernet.mtu` | `$.network.mtu` | Maximum transmission unit |
| `host.storage.format` | `$.storage.format` | File format |
| `host.storage.path` | `$.storage.path` | Output directory |
| `host.storage.compression` | `$.storage.compression` | TIFF compression |
| `host.display.fps` | `$.display.fps` | Preview frame rate |
| `host.display.color_map` | `$.display.colorMap` | Color mapping |
| `host.network.receive_buffer_mb` | `$.network.receiveBufferMB` | Receive buffer size |
| `host.network.receive_threads` | `$.network.receiveThreads` | Thread count |
| `host.network.packet_timeout_ms` | `$.network.packetTimeoutMs` | Packet timeout |

### Generated JSON Template

```json
{
  "$schema": "detector-sdk-config-schema.json",
  "image": {
    "width": 2048,
    "height": 2048,
    "bitDepth": 16,
    "bytesPerPixel": 2,
    "frameSizeBytes": 8388608
  },
  "network": {
    "port": 8000,
    "protocol": "udp",
    "payloadSize": 8192,
    "mtu": 9000,
    "receiveBufferMB": 64,
    "receiveThreads": 2,
    "packetTimeoutMs": 1000
  },
  "storage": {
    "format": "tiff",
    "path": "./frames",
    "compression": "none"
  },
  "display": {
    "fps": 15,
    "colorMap": "gray"
  }
}
```

### Derived Values (computed during conversion)

| Derived Value | Formula | Example |
|--------------|---------|---------|
| `bytesPerPixel` | `ceil(bit_depth / 8)` | 16-bit -> 2 |
| `frameSizeBytes` | `rows * cols * bytesPerPixel` | 2048*2048*2 = 8,388,608 |
| `packetsPerFrame` | `ceil(frameSizeBytes / payloadSize)` | 8388608/8192 = 1024 |
| `rawDataRateGbps` | `rows * cols * bitDepth * fps / 1e9` | 2048*2048*16*30/1e9 = 2.01 |

### Validation Rules

- `rawDataRateGbps` must not exceed ethernet speed (1.0 for 1gbe, 9.5 for 10gbe)
- `payloadSize` <= `mtu - 28` (IP + UDP headers)
- `frameSizeBytes` <= `receive_buffer_mb * 1e6` (buffer must hold at least 1 frame)

---

## 4. YAML to Simulator Config

**Tool**: `ConfigConverter` (C# .NET 8.0+)
**Output**: `tools/config/simulator-config.json`

### Mapping Rules

All YAML fields are directly passed to the simulator with additional simulation-specific parameters:

| YAML Path | Simulator Use | Notes |
|-----------|-------------|-------|
| `panel.*` | PanelSimulator initialization | Resolution, bit depth |
| `fpga.*` | FpgaSimulator initialization | Timing, buffer, CSI-2 config |
| `controller.*` | McuSimulator initialization | Ethernet, frame buffer |
| `host.*` | HostSimulator initialization | Storage, display |

### Additional Simulator Parameters (not in detector_config.yaml)

```json
{
  "simulation": {
    "noiseModel": "gaussian",
    "noiseStdDev": 100,
    "defectRate": 0.001,
    "randomSeed": 42,
    "maxFrames": 1000
  }
}
```

---

## 5. Cross-Validation Rules

These rules validate consistency across the entire configuration:

### Bandwidth Consistency

```
csi2_bandwidth = lane_count * lane_speed_mbps * 1e6  (bits/sec)
raw_data_rate  = rows * cols * bit_depth * fps        (bits/sec)
csi2_efficiency = 0.75  (protocol overhead factor)

ASSERT: raw_data_rate <= csi2_bandwidth * csi2_efficiency
```

### Ethernet Bandwidth Consistency

```
ethernet_bandwidth = 1e9 (1gbe) or 10e9 (10gbe)  (bits/sec)
udp_efficiency = 0.95  (Ethernet + IP + UDP overhead)

ASSERT: raw_data_rate <= ethernet_bandwidth * udp_efficiency
```

### Line Buffer Sizing

```
line_size_bytes = cols * ceil(bit_depth / 8)
bram_per_line = ceil(line_size_bytes / (bram_width_bits / 8))

ASSERT: bram_per_line * depth_lines <= 50  (Artix-7 35T BRAM limit)
```

### Frame Buffer Sizing

```
frame_size_bytes = rows * cols * ceil(bit_depth / 8)
total_buffer_bytes = frame_size_bytes * frame_buffer_count

ASSERT: total_buffer_bytes <= allocation_mb * 1e6
```

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (analyst) | Initial conversion mapping document |

---
