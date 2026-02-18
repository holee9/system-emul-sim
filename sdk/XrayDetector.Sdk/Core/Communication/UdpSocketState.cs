namespace XrayDetector.Core.Communication;

/// <summary>
/// Represents the current state of a UDP socket connection.
/// </summary>
public enum UdpSocketState
{
    /// <summary>Socket is not connected.</summary>
    Disconnected = 0,

    /// <summary>Socket is attempting to connect.</summary>
    Connecting = 1,

    /// <summary>Socket is connected and ready for communication.</summary>
    Connected = 2,

    /// <summary>Socket is closing connection.</summary>
    Closing = 3,

    /// <summary>Socket encountered an error.</summary>
    Error = 4
}
