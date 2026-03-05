//==============================================================================
// SPI Slave - Register Map and Protocol Engine
//==============================================================================
// File: spi_slave.sv
// Author: FPGA RTL Developer
// Date: 2026-02-18
// Version: 1.0.0
//
// Description:
//  SPI Slave implements Mode 0 (CPOL=0, CPHA=0) protocol for register access.
//  32-bit transaction format: 8-bit address + 8-bit R/W flag + 16-bit data.
//  Implements full register map per fpga-design.md Section 6.3.
//
// Requirements Implemented:
//  REQ-FPGA-040: SPI Mode 0 (CPOL=0, CPHA=0), up to 50 MHz
//  REQ-FPGA-041: 32-bit transaction (8-bit addr + 8-bit R/W + 16-bit data)
//  REQ-FPGA-042: Complete register map implementation
//  REQ-FPGA-043: Abort on CS_N deassertion mid-transaction
//  REQ-FPGA-044: Unmapped addresses return 0x0000
//
// Resources (Estimated):
//  LUTs: ~800, FFs: ~352, BRAMs: 0
//
//==============================================================================

module spi_slave (
    // SPI Physical Interface
    input  logic        sclk,           // SPI clock (from SoC master)
    input  logic        mosi,           // Master Out Slave In
    output logic        miso,           // Master In Slave Out
    input  logic        cs_n,           // Chip Select (active low)

    // System Interface
    input  logic        clk_sys,        // System clock (for register access)
    input  logic        rst_n,          // Active-low reset

    // Register Outputs (to other modules)
    output logic        start_scan,     // CONTROL[0]
    output logic        stop_scan,      // CONTROL[1]
    output logic        soft_reset,     // CONTROL[2]
    output logic [1:0]  scan_mode,      // CONTROL[3:2]
    output logic        error_clear,    // CONTROL[4]

    output logic [15:0] gate_on_ticks,     // TIMING_GATE_ON
    output logic [15:0] gate_off_ticks,    // TIMING_GATE_OFF
    output logic [7:0]  roic_settle_ticks, // TIMING_ROIC_SETTLE
    output logic [7:0]  adc_conv_ticks,    // TIMING_ADC_CONV
    output logic [15:0] line_period,       // TIMING_LINE_PERIOD
    output logic [15:0] frame_blanking,    // TIMING_FRAME_BLANK

    output logic [13:0] panel_rows,        // CONFIG_ROWS
    output logic [13:0] panel_cols,        // CONFIG_COLS
    output logic [4:0]  bit_depth,         // BIT_DEPTH

    output logic        csi2_tx_enable,    // CSI2_CONTROL[2]
    output logic [1:0]  csi2_lane_count,   // CSI2_CONTROL[1:0]
    output logic        csi2_continuous_clk, // CSI2_CONTROL[3]

    // Register Inputs (from other modules)
    input  logic [2:0]  fsm_state,         // Current FSM state
    input  logic        idle_flag,
    input  logic        busy_flag,
    input  logic        error_flag,
    input  logic [11:0] buffer_bank,       // Current active bank

    input  logic [31:0] frame_counter,     // 32-bit frame counter

    input  logic        csi2_link_up,      // CSI-2 link status
    input  logic        csi2_tx_ok,        // Last TX successful

    input  logic        overflow_flag,     // Line buffer overflow

    // Status Flags (read-only, mapped to ERROR_FLAGS)
    input  logic        timeout_flag,
    input  logic        roic_fault_flag,
    input  logic        dphy_error_flag,
    input  logic        config_error_flag,
    input  logic        watchdog_flag,
    input  logic        overexposure_flag
);

    //==========================================================================
    // SPI Transaction Parameters
    //==========================================================================
    localparam ADDR_BITS = 8;
    localparam RW_BITS = 8;
    localparam DATA_BITS = 16;
    localparam TOTAL_BITS = ADDR_BITS + RW_BITS + DATA_BITS;  // 32 bits

    //==========================================================================
    // SPI Shift Register (SDR - Single Data Rate)
    //==========================================================================
    logic [31:0] shift_reg;
    logic [7:0]  bit_count;

    always_ff @(posedge sclk or negedge cs_n) begin
        if (!cs_n) begin
            // Chip select deasserted: reset shift register
            shift_reg <= 32'h0;
            bit_count <= 8'h0;
        end else begin
            // Shift in MOSI on each clock edge
            shift_reg <= {shift_reg[30:0], mosi};
            bit_count <= bit_count + 1'b1;
        end
    end

    // Decode transaction fields
    logic [7:0]  spi_addr;
    logic [7:0]  spi_rw;
    logic [15:0] spi_wdata;

    assign spi_addr = shift_reg[31:24];
    assign spi_rw = shift_reg[23:16];
    assign spi_wdata = shift_reg[15:0];

    //==========================================================================
    // Transaction Valid Detection
    //==========================================================================
    logic transaction_valid;
    assign transaction_valid = (bit_count == TOTAL_BITS);

    //==========================================================================
    // Register File
    //==========================================================================
    logic [15:0] reg_file [256];  // 256 registers x 16-bit

    // Initialize register file with defaults
    initial begin
        // Identification Registers (0x00 - 0x1F)
        reg_file[8'h00] = 16'hD7E0;  // DEVICE_ID (upper)
        reg_file[8'h01] = 16'h0001;  // DEVICE_ID_LO (lower)
        reg_file[8'h10] = 16'h0000;  // ILA_CAPTURE_0
        reg_file[8'h11] = 16'h0000;  // ILA_CAPTURE_1
        reg_file[8'h12] = 16'h0000;  // ILA_CAPTURE_2
        reg_file[8'h13] = 16'h0000;  // ILA_CAPTURE_3

        // Status Register (0x20) - Read Only, computed dynamically
        reg_file[8'h20] = 16'h0000;

        // Control Register (0x21) - Read/Write
        reg_file[8'h21] = 16'h0000;

        // Frame Counter (0x30 - 0x31) - Read Only, computed dynamically
        reg_file[8'h30] = 16'h0000;
        reg_file[8'h31] = 16'h0000;

        // Panel Config (0x40 - 0x43)
        reg_file[8'h40] = 16'd2048;  // CONFIG_ROWS (default)
        reg_file[8'h41] = 16'd2048;  // CONFIG_COLS (default)
        reg_file[8'h42] = 16'd16;    // BIT_DEPTH (default)
        reg_file[8'h43] = 16'h002E;  // PIXEL_FORMAT (RAW16)

        // Timing Config (0x50 - 0x55)
        reg_file[8'h50] = 16'd1000;  // TIMING_GATE_ON (1 us)
        reg_file[8'h51] = 16'd100;   // TIMING_GATE_OFF
        reg_file[8'h52] = 16'd10;    // TIMING_ROIC_SETTLE
        reg_file[8'h53] = 16'd5;     // TIMING_ADC_CONV
        reg_file[8'h54] = 16'd16;    // TIMING_LINE_PERIOD
        reg_file[8'h55] = 16'd500;   // TIMING_FRAME_BLANK

        // CSI-2 Config (0x60 - 0x61)
        reg_file[8'h60] = 16'h0000;  // CSI2_LANE_SPEED
        reg_file[8'h61] = 16'h0005;  // CSI2_CONTROL (4-lane, TX enable)

        // Error Flags (0x80) - Read Only, computed dynamically
        reg_file[8'h80] = 16'h0000;

        // Data IF Status (0x90, 0x94, 0x98)
        reg_file[8'h90] = 16'h0000;
        reg_file[8'h94] = 16'h0000;
        reg_file[8'h98] = 16'h0000;

        // Version (0xF4, 0xF8)
        reg_file[8'hF4] = 16'h0100;  // VERSION 1.0.0
        reg_file[8'hF8] = 16'h0218;  // BUILD_DATE (02/18)
    end

    //==========================================================================
    // Read Mux (Dynamic Status Registers)
    //==========================================================================
    logic [15:0] read_data;

    always_comb begin
        case (spi_addr)
            8'h20: begin  // STATUS register
                read_data = {1'b0, buffer_bank, 3'b0, fsm_state, error_flag, busy_flag, idle_flag, 8'b0};
            end
            8'h30: begin  // FRAME_COUNT_LO
                read_data = frame_counter[15:0];
            end
            8'h31: begin  // FRAME_COUNT_HI
                read_data = frame_counter[31:16];
            end
            8'h80: begin  // ERROR_FLAGS
                read_data = {watchdog_flag, config_error_flag, dphy_error_flag,
                             roic_fault_flag, overexposure_flag,
                             overflow_flag, timeout_flag, 8'b0};
            end
            8'h90: begin  // DATA_IF_STATUS
                read_data = {7'b0, csi2_tx_ok, csi2_link_up, 8'b0};
            end
            default: begin
                read_data = reg_file[spi_addr];
            end
        endcase
    end

    //==========================================================================
    // MISO Output (Shift out read data during read transaction)
    //==========================================================================
    logic [15:0] read_shift_reg;
    logic        read_active;

    always_ff @(posedge sclk or negedge cs_n) begin
        if (!cs_n) begin
            read_shift_reg <= 16'h0;
            read_active <= 1'b0;
        end else if (bit_count == 16 && spi_rw[0] == 1'b0) begin
            // Start of data phase for read transaction
            read_shift_reg <= read_data;
            read_active <= 1'b1;
        end else if (read_active) begin
            read_shift_reg <= {read_shift_reg[14:0], 1'b0};
        end
    end

    assign miso = read_active ? read_shift_reg[15] : 1'b0;

    //==========================================================================
    // Register Write Logic (latched on transaction completion)
    //==========================================================================
    always_ff @(negedge cs_n) begin
        if (transaction_valid && spi_rw[0] == 1'b1) begin
            // Write transaction complete
            case (spi_addr)
                8'h21: begin  // CONTROL register
                    reg_file[8'h21] <= spi_wdata;
                end
                8'h40: begin  // CONFIG_ROWS
                    reg_file[8'h40] <= spi_wdata;
                end
                8'h41: begin  // CONFIG_COLS
                    reg_file[8'h41] <= spi_wdata;
                end
                8'h42: begin  // BIT_DEPTH
                    reg_file[8'h42] <= spi_wdata;
                end
                8'h50: begin  // TIMING_GATE_ON
                    reg_file[8'h50] <= spi_wdata;
                end
                8'h51: begin  // TIMING_GATE_OFF
                    reg_file[8'h51] <= spi_wdata;
                end
                8'h52: begin  // TIMING_ROIC_SETTLE
                    reg_file[8'h52] <= spi_wdata[7:0];
                end
                8'h53: begin  // TIMING_ADC_CONV
                    reg_file[8'h53] <= spi_wdata[7:0];
                end
                8'h54: begin  // TIMING_LINE_PERIOD
                    reg_file[8'h54] <= spi_wdata;
                end
                8'h55: begin  // TIMING_FRAME_BLANK
                    reg_file[8'h55] <= spi_wdata;
                end
                8'h60: begin  // CSI2_LANE_SPEED
                    reg_file[8'h60] <= spi_wdata;
                end
                8'h61: begin  // CSI2_CONTROL
                    reg_file[8'h61] <= spi_wdata;
                end
                default: begin
                    // Unmapped or read-only: ignore write (REQ-FPGA-044)
                end
            endcase
        end
    end

    //==========================================================================
    // Register Outputs (connect to register file)
    //==========================================================================
    // CONTROL register outputs
    assign start_scan  = reg_file[8'h21][0];
    assign stop_scan   = reg_file[8'h21][1];
    assign soft_reset  = reg_file[8'h21][2];
    assign scan_mode   = reg_file[8'h21][4:3];
    assign error_clear = reg_file[8'h21][5];

    // Timing registers
    assign gate_on_ticks    = reg_file[8'h50];
    assign gate_off_ticks   = reg_file[8'h51];
    assign roic_settle_ticks = reg_file[8'h52][7:0];
    assign adc_conv_ticks   = reg_file[8'h53][7:0];
    assign line_period      = reg_file[8'h54];
    assign frame_blanking   = reg_file[8'h55];

    // Panel config
    assign panel_rows = reg_file[8'h40][13:0];
    assign panel_cols = reg_file[8'h41][13:0];
    assign bit_depth  = reg_file[8'h42][4:0];

    // CSI-2 control
    assign csi2_tx_enable     = reg_file[8'h61][2];
    assign csi2_lane_count    = reg_file[8'h61][1:0];
    assign csi2_continuous_clk = reg_file[8'h61][3];

    //==========================================================================
    // Assertions
    //==========================================================================
    `ifdef FORMAL
        // REQ-FPGA-043: Abort on CS_N deassertion
        property p_cs_abort;
            @(posedge sclk)
            !cs_n |=> bit_count == 0;
        endproperty
        assert_cs_abort: assert property(p_cs_abort);
    `endif

endmodule

//==============================================================================
// End of File: spi_slave.sv
//==============================================================================
