# Common.Dto API Documentation

## Overview

Common.Dto provides the foundational data structures and interfaces for the X-ray Detector Panel System simulator suite. All components implement comprehensive validation, serialization support, and XML documentation.

## Interfaces

### ISimulator

Defines the standard interface for all simulator implementations.

**Requirement**: REQ-SIM-050

```csharp
public interface ISimulator
{
    void Initialize(object config);
    object Process(object input);
    void Reset();
    string GetStatus();
}
```

#### Methods

| Method | Description |
|--------|-------------|
| `Initialize(object config)` | Initializes the simulator with the specified configuration |
| `Process(object input)` | Processes the input data through the simulator and returns output |
| `Reset()` | Resets the simulator to its initial state |
| `GetStatus()` | Gets the current status of the simulator as a string |

## Data Transfer Objects

### FrameData

Represents a complete frame of pixel data from the X-ray detector panel.

**Requirement**: REQ-SIM-051

```csharp
public record FrameData
{
    int FrameNumber { get; init; }
    int Width { get; init; }
    int Height { get; init; }
    ushort[] Pixels { get; init; }
}
```

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `FrameNumber` | int | Sequential frame identifier |
| `Width` | int | Frame width in pixels (must be positive) |
| `Height` | int | Frame height in pixels (must be positive) |
| `Pixels` | ushort[] | Pixel data array (16-bit values, must match Width × Height) |

#### Constructor

```csharp
public FrameData(int frameNumber, int width, int height, ushort[] pixels)
```

**Validation Rules**:
- `width` must be positive
- `height` must be positive
- `pixels` array length must equal `width × height`
- `pixels` cannot be null

#### Serialization

JSON serialization is supported via `System.Text.Json`:

```json
{
  "frameNumber": 1,
  "width": 1024,
  "height": 1024,
  "pixels": [1000, 1005, 1010, ...]
}
```

#### Usage Example

```csharp
// Create a frame
var frame = new FrameData(
    frameNumber: 1,
    width: 1024,
    height: 1024,
    pixels: new ushort[1024 * 1024]
);

// Serialize
var json = JsonSerializer.Serialize(frame);

// Deserialize
var deserialized = JsonSerializer.Deserialize<FrameData>(json);
```

---

### LineData

Represents a single line of pixel data from the X-ray detector panel.

**Requirement**: REQ-SIM-051

```csharp
public record LineData
{
    int FrameNumber { get; init; }
    int LineNumber { get; init; }
    ushort[] Pixels { get; init; }
}
```

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `FrameNumber` | int | Parent frame identifier |
| `LineNumber` | int | Line number within the frame |
| `Pixels` | ushort[] | Pixel data for the line (16-bit values) |

#### Validation Rules

- `lineNumber` must be non-negative
- `pixels` cannot be null or empty

---

### Csi2Packet

Represents a MIPI CSI-2 packet transmitted between FPGA and SoC.

**Requirement**: REQ-SIM-051

```csharp
public record Csi2Packet
{
    Csi2DataType DataType { get; init; }
    int VirtualChannel { get; init; }
    byte[] Payload { get; init; }
}
```

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `DataType` | Csi2DataType | CSI-2 data type format |
| `VirtualChannel` | int | Virtual channel number (0-3) |
| `Payload` | byte[] | Packet payload data |

#### Constructor

```csharp
public Csi2Packet(Csi2DataType dataType, int virtualChannel, byte[] payload)
```

**Validation Rules**:
- `virtualChannel` must be between 0 and 3
- `payload` cannot be null or empty

#### Csi2DataType Enumeration

| Value | Hex | Description |
|-------|-----|-------------|
| `Raw8` | 0x30 | Raw 8-bit data |
| `Raw10` | 0x31 | Raw 10-bit data |
| `Raw12` | 0x32 | Raw 12-bit data |
| `Raw14` | 0x33 | Raw 14-bit data |
| `Raw16` | 0x34 | Raw 16-bit data (default for X-ray detector) |
| `Yuv4228Bit` | 0x1E | YUV422 8-bit data |
| `Rgb565` | 0x22 | RGB565 data |

#### Usage Example

```csharp
// Create CSI-2 packet with RAW16 data
var packet = new Csi2Packet(
    dataType: Csi2DataType.Raw16,
    virtualChannel: 0,
    payload: new byte[] { 0x12, 0x34, 0x56, 0x78 }
);

// Serialize
var json = JsonSerializer.Serialize(packet);
// Output: {"dataType":"Raw16","virtualChannel":0,"payload":[18,52,86,120]}
```

---

### UdpPacket

Represents a network packet structure for SoC-Host communication.

**Requirement**: REQ-SIM-051

```csharp
public record UdpPacket
{
    int SourcePort { get; init; }
    int DestinationPort { get; init; }
    byte[] Data { get; init; }
}
```

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `SourcePort` | int | Source port number |
| `DestinationPort` | int | Destination port number |
| `Data` | byte[] | Packet payload data |

#### Validation Rules

- `sourcePort` must be in valid port range (1-65535)
- `destinationPort` must be in valid port range (1-65535)
- `data` cannot be null

---

### SpiTransaction

Represents an SPI transaction structure for Host-FPGA control interface.

**Requirement**: REQ-SIM-051

```csharp
public record SpiTransaction
{
    SpiCommandType Command { get; init; }
    byte[] Data { get; init; }
}
```

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Command` | SpiCommandType | SPI command type |
| `Data` | byte[] | Transaction data |

#### Validation Rules

- `data` cannot be null

#### SpiCommandType Enumeration

| Value | Description |
|-------|-------------|
| `Read` | Read from FPGA register |
| `Write` | Write to FPGA register |
| `Reset` | Reset FPGA state |

---

## Design Principles

### Immutability

All DTOs use C# `record` type, providing:
- Thread-safe immutable data structures
- Built-in equality comparison
- Concise syntax

### Validation

Constructor validation ensures data integrity:
- All validation occurs at object creation time
- Invalid data throws descriptive exceptions
- No invalid objects can exist in the system

### Serialization

JSON serialization support via `System.Text.Json`:
- `JsonPropertyName` attributes for consistent property naming
- `JsonStringEnumConverter` for readable enum values
- Default serialization for all DTOs

### Documentation

Comprehensive XML documentation comments:
- Every public type documented
- Every public member documented
- Requirement references included
- Usage examples provided

---

## Testing

### Test Coverage

- **Coverage**: 97.08%
- **Tests**: 53 passing
- **Test Files**: 7

### Running Tests

```bash
# Run all tests
dotnet test tools/Common/tests/Common.Dto.Tests/

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test file
dotnet test --filter "FullyQualifiedName~FrameDataTests"
```

### Test Categories

| Test File | Description |
|-----------|-------------|
| `FrameDataTests.cs` | FrameData validation and serialization |
| `LineDataTests.cs` | LineData validation and serialization |
| `Csi2PacketTests.cs` | Csi2Packet validation and CSI-2 format handling |
| `UdpPacketTests.cs` | UdpPacket validation and network format handling |
| `SpiTransactionTests.cs` | SpiTransaction validation and SPI protocol handling |
| `Csi2DataTypeTests.cs` | Csi2DataType enumeration tests |
| `SerializationTests.cs` | JSON serialization/deserialization tests |

---

## Requirements Coverage

| Requirement | Status | Implementation |
|-------------|--------|----------------|
| REQ-SIM-050 | ✅ Complete | ISimulator interface defined |
| REQ-SIM-051 | ✅ Complete | All DTOs implemented (FrameData, LineData, Csi2Packet, UdpPacket, SpiTransaction) |
| REQ-SIM-052 | ✅ Complete | Validation and serialization implemented for all DTOs |
| REQ-SIM-053 | ✅ Complete | 97.08% code coverage (exceeds 85% target) |

---

## Dependencies

- .NET 8.0+
- System.Text.Json (built-in)

---

## Related Documentation

- [Common.Dto README](../../tools/Common/README.md)
- [SPEC-SIM-001](../../.moai/specs/SPEC-SIM-001/spec.md) - Simulator Suite Requirements
- [Common.Core API Documentation](Common.Core.md) - Shared utilities

---

**Last Updated**: 2026-02-17
**Version**: 0.1.0-alpha
**Copyright**: (c) 2026 ABYZ-Lab
