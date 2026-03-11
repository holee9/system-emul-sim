namespace IntegrationTests.Helpers.Cli;

/// <summary>
/// Result of a CLI program invocation.
/// Contains exit code, output streams, and execution duration.
/// </summary>
public class CliInvocationResult
{
    /// <summary>
    /// Gets the exit code returned by the CLI program.
    /// </summary>
    public int ExitCode { get; init; }

    /// <summary>
    /// Gets the standard output captured from the CLI program.
    /// </summary>
    public string StandardOutput { get; init; } = string.Empty;

    /// <summary>
    /// Gets the standard error output captured from the CLI program.
    /// </summary>
    public string StandardError { get; init; } = string.Empty;

    /// <summary>
    /// Gets the duration of the CLI program execution.
    /// </summary>
    public TimeSpan Duration { get; init; }
}
