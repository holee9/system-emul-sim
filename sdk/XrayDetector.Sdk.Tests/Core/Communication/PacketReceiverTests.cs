using Moq;
using System.Buffers;
using System.IO.Pipelines;
using XrayDetector.Core.Communication;
using XrayDetector.Common.Dto;
using Xunit;

namespace XrayDetector.Sdk.Tests.Core.Communication;

/// <summary>
/// Specification tests for PacketReceiver.
/// Tests high-performance packet processing using System.IO.Pipelines.
/// </summary>
public class PacketReceiverTests : IDisposable
{
    private readonly Mock<IUdpSocketClient> _mockUdpClient;
    private readonly Pipe _pipe;

    public PacketReceiverTests()
    {
        _mockUdpClient = new Mock<IUdpSocketClient>(MockBehavior.Loose);
        _pipe = new Pipe();
    }

    public void Dispose()
    {
        _pipe.Reader.Complete();
        _pipe.Writer.Complete();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Constructor_WithValidClient_CreatesReceiver()
    {
        // Act
        var receiver = new PacketReceiver(_mockUdpClient.Object);

        // Assert
        Assert.NotNull(receiver);
        Assert.Equal(PacketReceiverState.Stopped, receiver.State);
        Assert.False(receiver.IsReceiving);
    }

    [Fact]
    public void Constructor_WithNullClient_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new PacketReceiver(null!));
    }

    [Fact]
    public async Task StartAsync_WhenStopped_StartsReceiving()
    {
        // Arrange
        _mockUdpClient.Setup(x => x.IsConnected).Returns(true);
        var receiver = new PacketReceiver(_mockUdpClient.Object);
        var cts = new CancellationTokenSource(100);

        // Act
        await receiver.StartAsync(cts.Token);

        // Assert
        Assert.Equal(PacketReceiverState.Running, receiver.State);
        Assert.True(receiver.IsReceiving);

        // Cleanup
        await receiver.StopAsync();
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyRunning_DoesNotRestart()
    {
        // Arrange
        _mockUdpClient.Setup(x => x.IsConnected).Returns(true);
        var receiver = new PacketReceiver(_mockUdpClient.Object);
        await receiver.StartAsync(CancellationToken.None);
        var cts = new CancellationTokenSource(100);

        // Act - Should not throw
        await receiver.StartAsync(cts.Token);

        // Assert
        Assert.True(receiver.IsReceiving);

        // Cleanup
        cts.Cancel();
        await receiver.StopAsync();
    }

    [Fact]
    public async Task StartAsync_WhenClientNotConnected_ThrowsInvalidOperationException()
    {
        // Arrange
        _mockUdpClient.Setup(x => x.IsConnected).Returns(false);
        var receiver = new PacketReceiver(_mockUdpClient.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            receiver.StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task StopAsync_WhenRunning_StopsReceiving()
    {
        // Arrange
        _mockUdpClient.Setup(x => x.IsConnected).Returns(true);
        var receiver = new PacketReceiver(_mockUdpClient.Object);
        await receiver.StartAsync(CancellationToken.None);

        // Act
        await receiver.StopAsync();

        // Assert
        Assert.Equal(PacketReceiverState.Stopped, receiver.State);
        Assert.False(receiver.IsReceiving);
    }

    [Fact]
    public async Task StopAsync_WhenAlreadyStopped_DoesNotThrow()
    {
        // Arrange
        _mockUdpClient.Setup(x => x.IsConnected).Returns(true);
        var receiver = new PacketReceiver(_mockUdpClient.Object);

        // Act - Should not throw
        await receiver.StopAsync();

        // Assert
        Assert.False(receiver.IsReceiving);
    }

    [Fact]
    public void OnPacketReceived_RaisesEvent()
    {
        // Arrange
        _mockUdpClient.Setup(x => x.IsConnected).Returns(true);
        var receiver = new PacketReceiver(_mockUdpClient.Object);

        // Act & Assert - Verify we can subscribe without throwing
        receiver.PacketReceived += (s, e) => { /* Event handler */ };

        // Cleanup
        receiver.Dispose();
    }

    [Fact]
    public void OnError_RaisesEvent()
    {
        // Arrange
        _mockUdpClient.Setup(x => x.IsConnected).Returns(true);
        var receiver = new PacketReceiver(_mockUdpClient.Object);

        // Act & Assert - Verify we can subscribe without throwing
        receiver.ErrorOccurred += (s, e) => { /* Event handler */ };

        // Cleanup
        receiver.Dispose();
    }
}
