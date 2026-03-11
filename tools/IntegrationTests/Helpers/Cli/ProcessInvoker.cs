using System.Diagnostics;

namespace IntegrationTests.Helpers.Cli;

/// <summary>
/// Invokes CLI programs as external processes.
/// Captures stdout, stderr, and exit code from process execution.
/// </summary>
public class ProcessInvoker : ICliInvoker
{
    private readonly string _cliProjectName;

    /// <summary>
    /// Initializes a new instance of the ProcessInvoker class.
    /// </summary>
    /// <param name="cliProjectName">Name of the CLI project (e.g., "PanelSimulator.Cli").</param>
    public ProcessInvoker(string cliProjectName)
    {
        _cliProjectName = cliProjectName;
    }

    /// <summary>
    /// Invokes the CLI program as an external process.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Execution result with captured output.</returns>
    public CliInvocationResult Invoke(string[] args)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Find the CLI executable path
        // Navigate from test output dir (bin/Debug/net8.0) to solution root
        var solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));

        // Handle pattern: "PanelSimulator.Cli" -> "tools/PanelSimulator/src/PanelSimulator.Cli"
        var parts = _cliProjectName.Split('.');
        var simulatorName = parts[0]; // e.g., "PanelSimulator"
        var cliProjectName = _cliProjectName; // e.g., "PanelSimulator.Cli"

        // Build path: solutionRoot/tools/PanelSimulator/src/PanelSimulator.Cli/bin/Debug/net8.0/PanelSimulator.Cli.dll
        var executablePath = Path.Combine(
            solutionRoot,
            "tools",
            simulatorName,
            "src",
            cliProjectName,
            "bin",
            "Debug",
            "net8.0",
            $"{_cliProjectName}.dll"
        );

        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException(
                $"CLI executable not found at: {executablePath}" + Environment.NewLine +
                $"Solution root: {solutionRoot}" + Environment.NewLine +
                $"Base directory: {AppContext.BaseDirectory}" + Environment.NewLine +
                $"Simulator name: {simulatorName}, CLI project: {cliProjectName}"
            );
        }

        // Find dotnet executable
        var dotnetPath = Environment.OSVersion.Platform == PlatformID.Win32NT
            ? @"C:\Program Files\dotnet\dotnet.exe"
            : "dotnet";

        var startInfo = new ProcessStartInfo
        {
            FileName = dotnetPath,
            Arguments = $"\"{executablePath}\" {string.Join(" ", args)}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start process");
        }

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        stopwatch.Stop();

        return new CliInvocationResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = stdout,
            StandardError = stderr,
            Duration = stopwatch.Elapsed
        };
    }
}
