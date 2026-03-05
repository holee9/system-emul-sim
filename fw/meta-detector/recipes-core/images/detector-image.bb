# BitBake recipe for detector-image
# Recipe: detector-image.bb
# Target: NXP i.MX8M Plus (aarch64)
# Yocto: Scarthgap 5.0 LTS
# SPEC: SPEC-FW-001 (REQ-FW-080, REQ-FW-081)

SUMMARY = "X-ray Detector Panel SoC Firmware Image"
DESCRIPTION = "Complete Yocto image for X-ray Detector Panel SoC controller. \
Based on Variscite i.MX8M Plus BSP (Scarthgap 5.0 LTS). Includes detector-daemon, \
runtime dependencies, kernel modules, and system utilities."

# Image base: core-image-minimal for minimal Linux system
require recipes-core/images/core-image-minimal.bb

# Image features
IMAGE_FEATURES += " \
    debug-tweaks \
    ssh-server-openssh \
    tools-sdk \
    tools-debug \
    "

# Additional package management
IMAGE_INSTALL += " \
    packagegroup-core-build \
    kernel-modules \
    "

# ============================================================================
# Detector-Specific Packages (REQ-FW-081)
# ============================================================================

# Runtime dependencies for detector-daemon
IMAGE_INSTALL += " \
    detector-daemon \
    v4l-utils \
    spidev-test \
    iproute2 \
    iputils-ping \
    ethtool \
    libyaml \
    openssl \
    openssl-bin \
    bash \
    coreutils \
    util-linux \
    tzdata \
    "

# ============================================================================
# Development and Debugging Tools
# ============================================================================

IMAGE_INSTALL += " \
    gdb \
    strace \
    ltrace \
    vim \
    less \
    grep \
    gawk \
    sed \
    ncurses-tools \
    procps \
    htop \
    i2c-tools \
    spi-tools \
    can-utils \
    usbutils \
    pciutils \
    "

# ============================================================================
# Network Tools (for 10 GbE testing and diagnostics)
# ============================================================================

IMAGE_INSTALL += " \
    tcpdump \
    netperf \
    wget \
    curl \
    "

# ============================================================================
# Image Configuration
# ============================================================================

# Set root password (debug-tweaks enables empty root password)
# For production, override with:
# INHERIT += "extrausers"
# EXTRA_USERS_PARAMS = "usermod -p ' encrypted_password ' root;"

# Set timezone
IMAGE_ROOTFS_EXTRA_SPACE = "1048576"

# Post-install script for additional configuration
ROOTFS_POSTINSTALL_COMMAND = " \
    mkdir -p ${IMAGE_ROOTFS}${localstatedir}/log/detector; \
    mkdir -p ${IMAGE_ROOTFS}${sysconfdir}/detector; \
    "

# ============================================================================
# License Manifest
# ============================================================================

# Generate license manifest
COPY_LIC_MANIFEST = "1"
COPY_LIC_DIRS = "1"
COPY_LIC_FILES = "1"

# ============================================================================
# Image Size Control
# ============================================================================

# Root filesystem size (adjust based on actual needs)
IMAGE_ROOTFS_SIZE = "256000"

# ============================================================================
# Additional QA Checks
# ============================================================================

# Remove unnecessary files to reduce image size
ROOTFS_RM = " \
    ${IMAGE_ROOTFS}/usr/include/*.h \
    ${IMAGE_ROOTFS}/usr/lib/*.a \
    ${IMAGE_ROOTFS}/usr/lib/*.la \
    "

# Remove static libraries and headers from target image (but not from SDK)
IMAGE_PREPROCESS_COMMAND += "rootfs_remove_files; "

rootfs_remove_files() {
    # Remove static libraries
    find ${IMAGE_ROOTFS}${libdir} -name "*.a" -delete
    find ${IMAGE_ROOTFS}${libdir} -name "*.la" -delete

    # Remove include headers
    rm -rf ${IMAGE_ROOTFS}${includedir}

    # Remove debug symbols (if generating separate debug image)
    # rm -rf ${IMAGE_ROOTFS}${bindir}/*.${SOLIBSDEV}
}
