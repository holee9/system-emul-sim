using ConfigConverter.Models;

namespace ConfigConverter.Converters;

/// <summary>
/// Converts DetectorConfig to Xilinx FPGA constraints (.xdc format).
/// Implements REQ-TOOLS-020: Convert detector_config.yaml to FPGA constraints.
/// </summary>
public class XdcConverter
{
    /// <summary>
    /// Converts DetectorConfig to XDC constraint file content.
    /// </summary>
    /// <param name="config">Detector configuration</param>
    /// <returns>XDC file content as string</returns>
    /// <exception cref="ArgumentNullException">Thrown when config is null</exception>
    public string Convert(DetectorConfig config)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        var lines = new List<string>();

        // Header
        lines.Add("###############################################################################");
        lines.Add("# Xilinx Design Constraints (.xdc)");
        lines.Add("# Source: detector_config.yaml");
        lines.Add($"# Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        lines.Add("#");
        lines.Add("# WARNING: This file is auto-generated. Do not edit manually.");
        lines.Add("#          Any changes will be overwritten on regeneration.");
        lines.Add("###############################################################################");
        lines.Add(string.Empty);

        // Clock Constraints
        AddClockConstraints(lines, config);

        // SPI Timing Constraints
        AddSpiTimingConstraints(lines, config);

        // CSI-2 Timing Constraints
        AddCsi2TimingConstraints(lines, config);

        // I/O Constraints
        AddIoConstraints(lines, config);

        return string.Join(Environment.NewLine, lines);
    }

    private void AddClockConstraints(List<string> lines, DetectorConfig config)
    {
        lines.Add("###############################################################################");
        lines.Add("# Clock Constraints");
        lines.Add("###############################################################################");
        lines.Add(string.Empty);

        // SPI Clock (from SoC)
        var spiPeriodNs = 1000.0 / (config.Fpga.Spi.ClockHz / 1_000_000.0);
        lines.Add("# SPI slave interface clock (input from SoC)");
        lines.Add($"create_clock -period {spiPeriodNs:F3} -name clk_spi [get_ports clk_spi]");
        lines.Add(string.Empty);

        // CSI-2 Byte Clock
        // byte_clock = (lane_speed_mbps * lane_count) / 8
        var csi2Config = config.Fpga.DataInterface.Csi2;
        var csi2DataRateMbps = csi2Config.LaneSpeedMbps * csi2Config.LaneCount;
        var csi2ByteClockMhz = csi2DataRateMbps / 8.0;
        var csi2ByteClockPeriodNs = 1000.0 / csi2ByteClockMhz;
        lines.Add("# CSI-2 byte clock (from MIPI D-PHY IP)");
        lines.Add($"# Data rate: {csi2DataRateMbps} Mbps ({csi2Config.LaneCount} lanes @ {csi2Config.LaneSpeedMbps} Mbps/lane)");
        lines.Add($"# Byte clock: {csi2ByteClockMhz:F2} MHz");
        lines.Add($"create_clock -period {csi2ByteClockPeriodNs:F3} -name csi2_byte_clk [get_pins \"mipi_dphy_rx_inst/clk_out_byte\"]");
        lines.Add(string.Empty);

        // System Clock (assume 100 MHz for Artix-7)
        lines.Add("# System clock (100 MHz oscillator)");
        lines.Add("create_clock -period 10.000 -name clk_sys [get_ports clk_sys]");
        lines.Add(string.Empty);
    }

    private void AddSpiTimingConstraints(List<string> lines, DetectorConfig config)
    {
        lines.Add("###############################################################################");
        lines.Add("# SPI Interface Timing Constraints");
        lines.Add("###############################################################################");
        lines.Add(string.Empty);

        // SPI input/output delays based on mode
        var spiSetupNs = 2.0; // Typical setup time
        var spiHoldNs = 1.0;   // Typical hold time

        lines.Add("# SPI slave interface I/O delays");
        lines.Add($"set_input_delay -clock clk_spi -max {spiSetupNs:F2} [get_ports {{spi_miso spi_mosi spi_sclk}}]");
        lines.Add($"set_input_delay -clock clk_spi -min -{spiHoldNs:F2} [get_ports {{spi_miso spi_mosi spi_sclk}}]");
        lines.Add(string.Empty);

        // SPI output delay
        var spiOutputDelayNs = 1.0;
        lines.Add($"set_output_delay -clock clk_spi -max {spiOutputDelayNs:F2} [get_ports spi_miso]");
        lines.Add($"set_output_delay -clock clk_spi -min 0.0 [get_ports spi_miso]");
        lines.Add(string.Empty);

        // SPI CSn asynchronous (false path from clock)
        lines.Add("# SPI chip select is asynchronous (false path)");
        lines.Add("set_false_path -from [get_ports spi_csn] -to [all_registers]");
        lines.Add(string.Empty);
    }

    private void AddCsi2TimingConstraints(List<string> lines, DetectorConfig config)
    {
        lines.Add("###############################################################################");
        lines.Add("# CSI-2 MIPI D-PHY Timing Constraints");
        lines.Add("###############################################################################");
        lines.Add(string.Empty);

        // CSI-2 data type
        var csi2Config = config.Fpga.DataInterface.Csi2;
        var dataTypeCode = csi2Config.DataType.ToUpper() switch
        {
            "RAW16" => "0x2C",
            "RAW14" => "0x2D",
            _ => "0x2C"
        };

        lines.Add($"# CSI-2 data type: {csi2Config.DataType} ({dataTypeCode})");
        lines.Add($"# Virtual channel: VC{csi2Config.VirtualChannel}");
        lines.Add($"# Lane count: {csi2Config.LaneCount}");
        lines.Add(string.Empty);

        // Max delay for CSI-2 data path
        var csi2MaxDelayNs = 1000.0 / ((csi2Config.LaneSpeedMbps * csi2Config.LaneCount) / 8000.0);
        lines.Add($"# Maximum data path delay (byte clock period: {csi2MaxDelayNs:F3} ns)");
        lines.Add($"set_max_delay -from [get_cells \"*csi2*\"] -to [get_cells \"*csi2*\"] -datapath_only {csi2MaxDelayNs:F3}");
        lines.Add(string.Empty);

        // False path for lane clock inputs (handled by D-PHY IP)
        lines.Add("# False paths for D-PHY lane inputs (handled internally by IP)");
        lines.Add("set_false_path -from [get_ports lane*_clk] -to [all_registers]");
        lines.Add(string.Empty);
    }

    private void AddIoConstraints(List<string> lines, DetectorConfig config)
    {
        lines.Add("###############################################################################");
        lines.Add("# I/O Location Constraints");
        lines.Add("###############################################################################");
        lines.Add(string.Empty);

        // Note: Actual pin locations are board-specific
        // This template shows structure - actual pins must be assigned per board
        lines.Add("# WARNING: Pin locations below are placeholders.");
        lines.Add("#         Update with actual FPGA pin numbers from your board schematic.");
        lines.Add(string.Empty);

        lines.Add("# SPI Interface (Bank 15, 3.3V)");
        lines.Add("# set_property LOC IO_L1P ... [get_ports spi_sclk]");
        lines.Add("# set_property LOC IO_L2P ... [get_ports spi_mosi]");
        lines.Add("# set_property LOC IO_L3P ... [get_ports spi_miso]");
        lines.Add("# set_property LOC IO_L4P ... [get_ports spi_csn]");
        lines.Add(string.Empty);

        lines.Add("# CSI-2 MIPI D-PHY (Bank 34, HP I/O)");
        lines.Add("# set_property LOC IO_L1P_CC ... [get_ports mipi_dphy_clk_n]");
        lines.Add("# set_property LOC IO_L1N_CC ... [get_ports mipi_dphy_clk_p]");
        lines.Add("# set_property LOC IO_L2P ... [get_ports mipi_dphy_lane0_d_n]");
        lines.Add("# set_property LOC IO_L2N ... [get_ports mipi_dphy_lane0_d_p]");
        lines.Add("# set_property LOC IO_L3P ... [get_ports mipi_dphy_lane1_d_n]");
        lines.Add("# set_property LOC IO_L3N ... [get_ports mipi_dphy_lane1_d_p]");
        lines.Add(string.Empty);

        // I/O standard constraints
        lines.Add("# I/O Standards");
        lines.Add("set_property IOSTANDARD LVCMOS33 [get_ports spi*]");
        lines.Add("set_property IOSTANDARD MIPI_DPHY_HS [get_ports mipi_dphy_*]");
        lines.Add(string.Empty);

        // Pull constraints
        lines.Add("# Pull-up/Pull-down constraints");
        lines.Add("set_property PULLUP true [get_ports spi_csn]");
        lines.Add(string.Empty);
    }
}
