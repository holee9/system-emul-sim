# Contributing to X-ray Detector Panel System

Thank you for your interest in contributing to the X-ray Detector Panel System project. This document outlines the development workflow, coding conventions, and contribution process.

---

## Table of Contents

1. [Development Workflow](#1-development-workflow)
2. [Coding Conventions](#2-coding-conventions)
3. [Git Branch Strategy](#3-git-branch-strategy)
4. [Commit Message Rules](#4-commit-message-rules)
5. [Pull Request Process](#5-pull-request-process)
6. [Code Review Process](#6-code-review-process)
7. [Development Methodology](#7-development-methodology)
8. [Quality Standards](#8-quality-standards)

---

## 1. Development Workflow

### Getting Started

1. Clone the repository:
   ```bash
   git clone <gitea-url>/system-emul-sim.git
   cd system-emul-sim
   ```

2. Set up your development environment following `docs/guides/development-setup.md`

3. Create a feature branch from `main`:
   ```bash
   git checkout -b feat/your-feature-name
   ```

4. Develop using the appropriate methodology (TDD or DDD -- see Section 7)

5. Submit a pull request when your work is ready for review

### Prerequisites

- .NET 8.0 SDK (for simulators, SDK, tools)
- AMD Vivado 2023.2+ HL Design Edition (for FPGA development)
- Yocto SDK or Linaro toolchain (for SoC firmware)
- Git 2.30+

---

## 2. Coding Conventions

### 2.1 SystemVerilog (FPGA RTL)

| Rule | Convention | Example |
|------|-----------|---------|
| File naming | `snake_case.sv` | `panel_scan_fsm.sv` |
| Module naming | `snake_case` | `module line_buffer` |
| Signal naming | `snake_case` | `frame_counter` |
| Parameter naming | `UPPER_CASE` | `parameter ROWS_MAX = 3072` |
| Clock signals | `clk_<domain>` | `clk_sys`, `clk_csi2_byte` |
| Reset signals | `rst_n` (active-low) | `input logic rst_n` |
| State encoding | `typedef enum logic` | `ST_IDLE = 3'b000` |
| Sequential logic | `always_ff` | `always_ff @(posedge clk)` |
| Combinational logic | `always_comb` | `always_comb begin ... end` |

**Rules**:
- No combinational feedback loops
- No latches (use `default` in all `case` statements)
- Explicit clock and reset in all sequential blocks
- 2-stage FF synchronizer for all clock domain crossings

### 2.2 C (SoC Firmware)

| Rule | Convention | Example |
|------|-----------|---------|
| File naming | `snake_case.c/.h` | `spi_master.c` |
| Function naming | `snake_case` | `fpga_reg_write()` |
| Type naming | `snake_case_t` | `frame_header_t` |
| Macro naming | `UPPER_CASE` | `#define REG_CONTROL 0x00` |
| Struct naming | `snake_case` | `struct frame_buffer` |
| Header guards | `UPPER_CASE_H` | `#ifndef SPI_MASTER_H` |

**Rules**:
- C11 standard
- All functions must have a prototype in the header file
- No global mutable state (use struct-based context)
- Error codes returned as `int` (0 = success, negative = error)

### 2.3 C# (Simulators, SDK, Tools)

| Rule | Convention | Example |
|------|-----------|---------|
| File naming | `PascalCase.cs` | `FrameReassembler.cs` |
| Class naming | `PascalCase` | `class DetectorClient` |
| Interface naming | `IPascalCase` | `interface ISimulator` |
| Method naming | `PascalCase` | `ProcessFrame()` |
| Property naming | `PascalCase` | `public int Width { get; }` |
| Private field naming | `_camelCase` | `private int _frameCount` |
| Parameter naming | `camelCase` | `void Process(int frameIndex)` |
| Constant naming | `PascalCase` | `const int MaxRetries = 3` |

**Rules**:
- .NET 8.0 target framework
- Nullable reference types enabled
- `async/await` for all I/O operations
- `IDisposable` pattern for resource management
- XML documentation comments on all public members

### 2.4 Documentation (Markdown)

| Rule | Convention | Example |
|------|-----------|---------|
| File naming | `kebab-case.md` | `fpga-build-guide.md` |
| Headers | ATX-style (`#`) | `## Section Title` |
| Code blocks | Fenced with language | ` ```csharp ` |
| Tables | Pipe-delimited | `| Column | Value |` |

### 2.5 Code Comments

- Code comments are written in **English** (per `language.yaml`)
- Comments explain *why*, not *what*
- Avoid obvious comments (`// increment counter` on `counter++`)
- Use `TODO:` for known issues with tracking references

---

## 3. Git Branch Strategy

### Branch Types

| Branch | Pattern | Purpose | Lifetime |
|--------|---------|---------|----------|
| `main` | `main` | Stable, production-ready | Permanent |
| Feature | `feat/<name>` | New features | Until merged |
| Fix | `fix/<name>` | Bug fixes | Until merged |
| Docs | `docs/<name>` | Documentation updates | Until merged |
| Refactor | `refactor/<name>` | Code improvements | Until merged |
| Test | `test/<name>` | Test additions | Until merged |

### Branch Rules

- All development happens on feature branches
- `main` is protected: no direct commits
- Branch names use `kebab-case`: `feat/csi2-tx-wrapper`
- Delete branches after merge
- Keep branches short-lived (< 1 week preferred)

### Example Workflow

```bash
# Create feature branch
git checkout main
git pull origin main
git checkout -b feat/spi-slave-register-map

# Develop and commit
git add src/spi_slave.sv
git commit -m "feat(fpga): Add SPI slave register map implementation"

# Push and create PR
git push -u origin feat/spi-slave-register-map
```

---

## 4. Commit Message Rules

### Format

```
<type>(<scope>): <subject>

<body>

🗿 ABYZ-Lab <email@mo.ai.kr>
```

### Types

| Type | Usage |
|------|-------|
| `feat` | New feature |
| `fix` | Bug fix |
| `docs` | Documentation changes |
| `refactor` | Code refactoring (no behavior change) |
| `test` | Adding or modifying tests |
| `chore` | Build, CI, tooling changes |

### Scopes

| Scope | Component |
|-------|-----------|
| `fpga` | FPGA RTL and testbenches |
| `fw` | SoC firmware |
| `sdk` | Host SDK |
| `tools` | Simulators, GUI, CodeGenerator, etc. |
| `config` | detector_config.yaml, schemas |
| `docs` | Documentation |

### Rules

- Subject line: max 72 characters, imperative mood ("Add" not "Added")
- Body: explain *why* the change was made (in English)
- One logical change per commit
- Reference issue or SPEC when applicable

### Examples

```
feat(fpga): Add panel scan FSM with six-state encoding

Implements REQ-FPGA-010 through REQ-FPGA-016.
FSM states: IDLE, INTEGRATE, READOUT, LINE_DONE, FRAME_DONE, ERROR.
Three operating modes supported: single, continuous, calibration.

🗿 ABYZ-Lab <email@mo.ai.kr>
```

```
fix(sdk): Handle out-of-order UDP packets in FrameReassembler

Packets were being dropped when arriving out of sequence.
Now correctly buffers by packet_index and assembles on completion.
Fixes AC-SDK-004.

🗿 ABYZ-Lab <email@mo.ai.kr>
```

---

## 5. Pull Request Process

### Creating a PR

1. Ensure all tests pass locally:
   ```bash
   dotnet test           # .NET tests
   cd fpga && make sim   # FPGA simulation (if applicable)
   cd fw/build && ctest  # Firmware tests (if applicable)
   ```

2. Push your branch and create a PR on Gitea

3. PR title: Follow commit message format (`type(scope): subject`)

4. PR description must include:
   - Summary of changes
   - Related SPEC or issue references
   - Test results (pass/fail, coverage)
   - Breaking changes (if any)

### PR Template

```markdown
## Summary

Brief description of the changes.

## Related

- SPEC: SPEC-FPGA-001
- Requirements: REQ-FPGA-010~016

## Changes

- Added panel_scan_fsm.sv with six-state FSM
- Added tb_panel_scan_fsm.sv with state coverage tests
- Updated constraints/timing.xdc with FSM clock constraints

## Test Results

- Unit tests: 12/12 pass
- Coverage: Line 97%, Branch 93%, FSM 100%
- No regressions

## Checklist

- [ ] Tests pass
- [ ] Coverage meets targets (85%+)
- [ ] Documentation updated
- [ ] No critical warnings
```

### PR Review Criteria

A PR is ready for merge when:
- [ ] All CI checks pass
- [ ] At least 1 reviewer approves
- [ ] All review comments are resolved
- [ ] Coverage targets are met
- [ ] No unresolved conflicts with `main`

---

## 6. Code Review Process

### Reviewer Responsibilities

- Verify correctness: Does the code do what the SPEC requires?
- Check quality: Does it follow coding conventions?
- Assess coverage: Are tests sufficient?
- Evaluate safety: Are there security or safety concerns?
- Review documentation: Are public APIs documented?

### Review Guidelines

| Category | What to Check |
|----------|--------------|
| Correctness | Logic errors, edge cases, off-by-one |
| Style | Naming conventions, formatting |
| Safety | Buffer overflows, null dereference, CDC violations |
| Performance | Unnecessary allocations, hot path efficiency |
| Testability | Can this be unit tested? Are tests meaningful? |
| Documentation | XML comments on public APIs, inline comments for complex logic |

### Review Turnaround

- Respond to review requests within 1 business day
- Small PRs (< 200 lines): review within 4 hours
- Large PRs (> 500 lines): discuss splitting into smaller PRs

### Feedback Format

Use conventional comment prefixes:

- `nit:` - Minor style suggestion (non-blocking)
- `suggestion:` - Improvement idea (non-blocking)
- `question:` - Clarification needed
- `issue:` - Must be fixed before merge (blocking)
- `praise:` - Good work, well done

---

## 7. Development Methodology

The project uses **Hybrid** development methodology (configured in `.abyz-lab/config/sections/quality.yaml`).

### TDD for New Code

Applies to: Simulators, SDK, Tools, new firmware modules

**Cycle: RED-GREEN-REFACTOR**

1. **RED**: Write a failing test that describes the desired behavior
2. **GREEN**: Write the minimum code to make the test pass
3. **REFACTOR**: Clean up while keeping all tests green

```
Write failing test -> Run test (FAIL) -> Write code -> Run test (PASS) -> Refactor -> Run test (PASS)
```

### DDD for Existing Code

Applies to: FPGA RTL modifications, firmware HAL integration

**Cycle: ANALYZE-PRESERVE-IMPROVE**

1. **ANALYZE**: Read existing code, understand dependencies and behavior
2. **PRESERVE**: Write characterization tests that capture current behavior
3. **IMPROVE**: Make incremental changes, verifying characterization tests pass

```
Read code -> Write characterization tests -> Verify tests pass -> Make small change -> Verify tests still pass
```

---

## 8. Quality Standards

### Coverage Targets

| Component | Line | Branch | FSM | Overall |
|-----------|------|--------|-----|---------|
| FPGA RTL | >= 95% | >= 90% | 100% | N/A |
| Software (per module) | N/A | N/A | N/A | 80-90% |
| Overall project | N/A | N/A | N/A | 85%+ |

### TRUST 5 Framework

All contributions must satisfy:

- **Tested**: Coverage targets met, meaningful assertions
- **Readable**: Clear naming, English comments, consistent style
- **Unified**: Consistent formatting (EditorConfig)
- **Secured**: No secrets committed, input validation, OWASP compliance
- **Trackable**: Conventional commits, SPEC/issue references

### Pre-Commit Checklist

Before committing:

- [ ] Code compiles without errors
- [ ] All tests pass
- [ ] Coverage meets targets
- [ ] No lint warnings
- [ ] No sensitive data (secrets, keys, passwords)
- [ ] Commit message follows format
- [ ] SPEC or issue referenced (if applicable)

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | ABYZ-Lab Agent (architect) | Initial contribution guide |

---
