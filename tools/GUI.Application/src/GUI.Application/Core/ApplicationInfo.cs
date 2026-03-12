using System.Reflection;

namespace XrayDetector.Gui.Core;

/// <summary>
/// Singleton providing application version and build information (DEC-003).
/// Used by StatusBar and AboutViewModel.
/// </summary>
public sealed class ApplicationInfo
{
    private static readonly Lazy<ApplicationInfo> _instance =
        new(() => new ApplicationInfo(), LazyThreadSafetyMode.ExecutionAndPublication);

    private ApplicationInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var name = assembly.GetName();

        AppName = name.Name ?? "GUI.Application";
        Version = name.Version?.ToString() ?? "1.0.0";

        // Read build date from AssemblyMetadataAttribute
        var buildDateAttr = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "BuildDate");

        BuildDate = buildDateAttr?.Value ?? "Unknown";
    }

    /// <summary>Gets the singleton instance of ApplicationInfo.</summary>
    public static ApplicationInfo Instance => _instance.Value;

    /// <summary>Gets the application name from the assembly.</summary>
    public string AppName { get; }

    /// <summary>Gets the assembly version string.</summary>
    public string Version { get; }

    /// <summary>Gets the build date from AssemblyMetadataAttribute, or "Unknown".</summary>
    public string BuildDate { get; }
}
