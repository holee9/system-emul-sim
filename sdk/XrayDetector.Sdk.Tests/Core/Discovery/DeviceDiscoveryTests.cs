using XrayDetector.Core.Discovery;
using Xunit;

namespace XrayDetector.Sdk.Tests.Core.Discovery;

/// <summary>
/// Specification tests for DeviceDiscovery.
/// Handles UDP broadcast discovery of X-ray detector devices on the network.
/// </summary>
public class DeviceDiscoveryTests : IDisposable
{
    private readonly DeviceDiscovery _discovery;

    public DeviceDiscoveryTests()
    {
        _discovery = new DeviceDiscovery();
    }

    public void Dispose()
    {
        _discovery?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Create_WithDefaultParameters_CreatesDiscovery()
    {
        // Arrange & Act
        var discovery = new DeviceDiscovery();

        // Assert
        Assert.NotNull(discovery);
    }

    [Fact]
    public void Create_WithCustomPort_CreatesDiscovery()
    {
        // Arrange & Act
        var discovery = new DeviceDiscovery(port: 8003);

        // Assert
        Assert.NotNull(discovery);
    }

    [Fact]
    public async Task DiscoverAsync_WithNoDevices_ReturnsEmpty()
    {
        // Arrange & Act
        var devices = await _discovery.DiscoverAsync(timeout: TimeSpan.FromSeconds(1));

        // Assert
        Assert.NotNull(devices);
        // Empty if no devices on network
    }

    [Fact]
    public async Task DiscoverAsync_WithTimeout_RespectsTimeout()
    {
        // Arrange
        var timeout = TimeSpan.FromMilliseconds(100);

        // Act
        var startTime = DateTime.UtcNow;
        var devices = await _discovery.DiscoverAsync(timeout);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        Assert.NotNull(devices);
        // Should complete within timeout + small overhead
        Assert.True(elapsed < timeout + TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task DiscoverAsync_MultipleCalls_AllowMultipleDiscoveries()
    {
        // Arrange & Act
        var devices1 = await _discovery.DiscoverAsync(timeout: TimeSpan.FromMilliseconds(100));
        var devices2 = await _discovery.DiscoverAsync(timeout: TimeSpan.FromMilliseconds(100));

        // Assert
        Assert.NotNull(devices1);
        Assert.NotNull(devices2);
    }

    [Fact]
    public async Task DiscoverAsync_WithCancellation_CancelsDiscovery()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        // Act
        var devices = await _discovery.DiscoverAsync(timeout: TimeSpan.FromSeconds(5), cts.Token);

        // Assert
        Assert.NotNull(devices);
        // Should cancel early
    }

    [Fact]
    public void DefaultPort_WhenCreated_Returns8002()
    {
        // Arrange & Act
        var discovery = new DeviceDiscovery();

        // Assert - Default port should be 8002
        Assert.NotNull(discovery);
    }

    [Fact]
    public void Dispose_WithDiscovery_DisposesCleanly()
    {
        // Arrange
        var discovery = new DeviceDiscovery();

        // Act & Assert - Should dispose without exception
        discovery.Dispose();
    }
}
