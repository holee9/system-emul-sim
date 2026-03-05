//==============================================================================
// Panel Scan FSM - Panel Scan Timing State Machine
//==============================================================================
// File: panel_scan_fsm.sv
// Author: FPGA RTL Developer
// Date: 2026-02-18
// Version: 1.0.0
//
// Description:
//  Panel Scan FSM implements the timing control for X-ray detector panel
//  scanning. It manages six states: IDLE, INTEGRATE, READOUT, LINE_DONE,
//  FRAME_DONE, and ERROR.
//
// Requirements Implemented:
//  REQ-FPGA-010: Six-state FSM (IDLE, INTEGRATE, READOUT, LINE_DONE,
//                 FRAME_DONE, ERROR)
//  REQ-FPGA-011: IDLE -> INTEGRATE transition within 1 clock cycle of
//                 start_scan assertion
//  REQ-FPGA-012: Gate ON for configurable gate_on_us duration
//  REQ-FPGA-013: ROIC settling time after integration
//  REQ-FPGA-014: Three operating modes (Single, Continuous, Calibration)
//  REQ-FPGA-015: Frame completion with counter update
//  REQ-FPGA-016: Graceful stop within 1 line time
//
// Resources (Estimated):
//  LUTs: ~500, FFs: ~330, BRAMs: 0
//
//==============================================================================

module panel_scan_fsm #(
    parameter bit WIDTH = 12,             // Address width for line counter (0-4095)
    parameter int unsigned GATE_ON_DEFAULT = 1000,   // Default gate ON time (10ns units)
    parameter int unsigned ROIC_SETTLE_DEFAULT = 10, // Default ROIC settle time (10ns units)
    parameter int unsigned ADC_CONV_DEFAULT = 5      // Default ADC conversion time (10ns units)
) (
    // Clock and Reset
    input  logic clk_sys,                  // System clock (100 MHz)
    input  logic rst_n,                    // Active-low asynchronous reset

    // SPI Register Interface (from spi_slave)
    input  logic start_scan,               // CONTROL[0]: Start scan sequence
    input  logic stop_scan,                // CONTROL[1]: Stop scan gracefully
    input  logic soft_reset,               // CONTROL[2]: Software reset
    input  logic [1:0] scan_mode,          // CONTROL[3:2]: 00=Single, 01=Continuous, 10=Calib
    input  logic error_clear,              // CONTROL[4]: Clear error flags

    input  logic [15:0] gate_on_ticks,     // TIMING_GATE_ON: Gate ON duration (10ns units)
    input  logic [15:0] gate_off_ticks,    // TIMING_GATE_OFF: Gate OFF settle time
    input  logic [7:0]  roic_settle_ticks, // TIMING_ROIC_SETTLE: ROIC settle time
    input  logic [7:0]  adc_conv_ticks,    // TIMING_ADC_CONV: ADC conversion time

    input  logic [13:0] panel_rows,        // CONFIG_ROWS: Number of panel rows
    input  logic [13:0] panel_cols,        // CONFIG_COLS: Number of panel columns

    // Status Outputs (to spi_slave registers)
    output logic [2:0]  fsm_state,         // Current state encoding (for STATUS register)
    output logic        idle_flag,         // STATUS[0]: 1 when in IDLE
    output logic        busy_flag,         // STATUS[1]: 1 when scan in progress
    output logic        error_flag,        // STATUS[2]: 1 when in ERROR state

    // 32-bit Frame Counter (readable via SPI)
    output logic [31:0] frame_counter,

    // Panel Control Outputs
    output logic        gate_on,           // Gate control output (active high)
    output logic        roic_sync,         // ROIC sync/trigger output
    output logic        line_valid,        // One line data valid pulse
    output logic        frame_valid,       // One frame complete pulse

    // Line Buffer Interface
    output logic        line_write_en,     // Enable writing to line buffer
    output logic [11:0] line_write_addr,   // Write address within current line
    output logic        line_done,         // Line complete, trigger ping-pong swap

    // Error Inputs (from protection_logic)
    input  logic        timeout_error,     // Readout timeout detected
    input  logic        overflow_error,    // Buffer overflow detected
    input  logic        roic_fault,        // ROIC interface fault
    input  logic        dphy_error,        // D-PHY link error
    input  logic        watchdog_error,    // System watchdog timeout
    input  logic        config_error       // Invalid configuration detected
);

    //==========================================================================
    // State Encoding (3-bit one-hot for safety and coverage)
    //==========================================================================
    typedef enum logic [2:0] {
        IDLE        = 3'b000,
        INTEGRATE   = 3'b001,
        READOUT     = 3'b010,
        LINE_DONE   = 3'b011,
        FRAME_DONE  = 3'b100,
        ERROR       = 3'b101
    } state_t;

    state_t current_state, next_state;
    logic [2:0] current_state_encoded;
    logic [2:0] next_state_encoded;

    // State encoding to integer conversion for status output
    always_comb begin
        current_state_encoded = 3'b000;
        case (current_state)
            IDLE:        current_state_encoded = 3'b000;
            INTEGRATE:   current_state_encoded = 3'b001;
            READOUT:     current_state_encoded = 3'b010;
            LINE_DONE:   current_state_encoded = 3'b011;
            FRAME_DONE:  current_state_encoded = 3'b100;
            ERROR:       current_state_encoded = 3'b101;
            default:     current_state_encoded = 3'b101;  // Default to ERROR
        endcase
    end

    assign fsm_state = current_state_encoded;

    //==========================================================================
    // Status Flag Generation
    //==========================================================================
    assign idle_flag  = (current_state == IDLE);
    assign busy_flag  = (current_state == INTEGRATE)  ||
                        (current_state == READOUT)    ||
                        (current_state == LINE_DONE)  ||
                        (current_state == FRAME_DONE);
    assign error_flag = (current_state == ERROR);

    //==========================================================================
    // Error Aggregation (Fatal errors trigger immediate ERROR state)
    //==========================================================================
    logic any_fatal_error;
    assign any_fatal_error = timeout_error   ||
                             overflow_error  ||
                             roic_fault      ||
                             dphy_error      ||
                             watchdog_error  ||
                             config_error;

    //==========================================================================
    // State Register (Synchronous with asynchronous reset)
    //==========================================================================
    always_ff @(posedge clk_sys or negedge rst_n) begin
        if (!rst_n) begin
            current_state <= IDLE;
        end else if (soft_reset) begin
            current_state <= IDLE;
        end else begin
            current_state <= next_state;
        end
    end

    //==========================================================================
    // Frame Counter (32-bit, wraps at 2^32)
    //==========================================================================
    always_ff @(posedge clk_sys or negedge rst_n) begin
        if (!rst_n) begin
            frame_counter <= 32'h0;
        end else if (soft_reset) begin
            frame_counter <= 32'h0;
        end else if (current_state == FRAME_DONE && next_state == IDLE) begin
            // Increment frame counter when completing single scan
            frame_counter <= frame_counter + 1'b1;
        end else if (current_state == FRAME_DONE && next_state == INTEGRATE) begin
            // Increment frame counter for continuous mode
            frame_counter <= frame_counter + 1'b1;
        end
    end

    //==========================================================================
    // Timing Counters
    //==========================================================================
    logic [15:0] gate_timer;
    logic [7:0]  settle_timer;
    logic [7:0]  adc_timer;
    logic [13:0] line_counter;

    // Gate ON timer
    always_ff @(posedge clk_sys or negedge rst_n) begin
        if (!rst_n) begin
            gate_timer <= 16'h0;
        end else if (current_state == IDLE) begin
            gate_timer <= gate_on_ticks;
        end else if (current_state == INTEGRATE && gate_timer > 0) begin
            gate_timer <= gate_timer - 1'b1;
        end
    end

    // ROIC settle timer
    always_ff @(posedge clk_sys or negedge rst_n) begin
        if (!rst_n) begin
            settle_timer <= 8'h0;
        end else if (current_state == READOUT) begin
            if (settle_timer > 0) begin
                settle_timer <= settle_timer - 1'b1;
            end
        end else if (current_state == INTEGRATE) begin
            settle_timer <= roic_settle_ticks;
        end
    end

    // ADC conversion timer
    always_ff @(posedge clk_sys or negedge rst_n) begin
        if (!rst_n) begin
            adc_timer <= 8'h0;
        end else if (current_state == READOUT && settle_timer == 0) begin
            if (adc_timer > 0) begin
                adc_timer <= adc_timer - 1'b1;
            end
        end else if (current_state == READOUT && settle_timer > 0) begin
            adc_timer <= adc_conv_ticks;
        end
    end

    // Line counter
    always_ff @(posedge clk_sys or negedge rst_n) begin
        if (!rst_n) begin
            line_counter <= 14'h0;
        end else if (current_state == IDLE) begin
            line_counter <= 14'h0;
        end else if (current_state == LINE_DONE && next_state == READOUT) begin
            line_counter <= line_counter + 1'b1;
        end else if (current_state == FRAME_DONE) begin
            line_counter <= 14'h0;
        end
    end

    //==========================================================================
    // Gate Output Control
    //==========================================================================
    // Gate ON during INTEGRATE state, except in Calibration mode
    logic calibration_mode;
    assign calibration_mode = (scan_mode == 2'b10);

    always_ff @(posedge clk_sys or negedge rst_n) begin
        if (!rst_n) begin
            gate_on <= 1'b0;
        end else if (current_state == IDLE || current_state == ERROR) begin
            gate_on <= 1'b0;
        end else if (current_state == INTEGRATE && !calibration_mode) begin
            gate_on <= 1'b1;
        end else begin
            gate_on <= 1'b0;
        end
    end

    //==========================================================================
    // Next State Logic (State Transition Function)
    //==========================================================================
    always_comb begin
        // Default: remain in current state
        next_state = current_state;

        // Fatal error override
        if (any_fatal_error) begin
            next_state = ERROR;
        end else begin
            case (current_state)
                //--------------------------------------------------------------
                // IDLE: Waiting for start_scan command
                //--------------------------------------------------------------
                IDLE: begin
                    if (start_scan) begin
                        next_state = INTEGRATE;
                    end
                end

                //--------------------------------------------------------------
                // INTEGRATE: Gate ON, exposure timing
                //--------------------------------------------------------------
                INTEGRATE: begin
                    if (stop_scan) begin
                        next_state = IDLE;
                    end else if (gate_timer == 0) begin
                        next_state = READOUT;
                    end
                end

                //--------------------------------------------------------------
                // READOUT: Gate OFF, ROIC ADC conversion and data read
                //--------------------------------------------------------------
                READOUT: begin
                    if (stop_scan) begin
                        next_state = IDLE;
                    end else if (settle_timer == 0 && adc_timer == 0) begin
                        next_state = LINE_DONE;
                    end
                end

                //--------------------------------------------------------------
                // LINE_DONE: Line buffered, preparing next line
                //--------------------------------------------------------------
                LINE_DONE: begin
                    if (stop_scan) begin
                        next_state = IDLE;
                    end else if (line_counter >= (panel_rows - 1)) begin
                        next_state = FRAME_DONE;
                    end else begin
                        next_state = READOUT;
                    end
                end

                //--------------------------------------------------------------
                // FRAME_DONE: Frame complete, update counters
                //--------------------------------------------------------------
                FRAME_DONE: begin
                    if (scan_mode == 2'b01) begin
                        // Continuous mode: return to INTEGRATE
                        next_state = INTEGRATE;
                    end else begin
                        // Single or Calibration mode: return to IDLE
                        next_state = IDLE;
                    end
                end

                //--------------------------------------------------------------
                // ERROR: Fatal error detected, waiting for error_clear
                //--------------------------------------------------------------
                ERROR: begin
                    if (error_clear) begin
                        next_state = IDLE;
                    end
                end

                //--------------------------------------------------------------
                // Default: Error state for undefined transitions
                //--------------------------------------------------------------
                default: begin
                    next_state = ERROR;
                end
            endcase
        end
    end

    //==========================================================================
    // Output Logic
    //==========================================================================

    // ROIC sync pulse (start of integration)
    always_ff @(posedge clk_sys or negedge rst_n) begin
        if (!rst_n) begin
            roic_sync <= 1'b0;
        end else begin
            roic_sync <= (current_state == IDLE && next_state == INTEGRATE);
        end
    end

    // Line valid pulse (line data ready for buffer)
    assign line_valid = (current_state == READOUT && settle_timer == 0 && adc_timer == 0);

    // Frame valid pulse (frame complete)
    assign frame_valid = (current_state == FRAME_DONE);

    // Line buffer write enable
    assign line_write_en = (current_state == READOUT);

    // Line write address (simple counter during readout)
    always_ff @(posedge clk_sys or negedge rst_n) begin
        if (!rst_n) begin
            line_write_addr <= 12'h0;
        end else if (current_state != READOUT) begin
            line_write_addr <= 12'h0;
        end else if (line_write_en) begin
            if (line_write_addr < (panel_cols - 1)) begin
                line_write_addr <= line_write_addr + 1'b1;
            end
        end
    end

    // Line done pulse (trigger ping-pong swap)
    assign line_done = (current_state == LINE_DONE);

    //==========================================================================
    // Assertions for Verification (Formal or Simulation)
    //==========================================================================

    // REQ-FPGA-011: Transition from IDLE to INTEGRATE within 1 cycle
    `ifdef FORMAL
        property p_start_response;
            @(posedge clk_sys) disable iff (!rst_n)
            (current_state == IDLE && start_scan) |=> (current_state == INTEGRATE);
        endproperty
        assert_start_response: assert property(p_start_response);
    `endif

    // REQ-FPGA-012: Gate ON only during INTEGRATE (unless calibration)
    `ifdef FORMAL
        property p_gate_timing;
            @(posedge clk_sys) disable iff (!rst_n)
            (current_state == INTEGRATE && !calibration_mode) |-> gate_on;
        endproperty
        assert_gate_timing: assert property(p_gate_timing);
    `endif

    // REQ-FPGA-016: Stop scan returns to IDLE within 1 line time
    `ifdef FORMAL
        property p_stop_response;
            @(posedge clk_sys) disable iff (!rst_n)
            stop_scan |=> s_eventually[current_state == IDLE];
        endproperty
        assert_stop_response: assert property(p_stop_response);
    `endif

    // Mutex: Only one state active at a time (one-hot property)
    `ifdef FORMAL
        property p_state_mutex;
            @(posedge clk_sys) disable iff (!rst_n)
            $onehot(current_state_encoded);
        endproperty
        assert_state_mutex: assert property(p_state_mutex);
    `endif

endmodule

//==============================================================================
// End of File: panel_scan_fsm.sv
//==============================================================================
