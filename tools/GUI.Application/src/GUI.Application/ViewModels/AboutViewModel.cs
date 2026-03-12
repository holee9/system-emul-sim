using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using XrayDetector.Gui.Core;

namespace XrayDetector.Gui.ViewModels;

/// <summary>
/// ViewModel for About dialog showing application and system information (SPEC-HELP-001).
/// </summary>
public sealed class AboutViewModel : ObservableObject
{
    private readonly IClipboardService _clipboardService;

    /// <summary>
    /// Creates a new AboutViewModel.
    /// </summary>
    /// <param name="clipboardService">Clipboard abstraction for testability (DEC-004).</param>
    public AboutViewModel(IClipboardService clipboardService)
    {
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));

        var info = ApplicationInfo.Instance;
        AppName = info.AppName;
        Version = info.Version;
        BuildDate = info.BuildDate;
        DotNetVersion = RuntimeInformation.FrameworkDescription;
        OSVersion = RuntimeInformation.OSDescription;
        ProcessorCount = Environment.ProcessorCount;
        AvailableMemoryMB = (long)(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024));

        PipelineStatusItems =
        [
            new PipelineStatusItem("Panel", "Connected"),
            new PipelineStatusItem("FPGA", "Connected"),
            new PipelineStatusItem("MCU", "Connected"),
            new PipelineStatusItem("Host", "Connected"),
        ];

        CopyToClipboardCommand = new RelayCommand(OnCopyToClipboard);
        OpenGitHubCommand = new RelayCommand(OnOpenGitHub);
    }

    /// <summary>Application name.</summary>
    public string AppName { get; }

    /// <summary>Assembly version string.</summary>
    public string Version { get; }

    /// <summary>Build date from AssemblyMetadataAttribute.</summary>
    public string BuildDate { get; }

    /// <summary>.NET runtime description.</summary>
    public string DotNetVersion { get; }

    /// <summary>Operating system description.</summary>
    public string OSVersion { get; }

    /// <summary>Number of logical processors.</summary>
    public int ProcessorCount { get; }

    /// <summary>Available physical memory in MB.</summary>
    public long AvailableMemoryMB { get; }

    /// <summary>4-component pipeline connection status list.</summary>
    public IReadOnlyList<PipelineStatusItem> PipelineStatusItems { get; }

    /// <summary>Copies formatted system info to the clipboard.</summary>
    public ICommand CopyToClipboardCommand { get; }

    /// <summary>Opens the project GitHub URL in the default browser.</summary>
    public ICommand OpenGitHubCommand { get; }

    private void OnCopyToClipboard()
    {
        var text = $"""
            Application: {AppName}
            Version:     {Version}
            Build Date:  {BuildDate}
            .NET:        {DotNetVersion}
            OS:          {OSVersion}
            CPUs:        {ProcessorCount}
            Memory:      {AvailableMemoryMB} MB available
            """;

        _clipboardService.SetText(text);
    }

    private static void OnOpenGitHub()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/holee9/system-emul-sim",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open GitHub: {ex.Message}");
        }
    }
}

/// <summary>
/// Represents a single pipeline component status item displayed in the About dialog.
/// </summary>
public sealed record PipelineStatusItem(string Name, string Status);
