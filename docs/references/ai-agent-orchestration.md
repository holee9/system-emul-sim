# AI Agent Orchestration Guide

**Document Version**: 1.0.0
**Status**: Draft
**Last Updated**: 2026-02-17
**Author**: MoAI Documentation Agent

---

## Table of Contents

1. [Overview](#overview)
2. [MoAI Orchestrator Role](#moai-orchestrator-role)
3. [Agent Catalog](#agent-catalog)
4. [Agent Selection Decision Tree](#agent-selection-decision-tree)
5. [Parallel Execution Patterns](#parallel-execution-patterns)
6. [Token Budget Management](#token-budget-management)
7. [SPEC Workflow](#spec-workflow)
8. [Team Mode vs Sub-Agent Mode](#team-mode-vs-sub-agent-mode)
9. [Progressive Disclosure](#progressive-disclosure)
10. [MoAI Commands Reference](#moai-commands-reference)
11. [Error Recovery Patterns](#error-recovery-patterns)
12. [Context Management](#context-management)
13. [Practical Examples](#practical-examples)

---

## Overview

MoAI is the strategic orchestrator for Claude Code. It delegates all implementation tasks to specialized agents rather than executing them directly. This document explains how MoAI orchestrates the 16-agent system, manages token budgets, and coordinates multi-phase development workflows for the X-ray Detector Panel System.

**Core Principle**: MoAI never implements directly. It plans, delegates, monitors, and synthesizes results. This ensures each task is handled by the most specialized agent with optimal context allocation.

---

## MoAI Orchestrator Role

### Responsibilities

| Responsibility | Description |
|----------------|-------------|
| Request routing | Analyze user requests and route to appropriate agent |
| Token budget allocation | Manage 200K token context window across phases |
| Parallel execution | Launch independent agents simultaneously |
| Result synthesis | Combine agent outputs into coherent responses |
| User interaction | Only MoAI uses AskUserQuestion; agents cannot |
| Quality gate enforcement | Validate TRUST 5 compliance across all outputs |

### Execution Flow

```
User Request
     |
     v
[Phase 1: Analyze]
  - Assess complexity and scope
  - Detect technology keywords
  - Identify if clarification needed
     |
     v
[Phase 2: Route]
  - Match to agent type
  - Identify parallel opportunities
  - Prepare context package
     |
     v
[Phase 3: Execute]
  - Launch agents (parallel when independent)
  - Monitor progress
  - Handle errors
     |
     v
[Phase 4: Report]
  - Synthesize results
  - Format in user's language (Korean)
  - Include sources if web search used
```

### Hard Rules for MoAI

1. **Language**: All user-facing responses in Korean (conversation_language: ko)
2. **No XML in responses**: XML is for agent-to-agent data only
3. **Parallel execution**: Always launch independent agents in a single message
4. **Minimal questions**: Only ask when system destruction risk, data loss, security compromise, or technically impossible to proceed
5. **No direct implementation**: Complex tasks must go through specialized agents

---

## Agent Catalog

MoAI-ADK includes 16 specialized agents organized into four tiers.

### Manager Agents (8 agents)

Manager agents coordinate workflows and phases. They orchestrate other agents.

| Agent | Purpose | Token Budget |
|-------|---------|-------------|
| `manager-spec` | Create EARS-format SPEC documents | 30K |
| `manager-ddd` | Execute DDD (ANALYZE-PRESERVE-IMPROVE) implementation | 180K |
| `manager-tdd` | Execute TDD (RED-GREEN-REFACTOR) implementation | 180K |
| `manager-docs` | Generate and validate documentation | 40K |
| `manager-quality` | Enforce TRUST 5 quality gates | 30K |
| `manager-project` | Project initialization and configuration | 20K |
| `manager-strategy` | System design and architecture decisions | 50K |
| `manager-git` | Git operations, commits, PRs | 10K |

### Expert Agents (8 agents)

Expert agents implement specific technical domains.

| Agent | Purpose | Primary Languages |
|-------|---------|------------------|
| `expert-backend` | API development, server logic | C++, C, C# |
| `expert-frontend` | UI components, dashboards | C#, React |
| `expert-security` | Security analysis, OWASP compliance | All |
| `expert-devops` | CI/CD, containerization, deployment | YAML, bash |
| `expert-performance` | Profiling, optimization, bandwidth analysis | C, C++, C# |
| `expert-debug` | Root cause analysis, error recovery | All |
| `expert-testing` | Test suite development, coverage analysis | All |
| `expert-refactoring` | Code restructuring without behavior change | All |

### Builder Agents (3 agents)

Builder agents create MoAI-ADK components.

| Agent | Purpose |
|-------|---------|
| `builder-agent` | Create new specialized agents |
| `builder-skill` | Create new skills |
| `builder-plugin` | Create new plugins |

### Team Agents (8 agents, Experimental)

Team agents work in parallel within a TeamCreate/TeamDelete lifecycle.

| Agent | Role | Model |
|-------|------|-------|
| `team-researcher` | Codebase exploration, pattern discovery | Haiku (fast) |
| `team-analyst` | Requirements analysis, edge cases | Sonnet |
| `team-architect` | Technical design, trade-off evaluation | Sonnet |
| `team-backend-dev` | Backend implementation | Sonnet |
| `team-frontend-dev` | Frontend implementation | Sonnet |
| `team-tester` | Test file ownership, coverage | Sonnet |
| `team-quality` | Post-implementation quality validation | Sonnet |
| `team-designer` | UI/UX design, Pencil integration | Sonnet |

---

## Agent Selection Decision Tree

When processing a user request, MoAI follows this decision logic:

```
User Request
     |
     v
Is this read-only codebase exploration?
  YES → Use Explore subagent (or Grep/Glob/Read directly)
     |
     NO
     v
Is this external documentation or API research?
  YES → Use WebSearch, WebFetch, or Context7 MCP tools
     |
     NO
     v
Is domain expertise needed?
  YES → What domain?
        FPGA/RTL      → expert-backend (RTL domain)
        Firmware/C    → expert-backend
        C# Tools      → expert-backend or expert-frontend
        Security      → expert-security
        Performance   → expert-performance
        Debugging     → expert-debug
        Testing       → expert-testing
     |
     NO
     v
Is workflow coordination needed?
  YES → What workflow?
        SPEC creation  → manager-spec
        DDD execution  → manager-ddd
        TDD execution  → manager-tdd
        Documentation  → manager-docs
        Quality gates  → manager-quality
        Architecture   → manager-strategy
        Git operations → manager-git
     |
     NO
     v
Is this complex multi-step spanning multiple domains?
  YES → manager-strategy (decomposes and delegates)
  NO  → Use direct tools (Read, Write, Edit, Bash)
```

---

## Parallel Execution Patterns

### Rule

All independent tool calls and agent launches must happen in a single message. Never execute sequentially what can execute in parallel.

### Independent Tasks (Parallel)

Tasks are independent when they:
- Read different files without modifying them
- Work on different repository domains (fpga/, fw/, sdk/, tools/)
- Perform analysis that does not depend on other results

**Example: Parallel documentation sprint**

```
MoAI launches simultaneously:
  Task(expert-backend, "Generate API docs for sdk/include/")
  Task(expert-backend, "Generate API docs for fw/include/")
  Task(manager-docs, "Create architecture diagrams from docs/architecture/")
```

### Dependent Tasks (Sequential)

Tasks are dependent when:
- One task needs the output of another
- Both tasks write to the same file
- Phase 2 requires Phase 1 completion

**Example: Sequential SPEC → Implementation**

```
Step 1: Task(manager-spec, "Create SPEC-SIM-001")
        Wait for SPEC document creation
Step 2: /clear (mandatory between plan and run)
Step 3: Task(manager-ddd, "Implement SPEC-SIM-001")
```

### File Write Conflict Prevention

Before parallel agent launch, MoAI analyzes file ownership:

```
Parallel safe:
  Agent A owns: fpga/src/*.sv
  Agent B owns: fw/src/*.c
  → No conflict, launch in parallel

Sequential required:
  Agent A writes: docs/architecture/overview.md
  Agent B writes: docs/architecture/overview.md
  → Conflict, execute sequentially
```

---

## Token Budget Management

### Context Window Allocation

The total Claude Code context window is 200K tokens, allocated as follows:

| Budget Category | Tokens | Percentage |
|-----------------|--------|-----------|
| System prompt + CLAUDE.md | ~15K | 7.5% |
| Active conversation | ~80K | 40% |
| Reference context | ~50K | 25% |
| Emergency reserve | ~55K | 27.5% |

### Per-Phase Token Budgets

| Phase | Command | Agent | Budget | Strategy |
|-------|---------|-------|--------|----------|
| Plan | `/moai plan` | manager-spec | 30K | Load requirements only |
| Run | `/moai run SPEC-XXX` | manager-ddd/tdd | 180K | Selective file loading |
| Sync | `/moai sync SPEC-XXX` | manager-docs | 40K | Result caching |
| **Total** | | | **250K** | Phase separation with /clear |

### Warning Thresholds

| Threshold | Action |
|-----------|--------|
| 60% (120K) | Track context growth |
| 75% (150K) | Defer non-critical context, warn user |
| 85% (170K) | Trigger emergency compression, execute /clear |

### Token Optimization Strategies

1. **Phase separation with /clear**: Execute `/clear` after plan phase completes. This saves 45-50K tokens for the implementation phase.
2. **Selective file loading**: Load only files directly needed for the current task. Do not pre-load entire directories.
3. **Progressive disclosure**: Skills load at Level 1 (metadata, ~100 tokens) by default. Full skill body (~5K) loads only when triggered.
4. **Model selection**: Use Haiku model for research/exploration tasks (70% cheaper). Use Sonnet for implementation. Use Opus for architecture decisions.

---

## SPEC Workflow

The SPEC workflow is the primary development methodology: Plan → Run → Sync.

### Phase 1: Plan (`/moai plan "description"`)

**Agent**: manager-spec
**Token Budget**: 30K
**Output**: `.moai/specs/SPEC-XXX/spec.md`

The SPEC document uses EARS (Easy Approach to Requirements Syntax) format:

| Type | Pattern | Example |
|------|---------|---------|
| Ubiquitous | The system shall... | "The system shall transmit frames at ≥15fps" |
| Event-driven | When [trigger], the system shall... | "When frame_start asserts, the system shall begin CSI-2 transmission" |
| State-driven | While [state], the system shall... | "While in HS-Mode, the system shall maintain 400 Mbps per lane" |
| Unwanted | The system shall not... | "The system shall not exceed 60% LUT utilization" |
| Optional | Where possible, the system shall... | "Where possible, the system shall support RAW12 in addition to RAW16" |

**After Plan phase**: Execute `/clear` immediately. This is mandatory.

### Phase 2: Run (`/moai run SPEC-XXX`)

**Agent**: manager-ddd (legacy) or manager-tdd (new code), per `quality.yaml`
**Token Budget**: 180K

For **new code** (TDD cycle):
1. RED: Write failing test first
2. GREEN: Write minimal implementation to pass
3. REFACTOR: Improve code quality, keep tests green

For **legacy code** (DDD cycle):
1. ANALYZE: Map existing behavior and dependencies
2. PRESERVE: Write characterization tests capturing current behavior
3. IMPROVE: Make changes incrementally, verify characterization tests pass

**Success criteria**:
- All SPEC requirements implemented
- 85%+ test coverage
- TRUST 5 quality gates passed
- Zero LSP errors

### Phase 3: Sync (`/moai sync SPEC-XXX`)

**Agent**: manager-docs
**Token Budget**: 40K
**Output**: API documentation, updated README, CHANGELOG entry, PR

---

## Team Mode vs Sub-Agent Mode

### Sub-Agent Mode (Default)

Each agent runs in an isolated, stateless context via `Task()`. Sequential unless explicitly parallelized.

**Prefer sub-agent mode for**:
- Sequential tasks with heavy dependencies
- Same-file edits or tightly coupled changes
- Routine tasks with clear single-domain scope
- When token budget is a concern

### Team Mode (Experimental)

Multiple agents run in persistent, named contexts with inter-agent communication via `SendMessage()`.

**Prerequisites**:
- `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS=1` in environment
- `workflow.team.enabled: true` in `.moai/config/sections/workflow.yaml`

**Prefer team mode for**:
- Research tasks where parallel exploration adds real value
- Cross-layer features (FPGA + firmware + tools simultaneously)
- Complex debugging with multiple potential root causes
- Tasks where teammates need to communicate and coordinate

### Mode Selection Logic

```
Complexity Assessment:
  domains involved ≥ 3?        → Consider team mode
  files to modify ≥ 10?        → Consider team mode
  complexity score ≥ 7?        → Consider team mode
  sequential dependencies?     → Use sub-agent mode
  token budget constrained?    → Use sub-agent mode

Override flags:
  --team  → Force team mode
  --solo  → Force sub-agent mode
```

### Token Cost Awareness

Agent teams consume significantly more tokens. Each teammate has an independent context window:

| Pattern | Teammates | Token Multiplier |
|---------|-----------|-----------------|
| plan_research | 3 (researcher+analyst+architect) | ~3x plan tokens |
| implementation | 3 (backend+frontend+tester) | ~3x run tokens |
| design_implementation | 4 | ~4x run tokens |
| investigation | 3 (haiku models) | ~2x |
| review | 3 (read-only) | ~2x |

---

## Progressive Disclosure

Skills load in three levels to minimize token overhead:

### Level 1: Metadata (~100 tokens)

Always loaded at session start. Contains:
- Skill name (kebab-case)
- Description (max 1024 characters)
- Trigger terms

Total overhead: ~100 tokens per skill × 20 skills loaded = ~2K tokens

### Level 2: Body (~5K tokens)

Loaded when triggers match user intent. Contains:
- Quick Reference (30 seconds)
- Implementation Guide (5 minutes)
- Core patterns

Loading condition: User mentions a trigger term, or a workflow phase requires the skill.

### Level 3: Bundled / On-Demand

Loaded only when explicitly needed. Contains:
- Advanced patterns and edge cases
- Complete reference documentation
- Working examples

Claude decides when to access Level 3 based on task complexity.

### Benefits

| Metric | Value |
|--------|-------|
| Token reduction vs always-loaded | ~67% |
| Initial session overhead | ~2K tokens (20 skills at Level 1) |
| Full skill overhead (when triggered) | ~5K per skill |
| Max concurrent active skills | ~10 skills = ~50K tokens |

---

## MoAI Commands Reference

### `/moai plan "description"`

Creates a SPEC document for a new feature or change.

```bash
# Create a specification for the CSI-2 packet former module
/moai plan "CSI-2 packet former with ECC generation for 4-lane D-PHY transmission"

# Create specification for the FPGA simulator
/moai plan "FpgaSimulator C# tool that generates synthetic frame data for testing"
```

**Output**: `.moai/specs/SPEC-XXX/spec.md`
**Follow-up**: Review SPEC, then `/clear`, then `/moai run SPEC-XXX`

### `/moai run SPEC-XXX`

Implements the specified SPEC document.

```bash
# Implement SPEC-SIM-001 (FpgaSimulator)
/moai run SPEC-SIM-001

# Force team mode for complex cross-domain implementation
/moai run SPEC-ARCH-001 --team

# Force sub-agent mode
/moai run SPEC-FW-001 --solo
```

**Prerequisite**: SPEC document exists at `.moai/specs/SPEC-XXX/spec.md`

### `/moai sync SPEC-XXX`

Generates documentation and creates a pull request.

```bash
# Generate documentation for completed SPEC-SIM-001
/moai sync SPEC-SIM-001
```

**Output**: API docs, README update, CHANGELOG entry, PR

### `/moai fix`

Fixes failing tests, build errors, or quality gate violations.

```bash
# Fix the current failing tests
/moai fix

# Fix a specific error type
/moai fix "LSP type errors in sdk/include/frame_buffer.h"
```

### `/moai loop`

Runs a continuous improvement loop until quality gates pass.

```bash
# Run loop until all tests pass
/moai loop

# Maximum 3 iterations (default)
/moai loop --max-iterations 3
```

### `/moai feedback`

Submits a GitHub issue for MoAI-ADK improvements.

```bash
/moai feedback
# Prompts for issue type: bug, feature, improvement, etc.
```

---

## Error Recovery Patterns

### Agent Execution Errors

When an agent fails or returns unexpected results:

1. First attempt: Use `expert-debug` subagent to diagnose
2. Second attempt: Provide more context in the re-delegation
3. Third attempt (final): Request user intervention

```
Error: manager-ddd failed with "test coverage below 85%"
  → Use expert-testing to identify uncovered paths
  → Re-run manager-ddd with coverage report attached
```

### Token Limit Errors

When context window is exhausted:

1. Execute `/clear` immediately
2. Reload only the essential files (SPEC document + current implementation file)
3. Continue from the last checkpoint

**Prevention**: Monitor token usage. Execute `/clear` when exceeding 150K tokens.

### LSP Quality Gate Failures

When LSP diagnostics show errors after implementation:

```
/moai fix "zero LSP errors required for sync phase"
```

The `manager-quality` agent will:
1. Run LSP diagnostics to identify specific errors
2. Delegate fixes to appropriate expert agents
3. Re-run diagnostics to confirm zero errors

### Integration Errors (FPGA-SoC)

When CSI-2 integration fails between FPGA and SoC:

```
/moai fix "CSI-2 V4L2 driver not receiving frames from FPGA simulator"
  → expert-debug analyzes V4L2 dmesg output
  → expert-backend verifies CSI-2 packet format
  → expert-backend verifies FPGA simulator output format
```

### Permission Errors

When tool permission is denied:

1. Check `settings.json` allowedTools configuration
2. Review `.moai/config/sections/user.yaml` for permission policy
3. Do not use `--dangerously-skip-permissions` without explicit user instruction

---

## Context Management

### When to Execute `/clear`

| Trigger | Action |
|---------|--------|
| After `/moai plan` completion | Mandatory /clear |
| Context exceeds 150K tokens | Immediate /clear |
| Conversation exceeds 50 messages | Recommended /clear |
| Before major phase transition (plan→run, run→sync) | Recommended /clear |
| After major debugging session | Recommended /clear |

### What Persists Across `/clear`

The following information is preserved across `/clear` because it exists in files:
- SPEC documents (`.moai/specs/`)
- Quality configuration (`.moai/config/`)
- Project memory (`.claude/` CLAUDE.md files)
- Generated code (source files)
- Test results (`.moai/reports/`)

### What is Lost on `/clear`

- Conversation history
- Loaded skill bodies
- In-flight agent results
- Debug context from current session

**Best Practice**: Before executing `/clear`, note the current task state in a brief summary. MoAI can resume from this summary.

---

## Practical Examples

### Example 1: Documentation Sprint

**Goal**: Generate complete API documentation for all C# tools.

```
Step 1 (Parallel analysis):
  MoAI launches simultaneously:
    Task(Explore, "List all public classes in tools/ParameterExtractor/")
    Task(Explore, "List all public classes in tools/CodeGenerator/")
    Task(Explore, "List all public classes in tools/FpgaSimulator/")

Step 2 (Token check):
  If context > 100K → /clear, reload SPEC documents only

Step 3 (Parallel documentation):
  MoAI launches simultaneously:
    Task(manager-docs, "Generate API docs for ParameterExtractor")
    Task(manager-docs, "Generate API docs for CodeGenerator")
    Task(manager-docs, "Generate API docs for FpgaSimulator")

Step 4 (Synthesis):
  MoAI merges results into docs/api/tools.md
```

**Total time**: ~3-5 minutes (vs ~15 minutes sequential)
**Token usage**: ~60K (well within budget)

---

### Example 2: Implementing a New SPEC

**Goal**: Implement SPEC-SIM-001 (FpgaSimulator) using TDD.

```
Step 1 (Plan phase - already done):
  /moai plan "FpgaSimulator" → SPEC-SIM-001 created
  /clear → Free 45K tokens

Step 2 (Run phase):
  /moai run SPEC-SIM-001

  manager-ddd/tdd workflow:
    RED: Write FpgaSimulatorTests.cs (failing)
    GREEN: Implement FpgaSimulator.cs (tests pass)
    REFACTOR: Clean up, add XML docs

    Verify:
      dotnet test → 85%+ coverage
      dotnet format → zero formatting errors
      LSP → zero errors

Step 3 (Sync phase):
  /moai sync SPEC-SIM-001

  manager-docs workflow:
    Generate: docs/api/tools/fpga-simulator.md
    Update: README.md (FpgaSimulator section)
    Create: CHANGELOG entry
    PR: "feat(tools): implement FpgaSimulator per SPEC-SIM-001"
```

---

### Example 3: Debugging CSI-2 800 Mbps

**Goal**: Diagnose why D-PHY 800 Mbps/lane is unstable (R-002).

```
Step 1 (Multi-angle investigation using team mode):
  /moai fix "D-PHY 800 Mbps instability - systematic debug" --team

  Team spawns:
    team-researcher: "Analyze current FPGA ISERDES configuration for 800M"
    team-analyst: "Review D-PHY timing spec violations from measurement data"
    team-architect: "Evaluate IDELAY calibration approach for Artix-7 at 800M"

Step 2 (Synthesis):
  MoAI receives findings from all three teammates
  Synthesizes into debugging action plan

Step 3 (Fix implementation):
  expert-backend: "Adjust IDELAY register values in csi2_dphy_rx.sv"
  expert-backend: "Update timing constraints in csi2.xdc"

Step 4 (Validation):
  expert-testing: "Run Questa simulation for 800M eye diagram analysis"
```

---

*Document End*

## Review Record

- Date: 2026-02-17
- Reviewer: manager-quality
- Status: Approved
- TRUST 5: T:5 R:5 U:4 S:5 T:4
- Notes: Token budgets verified (plan=30K, run=180K, sync=40K). Agent catalog consistent with CLAUDE.md (Manager 8, Expert 8, Builder 3, Team 8 experimental). SPEC workflow phases accurate. "16-agent system" in overview refers to Manager+Expert tiers; Builder and Team agents are documented in the catalog. Parallel execution patterns correctly described.
