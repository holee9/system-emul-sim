using Moq;
using System.Net;
using XrayDetector.Core.Communication;
using Xunit;

namespace XrayDetector.Sdk.Tests.Core.Communication;

/// <summary>
/// Specification tests for UdpSocketClient.
/// Tests UDP communication, connection management, and error handling.
/// </summary>
public class UdpSocketClientTests : IDisposable
{
    private const string TestHost = "127.0.0.1";
    private const int TestPort = 18001;

    public void Dispose()
    {
        // Cleanup any resources
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesClient()
    {
        // Act
        var client = new UdpSocketClient(TestHost, TestPort);

        // Assert
        Assert.NotNull(client);
        Assert.Equal(TestHost, client.Host);
        Assert.Equal(TestPort, client.Port);
        Assert.Equal(UdpSocketState.Disconnected, client.State);
        Assert.False(client.IsConnected);
    }

    [Fact]
    public void Constructor_WithNullHost_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new UdpSocketClient(null!, TestPort));
    }

    [Fact]
    public void Constructor_WithInvalidPort_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new UdpSocketClient(TestHost, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new UdpSocketClient(TestHost, 65536));
    }

    [Fact]
    public async Task ConnectAsync_WhenNotConnected_EstablishesConnection()
    {
        // Arrange
        using var client = new UdpSocketClient(TestHost, TestPort);

        // Act
        await client.ConnectAsync(CancellationToken.None);

        // Assert
        Assert.Equal(UdpSocketState.Connected, client.State);
        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task ConnectAsync_WhenAlreadyConnected_DoesNotReconnect()
    {
        // Arrange
        using var client = new UdpSocketClient(TestHost, TestPort);
        await client.ConnectAsync(CancellationToken.None);

        // Act
        await client.ConnectAsync(CancellationToken.None);

        // Assert - Should still be connected, no exception
        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task DisconnectAsync_WhenConnected_ClosesConnection()
    {
        // Arrange
        using var client = new UdpSocketClient(TestHost, TestPort);
        await client.ConnectAsync(CancellationToken.None);

        // Act
        await client.DisconnectAsync();

        // Assert
        Assert.Equal(UdpSocketState.Disconnected, client.State);
        Assert.False(client.IsConnected);
    }

    [Fact]
    public async Task SendAsync_WhenConnected_SendsData()
    {
        // Arrange
        using var client = new UdpSocketClient(TestHost, TestPort);
        await client.ConnectAsync(CancellationToken.None);
        var data = new byte[] { 0x01, 0x02, 0x03 };

        // Act & Assert - Should not throw
        await client.SendAsync(data, CancellationToken.None);
    }

    [Fact]
    public async Task SendAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Arrange
        using var client = new UdpSocketClient(TestHost, TestPort);
        var data = new byte[] { 0x01, 0x02, 0x03 };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SendAsync(data, CancellationToken.None));
    }

    [Fact]
    public async Task SendAsync_WithNullData_ThrowsArgumentNullException()
    {
        // Arrange
        using var client = new UdpSocketClient(TestHost, TestPort);
        await client.ConnectAsync(CancellationToken.None);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.SendAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task SendAsync_WithCancellation_CancelsOperation()
    {
        // Arrange
        using var client = new UdpSocketClient(TestHost, TestPort);
        await client.ConnectAsync(CancellationToken.None);
        var data = new byte[] { 0x01, 0x02, 0x03 };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.SendAsync(data, cts.Token));
    }

    [Fact]
    public async Task ReceiveAsync_WhenConnected_ReturnsData()
    {
        // Arrange
        using var client = new UdpSocketClient(TestHost, TestPort);
        await client.ConnectAsync(CancellationToken.None);

        // Act - Note: This will timeout if no data is available
        var cts = new CancellationTokenSource(100); // 100ms timeout
        var result = await client.ReceiveAsync(cts.Token);

        // Assert - Result should have buffer (even if empty)
        Assert.NotNull(result.Buffer);
    }

    [Fact]
    public async Task ReceiveAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Arrange
        using var client = new UdpSocketClient(TestHost, TestPort);
        var cts = new CancellationTokenSource(100);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.ReceiveAsync(cts.Token));
    }

    [Fact]
    public async Task Dispose_WhenConnected_ClosesConnection()
    {
        // Arrange
        var client = new UdpSocketClient(TestHost, TestPort);
        await client.ConnectAsync(CancellationToken.None);

        // Act
        client.Dispose();

        // Assert - Client should be disconnected
        Assert.False(client.IsConnected);
    }

    [Fact]
    public void State_Transitions_AreCorrect()
    {
        // Arrange
        using var client = new UdpSocketClient(TestHost, TestPort);

        // Assert - Initial state
        Assert.Equal(UdpSocketState.Disconnected, client.State);

        // Note: Actual state transitions tested in async tests
    }
}
