SUMMARY = "X-ray Detector Panel SoC Controller Firmware Daemon"
DESCRIPTION = "User-space daemon for X-ray Detector Panel SoC controller. \
Manages SPI communication with FPGA, CSI-2 frame reception, Ethernet transmission, \
command protocol, and health monitoring."
LICENSE = "Proprietary"
LIC_FILES_CHKSUM = "file://LICENSE;md5=<hash>"

# Version
PV = "1.0.0"
PR = "r0"

# Source (local development)
SRC_URI = "file://detector-daemon \
           file://detector.service \
           file://detector_config.yaml"

S = "${WORKDIR}"

# Runtime dependencies (REQ-FW-081)
RDEPENDS_${PN} = " \
    v4l-utils \
    spidev \
    iproute2 \
    ethtool \
    libyaml \
    bash \
    coreutils \
"

# Build dependencies
DEPENDS = "libyaml"

# Systemd service (REQ-FW-120)
SYSTEMD_SERVICE_${PN} = "detector.service"
SYSTEMD_AUTO_ENABLE = "enable"

inherit systemd

# Compilation
do_compile() {
    # Build is done externally via CMake
    # This recipe only packages the pre-built binary
    bbnote "Packaging pre-built detector_daemon binary"
}

# Installation
do_install() {
    # Install binary
    install -d ${D}${bindir}
    install -m 0755 ${S}/detector-daemon ${D}${bindir}/detector_daemon

    # Install systemd service file
    install -d ${D}${systemd_system_unitdir}
    install -m 0644 ${S}/detector.service ${D}${systemd_system_unitdir}/detector.service

    # Install configuration file
    install -d ${D}${sysconfdir}/detector
    install -m 0644 ${S}/detector_config.yaml ${D}${sysconfdir}/detector/detector_config.yaml

    # Install PID file directory
    install -d ${D}${localstatedir}/run

    # Install log directory
    install -d ${D}${localstatedir}/log/detector

    # Create detector user and group (REQ-FW-102)
    # This is done in pkg_postinst to avoid build-time user creation
}

# Post-installation script
pkg_postinst_${PN}() {
    # Get current username
    CURRENT_USER=$(whoami)

    if [ "$CURRENT_USER" = "root" ]; then
        # Create detector user and group if not exists
        if ! getent group detector > /dev/null 2>&1; then
            groupadd -r detector
            bbnote "Created detector group"
        fi

        if ! getent passwd detector > /dev/null 2>&1; then
            useradd -r -g detector -s /bin/bash -d /var/lib/detector \
                -c "X-ray Detector Daemon" detector
            bbnote "Created detector user"
        fi

        # Set up device permissions
        if [ -e /dev/spidev0.0 ]; then
            chown detector:detector /dev/spidev0.0
            chmod 660 /dev/spidev0.0
        fi

        if [ -e /dev/video0 ]; then
            chown detector:detector /dev/video0
            chmod 660 /dev/video0
        fi

        if [ -e /dev/i2c-1 ]; then
            chown detector:detector /dev/i2c-1
            chmod 660 /dev/i2c-1
        fi

        # Reload systemd to recognize new service
        if [ -d ${sysconfdir}/systemd/system ]; then
            systemctl daemon-reload
        fi
    else
        bbwarn "Not running as root, skipping user creation and device setup"
    fi
}

# Pre-remove script
pkg_prerm_${PN}() {
    # Stop service before removing
    if [ -d ${sysconfdir}/systemd/system ]; then
        if systemctl is-active detector_daemon >/dev/null 2>&1; then
            systemctl stop detector_daemon
            bbnote "Stopped detector_daemon service"
        fi
    fi
}

# Post-remove script
pkg_postrm_${PN}() {
    # Optionally remove user and group
    # Commented out by default to preserve data
    # if getent passwd detector >/dev/null 2>&1; then
    #     userdel detector
    # fi
    # if getent group detector >/dev/null 2>&1; then
    #     groupdel detector
    # fi
}

# Package information
FILES_${PN} = " \
    ${bindir}/detector_daemon \
    ${systemd_system_unitdir}/detector.service \
    ${sysconfdir}/detector/detector_config.yaml \
"

# Allow empty package for debug
ALLOW_EMPTY_${PN} = "1"

# Package metadata
PKG_${PN} = "detector-daemon"
