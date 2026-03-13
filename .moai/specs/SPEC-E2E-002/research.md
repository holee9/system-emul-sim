# SPEC-E2E-002: E2E Infrastructure Analysis & Logging Gap Assessment

## Research Summary

This document provides a comprehensive analysis of the E2E testing infrastructure, FlaUI execution patterns, and identifies critical logging gaps that impact test reliability and debuggability.

**Status**: Complete
**Date**: 2026-03-14
**Researcher**: team-researcher
**Files Analyzed**: 15 core files + supporting infrastructure

---

## 1. E2E Test Infrastructure Architecture

### 1.1 Fixture Lifecycle (AppFixture.cs)

The `AppFixture` class manages the GUI.Application process lifecycle and UIAutomation connection for all E2E tests.

**Initialization Sequence** (InitializeAsync, lines 50-113):

1. **Desktop Detection** (line 52):
   ```csharp
   IsDesktopAvailable = RequiresDesktopFactAttribute.IsInteractiveDesktop();
   if (!IsDesktopAvailable) {
       Trace.WriteLine("[AppFixture] Non-interactive session detected...");
       return; // SKIP - tests will auto-skip with [RequiresDesktopFact]
   }
   ```
   - Checks: `CI == "true"` or `GITHUB_ACTIONS == "true"` or `!Environment.UserInteractive`
   - In non-interactive bash (Claude Code): IsDesktopAvailable = false, early return

2. **Process Launch** (lines 61-76):
   ```csharp
   var exePath = GetAppExePath(); // Finds Release or Debug GUI.Application.exe
   startInfo.Environment["XRAY_E2E_MODE"] = "true"; // .NET 8: use Environment, not EnvironmentVariables
   _appProcess = Process.Start(startInfo);
   _automation = new UIA3Automation(); // UIA3 automation instance
   _flaUiApp = FlaUI.Core.Application.Attach(_appProcess);
   ```

3. **Main Window Wait Loop** (lines 78-112):
   - Timeout: 30 seconds
   - Poll interval: 500ms
   - Calls: `_flaUiApp.GetMainWindow(_automation)` → returns null if window not in UIAutomation tree
   - **Current Issue**: In non-interactive sessions, WPF window never registers with UIAutomation
   - **Logging**: Trace.WriteLine on success, catch-all swallows exceptions

4. **Menu Warmup** (lines 93-104, WarmupSingleMenuAsync method):
   ```csharp
   await WarmupSingleMenuAsync("File", "MenuFileExit");
   await WarmupSingleMenuAsync("Help", "MenuHelpTopics");
   ```

   **Why This Exists**:
   - WPF MenuItem AutomationPeers register at Background Dispatcher priority
   - On first menu expansion, peers can take up to 26-40 seconds to register
   - If menu is collapsed before peers register, timer resets

   **Warmup Algorithm** (lines 121-160):
   - Click menu item to expand (line 136)
   - Poll every 500ms for target AutomationId in expanded menu (line 145)
   - Maximum 90 seconds wait (line 140)
   - Access peer properties to trigger full initialization (lines 149-150)
   - Escape to collapse menu (line 156)
   - Swallow all exceptions (line 159) - best-effort, tests retry anyway

**Key Properties**:
```csharp
public AutomationElement? MainWindow { get; private set; }
public bool IsDesktopAvailable { get; private set; } = true;
public UIA3Automation Automation => _automation ?? throw...
```

**Cleanup** (Dispose, lines 168-186):
- Kill process tree with 5000ms wait
- Dispose FlaUI automation and application
- Handle InvalidOperationException if process already exited

### 1.2 Test Base Class & Collection Definition

**E2ETestBase** (lines 1-26):
```csharp
[Collection("E2E")]
public abstract class E2ETestBase {
    protected readonly AppFixture Fixture;
    protected AutomationElement MainWindow =>
        Fixture.IsDesktopAvailable
            ? Fixture.MainWindow ?? throw InvalidOperationException(...)
            : throw InvalidOperationException("E2E tests require interactive desktop session");
}

[CollectionDefinition("E2E")]
public sealed class E2ECollection : ICollectionFixture<AppFixture> { }
```

**Implications**:
- Single AppFixture shared across all tests in [Collection("E2E")]
- Serial execution enforced: `--max-parallel-threads 1` in csproj
- IsDesktopAvailable fast-fail prevents cascade failures
- MainWindow throws if accessed in non-interactive session

### 1.3 Helper Utilities

**WaitHelper.cs** (23 lines):
```csharp
public static async Task<bool> WaitUntilAsync(
    Func<bool> condition,
    int timeoutMs = 5000,
    int pollIntervalMs = 100)
{
    var sw = Stopwatch.StartNew();
    while (sw.ElapsedMilliseconds < timeoutMs) {
        if (condition()) return true;
        await Task.Delay(pollIntervalMs);
    }
    return false;
}

public static Task DelayAsync(int ms = 500) => Task.Delay(ms);
```

**Issues**:
- No condition description parameter → logs show nothing about what was waiting for
- No logging of attempts or timeouts
- No exception capture/propagation
- Timeout returns false silently (tests must check return value)
- 100ms default polling can miss transient state changes

**ScreenshotHelper.cs** (34 lines):
```csharp
public static void CaptureOnFailure(string testName, AutomationElement? window = null)
{
    var capture = window != null
        ? Capture.Element(window)
        : Capture.Screen();
    capture.ToFile($"TestResults/Screenshots/{SanitizeName(testName)}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
}
```

**Issues**:
- Manual invocation only (NOT called automatically on exception)
- NO integration with xUnit test failure hooks
- Screenshots taken AFTER test assertion fails (may miss state at failure moment)
- No metadata about what test was running, which UIAutomation element, timing
- File path hardcoded (no test-context awareness)

**RetryFactAttribute.cs** (79 lines):
```csharp
[AttributeUsage(AttributeTargets.Method)]
public sealed class RetryFactAttribute : FactAttribute {
    public int MaxRetries { get; set; } = 2;
    public int RetryDelayMs { get; set; } = 500;
}

public static class RetryHelper {
    public static async Task RunWithRetryAsync(
        Func<Task> action,
        int maxRetries = 2,
        int retryDelayMs = 500)
    {
        Exception? lastException = null;
        for (int attempt = 0; attempt <= maxRetries; attempt++) {
            try {
                await action();
                return;
            } catch (Exception ex) when (attempt < maxRetries) {
                lastException = ex;
                await Task.Delay(retryDelayMs);
            }
            catch (Exception ex) {
                lastException = ex;
            }
        }
        if (lastException != null) throw lastException;
    }
}
```

**Issues**:
- Attribute marked as retryable but doesn't perform retries (comment line 9-10)
- Must manually call RetryHelper.RunWithRetryAsync in test body
- Zero logging of retry attempts or delays
- Silent swallow of intermediate exceptions
- No backoff strategy

**RequiresDesktopFactAttribute.cs** (26 lines):
```csharp
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequiresDesktopFactAttribute : FactAttribute {
    public RequiresDesktopFactAttribute() {
        if (!IsInteractiveDesktop())
            Skip = "Requires interactive desktop session...";
    }

    internal static bool IsInteractiveDesktop() {
        if (Environment.GetEnvironmentVariable("CI") == "true") return false;
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true") return false;
        return Environment.UserInteractive;
    }
}
```

**Implications**:
- Tests auto-skip in CI (GitHub Actions)
- Tests auto-skip in non-interactive shells (bash, cmd with non-interactive mode)
- Skip message appears in xUnit output (test is marked as skipped, not failed)
- Used on every test method ([RequiresDesktopFact] decoration)

---

## 2. Test Coverage Analysis

### 2.1 Feature Test Files

**AboutDialogE2ETests.cs** (110 lines):
- 3 tests, all [RequiresDesktopFact]
- Pattern 1: Use Win32 FindWindow() to locate modal dialog by title
- Pattern 2: Wrap HWND with `Fixture.Automation.FromHandle(hwnd)`
- Pattern 3: Use PageObject pattern (AboutDialogPage) for element interaction
- Uses access keys for keyboard input (bypasses DPI-scaling issues with mouse clicks)
- Manual retry loop for menu item discovery (up to 200 attempts × 200ms)

**CoreFlowE2ETests.cs** (74 lines):
- 5 tests covering menu items, buttons, tabs
- TabControl navigation via `tabs[i].AsTabItem().Select()`
- Button/MenuItem location via ByAutomationId() or ByName()
- No behavioral testing (only structure/existence checks)

**HelpSystemE2ETests.cs** (223 lines):
- 8 tests total: 4 structural (AutomationId existence) + 4 behavioral (actually open window)
- **Root Cause Analysis** (lines 10-28): Explains why original Help bug was undetected
  - Original tests: 4 tests, ZERO behavioral coverage
  - Missing: Actual click to open HelpWindow, F1 key test, TreeView visibility check
  - AppFixture.WarmupSingleMenuAsync only primed MenuHelpAbout, not MenuHelpTopics
- **New Behavioral Tests** (lines 104-221):
  - HelpTopicsMenuItem_OpensHelpWindow(): Click menu, use Win32 FindWindow("도움말")
  - F1Key_OpensHelpWindow(): Press F1, verify window title
  - HelpWindow_HasTopicTreeVisible(): Open window, check TreeView present in UIAutomation tree

**ParameterExtractionE2ETests.cs** (92 lines):
- 2 tests: Tab switching and button visibility
- Uses Assert.True/Equal (xUnit assertions, not FluentAssertions)
- Tab selection via `tabs[index].AsTabItem().Select()`
- Button location with exception if not found: `?? throw new Exception(...)`

**AppLaunchTests.cs** (70 lines):
- 4 smoke tests: app launch, version display, tab accessibility, menu existence
- MainWindowPage PageObject pattern for version extraction
- No exception handling; relies on test framework assertions

### 2.2 PageObject Classes

**HelpWindowPage.cs** (30 lines):
```csharp
public sealed class HelpWindowPage(AutomationElement window) {
    public string GetTitle() => window.Name;
    public bool IsTopicTreeVisible() {
        var tree = window.FindFirstDescendant(
            cf => cf.ByControlType(ControlType.Tree));
        return tree != null;
    }
    public void Close() {
        var closeBtn = window.FindFirstDescendant(
            cf => cf.ByControlType(ControlType.Button));
        closeBtn?.AsButton().Invoke();
    }
}
```

**AboutDialogPage.cs** (28 lines):
```csharp
public sealed class AboutDialogPage(AutomationElement dialog) {
    public string GetVersion() {
        var el = dialog.FindFirstDescendant(
            cf => cf.ByAutomationId("AboutVersionText"));
        return el?.Name ?? string.Empty;
    }
    public void ClickCopyToClipboard() { ... }
    public void Close() {
        var closeBtn = dialog.FindFirstDescendant(
            cf => cf.ByName("닫기")); // Korean: "Close"
        closeBtn?.AsButton().Invoke();
    }
}
```

**Issues**:
- PageObjects use `??` null coalescing but don't validate element found
- GetVersion() returns empty string if element not found (silent failure)
- Close() may fail silently if button not found
- No logging or diagnostics if element search fails

### 2.3 Project Configuration (GUI.Application.E2ETests.csproj)

```xml
<TargetFramework>net8.0-windows</TargetFramework>
<LangVersion>12.0</LangVersion>
<Nullable>enable</Nullable>
<IsTestProject>true</IsTestProject>
<TestRunnerAdditionalArguments>--max-parallel-threads 1</TestRunnerAdditionalArguments>

<PackageReference Include="FlaUI.UIA3" Version="4.0.0" />
<PackageReference Include="FlaUI.Core" Version="4.0.0" />
<PackageReference Include="FluentAssertions" Version="7.0.0" />
<PackageReference Include="xunit" Version="2.9.3" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
```

**Key Points**:
- Serial execution enforced at runner level
- FlaUI 4.0.0 (latest, breaking changes from 3.x)
- No custom logging integrations
- xunit.runner.visualstudio enables IDE integration but not test output capture

---

## 3. FlaUI Execution Patterns & Capabilities

### 3.1 Element Location Strategies

**ControlType Tree Traversal**:
```csharp
var menuBar = MainWindow.FindFirstDescendant(
    cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Menu));
```
- Returns first match in subtree
- No limit on search depth
- Used for root elements (MenuBar, TabControl, etc.)

**Name-Based Search**:
```csharp
var helpMenu = menuBar!.FindFirstChild(
    cf => cf.ByName("Help"));
```
- Searches immediate children only
- Case-sensitive exact match
- Used when name is known (menu items, buttons)

**AutomationId Search**:
```csharp
var menuItem = helpMenu.FindFirstChild(
    cf => cf.ByAutomationId("MenuHelpAbout"));
```
- Most reliable: developer-set identifier
- Used in tests as primary lookup method
- AutomationId set in XAML: `AutomationProperties.AutomationId="MenuHelpAbout"`

**Array of Children**:
```csharp
var tabs = tabControl!.FindAllChildren(
    cf => cf.ByControlType(ControlType.TabItem));
tabs[1].AsTabItem().Select();
```
- Returns all children as array (not recursive)
- Used for indexed access (tab switching)
- Length check verifies expected structure

**Win32 Bypass**:
```csharp
var hwnd = FindWindow(null, "About X-ray Detector GUI");
var dialog = Fixture.Automation.FromHandle(hwnd);
```
- UIAutomation tree doesn't include modal dialogs (ownership issue)
- Win32 FindWindow searches all top-level windows by exact title
- Used for modal/owned windows that aren't UIAutomation children

### 3.2 Interaction Methods

**Menu Invocation**:
```csharp
helpMenu!.AsMenuItem().Click(); // expand menu popup
FlaUI.Core.Input.Keyboard.Type('a'); // send access key
```
- `.AsMenuItem()` casts to MenuItem automation interface
- `.Click()` sends click event (position-sensitive on scaled displays)
- `Keyboard.Type()` safer than mouse for DPI-scaled screens
- Access keys require menu popup to be open and focused

**Button Invocation**:
```csharp
closeBtn?.AsButton().Invoke(); // programmatic invocation
var btn = MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("BtnStart"));
```
- `.Invoke()` triggers Click event handler without mouse simulation
- More reliable than `.Click()` for buttons

**Tab Selection**:
```csharp
tabs[3].AsTabItem().Select(); // selects tab
```
- `.AsTabItem()` casts to TabItem automation interface
- `.Select()` activates tab (changes tab and updates content)

**Keyboard Input**:
```csharp
FlaUI.Core.Input.Keyboard.Press(VirtualKeyShort.F1);
FlaUI.Core.Input.Keyboard.Press(VirtualKeyShort.ESCAPE);
FlaUI.Core.Input.Keyboard.Type('a');
```
- `Press()` for key codes (F1, ESCAPE, etc.)
- `Type()` for character input
- Requires window focus

**SetForeground**:
```csharp
MainWindow.SetForeground(); // brings window to front and focuses
```
- Used before keyboard input to ensure focus
- Necessary in test environments where focus may be lost

### 3.3 Timing & Polling Patterns

**Manual Retry Loop**:
```csharp
AutomationElement? aboutMenuItem = null;
for (int attempt = 0; attempt < 200; attempt++) {
    Thread.Sleep(200);
    aboutMenuItem = helpMenu.FindFirstChild(cf => cf.ByAutomationId("MenuHelpAbout"));
    if (aboutMenuItem != null) break;
}
aboutMenuItem.Should().NotBeNull("MenuHelpAbout AutomationId should exist");
```

**Issues with Current Approach**:
- 200 × 200ms = 40 seconds max wait (unbounded delay for large retries)
- Hardcoded delays (no exponential backoff, no timeout safety)
- No logging of attempts → invisible progress on timeout
- Break condition only checked after each delay (loses first 200ms)
- Exception on timeout only reaches assertion (no granular error context)

**WaitHelper Pattern**:
```csharp
await WaitHelper.WaitUntilAsync(() => {
    var hwnd = FindWindow(null, "About X-ray Detector GUI");
    return hwnd != IntPtr.Zero;
}, 12000); // 12 second timeout
```

**Issues**:
- Condition evaluated every 100ms (poll interval fixed)
- Timeout just returns false (tests must check return value)
- No logging of poll attempts
- No way to know which iteration succeeded (for performance analysis)

**Delay Pattern**:
```csharp
await WaitHelper.DelayAsync(300);
Thread.Sleep(500); // Also used in tests
```

**Issues**:
- Sleep vs. async delay inconsistently used
- Hard-coded delays (200ms, 300ms, 500ms, 2000ms) scattered in code
- No justification for delay values (seems empirical/tuned)
- May hide underlying synchronization issues

### 3.4 Error Scenarios

**Element Not Found**:
```csharp
var menuItem = helpMenu.FindFirstChild(cf => cf.ByAutomationId("MenuHelpAbout"));
if (menuItem == null) break; // silent on not found
// Later:
menuItem.Should().NotBeNull(); // fails at assertion
```

**Problem**: 40 seconds of retries, then assertion failure with no context about which iterations were attempted, what tree looked like, etc.

**Property Access Failure**:
```csharp
_ = target.AutomationId;
_ = target.Name;
```

**Problem**: Exceptions accessing properties (e.g., peer not fully initialized) are never caught or logged.

**Window Not Found**:
```csharp
var hwnd = FindWindow(null, "About X-ray Detector GUI");
if (hwnd == IntPtr.Zero) { /* timeout failure */ }
```

**Problem**: Zero visibility into why window wasn't found (process crash? title changed? timing issue?).

---

## 4. Logging Architecture & Current State

### 4.1 Trace.WriteLine Usage in Fixtures

**AppFixture.cs Only** (5 trace points):
```csharp
Line 55-57:  [AppFixture] Non-interactive session detected. Skipping WPF process launch.
Line 62:     [AppFixture] Starting GUI.Application: {exePath}
Line 73:     [AppFixture] Process started. PID={_appProcess.Id}
Line 88:     [AppFixture] Main window found after {sw.Elapsed.TotalSeconds:F1}s
Line 102:    [AppFixture] Menu warmup complete. E2E fixture ready.
```

**Zero Trace Points in**:
- Any test method (E2ETestBase subclasses)
- Helper utilities (WaitHelper, ScreenshotHelper, RetryHelper)
- PageObjects (HelpWindowPage, AboutDialogPage, etc.)

### 4.2 Serilog in Application (App.xaml.cs)

```csharp
protected override async void OnStartup(StartupEventArgs e) {
    base.OnStartup(e);

    LoggingBootstrap.Initialize();
    Log.ForContext("SourceContext", LogCategories.App)
        .Information("Application starting up");

    // ...
    isE2EMode = Environment.GetEnvironmentVariable("XRAY_E2E_MODE") == "true";
    if (!isE2EMode) {
        await _simulatedClient.ConnectAsync("sim", 0);
        await _simulatedClient.StartAcquisitionAsync();
        Log.ForContext(...).Information("Simulation auto-start completed");
    } else {
        Log.ForContext(...).Information("E2E mode: simulation auto-start skipped");
    }

    // Exception handlers also log
    AppDomain.CurrentDomain.UnhandledException += ...
    DispatcherUnhandledException += ...
}
```

**Issues**:
- Serilog logs go to file/console (external to test runner)
- xUnit test runner doesn't capture these logs
- No correlation between test output and application logs
- E2E mode prevents simulation start (good for Dispatcher) but no trace of why
- Exceptions in app are logged to Serilog but not reflected in test failure context

### 4.3 Screenshot Capture (ScreenshotHelper)

```csharp
public static void CaptureOnFailure(string testName,
    AutomationElement? window = null)
{
    var dir = Path.Combine("TestResults", "Screenshots");
    Directory.CreateDirectory(dir);
    var fileName = Path.Combine(dir,
        $"{SanitizeName(testName)}_{DateTime.Now:yyyyMMdd_HHmmss}.png");

    var capture = window != null
        ? Capture.Element(window)
        : Capture.Screen();
    capture.ToFile(fileName);
}
```

**Current Usage**: ZERO calls in codebase (not used)

**Issues if Used**:
- Manual invocation only (must call in test catch block)
- No automatic xUnit failure hook integration
- Screenshot taken AFTER assertion fails (may show stale state)
- Filename includes test name + timestamp (no correlation to test context)
- Path is "TestResults/Screenshots" relative (may be wrong directory)
- Exception swallowed if screenshot fails (line 25-28)

---

## 5. Logging Gaps & Missing Capabilities

### 5.1 Gap Analysis: Fixture Initialization

**What's Missing**:
1. **AppFixture.GetAppExePath()**: No logging of search attempts, found vs. missing paths
2. **Process.Start()**: No logging of StartInfo details (environment, arguments)
3. **GetMainWindow() wait loop**: No attempt counter, no timing telemetry
4. **Menu warmup**: No per-menu statistics (time to find target, attempts, failures)
5. **Exception handling**: Catch-all with no logging (line 107)

**Impact**: On timeout, impossible to know:
- Was process spawned?
- Did UIAutomation initialize?
- How many polls before timeout?
- Which menu item was last found?
- What was state when timeout occurred?

### 5.2 Gap Analysis: Test Methods

**What's Missing**:
1. **No test context logging**: Test name, class, method, parameters not logged
2. **No element search logging**: Which AutomationId was searched, where, how long it took
3. **No poll attempt logging**: For manual retry loops (200 attempts × 200ms) → no visibility
4. **No interaction logging**: What button/menu was clicked, when, result
5. **No exception logging**: Assertion failures don't show what was searched for or why

**Example - Current HelpWindowPage Usage**:
```csharp
var topicsItem = helpMenu.FindFirstChild(
    cf => cf.ByAutomationId("MenuHelpTopics"));
topicsItem.Should().NotBeNull("MenuHelpTopics AutomationId must exist");
```

**If This Fails**:
- Test output: "Expected <null> not to be null"
- What we don't know:
  - How many child elements were found?
  - What AutomationIds exist?
  - What was the tree structure?
  - How long did search take?
  - Was helpMenu actually expanded?

### 5.3 Gap Analysis: Failure Context

**No ITestOutputHelper Integration**:
```csharp
// NOT USED ANYWHERE:
public ConstructorWithTestOutput(ITestOutputHelper output) { }
```

**xUnit Pattern Not Implemented**:
```csharp
// SHOULD BE IN TESTS:
public SomeE2ETest(AppFixture fixture, ITestOutputHelper output) {
    _fixture = fixture;
    _output = output;
}

[Fact]
public void Test() {
    _output.WriteLine("About to search for element...");
    var element = MainWindow.FindFirstDescendant(...);
    _output.WriteLine($"Found: {element?.Name ?? "null"}");
}
```

**Result**: No per-test output capture; logs are either global (Serilog file) or system (Trace).

### 5.4 Gap Analysis: Failure Diagnostics

**Missing on UIAutomation Failure**:
1. **Tree dump**: What elements exist in failed element's subtree?
2. **Property snapshot**: AutomationId, Name, ControlType, IsEnabled, IsVisible
3. **Parent context**: What's the immediate parent? Siblings?
4. **Timing data**: How long was wait? How many attempts?
5. **Application state**: What was app doing? Logs from that period?

**Example Output We Cannot Generate**:
```
[14:23:45.123] TEST: HelpSystemE2ETests.HelpTopicsMenuItem_OpensHelpWindow
[14:23:45.456] AppFixture: Main window found after 0.5s
[14:23:46.012] AppFixture: Menu warmup (File) completed in 2.3s
[14:23:46.234] AppFixture: Menu warmup (Help) completed in 1.9s
[14:23:47.001] TEST: Searching for AutomationId="MenuHelpTopics" in Help menu
[14:23:47.001] SEARCH: Attempt 1/200, polling every 200ms, timeout 40s
[14:23:47.201] SEARCH: Attempt 2 - not found
... (invisible) ...
[14:23:49.001] SEARCH: Attempt 11 - found MenuHelpTopics (Name="도움말 항목")
[14:23:49.120] CLICK: Invoking MenuHelpTopics.AsMenuItem().Click()
[14:23:49.634] WAIT: Polling for window title="도움말", timeout 12s
[14:23:53.289] WAIT: Window found after 3.7s
[14:23:53.400] TREE_CHECK: Looking for TreeView in HelpWindow
[14:23:53.410] TREE_CHECK: Found Tree control, properties: Name="TopicTree", ControlType=Tree
[14:23:53.490] ASSERTION: IsTopicTreeVisible() returned true
[14:23:53.500] TEST: PASSED
```

**Currently**: Only xUnit test name in output; everything else is silent.

---

## 6. FlaUI Architecture Observations

### 6.1 WPF-Specific Behaviors

**MenuItem AutomationPeer Registration**:
- WPF registers MenuItem peers at Background Dispatcher priority (not UI-priority)
- Expansion doesn't guarantee immediate peer availability
- Menu must REMAIN OPEN for peers to register (collapse = reset)
- Initial expansion: up to 26-40 seconds observed (machine-dependent)
- Subsequent expansions: faster (peers already registered)

**Evidence**:
- AppFixture.WarmupSingleMenuAsync line 93-100
- Multiple 200-attempt retry loops in tests (each 200ms × 200 = 40s max)
- Comments in test files (line 22-23, 34-35) mention "up to 40s"

**Impact**: Tests must wait 90+ seconds during fixture initialization before any test runs.

### 6.2 Modal/Owned Window Handling

**UIAutomation Limitation**: Modal dialogs with Owner set don't appear in parent window's UIAutomation tree children.

**Workaround Used**: Win32 FindWindow bypasses UIAutomation hierarchy.

**Code**:
```csharp
[DllImport("user32.dll", CharSet = CharSet.Unicode)]
private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

var hwnd = FindWindow(null, "About X-ray Detector GUI");
if (hwnd != IntPtr.Zero) {
    var dialog = Fixture.Automation.FromHandle(hwnd);
    // interact with dialog
}
```

**Requirements**:
- Window title must be exact (case-sensitive in Korean UI!)
- Must have interactive desktop for Win32 API to work
- HWND to FlaUI conversion via `Automation.FromHandle(hwnd)`

### 6.3 Keyboard vs. Mouse Input

**Why Keyboard for Menu Access**:
- DPI-scaled screens: mouse coordinates become incorrect after DPI scaling
- Access keys work regardless of DPI (they're key codes, not coordinates)
- Keyboard input more reliable in automated environments

**Code**:
```csharp
// UNRELIABLE on scaled displays:
helpMenu!.AsMenuItem().Click(); // Uses mouse coordinates

// MORE RELIABLE:
helpMenu!.AsMenuItem().Click(); // open menu
FlaUI.Core.Input.Keyboard.Type('a'); // send 'A' as access key
```

**Used In**: AboutDialogE2ETests, HelpSystemE2ETests (proven approach)

---

## 7. Known Issues & Root Causes

### 7.1 E2E-DEBUG-001: AppFixture.InitializeAsync() Timeout

**Symptom**: 30-second timeout on GetMainWindow() in non-interactive bash sessions.

**Root Cause**:
- WPF window created but UIAutomation tree doesn't initialize in headless/non-interactive environments
- GetMainWindow() returns null consistently
- Timeout expires, InitializeAsync throws exception
- Test collection fixture fail cascades to all tests

**Status**: Won't fix in CLI (by design). Workaround: run in interactive desktop session.

**Reference**: `.moai/issues/E2E-DEBUG-001.md`

### 7.2 Help Window Menu Wiring Bug (Fixed in Recent Tests)

**Original Issue**: Help → Topics menu item missing or not wired to ShowHelpCommand.

**Why It Was Undetected**:
1. **No behavioral test**: AppFixture.WarmupSingleMenuAsync only primed MenuHelpAbout, not MenuHelpTopics
2. **Only structural tests**: Checked AutomationId existence, not actual behavior
3. **No window open test**: HelpWindowPage had only GetTitle() and Close(), no behavior verification

**Fix Applied**:
- HelpSystemE2ETests added:
  - HelpTopicsMenuItem_OpensHelpWindow(): Actually clicks menu item, waits for window
  - F1Key_OpensHelpWindow(): Tests keyboard shortcut
  - HelpWindow_HasTopicTreeVisible(): Verifies window opens with content

**Reference**: HelpSystemE2ETests.cs lines 10-28 (Root Cause Analysis comment block)

---

## 8. Reference Patterns for Future Enhancement

### 8.1 xUnit ITestOutputHelper Pattern

**Current Non-Use**:
```csharp
// NOT USED IN ANY TEST:
public SomeE2ETest(AppFixture fixture, ITestOutputHelper output) { }
```

**Why Not Used**:
- ITestOutputHelper only available in test class constructors
- AppFixture is ICollectionFixture (shared across all tests in collection)
- Fixtures don't receive ITestOutputHelper directly
- Would require thread-local storage or dependency injection workaround

**Possible Solutions**:
1. **Thread-Local Context** (simplest):
   ```csharp
   public static class TestContext {
       [ThreadStatic]
       private static ITestOutputHelper? _output;

       public static void SetOutput(ITestOutputHelper output) => _output = output;
       public static void WriteLine(string message) => _output?.WriteLine(message);
   }
   ```
   - Set in test constructor: `TestContext.SetOutput(output);`
   - Fixture calls: `TestContext.WriteLine(...)`
   - Works with serial execution (one test at a time)

2. **Test Failure Hook** (via xUnit extension):
   - Create `ITestFrameworkExecutor` extension
   - Capture test failures automatically
   - Call ScreenshotHelper and dump UI tree on failure

### 8.2 Enhanced WaitHelper Pattern

**Current**:
```csharp
public static async Task<bool> WaitUntilAsync(
    Func<bool> condition,
    int timeoutMs = 5000,
    int pollIntervalMs = 100)
```

**Enhanced Version**:
```csharp
public static async Task<bool> WaitUntilAsync(
    Func<bool> condition,
    string? description = null,
    int timeoutMs = 5000,
    int pollIntervalMs = 100,
    Action<int, long>? onPoll = null) // (attemptNumber, elapsedMs)
{
    var sw = Stopwatch.StartNew();
    int attempt = 0;
    while (sw.ElapsedMilliseconds < timeoutMs) {
        if (condition()) {
            TestContext.WriteLine($"WAIT[{description}]: Succeeded in {sw.Elapsed.TotalSeconds:F2}s after {attempt} attempts");
            return true;
        }
        attempt++;
        onPoll?.Invoke(attempt, sw.ElapsedMilliseconds);
        await Task.Delay(pollIntervalMs);
    }
    TestContext.WriteLine($"WAIT[{description}]: TIMEOUT after {sw.Elapsed.TotalSeconds:F2}s, {attempt} attempts");
    return false;
}
```

**Usage**:
```csharp
await WaitHelper.WaitUntilAsync(
    () => FindWindow(null, "About X-ray Detector GUI") != IntPtr.Zero,
    "About dialog window title",
    timeoutMs: 12000,
    pollIntervalMs: 100);
```

### 8.3 Element Search with Logging

**Current Pattern**:
```csharp
var menuItem = helpMenu.FindFirstChild(cf => cf.ByAutomationId("MenuHelpAbout"));
if (menuItem == null) {
    // timeout, retry, then fail with no context
}
```

**Enhanced Pattern**:
```csharp
public static AutomationElement? FindWithLogging(
    this AutomationElement element,
    ConditionFactory cf,
    string description,
    int maxAttempts = 200,
    int delayMs = 200)
{
    TestContext.WriteLine($"SEARCH: Looking for {description}");
    var sw = Stopwatch.StartNew();

    for (int i = 0; i < maxAttempts; i++) {
        try {
            var found = element.FindFirstChild(cf);
            if (found != null) {
                TestContext.WriteLine(
                    $"SEARCH: Found {description} in {sw.Elapsed.TotalSeconds:F2}s (attempt {i+1})");
                return found;
            }
        } catch (Exception ex) {
            TestContext.WriteLine($"SEARCH: Exception on attempt {i+1}: {ex.Message}");
        }
        Thread.Sleep(delayMs);
    }

    TestContext.WriteLine(
        $"SEARCH: NOT FOUND - {description} after {maxAttempts} attempts ({sw.Elapsed.TotalSeconds:F2}s)");
    DumpElementTree(element, "MenuHelpAbout search context");
    return null;
}
```

**Dependency**: Would require extension method on AutomationElement.

### 8.4 UIAutomation Tree Dump Utility

**Need**: When element search fails, what's in the tree?

**Prototype**:
```csharp
public static void DumpElementTree(AutomationElement element, string label, int maxDepth = 3)
{
    var sb = new StringBuilder();
    sb.AppendLine($"TREE DUMP [{label}]:");
    DumpRecursive(element, 0, maxDepth, sb);
    TestContext.WriteLine(sb.ToString());
}

private static void DumpRecursive(
    AutomationElement element,
    int depth,
    int maxDepth,
    StringBuilder sb)
{
    if (depth > maxDepth) return;

    var indent = new string(' ', depth * 2);
    var name = element.Name ?? "<unnamed>";
    var id = element.AutomationId ?? "<no-id>";
    var type = element.ControlType.Name;

    sb.AppendLine($"{indent}[{type}] Name='{name}' AutomationId='{id}'");

    try {
        var children = element.FindAllChildren();
        foreach (var child in children.Take(10)) { // Limit output
            DumpRecursive(child, depth + 1, maxDepth, sb);
        }
        if (children.Length > 10) {
            sb.AppendLine($"{indent}  ... and {children.Length - 10} more children");
        }
    } catch { /* skip on error */ }
}
```

**Output**:
```
TREE DUMP [MenuHelpAbout search context]:
[Menu] Name='File' AutomationId='<no-id>'
  [MenuItem] Name='Exit' AutomationId='MenuFileExit'
[Menu] Name='Edit' AutomationId='<no-id>'
[Menu] Name='Help' AutomationId='<no-id>'
  [MenuItem] Name='About' AutomationId='MenuHelpAbout'
  [MenuItem] Name='도움말 항목' AutomationId='<not-found-here>'
  ... more children
```

---

## 9. Recommendations for SPEC-E2E-002

### 9.1 Logging Instrumentation

**Priority 1 - Fixture Initialization**:
- Log each step of AppFixture.InitializeAsync (process start, window wait, menu warmup)
- Include timing for each phase
- Log exceptions instead of swallowing them
- Add debug-level logging for retry attempts

**Priority 2 - Test Method Context**:
- Implement ThreadLocal TestContext for ITestOutputHelper injection
- Log test start/stop with names
- Log fixture state (IsDesktopAvailable, MainWindow availability)

**Priority 3 - Element Search Logging**:
- Add logging to all AutomationElement.Find* calls (high-frequency, key debugging point)
- Include description of what was searched for
- Include attempt count and timing
- On failure, dump partial UI tree

**Priority 4 - Application Log Correlation**:
- Capture Serilog logs in test context
- Add test ID/name to application logs
- Show parallel timeline of test events + app events

### 9.2 Diagnostic Capture

**Priority 1 - Screenshot on Failure**:
- Integrate ScreenshotHelper with xUnit failure hooks
- Capture both full screen and element-focused views
- Store with test context metadata

**Priority 2 - UIAutomation Tree Dumps**:
- Export tree on fixture init completion
- Export tree on element search failure
- Export tree on test assertion failure

**Priority 3 - Process Diagnostics**:
- Log GUI.Application process ID, window handle, memory usage
- Capture stderr/stdout if available
- Log process exit status on fixture cleanup

### 9.3 Test Framework Enhancements

**Priority 1 - Conditional Tracing**:
- Add XRAY_E2E_DEBUG environment variable (enables verbose logging)
- Control log level per component (Fixture, WaitHelper, ElementSearch, etc.)

**Priority 2 - Retry Automation**:
- Enhance RetryFactAttribute to actually retry (not just metadata)
- Add logging for retry attempts and backoff delays

**Priority 3 - Timeout Management**:
- Make hardcoded delays configurable (environment variables)
- Add exponential backoff for element searches
- Implement smart timeout based on running average

---

## 10. Summary of Findings

### Test Infrastructure
- **Solid foundation**: AppFixture properly manages process lifecycle
- **Good patterns**: Win32 FindWindow for modal dialogs, access keys for DPI safety
- **Weak patterns**: Hardcoded delays, no logging, catch-all exceptions

### Logging Gaps
- **Fixture**: Basic Trace.WriteLine, no attempt/timing telemetry
- **Tests**: ZERO logging, no test context capture
- **Helpers**: No logging in WaitHelper or element search utilities
- **Failure**: No automatic diagnostics on test failure

### FlaUI Capabilities
- **Well-suited**: UIAutomation tree navigation, control invocation
- **WPF-specific**: MenuItem peer registration delays, modal window handling
- **Extensible**: Custom helpers for logging/diagnostics feasible

### Recommendation
Implement comprehensive logging architecture with:
1. Test context (ITestOutputHelper injection via ThreadLocal)
2. Instrumented element search (logging, tree dumps on failure)
3. Fixture initialization telemetry (timing, phase transitions)
4. Automatic screenshot/diagnostics on test failure
5. Application log correlation (Serilog ↔ test output)

This will provide visibility into E2E test execution and dramatically improve debuggability of failures in non-interactive environments.

---

## Appendix A: File Reference Map

| File | Lines | Purpose |
|------|-------|---------|
| AppFixture.cs | 187 | Process lifecycle, UIAutomation initialization |
| E2ETestBase.cs | 33 | Test base class, MainWindow access, collection def |
| WaitHelper.cs | 23 | Async polling utilities |
| ScreenshotHelper.cs | 34 | Screenshot capture (not used) |
| RetryFactAttribute.cs | 79 | Retry metadata and manual retry helpers |
| RequiresDesktopFactAttribute.cs | 26 | Desktop detection, auto-skip in CI |
| AboutDialogE2ETests.cs | 110 | Modal dialog testing, Win32 FindWindow pattern |
| CoreFlowE2ETests.cs | 74 | Basic menu/button/tab tests |
| HelpSystemE2ETests.cs | 223 | Root cause analysis, behavioral tests for Help |
| ParameterExtractionE2ETests.cs | 92 | Tab switching and button verification |
| AppLaunchTests.cs | 70 | Smoke tests |
| HelpWindowPage.cs | 30 | PageObject for HelpWindow |
| AboutDialogPage.cs | 28 | PageObject for AboutDialog |
| App.xaml.cs | 77 | Application entry point, E2E_MODE handling |
| E2E-DEBUG-001.md | 89 | Known issue: timeout in non-interactive sessions |

---

**Document Generated**: 2026-03-14
**Researcher**: team-researcher
**Status**: Analysis Complete - Ready for Requirements & Architecture Design
