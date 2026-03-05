# FPGA Testbench Framework

## Overview

This directory contains SystemVerilog testbenches for all FPGA RTL modules. Testbenches follow TDD methodology: tests are written first to define expected behavior, then RTL implementation is verified against these tests.

## Directory Structure

```
fpga/
├── rtl/           # RTL modules (panel_scan_fsm.sv, line_buffer.sv, etc.)
├── tb/            # Testbenches (this directory)
│   ├── README.md  # This file
│   ├── common/    # Shared testbench utilities
│   └── *_tb.sv    # Module-specific testbenches
├── sim/           # Simulation scripts and outputs
└── syn/           # Synthesis scripts and reports
```

## Coverage Targets

| Metric | Target | Measurement Tool |
|--------|--------|------------------|
| Line Coverage | >= 95% | Vivado xsim / Questa |
| Branch Coverage | >= 90% | Vivado xsim / Questa |
| FSM State Coverage | 100% | All states + transitions |
| Toggle Coverage | >= 80% | I/O ports only |

## Testbench Naming Convention

- RTL module: `<module_name>.sv`
- Testbench: `<module_name>_tb.sv`
- Example: `panel_scan_fsm.sv` → `panel_scan_fsm_tb.sv`

## Common Testbench Utilities

### File: `tb/common/utils.sv`

```systemverilog
// Common clock generation
task automatic clock_gen(ref logic clk, input time period);
  forever #(period/2) clk = ~clk;
endtask

// Common reset sequence
task automatic reset_sequence(ref logic rst_n, input cycles = 5);
  rst_n = 0;
  repeat(cycles) @(posedge clk);
  rst_n = 1;
endtask

// Coverage reporting
task automatic report_coverage();
  $display("=== Coverage Report ===");
  $display("Line Coverage: %0d%%", $get_coverage());
  // Additional coverage reporting
endtask
```

## Testbench Template

Each testbench follows this structure:

```systemverilog
`timescale 1ns/1ps

module <module_name>_tb;

  // ========================================================================
  // Parameters
  // ========================================================================
  parameter CLK_PERIOD = 10;  // 100 MHz
  parameter TIMEOUT_CYCLES = 100000;

  // ========================================================================
  // DUT Signals
  // ========================================================================
  logic clk;
  logic rst_n;
  // ... other DUT ports

  // ========================================================================
  // Clock Generation
  // ========================================================================
  initial clock_gen(clk, CLK_PERIOD);

  // ========================================================================
  // DUT Instantiation
  // ========================================================================
  <module_name> dut (
    .clk(clk),
    .rst_n(rst_n),
    // ... other connections
  );

  // ========================================================================
  // Test Program
  // ========================================================================
  initial begin
    // Initialize signals
    // Apply reset
    // Run test scenarios
    // Report results
  end

  // ========================================================================
  // Assertions
  // ========================================================================
  // Self-checking assertions for critical properties

  // ========================================================================
  // Covergroups
  // ========================================================================
  // Coverage groups for configurable parameters

  // ========================================================================
  // Scoreboard
  // ========================================================================
  // Expected vs actual output comparison

endmodule
```

## Test Scenario Execution Order

1. **Initialization**: Reset DUT to known state
2. **Configuration**: Write registers via SPI
3. **Operation**: Stimulus based on test case
4. **Verification**: Check outputs against expected values
5. **Cleanup**: Return to idle state
6. **Reporting**: Log pass/fail and coverage

## Running Simulations

### Vivado xsim

```bash
# Compile
xvlog -sv <module>_tb.sv ../rtl/<module>.sv

# Elaborate
xelab -debug typical <module>_tb -s <module>_sim

# Simulate
xsim <module>_sim -runall
```

### Questa

```bash
# Compile
vlog -sv <module>_tb.sv ../rtl/<module>.sv

# Simulate with coverage
vsim -c <module>_tb -do "run -all; coverage report -html"
```

## Acceptance Criteria Verification

Each testbench maps to specific acceptance criteria from `SPEC-FPGA-001`:

| Testbench | AC Coverage | Key Verification Points |
|-----------|-------------|-------------------------|
| panel_scan_fsm_tb | AC-FPGA-002 | FSM 100% state/transition |
| line_buffer_tb | AC-FPGA-003, AC-FPGA-008 | Data integrity, CDC |
| csi2_tx_wrapper_tb | AC-FPGA-004 | Packet correctness, CRC |
| spi_slave_tb | AC-FPGA-005 | Register access, protocol |
| protection_logic_tb | AC-FPGA-006 | Error response, safe state |

## Continuous Integration

All testbenches run automatically on RTL changes:
- Pre-commit: Quick smoke tests (core functionality)
- Post-push: Full regression suite with coverage
- Nightly: Extended simulations (throughput, stress tests)

## References

- SPEC-FPGA-001: `.moai/specs/SPEC-FPGA-001/spec.md`
- Acceptance Criteria: `.moai/specs/SPEC-FPGA-001/acceptance.md`
- Architecture: `docs/architecture/fpga-design.md`
