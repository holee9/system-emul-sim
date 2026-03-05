# meta-detector Yocto Layer

X-ray Detector Panel SoC Firmware Yocto Layer for Variscite i.MX8M Plus BSP.

## Layer Information

- **Name**: meta-detector
- **Compatibility**: Yocto Project Scarthgap 5.0 LTS
- **BSP**: Variscite meta-variscite-bsp (imx-6.6.52-2.2.0-v1.3)
- **Target**: NXP i.MX8M Plus (ARM Cortex-A53, aarch64)

## Directory Structure

```
meta-detector/
├── conf/
│   └── layer.conf          # Layer configuration
├── recipes-core/
│   ├── detector-daemon/
│   │   └── detector-daemon_1.0.0.bb    # BitBake recipe for detector-daemon
│   └── images/
│       └── detector-image.bb           # Image recipe with all dependencies
└── README.md
```

## Dependencies

This layer depends on:
- `core` (OE-Core)
- `meta-variscite-bsp` (Variscite i.MX8M Plus BSP)

## Usage

1. Add this layer to your Yocto build:
   ```bash
   bitbake-layers add-layer ../meta-detector
   ```

2. Build the detector image:
   ```bash
   bitbake detector-image
   ```

3. Or build only the daemon:
   ```bash
   bitbake detector-daemon
   ```

## Recipes

### detector-daemon

User-space daemon for X-ray Detector Panel SoC controller.
- Manages SPI communication with FPGA
- CSI-2 frame reception via V4L2
- Ethernet frame transmission (UDP)
- Command protocol processing
- Health monitoring

### detector-image

Target image including:
- detector-daemon
- Runtime dependencies (v4l-utils, spidev, iproute2, ethtool, libyaml)
- Systemd service configuration

## Build Requirements

- Yocto Project Scarthgap 5.0 LTS
- Variscite BSP layer (meta-variscite-bsp imx-6.6.52-2.2.0-v1.3)
- Cross-compiler: aarch64-poky-linux-gcc

## License

Proprietary - See LICENSE file in source repository.

## Maintainer

ABYZ-Lab (X-ray Detector Panel System Project)
