# manager-quality Agent Memory

## Project: X-ray Detector Panel System

### Key Technical Facts (verified 2026-02-17)

- Linux kernel: 6.6.52 (Variscite BSP imx-6.6.52-2.2.0-v1.3)
- Yocto: Scarthgap 5.0 LTS (NOT Mickledore)
- SoC: Variscite VAR-SOM-MX8M-PLUS (NXP i.MX8M Plus)
- FPGA: Xilinx Artix-7 XC7A35T-FGG484
- FPGA LUT budget: <60% = max 12,480 LUTs
- D-PHY: 400 Mbps/lane stable (verified HW), 800 Mbps/lane debugging in progress
- SPI: max 50 MHz, Mode 0 (CPOL=0, CPHA=0)

### Performance Tiers (corrected)

| Tier | Resolution | Bit Depth | FPS | Data Rate | Status |
|------|-----------|-----------|-----|-----------|--------|
| Minimum | 1024x1024 | 14-bit | 15 | ~0.21 Gbps | 400M stable |
| Intermediate-A | 2048x2048 | 16-bit | 15 | ~1.01 Gbps | 400M stable |
| Intermediate-B | 2048x2048 | 16-bit | 30 | ~2.01 Gbps | 800M debugging |
| Target (Final) | 3072x3072 | 16-bit | 15 | ~2.26 Gbps | 800M needed |

Note: 3072x3072@30fps (~4.53 Gbps) PERMANENTLY EXCLUDED - exceeds HW capability.

### Coverage Requirements

- RTL: Line >= 95%, Branch >= 90%, FSM 100% (all states and transitions)
- SW (firmware, SDK, simulators): 85%+ per module

### PoC Timing

- M0.5 PoC: W26 (after W22 implementation complete, document-first approach)
- PoC phase: W23-W26 (NOT W3-W6, which was the original pre-document-first schedule)

### Common SPEC Errors Found (2026-02-17 review session)

1. SPEC-POC-001: gate_week was W6, should be W26
2. SPEC-POC-001: Performance tier table had wrong FPS (30fps for mid tier, wrong tier names)
3. SPEC-POC-001: Maximum tier (3072x3072@30fps) should be removed (HW limit)
4. SPEC-POC-001: Linux 5.15+ reference in R-POC-003 mitigation section
5. SPEC-FW-001: REQ-FW-001 said "Linux 5.15+" instead of "Linux 6.6.52"

### TRUST 5 Scores from 2026-02-17 Review

| SPEC | T | R | U | S | T(race) | Status |
|------|---|---|---|---|---------|--------|
| SPEC-POC-001 | 4 | 4 | 4 | 4 | 4 | Approved (with fixes) |
| SPEC-FPGA-001 | 5 | 5 | 5 | 5 | 5 | Approved |
| SPEC-FW-001 | 4 | 5 | 5 | 4 | 5 | Approved (with fix) |
| SPEC-SDK-001 | 5 | 5 | 5 | 4 | 5 | Approved |
| SPEC-SIM-001 | 5 | 5 | 5 | 4 | 5 | Approved |
| SPEC-TOOLS-001 | 4 | 5 | 5 | 4 | 4 | Approved |

Last Updated: 2026-02-17
