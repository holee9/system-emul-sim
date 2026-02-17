# Changelog

All notable changes to the X-ray Detector Panel System will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Added
- SoC Firmware documentation (fw/README.md, fw/ARCHITECTURE.md)
- Comprehensive firmware architecture documentation with module breakdown
- Firmware development methodology guidelines (TDD for new code, DDD for HAL integration)
- Yocto build system instructions and deployment guide
- Security architecture documentation (fw/SECURITY_IMPROVEMENTS.md)

### Changed
- SPEC-FW-001 status updated from "approved" to "implemented"
- Documentation synchronized with firmware implementation progress
- Firmware README.md updated with security architecture section
- Firmware ARCHITECTURE.md enhanced with defense-in-depth security layers

### Security Improvements
- HMAC-SHA256 message authentication for Host commands
- Privilege drop mechanism (root → detector user)
- Systemd hardening with minimal capabilities
- Replay protection via monotonic sequence numbers
- Secure key storage (root:detector 0400 permissions)

### Added (Previous)
- 5 simulators implementation complete (Common.Dto, PanelSimulator, FpgaSimulator, McuSimulator, HostSimulator)
- 261 tests passing across all simulators
- 85%+ code coverage achieved for all simulator modules
- Comprehensive integration test framework with IT-01~IT-10 scenarios
- tools/Common/, tools/PanelSimulator/, tools/FpgaSimulator/, tools/McuSimulator/, tools/HostSimulator/ directories

### Changed (Previous)
- Performance target updated from 3072x3072@30fps to 3072x3072@15fps (CSI-2 bandwidth constraint)
- M2-Impl milestone: All simulators with unit tests passing

---

## [0.2.0-alpha] - 2026-02-17

### Added
- Common.Dto implementation with ISimulator interface and DTOs (FrameData, LineData, Csi2Packet, UdpPacket, SpiTransaction)
- 97.08% code coverage with 53 passing tests for Common.Dto
- Comprehensive XML documentation comments for all public APIs
- tools/Common/README.md with usage examples and API documentation
- 5 simulators implementation complete (Common.Dto, PanelSimulator, FpgaSimulator, McuSimulator, HostSimulator)
- 261 tests passing across all simulators
- 85%+ code coverage achieved for all simulator modules
- Comprehensive integration test framework with IT-01~IT-10 scenarios

### Changed
- Performance target updated from 3072x3072@30fps to 3072x3072@15fps (CSI-2 bandwidth constraint)
- M2-Impl milestone status: Complete

---

## [0.1.0-alpha] - 2026-02-17

### Added

#### Architecture (M0)
- System architecture design document (`docs/architecture/system-architecture.md`)
- FPGA RTL design document (`docs/architecture/fpga-design.md`)
- SoC firmware design document (`docs/architecture/soc-firmware-design.md`)
- Host SDK design document (`docs/architecture/host-sdk-design.md`)

#### SPEC Documents
- SPEC-ARCH-001: P0 Architecture Decisions and Technology Stack
- SPEC-POC-001: CSI-2 Proof of Concept validation plan
- SPEC-FPGA-001: FPGA RTL Requirements Specification
- SPEC-FW-001: SoC Firmware Requirements Specification
- SPEC-SDK-001: Host SDK Requirements Specification
- SPEC-SIM-001: Simulator Suite Requirements Specification
- SPEC-TOOLS-001: Development Tools Requirements Specification

#### Project Configuration
- `detector_config.yaml`: Single source of truth for system configuration
- `.moai/config/`: ABYZ-Lab-ADK project settings (quality, language, workflow)
- Quality configuration: Hybrid methodology (TDD + DDD)

#### Documentation
- README.md: Project overview with system architecture diagram
- CONTRIBUTING.md: Development workflow and coding conventions
- CHANGELOG.md: Version history tracking
- 8 development guides in `docs/guides/`

### Decisions Locked
- FPGA device: Xilinx Artix-7 XC7A35T-FGG484
- High-speed interface: CSI-2 MIPI 4-lane D-PHY (USB 3.x permanently excluded)
- SoC platform: NXP i.MX8M Plus (recommended)
- Host link: 10 GbE (required for Target tier)
- Development methodology: Hybrid (TDD for new code, DDD for existing)

---

## Version Plan

Future versions align with project milestones (W1-W28):

| Version | Milestone | Week | Key Deliverables |
|---------|-----------|------|-----------------|
| 0.1.0-alpha | M0 | W1 | Architecture, SPEC documents, project setup |
| 0.2.0-alpha | M0.5 | W6 | CSI-2 PoC validation (>= 70% throughput) |
| 0.3.0-alpha | M1 | W3 | Architecture review, config schema finalized |
| 0.4.0-beta | M2 | W9 | All simulators with unit tests passing |
| 0.5.0-beta | M3 | W14 | IT-01~IT-06 integration tests passing |
| 0.6.0-beta | M4 | W18 | HIL Pattern A/B validation |
| 0.7.0-rc | M5 | W23 | Code generator v1, generated RTL passes tests |
| 1.0.0 | M6 | W28 | Real panel frame acquisition, simulator calibration |

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | ABYZ-Lab Agent (architect) | Initial CHANGELOG creation |

---
