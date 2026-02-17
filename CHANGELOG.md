# Changelog

All notable changes to the X-ray Detector Panel System will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Added
- SPEC documents for all subsystems (FPGA, FW, SDK, SIM, TOOLS)
- Architecture design documents (system, FPGA, SoC firmware, Host SDK)
- Development guides (setup, FPGA build, firmware build, SDK build, simulator, tools, installation, deployment)
- Project documentation (CONTRIBUTING, CHANGELOG, roadmap, glossary)
- MoAI-ADK project configuration and workflow rules

### Changed
- Performance target updated from 3072x3072@30fps to 3072x3072@15fps (CSI-2 bandwidth constraint)

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
- `.moai/config/`: MoAI-ADK project settings (quality, language, workflow)
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
| 1.0.0 | 2026-02-17 | MoAI Agent (architect) | Initial CHANGELOG creation |

---
