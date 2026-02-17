# FPGA Build and Synthesis Guide

**Project**: X-ray Detector Panel System
**Target Device**: Xilinx Artix-7 XC7A35T-FGG484
**Version**: 1.0.0
**Last Updated**: 2026-02-17

---

## 1. Overview

This guide covers the complete FPGA build flow for the X-ray Detector Panel System, from RTL source compilation through synthesis, implementation, bitstream generation, and hardware programming. The FPGA serves as the real-time data acquisition engine controlling panel scan timing, line buffering, and CSI-2 MIPI data transmission.

### 1.1 Build Flow Summary

```
RTL Source (.sv)
    |
    v
[1. Lint & Static Analysis] --> Fix warnings
    |
    v
[2. Behavioral Simulation]  --> Verify functionality
    |
    v
[3. Synthesis]              --> Netlist generation
    |
    v
[4. Implementation]         --> Place & Route
    |   |
    |   +--> [Timing Analysis] --> Fix timing violations
    |
    v
[5. Bitstream Generation]   --> .bit file
    |
    v
[6. Hardware Programming]   --> JTAG download to FPGA
```

### 1.2 Target Device Specifications

| Parameter | Value |
|-----------|-------|
| Family | Artix-7 |
| Device | XC7A35T |
| Package | FGG484 |
| Speed Grade | -2 |
| Part Number | `xc7a35tfgg484-2` |
| LUTs | 20,800 |
| Flip-Flops | 41,600 |
| Block RAM (36Kb) | 50 |
| DSP48E1 | 90 |
| I/O Pins | ~250 |

### 1.3 Resource Budget

| Resource | Budget (60%) | Estimated Usage | Status |
|----------|-------------|-----------------|--------|
| LUTs | 12,480 | 7,100-9,600 (34-46%) | Within budget |
| BRAMs | 30 | 5-7 (10-14%) | Within budget |
| DSPs | 54 | 0-2 | Minimal usage |

---

## 2. Prerequisites

### 2.1 Required Software

| Software | Version | Purpose |
|----------|---------|---------|
| AMD Vivado | 2023.2+ HL Design Edition | Synthesis, implementation, simulation |
| Vivado License | HL Design Edition | Required for MIPI CSI-2 TX IP |

### 2.2 Environment Setup

**Linux**:
```bash
# Source Vivado environment
source /opt/Xilinx/Vivado/2023.2/settings64.sh

# Verify
vivado -version
which vivado

# Add to .bashrc for persistent access
echo 'source /opt/Xilinx/Vivado/2023.2/settings64.sh' >> ~/.bashrc
```

**Windows**:
```cmd
:: Vivado is typically added to PATH during installation
:: Verify from command prompt:
vivado -version
```

### 2.3 License Verification

```bash
# Check license status
vivado -mode tcl -nolog -nojournal -tclargs <<'EOF'
puts [get_property LICENSE_STATUS [get_ips *csi*]]
exit
EOF
```

The MIPI CSI-2 TX Subsystem IP requires HL Design Edition. Verify your license covers this IP.

---

## 3. Project Structure

### 3.1 FPGA Directory Layout

```
fpga/
  rtl/
    csi2_detector_top.sv        # Top-level module
    clk_gen.sv                  # MMCM/PLL clock generation
    panel_scan_fsm.sv           # Scan timing state machine
    line_buffer.sv              # Ping-Pong dual-bank BRAM
    csi2_tx_wrapper.sv          # CSI-2 TX IP wrapper
    spi_slave.sv                # SPI protocol engine + register map
    protection_logic.sv         # Error detection and safe shutdown
    roic_interface.sv           # LVDS deserializer and pixel formatter
  tb/
    tb_top.sv                   # Top-level testbench
    tb_panel_scan_fsm.sv        # FSM unit testbench
    tb_line_buffer.sv           # Line buffer testbench
    tb_spi_slave.sv             # SPI slave testbench
    tb_csi2_tx.sv               # CSI-2 TX testbench
    tb_protection_logic.sv      # Protection logic testbench
    tb_full_frame.sv            # Full-frame end-to-end testbench
  constraints/
    timing.xdc                  # Clock and timing constraints
    pins.xdc                    # Pin assignments
    physical.xdc                # Physical constraints (voltage, I/O std)
  ip/
    mipi_csi2_tx/               # CSI-2 TX Subsystem IP configuration
    clk_wiz/                    # Clocking Wizard IP configuration
  sim/
    Makefile                    # Simulation build scripts
    run_sim.tcl                 # Vivado simulation TCL script
    wave_config.wcfg            # Waveform configuration
  scripts/
    create_project.tcl          # Project creation script
    build.tcl                   # Full build automation script
    program.tcl                 # JTAG programming script
    report.tcl                  # Resource utilization report
```

### 3.2 Module Hierarchy

```
csi2_detector_top
  |-- clk_gen (MMCM/PLL)
  |     |-- clk_sys        (100 MHz, system clock)
  |     |-- clk_pixel      (125.83 MHz, pixel processing)
  |     |-- clk_csi2_byte  (125 MHz, CSI-2 byte clock)
  |     `-- clk_dphy_hs    (500 MHz, D-PHY high-speed DDR)
  |-- spi_slave + register_file
  |-- panel_scan_fsm
  |-- roic_interface
  |-- line_buffer (Ping-Pong BRAM)
  |-- csi2_tx_subsystem (AMD IP v3.1)
  |-- protection_logic
  `-- debug_infrastructure (ILA/VIO)
```

---

## 4. RTL Coding Standards

### 4.1 SystemVerilog Conventions

All RTL must follow these conventions:

```systemverilog
// File: panel_scan_fsm.sv
// Module naming: snake_case
// Signal naming: snake_case
// Parameter naming: UPPER_CASE
// Clock signals: clk_<domain>
// Reset signals: rst_n (active low) or rst (active high)

module panel_scan_fsm #(
    parameter int unsigned ROWS_MAX     = 3072,
    parameter int unsigned COLS_MAX     = 3072,
    parameter int unsigned TIMER_WIDTH  = 16
) (
    input  logic        clk_sys,
    input  logic        rst_n,
    // Control inputs
    input  logic        start_scan,
    input  logic        stop_scan,
    input  logic [1:0]  scan_mode,
    // Timing parameters
    input  logic [TIMER_WIDTH-1:0] gate_on_us,
    input  logic [TIMER_WIDTH-1:0] gate_off_us,
    // Status outputs
    output logic        idle,
    output logic        busy,
    output logic        error,
    output logic [2:0]  fsm_state
);

    // State encoding
    typedef enum logic [2:0] {
        ST_IDLE       = 3'b000,
        ST_INTEGRATE  = 3'b001,
        ST_READOUT    = 3'b010,
        ST_LINE_DONE  = 3'b011,
        ST_FRAME_DONE = 3'b100,
        ST_ERROR      = 3'b101
    } state_t;

    state_t state_q, state_d;

    // Sequential logic: always_ff
    always_ff @(posedge clk_sys or negedge rst_n) begin
        if (!rst_n)
            state_q <= ST_IDLE;
        else
            state_q <= state_d;
    end

    // Combinational logic: always_comb
    always_comb begin
        state_d = state_q; // default: hold state
        case (state_q)
            ST_IDLE: begin
                if (start_scan)
                    state_d = ST_INTEGRATE;
            end
            // ... other states
            default: state_d = ST_IDLE;
        endcase
    end

endmodule
```

### 4.2 RTL Coding Rules

1. **Synchronous design only**: No combinational feedback loops
2. **Explicit clock and reset**: All `always_ff` blocks use explicit clock/reset
3. **No latches**: Use `always_comb` for combinational, `always_ff` for sequential
4. **CDC handling**: 2-stage FF synchronizers for all clock domain crossings
5. **Reset strategy**: Active-low async reset for safety-critical outputs; sync reset elsewhere

---

## 5. Simulation

### 5.1 Run Behavioral Simulation

**Using Vivado xsim**:

```bash
cd fpga/sim

# Compile all RTL and testbench files
xvlog -sv ../rtl/*.sv ../tb/*.sv

# Elaborate the testbench
xelab -debug all -top tb_top -snapshot tb_snap

# Run simulation (batch mode)
xsim tb_snap -runall -log sim.log

# Run simulation (GUI mode with waveforms)
xsim tb_snap -gui
```

**Using Makefile**:

```bash
cd fpga/sim

# Run all testbenches
make all

# Run specific testbench
make test_fsm
make test_line_buffer
make test_spi
make test_csi2

# Run with coverage
make coverage

# Clean simulation artifacts
make clean
```

### 5.2 Testbench Categories

| Testbench | Module Under Test | Key Verification Points |
|-----------|------------------|------------------------|
| `tb_panel_scan_fsm` | panel_scan_fsm | All 6 states, transitions, timing accuracy |
| `tb_line_buffer` | line_buffer | Ping-pong operation, CDC, overflow detection |
| `tb_spi_slave` | spi_slave | Register R/W, unmapped address, CS abort |
| `tb_csi2_tx` | csi2_tx_wrapper | Packet format, CRC-16, D-PHY output |
| `tb_protection_logic` | protection_logic | All 8 error codes, safe state |
| `tb_full_frame` | csi2_detector_top | End-to-end frame capture and transmission |

### 5.3 Coverage Collection

```bash
# Run simulation with coverage collection
xsim tb_snap -runall -testplusarg COVERAGE

# Generate coverage report
# Vivado: Tools > Report > Code Coverage
# Or use the TCL command:
report_coverage -file coverage_report.txt
```

**Coverage Targets**:
- Line Coverage: >= 95%
- Branch Coverage: >= 90%
- FSM State Coverage: 100% (all states visited)
- FSM Transition Coverage: 100% (all valid transitions exercised)

### 5.4 Golden Reference Comparison

Compare RTL simulation output against the FpgaSimulator (C#) golden reference:

```bash
# 1. Generate expected output from FpgaSimulator
dotnet run --project tools/FpgaSimulator -- \
    --config config/detector_config.yaml \
    --output sim/expected_output.bin

# 2. Run RTL simulation producing actual output
cd fpga/sim
make golden_ref_test

# 3. Compare outputs
diff expected_output.bin actual_output.bin
# Expected: Files are identical (bit-exact match)
```

---

## 6. Synthesis

### 6.1 Run Synthesis

**Vivado GUI**:
1. Open the Vivado project
2. Click "Run Synthesis" in the Flow Navigator
3. Wait for synthesis to complete (typically 5-15 minutes)

**Vivado Batch Mode**:

```bash
cd fpga

# Run synthesis only
vivado -mode batch -source scripts/synth.tcl -log synth.log -journal synth.jou

# Or use the full build script
vivado -mode batch -source scripts/build.tcl -log build.log
```

**Example synth.tcl**:

```tcl
# Open project
open_project fpga/fpga.xpr

# Run synthesis
reset_run synth_1
launch_runs synth_1 -jobs 4
wait_on_run synth_1

# Check results
if {[get_property STATUS [get_runs synth_1]] != "synth_design Complete!"} {
    puts "ERROR: Synthesis failed!"
    exit 1
}

# Generate utilization report
open_run synth_1
report_utilization -file reports/post_synth_utilization.rpt
report_timing_summary -file reports/post_synth_timing.rpt
```

### 6.2 Synthesis Settings

| Setting | Value | Rationale |
|---------|-------|-----------|
| Strategy | Vivado Synthesis Defaults | Balanced optimization |
| Flatten Hierarchy | rebuilt | Allow cross-module optimization |
| FSM Encoding | auto | Let Vivado choose optimal encoding |
| Resource Sharing | on | Reduce LUT usage |
| SRL Style | register | Predictable timing |

### 6.3 Post-Synthesis Checks

After synthesis completes, verify:

```tcl
# 1. Check for critical warnings
report_compile_order -constraints
report_drc -file reports/post_synth_drc.rpt

# 2. Check utilization
report_utilization -file reports/post_synth_utilization.rpt
# Verify: LUT < 60%, BRAM < 50%

# 3. Check for latches (should be zero)
report_utilization -cells -include_replicated_objects

# 4. Check timing estimates
report_timing_summary -max_paths 10 -file reports/post_synth_timing.rpt
# Verify: WNS > 0
```

---

## 7. Implementation (Place & Route)

### 7.1 Run Implementation

```bash
# Vivado batch mode
vivado -mode batch -source scripts/impl.tcl -log impl.log
```

**Example impl.tcl**:

```tcl
open_project fpga/fpga.xpr

# Launch implementation
reset_run impl_1
launch_runs impl_1 -jobs 4
wait_on_run impl_1

# Check results
if {[get_property STATUS [get_runs impl_1]] != "route_design Complete!"} {
    puts "ERROR: Implementation failed!"
    exit 1
}

# Generate reports
open_run impl_1
report_utilization -file reports/post_impl_utilization.rpt
report_timing_summary -max_paths 20 -file reports/post_impl_timing.rpt
report_clock_utilization -file reports/post_impl_clocks.rpt
report_drc -file reports/post_impl_drc.rpt
report_cdc -file reports/post_impl_cdc.rpt
report_power -file reports/post_impl_power.rpt
```

### 7.2 Implementation Settings

| Setting | Value | Rationale |
|---------|-------|-----------|
| Strategy | Performance_ExplorePostRoutePhysOpt | Maximize timing closure |
| Incremental Compile | auto | Faster rebuilds |
| Physical Optimization | enabled | Improve timing post-route |

### 7.3 Timing Closure

**Primary Clock Constraints** (from `timing.xdc`):

```tcl
# Input clock
create_clock -period 10.000 -name clk_100mhz [get_ports clk_100mhz]

# MMCM-generated clocks are automatically derived
# clk_sys:       100.000 MHz (period = 10.000 ns)
# clk_csi2_byte: 125.000 MHz (period =  8.000 ns)
# clk_dphy_hs:   500.000 MHz (period =  2.000 ns)

# Asynchronous clock groups
set_clock_groups -asynchronous \
    -group [get_clocks clk_sys] \
    -group [get_clocks clk_roic]

set_clock_groups -asynchronous \
    -group [get_clocks clk_roic] \
    -group [get_clocks clk_csi2_byte]

# SPI clock
create_clock -period 20.000 -name spi_sclk [get_ports spi_sclk]
set_clock_groups -asynchronous \
    -group [get_clocks spi_sclk] \
    -group [get_clocks clk_sys]
```

**Timing Closure Checklist**:

1. WNS (Worst Negative Slack) >= 1 ns for all clocks
2. WHS (Worst Hold Slack) >= 0 ns
3. No timing violations on setup or hold paths
4. CDC report shows zero violations

**If timing fails**:

| Symptom | Cause | Fix |
|---------|-------|-----|
| WNS < 0 on clk_dphy_hs | 500 MHz path too long | Add pipeline registers in D-PHY output path |
| WNS < 0 on clk_csi2_byte | Complex logic in CSI-2 path | Break combinational logic into multiple stages |
| Hold violations | Over-constrained paths | Relax I/O timing constraints or add buffers |
| CDC violations | Missing synchronizer | Add 2-stage FF synchronizer on CDC path |

### 7.4 Post-Implementation Reports

Check these reports after every implementation run:

| Report | File | What to Verify |
|--------|------|----------------|
| Utilization | `post_impl_utilization.rpt` | LUT < 60%, BRAM < 50% |
| Timing | `post_impl_timing.rpt` | WNS >= 1 ns, WHS >= 0 |
| CDC | `post_impl_cdc.rpt` | Zero violations |
| Power | `post_impl_power.rpt` | Total < 2 W |
| DRC | `post_impl_drc.rpt` | No critical violations |

---

## 8. Bitstream Generation

### 8.1 Generate Bitstream

```bash
# Vivado batch mode
vivado -mode batch -source scripts/bitstream.tcl
```

**Example bitstream.tcl**:

```tcl
open_project fpga/fpga.xpr
open_run impl_1

# Generate bitstream
write_bitstream -force fpga/output/csi2_detector_top.bit

# Generate debug probes file (if ILA is instantiated)
write_debug_probes -force fpga/output/csi2_detector_top.ltx

puts "Bitstream generated: fpga/output/csi2_detector_top.bit"
```

### 8.2 Bitstream Configuration

| Setting | Value | Rationale |
|---------|-------|-----------|
| Startup Clock | CCLK | Use configuration clock |
| Configuration Rate | 33 MHz | Fast configuration |
| Compress Bitstream | Yes | Reduce file size |
| Security | No encryption | Development builds |

### 8.3 Bitstream Files

| File | Extension | Purpose |
|------|-----------|---------|
| Bitstream | `.bit` | FPGA programming file |
| Debug Probes | `.ltx` | ILA/VIO probe definitions |
| Binary | `.bin` | Flash programming file |
| MCS | `.mcs` | PROM programming file |

---

## 9. Hardware Programming

### 9.1 JTAG Programming

**Using Vivado Hardware Manager (GUI)**:

1. Connect JTAG cable (Platform Cable USB or Digilent HS2)
2. Open Hardware Manager (Flow Navigator > Program and Debug > Open Hardware Manager)
3. Click "Auto Connect" to detect the FPGA
4. Right-click the device > Program Device
5. Select the `.bit` file and `.ltx` file (if available)
6. Click "Program"

**Using Vivado Batch Mode**:

```bash
vivado -mode batch -source scripts/program.tcl
```

**Example program.tcl**:

```tcl
# Open Hardware Manager
open_hw_manager
connect_hw_server -allow_non_jtag

# Find and connect to the FPGA
open_hw_target

# Get the FPGA device
set device [get_hw_devices xc7a35t_0]
current_hw_device $device

# Set bitstream file
set_property PROGRAM.FILE {fpga/output/csi2_detector_top.bit} $device
set_property PROBES.FILE {fpga/output/csi2_detector_top.ltx} $device

# Program the device
program_hw_devices $device
puts "Programming complete!"

# Close
close_hw_manager
```

### 9.2 Flash Programming

For non-volatile programming (bitstream loads on power-up):

```tcl
# Create MCS file from bitstream
write_cfgmem -format mcs -interface SPIx4 -size 16 \
    -loadbit "up 0x0 fpga/output/csi2_detector_top.bit" \
    -force fpga/output/csi2_detector_top.mcs

# Program flash via JTAG
# (Use Hardware Manager or batch TCL)
```

### 9.3 Post-Programming Verification

After programming the FPGA, verify operation:

1. **Status LEDs**: Check LED[3:0] for expected pattern
2. **Heartbeat**: Verify heartbeat LED is toggling (~1 Hz)
3. **SPI Communication**: From SoC, read DEVICE_ID register (0xF0)
   ```bash
   # Expected: 0xA735
   ssh root@192.168.1.100 "detector_cli read-reg 0xF0"
   ```
4. **ILA Debug**: Open Hardware Manager > ILA probes to verify internal signals

---

## 10. Full Build Automation

### 10.1 Automated Build Script

```bash
cd fpga

# Full build: synthesis + implementation + bitstream
vivado -mode batch -source scripts/build.tcl -log build.log 2>&1
```

**Example build.tcl** (complete automation):

```tcl
# ============================================================
# Full FPGA Build Script
# Target: Artix-7 XC7A35T-FGG484
# ============================================================

set project_name "csi2_detector"
set project_dir  "fpga"
set output_dir   "fpga/output"
set report_dir   "fpga/reports"

# Create output directories
file mkdir $output_dir
file mkdir $report_dir

# Open or create project
if {[file exists ${project_dir}/${project_name}.xpr]} {
    open_project ${project_dir}/${project_name}.xpr
} else {
    source scripts/create_project.tcl
}

# ---- Synthesis ----
puts "========== SYNTHESIS =========="
reset_run synth_1
launch_runs synth_1 -jobs 4
wait_on_run synth_1

if {[get_property STATUS [get_runs synth_1]] != "synth_design Complete!"} {
    puts "ERROR: Synthesis failed!"
    exit 1
}

open_run synth_1
report_utilization -file ${report_dir}/post_synth_utilization.rpt
report_timing_summary -file ${report_dir}/post_synth_timing.rpt

# Check LUT utilization
set lut_used [get_property UTIL [get_report_configs {synth_1_utilization_report}]]
puts "Post-synthesis LUT utilization: ${lut_used}%"

# ---- Implementation ----
puts "========== IMPLEMENTATION =========="
reset_run impl_1
launch_runs impl_1 -jobs 4
wait_on_run impl_1

if {[get_property STATUS [get_runs impl_1]] != "route_design Complete!"} {
    puts "ERROR: Implementation failed!"
    exit 1
}

open_run impl_1
report_utilization -file ${report_dir}/post_impl_utilization.rpt
report_timing_summary -max_paths 20 -file ${report_dir}/post_impl_timing.rpt
report_cdc -file ${report_dir}/post_impl_cdc.rpt
report_power -file ${report_dir}/post_impl_power.rpt
report_drc -file ${report_dir}/post_impl_drc.rpt

# Check timing
set wns [get_property STATS.WNS [get_runs impl_1]]
if {$wns < 0} {
    puts "WARNING: Timing not met! WNS = ${wns} ns"
}

# ---- Bitstream ----
puts "========== BITSTREAM =========="
launch_runs impl_1 -to_step write_bitstream -jobs 4
wait_on_run impl_1

file copy -force ${project_dir}/${project_name}.runs/impl_1/${project_name}_top.bit \
    ${output_dir}/${project_name}.bit

puts "========== BUILD COMPLETE =========="
puts "Bitstream: ${output_dir}/${project_name}.bit"
puts "Reports:   ${report_dir}/"

exit 0
```

### 10.2 Incremental Builds

For faster iteration during development:

```tcl
# Enable incremental compile
set_property AUTO_INCREMENTAL_CHECKPOINT 1 [current_project]

# Run incremental synthesis (reuses previous results)
launch_runs synth_1 -jobs 4
```

---

## 11. Debug Infrastructure

### 11.1 ILA (Integrated Logic Analyzer) Setup

Add ILA probes to key signals for runtime debugging:

```systemverilog
// In csi2_detector_top.sv
ila_0 u_ila (
    .clk    (clk_sys),
    .probe0 (fsm_state),           // [2:0] FSM state
    .probe1 (pixel_data),          // [15:0] pixel data
    .probe2 (line_valid),          // line valid signal
    .probe3 (frame_valid),         // frame valid signal
    .probe4 (spi_transaction),     // SPI activity
    .probe5 (error_flags),         // [7:0] error flags
    .probe6 (csi2_tx_tvalid),      // CSI-2 AXI-S valid
    .probe7 (csi2_tx_tready)       // CSI-2 AXI-S ready
);
```

**ILA Resource Cost**: ~500-1,000 LUTs, 1-2 BRAMs

### 11.2 VIO (Virtual I/O) Setup

```systemverilog
// Runtime-adjustable debug controls
vio_0 u_vio (
    .clk        (clk_sys),
    .probe_in0  (frame_counter),    // Read: current frame count
    .probe_in1  (error_flags),      // Read: error status
    .probe_out0 (debug_start_scan), // Write: manual scan trigger
    .probe_out1 (debug_lane_speed)  // Write: lane speed override
);
```

---

## 12. Troubleshooting

### 12.1 Synthesis Issues

| Issue | Cause | Solution |
|-------|-------|---------|
| "Cannot find module" | Missing source file | Add file to project sources |
| Latch inference warning | Incomplete case statement | Add default branch to all case/if blocks |
| Multi-driven net | Same signal driven from multiple always blocks | Consolidate drivers into single block |
| MMCM configuration error | Invalid clock frequency ratio | Check MMCM parameters against device limits |

### 12.2 Implementation Issues

| Issue | Cause | Solution |
|-------|-------|---------|
| Timing failure (WNS < 0) | Critical path too long | Add pipeline registers, optimize logic |
| Placement failure | I/O bank conflicts | Review pin assignments, separate LVDS/LVCMOS |
| Routing congestion | High utilization in small area | Spread placement, reduce LUT usage |
| CDC violation | Missing synchronizer | Add 2-stage FF synchronizer |

### 12.3 Programming Issues

| Issue | Cause | Solution |
|-------|-------|---------|
| "No hardware targets found" | JTAG cable not connected | Check USB connection, install drivers |
| "Device not found" | Wrong JTAG chain | Verify device in chain, check JTAG settings |
| "Programming failed" | Bitstream/device mismatch | Verify target device matches bitstream |
| FPGA doesn't operate after programming | Configuration error | Check startup clock, verify power supply |

---

## 13. Build Checklist

Before releasing a bitstream, verify all items:

- [ ] All RTL lint warnings resolved
- [ ] All simulation testbenches pass
- [ ] Coverage: Line >= 95%, Branch >= 90%, FSM 100%
- [ ] Golden reference comparison: bit-exact match
- [ ] LUT utilization < 60% (12,480 LUTs)
- [ ] BRAM utilization < 50% (25 BRAMs)
- [ ] WNS >= 1 ns for all clock domains
- [ ] WHS >= 0 ns for all paths
- [ ] CDC report: zero violations
- [ ] Power estimate < 2 W
- [ ] DRC: no critical violations
- [ ] DEVICE_ID register (0xF0) reads 0xA735
- [ ] Heartbeat LED operational
- [ ] SPI communication verified from SoC

---

## 14. Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (architect) | Initial FPGA build and synthesis guide |

---
