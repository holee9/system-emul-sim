# Technical Glossary

**Document Version**: 1.0.0
**Status**: Draft
**Last Updated**: 2026-02-17
**Author**: MoAI Documentation Agent
**Project**: X-ray Detector Panel System

---

## Table of Contents

1. [Hardware Terms](#hardware-terms)
2. [Software Terms](#software-terms)
3. [Protocol Terms](#protocol-terms)
4. [Quality Terms](#quality-terms)
5. [Medical Terms](#medical-terms)
6. [Performance Tiers Reference](#performance-tiers-reference)

---

## Hardware Terms

**BRAM (Block RAM)**
Dedicated on-chip memory blocks in Xilinx FPGAs. Each block provides 36 Kbits of dual-port RAM. The Artix-7 XC7A35T has 50 BRAMs (1,800 Kbits total). Used for frame line buffers, FIFO queues, and lookup tables in the X-ray detector FPGA design.

**Carry Chain**
Dedicated fast-carry logic in Xilinx FPGAs used for arithmetic operations. Carry chains connect adjacent LUTs to implement adders, subtractors, and comparators without routing overhead. Critical for high-speed pixel data arithmetic in the scan controller.

**Clock Domain**
A set of flip-flops driven by a common clock signal. The X-ray detector FPGA has multiple clock domains: pixel clock (panel interface), CSI-2 byte clock (D-PHY), SPI clock, and system clock. Clock domain crossings require synchronization FIFOs or pulse synchronizers.

**Clock Lane**
In MIPI D-PHY, the differential pair that carries the reference clock for all data lanes. The clock lane operates in continuous or non-continuous mode. In the X-ray detector design, the clock lane frequency equals half the data lane bit rate (e.g., 400 MHz for 800 Mbps/lane DDR).

**Cortex-A53**
ARM 64-bit application processor core used in the NXP i.MX8M Plus SoC. The VAR-SOM-MX8M-PLUS has four Cortex-A53 cores running at up to 1.8 GHz. Runs the Yocto Linux system that receives CSI-2 frames from the FPGA and streams them to the Host via 10 GbE UDP.

**CSI-2 (Camera Serial Interface 2)**
MIPI Alliance standard for high-speed serial data transmission between image sensors and processors. Uses differential D-PHY physical layer. The X-ray detector FPGA acts as a CSI-2 transmitter; the i.MX8M Plus acts as the receiver. Supports multiple virtual channels and data types.

**D-PHY**
MIPI Alliance physical layer specification for CSI-2 and DSI interfaces. Uses differential signaling with two operating modes: LP-Mode (low power, single-ended, ≤10 Mbps) for control and HS-Mode (high speed, differential, up to 4.5 Gbps/lane) for data. The X-ray detector uses 4 data lanes plus 1 clock lane.

**Data Lane**
A differential pair in MIPI D-PHY that carries payload data in HS-Mode. The X-ray detector uses 4 data lanes, each capable of 400 Mbps (stable) or 800 Mbps (under debugging). Total throughput = lanes × bit rate = 4 × 800 Mbps = 3.2 Gbps maximum.

**DMA (Direct Memory Access)**
Hardware mechanism for transferring data between peripherals and memory without CPU involvement. On the i.MX8M Plus, the CSI-2 receiver uses DMA to transfer received frames directly to DDR4 memory, where the Linux kernel driver (V4L2) can access them.

**DSP Slice**
Dedicated multiplier-accumulator blocks in Xilinx FPGAs. The Artix-7 XC7A35T has 90 DSP48E1 slices. Used for pixel value arithmetic, CRC computation, and bandwidth calculations in the FPGA design.

**eMMC (Embedded MultiMediaCard)**
Non-volatile flash storage soldered onto the VAR-SOM-MX8M-PLUS. Stores the Yocto Linux image, firmware, and configuration files. The VAR-SOM has 8 GB eMMC by default. The detector_config.yaml and FPGA bitstream are stored here.

**FPGA (Field-Programmable Gate Array)**
Integrated circuit containing programmable logic blocks and interconnects that can be configured after manufacturing. In the X-ray detector, an Artix-7 XC7A35T implements: panel scan controller, CSI-2 TX, SPI slave, and frame formatting logic.

**Gate IC**
Application-specific IC that drives the gate lines of a thin-film transistor (TFT) X-ray detector panel. Controls the sequential activation of TFT rows to read out pixel charge. The FPGA panel scan FSM generates timing signals for the Gate IC.

**HS-Mode (High-Speed Mode)**
MIPI D-PHY operating mode for high-speed data transmission. Uses differential signaling at 80 Mbps to 4.5 Gbps per lane. Entered from LP-Mode via a specific LP-to-HS transition sequence. The X-ray detector operates at 400-800 Mbps/lane in HS-Mode.

**IDELAY**
Programmable input delay primitive in Xilinx 7-series FPGAs. Each IDELAY tap adds approximately 78 ps of delay. Used to align incoming D-PHY data lanes to the clock lane for setup/hold margin. IDELAY calibration is critical for reliable 800 Mbps D-PHY operation.

**i.MX8M Plus**
NXP applications processor SoC (System on Chip) with 4x Cortex-A53 cores, MIPI CSI-2 receiver, LPDDR4 memory interface, PCIe Gen3, and Gigabit Ethernet. Used as the SoC in the VAR-SOM-MX8M-PLUS. Receives frames from the FPGA via CSI-2 and forwards to Host via Ethernet.

**IOB (Input/Output Block)**
Configurable I/O cells at the periphery of an FPGA die. Each IOB contains input/output buffers, flip-flops, and delay elements (IDELAY/ODELAY). The Artix-7 XC7A35T-FGG484 has 250 I/O pins. The D-PHY interface uses differential IOBs (IBUFDS, OBUFDS).

**ISERDES (Input Serial/Deserializer)**
Xilinx 7-series primitive for high-speed serial-to-parallel conversion. Used in the D-PHY receiver to deserialize incoming HS-Mode bit stream (800 Mbps) into parallel 8-bit or 10-bit words at the byte clock rate (100 MHz for 800 Mbps). Critical block for CSI-2 reception.

**LPDDR4 (Low Power Double Data Rate 4)**
High-bandwidth, low-power DRAM standard used in mobile and embedded applications. The VAR-SOM-MX8M-PLUS has 2 GB LPDDR4. Provides the frame buffer memory for received CSI-2 frames before they are streamed to the Host.

**LP-Mode (Low-Power Mode)**
MIPI D-PHY operating mode for control signaling. Uses single-ended signaling at ≤10 Mbps. Used for inter-packet gaps, LP-11 idle state, and turnaround signaling. The D-PHY transitions between LP-Mode and HS-Mode around each CSI-2 packet.

**LUT (Look-Up Table)**
Fundamental programmable logic element in FPGAs. A 6-input LUT implements any Boolean function of 6 variables. The Artix-7 XC7A35T has 20,800 LUTs. The X-ray detector design must stay below 60% LUT utilization (12,480 LUTs) to ensure routing success and timing closure.

**MIPI (Mobile Industry Processor Interface)**
Standards body that defines serial interface specifications for mobile devices. Relevant MIPI standards for this project: CSI-2 (camera interface), D-PHY (physical layer), and C-PHY (alternative physical layer not used here).

**OSERDES (Output Serial/Deserializer)**
Xilinx 7-series primitive for parallel-to-serial conversion. Used in the D-PHY transmitter to serialize parallel pixel data into the high-speed bit stream for CSI-2 TX. Operates at DDR to achieve 800 Mbps from a 400 MHz byte clock.

**Pixel**
Smallest addressable element in an X-ray detector panel. In the target system, each pixel stores 16 bits (65,536 gray levels) of charge proportional to X-ray dose. The 3072×3072 panel contains approximately 9.4 million pixels per frame.

**ROIC (Readout Integrated Circuit)**
Application-specific IC that reads the charge accumulated by each pixel in a flat-panel X-ray detector. Converts analog pixel charge to digital values. The FPGA controls the ROIC via the panel scan FSM and SPI interface.

**SoC (System on Chip)**
Integrated circuit combining processor cores, memory controllers, I/O peripherals, and specialized accelerators. In this project: Variscite VAR-SOM-MX8M-PLUS with NXP i.MX8M Plus at its core.

**SPI (Serial Peripheral Interface)**
Synchronous serial communication protocol with four signals: SCLK (clock), MOSI (master-out-slave-in), MISO (master-in-slave-out), and CS_N (chip select, active low). The i.MX8M Plus acts as SPI master; the FPGA acts as SPI slave for configuration and control.

**SPI Signals**
- **MOSI**: Master Out Slave In - data from SoC to FPGA
- **MISO**: Master In Slave Out - data from FPGA to SoC (status readback)
- **SCLK**: Serial clock, generated by SPI master (SoC). Maximum 50 MHz for this design.
- **CS_N**: Chip Select, active low. Asserted by SoC to begin a transaction.
- **CPOL**: Clock polarity (0 = idle low, 1 = idle high). Design uses CPOL=0.
- **CPHA**: Clock phase (0 = sample on leading edge, 1 = sample on trailing edge). Design uses CPHA=0.

**TFT (Thin-Film Transistor)**
Transistor fabricated on a glass substrate using amorphous silicon, used as a pixel switch in flat-panel X-ray detectors. Each pixel has one TFT that connects the pixel electrode to the column readout line when the corresponding gate line is activated.

**VAR-SOM-MX8M-PLUS**
Variscite System-on-Module based on the NXP i.MX8M Plus. Features 4x Cortex-A53 @ 1.8 GHz, 2 GB LPDDR4, 8 GB eMMC, MIPI CSI-2 receiver (up to 4 lanes), PCIe Gen3, and Gigabit Ethernet. Running Yocto Scarthgap 5.0 LTS with Linux 6.6.52.

**Vivado**
AMD (formerly Xilinx) FPGA design environment for synthesis, implementation, and bitstream generation. Used for the Artix-7 XC7A35T design. Key tools: Synthesis (RTL to netlist), Implementation (place and route), Timing Analysis (setup/hold verification), and Power Analysis.

**XC7A35T-FGG484**
Specific Artix-7 FPGA part number. XC7A = Artix-7 family. 35T = approximately 33,280 logic cells. FGG = fine-pitch BGA package. 484 = 484 ball package (22×22 array). Key resources: 20,800 LUTs, 50 BRAMs, 90 DSP slices, 250 I/O pins.

---

## Software Terms

**BitBake**
Build system used by the Yocto Project. Reads recipe files (.bb) and executes tasks to compile, package, and install software components. The Variscite BSP provides BitBake recipes for the Linux kernel, U-Boot, and hardware-specific drivers for the VAR-SOM-MX8M-PLUS.

**BSP (Board Support Package)**
Collection of software (kernel, bootloader, drivers, device tree) needed to run an operating system on specific hardware. Variscite provides `meta-variscite-bsp` Yocto layer containing BSP for the VAR-SOM-MX8M-PLUS.

**cgroup (Control Group)**
Linux kernel feature for organizing processes into hierarchical groups and limiting their resource usage (CPU, memory, I/O). The frame streaming daemon uses cgroups to guarantee CPU allocation for real-time frame delivery.

**CSI-2 RX Driver**
Linux kernel driver that interfaces with the MIPI CSI-2 receive hardware in the i.MX8M Plus. Part of the V4L2 subsystem. Typically implemented as a V4L2 subdevice driver (e.g., `mxc-mipi-csi2_yav.c` in Variscite BSP) that configures the CSI-2 IP block and exposes it to the media framework.

**daemon**
Background process on Linux that runs continuously without a controlling terminal. The frame streaming service in the SoC firmware runs as a systemd daemon, receiving frames from V4L2 and forwarding them via UDP to the Host.

**DMA (in software context)**
Linux kernel DMA API for managing hardware DMA transfers. The V4L2 subsystem allocates DMA-coherent buffers (via `dma_alloc_coherent()`) that the CSI-2 hardware writes directly. User space accesses these via `mmap()`.

**ioctl**
System call for device-specific I/O operations. V4L2 uses ioctl extensively: `VIDIOC_QUERYCAP` (capabilities), `VIDIOC_S_FMT` (set format), `VIDIOC_REQBUFS` (request buffers), `VIDIOC_STREAMON` (start streaming), `VIDIOC_DQBUF` (dequeue buffer).

**Layer (Yocto)**
Collection of BitBake recipes, configuration files, and patches grouped by functionality. Key layers for this project: `meta-variscite-bsp` (Variscite hardware), `meta-openembedded` (additional packages), `meta-xray-detector` (project-specific customizations).

**mmap**
Memory-mapped I/O system call that maps a file or device memory into the process address space. V4L2 applications use `mmap()` to access frame buffer memory allocated by the kernel driver, enabling zero-copy frame access.

**Recipe (Yocto)**
BitBake recipe file (`.bb`) that describes how to fetch, configure, compile, and install one software component. Example: `linux-variscite_6.6.52.bb` describes how to build the Variscite-patched Linux kernel for the i.MX8M Plus.

**systemd**
System and service manager for Linux. Manages boot process, service dependencies, and daemon lifecycle. The frame streaming daemon is managed by a systemd service unit (`xray-detector.service`) that ensures automatic restart on failure.

**udev**
Linux kernel device manager. Dynamically creates device nodes in `/dev/` when hardware is detected. Used to set up `/dev/videoN` nodes for V4L2 capture devices and configure permissions for the frame streaming daemon.

**V4L2 (Video4Linux2)**
Linux kernel subsystem for video capture devices. Provides a standardized API for camera and image sensor drivers. The CSI-2 receiver driver exposes frames via V4L2 buffer queue. User-space daemon uses V4L2 API to dequeue frames and stream them to the Host.

**V4L2 subdev**
V4L2 sub-device driver model for modular media device configuration. The CSI-2 interface is split into subdevices: image sensor subdev (or FPGA simulator subdev), CSI-2 bridge subdev, and video capture device. Connected via the Media Controller API.

**Yocto**
Open-source project providing tools and metadata for building custom Linux distributions for embedded systems. Used to build the SoC firmware image for the VAR-SOM-MX8M-PLUS. Yocto Scarthgap 5.0 LTS is the target version.

---

## Protocol Terms

**CRC-16 (Cyclic Redundancy Check 16-bit)**
Error detection code appended to each CSI-2 Long Packet. Computed using the polynomial x^16 + x^12 + x^5 + 1 (CRC-CCITT). The FPGA computes CRC over the payload bytes and appends it as the last 2 bytes of each line packet. The SoC driver validates CRC on receive.

**Data Type (DT)**
6-bit field in the CSI-2 packet header identifying the format of the payload data. Key values for this project:
- `0x00`: Frame Start (short packet)
- `0x01`: Frame End (short packet)
- `0x2A`: RAW8
- `0x2B`: RAW10
- `0x2C`: RAW12
- `0x2E`: RAW16 (used for 16-bit detector pixels)

**ECC (Error Correction Code)**
1-byte Hamming code in the CSI-2 packet header that detects double-bit errors and corrects single-bit errors in the 24-bit header (VC + DT + WC). Required by MIPI CSI-2 specification. The FPGA generates ECC; the SoC validates it.

**Frame ID**
Incrementing sequence number embedded in Frame Start packets (VC-specific). Used by the Host to detect dropped frames. If consecutive Frame IDs are not sequential, a frame was lost in transmission. The Host SDK increments an error counter and logs the gap.

**MTU (Maximum Transmission Unit)**
Maximum size of a single network layer packet. Standard Ethernet MTU is 1,500 bytes. Jumbo frames allow up to 9,000 bytes. The X-ray detector UDP streaming protocol uses 8,192-byte payloads with jumbo frames enabled, carrying approximately 4,096 16-bit pixels per UDP packet.

**Packet Sequence**
The order of CSI-2 packets within a frame:
1. Frame Start (short packet, DT=0x00)
2. Line Start × N lines (short packets, DT=0x02, optional)
3. Long Packet × N lines (RAW16 payload per line)
4. Line End × N lines (short packets, DT=0x03, optional)
5. Frame End (short packet, DT=0x01)

**UDP (User Datagram Protocol)**
Connectionless transport protocol. Used for frame streaming from SoC to Host because its low overhead and predictable latency are more important than guaranteed delivery for real-time imaging. The Host application reassembles UDP datagrams into complete frames using the Frame ID and line offset fields.

**Virtual Channel (VC)**
2-bit field in the CSI-2 packet header distinguishing up to 4 independent data streams on the same physical interface. The X-ray detector uses VC0 for frame data. VC1-VC3 are reserved for future diagnostic channels (e.g., FPGA status telemetry).

**Word Count (WC)**
16-bit field in the CSI-2 Long Packet header specifying the number of payload bytes. For RAW16 format: `WC = cols × 2` (2 bytes per pixel). For the 3072-column target: WC = 6,144 bytes per line packet.

---

## Quality Terms

**ANALYZE-PRESERVE-IMPROVE**
The three phases of the DDD (Domain-Driven Development) cycle used for legacy code modification:
- ANALYZE: Understand existing behavior without modifying code
- PRESERVE: Write characterization tests capturing current behavior
- IMPROVE: Make changes incrementally, verifying tests pass after each step

**Characterization Test**
A test that captures and documents the current behavior of existing code, regardless of whether that behavior is "correct." Written during the PRESERVE phase of DDD. Acts as a regression guard preventing unintended behavior changes during refactoring.

**Coverage**
Metric measuring the proportion of code exercised by automated tests. This project targets: RTL line ≥95%, RTL branch ≥90%, RTL FSM 100%, software (all components) ≥85%.

**DDD (Domain-Driven Development)**
Development methodology for modifying legacy code. Uses the ANALYZE-PRESERVE-IMPROVE cycle. Applied in this project when modifying existing code files with less than 50% test coverage.

**EARS (Easy Approach to Requirements Syntax)**
Structured natural language format for writing software requirements. Five patterns: Ubiquitous (system shall), Event-driven (when X, system shall Y), State-driven (while X, system shall Y), Unwanted (system shall not), Optional (where possible, system shall). Used in all SPEC documents.

**Hybrid Mode**
The project's development methodology combining TDD (for new code) and DDD (for legacy code). Configured in `quality.yaml: development_mode: hybrid`. New files use TDD; existing files with <50% coverage use DDD.

**LSP (Language Server Protocol)**
Standard protocol for communication between code editors and language servers providing diagnostics (errors, warnings, type errors). MoAI-ADK uses LSP diagnostics as objective quality gates: zero errors required for run phase; max 10 warnings for sync phase.

**Mutation Testing**
Testing technique that introduces small code changes (mutants) to verify test suite effectiveness. A surviving mutant (not caught by any test) indicates a coverage gap. Target mutation score ≥75% for critical modules. Enabled selectively via `quality.yaml: mutation_testing_enabled`.

**RED-GREEN-REFACTOR**
The three phases of TDD (Test-Driven Development):
- RED: Write a failing test that describes the desired behavior
- GREEN: Write minimal code to make the test pass
- REFACTOR: Improve code quality while keeping tests green

**TDD (Test-Driven Development)**
Development methodology where tests are written before implementation code. Uses the RED-GREEN-REFACTOR cycle. Applied in this project for all new code (new files and new functions).

**TRUST 5**
The five-pillar quality framework for this project:
- **T**ested: ≥85% coverage, TDD/DDD methodology, mutation testing
- **R**eadable: Naming conventions, comment standards, 10-minute comprehension
- **U**nified: Consistent formatting (dotnet format, clang-format, prettier)
- **S**ecured: OWASP compliance, input validation, IEC 62443 alignment
- **T**rackable: Conventional commits, issue references, LSP snapshots

---

## Medical Terms

**Bit Depth**
Number of bits used to represent each pixel value. The X-ray detector uses 16-bit depth (65,536 gray levels). Higher bit depth enables detection of smaller dose differences, improving diagnostic sensitivity.

**DQE (Detective Quantum Efficiency)**
Measure of an X-ray detector's signal-to-noise performance relative to an ideal detector. DQE of 1.0 means perfect noise performance. Modern flat-panel detectors achieve DQE of 0.5-0.8 at 0 lp/mm. Higher DQE enables lower dose examinations.

**Dynamic Range**
Ratio between the maximum and minimum measurable X-ray signal. A 16-bit detector has a theoretical dynamic range of 65,535:1. In practice, electronic noise and saturation limit the effective dynamic range. The X-ray detector design targets >1000:1 usable dynamic range.

**Flat-Field Correction**
Image processing step that compensates for non-uniform pixel sensitivity and gain variations across the detector panel. Requires: offset calibration (dark frame subtraction) and gain calibration (flood field normalization). Applied in the Host software pipeline before display.

**Frame Rate**
Number of complete images captured per second. The target system operates at 15 fps (frames per second) for the full 3072×3072 resolution. Higher frame rates enable fluoroscopy (real-time X-ray video) and reduce motion blur.

**Integration Time**
Duration during which X-ray-generated charge is accumulated in detector pixels before readout. Longer integration time captures more signal but increases motion blur. The FPGA scan controller provides programmable integration time via SPI register.

**MTF (Modulation Transfer Function)**
Spatial frequency response of an imaging system. Describes how well the detector preserves contrast at different spatial frequencies. Measured in line pairs per millimeter (lp/mm). Higher MTF means sharper images.

**Pixel Pitch**
Center-to-center distance between adjacent pixels in a detector array, typically measured in micrometers. A smaller pixel pitch gives higher spatial resolution. The X-ray detector targets a pixel pitch compatible with the 3072×3072 format for the intended clinical application.

**SNR (Signal-to-Noise Ratio)**
Ratio of desired signal power to background noise power. In X-ray imaging, SNR depends on X-ray dose, detector efficiency, and electronic noise. Higher SNR enables lower-dose examinations. Expressed in dB or as a linear ratio.

**X-ray Detector Panel**
Flat-panel image detector that converts X-ray photons to electrical charge and reads out digital pixel values. Consists of: scintillator layer (converts X-rays to visible light), TFT array (pixel switches), ROIC (charge readout), Gate IC (row selection), and FPGA (digital control and data formatting). The system described in this project digitizes frames at up to 15 fps for a 3072×3072 pixel array.

---

## Performance Tiers Reference

| Tier | Resolution | Bit Depth | Frame Rate | Raw Bandwidth | CSI-2 (85%) | Host Link | Status |
|------|-----------|-----------|-----------|--------------|------------|----------|--------|
| Minimum | 1024×1024 | 14-bit | 15 fps | 0.22 Gbps | 0.19 Gbps | 1 GbE | Validated (400M) |
| Intermediate-A | 2048×2048 | 16-bit | 15 fps | 1.01 Gbps | 0.86 Gbps | 1 GbE | Validated (400M) |
| Intermediate-B | 2048×2048 | 16-bit | 30 fps | 2.01 Gbps | 1.71 Gbps | 10 GbE | Requires 800M |
| Target (Final) | 3072×3072 | 16-bit | 15 fps | 2.26 Gbps | 1.92 Gbps | 10 GbE | Requires 800M |

**Raw Bandwidth Formula**: `rows × cols × bit_depth × fps` (bits/s)

**CSI-2 Efficiency Factor**: 85% (accounts for packet headers, ECC, CRC, LP-HS transitions)

---

*Document End*

## Review Record

- Date: 2026-02-17
- Reviewer: manager-quality
- Status: Approved
- TRUST 5: T:5 R:5 U:5 S:5 T:4
- Notes: All hardware specs verified: XC7A35T has 20,800 LUTs, 50 BRAMs, 90 DSP slices. VAR-SOM-MX8M-PLUS entry correctly states Yocto Scarthgap 5.0 LTS with Linux 6.6.52. Performance tiers table bandwidth calculations verified: Minimum 0.22 Gbps, Intermediate-A 1.01 Gbps, Intermediate-B 2.01 Gbps, Target 2.26 Gbps all correct. CSI-2 Data Type hex values (RAW16=0x2E) confirmed accurate.
