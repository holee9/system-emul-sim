# Host SDK .NET Build Guide

**Project**: X-ray Detector Panel System
**Framework**: .NET 8.0 LTS, C# 12
**Version**: 1.0.0
**Last Updated**: 2026-02-17

---

## 1. Overview

The Host SDK (`XrayDetector.Sdk`) is the primary API for applications interacting with the X-ray Detector Panel System. It runs on the Host PC and communicates with the SoC Controller via 10 GbE UDP to receive pixel frames, send control commands, and manage frame storage.

### 1.1 SDK Architecture

```
XrayDetector.Sdk/
  IDetectorClient.cs            # Public API interface
  DetectorClient.cs             # API implementation
  Network/
    PacketReceiver.cs           # UDP packet reception
    FrameReassembler.cs         # Packet-to-frame reassembly
  Protocol/
    FrameHeader.cs              # Frame header struct (32 bytes)
    CommandPacket.cs            # Control command encoding
    Crc16.cs                    # CRC-16/CCITT
  Storage/
    TiffWriter.cs               # 16-bit TIFF output
    RawWriter.cs                # Binary RAW + JSON sidecar
  Display/
    WindowLevel.cs              # 16-bit to 8-bit mapping
  Models/
    Frame.cs                    # Frame data with IDisposable
    ScanMode.cs                 # Single, Continuous, Calibration
    ScanStatus.cs               # Runtime statistics
    DeviceInfo.cs               # Connected device metadata
    DetectorException.cs        # Domain-specific exceptions
```

### 1.2 Module Dependencies

```
Common.Dto (shared interfaces, DTOs)
    ^
    |
XrayDetector.Sdk (SDK library)
    ^
    |
XrayDetector.Sdk.Tests (unit tests)
```

The SDK depends only on `Common.Dto` for shared types. No direct dependencies on simulator projects.

---

## 2. Prerequisites

### 2.1 Required Software

| Software | Version | Purpose |
|----------|---------|---------|
| .NET SDK | 8.0 LTS | Build and run |
| Visual Studio 2022 | 17.8+ | IDE (Windows) |
| VS Code | Latest | IDE (cross-platform) |
| Git | 2.30+ | Version control |

### 2.2 Install .NET 8.0 SDK

**Windows**:
```powershell
winget install Microsoft.DotNet.SDK.8
dotnet --version
```

**Linux (Ubuntu 22.04+)**:
```bash
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0
dotnet --version
```

**macOS**:
```bash
brew install --cask dotnet-sdk
dotnet --version
```

### 2.3 Verify Installation

```bash
dotnet --list-sdks
# Expected: 8.0.xxx [/usr/share/dotnet/sdk]

dotnet --list-runtimes
# Expected: Microsoft.NETCore.App 8.0.xxx
```

---

## 3. Build the SDK

### 3.1 Quick Build

```bash
cd system-emul-sim

# Restore NuGet packages
dotnet restore sdk/XrayDetector.Sdk/XrayDetector.Sdk.csproj

# Build SDK
dotnet build sdk/XrayDetector.Sdk/XrayDetector.Sdk.csproj

# Build in Release mode
dotnet build sdk/XrayDetector.Sdk/XrayDetector.Sdk.csproj -c Release
```

### 3.2 Build SDK + Tests

```bash
# Build both SDK and test project
dotnet build sdk/XrayDetector.Sdk/XrayDetector.Sdk.csproj
dotnet build sdk/XrayDetector.Sdk.Tests/XrayDetector.Sdk.Tests.csproj

# Or build the solution (builds everything)
dotnet build
```

### 3.3 NuGet Package Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| LibTiff.NET | 2.4.6+ | 16-bit TIFF file read/write |
| System.IO.Pipelines | 8.0.0 | High-performance I/O pipelines |
| fo-dicom | 5.0+ | DICOM format output (optional) |
| xUnit | 2.6+ | Unit test framework |
| xUnit.runner.visualstudio | 2.5+ | Test runner for Visual Studio |
| FluentAssertions | 6.12+ | Expressive test assertions |
| coverlet.collector | 6.0+ | Code coverage collection |
| NSubstitute | 5.1+ | Mocking framework |

**Package Installation**:
```bash
# Add a NuGet package to the SDK project
cd sdk/XrayDetector.Sdk
dotnet add package LibTiff.Net --version 2.4.6
dotnet add package System.IO.Pipelines --version 8.0.0

# Add test packages
cd ../XrayDetector.Sdk.Tests
dotnet add package xunit --version 2.6.6
dotnet add package FluentAssertions --version 6.12.0
dotnet add package NSubstitute --version 5.1.0
dotnet add package coverlet.collector --version 6.0.1
```

---

## 4. Run Tests

### 4.1 Run All Tests

```bash
# Run tests with output
dotnet test sdk/XrayDetector.Sdk.Tests/ --logger "console;verbosity=normal"

# Run tests in verbose mode
dotnet test sdk/XrayDetector.Sdk.Tests/ -v detailed
```

### 4.2 Run Specific Tests

```bash
# Run tests matching a filter
dotnet test sdk/XrayDetector.Sdk.Tests/ --filter "FullyQualifiedName~FrameReassembler"
dotnet test sdk/XrayDetector.Sdk.Tests/ --filter "FullyQualifiedName~Crc16"
dotnet test sdk/XrayDetector.Sdk.Tests/ --filter "FullyQualifiedName~TiffWriter"

# Run tests by category
dotnet test sdk/XrayDetector.Sdk.Tests/ --filter "Category=Unit"
dotnet test sdk/XrayDetector.Sdk.Tests/ --filter "Category=Integration"
```

### 4.3 Code Coverage

```bash
# Collect coverage
dotnet test sdk/XrayDetector.Sdk.Tests/ \
    --collect:"XPlat Code Coverage" \
    --results-directory TestResults

# Install report generator (one-time)
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate HTML report
reportgenerator \
    -reports:"TestResults/*/coverage.cobertura.xml" \
    -targetdir:TestResults/CoverageReport \
    -reporttypes:Html

# Open report
# Windows:
start TestResults/CoverageReport/index.html
# Linux:
xdg-open TestResults/CoverageReport/index.html
```

**Coverage Target**: 85%+ for all new SDK code (TDD methodology)

### 4.4 Test Structure

Tests follow TDD (RED-GREEN-REFACTOR) methodology:

```csharp
// XrayDetector.Sdk.Tests/Network/FrameReassemblerTests.cs
using FluentAssertions;
using Xunit;

public class FrameReassemblerTests
{
    [Fact]
    public void Reassemble_AllPacketsInOrder_ProducesCompleteFrame()
    {
        // Arrange
        var reassembler = new FrameReassembler(maxSlots: 8, frameTimeout: TimeSpan.FromSeconds(2));
        var packets = CreateTestPackets(width: 1024, height: 1024, bitDepth: 16);

        // Act
        Frame? result = null;
        foreach (var packet in packets)
        {
            result = reassembler.AddPacket(packet);
        }

        // Assert
        result.Should().NotBeNull();
        result!.Width.Should().Be(1024);
        result.Height.Should().Be(1024);
        result.BitDepth.Should().Be(16);
        result.PixelData.Length.Should().Be(1024 * 1024);
    }

    [Fact]
    public void Reassemble_OutOfOrderPackets_ProducesIdenticalFrame()
    {
        // Arrange
        var reassembler = new FrameReassembler(maxSlots: 8, frameTimeout: TimeSpan.FromSeconds(2));
        var packets = CreateTestPackets(width: 1024, height: 1024, bitDepth: 16);
        var shuffled = packets.OrderBy(_ => Random.Shared.Next()).ToList();

        // Act
        Frame? result = null;
        foreach (var packet in shuffled)
        {
            result = reassembler.AddPacket(packet);
        }

        // Assert
        result.Should().NotBeNull();
        result!.PixelData.Should().Equal(GetExpectedPixelData(1024, 1024));
    }

    [Fact]
    public void Reassemble_MissingPackets_TimesOutAndReportsIncomplete()
    {
        // Arrange
        var reassembler = new FrameReassembler(maxSlots: 8, frameTimeout: TimeSpan.FromMilliseconds(100));
        var packets = CreateTestPackets(width: 1024, height: 1024, bitDepth: 16);
        // Remove 5% of packets
        var incomplete = packets.Where((_, i) => i % 20 != 0).ToList();

        // Act
        foreach (var packet in incomplete)
        {
            reassembler.AddPacket(packet);
        }
        Thread.Sleep(200); // Wait for timeout

        // Assert
        var frame = reassembler.GetExpiredFrames().FirstOrDefault();
        frame.Should().NotBeNull();
        frame!.IsComplete.Should().BeFalse();
    }
}
```

---

## 5. Project Configuration

### 5.1 SDK Project File

```xml
<!-- sdk/XrayDetector.Sdk/XrayDetector.Sdk.csproj -->
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>12.0</LangVersion>
    <RootNamespace>XrayDetector.Sdk</RootNamespace>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../../tools/Common.Dto/Common.Dto.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="LibTiff.Net" Version="2.4.6" />
    <PackageReference Include="System.IO.Pipelines" Version="8.0.0" />
  </ItemGroup>

</Project>
```

### 5.2 Test Project File

```xml
<!-- sdk/XrayDetector.Sdk.Tests/XrayDetector.Sdk.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../XrayDetector.Sdk/XrayDetector.Sdk.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.6" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.6" />
    <PackageReference Include="FluentAssertions" Version="6.12.0" />
    <PackageReference Include="NSubstitute" Version="5.1.0" />
    <PackageReference Include="coverlet.collector" Version="6.0.1" />
  </ItemGroup>

</Project>
```

---

## 6. API Usage Examples

### 6.1 Single Frame Capture

```csharp
using XrayDetector.Sdk;

// Create client
using var client = new DetectorClient();

// Connect to SoC
await client.ConnectAsync("192.168.1.100");

// Capture single frame
await client.StartScanAsync(ScanMode.Single);
var frame = await client.GetFrameAsync(TimeSpan.FromSeconds(5));

// Save frame
await client.SaveFrameAsync(frame, "output/frame_001.tiff", ImageFormat.Tiff);
Console.WriteLine($"Captured: {frame.Width}x{frame.Height}, {frame.BitDepth}-bit");

// Disconnect
await client.DisconnectAsync();
```

### 6.2 Continuous Streaming

```csharp
using XrayDetector.Sdk;

using var client = new DetectorClient();
await client.ConnectAsync("192.168.1.100");

// Start continuous capture
await client.StartScanAsync(ScanMode.Continuous);

// Stream frames
int count = 0;
await foreach (var frame in client.StreamFramesAsync())
{
    Console.WriteLine($"Frame {frame.SequenceNumber}: {frame.Width}x{frame.Height}");

    if (++count >= 100) break;

    frame.Dispose(); // Return buffer to pool
}

await client.StopScanAsync();
```

### 6.3 Event-Driven Reception

```csharp
using XrayDetector.Sdk;

using var client = new DetectorClient();

// Subscribe to events
client.FrameReceived += (sender, frame) =>
{
    Console.WriteLine($"Frame received: seq={frame.SequenceNumber}");
};

client.ErrorOccurred += (sender, error) =>
{
    Console.WriteLine($"Error: {error.Message} (recoverable={error.IsRecoverable})");
};

client.ConnectionChanged += (sender, state) =>
{
    Console.WriteLine($"Connection: {state}");
};

await client.ConnectAsync("192.168.1.100");
await client.StartScanAsync(ScanMode.Continuous);

// Wait for user to stop
Console.ReadLine();
await client.StopScanAsync();
```

---

## 7. Development Workflow

### 7.1 TDD Cycle

All new SDK code follows TDD (RED-GREEN-REFACTOR):

1. **RED**: Write a failing test
   ```bash
   dotnet test sdk/XrayDetector.Sdk.Tests/ --filter "NewFeatureTest"
   # Expected: FAIL
   ```

2. **GREEN**: Write minimal code to pass
   ```bash
   dotnet test sdk/XrayDetector.Sdk.Tests/ --filter "NewFeatureTest"
   # Expected: PASS
   ```

3. **REFACTOR**: Clean up while keeping tests green
   ```bash
   dotnet test sdk/XrayDetector.Sdk.Tests/
   # Expected: ALL PASS
   ```

### 7.2 Performance Considerations

- Use `ArrayPool<ushort>` for frame pixel buffers (avoid GC pressure)
- Frame implements `IDisposable` to return buffers to pool
- Receive loop runs on dedicated thread, not thread pool
- Use `System.IO.Pipelines` for zero-copy network I/O

### 7.3 Cross-Platform Notes

| Feature | Windows | Linux | macOS |
|---------|---------|-------|-------|
| SDK Library | Yes | Yes | Yes |
| Unit Tests | Yes | Yes | Yes |
| Integration Tests | Yes | Yes | Yes |
| WPF GUI | Yes | No | No |

For Linux/macOS, avoid WPF-dependent code paths. Use interface abstraction:

```csharp
// Platform-agnostic interface
public interface IFrameRenderer
{
    void Render(Frame frame);
}

// WPF implementation (Windows only)
#if WINDOWS
public class WpfFrameRenderer : IFrameRenderer { ... }
#endif
```

---

## 8. Troubleshooting

### 8.1 Build Issues

| Issue | Cause | Solution |
|-------|-------|---------|
| "SDK not found" | .NET 8.0 not installed | Install .NET 8.0 SDK |
| "Could not find project Common.Dto" | Wrong working directory | Build from project root |
| NuGet restore fails | Network/proxy issues | `dotnet nuget locals all --clear` |
| "Nullable warnings as errors" | Missing null checks | Add null checks or `?` annotations |

### 8.2 Test Issues

| Issue | Cause | Solution |
|-------|-------|---------|
| Tests not discovered | Missing test SDK reference | Add `Microsoft.NET.Test.Sdk` package |
| Coverage not collected | Missing coverlet | Add `coverlet.collector` package |
| Timeout in async tests | Default xUnit timeout | Set `[Fact(Timeout = 30000)]` |
| Socket errors in tests | Port already in use | Use random ports or mock network layer |

---

## 9. Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | MoAI Agent (architect) | Initial SDK build guide |

---
