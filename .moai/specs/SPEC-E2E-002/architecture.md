# SPEC-E2E-002: E2E Debug and Logging System — Architecture

Version: 1.0
Author: architect (MoAI team)
Date: 2026-03-13

---

## 1. Problem Statement

The existing E2E infrastructure has three gaps:

1. **No structured logs** — `Trace.WriteLine` outputs are invisible to xUnit's per-test output mechanism because `AppFixture` is an `ICollectionFixture` and xUnit cannot inject `ITestOutputHelper` into collection fixtures.
2. **No automatic screenshot on failure** — `ScreenshotHelper.CaptureOnFailure` exists but is never called automatically; tests must call it explicitly.
3. **No UIA tree dump on timeout** — When `WaitHelper.WaitUntilAsync` times out the test silently fails with no diagnostic of what UIA elements were present.

---

## 2. Constraint Analysis

### xUnit ITestOutputHelper injection limitation

xUnit injects `ITestOutputHelper` only into test class constructors — never into `ICollectionFixture` implementations. `AppFixture` is the single shared instance for the entire `"E2E"` collection and **cannot** receive `ITestOutputHelper` directly.

**Viable approaches (ordered by preference):**

| Approach | Description | Verdict |
|---|---|---|
| A. Shared log buffer | `AppFixture` holds `List<string>`; each test flushes to its own `ITestOutputHelper` in `BeginTest()`/`EndTest()` | **SELECTED** — simple, no extra dependencies |
| B. `IMessageSink` injection | xUnit 2.x allows `IMessageSink` in collection fixture constructor via `Xunit.Abstractions` | **Fallback** — constructor injection via `IMessageSink` works but logs go to runner console, not per-test output |
| C. File-based logging | Write to `TestResults/e2e-debug.log`; tests read/display on demand | **Supplement** — use in addition to A, not instead |
| D. `System.Diagnostics.Trace` | `TraceListener` forwarding to file | **Already present** — extend, not replace |

**Decision: Use approach A as primary + C as persistent backup.**

---

## 3. Component Architecture

### 3.1 E2ELogger

**Location:** `Infrastructure/E2ELogger.cs`

**Responsibilities:**
- Single shared instance created in `AppFixture` constructor
- Thread-safe in-memory ring buffer (bounded to 1000 entries to avoid memory growth across long test suites)
- Simultaneously writes to `System.Diagnostics.Trace` and the ring buffer
- Optional file sink: `TestResults/e2e-debug-{timestamp}.log`
- Provides structured log methods: `Info`, `Click`, `Find`, `Warn`, `Error`
- `BeginTest(testName)` — marks current test context, resets per-test buffer
- `EndTest(ITestOutputHelper)` — flushes per-test buffer to xUnit output, then clears it

**Instantiation chain:**
```
AppFixture constructor
  └── E2ELogger logger = new E2ELogger(logDir: "TestResults/Logs")

E2ETestBase constructor
  └── receives AppFixture, exposes protected E2ELogger Logger => Fixture.Logger

Test method (IAsyncLifetime)
  └── InitializeAsync: Logger.BeginTest(testName)
  └── DisposeAsync: Logger.EndTest(OutputHelper)
```

**Log entry format:**
```
[HH:mm:ss.fff] [LEVEL] [TestName] message
```

**xUnit flush mechanism:**
`E2ETestBase` implements `IAsyncLifetime`. In `DisposeAsync()` it calls `Logger.EndTest(OutputHelper)` which iterates the per-test buffer and calls `OutputHelper.WriteLine()` for each entry. This ensures all log lines appear in the xUnit test output under the correct test name.

**Design note on `ITestOutputHelper` access:**
`E2ETestBase` subclasses receive `ITestOutputHelper` via constructor injection (standard xUnit). The base class stores it as `protected readonly ITestOutputHelper OutputHelper`. This is possible because `E2ETestBase` is a test class (not a fixture), so injection works normally.

### 3.2 Enhanced E2ETestBase

**Location:** `Infrastructure/E2ETestBase.cs` (modify existing)

Additions:
- Constructor: also accept `ITestOutputHelper output`
- Implement `IAsyncLifetime`
- `InitializeAsync`: call `Logger.BeginTest(GetType().Name + "." + currentTest)`
- `DisposeAsync`: call `Logger.EndTest(OutputHelper)` + conditional `CaptureOnFailure`
- Protected property: `E2ELogger Logger => Fixture.Logger`

**Test failure detection in DisposeAsync:**
xUnit 2.x does not expose the current test result to the fixture or base class. The recommended pattern is to use a `bool _testPassed` flag:
- Set `_testPassed = false` in constructor
- Set `_testPassed = true` at end of each test method body (requires test author discipline), **OR**
- Use a `try/catch` wrapper method `RunTest(Action)` in the base class — the wrapper sets the flag on success

**Preferred approach: `RecordException` pattern**
`E2ETestBase` exposes:
```csharp
protected void RecordTestPassed() => _testPassed = true;
```
Tests call `RecordTestPassed()` as their last line. `DisposeAsync` reads `_testPassed` to decide whether to capture a screenshot. This is explicit but low-overhead and requires no framework changes.

**Alternative (zero-boilerplate):** Use `ITestContextAccessor` (available in xUnit v3) — but current project uses xUnit 2.9.3, so not applicable.

### 3.3 ScreenshotHelper Enhancement

**Location:** `Infrastructure/ScreenshotHelper.cs` (modify existing)

Current behavior: Static helper, called manually.

Enhancement: Add an overload that accepts `E2ELogger` to log the screenshot path:
```
CaptureOnFailure(testName, logger, window) → logs path via logger.Info(...)
```

No structural change to the static class needed. The integration point is `E2ETestBase.DisposeAsync`.

**Screenshot path:** `TestResults/Screenshots/{TestName}_{yyyyMMdd_HHmmss}.png`
Already implemented in current `ScreenshotHelper`; no path change needed.

### 3.4 WaitHelper Enhancement — UIA Tree Dump on Timeout

**Location:** `Infrastructure/WaitHelper.cs` (modify existing)

Add overload with logger parameter:
```csharp
WaitUntilAsync(condition, logger, context, timeoutMs, pollIntervalMs)
```

When timeout is reached:
1. Log: `[WARN] WaitUntil timed out after {ms}ms — context: {context}`
2. If an `AutomationElement` root is provided (e.g., `MainWindow`), call `DumpAutomationTree(root, logger)`

**DumpAutomationTree design:**
- Use `FindAllDescendants()` from FlaUI (returns flat list of all descendants)
- Limit to depth 4 (avoid dumping the entire OS tree)
- Format: `{indent}{ControlType} [{AutomationId}] "{Name}"`
- Output via `logger.Warn(...)` for each line
- Cap at 100 elements to avoid flooding logs

**Depth-limited traversal approach:**
Since FlaUI 4.0.0 `FindAllDescendants()` does not natively support depth limits, implement a recursive helper `DumpChildren(element, logger, depth, maxDepth)` that calls `FindAllChildren()` per level.

**Location of dump helper:** Static method inside a new `UiaTreeDumper` static class in `Infrastructure/UiaTreeDumper.cs` to keep `WaitHelper` focused.

### 3.5 Timing Instrumentation

**Location:** Integrated into `E2ELogger`

`E2ELogger` internally uses a `Stopwatch` started at construction (covers fixture lifetime). Each log entry records elapsed time from fixture start (not wall clock) for precise sequencing.

**AppFixture milestones** — logged via `logger.Info(...)` with timing already embedded in format:
- `[AppFixture] Starting GUI.Application: {path}`  → already present as Trace, move to logger
- `[AppFixture] Process started. PID={pid}` → move to logger
- `[AppFixture] Main window found after {elapsed}s` → move to logger
- `[AppFixture] Menu warmup complete. E2E fixture ready.` → move to logger

**Per-test timing:** `BeginTest` records `_testStartElapsed`. `EndTest` logs `Test duration: {ms}ms`.

No separate `StopwatchStep` class needed — the timestamp in every log line already provides a step-by-step timeline.

### 3.6 PowerShell Run Script

**Location:** `scripts/run-e2e-tests.ps1`

**Responsibilities:**
- Force `CI=""` (empty string, not absent) so `RequiresDesktopFactAttribute.IsInteractiveDesktop()` returns true
- Run `dotnet test` with `--logger "console;verbosity=detailed"` and `--logger "trx;LogFileName=e2e-results.trx"`
- Pipe output to a tee (console + `TestResults/run-{timestamp}.log`)
- After test run: print summary (Passed / Failed / Skipped counts) parsed from trx or console output
- Optionally open `TestResults/Logs/` in explorer

**CI detection note:**
Check how `RequiresDesktopFactAttribute.IsInteractiveDesktop()` is implemented — if it checks `Environment.GetEnvironmentVariable("CI")`, then `$env:CI = ""` in the script is sufficient.

---

## 4. File Map

| File | Action | Description |
|---|---|---|
| `Infrastructure/E2ELogger.cs` | **NEW** | Shared logger with ring buffer, file sink, flush to ITestOutputHelper |
| `Infrastructure/UiaTreeDumper.cs` | **NEW** | Depth-limited UIA tree dump utility |
| `Infrastructure/E2ETestBase.cs` | **MODIFY** | Add IAsyncLifetime, ITestOutputHelper, Logger property, RecordTestPassed |
| `Infrastructure/AppFixture.cs` | **MODIFY** | Add `public E2ELogger Logger` property, replace Trace calls with Logger calls, add timing milestones |
| `Infrastructure/WaitHelper.cs` | **MODIFY** | Add overload accepting E2ELogger + root element for tree dump on timeout |
| `Infrastructure/ScreenshotHelper.cs` | **MODIFY** | Add E2ELogger overload; auto-log screenshot path |
| `scripts/run-e2e-tests.ps1` | **NEW** | PowerShell runner that sets desktop env, runs tests, prints summary |

**No new NuGet packages required.** All components use existing dependencies (FlaUI.Core, xUnit, System.Diagnostics).

---

## 5. Data Flow Diagram

```
AppFixture (ICollectionFixture)
  └── E2ELogger (shared instance)
        ├── RingBuffer[1000] (per-fixture lifetime)
        ├── PerTestBuffer[unbounded] (reset each BeginTest)
        ├── FileStream → TestResults/Logs/e2e-debug-{timestamp}.log
        └── System.Diagnostics.Trace

E2ETestBase (test class)
  ├── ITestOutputHelper (xUnit injection)
  ├── InitializeAsync → Logger.BeginTest(name)
  └── DisposeAsync
        ├── if (!_testPassed) → ScreenshotHelper.CaptureOnFailure(...)
        └── Logger.EndTest(OutputHelper) → flush per-test buffer → xUnit output

Test Method
  ├── Logger.Info/Click/Find/Warn/Error(...)
  ├── WaitHelper.WaitUntilAsync(cond, Logger, "context", MainWindow, ...)
  └── RecordTestPassed()  ← last line of passing test
```

---

## 6. Interface Contracts

### E2ELogger (public API surface)

```
// Construction (by AppFixture)
E2ELogger(string logDirectory)

// Test lifecycle (by E2ETestBase)
void BeginTest(string testName)
void EndTest(ITestOutputHelper output)

// Logging methods
void Info(string message)
void Click(string elementDescription)
void Find(string elementDescription, bool found)
void Warn(string message)
void Error(string message, Exception? ex = null)

// Property
bool HasErrors { get; }  // used by DisposeAsync to decide screenshot
```

### UiaTreeDumper (internal API)

```
// Called by WaitHelper on timeout
static void Dump(AutomationElement root, E2ELogger logger, int maxDepth = 4, int maxElements = 100)
```

### WaitHelper (new overload)

```
static Task<bool> WaitUntilAsync(
    Func<bool> condition,
    E2ELogger? logger = null,
    string? context = null,
    AutomationElement? dumpRootOnTimeout = null,
    int timeoutMs = 5000,
    int pollIntervalMs = 100)
```

### E2ETestBase additions

```
protected readonly ITestOutputHelper OutputHelper;
protected E2ELogger Logger => Fixture.Logger;
protected void RecordTestPassed();
```

---

## 7. Key Design Decisions

| Decision | Rationale |
|---|---|
| Ring buffer capped at 1000 | Prevents memory growth across 50+ tests in a long suite |
| Per-test buffer (unbounded) | Tests are bounded in duration; per-test logs reset on BeginTest |
| `RecordTestPassed()` pattern | xUnit 2.9.3 has no test result API; explicit flag is zero-dependency |
| UIA tree dump in separate class | Keeps WaitHelper focused on timing; dumper has its own size/depth constraints |
| File log as supplement | Survives process crashes and provides post-mortem even when xUnit output is truncated |
| No new NuGet packages | Reduces maintenance surface; FlaUI.Core already provides Capture and TreeWalker |
| PS1 script sets `CI=""` | xUnit runner in bash/CI sets CI=true which makes IsInteractiveDesktop return false |

---

## 8. Test Coverage Requirements

Components introduced by this SPEC must themselves be testable:

- `E2ELogger`: Unit tests — BeginTest/EndTest flush, ring buffer eviction, concurrent write safety
- `UiaTreeDumper`: Integration test with a mock `AutomationElement` tree (depth > maxDepth)
- `WaitHelper` new overload: Verify logger is called on timeout, not on success

These tests live in a `E2ETests.UnitTests` project or alongside existing infrastructure tests.

---

## 9. Risks and Mitigations

| Risk | Likelihood | Mitigation |
|---|---|---|
| `FindAllChildren()` hangs on unresponsive UIA tree | Low | Wrap dump in `Task.Run` with 2s timeout; if timeout, log "UIA dump timed out" |
| File log not flushed on crash | Medium | Use `AutoFlush = true` on `StreamWriter` |
| `RecordTestPassed()` forgotten by test authors | Medium | Document in test base class XML comment; add analyzer rule or checklist item |
| Ring buffer eviction loses early fixture logs | Low | Ring buffer covers fixture init; per-test buffer is separate and never evicted |
