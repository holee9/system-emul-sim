#==============================================================================
# FPGA RTL Makefile
#==============================================================================
# Author: FPGA RTL Developer
# Date: 2026-02-18
#
# Description:
#   Makefile for compiling, simulating, and synthesizing FPGA RTL modules.
#
# Targets:
#   make all          - Build all modules
#   make sim-<module> - Simulate specific module
#   make sim-all      - Simulate all modules
#   make synth        - Run synthesis (requires Vivado)
#   make clean        - Clean build artifacts
#
#==============================================================================

# Project directories
PROJECT_DIR := $(shell pwd)
RTL_DIR     := $(PROJECT_DIR)/rtl
TB_DIR      := $(PROJECT_DIR)/tb
SIM_DIR     := $(PROJECT_DIR)/sim
LOG_DIR     := $(SIM_DIR)/logs
SCRIPT_DIR  := $(PROJECT_DIR)/scripts

# Toolchain
XVLOG      ?= xvlog
XELAB      ?= xelab
XSIM       ?= xsim
VIVADO     ?= vivado

# Module list
MODULES    := panel_scan_fsm line_buffer csi2_tx_wrapper spi_slave protection_logic
TOP_MODULE := csi2_detector_top

# Source files
RTL_SOURCES  := $(wildcard $(RTL_DIR)/*.sv)
TB_SOURCES   := $(wildcard $(TB_DIR)/*.sv)

#==============================================================================
# Phony Targets
#==============================================================================
.PHONY: all clean sim-all synth help lint coverage

#==============================================================================
# Default Target
#==============================================================================
all: $(TOP_MODULE)

#==============================================================================
# Simulation Targets
#==============================================================================
sim-all: $(MODULES) $(TOP_MODULE)

$(MODULES): %:
	@echo "=========================================="
	@echo "Simulating: $@"
	@echo "=========================================="
	@chmod +x $(SIM_DIR)/run_sim.sh
	@$(SIM_DIR)/run_sim.sh $@ xsim

$(TOP_MODULE): csi2_detector_top
	@echo "=========================================="
	@echo "Simulating Top-Level"
	@echo "=========================================="
	@chmod +x $(SIM_DIR)/run_sim.sh
	@$(SIM_DIR)/run_sim.sh $@ xsim

#==============================================================================
# Synthesis Target (requires Vivado project)
#==============================================================================
synth:
	@echo "Running synthesis..."
	@chmod +x $(SCRIPT_DIR)/run_synth.tcl
	@$(VIVADO) -mode batch -source $(SCRIPT_DIR)/run_synth.tcl

#==============================================================================
# Lint Target
#==============================================================================
lint:
	@echo "Running Verilator lint..."
	@which verilator > /dev/null || (echo "Error: verilator not found"; exit 1)
	@verilator --sv --lint-only -Wall \
		--top-module $(TOP_MODULE) \
		$(RTL_SOURCES) \
		-I$(RTL_DIR)

#==============================================================================
# Coverage Target (requires test run)
#==============================================================================
coverage:
	@echo "Generating coverage report..."
	@echo "Coverage data in: $(SIM_DIR)/coverage/"

#==============================================================================
# Clean Target
#==============================================================================
clean:
	@echo "Cleaning build artifacts..."
	@rm -rf $(SIM_DIR)/*.work
	@rm -rf $(SIM_DIR)/.Xil
	@rm -rf $(SIM_DIR)/xsim.dir
	@rm -rf $(SIM_DIR)/work
	@rm -f $(SIM_DIR)/*.jou
	@rm -f $(SIM_DIR)/*.log
	@rm -f $(SIM_DIR)/*.pb
	@rm -f $(LOG_DIR)/*.log
	@rm -f *.vcd
	@rm -f *.wcfg
	@echo "Clean complete."

#==============================================================================
# Help Target
#==============================================================================
help:
	@echo "FPGA RTL Makefile"
	@echo ""
	@echo "Available targets:"
	@echo "  make all          - Build all modules"
	@echo "  make sim-all      - Simulate all modules"
	@echo "  make sim-<module> - Simulate specific module"
	@echo "                     Available: $(MODULES) $(TOP_MODULE)"
	@echo "  make synth        - Run synthesis (requires Vivado)"
	@echo "  make lint         - Run Verilator lint"
	@echo "  make coverage     - Generate coverage report"
	@echo "  make clean        - Clean build artifacts"
	@echo "  make help         - Show this help message"
	@echo ""
	@echo "Examples:"
	@echo "  make sim-panel_scan_fsm"
	@echo "  make sim-all"
	@echo "  make synth"

#==============================================================================
# End of Makefile
#==============================================================================
