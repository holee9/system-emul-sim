# GUI.Application

X-ray Detector Panel System - Unified WPF GUI Application

## Overview

This is the unified WPF interface for detector configuration management and monitoring (REQ-TOOLS-040). It provides real-time simulator status display, frame preview at up to 15 fps, and parameter management capabilities.

## Features

- **Status Dashboard** (REQ-TOOLS-045): Real-time display of connection state, acquisition mode, frames received, dropped frames, and throughput (Gbps)
- **Frame Preview** (REQ-TOOLS-041, REQ-TOOLS-042): 16-bit to 8-bit grayscale display with Window/Level adjustment (< 100ms response)
- **Configuration Management**: View and manage detector parameters
- **SDK Integration** (REQ-TOOLS-043): Full integration with IDetectorClient for connection management, frame acquisition, and status monitoring
- **Frame Save** (REQ-TOOLS-044): Save frames in TIFF or RAW format

## Architecture

### MVVM Pattern

- **Models**: Frame, DetectorStatus (from SDK)
- **ViewModels**: MainViewModel, StatusViewModel, FramePreviewViewModel
- **Views**: MainWindow (XAML)

### Key Components

```
src/GUI.Application/
├── App.xaml              # Application entry point
├── Core/
│   └── ObservableObject.cs  # Base class for ViewModels
├── ViewModels/
│   ├── MainViewModel.cs     # Main coordinator, SDK integration
│   ├── StatusViewModel.cs   # Status dashboard (REQ-TOOLS-045)
│   └── FramePreviewViewModel.cs  # Frame display, Window/Level (REQ-TOOLS-041, REQ-TOOLS-042)
└── Views/
    └── MainWindow.xaml      # Main window with tabbed interface

tests/GUI.Application.Tests/
└── ViewModels/
    ├── MainViewModelTests.cs
    ├── StatusViewModelTests.cs
    └── FramePreviewViewModelTests.cs
```

## Requirements Coverage

| REQ | Description | Status |
|-----|-------------|--------|
| REQ-TOOLS-040 | Unified WPF interface | ✅ Implemented |
| REQ-TOOLS-041 | Real-time status & frame preview (15 fps) | ✅ Implemented |
| REQ-TOOLS-042 | Window/Level update < 100ms | ✅ Implemented |
| REQ-TOOLS-043 | IDetectorClient integration | ✅ Implemented |
| REQ-TOOLS-044 | Frame save (TIFF/RAW) | ✅ Implemented |
| REQ-TOOLS-045 | Status dashboard | ✅ Implemented |

## Building

```bash
cd tools/GUI.Application
dotnet build GUI.Application.sln
```

## Running Tests

```bash
dotnet test tests/GUI.Application.Tests/GUI.Application.Tests.csproj
```

## Running the Application

```bash
dotnet run --project src/GUI.Application/GUI.Application.csproj
```

## Dependencies

- .NET 8.0 Windows (WPF)
- XrayDetector.Sdk (Host SDK, SPEC-SDK-001)
- CommunityToolkit.Mvvm 8.4.0

## Test Coverage

- 40 unit tests passing
- ViewModels tested with TDD approach
- Mock-based testing with Moq
- FluentAssertions for readable assertions

## Technical Specifications

- **Framework**: WPF on .NET 8.0
- **Pattern**: MVVM with ObservableObject base
- **Threading**: UI thread updates via Dispatcher
- **Frame Preview**: WriteableBitmap for 16-bit to 8-bit conversion
- **Window/Level Mapping**: Using SDK's WindowLevelMapper (REQ-SDK-042)

## Future Enhancements

- Dark theme support (REQ-TOOLS-061, optional)
- Parameter extraction tab integration (ParameterExtractor)
- Integration test execution tab (IntegrationRunner)
- Code generation tab integration (CodeGenerator)

## Version

1.0.0

## Authors

System Emul-Sim Team
