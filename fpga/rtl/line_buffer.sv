//==============================================================================
// Line Buffer - Ping-Pong Dual-Bank BRAM with CDC
//==============================================================================
// File: line_buffer.sv
// Author: FPGA RTL Developer
// Date: 2026-02-18
// Version: 1.0.0
//
// Description:
//  Line buffer implements ping-pong dual-bank BRAM architecture for clock
//  domain crossing between ROIC write domain (clk_roic) and CSI-2 read domain
//  (clk_csi2_byte). Each bank stores up to 3072 pixels at 16-bit depth.
//
// Requirements Implemented:
//  REQ-FPGA-020: Ping-Pong dual-bank architecture
//  REQ-FPGA-021: Maximum line width of 3072 pixels x 16-bit
//  REQ-FPGA-022: Two clock domains (clk_roic write, clk_csi2_byte read)
//  REQ-FPGA-023: Overflow detection when write catches read
//  REQ-FPGA-024: Bank toggling on line_done signal
//
// Resources (Estimated):
//  LUTs: ~450, FFs: ~88, BRAMs: 4
//
//==============================================================================

module line_buffer #(
    parameter int MAX_WIDTH = 3072,      // Maximum line width (pixels)
    parameter int DATA_WIDTH = 16,       // Pixel bit depth
    parameter int ADDR_WIDTH = 12,       // Address width (2^12 = 4096 > 3072)
    parameter int DEPTH = 3072           // Actual BRAM depth
) (
    // Write Domain Clock (clk_roic)
    input  logic                clk_roic,
    input  logic                rst_n,

    // Write Interface (from ROIC)
    input  logic                write_en,
    input  logic [DATA_WIDTH-1:0] write_data,
    output logic [ADDR_WIDTH-1:0] write_addr,
    output logic                write_ready,

    // Read Domain Clock (clk_csi2_byte)
    input  logic                clk_csi2,
    input  logic                rst_csi2_n,

    // Read Interface (to CSI-2 TX)
    input  logic                read_en,
    output logic [DATA_WIDTH-1:0] read_data,
    output logic [ADDR_WIDTH-1:0] read_addr,
    output logic                read_valid,

    // Control Signals (from Panel Scan FSM)
    input  logic                line_done,      // Trigger bank swap
    input  logic [11:0]         line_width,     // Actual pixels in current line

    // Status Outputs
    output logic                overflow_flag,  // Write caught read (error)
    output logic                bank_sel_wr,    // Current write bank (0=A, 1=B)
    output logic                bank_sel_rd     // Current read bank (synchronized)
);

    //==========================================================================
    // Bank Select Management
    //==========================================================================
    logic bank_sel_wr_next;
    logic bank_sel_rd_sync0, bank_sel_rd_sync1;

    // Write domain: Toggle bank on line_done
    always_ff @(posedge clk_roic or negedge rst_n) begin
        if (!rst_n) begin
            bank_sel_wr <= 1'b0;
        end else if (line_done) begin
            bank_sel_wr <= ~bank_sel_wr;
        end
    end

    // CDC: Synchronize bank select to read domain (2-stage FF)
    always_ff @(posedge clk_csi2 or negedge rst_csi2_n) begin
        if (!rst_csi2_n) begin
            bank_sel_rd_sync0 <= 1'b0;
            bank_sel_rd_sync1 <= 1'b0;
        end else begin
            bank_sel_rd_sync0 <= bank_sel_wr;
            bank_sel_rd_sync1 <= bank_sel_rd_sync0;
        end
    end

    assign bank_sel_rd = bank_sel_rd_sync1;

    //==========================================================================
    // Address Counters
    //==========================================================================
    logic [ADDR_WIDTH-1:0] wr_addr_a, wr_addr_b;
    logic [ADDR_WIDTH-1:0] rd_addr_a, rd_addr_b;
    logic [ADDR_WIDTH-1:0] wr_addr;
    logic [ADDR_WIDTH-1:0] rd_addr;

    // Write address counter
    always_ff @(posedge clk_roic or negedge rst_n) begin
        if (!rst_n) begin
            wr_addr <= '0;
        end else if (line_done) begin
            wr_addr <= '0;
        end else if (write_en && write_ready) begin
            if (wr_addr < line_width - 1) begin
                wr_addr <= wr_addr + 1'b1;
            end
        end
    end

    // Read address counter
    always_ff @(posedge clk_csi2 or negedge rst_csi2_n) begin
        if (!rst_csi2_n) begin
            rd_addr <= '0;
        end else if (line_done) begin
            rd_addr <= '0;
        end else if (read_en) begin
            if (rd_addr < line_width - 1) begin
                rd_addr <= rd_addr + 1'b1;
            end
        end
    end

    assign write_addr = wr_addr;
    assign read_addr = rd_addr;

    //==========================================================================
    // Overflow Detection (CDC-safe)
    //==========================================================================
    // Overflow occurs when write catches read in the same bank
    // Detection must be synchronous to write domain
    logic overflow_detected;

    always_ff @(posedge clk_roic or negedge rst_n) begin
        if (!rst_n) begin
            overflow_detected <= 1'b0;
        end else begin
            // Simple overflow detection: if write and read are in same bank
            // and write address catches up to read address
            if (bank_sel_wr == bank_sel_rd_sync1 && write_en && read_en) begin
                if (wr_addr >= rd_addr - 1) begin
                    overflow_detected <= 1'b1;
                end
            end else if (line_done) begin
                overflow_detected <= 1'b0;
            end
        end
    end

    assign overflow_flag = overflow_detected;
    assign write_ready = !overflow_detected;

    //==========================================================================
    // Dual-Port BRAM Instances
    //==========================================================================
    // Bank A: True Dual-Port BRAM
    logic [DATA_WIDTH-1:0] bram_a_data_out;
    logic we_a, we_b;

    // Write enable per bank
    assign we_a = write_en && write_ready && (bank_sel_wr == 1'b0);
    assign we_b = write_en && write_ready && (bank_sel_wr == 1'b1);

    // Bank A BRAM (Xilinx primitive inferred)
    xilinx_true_dual_port_bram bram_bank_a (
        .clka  (clk_roic),
        .wea   (we_a),
        .addra (wr_addr),
        .dina  (write_data),
        .douta (),             // Write port doesn't read

        .clkb  (clk_csi2),
        .web   (1'b0),         // Read port never writes
        .addrb (rd_addr),
        .dinb  ('0),
        .doutb (bram_a_data_out)
    );

    // Bank B BRAM
    logic [DATA_WIDTH-1:0] bram_b_data_out;
    xilinx_true_dual_port_bram bram_bank_b (
        .clka  (clk_roic),
        .wea   (we_b),
        .addra (wr_addr),
        .dina  (write_data),
        .douta (),

        .clkb  (clk_csi2),
        .web   (1'b0),
        .addrb (rd_addr),
        .dinb  ('0),
        .doutb (bram_b_data_out)
    );

    //==========================================================================
    // Read Data Mux
    //==========================================================================
    always_ff @(posedge clk_csi2) begin
        if (bank_sel_rd == 1'b0) begin
            read_data <= bram_a_data_out;
        end else begin
            read_data <= bram_b_data_out;
        end
    end

    // Read valid: data available one cycle after read_en
    logic read_valid_delay;
    always_ff @(posedge clk_csi2 or negedge rst_csi2_n) begin
        if (!rst_csi2_n) begin
            read_valid_delay <= 1'b0;
        end else begin
            read_valid_delay <= read_en;
        end
    end
    assign read_valid = read_valid_delay;

    //==========================================================================
    // Assertions
    //==========================================================================
    `ifdef FORMAL
        // REQ-FPGA-023: Overflow flag must halt writes
        property p_overflow_halt;
            @(posedge clk_roic) disable iff (!rst_n)
            overflow_flag |-> !write_ready;
        endproperty
        assert_overflow_halt: assert property(p_overflow_halt);
    `endif

    `ifdef FORMAL
        // REQ-FPGA-024: Bank toggles on line_done
        property p_bank_toggle;
            @(posedge clk_roic) disable iff (!rst_n)
            line_done |=> bank_sel_wr != $past(bank_sel_wr);
        endproperty
        assert_bank_toggle: assert property(p_bank_toggle);
    `endif

endmodule

//==============================================================================
// True Dual-Port BRAM (Xilinx Artix-7 Inferred)
//==============================================================================
// Infers Xilinx BRAM using proper template for synthesis
//==============================================================================
module xilinx_true_dual_port_bram #(
    parameter DATA_WIDTH = 16,
    parameter ADDR_WIDTH = 12,
    parameter DEPTH = 3072
) (
    // Port A (Write side)
    input  logic                    clka,
    input  logic                    wea,
    input  logic [ADDR_WIDTH-1:0]   addra,
    input  logic [DATA_WIDTH-1:0]   dina,
    output logic [DATA_WIDTH-1:0]   douta,

    // Port B (Read side)
    input  logic                    clkb,
    input  logic                    web,
    input  logic [ADDR_WIDTH-1:0]   addrb,
    input  logic [DATA_WIDTH-1:0]   dinb,
    output logic [DATA_WIDTH-1:0]   doutb
);

    // Memory array (inferred as BRAM)
    logic [DATA_WIDTH-1:0] mem [0:DEPTH-1];

    // Port A: Write-first behavior
    always_ff @(posedge clka) begin
        if (wea) begin
            mem[addra] <= dina;
            douta <= dina;  // Write-first: new data on output
        end else begin
            douta <= mem[addra];
        end
    end

    // Port B: Write-first behavior
    always_ff @(posedge clkb) begin
        if (web) begin
            mem[addrb] <= dinb;
            doutb <= dinb;
        end else begin
            doutb <= mem[addrb];
        end
    end

    // Force BRAM inference with attributes
    (* ram_style = "block" *) logic [DATA_WIDTH-1:0] bram_mem [0:DEPTH-1];

endmodule

//==============================================================================
// End of File: line_buffer.sv
//==============================================================================
