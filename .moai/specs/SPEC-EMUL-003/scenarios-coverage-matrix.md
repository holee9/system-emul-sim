# SPEC-EMUL-003: Scenario Coverage Matrix

Generated: 2026-03-10
Total Scenarios: 168
Source: `.moai/specs/SPEC-EMUL-001/scenarios.md`

## Coverage Legend

| Status | Meaning |
|--------|---------|
| COVERED | Test exists and directly exercises this scenario |
| PARTIAL | Test partially covers this scenario or covers a related aspect |
| NOT_COVERED | No test exists for this scenario |

---

## 1. Panel (X-ray) Scenarios (P-01 to P-22)

### 1-1. X-ray Physics Response

| ID | Scenario | Covering Test(s) | Status |
|----|----------|-----------------|--------|
| P-01 | kVp signal level variation | IT17: `ScintillatorModel_SignalProportionalToKvp` (60 vs 120 kVp) | COVERED |
| P-02 | mAs linearity | IT17: `ScintillatorModel_SignalProportionalToKvp` (uses mAs=10 fixed) | PARTIAL |
| P-03 | Exposure time vs signal | IT17: `ExposureModel_ScalesWithGatePulse` (gate pulse variation) | PARTIAL |
| P-04 | Dark frame (no exposure) | IT17: `PhysicsChain_EndToEnd_ProducesRealisticOutput` (dark current) | PARTIAL |
| P-05 | Saturation limit | IT18: `ScintillatorModel_SaturationAt65535` | COVERED |

### 1-2. Noise Characteristics

| ID | Scenario | Covering Test(s) | Status |
|----|----------|-----------------|--------|
| P-06 | Poisson statistical noise | IT17: `CompositeNoise_StatisticalDistribution` (EnablePoissonNoise=true) | PARTIAL |
| P-07 | Gaussian electronic noise | IT17: `CompositeNoise_StatisticalDistribution` (EnableGaussianNoise=true) | PARTIAL |
| P-08 | Dark current temperature dependence | IT18: `DarkCurrentFrame_ProducesNonZeroSignal` | PARTIAL |
| P-09 | 1/f (Flicker) noise | IT17: `CompositeNoise_StatisticalDistribution` (EnableFlickerNoise=false) | NOT_COVERED |
| P-10 | Composite noise accuracy | IT17: `CompositeNoise_StatisticalDistribution` | PARTIAL |

### 1-3. Calibration Data

| ID | Scenario | Covering Test(s) | Status |
|----|----------|-----------------|--------|
| P-11 | Dark calibration | IT18: `CalibrationFrameGenerator_DarkFrameProducesRealisticNoise` | COVERED |
| P-12 | Flatfield calibration | IT18: `CalibrationFrameGenerator_FlatFieldFrameMeanApproximatesTarget` | COVERED |
| P-13 | Offset calibration | IT18: `CalibrationFrameGenerator_BiasFrameHasLowerMeanThanDark` | PARTIAL |
| P-14 | Defect pixel map | (PanelSimulator DefectMap used in pipeline tests) | PARTIAL |

### 1-4. Temporal Effects

| ID | Scenario | Covering Test(s) | Status |
|----|----------|-----------------|--------|
| P-15 | Ghosting | IT17: `LagModel_DecaysOverFrames` | COVERED |
| P-16 | Lag quantification | IT17: `LagModel_DecaysOverFrames` | COVERED |
| P-17 | Temperature drift | IT18: `DriftModel_LinearDriftIncreasesOverTime` | COVERED |
| P-18 | Long-term stability | IT09: stress tests (1000+ frames) | PARTIAL |

### 1-5. Gate/ROIC Interaction

| ID | Scenario | Covering Test(s) | Status |
|----|----------|-----------------|--------|
| P-19 | Gate ON/OFF response | IT18: `GateResponseModel_GateOffProducesDarkOnly`, `GateResponseModel_GateOnProducesHigherSignal` | COVERED |
| P-20 | Row-by-row ROIC readout | IT18: `RoicReadoutModel_ReadsAllRowsSequentially` | COVERED |
| P-21 | ROIC settle time impact | IT18: `RoicReadoutModel_SettleTimeAffectsReadoutTime` | COVERED |
| P-22 | Calibration mode readout | IT18: `CalibrationFrameGenerator_AveragedDarkReducesNoise` | PARTIAL |

### Panel Category Summary

| Category | Total | COVERED | PARTIAL | NOT_COVERED |
|----------|-------|---------|---------|-------------|
| X-ray Physics | 5 | 1 | 3 | 1 |
| Noise | 5 | 0 | 4 | 1 |
| Calibration | 4 | 2 | 2 | 0 |
| Temporal | 4 | 2 | 2 | 0 |
| Gate/ROIC | 4 | 3 | 1 | 0 |
| **Total** | **22** | **8** | **12** | **2** |

---

## 2. FPGA (Artix-7) Scenarios (F-01 to F-36)

### 2-1. FSM State Transitions

| ID | Scenario | Covering Test(s) | Status |
|----|----------|-----------------|--------|
| F-01 | Normal single scan cycle | IT14: `SequenceEngine_SingleScan_CompletesFullCycle` | COVERED |
| F-02 | Continuous scan cycle | IT14: `SequenceEngine_ContinuousScan_RepeatsAutomatically` | COVERED |
| F-03 | Calibration mode | IT03: SPI config tests, IT07 sequence tests | PARTIAL |
| F-04 | Forced stop | IT14: `SequenceEngine_StopScan_ReturnsToIdle` | COVERED |
| F-05 | Duplicate start command | IT07: `SequenceEngine_StartDuringScanning_ReturnsError` | COVERED |
| F-06 | Error from all states | IT16: `ProtectionLogic_ErrorFromAnyState` | COVERED |

### 2-2. Control Signal Timing

| ID | Scenario | Covering Test(s) | Status |
|----|----------|-----------------|--------|
| F-07 | Gate ON pulse width | IT03: SPI gate_on register tests | PARTIAL |
| F-08 | Gate OFF interval | IT03: SPI gate_off register tests | PARTIAL |
| F-09 | ROIC Sync timing | IT11: pipeline timing verification | PARTIAL |
| F-10 | Line Valid signal | IT04: CSI-2 line data tests | PARTIAL |
| F-11 | Settle/ADC timer separation | IT03: settle time register tests | PARTIAL |
| F-12 | Frame Valid signal | It04Csi2ProtocolValidationTests | PARTIAL |

### 2-3. Protection Logic

| ID | Scenario | Covering Test(s) | Status |
|----|----------|-----------------|--------|
| F-13 | Watchdog timeout | IT16: `ProtectionLogic_WatchdogTimeout_TriggersError` | COVERED |
| F-14 | Watchdog normal reset | IT16: `ProtectionLogic_WatchdogNormal_NoError` | COVERED |
| F-15 | Readout timeout | IT16: `ProtectionLogic_ReadoutTimeout_TriggersError` | COVERED |
| F-16 | Overflow error | IT05, IT15: buffer overflow tests | COVERED |
| F-17 | CRC error detection | It04Csi2ProtocolValidationTests | PARTIAL |
| F-18 | Safe shutdown timing | IT16: `ProtectionLogic_SafeShutdown_WithinTenClocks` | COVERED |
| F-19 | Error clear | IT16: `ProtectionLogic_ErrorClear_ReturnsToIdle` | COVERED |
| F-20 | Multiple simultaneous errors | IT16: `ProtectionLogic_MultipleErrors_AllFlagsLatched` | COVERED |

### 2-4. SPI Register

| ID | Scenario | Covering Test(s) | Status |
|----|----------|-----------------|--------|
| F-21 | STATUS real-time reflection | IT03: SPI status tests | COVERED |
| F-22 | Read-only protection | IT03: `SpiConfig_WriteToReadOnly_Rejected` | COVERED |
| F-23 | CONTROL bit decoding | IT03: CONTROL register tests | COVERED |
| F-24 | Frame counter 32-bit | IT03: frame counter register tests | COVERED |
| F-25 | Unmapped address read | IT03: unmapped address tests | COVERED |
| F-26 | ILA capture | IT16: error injection tests capture ILA data | PARTIAL |

### 2-5. Line Buffer

| ID | Scenario | Covering Test(s) | Status |
|----|----------|-----------------|--------|
| F-27 | Ping-Pong bank alternation | IT05: FrameBuffer ping-pong tests | COVERED |
| F-28 | Overflow detection | IT05, IT15: overflow detection tests | COVERED |
| F-29 | Maximum width test | IT11: 256x256 pipeline (not 3072 max) | PARTIAL |
| F-30 | CDC delay effect | IT05: CDC delay simulation | PARTIAL |

### 2-6. CSI-2 TX

| ID | Scenario | Covering Test(s) | Status |
|----|----------|-----------------|--------|
| F-31 | Full packet sequence | IT11, It04Csi2ProtocolValidationTests | COVERED |
| F-32 | ECC accuracy | It04Csi2ProtocolValidationTests | COVERED |
| F-33 | CRC-16 accuracy | It04Csi2ProtocolValidationTests | COVERED |
| F-34 | Virtual Channel | It04Csi2ProtocolValidationTests | PARTIAL |
| F-35 | Backpressure | (not implemented) | NOT_COVERED |
| F-36 | Backpressure release | (not implemented) | NOT_COVERED |

### FPGA Category Summary

| Category | Total | COVERED | PARTIAL | NOT_COVERED |
|----------|-------|---------|---------|-------------|
| FSM States | 6 | 5 | 1 | 0 |
| Control Timing | 6 | 0 | 6 | 0 |
| Protection | 8 | 7 | 1 | 0 |
| SPI Register | 6 | 5 | 1 | 0 |
| Line Buffer | 4 | 2 | 2 | 0 |
| CSI-2 TX | 6 | 3 | 1 | 2 |
| **Total** | **36** | **22** | **12** | **2** |

---

## 3. MCU/SoC (i.MX8MP) Scenarios (M-01 to M-38)

### 3-1. SequenceEngine FSM

| ID | Scenario | Covering Test(s) | Status |
|----|----------|-----------------|--------|
| M-01 | Single scan full cycle | IT07, IT14: full cycle tests | COVERED |
| M-02 | Continuous scan N frames | IT07: `SequenceEngine_ContinuousScan_10Frames` | COVERED |
| M-03 | Calibration scan | IT07: calibration mode tests | COVERED |
| M-04 | Invalid start | IT07: `SequenceEngine_InvalidStart_ReturnsError` | COVERED |
| M-05 | Error recovery (1 retry) | IT14: `SequenceEngine_ErrorRecovery_OneRetry` | COVERED |
| M-06 | Error recovery (3 exhausted) | IT14: `SequenceEngine_ErrorRecovery_ThreeExhausted` | COVERED |
| M-07 | Emergency stop | IT14: `SequenceEngine_EmergencyStop_FromAnyState` | COVERED |
| M-08 | Full 56-transition table | IT14: transition table tests | PARTIAL |

### 3-2. FrameBufferManager

| ID | Scenario | Covering Test(s) | Status |
|----|----------|-----------------|--------|
| M-09 | Normal 1-frame cycle | IT05, IT15: buffer lifecycle tests | COVERED |
| M-10 | 4-buffer sequential use | IT05: `FrameBuffer_FourBuffers_AllCycled` | COVERED |
| M-11 | 5th frame (oldest-drop) | IT05, IT15: oldest-drop tests | COVERED |
| M-12 | Producer faster than Consumer | IT09: stress tests | PARTIAL |
| M-13 | Consumer faster than Producer | IT05: empty buffer tests | COVERED |
| M-14 | Concurrency safety | IT05: multi-thread tests | COVERED |
| M-15 | GetReady on empty | IT05: `FrameBuffer_GetReadyOnEmpty_ReturnsNull` | COVERED |
| M-16 | Statistics accuracy | IT05: statistics accuracy tests | COVERED |
| M-17 | Drop during SENDING | IT15: `FrameBuffer_DropDuringSending_SendingSkipped` | COVERED |

### 3-3. HealthMonitor

| ID | Scenario | Covering Test(s) | Status |
|----|----------|-----------------|--------|
| M-18 | Watchdog normal | IT12: `HealthMonitor_WatchdogNormal_IsAlive` | COVERED |
| M-19 | Watchdog timeout | IT12: `HealthMonitor_WatchdogTimeout_NotAlive` | COVERED |
| M-20 | Stat counter update | IT12: stat counter tests | COVERED |
| M-21 | 9 counter independence | IT12: counter independence tests | COVERED |
| M-22 | GetStatus response time | IT10: latency measurement tests | COVERED |
| M-23 | Log level filtering | IT12: log level tests | PARTIAL |
| M-24 | System status snapshot | IT12: status snapshot tests | COVERED |

### 3-4. CommandProtocol

| ID | Scenario | Covering Test(s) | Status |
|----|----------|-----------------|--------|
| M-25 | Valid HMAC command | IT06: `HmacAuth_ValidCommand_Dispatched` | COVERED |
| M-26 | Invalid HMAC command | IT06: `HmacAuth_InvalidCommand_Rejected` | COVERED |
| M-27 | Replay attack | IT06: `HmacAuth_ReplayAttack_Rejected` | COVERED |
| M-28 | All command types | IT06: all command type tests | COVERED |
| M-29 | Payload tampering | IT06: `HmacAuth_PayloadTamper_Detected` | COVERED |
| M-30 | Consecutive auth failures | IT06: `HmacAuth_ConsecutiveFailures_Counted` | COVERED |

### 3-5. SPI Master

| ID | Scenario | Covering Test(s) | Status |
|----|----------|-----------------|--------|
| M-31 | FPGA register write | IT03: SPI write tests | COVERED |
| M-32 | FPGA register read | IT03: SPI read tests | COVERED |
| M-33 | FPGA config sequence | IT03: config sequence tests | COVERED |
| M-34 | SPI error counting | IT03: error counting tests | COVERED |

### 3-6. MCU Integration

| ID | Scenario | Covering Test(s) | Status |
|----|----------|-----------------|--------|
| M-35 | Full frame processing | IT11, IT13: full pipeline tests | COVERED |
| M-36 | 100-frame continuous | IT09: stress test 100 frames | COVERED |
| M-37 | Error mid-recovery | IT13: `Pipeline_ErrorMidRecovery_Stable` | COVERED |
| M-38 | Concurrent command+data | IT13: concurrency tests | PARTIAL |

### MCU Category Summary

| Category | Total | COVERED | PARTIAL | NOT_COVERED |
|----------|-------|---------|---------|-------------|
| SequenceEngine | 8 | 7 | 1 | 0 |
| FrameBufferManager | 9 | 8 | 1 | 0 |
| HealthMonitor | 7 | 6 | 1 | 0 |
| CommandProtocol | 6 | 6 | 0 | 0 |
| SPI Master | 4 | 4 | 0 | 0 |
| MCU Integration | 4 | 3 | 1 | 0 |
| **Total** | **38** | **34** | **4** | **0** |

---

## 4. Network (10GbE/UDP) Scenarios (N-01 to N-18)

### 4-1. Packet Loss

| ID | Scenario | Covering Test(s) | Status |
|----|----------|-----------------|--------|
| N-01 | 0% loss (baseline) | IT08: `PacketLoss_ZeroLoss_AllArrive` | COVERED |
| N-02 | 1% random loss | IT08: `PacketLoss_OnePct_SomePacketsLost` | COVERED |
| N-03 | 5% random loss | IT08: `PacketLoss_FivePct_ManyFramesIncomplete` | COVERED |
| N-04 | Burst loss | IT08: burst loss tests | COVERED |
| N-05 | Last packet loss | IT08: last packet loss tests | COVERED |
| N-06 | First packet loss | IT08: first packet loss tests | COVERED |

### 4-2. Packet Reordering

| ID | Scenario | Covering Test(s) | Status |
|----|----------|-----------------|--------|
| N-07 | Minor reorder | IT08: minor reorder tests | COVERED |
| N-08 | Severe reorder | IT08: severe reorder tests | COVERED |
| N-09 | Inter-frame interleaving | IT08: interleaving tests | COVERED |
| N-10 | Reverse arrival | IT08: reverse arrival tests | COVERED |

### 4-3. Delay/Jitter

| ID | Scenario | Covering Test(s) | Status |
|----|----------|-----------------|--------|
| N-11 | Uniform delay | It02PerformanceTargetTierTests | PARTIAL |
| N-12 | Variable jitter | It02PerformanceTargetTierTests | PARTIAL |
| N-13 | Timeout boundary | (not explicitly tested) | NOT_COVERED |
| N-14 | Timeout exceeded | (not explicitly tested) | NOT_COVERED |

### 4-4. Data Corruption

| ID | Scenario | Covering Test(s) | Status |
|----|----------|-----------------|--------|
| N-15 | Header CRC corruption | It01FullPipelineTests, IT13 | PARTIAL |
| N-16 | Payload corruption | It04Csi2ProtocolValidationTests | PARTIAL |
| N-17 | Magic number corruption | (not explicitly tested) | NOT_COVERED |
| N-18 | Combined faults | IT08: combined fault tests | PARTIAL |

### Network Category Summary

| Category | Total | COVERED | PARTIAL | NOT_COVERED |
|----------|-------|---------|---------|-------------|
| Packet Loss | 6 | 6 | 0 | 0 |
| Reordering | 4 | 4 | 0 | 0 |
| Delay/Jitter | 4 | 0 | 2 | 2 |
| Corruption | 4 | 0 | 3 | 1 |
| **Total** | **18** | **10** | **5** | **3** |

---

## 5. Host (PC) Scenarios (H-01 to H-12)

### 5-1. Reassembly

| ID | Scenario | Covering Test(s) | Status |
|----|----------|-----------------|--------|
| H-01 | Normal 1-frame reassembly | It01FullPipelineTests, IT11 | COVERED |
| H-02 | Out-of-order reassembly | IT08: reorder tests with host reassembly | COVERED |
| H-03 | Duplicate packet handling | IT13: `Pipeline_DuplicatePackets_SecondIgnored` | COVERED |
| H-04 | Multi-frame concurrent | IT13: concurrent frame tests | COVERED |
| H-05 | Large frame | IT09: `MaxTier_LargeFrame_2048x2048` | COVERED |

### 5-2. Timeout/Recovery

| ID | Scenario | Covering Test(s) | Status |
|----|----------|-----------------|--------|
| H-06 | Timeout detection | IT08: partial packet tests with timeout | PARTIAL |
| H-07 | Partial frame return | IT08: partial frame tests | PARTIAL |
| H-08 | Zero packets | (not explicitly tested) | NOT_COVERED |
| H-09 | Consecutive timeouts | (not explicitly tested) | NOT_COVERED |

### 5-3. Storage Output

| ID | Scenario | Covering Test(s) | Status |
|----|----------|-----------------|--------|
| H-10 | TIFF save | (TIFF save not implemented in current scope) | NOT_COVERED |
| H-11 | RAW save | (RAW save not tested in integration tests) | NOT_COVERED |
| H-12 | Continuous save | (storage save not in integration tests) | NOT_COVERED |

### Host Category Summary

| Category | Total | COVERED | PARTIAL | NOT_COVERED |
|----------|-------|---------|---------|-------------|
| Reassembly | 5 | 5 | 0 | 0 |
| Timeout/Recovery | 4 | 0 | 2 | 2 |
| Storage Output | 3 | 0 | 0 | 3 |
| **Total** | **12** | **5** | **2** | **5** |

---

## 6. End-to-End Integration Scenarios (E-01 to E-15)

### 6-1. Normal Operation

| ID | Scenario | Covering Test(s) | Status |
|----|----------|-----------------|--------|
| E-01 | Single frame bit-exact | It01FullPipelineTests, IT11 | COVERED |
| E-02 | 100-frame continuous | IT09: `MaxTier_100Frames_AllBitExact` | COVERED |
| E-03 | 1000-frame stress | IT09: `MaxTier_1000Frames_MemoryStable` | COVERED |
| E-04 | 3-mode switching | IT14: mode switching tests | COVERED |

### 6-2. Error Injection & Recovery

| ID | Scenario | Covering Test(s) | Status |
|----|----------|-----------------|--------|
| E-05 | FPGA watchdog error | IT13: `Pipeline_WatchdogError_AutoRecovery` | COVERED |
| E-06 | CSI-2 CRC error | IT13: CRC error injection tests | COVERED |
| E-07 | Buffer overflow | IT09: overflow stress tests | COVERED |
| E-08 | Network fault recovery | IT08: fault recovery tests | COVERED |
| E-09 | Multiple simultaneous errors | IT13: multi-error tests | COVERED |

### 6-3. Checkpoint Verification

| ID | Scenario | Covering Test(s) | Status |
|----|----------|-----------------|--------|
| E-10 | Per-layer output snapshot | IT11: `ProcessFrameWithCheckpoints()` | COVERED |
| E-11 | Per-layer latency | IT10: latency measurement tests | COVERED |
| E-12 | Data transformation tracking | IT11: data integrity checkpoint tests | COVERED |

### 6-4. Performance & Stability

| ID | Scenario | Covering Test(s) | Status |
|----|----------|-----------------|--------|
| E-13 | Throughput measurement | It02PerformanceTargetTierTests | COVERED |
| E-14 | Memory profiling | IT09: 10000-frame memory stability | PARTIAL |
| E-15 | Reset and restart | IT14: `SequenceEngine_Reset_ClearsState` | PARTIAL |

### End-to-End Category Summary

| Category | Total | COVERED | PARTIAL | NOT_COVERED |
|----------|-------|---------|---------|-------------|
| Normal Operation | 4 | 4 | 0 | 0 |
| Error & Recovery | 5 | 5 | 0 | 0 |
| Checkpoint | 3 | 3 | 0 | 0 |
| Performance | 3 | 1 | 2 | 0 |
| **Total** | **15** | **13** | **2** | **0** |

---

## 7. CLI Standalone Scenarios (C-01 to C-18)

### 7-1. Single Module

| ID | Scenario | Covering Test(s) | Status |
|----|----------|-----------------|--------|
| C-01 | Panel standalone | (no CLI integration test) | NOT_COVERED |
| C-02 | FPGA standalone | (no CLI integration test) | NOT_COVERED |
| C-03 | MCU standalone | (no CLI integration test) | NOT_COVERED |
| C-04 | Host standalone | (no CLI integration test) | NOT_COVERED |
| C-05 | Full chain CLI | (no CLI integration test) | NOT_COVERED |

### 7-2. Pipeline Composition

| ID | Scenario | Covering Test(s) | Status |
|----|----------|-----------------|--------|
| C-06 | Shell pipe | (stdin/stdout piping not tested) | NOT_COVERED |
| C-07 | Intermediate inspection | (not tested) | NOT_COVERED |
| C-08 | Shared config | (not tested) | NOT_COVERED |
| C-09 | Fixed seed reproduction | (determinism tested in It01, not CLI-level) | NOT_COVERED |
| C-10 | Multiple resolutions | IT09: multi-resolution pipeline tests | PARTIAL |

### 7-3. Analysis/Debug

| ID | Scenario | Covering Test(s) | Status |
|----|----------|-----------------|--------|
| C-11 | Noise analysis | (CLI not tested) | NOT_COVERED |
| C-12 | Calibration set | (CLI not tested) | NOT_COVERED |
| C-13 | CSI-2 protocol dump | (CLI not tested) | NOT_COVERED |
| C-14 | UDP packet analysis | (CLI not tested) | NOT_COVERED |
| C-15 | Reassembly debug | (CLI not tested) | NOT_COVERED |
| C-16 | Error injection test | (CLI not tested) | NOT_COVERED |
| C-17 | Network fault test | IT08: network fault covered at API level | PARTIAL |
| C-18 | Performance benchmark | It02: performance at API level | PARTIAL |

### CLI Category Summary

| Category | Total | COVERED | PARTIAL | NOT_COVERED |
|----------|-------|---------|---------|-------------|
| Single Module | 5 | 0 | 0 | 5 |
| Pipeline Composition | 5 | 0 | 1 | 4 |
| Analysis/Debug | 8 | 0 | 2 | 6 |
| **Total** | **18** | **0** | **3** | **15** |

---

## 8. HW Design Verification Scenarios (G-01 to G-15)

### 8-1. FPGA RTL Validation

| ID | Scenario | Covering Test(s) | Status |
|----|----------|-----------------|--------|
| G-01 | FSM design review | IT14: full FSM transition coverage | PARTIAL |
| G-02 | SPI protocol verification | IT03: SPI register map tests | PARTIAL |
| G-03 | CSI-2 packet format | It04Csi2ProtocolValidationTests | PARTIAL |
| G-04 | Timing parameters | IT03: timing register tests | PARTIAL |
| G-05 | Error scenarios | IT16: error injection and protection | PARTIAL |

### 8-2. Firmware Development Support

| ID | Scenario | Covering Test(s) | Status |
|----|----------|-----------------|--------|
| G-06 | API pre-validation | IT11, IT13: API scenario tests | PARTIAL |
| G-07 | State transition verification | IT14: SequenceEngine full cycle | PARTIAL |
| G-08 | Buffer policy verification | IT05, IT15: buffer drop policy | PARTIAL |
| G-09 | Communication protocol | IT06: HMAC/UDP protocol tests | PARTIAL |
| G-10 | Watchdog tuning | IT16: watchdog timeout tests | PARTIAL |

### 8-3. System Integration Pre-verification

| ID | Scenario | Covering Test(s) | Status |
|----|----------|-----------------|--------|
| G-11 | Full data path | IT11: full 4-layer pipeline | PARTIAL |
| G-12 | Error propagation analysis | IT13: error propagation tests | PARTIAL |
| G-13 | Performance bottleneck prediction | IT10: latency per layer | PARTIAL |
| G-14 | Configuration combination testing | IT09: various config combos | PARTIAL |
| G-15 | Long-term stress | IT09: 1000+ frame continuous | PARTIAL |

### HW Verification Category Summary

| Category | Total | COVERED | PARTIAL | NOT_COVERED |
|----------|-------|---------|---------|-------------|
| RTL Validation | 5 | 0 | 5 | 0 |
| Firmware Support | 5 | 0 | 5 | 0 |
| System Integration | 5 | 0 | 5 | 0 |
| **Total** | **15** | **0** | **15** | **0** |

---

## Overall Coverage Summary

| Category | Total | COVERED | PARTIAL | NOT_COVERED | Coverage % |
|----------|-------|---------|---------|-------------|-----------|
| Panel (P) | 22 | 8 | 12 | 2 | 36% / 91% w/ partial |
| FPGA (F) | 36 | 22 | 12 | 2 | 61% / 94% w/ partial |
| MCU (M) | 38 | 34 | 4 | 0 | 89% / 100% w/ partial |
| Network (N) | 18 | 10 | 5 | 3 | 56% / 83% w/ partial |
| Host (H) | 12 | 5 | 2 | 5 | 42% / 58% w/ partial |
| E2E (E) | 15 | 13 | 2 | 0 | 87% / 100% w/ partial |
| CLI (C) | 18 | 0 | 3 | 15 | 0% / 17% w/ partial |
| HW (G) | 15 | 0 | 15 | 0 | 0% / 100% w/ partial |
| **TOTAL** | **168** | **92** | **55** | **21** | **55% / 87% w/ partial** |

### Key Findings

**Well-Covered Areas (>80% full coverage):**
- MCU Scenarios: 89% fully covered (SequenceEngine, HMAC, FrameBuffer)
- End-to-End: 87% fully covered (pipeline integration, error injection)
- FPGA Protection Logic: 87% fully covered

**Gaps Requiring Attention:**
1. **CLI Scenarios (C-01 to C-18)**: 0 fully covered - no CLI round-trip tests exist
2. **Host Storage (H-10 to H-12)**: TIFF/RAW save not tested
3. **Network Delay/Jitter (N-13, N-14)**: Timeout boundary not tested
4. **P-09 (1/f Flicker Noise)**: Flicker noise model not tested
5. **F-35, F-36 (CSI-2 Backpressure)**: Not implemented in C# emulator

**Priority Recommendations:**
1. Add CLI round-trip tests (SPEC-EMUL-003 IT18/IT19 scope)
2. Add Panel physics scenario tests P-02, P-03, P-04, P-09
3. Add Network timeout boundary tests N-13, N-14
4. Add Host storage tests H-10, H-11

---

*Matrix generated for SPEC-EMUL-003: Scenario Verification & CLI Hardening*
*Test file locations: `tools/IntegrationTests/Integration/IT*.cs`*
