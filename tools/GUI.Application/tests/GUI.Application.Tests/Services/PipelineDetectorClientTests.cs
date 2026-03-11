using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using IntegrationRunner.Core;
using IntegrationRunner.Core.Models;
using IntegrationRunner.Core.Network;
using XrayDetector.Common.Dto;
using XrayDetector.Gui.Services;
using XrayDetector.Implementation;
using XrayDetector.Models;
using Xunit;

namespace XrayDetector.Gui.Tests.Services;

/// <summary>
/// TDD tests for PipelineDetectorClient (REQ-UI-011).
/// RED phase: Define expected behavior for IDetectorClient implementation wrapping SimulatorPipeline.
/// </summary>
public class PipelineDetectorClientTests : IDisposable
{
    private readonly PipelineDetectorClient _client;
    private readonly DetectorConfig _defaultConfig;

    public PipelineDetectorClientTests()
    {
        _client = new PipelineDetectorClient();
        _defaultConfig = GetDefaultConfig();
    }

    private static DetectorConfig GetDefaultConfig()
    {
        return new DetectorConfig
        {
            Panel = new PanelConfig
            {
                Rows = 64,
                Cols = 64,
                BitDepth = 14
            },
            Fpga = new FpgaConfig
            {
                Csi2Lanes = 4,
                Csi2DataRateMbps = 1500,
                LineBufferDepth = 1024
            },
            Soc = new SocConfig
            {
                FrameBufferCount = 4,
                UdpPort = 8001,
                EthernetPort = 9001
            },
            Simulation = new SimulationConfig
            {
                MaxFrames = 10 // Fast unit test execution
            }
        };
    }

    [Fact]
    public void Implements_IDetectorClient()
    {
        // Assert: PipelineDetectorClient should implement IDetectorClient
        _client.Should().BeAssignableTo<IDetectorClient>();
    }

    [Fact]
    public async Task ConnectAsync_sets_IsConnected_to_true()
    {
        // Arrange
        _client.IsConnected.Should().BeFalse("initial state should be disconnected");

        // Act
        await _client.ConnectAsync("127.0.0.1", 8000);

        // Assert
        _client.IsConnected.Should().BeTrue("after ConnectAsync, client should be connected");
    }

    [Fact]
    public async Task ConnectAsync_fires_ConnectionChanged_event()
    {
        // Arrange
        var eventFired = false;
        ConnectionStateChangedEventArgs? args = null;
        _client.ConnectionChanged += (s, e) =>
        {
            eventFired = true;
            args = e;
        };

        // Act
        await _client.ConnectAsync("127.0.0.1", 8000);

        // Assert
        eventFired.Should().BeTrue("ConnectionChanged event should fire on ConnectAsync");
        args.Should().NotBeNull();
        args!.IsConnected.Should().BeTrue("event args should indicate connected state");
        args.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task DisconnectAsync_sets_IsConnected_to_false()
    {
        // Arrange
        await _client.ConnectAsync("127.0.0.1", 8000);
        _client.IsConnected.Should().BeTrue();

        // Act
        await _client.DisconnectAsync();

        // Assert
        _client.IsConnected.Should().BeFalse("after DisconnectAsync, client should be disconnected");
    }

    [Fact]
    public async Task DisconnectAsync_fires_ConnectionChanged_event()
    {
        // Arrange
        await _client.ConnectAsync("127.0.0.1", 8000);
        var eventFired = false;
        ConnectionStateChangedEventArgs? args = null;
        _client.ConnectionChanged += (s, e) =>
        {
            eventFired = true;
            args = e;
        };

        // Act
        await _client.DisconnectAsync();

        // Assert
        eventFired.Should().BeTrue("ConnectionChanged event should fire on DisconnectAsync");
        args.Should().NotBeNull();
        args!.IsConnected.Should().BeFalse("event args should indicate disconnected state");
    }

    [Fact]
    public async Task ConfigureAsync_accepts_DetectorConfiguration()
    {
        // Arrange
        var detectorConfig = new DetectorConfiguration(
            width: 64,
            height: 64,
            bitDepth: 14,
            frameRate: 10,
            gain: 1000,
            offset: 0
        );

        // Act & Assert (should not throw)
        await _client.ConfigureAsync(detectorConfig);
    }

    [Fact]
    public async Task StartAcquisitionAsync_starts_frame_generation()
    {
        // Arrange
        _client.UpdateConfig(_defaultConfig);
        await _client.ConnectAsync("127.0.0.1", 8000);
        var frameReceivedEventCount = 0;
        _client.FrameReceived += (s, e) => frameReceivedEventCount++;

        // Act
        await _client.StartAcquisitionAsync();

        // Wait for at least 2 frames (100ms each at 10fps)
        await Task.Delay(300);

        // Assert
        frameReceivedEventCount.Should().BeGreaterOrEqualTo(2,
            "should receive at least 2 frames in 300ms at 10fps");
    }

    [Fact]
    public async Task StopAcquisitionAsync_stops_frame_generation()
    {
        // Arrange
        _client.UpdateConfig(_defaultConfig);
        await _client.ConnectAsync("127.0.0.1", 8000);
        await _client.StartAcquisitionAsync();

        var frameReceivedEventCount = 0;
        _client.FrameReceived += (s, e) => frameReceivedEventCount++;

        // Act
        await _client.StopAcquisitionAsync();

        // Wait for potential additional frames
        await Task.Delay(300);

        // Assert: frame count should not increase significantly after stop
        var countAfterStop = frameReceivedEventCount;
        await Task.Delay(200);
        frameReceivedEventCount.Should().Be(countAfterStop,
            "frame count should not increase after StopAcquisitionAsync");
    }

    [Fact]
    public async Task FrameReceived_event_contains_valid_frame()
    {
        // Arrange
        _client.UpdateConfig(_defaultConfig);
        await _client.ConnectAsync("127.0.0.1", 8000);
        await _client.StartAcquisitionAsync();

        Frame? capturedFrame = null;
        var tcs = new TaskCompletionSource<bool>();
        _client.FrameReceived += (s, e) =>
        {
            capturedFrame = e.Frame;
            tcs.TrySetResult(true);
        };

        // Act
        await Task.WhenAny(tcs.Task, Task.Delay(1000));

        // Assert
        capturedFrame.Should().NotBeNull("should receive a frame within 1 second");
        capturedFrame!.Width.Should().Be(64, "frame width should match config");
        capturedFrame.Height.Should().Be(64, "frame height should match config");
        capturedFrame.PixelData.Should().NotBeEmpty("frame should have pixel data");
    }

    [Fact]
    public async Task CaptureFrameAsync_returns_single_frame()
    {
        // Arrange
        _client.UpdateConfig(_defaultConfig);
        await _client.ConnectAsync("127.0.0.1", 8000);
        await _client.StartAcquisitionAsync();

        // Act
        var frame = await _client.CaptureFrameAsync();

        // Assert
        frame.Should().NotBeNull();
        frame!.Width.Should().Be(64);
        frame.Height.Should().Be(64);
        frame.PixelData.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetStatusAsync_returns_current_status()
    {
        // Arrange
        _client.UpdateConfig(_defaultConfig);
        await _client.ConnectAsync("127.0.0.1", 8000);
        await _client.StartAcquisitionAsync();

        // Act
        var status = await _client.GetStatusAsync();

        // Assert
        status.Should().NotBeNull();
        status!.ConnectionState.Should().Be(ConnectionState.Connected);
        status.AcquisitionState.Should().Be(AcquisitionState.Acquiring);
        status.Temperature.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        // Arrange
        _client.UpdateConfig(_defaultConfig);

        // Act & Assert (should not throw)
        _client.Dispose();
        _client.Dispose();
    }

    [Fact]
    public async Task Operations_after_Dispose_should_fail_gracefully()
    {
        // Arrange
        _client.UpdateConfig(_defaultConfig);
        await _client.ConnectAsync("127.0.0.1", 8000);
        _client.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _client.StartAcquisitionAsync());
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}
