using System.IO;
using System.Reflection;

namespace IntegrationTests.Helpers.Cli;

/// <summary>
/// Invokes CLI programs directly in-memory by calling their Main method.
/// Faster than ProcessInvoker but requires same AppDomain execution.
/// </summary>
public class DirectCallInvoker : ICliInvoker
{
    private readonly string _cliProjectName;

    /// <summary>
    /// Initializes a new instance of the DirectCallInvoker class.
    /// </summary>
    /// <param name="cliProjectName">Name of the CLI project (e.g., "PanelSimulator.Cli").</param>
    public DirectCallInvoker(string cliProjectName)
    {
        _cliProjectName = cliProjectName;
    }

    /// <summary>
    /// Invokes the CLI program by directly calling its Main method.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Execution result with captured output.</returns>
    public CliInvocationResult Invoke(string[] args)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Capture console output
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdoutWriter = new StringWriter();
        var stderrWriter = new StringWriter();

        try
        {
            Console.SetOut(stdoutWriter);
            Console.SetError(stderrWriter);

            // Load the CLI assembly
            var solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));

            // Handle both patterns: "PanelSimulator.Cli" -> "tools/PanelSimulator/src/PanelSimulator.Cli"
            // And generic pattern: "SomeProject.Cli" -> "tools/SomeProject/src/SomeProject.Cli"
            var parts = _cliProjectName.Split('.');
            var simulatorName = parts[0]; // e.g., "PanelSimulator"
            var cliProjectName = _cliProjectName; // e.g., "PanelSimulator.Cli"

            var assemblyPath = Path.Combine(solutionRoot, "tools", simulatorName, "src", cliProjectName, "bin", "Debug", "net8.0", $"{_cliProjectName}.dll");

            if (!File.Exists(assemblyPath))
            {
                throw new FileNotFoundException($"CLI assembly not found: {assemblyPath}");
            }

            var assembly = Assembly.LoadFrom(assemblyPath);

            // Find the Program class and Main method
            var programType = assembly.GetType($"{_cliProjectName}.Program");
            if (programType == null)
            {
                throw new Exception($"Program class not found in assembly {assemblyPath}");
            }

            var mainMethod = programType.GetMethod("Main", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (mainMethod == null)
            {
                throw new Exception($"Main method not found in {programType.FullName}");
            }

            // Invoke Main method
            var mainArgs = new object[] { args };
            var exitCodeObj = mainMethod.Invoke(null, mainArgs);
            var exitCode = exitCodeObj is int code ? code : 0;

            stopwatch.Stop();

            return new CliInvocationResult
            {
                ExitCode = exitCode,
                StandardOutput = stdoutWriter.ToString(),
                StandardError = stderrWriter.ToString(),
                Duration = stopwatch.Elapsed
            };
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            stdoutWriter.Dispose();
            stderrWriter.Dispose();
        }
    }
}
