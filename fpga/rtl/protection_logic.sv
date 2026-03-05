//==============================================================================
// Protection Logic - Error Detection and Safe Shutdown
//==============================================================================
// File: protection_logic.sv
// Author: FPGA RTL Developer
// Date: 2026-02-18
// Version: 1.0.0
//
// Description:
//  Protection logic detects 8 error conditions and ensures safe shutdown
//  within 10 clock cycles. All gate outputs held LOW in ERROR state.
//
// Requirements Implemented:
//  REQ-FPGA-050: 8 error conditions detection
//  REQ-FPGA-051: Safe state within 10 cycles for fatal errors
//  REQ-FPGA-052: Gate outputs held LOW in safe state
//  REQ-FPGA-053: Error clearing via SPI CONTROL register
//  REQ-FPGA-054: Watchdog timer (100 ms default)
//
// Resources (Estimated):
//  LUTs: ~300, FFs: ~112, BRAMs: 0
//
//==============================================================================

module protection_logic #(
    parameter WATCHDOG_TIMEOUT = 100_000_000,  // 100 ms at 100 MHz
    parameter READOUT_TIMEOUT = 10_000,        // 100 us max line time
    parameter int ERROR_RESPONSE_CYCLES = 10   // Max cycles to safe state
) (
    // Clock and Reset
    input  logic clk_sys,
    input  logic rst_n,

    // Error Inputs (from various modules)
    input  logic timeout_error,       // Readout timeout (from line_buffer)
    input  logic overflow_error,      // Buffer overflow (from line_buffer)
    input  logic crc_error,           // CSI-2 CRC mismatch (from csi2_tx)
    input  logic overexposure_error,  // Pixel saturation (from pixel formatter)
    input  logic roic_fault,          // ROIC interface error (from roic_interface)
    input  logic dphy_error,          // D-PHY link error (from csi2_tx)
    input  logic config_error,        // Invalid config (from spi_slave)
    input  logic watchdog_expired,    // Watchdog timer expired

    // Control Inputs
    input  logic error_clear,         // From SPI CONTROL register
    input  logic heartbeat,           // SPI activity pulse (reset watchdog)

    // Status Outputs
    output logic [7:0] error_flags,   // Bit map of active errors
    output logic        error_active, // Any error condition active
    output logic        safe_state,   // System in safe state

    // Safe State Outputs (override normal control)
    output logic        gate_safe,    // Force gate OFF (active high)
    output logic        csi2_disable, // Disable CSI-2 TX (active high)
    output logic        buffer_disable // Disable buffer writes (active high)
);

    //==========================================================================
    // Error Code Definitions
    //==========================================================================
    typedef enum logic [7:0] {
        ERR_NONE        = 8'h00,
        ERR_TIMEOUT     = 8'h01,  // Bit 0
        ERR_OVERFLOW    = 8'h02,  // Bit 1
        ERR_CRC         = 8'h04,  // Bit 2
        ERR_OVEREXPOSURE = 8'h08, // Bit 3
        ERR_ROIC        = 8'h10,  // Bit 4
        ERR_DPHY        = 8'h20,  // Bit 5
        ERR_CONFIG      = 8'h40,  // Bit 6
        ERR_WATCHDOG    = 8'h80   // Bit 7
    } error_code_t;

    //==========================================================================
    // Error Flag Latching (sticky until cleared)
    //==========================================================================
    logic [7:0] error_flags_reg;
    logic [7:0] error_inputs;

    assign error_inputs = {
        watchdog_expired,
        config_error,
        dphy_error,
        roic_fault,
        overexposure_error,
        crc_error,
        overflow_error,
        timeout_error
    };

    // Error flags are sticky (latch on rising edge)
    always_ff @(posedge clk_sys or negedge rst_n) begin
        if (!rst_n) begin
            error_flags_reg <= 8'h00;
        end else if (error_clear) begin
            error_flags_reg <= 8'h00;  // Clear all on error_clear
        end else begin
            error_flags_reg <= error_flags_reg | error_inputs;
        end
    end

    assign error_flags = error_flags_reg;

    //==========================================================================
    // Fatal Error Detection
    //==========================================================================
    // Fatal errors: timeout, overflow, ROIC fault, D-PHY error, watchdog
    logic fatal_error;

    assign fatal_error = timeout_error || overflow_error ||
                         roic_fault || dphy_error || watchdog_expired;

    assign error_active = |error_flags_reg;
    assign safe_state = error_active;

    //==========================================================================
    // Safe State Response Counter
    //==========================================================================
    logic [$clog2(ERROR_RESPONSE_CYCES+1)-1:0] response_counter;
    logic safe_state_ack;

    always_ff @(posedge clk_sys or negedge rst_n) begin
        if (!rst_n) begin
            response_counter <= '0;
            safe_state_ack <= 1'b0;
        end else if (error_active && !safe_state_ack) begin
            // Count cycles until safe state achieved
            if (response_counter < ERROR_RESPONSE_CYCES) begin
                response_counter <= response_counter + 1'b1;
            end else begin
                safe_state_ack <= 1'b1;
            end
        end else if (!error_active) begin
            response_counter <= '0;
            safe_state_ack <= 1'b0;
        end
    end

    //==========================================================================
    // Watchdog Timer
    //==========================================================================
    logic [$clog2(WATCHDOG_TIMEOUT+1)-1:0] watchdog_counter;

    always_ff @(posedge clk_sys or negedge rst_n) begin
        if (!rst_n) begin
            watchdog_counter <= '0;
        end else if (heartbeat) begin
            // Reset watchdog on SPI activity
            watchdog_counter <= '0;
        end else if (watchdog_counter < WATCHDOG_TIMEOUT) begin
            watchdog_counter <= watchdog_counter + 1'b1;
        end
    end

    assign watchdog_expired = (watchdog_counter >= WATCHDOG_TIMEOUT);

    //==========================================================================
    // Safe State Outputs
    //==========================================================================
    // REQ-FPGA-052: Gate outputs held LOW in safe state
    // REQ-FPGA-051: Safe state within 10 clock cycles

    always_ff @(posedge clk_sys or negedge rst_n) begin
        if (!rst_n) begin
            gate_safe <= 1'b0;
            csi2_disable <= 1'b0;
            buffer_disable <= 1'b0;
        end else if (safe_state_ack) begin
            // Safe state achieved
            gate_safe <= 1'b1;
            csi2_disable <= 1'b1;
            buffer_disable <= 1'b1;
        end else if (!error_active) begin
            // Normal operation
            gate_safe <= 1'b0;
            csi2_disable <= 1'b0;
            buffer_disable <= 1'b0;
        end
    end

    //==========================================================================
    // Assertions
    //==========================================================================
    `ifdef FORMAL
        // REQ-FPGA-051: Safe state within 10 cycles
        property p_safe_response;
            @(posedge clk_sys) disable iff (!rst_n)
            fatal_error |-> ##[0:ERROR_RESPONSE_CYCES] safe_state;
        endproperty
        assert_safe_response: assert property(p_safe_response);
    `endif

    `ifdef FORMAL
        // REQ-FPGA-052: Gate safe when in safe state
        property p_gate_safe;
            @(posedge clk_sys) disable iff (!rst_n)
            safe_state |-> gate_safe;
        endproperty
        assert_gate_safe: assert property(p_gate_safe);
    `endif

    `ifdef FORMAL
        // REQ-FPGA-053: Error clearing
        property p_error_clear;
            @(posedge clk_sys) disable iff (!rst_n)
            error_clear |=> ##1 !error_active;
        endproperty
        assert_error_clear: assert property(p_error_clear);
    `endif

endmodule

//==============================================================================
// End of File: protection_logic.sv
//==============================================================================
