# SPEC-GUITOOLS-001: GUI Test Automation Tool Requirements Specification

---
id: SPEC-GUITOOLS-001
version: 1.1.0
status: draft
created: 2026-02-18
updated: 2026-02-18
author: ABYZ-Lab Agent (analyst)
priority: medium
milestone: M6
gate_week: W26
---

## HISTORY

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.1.0 | 2026-02-18 | ABYZ-Lab Agent (analyst) | Add debugging strategy, update performance constraints with x2 margins, refine risk mitigation |
| 1.0.0 | 2026-02-18 | ABYZ-Lab Agent (analyst) | Initial SPEC creation for GUI test automation tool |

---

## Overview

### Scope

This SPEC covers a **standalone GUI test automation tool** that operates externally from target GUI applications:

| Component | Purpose | Technology |
|-----------|---------|-----------|
| **GuiTestRunner** | CLI test executor that launches target apps and automates UI interactions | C# .NET 8.0 |
| **TestScenario Framework** | JSON-based test scenario definition language | JSON Schema |
| **FlaUI Wrapper** | Abstraction layer over FlaUI for UI automation | C# |
| **LogVerifier** | Log-based assertion engine for validation | C# |
| **CI/CD Integration** | GitHub Actions workflow for automated testing | YAML |

**Key Design Principle**: Complete separation between test tool and target applications. The test tool runs as an external process, controls GUI apps via UI Automation, and validates through log file analysis.

### Development Methodology

All components are **new code** and follow **TDD (RED-GREEN-REFACTOR)** per quality.yaml.

### Target Applications

The test tool is designed to automate testing of:

1. **GUI.Application** (tools/GUI.Application/) - Unified WPF GUI for detector control
2. **ParameterExtractor.Wpf** (tools/ParameterExtractor/src/ParameterExtractor.Wpf/) - Parameter extraction GUI

---

## Requirements

### 1. Architecture Requirements

**REQ-GUITOOLS-001**: The GuiTestRunner **shall** execute as a separate process from target applications, controlling them via Microsoft UI Automation API.

**WHY**: Complete separation ensures zero coupling between test code and application code. Tests remain valid even as application internals change.

**IMPACT**: GuiTestRunner launches target exe as subprocess, attaches via FlaUI UIA3, no test references in target app projects.

---

**REQ-GUITOOLS-002**: The test tool **shall** be stored in a separate repository (`gui-test-tools/`) with independent version control from the main project.

**WHY**: Independent development cycle. Test tool can evolve without affecting main project release cadence.

**IMPACT**: Separate Gitea repository. Git submodule or NuGet package for distribution.

---

**REQ-GUITOOLS-003**: Test scenarios **shall** be defined in JSON format, separate from test runner code.

**WHY**: Non-programmers can write tests. Scenarios can be versioned independently from runner code.

**IMPACT**: `TestScenarios/*.json` files with schema validation. Scenario parser in runner.

---

### 2. GuiTestRunner CLI Requirements

**REQ-GUITOOLS-010**: The GuiTestRunner **shall** provide a CLI interface: `GuiTestRunner.exe <target-app> <scenario-file> [options]`

**WHY**: CLI interface enables CI/CD integration and scriptable test execution.

**IMPACT**: `Main(string[] args)` parses arguments. Options include `--verbose`, `--screenshot-dir`, `--timeout`.

---

**REQ-GUITOOLS-011**: **WHEN** launched **THEN** the GuiTestRunner **shall** start the target application and wait for the main window to be ready.

**WHY**: Tests must begin only when the application is fully initialized.

**IMPACT**: `Application.Launch()` with retry logic. Main window detection via `GetMainWindow()` with timeout.

---

**REQ-GUITOOLS-012**: **WHEN** test execution completes **THEN** the GuiTestRunner **shall** close the target application and exit with code 0 for pass, 1 for fail.

**WHY**: CI/CD pipelines rely on exit codes for pass/fail determination. Clean shutdown prevents zombie processes.

**IMPACT**: `app.Close()` with graceful shutdown. Exit code set from test result.

---

**REQ-GUITOOLS-013**: The GuiTestRunner **shall** support `--list-scenarios` flag to display all available test scenarios without execution.

**WHY**: Test discovery and documentation. CI scripts can validate scenario existence before execution.

**IMPACT**: Scan `TestScenarios/` directory, parse JSON files, print scenario names and descriptions.

---

### 3. TestScenario JSON Requirements

**REQ-GUITOOLS-020**: Test scenarios **shall** support the following step types:

| Step Type | Purpose | Example Parameters |
|-----------|---------|-------------------|
| `Click` | Simulate mouse click | `target: {automationId: "StartButton"}` |
| `Type` | Simulate keyboard input | `target: {...}, text: "config.yaml"` |
| `Wait` | Pause execution | `duration: 5000` (ms) |
| `Verify` | Assert UI state | `assertions: [...]` |
| `LogCheck` | Assert log file content | `contains: "Acquisition started"` |

**WHY**: Comprehensive set of actions to exercise all GUI interactions.

**IMPACT**: `StepType` enum with handler per type. Extensible design for new step types.

---

**REQ-GUITOOLS-021**: Test scenarios **shall** support element targeting by `automationId`, `name`, or `xpath`.

**WHY**: Different applications may use different identification strategies. Fallback mechanisms improve robustness.

**IMPACT**: `ElementTarget` JSON object with optional fields. Try automationId → name → xpath in order.

---

**REQ-GUITOOLS-022**: **WHEN** a step fails **THEN** the TestScenario execution **shall** stop immediately and report the failure with step index and description.

**WHY**: Fast feedback on test failure. Continuing after failure produces misleading error messages.

**IMPACT**: Exception on assertion failure. Test result includes `FailedAtStep` index.

---

**REQ-GUITOOLS-023**: Test scenarios **shall** support `tags` array for categorization (smoke, regression, e2e).

**WHY**: Selective test execution. CI can run smoke tests on every commit, full regression nightly.

**IMPACT**: JSON `tags: ["smoke", "critical"]`. CLI flag `--tags smoke` to filter.

---

### 4. FlaUI Wrapper Requirements

**REQ-GUITOOLS-030**: The FlaUI wrapper **shall** provide a simplified API for common UI operations: `Click()`, `TypeText()`, `WaitForElement()`, `GetText()`, `Exists()`.

**WHY**: Direct FlaUI API is verbose. Wrapper reduces test code complexity.

**IMPACT**: `FlaUIHelper` class with extension methods. Error handling and retry logic built-in.

---

**REQ-GUITOOLS-031**: **WHEN** an element is not found **THEN** the wrapper **shall** throw `ElementNotFoundException` with element identification details.

**WHY**: Clear error messages accelerate test debugging. Generic timeout messages are insufficient.

**IMPACT**: Exception includes `AutomationId`, `Name`, `XPath` attempted, timeout duration.

---

**REQ-GUITOOLS-032**: The wrapper **shall** automatically retry UI operations with exponential backoff (initial 100ms, max 5s).

**WHY**: GUI applications have timing variations. Retry reduces flaky tests.

**IMPACT**: `RetryPolicy` configuration per operation type. Configurable via CLI flag.

---

**REQ-GUITOOLS-033**: **WHEN** a test fails **THEN** the wrapper **shall** capture a screenshot of the application window.

**WHY**: Screenshots provide context for failure diagnosis in CI logs.

**IMPACT**: `CaptureScreenshot()` on exception. Save to `--screenshot-dir` with timestamp.

---

### 5. LogVerifier Requirements

**REQ-GUITOOLS-040**: The LogVerifier **shall** monitor application log files and support pattern matching for assertions.

**WHY**: Log-based validation confirms internal behavior without UI inspection.

**IMPACT**: `LogVerifier` class with `ContainsPattern()`, `MatchesRegex()`, `HasLevel()` methods.

---

**REQ-GUITOOLS-041**: **WHEN** `LogCheck` step is executed **THEN** the LogVerifier **shall** tail the log file and wait for the pattern match with timeout.

**WHY**: Log entries may be written after UI action completes. Asynchronous verification needed.

**IMPACT**: `FileSystemWatcher` or polling loop. Timeout default 10 seconds, configurable.

---

**REQ-GUITOOLS-042**: The LogVerifier **shall** parse Serilog log format and support filtering by timestamp, level, and message template.

**WHY**: Target applications use Serilog for logging. Parsing enables precise assertions.

**IMPACT**: Regex for Serilog format `[Timestamp] [Level] Message`. Filter methods for time range.

---

**REQ-GUITOOLS-043**: **WHEN** logs are verified **THEN** the LogVerifier **shall** report matched log lines with timestamp and level for debugging.

**WHY**: Test reports should show which log entries satisfied assertions.

**IMPACT**: `LogMatch` result object with `Timestamp`, `Level`, `Message`, `LineNumber`.

---

### 6. CI/CD Integration Requirements

**REQ-GUITOOLS-050**: The project **shall** include a GitHub Actions workflow that builds target apps and executes GUI tests on Windows runners.

**WHY**: Automated regression testing on every pull request prevents breaking changes.

**IMPACT**: `.github/workflows/gui-test.yml` with `runs-on: windows-latest`.

---

**REQ-GUITOOLS-051**: **WHEN** tests fail in CI **THEN** the workflow **shall** upload test logs and screenshots as artifacts.

**WHY**: Developers need diagnostic data to fix CI failures without local reproduction.

**IMPACT**: `actions/upload-artifact@v3` for `logs/` and `screenshots/` directories.

---

**REQ-GUITOOLS-052**: The CI workflow **shall** support parallel execution of independent test scenarios.

**WHY**: Parallel execution reduces total CI runtime for large test suites.

**IMPACT**: GitHub Actions `matrix` strategy. Test scenarios grouped by independence.

---

**REQ-GUITOOLS-053**: **WHEN** all tests pass **THEN** the workflow **shall** generate a test summary report with pass/fail counts and execution time.

**WHY**: PR comments with test summaries provide quick visibility to reviewers.

**WHY**: Summary job after test matrix completion. Report via PR comment.

---

### 7. Unwanted Requirements

**REQ-GUITOOLS-060**: The test tool **shall not** modify target application code or add test-specific dependencies.

**WHY**: Separation of concerns. Target applications should not require test instrumentation.

**IMPACT**: No test attributes in target app code. No reference to test assemblies.

---

**REQ-GUITOOLS-061**: Test scenarios **shall not** include embedded code or script execution.

**WHY**: Security risk. Test scenarios should be declarative, not executable code.

**IMPACT**: JSON parser rejects unknown fields. No `eval()` or code execution.

---

**REQ-GUITOOLS-062**: The test tool **shall not** require administrative privileges or elevated permissions.

**WHY**: CI environments run with standard user permissions. Privilege escalation limits portability.

**IMPACT**: No registry access to protected keys. No `runas` requirements.

---

### 8. Optional Requirements

**REQ-GUITOOLS-070**: **Where possible**, the GuiTestRunner should support headless mode for CI environments without display.

**WHY**: Some CI environments lack active display. Headless mode enables testing in these environments.

**IMPACT**: Display emulator (e.g., xvfb on Linux, `Desktop` API on Windows). Priority: low.

---

**REQ-GUITOOLS-071**: **Where possible**, test scenarios should support data-driven testing with parameter tables.

**WHY**: Testing multiple input combinations without duplicating scenarios.

**IMPACT**: `parameters` array in JSON. Iteration in runner. Priority: low.

---

**REQ-GUITOOLS-072**: **Where possible**, the LogVerifier should support structured log queries (e.g., Serilog Expressions).

**WHY**: Complex assertions on log data may require filter expressions.

**IMPACT**: Serilog.Expressions integration. Priority: low.

---

## Technical Constraints

### Platform Constraints

| Constraint | Value | Rationale |
|-----------|-------|-----------|
| Target Framework | .NET 8.0 LTS | Consistent with Host SDK (SPEC-SDK-001) |
| Language | C# 12 | Modern language features, consistent toolchain |
| UI Automation Library | FlaUI.UIA3 4.0+ | Active development, WPF support |
| Test Runtime | Windows 10+ | UI Automation API requires Windows |
| CI Environment | GitHub Actions (windows-latest) | CI/CD infrastructure |

### Tool-Specific Constraints

| Component | Constraint | Value |
|-----------|-----------|-------|
| FlaUI Wrapper | Timeout (element wait) | 5 seconds default, configurable |
| LogVerifier | Log file polling | 100ms interval, 10s timeout |
| TestScenario | Max scenario file size | 100 KB (prevents abuse) |
| TestScenario | Max step count per scenario | 100 steps |
| GuiTestRunner | Concurrent apps | 1 target app per runner instance |

### Performance Constraints

| Metric | Target | Component | Notes |
|--------|--------|-----------|-------|
| Test startup time | < 3 seconds | GuiTestRunner | Acceptable overhead for real app execution |
| Element find timeout | < 10 seconds | FlaUI Wrapper | **x2 margin** for timing variations |
| Log pattern match | < 20 seconds | LogVerifier | **x2 margin** for async log writes |
| Screenshot capture | < 1 second | FlaUI Wrapper | Fast enough for CI diagnostics |
| Total scenario execution | < 120 seconds | Typical smoke test | **Real usage validation** (feature, not bug) |

**Performance Philosophy**: Test execution time reflects actual application behavior. This is intentional validation, not overhead to be minimized.

---

## Debugging Strategy

### Principle: Log-Based Modular Debugging

When GUI tests fail, the debugging process follows a structured workflow to ensure efficient resolution:

```
┌─────────────────────────────────────────────────────────────┐
│  [1단계] 실패 감지                                            │
│  - Test fails at step N                                      │
│  - Screenshot captured automatically                         │
│  - Log snippets collected                                   │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│  [2단계] GitHub Issue 등록                                   │
│  Template: GUI-TEST-{module}-{step}                          │
│  - Module: GUI.Application / ParameterExtractor.Wpf          │
│  - Attach: Screenshot + Log excerpts                         │
│  - Tag: gui-test-failure                                    │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│  [3단계] 일괄 수정 작업                                       │
│  - Fix batch: accumulate similar failures                   │
│  - Single fix session for all issues                        │
│  - Re-test all affected scenarios                           │
└─────────────────────────────────────────────────────────────┘
```

### Issue Template

```markdown
## GUI Test Failure: {ScenarioName} @ Step {StepIndex}

### Environment
- Runner: GuiTestRunner v{version}
- Target App: {AppName} v{version}
- Platform: Windows 10/11

### Failure Details
- **Step Index**: {N}
- **Expected**: {expected behavior}
- **Actual**: {actual behavior}
- **Log Check**: {log pattern} not found within {timeout}s

### Diagnostics
**Screenshot**: `screenshots/failure_{timestamp}.png`

**Log Excerpt**:
```
{last 20 lines before failure}
```

### Module
- [ ] GUI.Application (MainViewModel, FramePreviewViewModel, etc.)
- [ ] ParameterExtractor.Wpf
- [ ] LogVerifier (false positive)
- [ ] FlaUI Wrapper (timing issue)

### Tags
`gui-test-failure`, `{module-name}`, `needs-investigation`
```

### Batch Fix Process

| Frequency | Trigger | Action |
|-----------|---------|--------|
| Daily | 5+ similar issues | Batch fix session |
| Weekly | Any accumulated issues | Weekly fix sprint |
| Per Milestone | All open issues | Pre-milestone cleanup |

### Success Metrics

- Mean Time To Resolution (MTTR) < 2 business days
- Issue accumulation < 10 at any time
- Zero critical failures blocking CI

---

## Quality Gates

### QG-001: TRUST 5 Framework Compliance

- **Tested**: 85%+ code coverage (TDD for all new code)
- **Readable**: English code comments, XML documentation on public APIs
- **Unified**: Consistent C# coding style (EditorConfig), JSON schema for scenarios
- **Secured**: No arbitrary code execution in scenarios, input validation on all JSON
- **Trackable**: Git-tracked with conventional commits, SPEC-GUITOOLS-001 traceability tags

### QG-002: Functional Quality

- All test scenarios execute without manual intervention
- Zero false positives (tests should not pass when application is broken)
- Zero false negatives (tests should not fail when application is correct)
- Screenshot captured on every failure

### QG-003: Integration Readiness

- GuiTestRunner successfully automates GUI.Application
- GuiTestRunner successfully automates ParameterExtractor.Wpf
- CI workflow executes tests on GitHub Actions
- Test results reported in CI logs

---

## Traceability

### Parent Documents

- **SPEC-TOOLS-001**: Development Tools Requirements (target applications: GUI.Application, ParameterExtractor.Wpf)
- **SPEC-SDK-001**: Host SDK API (IDetectorClient integration tested via GUI)
- **X-ray_Detector_Optimal_Project_Plan.md**: Phase 2-3 quality assurance

### Configuration References

- **detector_config.yaml**: Test data for configuration scenarios
- **Serilog Configuration**: Log format expected by LogVerifier

### Child Documents

- Test scenarios (`TestScenarios/*.json`) define specific test cases
- CI workflow (`.github/workflows/gui-test.yml`) defines automation

---

## Acceptance Criteria

### AC-GUITOOLS-001: Smoke Test Execution

**GIVEN**: GuiTestRunner built and GUI.Application available
**WHEN**: `GuiTestRunner.exe GUI.Application.exe SmokeTest.json` is executed
**THEN**: Application launches, all steps execute, runner exits with code 0
**AND**: Log file contains expected completion markers

---

### AC-GUITOOLS-002: Element Click Automation

**GIVEN**: GUI.Application running with "StartButton" (AutomationId)
**WHEN**: Test scenario step `{"action": "Click", "target": {"automationId": "StartButton"}}` executes
**THEN**: Button is clicked and application state changes to "Acquiring"
**AND**: Log contains "Acquisition started" message

---

### AC-GUITOOLS-003: Log Pattern Verification

**GIVEN**: GUI.Application writing to `logs/gui_<date>.log`
**WHEN**: Test scenario step with `logCheck: {"contains": "Frame received"}` executes
**THEN**: LogVerifier tails log file and detects pattern within timeout
**AND**: Test proceeds to next step

---

### AC-GUITOOLS-004: Failure Screenshot Capture

**GIVEN**: Test scenario with failing assertion
**WHEN**: Assertion fails
**THEN**: Screenshot captured to `screenshots/failure_<timestamp>.png`
**AND**: Test report includes screenshot path
**AND**: Runner exits with code 1

---

### AC-GUITOOLS-005: CI Workflow Execution

**GIVEN**: Pull request with GUI code changes
**WHEN**: GitHub Actions workflow triggers
**THEN**: GUI.Application builds successfully
**AND**: GuiTestRunner executes all smoke scenarios
**AND**: Test results uploaded as artifacts
**AND**: Workflow passes if all tests pass

---

### AC-GUITOOLS-006: Tag Filtering

**GIVEN**: Test scenarios with tags `["smoke"]`, `["regression"]`
**WHEN**: `GuiTestRunner.exe --tags smoke` is executed
**THEN**: Only scenarios with "smoke" tag execute
**AND**: Report includes executed scenario count

---

### AC-GUITOOLS-007: Scenario List

**GIVEN**: Multiple test scenarios in `TestScenarios/` directory
**WHEN**: `GuiTestRunner.exe --list-scenarios` is executed
**THEN**: All scenarios listed with name, description, tags
**AND**: No applications launched
**AND**: Exit code 0

---

### AC-GUITOOLS-008: Element Retry Logic

**GIVEN**: Application with delayed element appearance (2s delay)
**WHEN**: Test scenario attempts to click element immediately
**THEN**: FlaUI wrapper retries with exponential backoff
**AND**: Element found and clicked successfully
**AND**: No test failure

---

## Dependencies

- **FlaUI.UIA3** 4.0+ NuGet package for UI automation
- **Serilog** (target app dependency) for log format parsing
- **GitHub Actions** (CI infrastructure) for automated testing
- **Target applications** (GUI.Application, ParameterExtractor.Wpf) must be instrumented with Serilog

---

## Risks

### R-GUITOOLS-001: UI Automation Timing Variations

**Risk**: Timing-dependent tests may fail intermittently due to UI delays.
**Probability**: Medium (mitigated). **Impact**: Low (structured process).
**Mitigation**:
- **Timeout margins**: x2 allocation for all timing-sensitive operations (10s element find, 20s log match)
- **Exponential backoff retry**: Automatic retry with increasing intervals (100ms → 5s max)
- **Structured debugging**: Log-based GitHub Issue registration → batch fix sessions
- **Screenshot capture**: Automatic capture on failure for rapid diagnosis

### R-GUITOOLS-002: CI Environment Limitations

**Risk**: GitHub Actions Windows runner may have display limitations.
**Probability**: Low. **Impact**: Medium.
**Mitigation**: Use `windows-latest` runner with desktop support. Fallback to self-hosted runner if needed.

### R-GUITOOLS-003: Log File Access

**Risk**: Concurrent test runs may conflict on log file access.
**Probability**: Medium. **Impact**: Low.
**Mitigation**: Unique log file per test run. File share mode `Read` for LogVerifier.

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-18 | ABYZ-Lab Agent (analyst) | Initial SPEC creation for GUI test automation tool |

---

## Review Record

- Status: Draft
- Pending: Review by manager-quality
- Pending: User approval

---

**END OF SPEC**
