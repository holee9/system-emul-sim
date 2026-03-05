# BitBake recipe for detector-daemon
# Recipe: detector-daemon_1.0.0.bb
# Target: NXP i.MX8M Plus (aarch64)
# Yocto: Scarthgap 5.0 LTS
# SPEC: SPEC-FW-001 (REQ-FW-080, REQ-FW-081)

SUMMARY = "X-ray Detector Panel SoC Controller Firmware Daemon"
DESCRIPTION = "User-space daemon for X-ray Detector Panel SoC controller. \
Manages SPI communication with FPGA, CSI-2 frame reception, Ethernet transmission, \
command protocol, and health monitoring. Implements HMAC-SHA256 authentication, \
battery monitoring (BQ40z50), and systemd integration."

LICENSE = "Proprietary"
LIC_FILES_CHKSUM = "file://LICENSE;md5=TODO"

# Version information
PV = "1.0.0"
PR = "r0"

# Source directories (local development in fw/)
SRC_URI = "file://src \
           file://include \
           file://deploy/detector.service \
           file://deploy/detector_config.yaml"

S = "${WORKDIR}"

# Toolchain requirements (REQ-FW-002)
# Requires CMake cross-compilation toolchain
inherit cmake systemd

# Extra OECMake generation flags
EXTRA_OECMAKE = "-DCMAKE_BUILD_TYPE=Release \
                 -DBUILD_TESTS=OFF \
                 -DCROSS_COMPILE=ON"

# Runtime dependencies (REQ-FW-081)
# v4l-utils: V4L2 device management
# spidev: SPI user-space interface
# iproute2: Network configuration
# ethtool: 10GbE network diagnostics
# libyaml: YAML configuration parsing
# openssl: HMAC-SHA256 authentication
RDEPENDS_${PN} = " \
    v4l-utils \
    bash \
    coreutils \
    iproute2 \
    ethtool \
    libyaml \
    openssl-bin \
"

# Build dependencies (REQ-FW-002)
DEPENDS = "libyaml openssl"

# Systemd service configuration (REQ-FW-120)
SYSTEMD_SERVICE_${PN} = "detector.service"
SYSTEMD_AUTO_ENABLE = "enable"
SYSTEMD_PACKAGES = "${PN}"

# Package configuration
FILES_${PN} = " \
    ${bindir}/detector_daemon \
    ${sysconfdir}/detector/detector_config.yaml \
    ${sysconfdir}/default/detector \
    ${systemd_system_unitdir}/detector.service \
"

# Allow empty debug package
ALLOW_EMPTY_${PN}-dbg = "1"

# Security hardening (REQ-FW-102)
# Enable security flags for the binary
SECURITY_CFLAGS = "-fstack-protector-all -Wformat -Wformat-security"

# ============================================================================
# do_compile: Build detector-daemon with cross-compiler
# ============================================================================
do_compile() {
    bbnote "Compiling detector-daemon for aarch64 target"
    # CMake inherit handles the cross-compilation build
    # Output binary: ${B}/detector_daemon
}

# ============================================================================
# do_install: Install binary, configs, and systemd service
# ============================================================================
do_install() {
    # Install binary
    install -d ${D}${bindir}
    install -m 0755 ${B}/detector_daemon ${D}${bindir}/detector_daemon

    # Install configuration directory
    install -d ${D}${sysconfdir}/detector
    install -m 0644 ${S}/deploy/detector_config.yaml ${D}${sysconfdir}/detector/detector_config.yaml

    # Install environment defaults
    install -d ${D}${sysconfdir}/default
    cat > ${D}${sysconfdir}/default/detector << 'EOF'
# Detector Daemon Defaults
# Configuration file path
DETECTOR_CONFIG="/etc/detector/detector_config.yaml"

# Log level (DEBUG, INFO, WARNING, ERROR, CRITICAL)
DETECTOR_LOG_LEVEL="INFO"

# User to run as (REQ-FW-102)
DETECTOR_USER="detector"
DETECTOR_GROUP="detector"
EOF

    # Install systemd service file
    install -d ${D}${systemd_system_unitdir}
    install -m 0644 ${S}/deploy/detector.service ${D}${systemd_system_unitdir}/detector.service

    # Create runtime directories
    install -d ${D}${localstatedir}/lib/detector
    install -d ${D}${localstatedir}/log/detector
    install -d ${D}${localstatedir}/run/detector

    # Create detector user home directory
    install -d -o detector -g detector ${D}${localstatedir}/lib/detector
}

# ============================================================================
# pkg_postinst: Create detector user and configure permissions
# ============================================================================
pkg_postinst_${PN}() {
    if [ -n "$D" ]; then
        # Running in image creation context, defer to first boot
        exit 1
    fi

    # Get current username
    CURRENT_USER=$(whoami)

    if [ "$CURRENT_USER" = "root" ]; then
        # Create detector group if not exists
        if ! getent group detector > /dev/null 2>&1; then
            groupadd -r detector
            bbnote "Created detector group"
        fi

        # Create detector user if not exists (REQ-FW-102)
        if ! getent passwd detector > /dev/null 2>&1; then
            useradd -r -g detector -s /sbin/nologin -d /var/lib/detector \
                -c "X-ray Detector Daemon" detector
            bbnote "Created detector user"
        fi

        # Set up device permissions via udev rules
        # SPI device
        if [ -e /dev/spidev0.0 ]; then
            chown root:detector /dev/spidev0.0
            chmod 660 /dev/spidev0.0
        fi

        # V4L2 device
        if [ -e /dev/video0 ]; then
            chown root:detector /dev/video0
            chmod 660 /dev/video0
        fi

        # I2C devices for BQ40z50 battery (SMBus on I2C-1)
        if [ -e /dev/i2c-1 ]; then
            chown root:detector /dev/i2c-1
            chmod 660 /dev/i2c-1
        fi

        # I2C-7 for BMI160 IMU (Phase 3, W23-W28)
        if [ -e /dev/i2c-7 ]; then
            chown root:detector /dev/i2c-7
            chmod 660 /dev/i2c-7
        fi

        # Reload systemd and enable service
        if [ -d /run/systemd/system ]; then
            systemctl daemon-reload
            systemctl enable detector.service
            bbnote "Enabled detector.service"
        fi
    else
        bbwarn "Not running as root, skipping user creation and device setup"
    fi
}

# ============================================================================
# pkg_prerm: Stop service before removal
# ============================================================================
pkg_prerm_${PN}() {
    if [ -n "$D" ]; then
        exit 1
    fi

    # Stop service before removing
    if [ -d /run/systemd/system ]; then
        if systemctl is-active detector.service >/dev/null 2>&1; then
            systemctl stop detector.service
            bbnote "Stopped detector.service"
        fi
        systemctl disable detector.service 2>/dev/null || true
    fi
}

# ============================================================================
# Initialize sysroot
# ============================================================================
sysroot_stage_all() {
    :
}
