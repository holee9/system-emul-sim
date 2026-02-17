# Glossary

**Project**: X-ray Detector Panel System
**Version**: 1.0.0
**Last Updated**: 2026-02-17

---

## A

**ADC (Analog-to-Digital Converter)**
Converts analog voltage from the ROIC to digital pixel values. Resolution determines bit depth (14-bit or 16-bit).

**Artix-7**
Xilinx (AMD) FPGA family used in this project. Specifically the XC7A35T-FGG484, a small-class device with 20,800 LUTs and 50 BRAMs.

**AXI4-Stream**
ARM AMBA interface protocol used for point-to-point data streaming. Used between the line buffer and CSI-2 TX IP inside the FPGA.

---

## B

**Bitstream**
Binary file (`.bit`) that configures the FPGA. Contains the programmed logic, routing, and I/O configuration for the Artix-7 device.

**BRAM (Block RAM)**
Dedicated memory blocks inside the FPGA. Each 36Kb BRAM provides dual-port access. Used for the Ping-Pong line buffer (4 BRAMs for 3072x16-bit dual-bank).

**BSP (Board Support Package)**
Software package providing Linux kernel, drivers, and bootloader for the SoC platform. NXP provides BSP for i.MX8M Plus via Yocto Project.

---

## C

**CDC (Clock Domain Crossing)**
Point where a signal transitions between two different clock domains. Requires synchronization (2-stage FF, Gray coding, or dual-port BRAM) to prevent metastability.

**CRC-16 (Cyclic Redundancy Check, 16-bit)**
Error detection code used in CSI-2 line packets and UDP frame headers. Polynomial: CRC-16/CCITT (0x8408). Detects single-bit and burst errors.

**CSI-2 (Camera Serial Interface 2)**
MIPI Alliance standard for camera-to-processor data transfer. Used as the sole high-speed interface between FPGA and SoC. Supports RAW16 data type on 4 data lanes.

---

## D

**D-PHY (MIPI D-PHY)**
Physical layer specification for MIPI CSI-2. Operates in Low-Power (LP) and High-Speed (HS) modes. In this project, implemented via FPGA OSERDES + LVDS I/O at 400-1250 Mbps/lane.

**DDD (Domain-Driven Development)**
Development methodology used for existing code. Follows the ANALYZE-PRESERVE-IMPROVE cycle: understand existing behavior, write characterization tests, then improve incrementally.

**DDR4 (Double Data Rate 4)**
DRAM technology used by the SoC for frame buffering. The i.MX8M Plus has 4 GB DDR4, storing up to 4 frame buffers (72 MB at Target tier).

**DMA (Direct Memory Access)**
Hardware mechanism that transfers data between peripherals and memory without CPU intervention. Used by the SoC CSI-2 RX driver for zero-copy frame capture.

**DRC (Design Rule Check)**
Vivado verification step that checks the FPGA design against physical and electrical constraints. Must pass with zero critical violations before deployment.

**DTS (Device Tree Source)**
Text file describing hardware configuration for Linux kernel. The ConfigConverter generates DTS overlays for CSI-2 and SPI device configuration on the SoC.

---

## E

**EARS (Easy Approach to Requirements Syntax)**
Requirements writing format used in SPEC documents. Templates: "shall" (ubiquitous), "WHEN...THEN" (event-driven), "WHILE...THEN" (state-driven), "IF...THEN" (optional), "shall not" (unwanted).

---

## F

**FPC (Flexible Printed Circuit)**
Thin flexible cable connecting FPGA CSI-2 TX output to SoC CSI-2 RX input. Typically 10 cm length for board-to-board connection.

**FPGA (Field-Programmable Gate Array)**
Reconfigurable integrated circuit used for real-time data acquisition. Handles panel scan timing, line buffering, and CSI-2 transmission with nanosecond-level precision.

**FSM (Finite State Machine)**
Sequential logic pattern used for control flow. The Panel Scan FSM has six states: IDLE, INTEGRATE, READOUT, LINE_DONE, FRAME_DONE, ERROR.

---

## G

**Gate IC**
Integrated circuit that controls X-ray exposure timing. The FPGA generates gate control signals to turn X-ray exposure on and off with sub-microsecond accuracy.

**Golden Reference**
Software model (FpgaSimulator) that produces bit-exact output for comparison with FPGA RTL simulation. Used to verify correctness of hardware implementation.

---

## H

**HAL (Hardware Abstraction Layer)**
Software layer that provides a uniform API over hardware-specific interfaces. The SoC firmware HAL wraps V4L2, spidev, and socket APIs.

**HIL (Hardware-In-the-Loop)**
Testing methodology where real hardware components are connected to the simulator pipeline. HIL Pattern A tests FPGA+SoC; Pattern B tests the full system.

---

## I

**ILA (Integrated Logic Analyzer)**
Vivado debug tool that captures real-time signal data from the FPGA via JTAG. Used to observe FSM states, pixel data, and error flags during hardware debugging.

**i.MX8M Plus**
NXP SoC with Quad Cortex-A53 (1.8 GHz), native CSI-2 RX, and Linux support. Recommended platform for the SoC Controller layer.

**ISP (Image Signal Processor)**
Hardware block in the SoC that processes camera data (debayering, color correction). Must be bypassed for raw X-ray pixel data pass-through.

---

## J

**JTAG (Joint Test Action Group)**
Debug and programming interface for FPGAs. Uses a 4-wire protocol (TDI, TDO, TMS, TCK) to program bitstreams and connect ILA/VIO debug tools.

---

## L

**Line Buffer**
Dual-bank (Ping-Pong) BRAM structure in the FPGA that captures one line of pixel data while the previous line is being transmitted via CSI-2. Sized for maximum 3072 pixels x 16 bits.

**LP Mode (Low-Power Mode)**
D-PHY idle state where data lanes are in a low-power signaling state. Used between CSI-2 frames and during safe shutdown.

**LUT (Look-Up Table)**
Basic logic element in Xilinx FPGAs. Each LUT implements a combinational function of up to 6 inputs. The XC7A35T has 20,800 LUTs with a budget target of < 60% utilization.

**LVDS (Low-Voltage Differential Signaling)**
I/O standard used for D-PHY high-speed data transmission on Artix-7. LVDS_25 provides 200 mV differential swing.

---

## M

**MIPI (Mobile Industry Processor Interface)**
Industry alliance that defines interface standards for mobile and embedded systems. CSI-2 and D-PHY are MIPI specifications used in this project.

**MMCM (Mixed-Mode Clock Manager)**
Xilinx clock management primitive that generates multiple output clocks from a single input. Generates clk_sys (100 MHz), clk_csi2_byte (125 MHz), and clk_dphy_hs (500 MHz).

**MoAI-ADK**
AI-assisted development framework used for project management, SPEC creation, and code quality enforcement. Provides orchestrated agent workflows.

---

## N

**NIC (Network Interface Card)**
10 GbE PCIe adapter installed in the Host PC for high-throughput frame reception from the SoC.

---

## O

**OSERDES (Output Serializer/Deserializer)**
Xilinx I/O primitive that serializes parallel data for high-speed output. Used for D-PHY lane serialization (10:1 DDR) at up to 1.0-1.25 Gbps on Artix-7.

---

## P

**Ping-Pong Buffer**
Dual-bank memory architecture where one bank is written while the other is read. Provides seamless data flow without stalling either the producer or consumer.

**PoC (Proof of Concept)**
Validation activity at W3-W6 to verify CSI-2 D-PHY interface performance. Must achieve >= 70% of target throughput to proceed.

---

## R

**RAW16**
MIPI CSI-2 data type (0x2C) for 16-bit raw pixel data. Each pixel is transmitted as 2 bytes without any compression or processing.

**ROIC (Readout Integrated Circuit)**
IC that reads analog charge from the X-ray detector panel pixels and converts to digital values via ADC. Output is typically LVDS serial data.

---

## S

**SoC (System on Chip)**
Integrated processor with CPU, memory controller, and peripherals. The NXP i.MX8M Plus SoC serves as the bridge between FPGA and Host PC.

**SPI (Serial Peripheral Interface)**
4-wire synchronous serial protocol used for FPGA register access. Mode 0 (CPOL=0, CPHA=0), up to 50 MHz clock. 32-bit transaction format: 8-bit address + 8-bit R/W + 16-bit data.

**SPEC (Specification)**
Formal requirements document using EARS format. Each SPEC defines requirements, acceptance criteria, dependencies, and risks for a subsystem.

---

## T

**TDD (Test-Driven Development)**
Development methodology for new code. Follows RED-GREEN-REFACTOR cycle: write failing test, write minimal passing code, refactor.

**TIFF (Tagged Image File Format)**
Lossless image format supporting 16-bit grayscale. Primary storage format for X-ray detector frames.

**TRUST 5**
Quality framework with five dimensions: Tested, Readable, Unified, Secured, Trackable. All code contributions must satisfy TRUST 5 criteria.

---

## U

**UDP (User Datagram Protocol)**
Connectionless transport protocol used for frame streaming from SoC to Host. Provides low latency at the cost of no guaranteed delivery (handled by SDK reassembly logic).

---

## V

**V4L2 (Video for Linux 2)**
Linux kernel API for video capture devices. The SoC CSI-2 RX driver uses V4L2 for frame capture with MMAP DMA buffers.

**VC (Virtual Channel)**
CSI-2 multiplexing feature. This project uses VC0 for single-sensor configuration.

**VIO (Virtual I/O)**
Vivado debug tool providing runtime-adjustable inputs and readable outputs via JTAG. Used for manual scan triggering and configuration override during development.

---

## W

**Window/Level**
Medical imaging display technique for mapping 16-bit pixel values to 8-bit display. Window (width) controls contrast; Level (center) controls brightness.

**WNS (Worst Negative Slack)**
Timing analysis metric indicating the tightest timing margin. WNS >= 0 means timing is met; target is WNS >= 1 ns for all clock domains.

---

## X

**XDC (Xilinx Design Constraints)**
Constraint file format for Vivado. Defines timing constraints, pin assignments, and I/O standards. The ConfigConverter generates XDC from `detector_config.yaml`.

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (architect) | Initial glossary |

---
