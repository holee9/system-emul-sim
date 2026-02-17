namespace HostSimulator.Core.Configuration;

/// <summary>
/// Configuration for HostSimulator.
/// Loaded from detector_config.yaml host section.
/// </summary>
public sealed class HostConfig
{
    /// <summary>
    /// Gets or sets the UDP listen port.
    /// </summary>
    public int ListenPort { get; set; } = 8000;

    /// <summary>
    /// Gets or sets the packet timeout in milliseconds.
    /// </summary>
    public int PacketTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the number of receive threads.
    /// </summary>
    public int ReceiveThreads { get; set; } = 1;

    /// <summary>
    /// Gets or sets the output directory for saved frames.
    /// </summary>
    public string? OutputDirectory { get; set; }

    /// <summary>
    /// Gets or sets whether to save frames as TIFF.
    /// </summary>
    public bool SaveTiff { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to save frames as RAW.
    /// </summary>
    public bool SaveRaw { get; set; } = false;
}
