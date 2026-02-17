# SPEC-POC-001 Implementation Plan

---
spec_id: SPEC-POC-001
milestone: M0.5
timeline: W23-W26 (3 weeks)
status: approved
---

## Overview

This plan details the execution strategy for M0.5 CSI-2 Proof of Concept, including hardware setup, FPGA configuration, SoC driver configuration, test procedures, and decision criteria.

---

## Phase Breakdown

### Phase 1: Hardware Setup and Basic Connectivity (W23, Days 1-3)

**Objective**: Assemble PoC hardware and validate basic FPGA-SoC connectivity

**Tasks**:

1. **FPGA Board Setup** (Day 1)
   - Unbox Artix-7 XC7A35T evaluation board
   - Install JTAG programmer (Xilinx Platform Cable USB II or on-board USB-JTAG)
   - Connect FPGA power supply (verify voltage: 12V or USB 5V per board spec)
   - Test JTAG connection: `vivado -mode tcl` → `connect_hw_server` → `open_hw_target`
   - Flash LED blink test bitstream to verify FPGA programming

2. **SoC Board Setup** (Day 1)
   - Unbox NXP i.MX8M Plus EVK (Variscite VAR-SOM-MX8M-PLUS DART variant)
   - Flash Yocto Scarthgap (5.0 LTS) image to eMMC or SD card:
     ```bash
     # Variscite BSP: imx-6.6.52-2.2.0-v1.3
     dd if=core-image-minimal-imx8mp-var-dart.wic of=/dev/mmcblk0 bs=1M status=progress
     sync
     ```
   - Connect UART console (USB-to-serial adapter, 115200 baud, 8N1)
   - Boot SoC and verify kernel version: `uname -r` (Expected: 6.6.52, Yocto Scarthgap 5.0 LTS)
   - Verify confirmed hardware peripherals:
     ```bash
     # WiFi/BT: Ezurio Sterling 60 (QCA6174A, M.2)
     lspci | grep -i qca
     dmesg | grep ath10k
     iw dev  # Check wlan0 interface

     # Battery: TI BQ40z50 (SMBus, I2C addr 0x0b)
     i2cdetect -y 0
     cat /sys/class/power_supply/bq40z50-0/capacity  # If driver loaded

     # IMU: Bosch BMI160 (I2C7, addr 0x68)
     i2cdetect -y 7
     cat /sys/bus/iio/devices/iio:device0/name  # Expected: bmi160

     # GPIO: NXP PCA9534 (I2C)
     dmesg | grep pca953x

     # 2.5GbE: Identify chip model
     lspci -nn | grep -i ethernet
     ip link show
     ethtool eth0  # Check link speed
     ```
   - Test CSI-2 receiver readiness:
     ```bash
     # V4L2 device (custom FPGA driver, new development)
     v4l2-ctl --list-devices
     ls -l /dev/video*
     ```

3. **FPC Cable Connection** (Day 2)
   - Identify FPGA CSI-2 TX connector pinout (data lanes D0-D3, clock lane, grounds)
   - Identify SoC CSI-2 RX connector pinout (verify lane mapping matches FPGA)
   - Connect 10 cm FPC cable between FPGA and SoC
   - Verify physical connection: no bent pins, secure latches

4. **Power-On Test** (Day 2)
   - Power on FPGA and SoC simultaneously
   - Monitor UART console for kernel messages: `dmesg | grep mipi`
   - Expected: "imx8-mipi-csi2 32e40000.mipi-csi: Registered sensor subdevice"
   - If errors: Check FPC cable orientation (some cables are reversible, some are not)

5. **Control Channel Test (SPI)** (Day 3)
   - Connect SPI wires between FPGA GPIO and SoC SPI master pins
   - Load FPGA bitstream with simple SPI slave register (address 0x00, read/write test)
   - SoC sends SPI write: `spidev_test -D /dev/spidev0.0 -v -s 1000000 -p 0x55`
   - FPGA ILA captures SPI transaction, verify received byte = 0x55
   - SoC reads back: verify echo = 0x55

**Deliverables**:
- Hardware connectivity verified
- FPGA programming functional
- SoC Linux boot successful
- SPI control channel operational

**Risks**:
- FPC cable pinout mismatch → Mitigation: Pre-verify pinout with multimeter continuity test
- SoC kernel driver not loaded → Mitigation: Rebuild kernel with CONFIG_VIDEO_IMX8_MIPI_CSI2=y

---

### Phase 2: FPGA CSI-2 TX IP Integration (W24, Days 4-7)

**Objective**: Instantiate AMD MIPI CSI-2 TX Subsystem IP and generate basic test pattern

**Tasks**:

1. **Vivado Project Creation** (Day 4)
   - Create new Vivado project: Target device = xc7a35tfgg484-1
   - Add constraint file: `constraints/csi2_poc.xdc` (pin assignments for D-PHY lanes)
   - Clock constraints: `create_clock -period 10.0 [get_ports clk_100mhz]` (100 MHz input)

2. **CSI-2 TX IP Configuration** (Day 4)
   - IP Catalog → MIPI CSI-2 TX Subsystem v3.1
   - Configuration GUI:
     - Lanes: 4 data lanes + 1 clock lane
     - Lane speed: 1.0 Gbps/lane (initial conservative setting)
     - Data type: RAW16 (0x2E)
     - Virtual channel: VC0
     - Line blanking: 100 pixel clocks
     - Frame blanking: 10 line times
   - Generate IP: `generate_target all [get_ips mipi_csi2_tx]`

3. **Test Pattern Generator RTL** (Day 5)
   - Module: `test_pattern_gen.sv`
   - Inputs: `clk`, `rst_n`, `enable`, `pattern_mode` (2 bits: 00=counter, 01=checkerboard, 10=PRBS)
   - Outputs: `pixel_data` (16-bit), `line_valid`, `frame_valid`, `pixel_valid`
   - Counter pattern logic:
     ```systemverilog
     logic [15:0] counter;
     always_ff @(posedge clk) begin
         if (!rst_n) counter <= 16'd0;
         else if (pixel_valid) counter <= counter + 1;
     end
     assign pixel_data = counter;
     ```
   - Frame timing: 1024×1024 pixels, 15 fps (line period = 65.1 µs)

4. **Top-Level Integration** (Day 5)
   - Module: `csi2_poc_top.sv`
   - Instantiate: `test_pattern_gen` → `mipi_csi2_tx` IP
   - Connect: `pixel_data` → CSI-2 TX `tdata`, `pixel_valid` → `tvalid`
   - AXI4-Stream protocol: `tdata[15:0]`, `tvalid`, `tready`, `tlast` (end of line)

5. **Synthesis and Implementation** (Day 6)
   - Run synthesis: `synth_design -top csi2_poc_top`
   - Check resource utilization: LUTs <5,000 (target)
   - Run implementation: `opt_design` → `place_design` → `route_design`
   - Check timing: WNS ≥0 ns (no timing violations)
   - Generate bitstream: `write_bitstream csi2_poc.bit`

6. **Bitstream Programming and ILA Validation** (Day 7)
   - Program FPGA: `program_hw_devices [get_hw_devices xc7a35t_0] -bitstream csi2_poc.bit`
   - Insert ILA (Integrated Logic Analyzer) probes:
     - Probe 0: `test_pattern_gen/pixel_data[15:0]`
     - Probe 1: `test_pattern_gen/pixel_valid`
     - Probe 2: `mipi_csi2_tx/dphy_data[3:0]` (D-PHY lane outputs)
   - Trigger ILA on `frame_valid` rising edge
   - Capture waveform: Verify counter increments 0x0000 → 0x0001 → 0x0002
   - Export ILA waveform: `write_hw_ila_data ila_counter_pattern.csv`

**Deliverables**:
- FPGA bitstream with CSI-2 TX IP and test pattern generator
- LUT utilization report: <5,000 LUTs (24% of device)
- ILA waveform confirms test pattern correctness

**Risks**:
- CSI-2 TX IP license not available → Mitigation: Confirm license at M0, purchase Vivado HL Design Edition if needed
- Timing closure failure → Mitigation: Reduce lane speed to 800 Mbps, add pipeline stages

---

### Phase 3: SoC CSI-2 RX Configuration and Frame Capture (W25, Days 8-11)

**Objective**: Configure i.MX8M Plus CSI-2 receiver and capture FPGA-transmitted frames

**Tasks**:

1. **Device Tree Configuration** (Day 8)
   - Edit device tree: `/boot/dtb/imx8mp-evk.dtb` (or source `.dts` file)
   - Add MIPI CSI-2 node:
     ```dts
     &mipi_csi_0 {
         status = "okay";
         ports {
             port@0 {
                 reg = <0>;
                 mipi_csi0_in: endpoint {
                     remote-endpoint = <&fpga_csi2_out>;
                     data-lanes = <1 2 3 4>;
                     clock-lanes = <0>;
                     link-frequencies = /bits/ 64 <1000000000>; // 1.0 Gbps
                 };
             };
         };
     };
     ```
   - Recompile device tree: `dtc -I dts -O dtb -o imx8mp-evk.dtb imx8mp-evk.dts`
   - Copy to boot partition: `cp imx8mp-evk.dtb /boot/dtb/`

2. **Kernel Driver Verification** (Day 8)
   - Reboot SoC: `reboot`
   - Check driver loaded: `lsmod | grep imx8_mipi_csi2` → should show module
   - Check V4L2 device: `v4l2-ctl --list-devices`
     - Expected output: `/dev/video0` (MIPI CSI-2 receiver)
   - Query capabilities: `v4l2-ctl -d /dev/video0 --all`
     - Expected: Format = RAW16, Width = 1024, Height = 1024

3. **Frame Capture Test (Single Frame)** (Day 9)
   - Start frame capture: `v4l2-ctl --device /dev/video0 --stream-mmap --stream-count=1 --stream-to=frame_001.raw`
   - Verify file size: `ls -lh frame_001.raw` → Expected: 2,097,152 bytes (1024×1024 pixels × 2 bytes)
   - Hexdump first 32 bytes: `hexdump -C frame_001.raw | head -n 2`
     - Expected (counter pattern): `00 00 01 00 02 00 03 00 04 00 05 00 06 00 07 00`

4. **Data Integrity Validation Script** (Day 9-10)
   - Create Python script: `validate_frame.py`
   - Logic:
     ```python
     import numpy as np
     frame = np.fromfile('frame_001.raw', dtype=np.uint16).reshape(1024, 1024)
     expected = np.arange(1024*1024, dtype=np.uint16).reshape(1024, 1024)
     errors = np.sum(frame != expected)
     print(f"Bit errors: {errors} / {1024*1024} pixels")
     ```
   - Run validation: `python3 validate_frame.py`
   - Expected output: `Bit errors: 0 / 1048576 pixels` ✅

5. **Multi-Frame Capture (100 Frames)** (Day 10)
   - Capture 100 frames: `v4l2-ctl --device /dev/video0 --stream-mmap --stream-count=100 --stream-to=frames_%03d.raw`
   - Batch validation:
     ```bash
     for i in {001..100}; do
         python3 validate_frame.py frames_$i.raw >> validation_log.txt
     done
     ```
   - Check log: `grep "Bit errors: [^0]" validation_log.txt` → Should be empty (no errors)

6. **Frame Drop Detection** (Day 11)
   - Modify validation script to check frame counter (first pixel value = frame number)
   - Detect missing frames: If frame N is missing, pixel[0] jumps from N-1 to N+1
   - Run 1000-frame capture: `v4l2-ctl --stream-count=1000 --stream-to=frames_%04d.raw`
   - Validate sequence: `python3 validate_sequence.py` → Report dropped frames

**Deliverables**:
- SoC successfully captures FPGA-transmitted frames
- 100-frame capture with zero bit errors
- Frame drop rate measured (target: <1%)

**Risks**:
- SoC driver fails to capture frames → Mitigation: Enable verbose logging (`dmesg -w`), check for DMA errors
- High frame drop rate → Mitigation: Increase line blanking in FPGA, reduce SoC CPU load

---

### Phase 4: Throughput Measurement and Lane Speed Characterization (W25-W26, Days 12-15)

**Objective**: Measure end-to-end throughput and characterize maximum stable lane speed

**Tasks**:

1. **Target Tier Configuration (2048×2048, 30 fps)** (Day 12)
   - Modify FPGA test pattern generator: Resolution = 2048×2048, FPS = 30
   - Recalculate timing:
     - Pixel clock = 2048 × 2048 × 30 = 125.83 MHz
     - Line period = 2048 pixels / 125.83 MHz = 16.3 µs
   - Recompile FPGA bitstream with new timing
   - Update SoC device tree: `link-frequencies = /bits/ 64 <1000000000>` (1.0 Gbps/lane)

2. **Throughput Measurement (1.0 Gbps/lane)** (Day 12)
   - Capture 1000 frames: `v4l2-ctl --stream-count=1000 --stream-to=/dev/null`
   - Measure duration: `time v4l2-ctl --stream-count=1000 --stream-to=/dev/null`
     - Expected: 33.33 seconds (1000 frames / 30 fps)
   - Calculate throughput:
     - Data volume = 1000 frames × 2048×2048 pixels × 2 bytes = 8.39 GB
     - Throughput = 8.39 GB / 33.33 s = 0.252 GB/s = 2.01 Gbps ✅
   - Validate: Throughput ≥1.58 Gbps (70% threshold) → PASS

3. **Lane Speed Sweep (1.0, 1.1, 1.2, 1.25 Gbps)** (Day 13-14)
   - For each lane speed:
     - Reconfigure CSI-2 TX IP: Lane speed = X Gbps
     - Regenerate bitstream, program FPGA
     - Update SoC device tree: `link-frequencies = /bits/ 64 <X000000000>`
     - Reboot SoC: `reboot`
     - Capture 100 frames: `v4l2-ctl --stream-count=100 --stream-to=/dev/null`
     - Validate data integrity: `python3 validate_all_frames.py`
     - Record results: Lane speed, bit errors, frame drops
   - Results table:
     | Lane Speed (Gbps) | Bit Errors | Frame Drops | Stable? |
     |------------------|-----------|-------------|---------|
     | 1.0 | 0 | 0 | ✅ Yes |
     | 1.1 | 0 | 0 | ✅ Yes |
     | 1.2 | 0 | 0 | ✅ Yes |
     | 1.25 | ? | ? | ? |

4. **Maximum Stable Lane Speed Identification** (Day 14)
   - Determine maximum speed with BER <10^-12 (zero errors in 100 frames)
   - If 1.25 Gbps is stable: Document as maximum → 4-lane aggregate = 5.0 Gbps
   - If <1.25 Gbps: Document actual maximum, calculate revised Target tier throughput

5. **Maximum Tier Feasibility Test** (Day 15, Optional)
   - Configure: 3072×3072, 30 fps (4.53 Gbps required)
   - Calculate: Required lane speed = 4.53 Gbps / 4 lanes / 0.75 (CSI-2 efficiency) = 1.51 Gbps/lane
   - If maximum stable lane speed <1.51 Gbps/lane: Maximum tier NOT feasible at 30 fps
   - Fallback: Test 3072×3072 at 20 fps (3.02 Gbps) or 15 fps (2.26 Gbps)

**Deliverables**:
- Throughput measurement: 2.01 Gbps at Target tier (2048×2048, 30 fps)
- Lane speed characterization: Maximum stable speed documented
- GO/NO-GO recommendation: Based on ≥1.58 Gbps threshold

**Risks**:
- Throughput <1.58 Gbps → NO-GO, triggers architecture review
- Lane speed <1.0 Gbps → NO-GO, requires external D-PHY PHY chip

---

### Phase 5: Signal Integrity Validation (Optional, W26, Days 16-17)

**Objective**: Measure D-PHY electrical characteristics and validate eye diagram

**Prerequisites**: Logic analyzer with MIPI D-PHY decode capability (Total Phase Promira or equivalent)

**Tasks**:

1. **Logic Analyzer Setup** (Day 16)
   - Connect logic analyzer probes to D-PHY lane 0 (data lane) at FPC cable midpoint
   - Configure analyzer:
     - Protocol: MIPI D-PHY
     - Lane speed: 1.0-1.25 Gbps (match FPGA configuration)
     - Trigger: Frame start (FS packet)
   - Capture waveform: 100 frames minimum

2. **Eye Diagram Analysis** (Day 16)
   - Export eye diagram from logic analyzer
   - Measure:
     - Vertical opening (mV): Should be ≥200 mV (MIPI D-PHY spec)
     - Horizontal opening (UI): Should be ≥0.5 UI (400 ps at 1.25 Gbps)
     - Rise/fall time (ps): Should be ≤100 ps
   - Screenshot eye diagram for PoC report

3. **Signal Integrity Margin Analysis** (Day 17)
   - Calculate margin:
     - Vertical margin = (Measured opening - 200 mV) / 200 mV × 100%
     - Horizontal margin = (Measured opening - 0.5 UI) / 0.5 UI × 100%
   - Target: >20% margin for production robustness
   - If margin <20%: Recommend PCB layout optimization or shorter cable

4. **Cable Length Characterization (Optional)** (Day 17)
   - Test 5 cm, 10 cm, 15 cm cable lengths
   - For each length:
     - Capture eye diagram
     - Measure bit error rate (BER) over 1000 frames
     - Document: Cable length, eye opening, BER
   - Determine maximum reliable length (BER <10^-12)

**Deliverables**:
- Eye diagram screenshot (vertical/horizontal opening annotated)
- SI margin analysis report
- Cable length characterization (if tested)

**Note**: If logic analyzer is unavailable, data integrity tests (Phase 3-4) provide functional validation. Eye diagram is supplementary.

---

### Phase 6: PoC Report and GO/NO-GO Decision (W26, Days 18-19)

**Objective**: Compile test results and make GO/NO-GO recommendation

**Tasks**:

1. **Data Compilation** (Day 18)
   - Aggregate test results:
     - FPGA resource utilization (LUTs, BRAMs)
     - SoC frame capture success rate
     - Data integrity (bit errors across all tests)
     - Throughput measurements (Minimum, Target, Maximum tiers)
     - Lane speed characterization (maximum stable speed)
     - Signal integrity (eye diagram, if available)
   - Create tables and charts:
     - Throughput vs lane speed
     - Bit error rate vs cable length (if tested)

2. **GO/NO-GO Analysis** (Day 18)
   - Check GO criteria:
     - ✅ Measured throughput ≥1.58 Gbps (70% of Target tier)?
     - ✅ Zero data corruption (bit errors) in 1000 frames?
     - ✅ Signal integrity validated (eye diagram or functional test)?
     - ✅ SoC CSI-2 receiver successfully decodes packets?
   - If all ✅: Recommend **GO** (proceed to M1)
   - If any ❌: Recommend **NO-GO** (architecture review)

3. **Risk Assessment (NO-GO Scenario)** (Day 18)
   - If throughput <1.58 Gbps:
     - Option A: External D-PHY PHY chip (e.g., TI DLPC3439) for 2.5 Gbps/lane
       - Cost: ~$50 per unit
       - Schedule: +2 weeks for integration
     - Option B: Reduce Target tier to 1024×1024 or 1536×1536
       - Cost: $0
       - Impact: Reduced clinical imaging capability
     - Option C: Alternative SoC platform with higher CSI-2 receiver performance
       - Cost: ~$200 per unit
       - Schedule: +4 weeks for re-evaluation and driver development

4. **PoC Report Writing** (Day 19)
   - Structure:
     - Executive Summary (GO/NO-GO decision on page 1)
     - Test Setup (hardware, FPGA configuration, SoC configuration)
     - Test Results (throughput, data integrity, lane speed, SI)
     - Analysis (GO criteria evaluation, risk assessment)
     - Recommendations (next steps for M1, or architecture alternatives)
     - Appendices (test logs, waveforms, validation scripts)
   - Page count: 15-20 pages
   - Deliverable: `POC_Report_M0.5_CSI2_YYYYMMDD.pdf`

5. **Stakeholder Review** (Day 19)
   - Present PoC results to project team:
     - System architect
     - FPGA lead
     - SoC firmware lead
     - Project manager
   - Q&A session: Address questions on test methodology, result interpretation
   - Decision: Stakeholder approval for GO or NO-GO

**Deliverables**:
- PoC Test Report (PDF, 15-20 pages)
- GO/NO-GO recommendation with justification
- Stakeholder sign-off

---

## Technical Approach

### FPGA CSI-2 TX Architecture

**Block Diagram**:
```
[Test Pattern Generator] → [AXI4-Stream FIFO] → [MIPI CSI-2 TX IP] → [OSERDES + LVDS] → FPC Cable
         ↓                                              ↓
   [SPI Control Regs]                          [ILA Debug Probes]
```

**CSI-2 Packet Structure** (RAW16, 2048-pixel line):
```
[Frame Start (FS)] → [Line Start (LS)] → [Pixel Data (4096 bytes)] → [Line End (LE)] → ... → [Frame End (FE)]
```
- FS packet: 4 bytes (sync + VC + data type + frame number)
- LS packet: 4 bytes (sync + VC + data type + line number)
- Pixel data: RAW16 (0x2E), 2048 pixels × 2 bytes = 4096 bytes
- CRC-16: 2 bytes (appended after pixel data)

**Timing Constraints**:
- Input clock: 100 MHz (from board oscillator)
- Pixel clock: 125.83 MHz (for 2048×2048 @ 30 fps)
- CSI-2 byte clock: 125 MHz (lane speed / 8 = 1.0 Gbps / 8)
- D-PHY HS clock: 500 MHz (DDR, 1.0 Gbps / 2)

**Pin Assignments** (example for Artix-7 35T FGG484):
```
# D-PHY clock lane (differential)
set_property PACKAGE_PIN AB12 [get_ports dphy_clk_p]
set_property PACKAGE_PIN AB13 [get_ports dphy_clk_n]
set_property IOSTANDARD LVDS_25 [get_ports dphy_clk_p]

# D-PHY data lane 0 (differential)
set_property PACKAGE_PIN AA10 [get_ports dphy_data_p[0]]
set_property PACKAGE_PIN AA11 [get_ports dphy_data_n[0]]
set_property IOSTANDARD LVDS_25 [get_ports dphy_data_p[0]]

# ... (lanes 1-3 similar)
```

---

### SoC CSI-2 RX Configuration

**i.MX8M Plus CSI-2 Receiver Pipeline**:
```
[D-PHY RX] → [MIPI CSI-2 Packet Parser] → [ISP (optional)] → [DMA] → [DDR4 Memory]
```

**Driver Configuration** (`/etc/modprobe.d/imx8-mipi-csi2.conf`):
```
options imx8_mipi_csi2 debug=1 max_lanes=4
```

**V4L2 Format Configuration**:
```bash
# Set pixel format to RAW16
v4l2-ctl --device /dev/video0 --set-fmt-video=width=2048,height=2048,pixelformat=RG16

# Query current format
v4l2-ctl --device /dev/video0 --get-fmt-video
# Output: Width/Height: 2048x2048, Pixel Format: 'RG16' (16-bit Bayer RGRG/GBGB)
```

**Memory Allocation**:
- Frame buffer size: 2048 × 2048 × 2 bytes = 8.39 MB per frame
- Buffer count: 4 (ping-pong buffering, 2× for double-buffering + 2× spare)
- Total allocation: 4 × 8.39 MB = 33.6 MB reserved in DDR4

---

### Validation Methodology

**Counter Pattern Validation Algorithm**:
```python
def validate_counter_pattern(frame_file, width, height):
    """
    Validates that frame contains sequential counter pattern.
    Expected: pixel[i] = i % 65536 (16-bit wrap-around)
    """
    frame = np.fromfile(frame_file, dtype=np.uint16).reshape(height, width)
    expected = np.arange(width * height, dtype=np.uint16).reshape(height, width)

    # Compare frames
    errors = np.sum(frame != expected)

    # Error locations (for debugging)
    if errors > 0:
        error_indices = np.where(frame != expected)
        for row, col in zip(error_indices[0][:10], error_indices[1][:10]):  # First 10 errors
            print(f"Error at ({row},{col}): expected={expected[row,col]:04X}, actual={frame[row,col]:04X}")

    return errors
```

**Throughput Calculation**:
```python
def calculate_throughput(width, height, fps, num_frames, duration_sec):
    """
    Calculates measured throughput in Gbps.
    """
    bits_per_frame = width * height * 16  # 16-bit pixels
    total_bits = bits_per_frame * num_frames
    throughput_bps = total_bits / duration_sec
    throughput_gbps = throughput_bps / 1e9

    # Expected FPS
    expected_duration = num_frames / fps
    fps_measured = num_frames / duration_sec

    return {
        'throughput_gbps': throughput_gbps,
        'fps_measured': fps_measured,
        'frame_drops': num_frames * (1 - duration_sec / expected_duration)
    }
```

---

## Milestones and Checkpoints

### Checkpoint 1: Hardware Setup Complete (End of W23)
- ✅ FPGA and SoC boards powered on
- ✅ FPC cable connected
- ✅ SPI control channel functional
- **Decision**: If hardware issues, escalate procurement or use alternative boards

---

### Checkpoint 2: FPGA Bitstream Validated (End of W24)
- ✅ CSI-2 TX IP instantiated, bitstream generated
- ✅ LUT utilization <5,000 (24%)
- ✅ ILA captures test pattern (counter increments)
- **Decision**: If timing failure, reduce lane speed or add pipeline stages

---

### Checkpoint 3: SoC Frame Capture Success (Middle of W25)
- ✅ SoC captures 100 frames with zero bit errors
- ✅ Frame drop rate <1%
- **Decision**: If capture fails, debug SoC driver or device tree

---

### Checkpoint 4: Throughput Validated (End of W25)
- ✅ Target tier (2048×2048, 30 fps) achieves ≥1.58 Gbps
- ✅ Lane speed characterization complete
- **Decision**: GO if throughput ≥1.58 Gbps, NO-GO if <1.58 Gbps

---

### Checkpoint 5: PoC Report Complete (End of W26)
- ✅ GO/NO-GO recommendation documented
- ✅ Stakeholder review completed
- **Decision**: Proceed to M1 (GO) or architecture review (NO-GO)

---

## Resource Allocation

### Personnel (W23-W26)

**FPGA Engineer** (1 FTE):
- W23: Hardware setup, JTAG test
- W24: CSI-2 TX IP integration, bitstream generation
- W25: Lane speed characterization, throughput measurement
- W26: Signal integrity validation (if logic analyzer available)

**Embedded Engineer** (0.5 FTE):
- W23: SoC Linux boot, driver verification
- W24-W25: Device tree configuration, frame capture testing
- W26: Validation script development

**System Architect** (0.25 FTE):
- W23: PoC test plan review
- W26: PoC report review, GO/NO-GO decision

**Total Effort**: 1.75 FTE × 3 weeks = 5.25 person-weeks

---

### Equipment

**Required (Must Have)**:
- Xilinx Artix-7 XC7A35T FGG484 evaluation board ($200-400)
- NXP i.MX8M Plus EVK ($400-500)
- MIPI CSI-2 FPC cable, 10 cm ($10-20)
- JTAG programmer (if not on-board) ($100-200)

**Optional (Nice to Have)**:
- Logic analyzer with MIPI D-PHY decode ($5,000-20,000)
- Oscilloscope with D-PHY probe ($10,000-30,000)

**Total Budget**: $700-1,200 (required only), $6,000-50,000 (with optional SI tools)

---

### Software/Licenses

- AMD Vivado HL Design Edition (annual subscription, $3,000/year, includes CSI-2 IP)
- NXP i.MX8M Plus Linux BSP (free, Yocto Scarthgap 5.0 LTS, Variscite BSP imx-6.6.52-2.2.0-v1.3)
- Python 3.10+ with NumPy (free)

---

## Next Steps After PoC

### If GO (Proceed to M6-Final)

**Immediate Actions (W27)**:
- Update Architecture Design Document with validated CSI-2 parameters from PoC
- Confirm FPGA RTL integration matches PoC results (lane speed, throughput)
- Proceed to HIL testing and system validation (W27-W28)

**M6-Final Milestone (W28)**:
- System validation with real panel frame acquisition
- Full pipeline verified: FPGA -> SoC -> Host
- Production readiness assessment

---

### If NO-GO (Architecture Review)

**Immediate Actions (W27-W28)**:
- Evaluate alternatives:
  - **Option A**: External D-PHY PHY chip (TI DLPC3439, ~$50/unit, +2 weeks)
  - **Option B**: Alternative SoC platform (Raspberry Pi CM4, NVIDIA Jetson Nano, +4 weeks)
  - **Option C**: Reduce performance tier (1536x1536 or lower, no schedule impact)
- Update project plan with revised timeline and budget
- Stakeholder approval for chosen alternative

**Revised PoC** (schedule extension required):
- Re-run PoC with alternative solution
- Validate GO criteria again

---

## Traceability

**Implements**:
- SPEC-POC-001 Requirements (REQ-POC-001 through REQ-POC-019)
- SPEC-POC-001 Acceptance Criteria (AC-001 through AC-008)

**Depends On**:
- SPEC-ARCH-001 (M0 P0 decisions: CSI-2 selected, Target tier defined)
- X-ray_Detector_Optimal_Project_Plan.md Section 5.4 Phase PoC

**Delivers To**:
- M1 Architecture Design Document (if GO)
- Architecture Review (if NO-GO)

---

**Plan Version**: 1.0.1
**Created**: 2026-02-17
**Updated**: 2026-02-17
**Author**: MoAI Agent (manager-spec)
**Reviewer**: spec-fpga (doc-approval-sprint)
**Changes**: Timeline W3-W6→W23-W26, status planned→approved, RAW16 0x2C→0x2E, GO threshold aligned to 1.58 Gbps, Linux/Yocto references corrected, Next Steps updated for post-implementation context

🗿 MoAI <email@mo.ai.kr>
