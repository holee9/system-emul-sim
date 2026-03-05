//==============================================================================
// Top-Level Module: CSI-2 Detector Panel System
//==============================================================================
// File: csi2_detector_top.sv
// Author: FPGA RTL Developer
// Date: 2026-02-18
// Version: 1.0.0
//
// Description:
//  Top-level module integrating all FPGA RTL blocks for the X-ray Detector
//  Panel System. Instantiates Panel Scan FSM, Line Buffer, CSI-2 TX Wrapper,
//  SPI Slave, and Protection Logic.
//
// Target Device: Xilinx Artix-7 XC7A35T-FGG484
//
//==============================================================================

module csi2_detector_top (
    //==========================================================================
    // Clocks and Resets
    //==========================================================================
    input  logic        clk_100mhz,      // 100 MHz board oscillator
    input  logic        clk_roic,        // ROIC master clock (variable)
    input  logic        rst_n,           // Global reset (active low)

    //==========================================================================
    // SPI Interface (from SoC)
    //==========================================================================
    input  logic        spi_sclk,
    input  logic        spi_mosi,
    output logic        spi_miso,
    input  logic        spi_cs_n,

    //==========================================================================
    // D-PHY CSI-2 Output (to SoC)
    //==========================================================================
    output logic        dphy_clk_p,
    output logic        dphy_clk_n,
    output logic [3:0]  dphy_data_p,
    output logic [3:0]  dphy_data_n,

    //==========================================================================
    // Panel Control Outputs
    //==========================================================================
    output logic        gate_on,
    output logic        roic_clk,
    output logic        roic_sync,

    //==========================================================================
    // ROIC Data Input (LVDS)
    //==========================================================================
    input  logic        roic_data_p,
    input  logic        roic_data_n,

    //==========================================================================
    // Debug and Status
    //==========================================================================
    output logic [3:0]  led,
    output logic        error_n,
    output logic        heartbeat
);

    //==========================================================================
    // Internal Clock Generation (MMCM/PLL)
    //==========================================================================
    // In real implementation, this would use Xilinx MMCM primitive
    // For simulation, we generate clocks directly
    logic clk_sys;        // 100 MHz
    logic clk_csi2_byte;  // 125 MHz
    logic clk_dphy_hs;    // 500 MHz

    // Simple clock assignment for simulation (replace with MMCM in synthesis)
    assign clk_sys = clk_100mhz;
    assign clk_csi2_byte = clk_100mhz;  // Will be 125 MHz with MMCM

    //==========================================================================
    // Register Map Internal Signals
    //==========================================================================
    // Control registers (from SPI Slave)
    logic        start_scan, stop_scan, soft_reset;
    logic [1:0]  scan_mode;
    logic        error_clear;

    // Timing registers
    logic [15:0] gate_on_ticks, gate_off_ticks;
    logic [7:0]  roic_settle_ticks, adc_conv_ticks;
    logic [15:0] line_period, frame_blanking;

    // Panel config
    logic [13:0] panel_rows, panel_cols;
    logic [4:0]  bit_depth;

    // CSI-2 config
    logic        csi2_tx_enable;
    logic [1:0]  csi2_lane_count;
    logic        csi2_continuous_clk;

    //==========================================================================
    // Panel Scan FSM
    //==========================================================================
    logic [2:0]  fsm_state;
    logic        idle_flag, busy_flag, error_flag_fsm;
    logic [31:0] frame_counter;
    logic        line_valid, frame_valid;
    logic        line_write_en_fsm;
    logic [11:0] line_write_addr_fsm;
    logic        line_done_fsm;

    panel_scan_fsm panel_fsm (
        .clk_sys(clk_100mhz),
        .rst_n,

        // SPI Register Interface
        .start_scan,
        .stop_scan,
        .soft_reset,
        .scan_mode,
        .error_clear,

        .gate_on_ticks,
        .gate_off_ticks,
        .roic_settle_ticks,
        .adc_conv_ticks,

        .panel_rows,
        .panel_cols,

        // Status
        .fsm_state,
        .idle_flag,
        .busy_flag,
        .error_flag(error_flag_fsm),

        // Frame Counter
        .frame_counter,

        // Panel Control
        .gate_on,
        .roic_sync,

        // Line Buffer Interface
        .line_write_en(line_write_en_fsm),
        .line_write_addr(line_write_addr_fsm),
        .line_done(line_done_fsm),

        // Error Inputs (from Protection Logic)
        .timeout_error(),
        .overflow_error(),
        .roic_fault(),
        .dphy_error(),
        .watchdog_error(),
        .config_error()
    );

    // Assign unused outputs
    assign line_valid = 1'b0;
    assign frame_valid = 1'b0;

    //==========================================================================
    // Line Buffer
    //==========================================================================
    logic        overflow_flag;
    logic        bank_sel_wr, bank_sel_rd;
    logic [11:0] buffer_bank;

    assign buffer_bank = {11'h0, bank_sel_wr};

    // Dummy write data for simulation
    logic [15:0] pixel_data;
    assign pixel_data = 16'h1234;

    line_buffer line_buf (
        .clk_roic,
        .rst_n,

        // Write Interface
        .write_en(line_write_en_fsm),
        .write_data(pixel_data),
        .write_addr(),
        .write_ready(),

        // Read Interface
        .clk_csi2: clk_csi2_byte,
        .rst_csi2_n: rst_n,
        .read_en: 1'b0,
        .read_data(),
        .read_addr(),
        .read_valid(),

        // Control
        .line_done: line_done_fsm,
        .line_width: panel_cols,

        // Status
        .overflow_flag,
        .bank_sel_wr,
        .bank_sel_rd
    );

    //==========================================================================
    // CSI-2 TX Wrapper
    //==========================================================================
    logic        csi2_link_up, csi2_tx_ok;
    logic [15:0] tx_frame_count, tx_error_count;
    logic        crc_match, crc_error;

    csi2_tx_wrapper csi2_tx (
        .clk_csi2_byte,
        .rst_n,

        // AXI4-Stream
        .s_axis_tdata(pixel_data),
        .s_axis_tvalid(line_write_en_fsm),
        .s_axis_tready(),
        .s_axis_tlast(line_done_fsm),
        .s_axis_tuser(1'b0),

        // Control
        .tx_enable: csi2_tx_enable,
        .continuous_clk: csi2_continuous_clk,
        .lane_count: csi2_lane_count,

        // Status
        .link_up: csi2_link_up,
        .tx_active(),
        .frame_count: tx_frame_count,
        .error_count: tx_error_count,

        // CRC
        .crc_match,
        .crc_error
    );

    //==========================================================================
    // SPI Slave
    //==========================================================================
    spi_slave spi (
        // SPI
        .sclk: spi_sclk,
        .mosi: spi_mosi,
        .miso: spi_miso,
        .cs_n: spi_cs_n,

        .clk_sys,
        .rst_n,

        // Register Outputs
        .start_scan,
        .stop_scan,
        .soft_reset,
        .scan_mode,
        .error_clear,

        .gate_on_ticks,
        .gate_off_ticks,
        .roic_settle_ticks,
        .adc_conv_ticks,
        .line_period,
        .frame_blanking,

        .panel_rows,
        .panel_cols,
        .bit_depth,

        .csi2_tx_enable,
        .csi2_lane_count,
        .csi2_continuous_clk,

        // Register Inputs
        .fsm_state,
        .idle_flag,
        .busy_flag,
        .error_flag: error_flag_fsm || error_flag_prot,
        .buffer_bank,
        .frame_counter,
        .csi2_link_up,
        .csi2_tx_ok,
        .overflow_flag,

        .timeout_flag(),
        .roic_fault_flag(),
        .dphy_error_flag(),
        .config_error_flag(),
        .watchdog_flag(),
        .overexposure_flag()
    );

    //==========================================================================
    // Protection Logic
    //==========================================================================
    logic        error_flag_prot;
    logic [7:0]  error_flags_reg;
    logic        safe_state;
    logic        gate_safe, csi2_disable, buffer_disable;

    protection_logic protection (
        .clk_sys(clk_100mhz),
        .rst_n,

        // Error Inputs
        .timeout_error(1'b0),
        .overflow_flag,
        .crc_error,
        .overexposure_error(1'b0),
        .roic_fault(1'b0),
        .dphy_error(1'b0),
        .config_error(1'b0),
        .watchdog_expired(1'b0),

        // Control
        .error_clear,
        .heartbeat(spi_cs_n),

        // Status
        .error_flags: error_flags_reg,
        .error_active: error_flag_prot,
        .safe_state,

        // Safe State Outputs
        .gate_safe(),
        .csi2_disable(),
        .buffer_disable()
    );

    assign crc_error = crc_error;  // Connect from CSI-2
    assign overexposure_error = 1'b0;

    //==========================================================================
    // Status LEDs and Outputs
    //==========================================================================
    assign heartbeat = clk_sys;  // Simple heartbeat
    assign error_n = !error_flag_prot;

    // LED mapping
    always_comb begin
        led = 4'b0000;
        led[0] = idle_flag;       // LED0: Idle
        led[1] = busy_flag;       // LED1: Busy
        led[2] = error_flag_fsm || error_flag_prot;  // LED2: Error
        led[3] = safe_state;      // LED3: Safe State
    end

    //==========================================================================
    // D-PHY Outputs (placeholder - requires AMD CSI-2 TX IP)
    //==========================================================================
    // In real implementation, these connect to OSERDES primitives
    assign dphy_clk_p = 1'b0;
    assign dphy_clk_n = 1'b1;
    assign dphy_data_p = 4'b0000;
    assign dphy_data_n = 4'b1111;

    //==========================================================================
    // ROIC Interface Outputs
    //==========================================================================
    assign roic_clk = clk_roic;

endmodule

//==============================================================================
// End of File: csi2_detector_top.sv
//==============================================================================
