---
name: abyz-lab-reference
description: >
  Common execution patterns, flag reference, legacy command mapping,
  configuration file paths, and error handling delegation used across all
  ABYZ-Lab workflows. Provides resume patterns and context propagation guidance.
  Use when needing execution patterns, flag details, or configuration reference.
license: Apache-2.0
compatibility: Designed for Claude Code
user-invocable: false
metadata:
  version: "1.1.0"
  category: "foundation"
  status: "active"
  updated: "2026-02-03"
  tags: "reference, patterns, flags, configuration, legacy, resume, context"

# ABYZ-Lab Extension: Progressive Disclosure
progressive_disclosure:
  enabled: true
  level1_tokens: 100
  level2_tokens: 5000

# ABYZ-Lab Extension: Triggers
triggers:
  keywords: ["reference", "pattern", "flag", "config", "resume", "legacy", "mapping"]
  agents: ["manager-spec", "manager-ddd", "manager-docs", "manager-quality", "manager-git"]
  phases: ["plan", "run", "sync"]
---

# ABYZ-Lab Skill Reference

Common patterns, flag reference, legacy command mapping, and configuration files used across all ABYZ-Lab workflows.

---

## Execution Patterns

### Parallel Execution Pattern

When multiple operations are independent, invoke them in a single response. Claude Code automatically runs multiple Task() calls in parallel (up to 10 concurrent).

Use Cases:

- Exploration Phase: Launch codebase analysis, documentation research, and quality assessment simultaneously via separate Task() calls
- Diagnostic Scan: Run LSP diagnostics, AST-grep analysis, and linter checks in parallel
- Multi-file Generation: Generate product.md, structure.md, and tech.md simultaneously when analysis is complete

Implementation:

- Include multiple Task() calls in the same response message
- Each Task() targets a different subagent or a different scope within the same agent
- Results are collected when all parallel tasks complete
- Maximum 10 concurrent Task() calls for optimal throughput

### Sequential Execution Pattern

When operations have dependencies, chain them sequentially. Each Task() call receives context from the previous phase results.

Use Cases:

- DDD Workflow: Phase 1 (planning) feeds Phase 2 (implementation) feeds Phase 2.5 (quality validation)
- SPEC Creation: Explore agent results feed into manager-spec agent for document generation
- Release Pipeline: Quality gates must pass before version selection, which must complete before tagging

Implementation:

- Wait for each Task() to return before invoking the next
- Include previous phase outputs in the next Task() prompt as context
- Ensure semantic continuity: each agent receives sufficient context to operate independently

### Hybrid Execution Pattern

Combine parallel and sequential patterns within a single workflow.

Use Cases:

- Fix Workflow: Parallel diagnostic scan (LSP + linters + AST-grep), then sequential fix application based on combined results
- ABYZ-Lab Workflow: Parallel exploration phase, then sequential SPEC generation and DDD implementation
- Run Workflow: Parallel quality checks, then sequential implementation tasks

Implementation:

- Identify which operations are independent (parallelize these)
- Identify which operations depend on prior results (sequence these)
- Group parallel operations at the beginning of each phase, followed by sequential dependent operations

---

## Resume Pattern

When a workflow is interrupted or needs to continue from a previous session, use the --resume flag.

Behavior:

- Read existing SPEC document from .abyz-lab/specs/SPEC-XXX/
- Determine last completed phase from SPEC status markers
- Skip completed phases and resume from the next pending phase
- Preserve all prior analysis, decisions, and generated artifacts

Applicable Workflows:

- plan --resume SPEC-XXX: Resume SPEC creation from last checkpoint
- run --resume SPEC-XXX: Resume DDD implementation from last completed task
- abyz-lab --resume SPEC-XXX: Resume full autonomous workflow from last phase
- fix --resume: Resume fix cycle from last diagnostic state

---

## Context Propagation Between Phases

Each phase must pass results forward to the next phase to avoid redundant analysis.

Required Context Elements:

- Exploration Results: File paths, architecture patterns, technology stack, dependency map
- SPEC Data: Requirements list, acceptance criteria, technical approach, scope boundaries
- Implementation Results: Files modified, tests created, coverage metrics, remaining tasks
- Quality Results: Test pass/fail counts, lint errors, type check results, security findings
- Git State: Current branch, commit count since last tag, tag history

Propagation Method:

- Include a structured summary of previous phase outputs in the Task() prompt
- Reference specific file paths rather than inline large content blocks
- Use SPEC document as the canonical source of truth across phases

---

## Flag Reference

### Global Flags (Available Across All Workflows)

- --resume [ID]: Resume workflow from last checkpoint (SPEC-ID or snapshot ID)
- --seq: Force sequential execution instead of parallel where applicable
- --ultrathink: Activate Sequential Thinking MCP for deep analysis before execution

### Plan Flags

- --worktree: Create an isolated git worktree for the SPEC implementation
- --branch: Create a feature branch for the SPEC (default branch naming: spec/SPEC-XXX)
- --resume SPEC-XXX: Resume an interrupted plan session

### Run Flags

- --resume SPEC-XXX: Resume DDD implementation from last completed task

### Sync Flags

- Modes (positional): auto (default), force, status, project
- --merge: Auto-merge PR and clean up branch after sync

### Fix Flags

- --dry: Preview detected issues without applying fixes
- --level N: Control fix depth (Level 1: auto-fixable, Level 2: simple logic, Level 3: complex, Level 4: architectural)
- --security: Include security issues in scan

### Loop Flags

- --max N: Maximum iteration count (default: 100)
- --auto: Enable automatic fix application for Level 1-2

### ABYZ-Lab (Default) Flags

- --loop: Enable iterative fixing during run phase
- --max N: Maximum fix iterations when --loop is active
- --branch: Create feature branch before implementation
- --pr: Create pull request after completion

---

## Legacy Command Mapping

Previous /abyz-lab:X-Y command format mapped to new /abyz-lab subcommand format:

- /abyz-lab:0-project maps to /abyz-lab project
- /abyz-lab:1-plan maps to /abyz-lab plan
- /abyz-lab:2-run maps to /abyz-lab run
- /abyz-lab:3-sync maps to /abyz-lab sync
- /abyz-lab:9-feedback maps to /abyz-lab feedback
- /abyz-lab:fix maps to /abyz-lab fix
- /abyz-lab:loop maps to /abyz-lab loop
- /abyz-lab:abyz-lab maps to /abyz-lab (default autonomous workflow)

Note: /abyz-lab:99-release is a separate local-only command, not part of the /abyz-lab skill.

---

## Configuration Files Reference

### Core Configuration

- .abyz-lab/config/config.yaml: Main configuration file (merged from section files)
- .abyz-lab/config/sections/language.yaml: Language settings (conversation_language, agent_prompt_language, code_comments)
- .abyz-lab/config/sections/user.yaml: User identification (name)
- .abyz-lab/config/sections/quality.yaml: TRUST 5 framework settings, LSP quality gates, test coverage targets
- .abyz-lab/config/sections/system.yaml: System metadata (abyz-lab.version)

### Project Documentation

- .abyz-lab/project/product.md: Product overview, features, user value
- .abyz-lab/project/structure.md: Project architecture and directory organization
- .abyz-lab/project/tech.md: Technology stack, dependencies, technical decisions

### SPEC Documents

- .abyz-lab/specs/SPEC-XXX/spec.md: Specification document with EARS format requirements
- .abyz-lab/specs/SPEC-XXX/plan.md: Execution plan with task breakdown
- .abyz-lab/specs/SPEC-XXX/acceptance.md: Acceptance criteria and test plan

### Release Artifacts

- CHANGELOG.md: Bilingual changelog (English + Korean per version)
- .abyz-lab/cache/release-snapshots/latest.json: Release state snapshot for recovery

### Version Files (5 files synchronized during release)

- pyproject.toml: Authoritative version source
- pkg/version/version.go: Runtime version with build-time injection
- .abyz-lab/config/config.yaml: Config display version
- .abyz-lab/config/sections/system.yaml: System metadata version
- internal/template/templates/: Embedded template directory for binary bundling

---

## Completion Markers

AI adds markers to signal workflow state:

- `<abyz-lab>DONE</abyz-lab>`: Single task or phase completed
- `<abyz-lab>COMPLETE</abyz-lab>`: Full workflow completed (all phases finished)

These markers enable automation detection and loop termination in the loop workflow.

---

## Error Handling Delegation

- Quality gate failures: Use expert-debug subagent for diagnosis and resolution
- Agent execution failures: Use expert-debug subagent for investigation
- Token limit errors: Execute /clear, then guide user to resume with --resume flag
- Permission errors: Review .claude/settings.json manually
- Integration errors: Use expert-devops subagent
- ABYZ-Lab-ADK errors: Suggest /abyz-lab feedback to create a GitHub issue

---

Version: 1.1.0
Last Updated: 2026-01-28
