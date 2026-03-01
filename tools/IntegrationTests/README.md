# Integration Tests

X-ray Detector Panel System integration test suite. Validates end-to-end system operation across the 3-tier architecture (FPGA → SoC → Host PC).

## Test Framework

- **Framework**: xUnit 2.9.0
- **Target**: .NET 8.0 LTS
- **Coverage Target**: ≥85%
- **Dependencies**: FluentAssertions 6.12.0, Moq 4.20.70
- **Total Tests**: 413 (all projects combined)
- **Pass Rate**: 100% (4 skipped for CI stability)

## Project Structure

```
tools/IntegrationTests/
├── Helpers/                    # Common test utilities
│   ├── TestFrameFactory.cs     # Test frame generation
│   ├── PacketFactory.cs        # CSI-2/Ethernet packets
│   ├── SimulatorPipelineBuilder.cs # Pipeline setup
│   ├── LatencyMeasurer.cs      # Latency analysis
│   └── HMACTestHelper.cs       # HMAC test vectors
├── Integration/                # Integration test scenarios
│   ├── It01FullPipelineTests.cs
│   ├── It02PerformanceTargetTierTests.cs
│   ├── It04Csi2ProtocolValidationTests.cs
│   ├── IT03_SpiConfigurationTests.cs
│   ├── IT05_FrameBufferOverflowTests.cs
│   ├── IT06_HmacAuthenticationTests.cs
│   ├── IT07_SequenceEngineTests.cs
│   ├── IT08_PacketLossRetransmissionTests.cs
│   ├── IT09_MaximumTierStressTests.cs
│   └── IT10_LatencyMeasurementTests.cs
└── IntegrationTests.csproj
```

## Running Tests

### All Tests
```bash
cd tools/IntegrationTests
dotnet test
```

### Specific Test
```bash
dotnet test --filter "FullyQualifiedName~IT01"
```

### With Coverage
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## Integration Test Scenarios

| ID | Description | Tier | Validation |
|----|-------------|------|------------|
| IT-01 | Single Frame Capture | Minimum (1024×1024@15fps) | Frame integrity, CRC-16 |
| IT-02 | Continuous 300 Frames | Target (2048×2048@30fps) | Throughput, frame drops |
| IT-03 | SPI Configuration | N/A | Register read/write |
| IT-04 | CSI-2 Protocol | N/A | Headers, CRC, payload |
| IT-05 | Frame Buffer Overflow | Target | Recovery, no deadlock |
| IT-06 | HMAC Authentication | N/A | Valid/invalid HMAC rejection |
| IT-07 | Sequence Engine FSM | N/A | State transitions |
| IT-08 | Packet Loss Recovery | Target | Retransmission, 2s timeout |
| IT-09 | Maximum Tier Stress | Maximum (3072×3072@30fps) | Stability, 60s duration |
| IT-10 | End-to-End Latency | Target | p95 < 50ms |

## Performance Tiers

| Tier | Resolution | Frame Rate | Data Rate |
|------|------------|------------|-----------|
| Minimum | 1024×1024 | 15 fps | 0.21 Gbps |
| Target | 2048×2048 | 30 fps | 2.01 Gbps |
| Maximum | 3072×3072 | 30 fps | 4.53 Gbps |

## Acceptance Criteria

- All tests pass (100% success rate)
- Code coverage ≥85%
- Frame drop rate <1%
- End-to-end latency p95 <50ms
- TRUST 5 compliance

## TRUST 5 Quality Framework

- **Tested**: Unit tests pass, coverage ≥85%
- **Readable**: Clear naming (Describe_When_Then), English comments
- **Unified**: C# coding style, xUnit patterns
- **Secured**: No credentials, HMAC validation tested
- **Trackable**: Git-tracked, structured output

## References

- SPEC Document: `.moai/specs/SPEC-INTEG-001/spec.md`
- CSI-2 Protocol: `docs/api/csi2-protocol.md`
- Ethernet Protocol: `docs/api/ethernet-protocol.md`
