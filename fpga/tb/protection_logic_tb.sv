//==============================================================================
// Testbench: Protection Logic
//==============================================================================
// File: protection_logic_tb.sv
// Author: FPGA RTL Developer
// Date: 2026-02-18
// Version: 1.0.0
//
// Description:
//  Comprehensive testbench for protection_logic module testing all 8
//  error conditions, safe state response, and watchdog timer.
//
// Coverage Goals:
//  - Line Coverage: >= 95%
//  - Branch Coverage: >= 90%
//
//==============================================================================

module protection_logic_tb;

    //==========================================================================
    // Clock Generation
    //==========================================================================
    logic clk_sys;

    // System clock: 100 MHz
    initial begin
        clk_sys = 0;
        forever #5 clk_sys = ~clk_sys;
    end

    //==========================================================================
    // Reset Generation
    //==========================================================================
    logic rst_n;

    initial begin
        rst_n = 0;
        #200;
        rst_n = 1;
    end

    //==========================================================================
    // DUT Signals
    //==========================================================================
    // Error Inputs
    logic timeout_error, overflow_error, crc_error;
    logic overexposure_error, roic_fault, dphy_error;
    logic config_error, watchdog_expired;

    // Control Inputs
    logic error_clear, heartbeat;

    // Status Outputs
    logic [7:0] error_flags;
    logic error_active, safe_state;

    // Safe State Outputs
    logic gate_safe, csi2_disable, buffer_disable;

    //==========================================================================
    // DUT Instantiation
    //==========================================================================
    protection_logic #(
        .WATCHDOG_TIMEOUT(100),    // Shortened for simulation (1 us)
        .READOUT_TIMEOUT(100),
        .ERROR_RESPONSE_CYCLES(10)
    ) dut (
        .clk_sys, .rst_n,

        // Error Inputs
        .timeout_error, .overflow_error, .crc_error,
        .overexposure_error, .roic_fault, .dphy_error,
        .config_error, .watchdog_expired,

        // Control
        .error_clear, .heartbeat,

        // Status
        .error_flags, .error_active, .safe_state,

        // Safe State Outputs
        .gate_safe, .csi2_disable, .buffer_disable
    );

    //==========================================================================
    // Test Variables
    //==========================================================================
    int test_passed;
    int test_failed;
    string test_name;
    int error_response_time;

    //==========================================================================
    // Tasks
    //==========================================================================
    task wait_cycles(int n);
        repeat(n) @(posedge clk_sys);
    endtask

    task init_inputs();
        timeout_error = 1'b0;
        overflow_error = 1'b0;
        crc_error = 1'b0;
        overexposure_error = 1'b0;
        roic_fault = 1'b0;
        dphy_error = 1'b0;
        config_error = 1'b0;
        watchdog_expired = 1'b0;
        error_clear = 1'b0;
        heartbeat = 1'b0;
    endtask

    //==========================================================================
    // Test 1: Reset State
    //==========================================================================
    initial begin
        test_name = "TEST_01_RESET";
        init_inputs();

        wait_cycles(10);

        if (error_flags == 8'h00 && !error_active && !safe_state) begin
            $display("[%s] PASSED: Reset state verified", test_name);
            test_passed++;
        end else begin
            $error("[%s] FAILED: Reset state incorrect", test_name);
            test_failed++;
        end
    end

    //==========================================================================
    // Test 2: Timeout Error Detection
    //==========================================================================
    initial begin
        test_name = "TEST_02_TIMEOUT";
        init_inputs();

        @(posedge rst_n);
        wait_cycles(20);

        // Inject timeout error
        timeout_error = 1'b1;
        wait_cycles(1);
        timeout_error = 1'b0;

        wait_cycles(2);

        if (error_flags[0] && error_active) begin
            $display("[%s] PASSED: Timeout error detected", test_name);
            test_passed++;
        end else begin
            $error("[%s] FAILED: Timeout error not detected", test_name);
            test_failed++;
        end

        // Clear error
        error_clear = 1'b1;
        wait_cycles(1);
        error_clear = 1'b0;
    end

    //==========================================================================
    // Test 3: Overflow Error Detection
    //==========================================================================
    initial begin
        test_name = "TEST_03_OVERFLOW";
        init_inputs();

        @(posedge rst_n);
        wait_cycles(40);

        overflow_error = 1'b1;
        wait_cycles(1);
        overflow_error = 1'b0;

        wait_cycles(2);

        if (error_flags[1]) begin
            $display("[%s] PASSED: Overflow error detected", test_name);
            test_passed++;
        end else begin
            $error("[%s] FAILED: Overflow error not detected", test_name);
            test_failed++;
        end

        error_clear = 1'b1;
        wait_cycles(1);
        error_clear = 1'b0;
    end

    //==========================================================================
    // Test 4: CRC Error Detection
    //==========================================================================
    initial begin
        test_name = "TEST_04_CRC";
        init_inputs();

        @(posedge rst_n);
        wait_cycles(60);

        crc_error = 1'b1;
        wait_cycles(1);
        crc_error = 1'b0;

        wait_cycles(2);

        if (error_flags[2]) begin
            $display("[%s] PASSED: CRC error detected", test_name);
            test_passed++;
        end else begin
            $error("[%s] FAILED: CRC error not detected", test_name);
            test_failed++;
        end

        error_clear = 1'b1;
        wait_cycles(1);
        error_clear = 1'b0;
    end

    //==========================================================================
    // Test 5: Overexposure Error Detection
    //==========================================================================
    initial begin
        test_name = "TEST_05_OVEREXPOSURE";
        init_inputs();

        @(posedge rst_n);
        wait_cycles(80);

        overexposure_error = 1'b1;
        wait_cycles(1);
        overexposure_error = 1'b0;

        wait_cycles(2);

        if (error_flags[3]) begin
            $display("[%s] PASSED: Overexposure error detected", test_name);
            test_passed++;
        end else begin
            $error("[%s] FAILED: Overexposure error not detected", test_name);
            test_failed++;
        end

        error_clear = 1'b1;
        wait_cycles(1);
        error_clear = 1'b0;
    end

    //==========================================================================
    // Test 6: ROIC Fault Detection
    //==========================================================================
    initial begin
        test_name = "TEST_06_ROIC";
        init_inputs();

        @(posedge rst_n);
        wait_cycles(100);

        roic_fault = 1'b1;
        wait_cycles(1);
        roic_fault = 1'b0;

        wait_cycles(2);

        if (error_flags[4]) begin
            $display("[%s] PASSED: ROIC fault detected", test_name);
            test_passed++;
        end else begin
            $error("[%s] FAILED: ROIC fault not detected", test_name);
            test_failed++;
        end

        error_clear = 1'b1;
        wait_cycles(1);
        error_clear = 1'b0;
    end

    //==========================================================================
    // Test 7: D-PHY Error Detection
    //==========================================================================
    initial begin
        test_name = "TEST_07_DPHY";
        init_inputs();

        @(posedge rst_n);
        wait_cycles(120);

        dphy_error = 1'b1;
        wait_cycles(1);
        dphy_error = 1'b0;

        wait_cycles(2);

        if (error_flags[5]) begin
            $display("[%s] PASSED: D-PHY error detected", test_name);
            test_passed++;
        end else begin
            $error("[%s] FAILED: D-PHY error not detected", test_name);
            test_failed++;
        end

        error_clear = 1'b1;
        wait_cycles(1);
        error_clear = 1'b0;
    end

    //==========================================================================
    // Test 8: Config Error Detection
    //==========================================================================
    initial begin
        test_name = "TEST_08_CONFIG";
        init_inputs();

        @(posedge rst_n);
        wait_cycles(140);

        config_error = 1'b1;
        wait_cycles(1);
        config_error = 1'b0;

        wait_cycles(2);

        if (error_flags[6]) begin
            $display("[%s] PASSED: Config error detected", test_name);
            test_passed++;
        end else begin
            $error("[%s] FAILED: Config error not detected", test_name);
            test_failed++;
        end

        error_clear = 1'b1;
        wait_cycles(1);
        error_clear = 1'b0;
    end

    //==========================================================================
    // Test 9: Watchdog Timer
    //==========================================================================
    initial begin
        test_name = "TEST_09_WATCHDOG";
        init_inputs();

        @(posedge rst_n);
        wait_cycles(160);

        // Wait for watchdog to expire (100 cycles)
        wait_cycles(110);

        if (error_flags[7] && watchdog_expired) begin
            $display("[%s] PASSED: Watchdog timer expired", test_name);
            test_passed++;
        end else begin
            $error("[%s] FAILED: Watchdog did not expire", test_name);
            test_failed++;
        end

        // Reset watchdog via heartbeat
        heartbeat = 1'b1;
        wait_cycles(1);
        heartbeat = 1'b0;

        error_clear = 1'b1;
        wait_cycles(1);
        error_clear = 1'b0;
    end

    //==========================================================================
    // Test 10: Safe State Response (REQ-FPGA-051)
    //==========================================================================
    initial begin
        test_name = "TEST_10_SAFE_STATE";
        init_inputs();

        @(posedge rst_n);
        wait_cycles(200);

        // Inject fatal error (timeout)
        timeout_error = 1'b1;
        wait_cycles(1);
        timeout_error = 1'b0;

        // Measure time to safe state
        logic safe_before = safe_state;
        int cycle_count = 0;
        while (!safe_state && cycle_count < 20) begin
            @(posedge clk_sys);
            cycle_count++;
        end

        if (safe_state && cycle_count <= 10) begin
            $display("[%s] PASSED: Safe state achieved in %d cycles (max 10)",
                     test_name, cycle_count);
            test_passed++;
        end else begin
            $error("[%s] FAILED: Safe state not achieved in time", test_name);
            test_failed++;
        end

        // Check safe state outputs
        if (gate_safe && csi2_disable && buffer_disable) begin
            $display("[%s] PASSED: Safe state outputs correct", test_name);
            test_passed++;
        end else begin
            $error("[%s] FAILED: Safe state outputs incorrect", test_name);
            test_failed++;
        end

        error_clear = 1'b1;
        wait_cycles(1);
        error_clear = 1'b0;
    end

    //==========================================================================
    // Test 11: Error Clearing (REQ-FPGA-053)
    //==========================================================================
    initial begin
        test_name = "TEST_11_ERROR_CLEAR";
        init_inputs();

        @(posedge rst_n);
        wait_cycles(250);

        // Inject error
        overflow_error = 1'b1;
        wait_cycles(1);
        overflow_error = 1'b0;

        wait_cycles(5);

        logic error_before = error_active;

        // Clear error
        error_clear = 1'b1;
        wait_cycles(1);
        error_clear = 1'b0;

        wait_cycles(2);

        if (!error_active && error_flags == 8'h00) begin
            $display("[%s] PASSED: Error cleared successfully", test_name);
            test_passed++;
        end else begin
            $error("[%s] FAILED: Error not cleared", test_name);
            test_failed++;
        end
    end

    //==========================================================================
    // Waveform Dump
    //==========================================================================
    initial begin
        $dumpfile("protection_logic_tb.vcd");
        $dumpvars(0, protection_logic_tb);
    end

    //==========================================================================
    // Test Summary
    //==========================================================================
    final begin
        $display("\n========================================");
        $display("  Protection Logic Testbench Summary");
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

    // Simulation timeout
    initial begin
        #1000000;  // 1 ms simulation time
        $display("Simulation timeout reached!");
        $finish;
    end

endmodule

//==============================================================================
// End of File: protection_logic_tb.sv
//==============================================================================
