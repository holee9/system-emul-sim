using System.Windows.Threading;
using IntegrationRunner.Core;
using XrayDetector.Gui.Core;

namespace XrayDetector.Gui.ViewModels;

/// <summary>
/// ViewModel for pipeline statistics display (REQ-UI-013).
/// Shows real-time statistics from SimulatorPipeline with 2Hz polling.
/// </summary>
public sealed class PipelineStatusViewModel : ObservableObject
{
    private const double YellowFailureThreshold = 0.05; // 5%
    private const double RedFailureThreshold = 0.10; // 10%
    private const int PollingIntervalMs = 500; // 2Hz = 500ms

    private readonly DispatcherTimer _pollingTimer;
    private int _framesProcessed;
    private int _framesFailed;
    private double _avgProcessingTimeMs;
    private long _packetsSent;
    private long _packetsLost;
    private long _packetsReordered;
    private long _packetsCorrupted;
    private string _statusIndicator = "Green";

    /// <summary>
    /// Total frames processed by the pipeline.
    /// </summary>
    public int FramesProcessed
    {
        get => _framesProcessed;
        set => SetField(ref _framesProcessed, value);
    }

    /// <summary>
    /// Total frames that failed processing.
    /// </summary>
    public int FramesFailed
    {
        get => _framesFailed;
        set => SetField(ref _framesFailed, value);
    }

    /// <summary>
    /// Average processing time per frame in milliseconds.
    /// </summary>
    public double AvgProcessingTimeMs
    {
        get => _avgProcessingTimeMs;
        set => SetField(ref _avgProcessingTimeMs, value);
    }

    /// <summary>
    /// Total packets sent through the network channel.
    /// </summary>
    public long PacketsSent
    {
        get => _packetsSent;
        set => SetField(ref _packetsSent, value);
    }

    /// <summary>
    /// Total packets lost due to simulated network loss.
    /// </summary>
    public long PacketsLost
    {
        get => _packetsLost;
        set => SetField(ref _packetsLost, value);
    }

    /// <summary>
    /// Total packets reordered by the network channel.
    /// </summary>
    public long PacketsReordered
    {
        get => _packetsReordered;
        set => SetField(ref _packetsReordered, value);
    }

    /// <summary>
    /// Total packets corrupted by the network channel.
    /// </summary>
    public long PacketsCorrupted
    {
        get => _packetsCorrupted;
        set => SetField(ref _packetsCorrupted, value);
    }

    /// <summary>
    /// Status indicator color (Green/Yellow/Red).
    /// </summary>
    public string StatusIndicator
    {
        get => _statusIndicator;
        set => SetField(ref _statusIndicator, value);
    }

    /// <summary>
    /// Creates a new PipelineStatusViewModel.
    /// </summary>
    public PipelineStatusViewModel()
    {
        _pollingTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(PollingIntervalMs)
        };
        _pollingTimer.Tick += OnPollingTick;
    }

    /// <summary>
    /// Starts the polling timer.
    /// </summary>
    public void StartPolling()
    {
        _pollingTimer.Start();
    }

    /// <summary>
    /// Stops the polling timer.
    /// </summary>
    public void StopPolling()
    {
        _pollingTimer.Stop();
    }

    /// <summary>
    /// Updates statistics from PipelineStatistics snapshot.
    /// </summary>
    public void UpdateStatistics(PipelineStatistics stats)
    {
        FramesProcessed = stats.FramesProcessed;
        FramesFailed = stats.FramesFailed;
        AvgProcessingTimeMs = CalculateAvgProcessingTime(stats);

        if (stats.NetworkStats != null)
        {
            PacketsSent = stats.NetworkStats.PacketsSent;
            PacketsLost = stats.NetworkStats.PacketsLost;
            PacketsReordered = stats.NetworkStats.PacketsReordered;
            PacketsCorrupted = stats.NetworkStats.PacketsCorrupted;
        }

        UpdateStatusIndicator();
    }

    /// <summary>
    /// Resets all statistics to zero.
    /// </summary>
    public void Reset()
    {
        FramesProcessed = 0;
        FramesFailed = 0;
        AvgProcessingTimeMs = 0.0;
        PacketsSent = 0;
        PacketsLost = 0;
        PacketsReordered = 0;
        PacketsCorrupted = 0;
        StatusIndicator = "Green";
    }

    private void OnPollingTick(object? sender, EventArgs e)
    {
        // TODO: Fetch statistics from SimulatorPipeline
        // This will be implemented when integrating with MainViewModel
    }

    private double CalculateAvgProcessingTime(PipelineStatistics stats)
    {
        if (stats.FramesProcessed == 0)
            return 0.0;

        // Placeholder: actual implementation would measure timing
        return 1.0; // 1ms per frame default
    }

    private void UpdateStatusIndicator()
    {
        if (FramesProcessed == 0)
        {
            StatusIndicator = "Green";
            return;
        }

        double failureRate = (double)FramesFailed / FramesProcessed;

        if (failureRate >= RedFailureThreshold)
            StatusIndicator = "Red";
        else if (failureRate >= YellowFailureThreshold)
            StatusIndicator = "Yellow";
        else
            StatusIndicator = "Green";
    }
}
