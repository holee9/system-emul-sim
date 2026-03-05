# BitBake recipe for packagegroup-detector
# Recipe: packagegroup-detector.bb
# Target: NXP i.MX8M Plus (aarch64)
# Yocto: Scarthgap 5.0 LTS
# SPEC: SPEC-FW-001 (REQ-FW-081)

SUMMARY = "X-ray Detector Panel Runtime Dependencies Package Group"
DESCRIPTION = "Package group containing all runtime dependencies required \
for detector-daemon operation on i.MX8M Plus SoC."

inherit packagegroup

# ============================================================================
# Runtime Dependencies (REQ-FW-081)
# ============================================================================

RDEPENDS_${PN} = " \
    v4l-utils \
    spidev-test \
    iproute2 \
    ethtool \
    libyaml \
    openssl \
    openssl-bin \
    "

# Optional: Development tools for debugging
RRECOMMENDS_${PN} = " \
    gdb \
    strace \
    tcpdump \
    "
