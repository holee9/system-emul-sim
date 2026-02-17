# Configuration Conversion Mapping Guide

**Document Version**: 1.0.0
**Status**: Draft
**Last Updated**: 2026-02-17
**Author**: MoAI Documentation Agent
**Project**: X-ray Detector Panel System

---

## Table of Contents

1. [Overview](#overview)
2. [detector_config.yaml Schema](#detector_configyaml-schema)
3. [YAML to FPGA TCL Mapping](#yaml-to-fpga-tcl-mapping)
4. [YAML to C Header Mapping](#yaml-to-c-header-mapping)
5. [YAML to C# AppSettings Mapping](#yaml-to-c-appsettings-mapping)
6. [Bandwidth Calculation Formulas](#bandwidth-calculation-formulas)
7. [Per-Tier Configuration Examples](#per-tier-configuration-examples)
8. [CodeGenerator Template Variables](#codegenerator-template-variables)
9. [ConfigConverter Command Examples](#configconverter-command-examples)

---

## Overview

`detector_config.yaml` is the **single source of truth** for all system parameters. All other configuration artifacts (FPGA TCL scripts, C headers, C# AppSettings) are generated from this file using the `ConfigConverter` and `CodeGenerator` tools. No parameter should exist in two places; always update `detector_config.yaml` and regenerate.

### File Locations

| File | Repository | Purpose |
|------|-----------|---------|
| `detector_config.yaml` | `config/` | Single source of truth |
| `config/schema/detector-config-schema.json` | `config/` | JSON Schema for validation |
| `config/templates/fpga_params.tcl.j2` | `config/` | Jinja2 template for FPGA TCL |
| `config/templates/detector_params.h.j2` | `config/` | Jinja2 template for C header |
| `config/templates/appsettings.json.j2` | `config/` | Jinja2 template for C# AppSettings |
| `tools/ConfigConverter/` | `tools/` | YAML validation and conversion tool |
| `tools/CodeGenerator/` | `tools/` | Template rendering tool |

### Generation Flow

```
detector_config.yaml
        |
        v
[ConfigConverter] validates against detector-config-schema.json
        |
        v
[CodeGenerator] renders Jinja2 templates
        |
        +---> fpga/constraints/detector_params.tcl   (FPGA Vivado parameters)
        |
        +---> fw/include/detector_params.h            (SoC firmware C header)
        |
        +---> sdk/config/appsettings.json             (Host SDK C# settings)
        |
        +---> tools/FpgaSimulator/appsettings.json    (Simulator C# settings)
```

---

## detector_config.yaml Schema

The configuration file has four top-level sections. Below is the annotated schema with types and constraints.

```yaml
# detector_config.yaml - Single Source of Truth
# Schema version must match detector-config-schema.json

schema_version: "1.0.0"    # string, required

panel:
  rows: int                  # Panel row count. Valid: 1024, 2048, 3072
  cols: int                  # Panel column count. Valid: 1024, 2048, 3072
  bit_depth: int             # Bits per pixel. Valid: 8, 10, 12, 14, 16
  fps: int                   # Target frame rate. Valid: 1-30
  pixel_pitch_um: float      # Pixel pitch in micrometers. Informational only.

fpga:
  device: string             # Xilinx part number. Fixed: "xc7a35tfgg484-1"
  csi2:
    lanes: int               # D-PHY data lane count. Valid: 1, 2, 4
    lane_mbps: int           # Bit rate per lane. Valid: 200, 400, 800
    virtual_channel: int     # CSI-2 VC number. Valid: 0-3
    data_type: string        # MIPI DT value. Valid: "RAW8","RAW10","RAW12","RAW14","RAW16"
    ecc_enabled: bool        # Enable CSI-2 ECC. Recommended: true
    crc_enabled: bool        # Enable CSI-2 CRC. Recommended: true
  spi:
    max_freq_mhz: int        # SPI clock frequency. Valid: 1-50
    cpol: int                # Clock polarity. Valid: 0, 1
    cpha: int                # Clock phase. Valid: 0, 1
  resources:
    lut_budget_pct: int      # Max LUT utilization %. Constraint: ≤60
    bram_budget_blocks: int  # Max BRAM blocks. Constraint: ≤50

controller:
  soc: string                # SoC module identifier. Fixed: "VAR-SOM-MX8M-PLUS"
  os: string                 # OS distribution. Fixed: "yocto-scarthgap-5.0"
  kernel: string             # Linux kernel version. Fixed: "6.6.52"
  network:
    protocol: string         # Streaming protocol. Fixed: "udp"
    mtu_bytes: int           # UDP payload size. Valid: 1500, 8192 (jumbo)
    dst_port: int            # Host data port. Default: 8000 (canonical: ethernet-protocol.md)
    src_port: int            # SoC source port. Default: 8001 (canonical: ethernet-protocol.md)
    discovery_port: int      # Discovery/handshake port. Default: 8002 (canonical: ethernet-protocol.md)

host:
  platform: string           # Host OS. Example: "linux-x86_64"
  framework: string          # SDK framework. Fixed: ".NET 8.0"
  network:
    interface: string        # NIC name or "auto". Example: "eth0"
    link_speed_gbps: int     # Expected link speed. Valid: 1, 10
  buffer:
    frame_queue_depth: int   # Number of frames in receive queue. Default: 4
    udp_socket_buffer_mb: int # SO_RCVBUF size in MB. Default: 32
```

---

## YAML to FPGA TCL Mapping

Vivado uses TCL scripts for project configuration and parameter passing. The `CodeGenerator` renders `fpga_params.tcl.j2` into `fpga/constraints/detector_params.tcl`.

### Mapping Table

| YAML Path | TCL Variable | Type | Example Value |
|-----------|-------------|------|---------------|
| `panel.rows` | `PANEL_ROWS` | integer | `3072` |
| `panel.cols` | `PANEL_COLS` | integer | `3072` |
| `panel.bit_depth` | `PANEL_BIT_DEPTH` | integer | `16` |
| `panel.fps` | `PANEL_FPS` | integer | `15` |
| `fpga.csi2.lanes` | `CSI2_LANE_COUNT` | integer | `4` |
| `fpga.csi2.lane_mbps` | `CSI2_LANE_MBPS` | integer | `800` |
| `fpga.csi2.virtual_channel` | `CSI2_VC` | integer | `0` |
| `fpga.csi2.data_type` | `CSI2_DATA_TYPE` | string | `"RAW16"` |
| `fpga.csi2.ecc_enabled` | `CSI2_ECC_EN` | boolean (1/0) | `1` |
| `fpga.csi2.crc_enabled` | `CSI2_CRC_EN` | boolean (1/0) | `1` |
| `fpga.spi.max_freq_mhz` | `SPI_CLK_MHZ` | integer | `50` |
| `fpga.spi.cpol` | `SPI_CPOL` | integer | `0` |
| `fpga.spi.cpha` | `SPI_CPHA` | integer | `0` |
| `fpga.resources.lut_budget_pct` | `LUT_BUDGET_PCT` | integer | `60` |
| `fpga.resources.bram_budget_blocks` | `BRAM_BUDGET` | integer | `50` |

### Derived TCL Parameters

These values are computed by the CodeGenerator from YAML fields:

| Derived Variable | Formula | Example |
|-----------------|---------|---------|
| `PIXEL_COUNT` | `panel.rows × panel.cols` | `9,437,184` |
| `WC_BYTES` | `panel.cols × (panel.bit_depth / 8)` | `6,144` |
| `BYTE_CLK_MHZ` | `fpga.csi2.lane_mbps / 8` | `100` |
| `RAW_BW_GBPS` | `rows × cols × bit_depth × fps / 1e9` | `2.264` |
| `CSI2_BW_GBPS` | `RAW_BW_GBPS / 0.85` | `2.664` |
| `BW_PER_LANE_MBPS` | `CSI2_BW_GBPS × 1000 / lanes` | `666` |

### Generated TCL File Example

```tcl
# AUTO-GENERATED by CodeGenerator from detector_config.yaml
# DO NOT EDIT MANUALLY - edit detector_config.yaml instead
# Generated: 2026-02-17T00:00:00Z

set PANEL_ROWS        3072
set PANEL_COLS        3072
set PANEL_BIT_DEPTH   16
set PANEL_FPS         15

set CSI2_LANE_COUNT   4
set CSI2_LANE_MBPS    800
set CSI2_VC           0
set CSI2_DATA_TYPE    "RAW16"
set CSI2_ECC_EN       1
set CSI2_CRC_EN       1

set SPI_CLK_MHZ       50
set SPI_CPOL          0
set SPI_CPHA          0

# Derived parameters
set PIXEL_COUNT       [expr {$PANEL_ROWS * $PANEL_COLS}]
set WC_BYTES          [expr {$PANEL_COLS * ($PANEL_BIT_DEPTH / 8)}]
set BYTE_CLK_MHZ      [expr {$CSI2_LANE_MBPS / 8}]
```

---

## YAML to C Header Mapping

The firmware C header (`fw/include/detector_params.h`) is generated by the CodeGenerator from `detector_params.h.j2`.

### Mapping Table

| YAML Path | C Macro | Type | Example Value |
|-----------|---------|------|---------------|
| `panel.rows` | `DETECTOR_ROWS` | `uint16_t` | `3072U` |
| `panel.cols` | `DETECTOR_COLS` | `uint16_t` | `3072U` |
| `panel.bit_depth` | `DETECTOR_BIT_DEPTH` | `uint8_t` | `16U` |
| `panel.fps` | `DETECTOR_FPS` | `uint8_t` | `15U` |
| `fpga.csi2.lanes` | `CSI2_LANE_COUNT` | `uint8_t` | `4U` |
| `fpga.csi2.lane_mbps` | `CSI2_LANE_MBPS` | `uint16_t` | `800U` |
| `fpga.csi2.virtual_channel` | `CSI2_VIRTUAL_CHANNEL` | `uint8_t` | `0U` |
| `fpga.csi2.data_type` | `CSI2_DATA_TYPE_HEX` | `uint8_t` (hex) | `0x2EU` |
| `fpga.csi2.ecc_enabled` | `CSI2_ECC_ENABLE` | `uint8_t` (bool) | `1U` |
| `fpga.csi2.crc_enabled` | `CSI2_CRC_ENABLE` | `uint8_t` (bool) | `1U` |
| `fpga.spi.max_freq_mhz` | `SPI_MAX_FREQ_MHZ` | `uint8_t` | `50U` |
| `fpga.spi.cpol` | `SPI_CPOL` | `uint8_t` | `0U` |
| `fpga.spi.cpha` | `SPI_CPHA` | `uint8_t` | `0U` |
| `controller.network.mtu_bytes` | `UDP_MTU_BYTES` | `uint16_t` | `8192U` |
| `controller.network.dst_port` | `HOST_UDP_PORT` | `uint16_t` | `8000U` |
| `controller.network.src_port` | `SOC_UDP_PORT` | `uint16_t` | `8001U` |
| `controller.network.discovery_port` | `DISCOVERY_UDP_PORT` | `uint16_t` | `8002U` |

### Data Type Hex Lookup

| `data_type` YAML value | `CSI2_DATA_TYPE_HEX` |
|-----------------------|---------------------|
| `"RAW8"` | `0x2AU` |
| `"RAW10"` | `0x2BU` |
| `"RAW12"` | `0x2CU` |
| `"RAW14"` | `0x2DU` |
| `"RAW16"` | `0x2EU` |

### Derived C Macros

| Derived Macro | Formula | Type | Example |
|--------------|---------|------|---------|
| `FRAME_SIZE_BYTES` | `DETECTOR_ROWS × DETECTOR_COLS × (DETECTOR_BIT_DEPTH / 8)` | `uint32_t` | `18,874,368` |
| `LINE_BYTES` | `DETECTOR_COLS × (DETECTOR_BIT_DEPTH / 8)` | `uint16_t` | `6,144` |
| `FRAMES_PER_SECOND` | Same as `DETECTOR_FPS` | `uint8_t` | `15` |
| `UDP_PIXELS_PER_PACKET` | `UDP_MTU_BYTES / (DETECTOR_BIT_DEPTH / 8)` | `uint16_t` | `4,096` |

### Generated C Header Example

```c
/**
 * @file detector_params.h
 * @brief AUTO-GENERATED from detector_config.yaml
 *        DO NOT EDIT MANUALLY - edit detector_config.yaml instead
 * @generated 2026-02-17T00:00:00Z
 */
#ifndef DETECTOR_PARAMS_H
#define DETECTOR_PARAMS_H

#include <stdint.h>

/* Panel geometry */
#define DETECTOR_ROWS         3072U
#define DETECTOR_COLS         3072U
#define DETECTOR_BIT_DEPTH    16U
#define DETECTOR_FPS          15U

/* CSI-2 interface */
#define CSI2_LANE_COUNT       4U
#define CSI2_LANE_MBPS        800U
#define CSI2_VIRTUAL_CHANNEL  0U
#define CSI2_DATA_TYPE_HEX    0x2EU   /* RAW16 */
#define CSI2_ECC_ENABLE       1U
#define CSI2_CRC_ENABLE       1U

/* SPI interface */
#define SPI_MAX_FREQ_MHZ      50U
#define SPI_CPOL              0U
#define SPI_CPHA              0U

/* UDP streaming */
#define UDP_MTU_BYTES         8192U
#define HOST_UDP_PORT         8000U  /* data port */
#define SOC_UDP_PORT          8001U  /* source port */
#define DISCOVERY_UDP_PORT    8002U  /* discovery/handshake port */

/* Derived: frame geometry */
#define FRAME_SIZE_BYTES      (DETECTOR_ROWS * DETECTOR_COLS * (DETECTOR_BIT_DEPTH / 8U))
#define LINE_BYTES            (DETECTOR_COLS * (DETECTOR_BIT_DEPTH / 8U))
#define UDP_PIXELS_PER_PACKET (UDP_MTU_BYTES / (DETECTOR_BIT_DEPTH / 8U))

#endif /* DETECTOR_PARAMS_H */
```

---

## YAML to C# AppSettings Mapping

The Host SDK and FpgaSimulator tools use `appsettings.json` for runtime configuration. Generated by `CodeGenerator` from `appsettings.json.j2`.

### Mapping Table

| YAML Path | AppSettings JSON Path | C# Type | Example Value |
|-----------|----------------------|---------|---------------|
| `panel.rows` | `Detector.Rows` | `int` | `3072` |
| `panel.cols` | `Detector.Cols` | `int` | `3072` |
| `panel.bit_depth` | `Detector.BitDepth` | `int` | `16` |
| `panel.fps` | `Detector.TargetFps` | `int` | `15` |
| `fpga.csi2.lanes` | `Csi2.LaneCount` | `int` | `4` |
| `fpga.csi2.lane_mbps` | `Csi2.LaneMbps` | `int` | `800` |
| `fpga.csi2.virtual_channel` | `Csi2.VirtualChannel` | `int` | `0` |
| `fpga.csi2.data_type` | `Csi2.DataType` | `string` | `"RAW16"` |
| `controller.network.dst_port` | `Network.HostUdpPort` | `int` | `8000` |
| `controller.network.src_port` | `Network.SocUdpPort` | `int` | `8001` |
| `controller.network.discovery_port` | `Network.DiscoveryUdpPort` | `int` | `8002` |
| `controller.network.mtu_bytes` | `Network.MtuBytes` | `int` | `8192` |
| `host.network.interface` | `Network.Interface` | `string` | `"eth0"` |
| `host.network.link_speed_gbps` | `Network.LinkSpeedGbps` | `int` | `10` |
| `host.buffer.frame_queue_depth` | `Buffer.FrameQueueDepth` | `int` | `4` |
| `host.buffer.udp_socket_buffer_mb` | `Buffer.UdpSocketBufferMb` | `int` | `32` |

### Generated AppSettings Example

```json
{
  "$comment": "AUTO-GENERATED from detector_config.yaml. DO NOT EDIT MANUALLY.",
  "$generated": "2026-02-17T00:00:00Z",

  "Detector": {
    "Rows": 3072,
    "Cols": 3072,
    "BitDepth": 16,
    "TargetFps": 15,
    "FrameSizeBytes": 18874368
  },

  "Csi2": {
    "LaneCount": 4,
    "LaneMbps": 800,
    "VirtualChannel": 0,
    "DataType": "RAW16"
  },

  "Network": {
    "Interface": "eth0",
    "LinkSpeedGbps": 10,
    "HostUdpPort": 8000,
    "SocUdpPort": 8001,
    "DiscoveryUdpPort": 8002,
    "MtuBytes": 8192
  },

  "Buffer": {
    "FrameQueueDepth": 4,
    "UdpSocketBufferMb": 32
  }
}
```

### C# Configuration Binding Classes

The Host SDK uses strongly-typed configuration classes bound via `IOptions<T>`:

```csharp
// Generated class skeleton (implemented in sdk/Config/DetectorConfig.cs)
public record DetectorConfig
{
    public int Rows { get; init; }
    public int Cols { get; init; }
    public int BitDepth { get; init; }
    public int TargetFps { get; init; }
    public long FrameSizeBytes => Rows * Cols * (BitDepth / 8L);
}

public record NetworkConfig
{
    public string Interface { get; init; } = "auto";
    public int LinkSpeedGbps { get; init; }
    public int HostUdpPort { get; init; }
    public int SocUdpPort { get; init; }
    public int MtuBytes { get; init; }
    public int PixelsPerPacket => MtuBytes / (/* from Detector.BitDepth */ 2);
}
```

---

## Bandwidth Calculation Formulas

All bandwidth calculations use these formulas. The CodeGenerator validates that the configured tier does not exceed hardware limits.

### Raw Data Rate

```
RawBandwidth_bps = rows × cols × bit_depth × fps

Example (Target tier):
  3072 × 3072 × 16 × 15 = 2,264,924,160 bps = 2.265 Gbps
```

### CSI-2 Effective Bandwidth

The CSI-2 interface has overhead due to packet headers, ECC, CRC, LP-to-HS transitions, and inter-frame gaps. The efficiency factor is approximately 85%.

```
Csi2Bandwidth_bps = RawBandwidth_bps / 0.85

Example (Target tier):
  2.265 Gbps / 0.85 = 2.664 Gbps  (required CSI-2 throughput)

Available bandwidth at 800 Mbps × 4 lanes:
  800 Mbps × 4 = 3,200 Mbps = 3.2 Gbps

Utilization = 2.664 / 3.200 = 83.3%
Headroom    = 16.7%  (CSI-2 bandwidth basis: 0.536 Gbps remaining)
            = 29.2%  (raw bandwidth basis: 1 - 2.265/3.2, used in project planning)
```

### Per-Lane Required Bandwidth

```
BandwidthPerLane_Mbps = Csi2Bandwidth_bps / (lanes × 1e6)

Example (Target tier, 4 lanes):
  2,664 Mbps / 4 = 666 Mbps/lane  (< 800 Mbps, fits within budget)
```

### UDP Streaming Bandwidth

```
UdpBandwidth_bps = RawBandwidth_bps / UdpPayloadEfficiency

Where:
  UdpPayloadEfficiency = UdpMtuBytes / (UdpMtuBytes + UdpHeaderBytes)
  UdpHeaderBytes = 8 (UDP) + 20 (IP) + 14 (Ethernet) + CustomHeader
                 ≈ 50 bytes for this protocol

Example (Target tier, 8192-byte MTU):
  Efficiency = 8192 / (8192 + 50) = 99.4%
  UdpBandwidth = 2.265 Gbps / 0.994 = 2.279 Gbps

Required link: 10 GbE (10 Gbps) → 22.8% utilization
```

### Bandwidth Validation Logic (CodeGenerator)

The CodeGenerator validates the selected tier against hardware limits before generating any output:

```
Validation Checks:
  1. CSI2 utilization ≤ 95% of (lanes × lane_mbps):
       2664 Mbps ≤ 0.95 × 3200 Mbps = 3040 Mbps  ✓

  2. UDP bandwidth ≤ 90% of link speed:
       2279 Mbps ≤ 0.90 × 10000 Mbps = 9000 Mbps  ✓

  3. LUT utilization ≤ lut_budget_pct:
       (Estimated from RTL analysis, not calculated here)

  4. BRAM usage ≤ bram_budget_blocks:
       Line buffer: 4 BRAMs (Ping-Pong dual-bank, 2×36Kb BRAMs per bank)
         Per bank: 3072 pixels × 16-bit = 49,152 bits > 36,864 bits (1 BRAM limit)
         → 2 BRAMs cascaded per bank, 4 BRAMs total for Ping-Pong operation
         Reference: fpga-design.md Section 4.2
       ✓ (4 BRAMs < 50 limit)
```

---

## Per-Tier Configuration Examples

### Minimum Tier (1024x1024, 14-bit, 15fps)

```yaml
panel:
  rows: 1024
  cols: 1024
  bit_depth: 14
  fps: 15

fpga:
  csi2:
    lanes: 4
    lane_mbps: 400        # 400M sufficient
    data_type: "RAW14"    # 0x2D

# Bandwidth: 1024×1024×14×15 = 220,200,960 bps = 0.22 Gbps
# CSI-2 needed: 0.22/0.85 = 0.26 Gbps
# Available at 400M×4: 1.6 Gbps → 16% utilization
```

### Intermediate-A Tier (2048x2048, 16-bit, 15fps) - Current Baseline

```yaml
panel:
  rows: 2048
  cols: 2048
  bit_depth: 16
  fps: 15

fpga:
  csi2:
    lanes: 4
    lane_mbps: 400        # 400M sufficient
    data_type: "RAW16"    # 0x2E

controller:
  network:
    mtu_bytes: 8192       # Jumbo frames recommended

host:
  network:
    link_speed_gbps: 10   # Requires 10 GbE
    # NOTE: 1 GbE (0.95 Gbps effective) is insufficient for 2048x2048@15fps (1.007 Gbps raw)
    # See system-architecture.md Section 6.2 for bandwidth table

# Bandwidth: 2048×2048×16×15 = 1,006,632,960 bps = 1.007 Gbps
# CSI-2 needed: 1.007/0.85 = 1.184 Gbps
# Available at 400M×4: 1.6 Gbps → 74% utilization (fits)
# UDP: 1.007 Gbps exceeds 1 GbE (0.95 Gbps effective) → 10 GbE required
```

### Target Tier (3072x3072, 16-bit, 15fps) - Final Goal

```yaml
panel:
  rows: 3072
  cols: 3072
  bit_depth: 16
  fps: 15
  pixel_pitch_um: 150.0

fpga:
  device: "xc7a35tfgg484-1"
  csi2:
    lanes: 4
    lane_mbps: 800        # 800M required
    virtual_channel: 0
    data_type: "RAW16"    # 0x2E
    ecc_enabled: true
    crc_enabled: true
  spi:
    max_freq_mhz: 50
    cpol: 0
    cpha: 0
  resources:
    lut_budget_pct: 60
    bram_budget_blocks: 50

controller:
  soc: "VAR-SOM-MX8M-PLUS"
  os: "yocto-scarthgap-5.0"
  kernel: "6.6.52"
  network:
    protocol: "udp"
    mtu_bytes: 8192       # Jumbo frames required
    dst_port: 8000        # Canonical port per ethernet-protocol.md
    src_port: 8001        # Canonical port per ethernet-protocol.md
    discovery_port: 8002  # Canonical port per ethernet-protocol.md

host:
  platform: "linux-x86_64"
  framework: ".NET 8.0"
  network:
    interface: "eth0"
    link_speed_gbps: 10   # 10 GbE required
  buffer:
    frame_queue_depth: 4
    udp_socket_buffer_mb: 32

# Bandwidth: 3072×3072×16×15 = 2,264,924,160 bps = 2.265 Gbps
# CSI-2 needed: 2.265/0.85 = 2.664 Gbps
# Available at 800M×4: 3.2 Gbps → 83.3% CSI-2 utilization (29% raw headroom / 16.7% CSI-2 headroom)
# UDP with jumbo: 2.265 Gbps on 10 GbE → 22.7% utilization
```

---

## CodeGenerator Template Variables

The CodeGenerator exposes computed values to Jinja2 templates as variables. Template authors can use these directly.

### Available Template Variables

| Variable | Source | Type | Example |
|----------|--------|------|---------|
| `config.panel.rows` | YAML direct | int | `3072` |
| `config.panel.cols` | YAML direct | int | `3072` |
| `config.panel.bit_depth` | YAML direct | int | `16` |
| `config.panel.fps` | YAML direct | int | `15` |
| `derived.frame_size_bytes` | `rows × cols × bit_depth/8` | int | `18874368` |
| `derived.line_bytes` | `cols × bit_depth/8` | int | `6144` |
| `derived.wc_bytes` | Same as `line_bytes` | int | `6144` |
| `derived.raw_bw_gbps` | `rows×cols×bit_depth×fps / 1e9` | float | `2.265` |
| `derived.csi2_bw_gbps` | `raw_bw_gbps / 0.85` | float | `2.664` |
| `derived.csi2_utilization_pct` | `csi2_bw × 100 / (lanes × lane_mbps / 1000)` | float | `83.3` |
| `derived.udp_pixels_per_packet` | `mtu_bytes / (bit_depth/8)` | int | `4096` |
| `derived.byte_clk_mhz` | `lane_mbps / 8` | int | `100` |
| `meta.generated_at` | Timestamp | string | `"2026-02-17T00:00:00Z"` |
| `meta.schema_version` | YAML direct | string | `"1.0.0"` |

### Jinja2 Template Example (C Header)

```jinja
/**
 * @file detector_params.h
 * @brief AUTO-GENERATED from detector_config.yaml
 *        Generated: {{ meta.generated_at }}
 */
#define DETECTOR_ROWS        {{ config.panel.rows }}U
#define DETECTOR_COLS        {{ config.panel.cols }}U
#define DETECTOR_BIT_DEPTH   {{ config.panel.bit_depth }}U
#define FRAME_SIZE_BYTES     {{ derived.frame_size_bytes }}UL
#define CSI2_DATA_TYPE_HEX   {{ derived.csi2_data_type_hex }}U  /* {{ config.fpga.csi2.data_type }} */

/* Bandwidth summary (informational):
 * Raw:   {{ "%.3f"|format(derived.raw_bw_gbps) }} Gbps
 * CSI-2: {{ "%.3f"|format(derived.csi2_bw_gbps) }} Gbps ({{ "%.1f"|format(derived.csi2_utilization_pct) }}% utilization)
 */
```

---

## ConfigConverter Command Examples

The `ConfigConverter` tool validates `detector_config.yaml` and reports any schema violations or bandwidth constraint violations before code generation.

### Validate Configuration

```bash
# Validate detector_config.yaml against schema
dotnet run --project tools/ConfigConverter -- validate \
    --config config/detector_config.yaml \
    --schema config/schema/detector-config-schema.json

# Expected output (valid):
# [OK] Schema validation passed
# [OK] Bandwidth check: CSI-2 utilization 83.3% (< 95% limit)
# [OK] UDP bandwidth: 2.28 Gbps (< 90% of 10 GbE)
# [OK] BRAM estimate: 4 blocks (Ping-Pong line buffer, < 50 limit)
# Configuration is valid.
```

### Generate All Targets

```bash
# Generate all artifacts from current config
dotnet run --project tools/CodeGenerator -- generate-all \
    --config config/detector_config.yaml \
    --output-tcl fpga/constraints/detector_params.tcl \
    --output-header fw/include/detector_params.h \
    --output-appsettings-host sdk/config/appsettings.json \
    --output-appsettings-sim tools/FpgaSimulator/appsettings.json

# Expected output:
# [GEN] fpga/constraints/detector_params.tcl → 847 bytes
# [GEN] fw/include/detector_params.h → 1,243 bytes
# [GEN] sdk/config/appsettings.json → 612 bytes
# [GEN] tools/FpgaSimulator/appsettings.json → 612 bytes
# All artifacts generated successfully.
```

### Generate Single Target

```bash
# Generate only the C header
dotnet run --project tools/CodeGenerator -- generate \
    --config config/detector_config.yaml \
    --template config/templates/detector_params.h.j2 \
    --output fw/include/detector_params.h

# Generate only the FPGA TCL
dotnet run --project tools/CodeGenerator -- generate \
    --config config/detector_config.yaml \
    --template config/templates/fpga_params.tcl.j2 \
    --output fpga/constraints/detector_params.tcl
```

### Switch Between Performance Tiers

```bash
# Use the Intermediate-A tier profile (400M, 2048x2048)
dotnet run --project tools/ConfigConverter -- apply-profile \
    --config config/detector_config.yaml \
    --profile config/profiles/intermediate-a.yaml \
    --output config/detector_config.yaml

# Regenerate all artifacts after tier switch
dotnet run --project tools/CodeGenerator -- generate-all \
    --config config/detector_config.yaml

# Verify the new bandwidth figures
dotnet run --project tools/ConfigConverter -- validate \
    --config config/detector_config.yaml \
    --show-bandwidth
```

### Show Bandwidth Summary

```bash
dotnet run --project tools/ConfigConverter -- bandwidth-summary \
    --config config/detector_config.yaml

# Expected output:
# === Bandwidth Summary ===
# Panel: 3072 x 3072, 16-bit, 15 fps
#
# Raw data rate:    2.265 Gbps
# CSI-2 efficiency: 85.0%
# CSI-2 required:   2.664 Gbps
# CSI-2 available:  3.200 Gbps (800 Mbps × 4 lanes)
# CSI-2 utilization: 83.3% [OK, headroom: 16.7%]
#
# UDP efficiency:   99.4% (8192 B MTU + 50 B overhead)
# UDP required:     2.279 Gbps
# Link speed:       10.000 Gbps (10 GbE)
# UDP utilization:  22.8% [OK]
#
# Frame size:       18,874,368 bytes (18.0 MiB)
# At 15 fps:        283,115,520 bytes/sec = 2.265 Gbps
```

---

*Document End*

## Review Record

- Date: 2026-02-17
- Reviewer: manager-quality
- Status: Approved (with corrections)
- TRUST 5: T:4 R:5 U:4 S:5 T:4
- Corrections Applied:
  - Bandwidth section: Clarified headroom calculation ambiguity. "29% headroom" is raw bandwidth basis (1 - 2.265/3.2 = 29.2%). CSI-2 efficiency-adjusted headroom is 16.7% (1 - 2.664/3.2). Both figures now explicitly labeled to avoid confusion.
  - Target tier YAML comment: Updated inline comment to distinguish raw vs CSI-2 adjusted headroom.
- Notes: YAML schema accurate. Jinja2 template variables correct. All bandwidth formulas verified. ConfigConverter command examples consistent with tool architecture. OS/kernel versions confirmed (yocto-scarthgap-5.0, kernel 6.6.52).
- v1.0.1 corrections (2026-02-17):
  - UDP ports corrected to canonical values: 8000 (data), 8001 (source), 8002 (discovery) per ethernet-protocol.md
  - Intermediate-A host.network.link_speed_gbps corrected from 1 to 10 (1 GbE insufficient for 1.007 Gbps raw data rate)
  - BRAM line buffer corrected from 6.8 blocks to 4 BRAMs (Ping-Pong dual-bank per fpga-design.md Section 4.2)
