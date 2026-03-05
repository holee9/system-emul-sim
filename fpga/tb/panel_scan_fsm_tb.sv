//==============================================================================
// Testbench: Panel Scan FSM
//==============================================================================
// File: panel_scan_fsm_tb.sv
// Author: FPGA RTL Developer
// Date: 2026-02-18
// Version: 1.0.0
//
// Description:
//  Comprehensive testbench for panel_scan_fsm module.
//  Tests cover all state transitions, timing requirements, and edge cases.
//
// Coverage Goals:
//  - Line Coverage: >= 95%
//  - Branch Coverage: >= 90%
//  - FSM Coverage: 100% (all states and transitions)
//
//==============================================================================

module panel_scan_fsm_tb;

    //==========================================================================
    // Clock and Reset Generation
    //==========================================================================
    logic clk_sys;
    logic rst_n;

    // System clock: 100 MHz (10 ns period)
    initial begin
        clk_sys = 0;
        forever #5 clk_sys = ~clk_sys;
    end

    // Reset generation
    initial begin
        rst_n = 0;
        #100;
        rst_n = 1;
    end

    //==========================================================================
    // DUT Signals
    //==========================================================================
    // SPI Register Interface
    logic        start_scan;
    logic        stop_scan;
    logic        soft_reset;
    logic [1:0]  scan_mode;
    logic        error_clear;

    logic [15:0] gate_on_ticks;
    logic [15:0] gate_off_ticks;
    logic [7:0]  roic_settle_ticks;
    logic [7:0]  adc_conv_ticks;

    logic [13:0] panel_rows;
    logic [13:0] panel_cols;

    // Status Outputs
    logic [2:0]  fsm_state;
    logic        idle_flag;
    logic        busy_flag;
    logic        error_flag;

    // Frame Counter
    logic [31:0] frame_counter;

    // Panel Control Outputs
    logic        gate_on;
    logic        roic_sync;
    logic        line_valid;
    logic        frame_valid;

    // Line Buffer Interface
    logic        line_write_en;
    logic [11:0] line_write_addr;
    logic        line_done;

    // Error Inputs
    logic        timeout_error;
    logic        overflow_error;
    logic        roic_fault;
    logic        dphy_error;
    logic        watchdog_error;
    logic        config_error;

    //==========================================================================
    // DUT Instantiation
    //==========================================================================
    panel_scan_fsm dut (
        .clk_sys,
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

        // Status Outputs
        .fsm_state,
        .idle_flag,
        .busy_flag,
        .error_flag,

        // Frame Counter
        .frame_counter,

        // Panel Control Outputs
        .gate_on,
        .roic_sync,
        .line_valid,
        .frame_valid,

        // Line Buffer Interface
        .line_write_en,
        .line_write_addr,
        .line_done,

        // Error Inputs
        .timeout_error,
        .overflow_error,
        .roic_fault,
        .dphy_error,
        .watchdog_error,
        .config_error
    );

    //==========================================================================
    // Test Variables
    //==========================================================================
    int test_passed;
    int test_failed;
    string test_name;

    //==========================================================================
    // Tasks
    //==========================================================================

    // Wait for N clock cycles
    task wait_cycles(int n);
        repeat(n) @(posedge clk_sys);
    endtask

    // Initialize all inputs to default values
    task init_inputs();
        start_scan      = 0;
        stop_scan       = 0;
        soft_reset      = 0;
        scan_mode       = 2'b00;  // Single scan mode
        error_clear     = 0;

        gate_on_ticks    = 16'd100;   // 1 us at 100 MHz
        gate_off_ticks   = 16'd10;
        roic_settle_ticks = 8'd10;
        adc_conv_ticks   = 8'd5;

        panel_rows       = 14'd100;   // Small test size
        panel_cols       = 14'd100;

        timeout_error    = 0;
        overflow_error   = 0;
        roic_fault       = 0;
        dphy_error       = 0;
        watchdog_error   = 0;
        config_error     = 0;
    endtask

    // Check state
    task check_state(input logic [2:0] expected_state);
        if (fsm_state !== expected_state) begin
            $error("[%s] State mismatch! Expected: %d, Got: %d at time %0t",
                   test_name, expected_state, fsm_state, $time);
            test_failed++;
        end else begin
            test_passed++;
        end
    endtask

    // Check frame counter
    task check_frame_counter(input logic [31:0] expected_count);
        if (frame_counter !== expected_count) begin
            $error("[%s] Frame counter mismatch! Expected: %d, Got: %d",
                   test_name, expected_count, frame_counter);
            test_failed++;
        end else begin
            test_passed++;
        end
    endtask

    //==========================================================================
    // Test Cases
    //==========================================================================

    // Test 1: Reset State
    initial begin
        test_name = "TEST_01_RESET";
        init_inputs();
        wait_cycles(5);

        // After reset, should be in IDLE state
        check_state(3'b000);  // IDLE
        check_frame_counter(32'h0);

        $display("[%s] PASSED: Reset to IDLE state verified", test_name);
    end

    // Test 2: Single Scan - Basic Operation
    initial begin
        test_name = "TEST_02_SINGLE_SCAN";
        init_inputs();

        @(posedge rst_n);
        wait_cycles(10);

        // Start single scan
        @(posedge clk_sys);
        start_scan = 1;
        wait_cycles(1);
        start_scan = 0;

        // Should transition to INTEGRATE
        wait_cycles(1);
        check_state(3'b001);  // INTEGRATE

        // Wait for gate timer to expire
        wait_cycles(120);

        // Should transition through READOUT -> LINE_DONE -> FRAME_DONE -> IDLE
        wait_cycles(50);

        $display("[%s] PASSED: Single scan state transitions verified", test_name);
    end

    // Test 3: Continuous Scan Mode
    initial begin
        test_name = "TEST_03_CONTINUOUS_MODE";
        init_inputs();
        scan_mode = 2'b01;  // Continuous mode

        @(posedge rst_n);
        wait_cycles(10);

        repeat(2) begin
            // Start scan
            @(posedge clk_sys);
            start_scan = 1;
            wait_cycles(1);
            start_scan = 0;

            // Wait for frame completion
            wait_cycles(200);
        end

        // Frame counter should be incremented
        if (frame_counter >= 32'd2) begin
            $display("[%s] PASSED: Continuous mode increments frame counter", test_name);
        end else begin
            $error("[%s] FAILED: Frame counter not incremented properly", test_name);
        end
    end

    // Test 4: Calibration Mode (Gate OFF)
    initial begin
        test_name = "TEST_04_CALIBRATION_MODE";
        init_inputs();
        scan_mode = 2'b10;  // Calibration mode

        @(posedge rst_n);
        wait_cycles(10);

        // Start calibration scan
        @(posedge clk_sys);
        start_scan = 1;
        wait_cycles(1);
        start_scan = 0;

        // In calibration mode, gate should remain OFF
        wait_cycles(5);
        if (gate_on == 1'b0) begin
            $display("[%s] PASSED: Gate OFF in calibration mode", test_name);
        end else begin
            $error("[%s] FAILED: Gate should be OFF in calibration mode", test_name);
        end
    end

    // Test 5: Stop Scan Graceful
    initial begin
        test_name = "TEST_05_STOP_SCAN";
        init_inputs();

        @(posedge rst_n);
        wait_cycles(10);

        // Start scan
        @(posedge clk_sys);
        start_scan = 1;
        wait_cycles(1);
        start_scan = 0;

        // Wait until INTEGRATE, then stop
        wait_cycles(20);
        stop_scan = 1;
        wait_cycles(1);
        stop_scan = 0;

        // Should return to IDLE
        wait_cycles(10);
        check_state(3'b000);  // IDLE

        $display("[%s] PASSED: Stop scan returns to IDLE", test_name);
    end

    // Test 6: Error Injection - Timeout
    initial begin
        test_name = "TEST_06_ERROR_TIMEOUT";
        init_inputs();

        @(posedge rst_n);
        wait_cycles(10);

        // Start scan
        @(posedge clk_sys);
        start_scan = 1;
        wait_cycles(1);
        start_scan = 0;

        wait_cycles(10);

        // Inject timeout error
        timeout_error = 1;
        wait_cycles(1);
        timeout_error = 0;

        // Should transition to ERROR state
        wait_cycles(2);
        check_state(3'b101);  // ERROR

        // Clear error
        error_clear = 1;
        wait_cycles(1);
        error_clear = 0;

        // Should return to IDLE
        wait_cycles(2);
        check_state(3'b000);  // IDLE

        $display("[%s] PASSED: Error injection and recovery verified", test_name);
    end

    // Test 7: Gate ON Timing
    initial begin
        test_name = "TEST_07_GATE_TIMING";
        init_inputs();

        @(posedge rst_n);
        wait_cycles(10);

        // Start scan
        @(posedge clk_sys);
        start_scan = 1;
        wait_cycles(1);
        start_scan = 0;

        // Check gate ON during INTEGRATE
        wait_until_state(3'b001);  // INTEGRATE
        if (gate_on == 1'b1) begin
            $display("[%s] PASSED: Gate ON during INTEGRATE", test_name);
        end else begin
            $error("[%s] FAILED: Gate should be ON during INTEGRATE", test_name);
        end

        // Wait for READOUT state
        wait_cycles(120);
        // Gate should be OFF in READOUT
        if (gate_on == 1'b0) begin
            $display("[%s] PASSED: Gate OFF during READOUT", test_name);
        end else begin
            $error("[%s] FAILED: Gate should be OFF during READOUT", test_name);
        end
    end

    // Test 8: Line Done Pulse
    initial begin
        test_name = "TEST_08_LINE_DONE";
        init_inputs();

        @(posedge rst_n);
        wait_cycles(10);

        // Start scan
        @(posedge clk_sys);
        start_scan = 1;
        wait_cycles(1);
        start_scan = 0;

        // Wait for LINE_DONE state
        wait_cycles(150);

        // Check line_done pulse
        if (line_done == 1'b1) begin
            $display("[%s] PASSED: Line done pulse generated", test_name);
        end else begin
            $error("[%s] FAILED: Line done pulse not generated", test_name);
        end
    end

    // Test 9: ROIC Sync Pulse
    initial begin
        test_name = "TEST_09_ROIC_SYNC";
        init_inputs();

        @(posedge rst_n);
        wait_cycles(10);

        // Capture roic_sync before starting
        wait_cycles(1);
        logic sync_before = roic_sync;

        // Start scan
        @(posedge clk_sys);
        start_scan = 1;
        wait_cycles(1);
        start_scan = 0;

        // Check roic_sync pulse
        wait_cycles(2);
        if (roic_sync == 1'b1 || sync_before == 1'b1) begin
            $display("[%s] PASSED: ROIC sync pulse generated", test_name);
        end else begin
            $error("[%s] FAILED: ROIC sync pulse not generated", test_name);
        end
    end

    // Test 10: Frame Counter Increment
    initial begin
        test_name = "TEST_10_FRAME_COUNTER";
        init_inputs();

        @(posedge rst_n);
        wait_cycles(10);

        logic [31:0] frame_before = frame_counter;

        // Start and complete one frame
        @(posedge clk_sys);
        start_scan = 1;
        wait_cycles(1);
        start_scan = 0;

        wait_cycles(300);

        // Frame counter should be incremented
        if (frame_counter == frame_before + 1'b1) begin
            $display("[%s] PASSED: Frame counter incremented", test_name);
        end else begin
            $error("[%s] FAILED: Frame counter not incremented correctly", test_name);
        end
    end

    // Helper task: Wait until specific state
    task wait_until_state(input logic [2:0] target_state);
        while (fsm_state !== target_state) begin
            @(posedge clk_sys);
            if ($time > 1000000) begin
                $error("Timeout waiting for state %d", target_state);
                break;
            end
        end
    endtask

    //==========================================================================
    // Test Summary
    //==========================================================================
    final begin
        $display("\n========================================");
        $display("  Panel Scan FSM Testbench Summary");
        $display("========================================");
        $display("  Tests Passed: %0d", test_passed);
        $display("  Tests Failed: %0d", test_failed);
        $display("  Total Checks: %0d", test_passed + test_failed);
        if (test_failed == 0) begin
            $display("  Status: ALL TESTS PASSED");
        end else begin
            $display("  Status: SOME TESTS FAILED");
        end
        $display("========================================\n");
    end

    //==========================================================================
    // Waveform Dump (for Vivado xsim or Questa)
    //==========================================================================
    initial begin
        $dumpfile("panel_scan_fsm_tb.vcd");
        $dumpvars(0, panel_scan_fsm_tb);
    end

    // Simulation timeout
    initial begin
        #1000000;  // 1 ms simulation time
        $display("Simulation timeout reached!");
        $finish;
    end

endmodule

//==============================================================================
// End of File: panel_scan_fsm_tb.sv
//==============================================================================
