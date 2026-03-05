###############################################################################
# Xilinx Design Constraints (.xdc)
# Source: detector_config.yaml
# Generated: 2026-02-18 10:23:11 UTC
#
# WARNING: This file is auto-generated. Do not edit manually.
#          Any changes will be overwritten on regeneration.
###############################################################################

###############################################################################
# Clock Constraints
###############################################################################

# SPI slave interface clock (input from SoC)
create_clock -period 20.000 -name clk_spi [get_ports clk_spi]

# CSI-2 byte clock (from MIPI D-PHY IP)
# Data rate: 1600 Mbps (4 lanes @ 400 Mbps/lane)
# Byte clock: 200.00 MHz
create_clock -period 5.000 -name csi2_byte_clk [get_pins "mipi_dphy_rx_inst/clk_out_byte"]

# System clock (100 MHz oscillator)
create_clock -period 10.000 -name clk_sys [get_ports clk_sys]

###############################################################################
# SPI Interface Timing Constraints
###############################################################################

# SPI slave interface I/O delays
set_input_delay -clock clk_spi -max 2.00 [get_ports {spi_miso spi_mosi spi_sclk}]
set_input_delay -clock clk_spi -min -1.00 [get_ports {spi_miso spi_mosi spi_sclk}]

set_output_delay -clock clk_spi -max 1.00 [get_ports spi_miso]
set_output_delay -clock clk_spi -min 0.0 [get_ports spi_miso]

# SPI chip select is asynchronous (false path)
set_false_path -from [get_ports spi_csn] -to [all_registers]

###############################################################################
# CSI-2 MIPI D-PHY Timing Constraints
###############################################################################

# CSI-2 data type: RAW16 (0x2C)
# Virtual channel: VC0
# Lane count: 4

# Maximum data path delay (byte clock period: 5000.000 ns)
set_max_delay -from [get_cells "*csi2*"] -to [get_cells "*csi2*"] -datapath_only 5000.000

# False paths for D-PHY lane inputs (handled internally by IP)
set_false_path -from [get_ports lane*_clk] -to [all_registers]

###############################################################################
# I/O Location Constraints
###############################################################################

# WARNING: Pin locations below are placeholders.
#         Update with actual FPGA pin numbers from your board schematic.

# SPI Interface (Bank 15, 3.3V)
# set_property LOC IO_L1P ... [get_ports spi_sclk]
# set_property LOC IO_L2P ... [get_ports spi_mosi]
# set_property LOC IO_L3P ... [get_ports spi_miso]
# set_property LOC IO_L4P ... [get_ports spi_csn]

# CSI-2 MIPI D-PHY (Bank 34, HP I/O)
# set_property LOC IO_L1P_CC ... [get_ports mipi_dphy_clk_n]
# set_property LOC IO_L1N_CC ... [get_ports mipi_dphy_clk_p]
# set_property LOC IO_L2P ... [get_ports mipi_dphy_lane0_d_n]
# set_property LOC IO_L2N ... [get_ports mipi_dphy_lane0_d_p]
# set_property LOC IO_L3P ... [get_ports mipi_dphy_lane1_d_n]
# set_property LOC IO_L3N ... [get_ports mipi_dphy_lane1_d_p]

# I/O Standards
set_property IOSTANDARD LVCMOS33 [get_ports spi*]
set_property IOSTANDARD MIPI_DPHY_HS [get_ports mipi_dphy_*]

# Pull-up/Pull-down constraints
set_property PULLUP true [get_ports spi_csn]
