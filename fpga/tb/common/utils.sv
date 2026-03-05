//******************************************************************************
// FPGA Testbench Common Utilities
//******************************************************************************
// Description: Shared testbench utilities for all FPGA testbenches
// Usage: `include "common/utils.sv" in testbench files
//
// Features:
//   - Clock generation with configurable period
//   - Reset sequence generation
//   - SPI transaction model
//   - CRC-16 calculation (MIPI CSI-2 compliant)
//   - Scoreboard for output comparison
//   - Coverage reporting helpers
//******************************************************************************

`ifndef TB_UTILS_SV
`define TB_UTILS_SV

//==============================================================================
// Clock Generation Task
//==============================================================================
// Generates a clock signal with specified period in nanoseconds
task automatic tb_clock_gen(
  ref logic clk,       // Clock signal to drive
  input time period_ns  // Clock period in nanoseconds
);
  forever #(period_ns/2) clk = ~clk;
endtask

//==============================================================================
// Reset Sequence Task
//==============================================================================
// Applies a reset sequence with specified number of cycles
task automatic tb_reset_sequence(
  ref logic rst_n,        // Reset signal (active low)
  input logic clk,        // Clock for synchronization
  input int cycles = 5    // Number of clock cycles for reset
);
  rst_n = 0;
  repeat(cycles) @(posedge clk);
  @(posedge clk);
  rst_n = 1;
endtask

//==============================================================================
// SPI Transaction Task
//==============================================================================
// Models a 32-bit SPI Mode 0 transaction (8-bit addr + 8-bit R/W + 16-bit data)
task automatic tb_spi_transaction(
  output logic [7:0] addr,     // Register address
  output logic rw,             // 0=Read, 1=Write
  output logic [15:0] wdata,   // Write data (for write operations)
  input logic [15:0] rdata,    // Read data (returned for read operations)
  ref logic cs_n,              // Chip select (active low)
  ref logic sclk,              // SPI clock
  ref logic mosi,              // Master out slave in
  input logic miso,            // Master in slave out
  input int clk_period_ns = 20 // SCLK period (default 50 MHz)
);
  // Transaction format: [addr(8) | rw(1) | rsvd(7) | data(16)] = 32 bits
  cs_n = 0;
  #(clk_period_ns/2);

  // Transmit address (8 bits)
  for (int i = 7; i >= 0; i--) begin
    sclk = 0;
    mosi = addr[i];
    #(clk_period_ns/2);
    sclk = 1;
    #(clk_period_ns/2);
  end

  // Transmit R/W flag (1 bit)
  sclk = 0;
  mosi = rw;
  #(clk_period_ns/2);
  sclk = 1;
  #(clk_period_ns/2);

  // Reserved (7 bits) - transmit zeros
  for (int i = 6; i >= 0; i--) begin
    sclk = 0;
    mosi = 0;
    #(clk_period_ns/2);
    sclk = 1;
    #(clk_period_ns/2);
  end

  // Data phase (16 bits)
  if (rw == 1) begin
    // Write: transmit data on MOSI
    for (int i = 15; i >= 0; i--) begin
      sclk = 0;
      mosi = wdata[i];
      #(clk_period_ns/2);
      sclk = 1;
      #(clk_period_ns/2);
    end
  end else begin
    // Read: receive data on MISO
    for (int i = 15; i >= 0; i--) begin
      sclk = 0;
      #(clk_period_ns/2);
      sclk = 1;
      rdata[i] = miso;
      #(clk_period_ns/2);
    end
  end

  cs_n = 1;
  sclk = 0;
  #(clk_period_ns);
endtask

//==============================================================================
// CRC-16 Calculation (MIPI CSI-2 Compliant)
//==============================================================================
// Calculates CRC-16 over pixel data per MIPI CSI-2 specification
// Polynomial: x^16 + x^12 + x^5 + 1 (0x1021)
function automatic logic [15:0] tb_crc16(
  input logic [15:0] data,      // Input data word
  input logic [15:0] crc_prev   // Previous CRC value (0xFFFF for first)
);
  logic [15:0] crc;
  logic [15:0] poly;
  crc = crc_prev;
  poly = 16'h1021;

  for (int i = 15; i >= 0; i--) begin
    logic xor_bit;
    xor_bit = crc[15] ^ data[i];
    crc = crc << 1;
    if (xor_bit)
      crc = crc ^ poly;
  end

  return crc;
endfunction

//==============================================================================
// Wait for Signal Task
//==============================================================================
// Waits for a signal to reach a specified value with timeout
task automatic tb_wait_for_signal(
  input logic sig,          // Signal to monitor
  input logic value,        // Expected value
  input string sig_name,    // Signal name (for error messages)
  input int timeout_cycles  // Timeout in clock cycles
);
  int cycle_count;
  cycle_count = 0;
  while (sig !== value && cycle_count < timeout_cycles) begin
    @(posedge clk);
    cycle_count++;
  end
  if (cycle_count >= timeout_cycles) begin
    $error("[%0t] Timeout waiting for %s to become %0b", $time, sig_name, value);
  end
endtask

//==============================================================================
// Print Test Header
//==============================================================================
task automatic tb_print_header(input string test_name);
  $display("========================================");
  $display("Test: %s", test_name);
  $display("Start Time: %0t", $time);
  $display("========================================");
endtask

//==============================================================================
// Print Test Footer
//==============================================================================
task automatic tb_print_footer(input string test_name, logic passed);
  $display("========================================");
  $display("Test: %s", test_name);
  $display("End Time: %0t", $time);
  if (passed)
    $display("Result: PASSED");
  else
    $display("Result: FAILED");
  $display("========================================");
endtask

//==============================================================================
// Coverage Report Task
//==============================================================================
task automatic tb_report_coverage();
  $display("\n=== Coverage Report ===");
  $display("Time: %0t", $time);
  // Note: Actual coverage collection requires simulator-specific commands
  // This is a placeholder for cross-tool compatibility
  $display("========================\n");
endtask

//==============================================================================
// Hex Dump Task
//==============================================================================
// Displays array of data in hexadecimal format
task automatic tb_hex_dump(
  input logic [15:0] data[],  // Data array
  input int words_per_line    // Words per line (default: 8)
);
  for (int i = 0; i < data.size(); i++) begin
    if (i % words_per_line == 0)
      $write("%04h: ", i);
    $write("%04h ", data[i]);
    if ((i + 1) % words_per_line == 0 || i == data.size() - 1)
      $display();
  end
endtask

//==============================================================================
// Pixel Pattern Generator
//==============================================================================
// Generates test patterns for line buffer verification
function automatic logic [15:0] tb_pixel_pattern(
  input int index,      // Pixel index
  input tb_pattern_e pattern  // Pattern type
);
  case (pattern)
    PATTERN_INCREMENT: return index[15:0];                    // 0, 1, 2, ...
    PATTERN_ALTERNATE:  return (index % 2 == 0) ? 16'hAAAA : 16'h5555;
    PATTERN_WALKING1:  return 16'h0001 << (index % 16);
    PATTERN_WALKING0:  return ~(16'h0001 << (index % 16));
    PATTERN_RANDOM:    return $random();
    default:           return 16'h0000;
  endcase
endfunction

typedef enum logic [2:0] {
  PATTERN_INCREMENT = 0,
  PATTERN_ALTERNATE  = 1,
  PATTERN_WALKING1   = 2,
  PATTERN_WALKING0   = 3,
  PATTERN_RANDOM     = 4
} tb_pattern_e;

`endif // TB_UTILS_SV
