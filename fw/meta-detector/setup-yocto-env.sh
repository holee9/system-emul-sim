#!/bin/bash
# Yocto Build Environment Setup for meta-detector
# Purpose: Configure Yocto Scarthgap 5.0 LTS build environment
#          with Variscite BSP and meta-detector layer
# Usage: source setup-yocto-env.sh

set -e

# ============================================================================
# Configuration
# ============================================================================

# Yocto release
YOCTO_RELEASE="scarthgap"
YOCTO_VERSION="5.0.0"

# Variscite BSP configuration
VARISCITE_BSP_BRANCH="master"
VARISCITE_BSP_REPO="https://github.com/varigit/meta-variscite-bsp.git"

# Build directory
BUILD_DIR="build-yocto"
INSTALL_DIR="install-yocto"

# Machine (Variscite VAR-SOM-MX8M-PLUS)
MACHINE="imx8mp-var-dart"

# Number of parallel threads
BB_NUMBER_THREADS=${BB_NUMBER_THREADS:-$(nproc)}
PARALLEL_MAKE="${PARALLEL_MAKE:--j$(nproc)}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# ============================================================================
# Functions
# ============================================================================

log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

check_requirements() {
    log_info "Checking build requirements..."

    # Check required packages
    local required_packages="git tar bzip2 wget unzip xz-utils syslinux"
    for pkg in $required_packages; do
        if ! command -v $pkg &> /dev/null; then
            log_error "Missing required package: $pkg"
            log_info "Install with: sudo apt-get install $pkg"
            exit 1
        fi
    done

    log_info "Build requirements satisfied"
}

setup_poky() {
    log_info "Setting up Poky Yocto ${YOCTO_RELEASE}..."

    if [ ! -d "poky" ]; then
        log_info "Cloning Poky repository..."
        git clone -b ${YOCTO_RELEASE} git://git.yoctoproject.org/poky.git
    else
        log_info "Poky directory already exists"
    fi

    # Initialize build directory
    if [ ! -d "${BUILD_DIR}" ]; then
        log_info "Initializing build directory..."
        source poky/oe-init-build-env ${BUILD_DIR}
    else
        log_info "Build directory already exists"
        source poky/oe-init-build-env ${BUILD_DIR}
    fi
}

setup_bblayers() {
    log_info "Configuring BitBake layers..."

    # Create bblayers.conf if not exists
    cat > conf/bblayers.conf << EOF
# BitBake layers for detector-image
POKY_BBLAYERS_CONF_VERSION = "2"

BBLAYERS ?= " \\
    ${OE_ROOT}/layers/meta \\
    ${OE_ROOT}/layers/meta-poky \\
    ${OE_ROOT}/layers/meta-yocto-bsp \\
    ${OE_ROOT}/layers/meta-variscite-bsp \\
    ${OE_ROOT}/layers/meta-variscite-bsp/imxmeta-yocto-bsp \\
    ${OE_ROOT}/layers/meta-oxide/meta-detector \\
    "
EOF
}

setup_local_conf() {
    log_info "Configuring local.conf..."

    # Backup existing config
    if [ -f conf/local.conf ] && [ ! -f conf/local.conf.bak ]; then
        cp conf/local.conf conf/local.conf.bak
    fi

    # Add detector-specific configuration
    cat >> conf/local.conf << EOF

# ============================================================================
# X-ray Detector Panel Build Configuration
# ============================================================================

# Machine configuration (VAR-SOM-MX8M-PLUS)
MACHINE = "${MACHINE}"

# Parallel build configuration
BB_NUMBER_THREADS = "${BB_NUMBER_THREADS}"
PARALLEL_MAKE = "${PARALLEL_MAKE}"

# Package type (IPK for embedded)
PACKAGE_CLASSES = "package_ipk"

# SDK configuration
SDKMACHINE = "${BUILD_SDKMACHINE}"
DISTRO_FEATURES:append = " systemd"
VIRTUAL-RUNTIME_init_manager = "systemd"
DISTRO_FEATURES_BACKFILL_CONSIDERED = "sysvinit"

# Image features
IMAGE_FEATURES += "debug-tweaks ssh-server-openssh"

# Extra image space
IMAGE_ROOTFS_EXTRA_SPACE = "1048576"

# License handling
LICENSE_ACCEPTED = "Proprietary"

# Enable IPK packaging
EXTRA_IMAGE_FEATURES = "debug-tweaks tools-sdk"

# ============================================================================
# detector-daemon Configuration
# ============================================================================

# Enable detector packagegroup
IMAGE_INSTALL_append = " packagegroup-detector"

# Systemd service
SYSTEMD_AUTO_ENABLE = "enable"
EOF
}

add_detector_layer() {
    log_info "Adding meta-detector layer..."

    # Find bblayers script
    if [ -f scripts/bitbake-layers ]; then
        BITBAKE_LAYERS="scripts/bitbake-layers"
    elif command -v bitbake-layers &> /dev/null; then
        BITBAKE_LAYERS="bitbake-layers"
    else
        log_warn "bitbake-layers not found, skipping layer detection"
        return
    fi

    # Add meta-detector layer
    if ! $BITBAKE_LAYERS show-layers | grep -q "meta-detector"; then
        log_info "Adding meta-detector layer to build..."
        $BITBAKE_LAYERS add-layer ../../meta-detector
    else
        log_info "meta-detector layer already added"
    fi
}

build_detector() {
    log_info "Starting detector-image build..."
    log_info "This may take 1-2 hours on first build..."

    # Initialize environment if not already done
    if [ -z "${BUILDDIR}" ]; then
        source poky/oe-init-build-env ${BUILD_DIR}
    fi

    # Build detector image
    bitbake detector-image
}

# ============================================================================
# Main
# ============================================================================

main() {
    log_info "Yocto Build Environment Setup for meta-detector"
    log_info "================================================"

    check_requirements
    setup_poky
    setup_bblayers
    setup_local_conf
    add_detector_layer

    log_info "Build environment configured successfully!"
    log_info ""
    log_info "Next steps:"
    log_info "1. Review conf/local.conf for custom settings"
    log_info "2. Build detector-image: bitbake detector-image"
    log_info "3. Or build only daemon: bitbake detector-daemon"
    log_info ""
    log_info "Build output will be in: ${BUILD_DIR}/tmp/deploy/images/${MACHINE}/"
}

# Run main function
main "$@"
