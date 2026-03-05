#!/bin/bash
#==============================================================================
# FPGA RTL Simulation Script
#==============================================================================
# Author: FPGA RTL Developer
# Date: 2026-02-18
#
# Description:
#   Run simulation tests for FPGA RTL modules using Vivado xsim or Questa.
#
# Usage:
#   ./run_sim.sh [module] [tool]
#
# Examples:
#   ./run_sim.sh panel_scan_fsm xsim
#   ./run_sim.sh all questa
#
#==============================================================================

set -e  # Exit on error

#==========================================================================
# Configuration
#==========================================================================
PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RTL_DIR="${PROJECT_DIR}/rtl"
TB_DIR="${PROJECT_DIR}/tb"
SIM_DIR="${PROJECT_DIR}/sim"
LOG_DIR="${SIM_DIR}/logs"

# Create directories
mkdir -p "${SIM_DIR}"
mkdir -p "${LOG_DIR}"

# Module list
MODULES=(
    "panel_scan_fsm"
    "line_buffer"
    "csi2_tx_wrapper"
    "spi_slave"
    "protection_logic"
    "csi2_detector_top"
)

# Tool selection
TOOL=${2:-xsim}  # Default to xsim

#==========================================================================
# Functions
#==========================================================================

usage() {
    echo "Usage: $0 [module|all] [xsim|questa]"
    echo ""
    echo "Modules:"
    for mod in "${MODULES[@]}"; do
        echo "  - $mod"
    done
    echo "  - all (run all modules)"
    echo ""
    echo "Tools:"
    echo "  - xsim (Vivado simulator, default)"
    echo "  - questa (Questa/ModelSim)"
    exit 1
}

compile_xsim() {
    local module=$1
    local tb="${module}_tb"

    echo "=========================================="
    echo "Compiling ${module} for xsim..."
    echo "=========================================="

    # Analyze RTL files
    xvlog -sv \
        -work "${SIM_DIR}/${module}.work" \
        -log "${LOG_DIR}/${module}_compile.log" \
        "${RTL_DIR}/${module}.sv"

    # Analyze testbench
    xelab -sv \
        -work "${SIM_DIR}/${module}.work" \
        -debug typical \
        -log "${LOG_DIR}/${module}_elab.log" \
        "${tb}" \
        -s "${module}_sim"

    echo "Compilation complete: ${module}"
}

run_xsim() {
    local module=$1

    echo "=========================================="
    echo "Running ${module} simulation..."
    echo "=========================================="

    xsim "${module}_sim" \
        -log "${LOG_DIR}/${module}_run.log" \
        -runall

    echo "Simulation complete: ${module}"
}

compile_questa() {
    local module=$1
    local tb="${module}_tb"

    echo "=========================================="
    echo "Compiling ${module} for Questa..."
    echo "=========================================="

    # Create library
    vlib "${SIM_DIR}/work"

    # Compile RTL
    vlog -sv \
        -work "${SIM_DIR}/work" \
        "${RTL_DIR}/${module}.sv"

    # Compile testbench
    vlog -sv \
        -work "${SIM_DIR}/work" \
        "${TB_DIR}/${tb}.sv"

    echo "Compilation complete: ${module}"
}

run_questa() {
    local module=$1
    local tb="${module}_tb"

    echo "=========================================="
    echo "Running ${module} simulation..."
    echo "=========================================="

    vsim -c \
        -lib "${SIM_DIR}/work" \
        "${tb}" \
        -do "run -all; quit -f"

    echo "Simulation complete: ${module}"
}

run_module() {
    local module=$1

    echo ""
    echo "════════════════════════════════════════════"
    echo "  Testing Module: ${module}"
    echo "════════════════════════════════════════════"
    echo ""

    case "${TOOL}" in
        xsim)
            compile_xsim "${module}"
            run_xsim "${module}"
            ;;
        questa)
            compile_questa "${module}"
            run_questa "${module}"
            ;;
        *)
            echo "Error: Unknown tool '${TOOL}'"
            exit 1
            ;;
    esac
}

#==========================================================================
# Main
#==========================================================================

# Check arguments
if [ $# -lt 1 ]; then
    usage
fi

TARGET_MODULE=$1

# Validate module
if [ "${TARGET_MODULE}" != "all" ]; then
    if [[ ! " ${MODULES[@]} " =~ " ${TARGET_MODULE} " ]]; then
        echo "Error: Unknown module '${TARGET_MODULE}'"
        usage
    fi
fi

# Run tests
if [ "${TARGET_MODULE}" == "all" ]; then
    echo "Running all module tests..."
    for module in "${MODULES[@]}"; do
        run_module "${module}"
    done
else
    run_module "${TARGET_MODULE}"
fi

echo ""
echo "=========================================="
echo "  All Tests Complete"
echo "=========================================="
echo ""

# Show summary
if [ -d "${LOG_DIR}" ]; then
    echo "Log files:"
    ls -la "${LOG_DIR}"
fi

exit 0
