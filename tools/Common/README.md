# Common.Dto

Data Transfer Objects (DTOs) and interfaces for the X-ray Detector Panel System simulator suite.

## Overview

Common.Dto provides the foundational data structures and interfaces used across all simulator components (FPGA, SoC, Host). This ensures type safety, data consistency, and seamless communication between simulator modules.

## Components

### Interfaces

- **ISimulator** - Standard interface for all simulator implementations

### Data Transfer Objects

- **FrameData** - Complete frame of pixel data from X-ray detector panel
- **LineData** - Single line of pixel data
- **Csi2Packet** - MIPI CSI-2 packet structure for FPGA-SoC communication
- **UdpPacket** - Network packet structure for SoC-Host communication
- **SpiTransaction** - SPI transaction structure for Host-FPGA control interface

## Requirements Coverage

- REQ-SIM-050: Common.Dto shall define the ISimulator interface
- REQ-SIM-051: Common.Dto shall define data transfer objects including FrameData, Csi2Packet, UdpPacket, SpiTransaction
- REQ-SIM-052: All DTOs shall implement validation and serialization
- REQ-SIM-053: Common.Dto shall achieve 85%+ code coverage

## Design Principles

1. **Immutability** - All DTOs use C# `record` type for thread-safe, immutable data structures
2. **Validation** - Constructor validation ensures data integrity at creation time
3. **Serialization** - JSON serialization support via System.Text.Json
4. **Documentation** - Comprehensive XML documentation comments for API reference

## Usage Examples

### FrameData

```csharp
// Create a 1024x1024 frame with 16-bit pixel data
var frame = new FrameData(
    frameNumber: 1,
    width: 1024,
    height: 1024,
    pixels: new ushort[1024 * 1024]
);

// Serialize to JSON
var json = JsonSerializer.Serialize(frame);

// Deserialize from JSON
var deserialized = JsonSerializer.Deserialize<FrameData>(json);
```

### Csi2Packet

```csharp
// Create CSI-2 packet with RAW16 data
var packet = new Csi2Packet(
    dataType: Csi2DataType.Raw16,
    virtualChannel: 0,
    payload: new byte[] { 0x12, 0x34, 0x56, 0x78 }
);

// Virtual channel validation (0-3) enforced
// Payload validation (non-empty) enforced
```

### ISimulator

```csharp
public class FpgaSimulator : ISimulator
{
    public void Initialize(object config)
    {
        // Initialize simulator with configuration
    }

    public object Process(object input)
    {
        // Process input data and return output
        return new FrameData(...);
    }

    public void Reset()
    {
        // Reset to initial state
    }

    public string GetStatus()
    {
        return "Ready";
    }
}
```

## API Documentation

All public APIs are documented with XML comments. Generate API documentation using:

```bash
dotnet build /doc:api.xml
```

## Testing

Run tests using:

```bash
dotnet test tools/Common/tests/Common.Dto.Tests/
```

View coverage report:

```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Dependencies

- .NET 8.0+
- System.Text.Json (built-in)

## License

Copyright (c) 2026 ABYZ-Lab

## Related Components

- Common.Core - Shared utilities and extensions
- Common.Interfaces - Additional interfaces for simulator coordination
- Simulator.Fpga - FPGA simulator implementation
- Simulator.Soc - SoC simulator implementation
