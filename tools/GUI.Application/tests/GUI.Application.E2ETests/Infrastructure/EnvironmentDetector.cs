namespace XrayDetector.Gui.E2ETests.Infrastructure;

/// <summary>
/// Detects whether the current environment supports interactive desktop E2E tests.
/// TAG-001: Centralizes all environment detection logic for E2E infrastructure.
///
/// Environment Variables:
///   CI                   - Set to "true" in CI/CD systems (GitHub Actions, Jenkins, etc.)
///   GITHUB_ACTIONS       - Set to "true" by GitHub Actions runner
///   XRAY_E2E_FORCE       - Set to "1" to force interactive mode even in non-interactive sessions
///   XRAY_E2E_ATTACH_PID  - Set to a process PID to enable FlaUI attach mode (SPEC-E2E-004)
///   SESSIONNAME          - Windows session name: "Console" (local), "RDP-Tcp#N" (remote desktop)
///   MSYSTEM              - Set by MSYS2/Git Bash; indicates non-real-desktop terminal emulator
/// </summary>
public static class EnvironmentDetector
{
    /// <summary>
    /// Returns true if running in a known CI environment.
    /// </summary>
    public static bool IsCI() =>
        Environment.GetEnvironmentVariable("CI") == "true" ||
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));

    /// <summary>
    /// Returns true if XRAY_E2E_FORCE=1 is set (manual override for interactive mode).
    /// </summary>
    public static bool IsForced() =>
        Environment.GetEnvironmentVariable("XRAY_E2E_FORCE") == "1";

    /// <summary>
    /// Returns true if XRAY_E2E_ATTACH_PID is set and non-empty.
    /// When true, AppFixture attaches to an existing process instead of launching a new one.
    /// SPEC-E2E-004: TAG-002 — controls attach vs. launch mode selection.
    /// </summary>
    public static bool IsAttachMode() =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("XRAY_E2E_ATTACH_PID"));

    /// <summary>
    /// Returns true if the current session supports FlaUI UIAutomation (real desktop).
    /// Uses actual environment variables. For unit testing, use the internal overload.
    /// </summary>
    public static bool IsInteractiveDesktop() =>
        IsInteractiveDesktop(
            ci: Environment.GetEnvironmentVariable("CI"),
            githubActions: Environment.GetEnvironmentVariable("GITHUB_ACTIONS"),
            force: Environment.GetEnvironmentVariable("XRAY_E2E_FORCE"),
            sessionName: Environment.GetEnvironmentVariable("SESSIONNAME"),
            userInteractive: Environment.UserInteractive,
            wtSession: Environment.GetEnvironmentVariable("WT_SESSION"),
            msystem: Environment.GetEnvironmentVariable("MSYSTEM"));

    /// <summary>
    /// Core detection logic — injectable for unit tests.
    /// Decision order:
    ///   1. CI environments: always false (CI/GITHUB_ACTIONS)
    ///   2. Force override: always true (XRAY_E2E_FORCE=1)
    ///   3. Windows session type: Console or RDP = true
    ///   4. Terminal emulators that are NOT real desktops: Git Bash (MSYSTEM) = false
    ///   5. Legacy fallback: Environment.UserInteractive
    /// </summary>
    internal static bool IsInteractiveDesktop(
        string? ci,
        string? githubActions,
        string? force,
        string? sessionName,
        bool userInteractive,
        string? wtSession,
        string? msystem)
    {
        // 1. CI environments: always false
        if (ci == "true" || !string.IsNullOrEmpty(githubActions)) return false;

        // 2. Force override
        if (force == "1") return true;

        // 3. Windows session type: Console or RDP = true
        if (sessionName?.StartsWith("Console", StringComparison.OrdinalIgnoreCase) == true) return true;
        if (sessionName?.StartsWith("RDP-Tcp", StringComparison.OrdinalIgnoreCase) == true) return true;

        // 4. Terminal emulators that are NOT real desktops
        if (!string.IsNullOrEmpty(msystem)) return false; // Git Bash / MSYS2

        // 5. Legacy fallback
        return userInteractive;
    }

    /// <summary>
    /// Returns a diagnostic summary of current environment variables for skip messages.
    /// </summary>
    public static string GetEnvironmentSummary() =>
        $"CI={Environment.GetEnvironmentVariable("CI") ?? "null"}, " +
        $"SESSIONNAME={Environment.GetEnvironmentVariable("SESSIONNAME") ?? "null"}, " +
        $"UserInteractive={Environment.UserInteractive}, " +
        $"MSYSTEM={Environment.GetEnvironmentVariable("MSYSTEM") ?? "null"}, " +
        $"XRAY_E2E_ATTACH_PID={Environment.GetEnvironmentVariable("XRAY_E2E_ATTACH_PID") ?? "null"}";
}
