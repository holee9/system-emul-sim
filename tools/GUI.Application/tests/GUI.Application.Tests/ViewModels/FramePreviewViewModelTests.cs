using System.ComponentModel;
using FluentAssertions;
using XrayDetector.Core.Processing;
using XrayDetector.Gui.ViewModels;
using XrayDetector.Models;

namespace XrayDetector.Gui.Tests.ViewModels;

/// <summary>
/// TDD tests for FramePreviewViewModel (RED phase).
/// Tests frame preview per REQ-TOOLS-041, REQ-TOOLS-042.
/// </summary>
public class FramePreviewViewModelTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        // Act
        var viewModel = new FramePreviewViewModel();

        // Assert - REQ-TOOLS-042: Window/Level defaults
        viewModel.WindowCenter.Should().BeApproximately(32768.0, 1.0);
        viewModel.WindowWidth.Should().BeApproximately(65535.0, 1.0);
        viewModel.CurrentFrame.Should().BeNull();
        viewModel.FrameInfo.Should().Be("No frame");
        viewModel.ZoomLevel.Should().BeApproximately(1.0, 0.01);
    }

    [Fact]
    public void SetFrame_WithValidFrame_ShouldUpdateProperties()
    {
        // Arrange
        var viewModel = new FramePreviewViewModel();
        var testFrame = CreateTestFrame(128, 128);

        // Act
        viewModel.SetFrame(testFrame);

        // Assert - REQ-TOOLS-041: Frame display
        viewModel.CurrentFrame.Should().Be(testFrame);
        viewModel.FrameInfo.Should().Contain("128x128");
        viewModel.FrameInfo.Should().Contain("16-bit");
    }

    [Fact]
    public void SetFrame_WithNullFrame_ShouldClearDisplay()
    {
        // Arrange
        var viewModel = new FramePreviewViewModel();
        var testFrame = CreateTestFrame(64, 64);
        viewModel.SetFrame(testFrame);

        // Act
        viewModel.SetFrame(null!);

        // Assert
        viewModel.CurrentFrame.Should().BeNull();
        viewModel.FrameInfo.Should().Be("No frame");
    }

    [Fact]
    public void SetFrame_ShouldUpdatePixelData()
    {
        // Arrange
        var viewModel = new FramePreviewViewModel();
        var testFrame = CreateTestFrame(64, 64);

        // Act
        viewModel.SetFrame(testFrame);

        // Assert - REQ-TOOLS-041: Pixel data available for display
        viewModel.DisplayPixels.Should().NotBeNull();
        viewModel.DisplayPixels.Length.Should().Be(64 * 64);
    }

    [Fact]
    public void UpdateWindowLevel_ShouldApplyWindowLevelMapping()
    {
        // Arrange
        var viewModel = new FramePreviewViewModel();
        var testFrame = CreateTestFrame(64, 64);
        viewModel.SetFrame(testFrame);

        var originalPixels = (byte[])viewModel.DisplayPixels.Clone();

        // Act - REQ-TOOLS-042: Window/Level update within 100ms
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        viewModel.UpdateWindowLevel(16384.0, 32768.0);
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, "Window/Level update must be < 100ms per REQ-TOOLS-042");
        viewModel.WindowCenter.Should().BeApproximately(16384.0, 1.0);
        viewModel.WindowWidth.Should().BeApproximately(32768.0, 1.0);
    }

    [Fact]
    public void UpdateWindowLevel_ShouldRaisePropertyChanged()
    {
        // Arrange
        var viewModel = new FramePreviewViewModel();
        var testFrame = CreateTestFrame(64, 64);
        viewModel.SetFrame(testFrame);

        var propertiesChanged = new List<string>();
        viewModel.PropertyChanged += (s, e) => propertiesChanged.Add(e.PropertyName!);

        // Act
        viewModel.UpdateWindowLevel(20000.0, 40000.0);

        // Assert
        propertiesChanged.Should().Contain(nameof(FramePreviewViewModel.WindowCenter));
        propertiesChanged.Should().Contain(nameof(FramePreviewViewModel.WindowWidth));
    }

    [Fact]
    public void UpdateWindowLevel_WithInvalidWidth_ShouldThrowArgumentException()
    {
        // Arrange
        var viewModel = new FramePreviewViewModel();

        // Act
        var act = () => viewModel.UpdateWindowLevel(32768.0, 0);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*width must be positive*");
    }

    [Fact]
    public void UpdateWindowLevel_WithNegativeWidth_ShouldThrowArgumentException()
    {
        // Arrange
        var viewModel = new FramePreviewViewModel();

        // Act
        var act = () => viewModel.UpdateWindowLevel(32768.0, -100.0);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AutoWindowLevel_ShouldCalculateFromFrameData()
    {
        // Arrange
        var viewModel = new FramePreviewViewModel();
        var testFrame = CreateTestFrameWithGradient(256, 256);
        viewModel.SetFrame(testFrame);

        // Act
        viewModel.AutoWindowLevel();

        // Assert - Auto-calculated window/level should cover data range
        viewModel.WindowWidth.Should().BeGreaterThan(0);
        viewModel.WindowCenter.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SetZoomLevel_ShouldUpdateZoomProperty()
    {
        // Arrange
        var viewModel = new FramePreviewViewModel();

        // Act
        viewModel.SetZoomLevel(2.5);

        // Assert
        viewModel.ZoomLevel.Should().BeApproximately(2.5, 0.01);
    }

    [Fact]
    public void SetZoomLevel_ShouldRaisePropertyChanged()
    {
        // Arrange
        var viewModel = new FramePreviewViewModel();
        var propertyChanged = false;

        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(FramePreviewViewModel.ZoomLevel))
                propertyChanged = true;
        };

        // Act
        viewModel.SetZoomLevel(1.5);

        // Assert
        propertyChanged.Should().BeTrue();
    }

    [Fact]
    public void SetZoomLevel_WithInvalidValue_ShouldClampToValidRange()
    {
        // Arrange
        var viewModel = new FramePreviewViewModel();

        // Act - Negative zoom should be clamped
        viewModel.SetZoomLevel(-1.0);

        // Assert
        viewModel.ZoomLevel.Should().BeGreaterOrEqualTo(0.1);
    }

    [Fact]
    public void GetDisplayPixelData_ShouldReturn8BitGrayscale()
    {
        // Arrange
        var viewModel = new FramePreviewViewModel();
        var testFrame = CreateTestFrame(64, 64);
        viewModel.SetFrame(testFrame);

        // Act
        var displayData = viewModel.DisplayPixels;

        // Assert - 16-bit to 8-bit conversion per REQ-TOOLS-041
        displayData.Should().NotBeNull();
        displayData.Length.Should().Be(64 * 64);
        displayData.Should().OnlyContain(x => x >= 0 && x <= 255, "display pixels must be 8-bit grayscale");
    }

    [Fact]
    public void GetDisplayPixelData_AfterWindowLevelChange_ShouldReflectMapping()
    {
        // Arrange
        var viewModel = new FramePreviewViewModel();
        var testFrame = CreateTestFrame(64, 64);
        viewModel.SetFrame(testFrame);

        var originalPixels = (byte[])viewModel.DisplayPixels.Clone();

        // Act - Apply narrow window to increase contrast
        viewModel.UpdateWindowLevel(32768.0, 10000.0);
        var newPixels = viewModel.DisplayPixels;

        // Assert - Pixels should be different after window/level change
        newPixels.Should().NotBeEquivalentTo(originalPixels, "window/level change should affect display mapping");
    }

    [Fact]
    public void FrameInfo_ShouldIncludeFrameNumber()
    {
        // Arrange
        var viewModel = new FramePreviewViewModel();
        var testFrame = CreateTestFrame(64, 64, frameNumber: 42);

        // Act
        viewModel.SetFrame(testFrame);

        // Assert
        viewModel.FrameInfo.Should().Contain("42");
    }

    [Fact]
    public void FrameInfo_ShouldIncludeTimestamp()
    {
        // Arrange
        var viewModel = new FramePreviewViewModel();
        var testFrame = CreateTestFrame(64, 64);
        viewModel.SetFrame(testFrame);

        // Assert
        viewModel.FrameInfo.Should().Contain(testFrame.Timestamp.ToString("HH:mm:ss"));
    }

    [Fact]
    public void CanSaveFrame_WithNoFrame_ShouldReturnFalse()
    {
        // Arrange
        var viewModel = new FramePreviewViewModel();

        // Act
        var canSave = viewModel.CanSaveFrame();

        // Assert
        canSave.Should().BeFalse("cannot save without a frame");
    }

    [Fact]
    public void CanSaveFrame_WithFrame_ShouldReturnTrue()
    {
        // Arrange
        var viewModel = new FramePreviewViewModel();
        var testFrame = CreateTestFrame(64, 64);
        viewModel.SetFrame(testFrame);

        // Act
        var canSave = viewModel.CanSaveFrame();

        // Assert
        canSave.Should().BeTrue("should be able to save when frame is loaded");
    }

    private static Frame CreateTestFrame(int width, int height, uint frameNumber = 1)
    {
        var pixelData = new ushort[width * height];
        for (int i = 0; i < pixelData.Length; i++)
        {
            pixelData[i] = (ushort)(i % 65536);
        }

        var metadata = new XrayDetector.Common.Dto.FrameMetadata(
            width: width,
            height: height,
            bitDepth: 16,
            timestamp: DateTime.UtcNow,
            frameNumber: frameNumber
        );

        return new Frame(pixelData, metadata);
    }

    private static Frame CreateTestFrameWithGradient(int width, int height)
    {
        var pixelData = new ushort[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                pixelData[y * width + x] = (ushort)((x + y) * 256 / (width + height));
            }
        }

        var metadata = new XrayDetector.Common.Dto.FrameMetadata(
            width: width,
            height: height,
            bitDepth: 16,
            timestamp: DateTime.UtcNow,
            frameNumber: 1
        );

        return new Frame(pixelData, metadata);
    }
}
