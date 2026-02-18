using System.Net;
using System.Net.Sockets;
using System.Text;

namespace XrayDetector.Core.Discovery;

/// <summary>
/// Represents a discovered X-ray detector device.
/// </summary>
public sealed class DiscoveredDevice
{
    /// <summary>Device IP address.</summary>
    public string IpAddress { get; }

    /// <summary>Device MAC address (if available).</summary>
    public string? MacAddress { get; }

    /// <summary>Device model name.</summary>
    public string Model { get; }

    /// <summary>Device serial number.</summary>
    public string SerialNumber { get; }

    /// <summary>Device firmware version.</summary>
    public string FirmwareVersion { get; }

    /// <summary>Response timestamp.</summary>
    public DateTime DiscoveredAt { get; }

    public DiscoveredDevice(
        string ipAddress,
        string? macAddress,
        string model,
        string serialNumber,
        string firmwareVersion,
        DateTime discoveredAt)
    {
        IpAddress = ipAddress ?? throw new ArgumentNullException(nameof(ipAddress));
        MacAddress = macAddress;
        Model = model ?? throw new ArgumentNullException(nameof(model));
        SerialNumber = serialNumber ?? throw new ArgumentNullException(nameof(serialNumber));
        FirmwareVersion = firmwareVersion ?? throw new ArgumentNullException(nameof(firmwareVersion));
        DiscoveredAt = discoveredAt;
    }

    public override string ToString()
    {
        return $"{Model} (SN: {SerialNumber}) at {IpAddress}, FW: {FirmwareVersion}";
    }
}

/// <summary>
/// UDP broadcast device discovery for X-ray detectors.
/// Sends broadcast on port 8002 and collects responses.
/// </summary>
public sealed class DeviceDiscovery : IDisposable
{
    private readonly int _port;
    private readonly UdpClient _udpClient;
    private bool _disposed;

    /// <summary>
    /// Creates a new device discovery with default port (8002).
    /// </summary>
    public DeviceDiscovery()
        : this(8002)
    {
    }

    /// <summary>
    /// Creates a new device discovery with custom port.
    /// </summary>
    /// <param name="port">UDP port to use for discovery.</param>
    public DeviceDiscovery(int port)
    {
        _port = port;
        _udpClient = new UdpClient();
        _udpClient.EnableBroadcast = true;
        _udpClient.Client.ReceiveTimeout = 3000; // 3 second timeout to prevent blocking
        _disposed = false;
    }

    /// <summary>
    /// Discovers X-ray detector devices on the local network.
    /// </summary>
    /// <param name="timeout">Discovery timeout.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of discovered devices.</returns>
    public async Task<List<DiscoveredDevice>> DiscoverAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var devices = new List<DiscoveredDevice>();
        var discoveryTasks = new List<Task>();
        var receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            // Send discovery broadcast
            byte[] discoveryMessage = Encoding.UTF8.GetBytes("DISCOVER_XRAY_DETECTOR");
            IPEndPoint broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, _port);

            await _udpClient.SendAsync(discoveryMessage, discoveryMessage.Length, broadcastEndpoint);

            // Start receiving responses
            var receiveTask = ReceiveResponsesAsync(devices, receiveCts.Token);

            // Wait for timeout or cancellation
            var timeoutTask = Task.Delay(timeout, cancellationToken);
            var completedTask = await Task.WhenAny(receiveTask, timeoutTask);

            // Cancel receiving
            receiveCts.Cancel();

            try
            {
                await receiveTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            return devices;
        }
        catch (OperationCanceledException)
        {
            return devices;
        }
    }

    private async Task ReceiveResponsesAsync(
        List<DiscoveredDevice> devices,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && !_disposed)
        {
            try
            {
                // Use Task.WhenAny to implement timeout-safe receive
                var receiveTask = _udpClient.ReceiveAsync();
                var timeoutTask = Task.Delay(1000, cancellationToken); // Check cancellation every second
                var completedTask = await Task.WhenAny(receiveTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    // Timeout reached, check cancellation and continue
                    continue;
                }

                var result = await receiveTask;
                byte[] data = result.Buffer;
                string response = Encoding.UTF8.GetString(data);

                // Parse discovery response
                // Expected format: "DEVICE_INFO:model,serial,firmware"
                if (response.StartsWith("DEVICE_INFO:"))
                {
                    var parts = response.Substring("DEVICE_INFO:".Length).Split(',');
                    if (parts.Length >= 3)
                    {
                        var device = new DiscoveredDevice(
                            ipAddress: result.RemoteEndPoint.Address.ToString(),
                            macAddress: null,
                            model: parts[0],
                            serialNumber: parts[1],
                            firmwareVersion: parts[2],
                            discoveredAt: DateTime.UtcNow
                        );
                        devices.Add(device);
                    }
                }
            }
            catch (SocketException)
            {
                // Timeout or no more data
                break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _udpClient?.Dispose();
        _disposed = true;
    }
}
