# Host SDK Build and Test Guide

**Document Version**: 1.0.0
**Status**: Reviewed - Approved
**Last Updated**: 2026-02-17

---

## Prerequisites

### Required Software

| Software | Version | Purpose |
|----------|---------|---------|
| .NET SDK | 8.0 LTS | C# build toolchain |
| Visual Studio 2022 | 17.8+ | IDE with WPF designer (Windows) |
| VS Code | Latest | Cross-platform editor |
| ReportGenerator | Latest | HTML coverage report generation |

Install .NET 8.0 SDK (Windows):

```powershell
winget install Microsoft.DotNet.SDK.8
dotnet --version
# Expected: 8.0.x
```

Install .NET 8.0 SDK (Linux):

```bash
sudo apt-get install -y dotnet-sdk-8.0
dotnet --version
```

Install ReportGenerator globally:

```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
```

---

## Setup

### Project Structure Overview

```
sdk/
├── XrayDetector.SDK/          # Core SDK library (.NET 8.0, C# 12)
│   ├── IDetectorClient.cs     # Public API interface
│   ├── DetectorClient.cs      # API implementation
│   ├── Network/               # UDP packet reception, frame reassembly
│   ├── Protocol/              # Frame headers, command packets, CRC-16
│   ├── Storage/               # TIFF and RAW file writers
│   ├── Display/               # 16-bit to 8-bit window/level mapping
│   └── Models/                # Frame, ScanMode, DeviceInfo, exceptions
├── XrayDetector.SDK.Tests/    # xUnit test project
│   ├── Unit/                  # Unit tests (no hardware required)
│   └── Integration/           # Integration tests (requires simulator)
├── XrayDetector.GUI/          # WPF GUI application (Windows only)
│   ├── App.xaml               # Application entry point
│   ├── MainWindow.xaml        # Main window
│   └── Views/                 # MVVM views
└── XrayDetector.SDK.sln       # Solution file
```

### Open in Visual Studio 2022

1. Launch Visual Studio 2022.
2. File > Open > Project/Solution.
3. Select `sdk/XrayDetector.SDK.sln`.
4. Wait for NuGet package restore to complete.
5. Build > Build Solution (Ctrl+Shift+B).

### Open in VS Code

```bash
cd sdk
code .
```

Install the C# Dev Kit extension when prompted.

---

## Build

### Build from CLI

```bash
cd sdk

# Restore all NuGet packages
dotnet restore

# Build in Release configuration
dotnet build --configuration Release
```

Expected output:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### Build Individual Projects

```bash
# Build only the SDK library
dotnet build XrayDetector.SDK/XrayDetector.SDK.csproj --configuration Release

# Build the GUI (Windows only)
dotnet build XrayDetector.GUI/XrayDetector.GUI.csproj --configuration Release
```

---

## Test

### Running Tests with Coverage

```bash
dotnet test --collect:"XPlat Code Coverage" \
    --results-directory ./coverage \
    -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura
```

### Generate HTML Coverage Report

```bash
reportgenerator \
    -reports:coverage/**/coverage.cobertura.xml \
    -targetdir:coverage/report \
    -reporttypes:Html

# Open the report
# Windows
start coverage/report/index.html
# Linux
xdg-open coverage/report/index.html
```

Coverage target: >= 85% line coverage for `XrayDetector.SDK`.

### Run Specific Test Category

```bash
# Run only unit tests (no hardware required)
dotnet test --filter "Category=Unit"

# Run integration tests (requires simulator or hardware)
dotnet test --filter "Category=Integration"

# Run tests matching a name pattern
dotnet test --filter "FullyQualifiedName~FrameReassembler"
```

### Run Tests in Verbose Mode

```bash
dotnet test --logger "console;verbosity=detailed" \
    --filter "Category=Unit"
```

---

## Build Artifacts

### Building a NuGet Package

```bash
dotnet pack XrayDetector.SDK/XrayDetector.SDK.csproj \
    --configuration Release \
    --output ./packages
```

The output package appears at `packages/XrayDetector.SDK.1.0.0.nupkg`.

### Code Style Enforcement

Check for formatting issues without applying changes:

```bash
dotnet format --verify-no-changes
```

Apply formatting automatically:

```bash
dotnet format
```

### Publishing the GUI Application

Publish a self-contained single-file executable for Windows x64:

```bash
dotnet publish XrayDetector.GUI \
    -r win-x64 \
    --self-contained \
    -p:PublishSingleFile=true \
    -o ./publish/win-x64
```

The published executable is at `publish/win-x64/XrayDetector.GUI.exe`.

Publish for Linux x64:

```bash
dotnet publish XrayDetector.SDK \
    -r linux-x64 \
    --self-contained \
    -p:PublishSingleFile=true \
    -o ./publish/linux-x64
```

---

## Integration Testing with Simulator

To run integration tests, start the HostSimulator as a mock detector backend:

```bash
# Terminal 1: Start mock detector simulator
cd /path/to/system-emul-sim
dotnet run --project tools/HostSimulator -- --mode server --port 5001

# Terminal 2: Run integration tests against simulator
cd sdk
dotnet test --filter "Category=Integration"
```

---

## Troubleshooting

### NuGet Package Issues

| Issue | Cause | Solution |
|-------|-------|---------|
| Package restore fails | No network access or wrong feed | Check proxy, run `dotnet nuget locals all --clear` |
| Package version conflict | Multiple versions of same package | Add `<PackageReference>` with specific version, run `dotnet restore` |
| Package not found | Private feed not configured | Add NuGet feed to `NuGet.config` |

### Build Failures

| Issue | Cause | Solution |
|-------|-------|---------|
| `CS8618: Non-nullable property must contain a non-null value` | Nullable reference types enabled | Initialize the property or mark nullable with `?` |
| `CS0246: The type or namespace could not be found` | Missing using directive or package reference | Add `using` statement or add NuGet package |
| WPF compile error on Linux | WPF is Windows-only | Use `dotnet build` only on Windows for GUI project |

---

## Common Errors

| Error | Context | Meaning | Fix |
|-------|---------|---------|-----|
| `MSBUILD: error MSB1003` | CLI build | Solution or project file not found | Run from the `sdk/` directory |
| `error CS0234: namespace does not exist` | Compile | Missing NuGet package | Run `dotnet restore` |
| `System.IO.FileNotFoundException: Could not load file or assembly` | Runtime | Missing runtime dependency | Run `dotnet publish --self-contained` |
| `xUnit1004` test warning | Test | Test method not returning Task | Change test return type to `async Task` if async |
| `No test is available` | Test filter | Filter matched nothing | Check filter syntax: `dotnet test --filter "Category=Unit"` |
| Coverage report shows 0% | No coverage data | `--collect` flag missing | Include `--collect:"XPlat Code Coverage"` in test command |

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-02-17 | ABYZ-Lab Agent | Complete Host SDK build and test guide |
| 1.0.1 | 2026-02-17 | manager-docs (doc-approval-sprint) | Reviewed → Approved. No technical corrections required. |

---

## Review Notes

**TRUST 5 Assessment**

- **Testable (4/5)**: All build and test commands are verifiable. Coverage target (85%) and test filter syntax are clearly specified. Minor gap: no explicit verification command for NuGet package integrity.
- **Readable (5/5)**: Well-structured with clear section headings, consistent table formatting, and descriptive command examples throughout.
- **Unified (5/5)**: Consistent use of dotnet CLI conventions, uniform table layout, and aligned code block formatting across all sections.
- **Secured (4/5)**: No credential exposure. Covers self-contained publish to minimize runtime dependencies. Does not address code signing for published executables.
- **Trackable (4/5)**: Revision history present. Integration test dependency on HostSimulator is documented. Does not reference specific SPEC document.

**Corrections Applied**

None required.

**Minor Observations (non-blocking)**

- The HostSimulator integration test section uses `--port 5001` which is a mock internal port; the canonical SDK data port (UDP 8000) and control port (TCP 8001) are not referenced here. This is acceptable for a build guide but could be cross-referenced to the user manual for clarity.
