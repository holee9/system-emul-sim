using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace XrayDetector.Core.Communication;

/// <summary>
/// UDP socket client wrapper for testable network communication.
/// Wraps System.Net.Sockets.UdpClient with connection state management.
/// </summary>
public sealed class UdpSocketClient : IUdpSocketClient
{
    private readonly UdpClient _udpClient;
    private int _state = (int)UdpSocketState.Disconnected;

    /// <inheritdoc />
    public string Host { get; }

    /// <inheritdoc />
    public int Port { get; }

    /// <inheritdoc />
    public UdpSocketState State => (UdpSocketState)Volatile.Read(ref _state);

    /// <inheritdoc />
    public bool IsConnected => State == UdpSocketState.Connected;

    /// <summary>
    /// Creates a new UDP socket client.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// Thrown when host is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when port is not in valid range [1, 65535].
    /// </exception>
    public UdpSocketClient(string host, int port)
    {
        Host = host ?? throw new ArgumentNullException(nameof(host));
        if (port < 1 || port > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");

        Port = port;
        _udpClient = new UdpClient();
    }

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (IsConnected)
            return; // Already connected

        SetState(UdpSocketState.Connecting);

        try
        {
            // UdpClient.Connect(string host, int port) is synchronous
            // We need to resolve DNS and connect asynchronously
            var addresses = await Dns.GetHostAddressesAsync(Host, cancellationToken);
            var endPoint = new IPEndPoint(addresses.First(), Port);

            await Task.Run(() => _udpClient.Connect(endPoint), cancellationToken);

            SetState(UdpSocketState.Connected);
        }
        catch
        {
            SetState(UdpSocketState.Error);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DisconnectAsync()
    {
        if (!IsConnected)
            return; // Already disconnected

        SetState(UdpSocketState.Closing);

        try
        {
            _udpClient.Close();
            await Task.CompletedTask; // Sync operation, but async for interface consistency
        }
        finally
        {
            SetState(UdpSocketState.Disconnected);
        }
    }

    /// <inheritdoc />
    public async Task SendAsync(byte[] data, CancellationToken cancellationToken)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Socket is not connected.");

        if (data == null)
            throw new ArgumentNullException(nameof(data));

        // Throw if already cancelled
        cancellationToken.ThrowIfCancellationRequested();

        await _udpClient.SendAsync(data, data.Length)
            .WaitAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UdpReceiveResult> ReceiveAsync(CancellationToken cancellationToken)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Socket is not connected.");

        try
        {
            var result = await _udpClient.ReceiveAsync()
                .WaitAsync(cancellationToken);

            return new UdpReceiveResult(result.Buffer, result.RemoteEndPoint);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Return empty result on timeout instead of throwing
            return new UdpReceiveResult(Array.Empty<byte>(), null);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (IsConnected)
        {
            try
            {
                _udpClient.Close();
            }
            catch { /* Ignore cleanup errors */ }
        }

        _udpClient.Dispose();
        SetState(UdpSocketState.Disconnected);
    }

    private void SetState(UdpSocketState newState)
    {
        Volatile.Write(ref _state, (int)newState);
    }
}
