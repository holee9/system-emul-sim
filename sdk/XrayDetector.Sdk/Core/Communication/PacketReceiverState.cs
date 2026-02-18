namespace XrayDetector.Core.Communication;

/// <summary>
/// Represents the current state of the packet receiver.
/// </summary>
public enum PacketReceiverState
{
    /// <summary>Receiver is not running.</summary>
    Stopped = 0,

    /// <summary>Receiver is actively processing packets.</summary>
    Running = 1,

    /// <summary>Receiver is stopping.</summary>
    Stopping = 2,

    /// <summary>Receiver encountered an error.</summary>
    Error = 3
}
