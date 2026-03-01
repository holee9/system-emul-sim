using System.CommandLine;

namespace Common.Cli;

/// <summary>
/// Simulation fidelity level for all module CLIs.
/// </summary>
public enum Fidelity
{
    /// <summary>Fast, approximate simulation.</summary>
    Low,

    /// <summary>Balanced speed and accuracy.</summary>
    Medium,

    /// <summary>Full physics-based simulation.</summary>
    High
}

/// <summary>
/// Base class for all module CLI applications.
/// Provides common options and the ParseAndRun execution pattern.
/// </summary>
public abstract class CliFramework
{
    /// <summary>Common option: configuration file path.</summary>
    protected static readonly Option<FileInfo?> ConfigOption = new("--config", "-c")
    {
        Description = "Path to configuration file (JSON/YAML)"
    };

    /// <summary>Common option: output file or directory path.</summary>
    protected static readonly Option<string?> OutputOption = new("--output", "-o")
    {
        Description = "Output file or directory path"
    };

    /// <summary>Common option: simulation fidelity level.</summary>
    protected static readonly Option<Fidelity> FidelityOption = new("--fidelity", "-f")
    {
        Description = "Simulation fidelity level (low/medium/high)",
        DefaultValueFactory = _ => Fidelity.Medium
    };

    /// <summary>Common option: random seed for reproducibility.</summary>
    protected static readonly Option<int?> SeedOption = new("--seed", "-s")
    {
        Description = "Random seed for reproducible results"
    };

    /// <summary>Common option: verbose output.</summary>
    protected static readonly Option<bool> VerboseOption = new("--verbose", "-v")
    {
        Description = "Enable verbose diagnostic output",
        DefaultValueFactory = _ => false
    };

    /// <summary>
    /// Gets the root command description.
    /// </summary>
    protected abstract string CommandDescription { get; }

    /// <summary>
    /// Builds the root command with module-specific options and handler.
    /// </summary>
    /// <returns>Configured root command.</returns>
    protected abstract RootCommand BuildCommand();

    /// <summary>
    /// Parses command-line arguments and runs the CLI.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Exit code (0 = success).</returns>
    public int ParseAndRun(string[] args)
    {
        var rootCommand = BuildCommand();
        var parseResult = rootCommand.Parse(args);
        return parseResult.Invoke();
    }

    /// <summary>
    /// Parses command-line arguments and runs the CLI asynchronously.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Exit code (0 = success).</returns>
    public async Task<int> ParseAndRunAsync(string[] args)
    {
        var rootCommand = BuildCommand();
        var parseResult = rootCommand.Parse(args);
        return await parseResult.InvokeAsync();
    }

    /// <summary>
    /// Creates a root command with all common options pre-added.
    /// Subclasses should call this then add module-specific options.
    /// </summary>
    protected RootCommand CreateRootCommand()
    {
        var root = new RootCommand(CommandDescription);
        root.Add(VerboseOption);
        root.Add(SeedOption);
        root.Add(FidelityOption);
        root.Add(ConfigOption);
        return root;
    }

    /// <summary>
    /// Writes a diagnostic message to stderr when verbose mode is enabled.
    /// </summary>
    protected static void WriteVerbose(bool verbose, string message)
    {
        if (verbose)
        {
            Console.Error.WriteLine($"[VERBOSE] {message}");
        }
    }
}
