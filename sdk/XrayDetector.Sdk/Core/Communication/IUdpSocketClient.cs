using System.Net;

namespace XrayDetector.Core.Communication;

/// <summary>
/// Interface for UDP socket client abstraction.
/// Provides testable abstraction over System.Net.Sockets.UdpClient.
/// </summary>
public interface IUdpSocketClient : IDisposable
{
    /// <summary>Remote host endpoint.</summary>
    string Host { get; }

    /// <summary>Remote port number.</summary>
    int Port { get; }

    /// <summary>Current socket state.</summary>
    UdpSocketState State { get; }

    /// <summary>True if socket is connected.</summary>
    bool IsConnected { get; }

    /// <summary>Establishes connection to remote endpoint.</summary>
    Task ConnectAsync(CancellationToken cancellationToken);

    /// <summary>Closes the connection.</summary>
    Task DisconnectAsync();

    /// <summary>Sends data to remote endpoint.</summary>
    Task SendAsync(byte[] data, CancellationToken cancellationToken);

    /// <summary>Receives data from remote endpoint.</summary>
    Task<UdpReceiveResult> ReceiveAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Result of a UDP receive operation.
/// </summary>
public readonly record struct UdpReceiveResult(byte[] Buffer, IPEndPoint? RemoteEndPoint);
