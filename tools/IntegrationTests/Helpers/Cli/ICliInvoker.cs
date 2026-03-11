namespace IntegrationTests.Helpers.Cli;

/// <summary>
/// Interface for invoking CLI programs with output capture.
/// Supports both external process execution and direct in-memory calls.
/// </summary>
public interface ICliInvoker
{
    /// <summary>
    /// Invokes the CLI program with the specified arguments.
    /// </summary>
    /// <param name="args">Command-line arguments to pass to the CLI program.</param>
    /// <returns>Result containing exit code, output, and duration.</returns>
    CliInvocationResult Invoke(string[] args);
}
