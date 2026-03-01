# SPEC-EMUL-001: Verification Scenarios (168 Total)

## 1. Panel (X-ray) Scenarios (22)

### 1-1. X-ray Physics Response (5)
| ID | Scenario | Input | Verification |
|----|----------|-------|-------------|
| P-01 | kVp signal level variation | kVp=40,60,80,100,120 / mAs=10 | Higher energy = higher pixel signal, CsI(Tl) response curve match |
| P-02 | mAs linearity | kVp=80 / mAs=1,2,5,10,20,50 | Signal linear to mAs, R²>0.999 |
| P-03 | Exposure time vs signal | gate_on=5ms,10ms,20ms,50ms | Signal proportional to gate time |
| P-04 | Dark frame (no exposure) | kVp=0, mAs=0 | Dark current only, Gaussian distribution, temperature dependent |
| P-05 | Saturation limit | kVp=120, mAs=100 (overexposure) | 16-bit max (65535) clipping, saturation pixel map |

### 1-2. Noise Characteristics (5)
| ID | Scenario | Input | Verification |
|----|----------|-------|-------------|
| P-06 | Poisson statistical noise | kVp=80, 10 frame average | Variance = Mean (Poisson), SNR proportional to sqrt(signal) |
| P-07 | Gaussian electronic noise | 100 dark frames | Constant std dev, normal distribution histogram |
| P-08 | Dark current temperature dependence | Temp=20,25,30,40C | Dark current ~2x per 10C increase |
| P-09 | 1/f (Flicker) noise | 1000 continuous frames | Low-frequency component, 1/f slope in power spectrum |
| P-10 | Composite noise accuracy | Composite mode, 100 frame avg | Total variance = sum of individual variances |

### 1-3. Calibration Data (4)
| ID | Scenario | Input | Verification |
|----|----------|-------|-------------|
| P-11 | Dark calibration | 100 dark frames averaged | Dark map generated, defect pixel auto-detection |
| P-12 | Flatfield calibration | 100 uniform exposure averaged | Gain map = Flatfield / mean, variation < 5% |
| P-13 | Offset calibration | Dark correction applied | Residual offset < 1 DN |
| P-14 | Defect pixel map | Dark + Flatfield combined | Dead/Hot/Noisy classification, defect rate < 0.1% |

### 1-4. Temporal Effects (4)
| ID | Scenario | Input | Verification |
|----|----------|-------|-------------|
| P-15 | Ghosting | High signal then no exposure | Ghosting decay curve, 1/e time |
| P-16 | Lag quantification | 1 bright frame then 10 dark | lag_n = signal_n / signal_0 |
| P-17 | Temperature drift | 1000 frames, temp rising | Offset increase trend over time |
| P-18 | Long-term stability | 10000 continuous frames | Mean/variance change < 1% |

### 1-5. Gate/ROIC Interaction (4)
| ID | Scenario | Input | Verification |
|----|----------|-------|-------------|
| P-19 | Gate ON/OFF response | gate_on pulse then exposure | Signal proportional to gate_on timing |
| P-20 | Row-by-row ROIC readout | 2048 rows sequential read | Each row valid after settle time, uniform inter-row timing |
| P-21 | ROIC settle time impact | settle=0.5us,1us,2us | Insufficient settle shows previous row residual |
| P-22 | Calibration mode readout | ScanMode=Calibration | Full row repeated read, noise averaging effect |

---

## 2. FPGA (Artix-7) Scenarios (36)

### 2-1. FSM State Transitions (6)
| ID | Scenario | Input | Verification |
|----|----------|-------|-------------|
| F-01 | Normal single scan cycle | StartScan(Single) to completion | IDLE->INTEGRATE->READOUT->LINE_DONE->(repeat)->FRAME_DONE->IDLE |
| F-02 | Continuous scan cycle | StartScan(Continuous), N frames | FRAME_DONE->INTEGRATE auto-restart, frame counter increment |
| F-03 | Calibration mode | StartScan(Calibration) | gate_on inactive (dark calibration), rest same |
| F-04 | Forced stop | StopScan() during Continuous | Safe return to IDLE from any state, counter reset |
| F-05 | Duplicate start command | StartScan() during SCANNING | Ignored or error, state unchanged |
| F-06 | Error from all states | ERROR event in each state | Immediate ERROR state, ErrorFlags latched |

### 2-2. Control Signal Timing (6)
| ID | Scenario | Input | Verification |
|----|----------|-------|-------------|
| F-07 | Gate ON pulse width | gate_on_us=5,10,20,50 | HIGH for exact duration during INTEGRATE |
| F-08 | Gate OFF interval | gate_off_us=3,5,10 | Inter-line gate_off matches setting |
| F-09 | ROIC Sync timing | IDLE to INTEGRATE transition | roic_sync pulse at exact transition |
| F-10 | Line Valid signal | READOUT state entry | line_valid HIGH after settle+ADC complete |
| F-11 | Settle/ADC timer separation | settle=1us, adc=2us | Sequential countdown: settle then ADC |
| F-12 | Frame Valid signal | Last line complete | frame_valid HIGH at FRAME_DONE, LOW after 1 clock |

### 2-3. Protection Logic (8)
| ID | Scenario | Input | Verification |
|----|----------|-------|-------------|
| F-13 | Watchdog timeout | SPI heartbeat stops for 5s | Watchdog error, FSM->ERROR, gate_safe active |
| F-14 | Watchdog normal reset | 1s interval SPI activity | Counter reset, no error |
| F-15 | Readout timeout | READOUT exceeds 100us | Timeout error, ERROR state |
| F-16 | Overflow error | LineBuffer write exceeds capacity | Overflow flag, buffer protected |
| F-17 | CRC error detection | CSI-2 packet CRC mismatch | CRC error flag set |
| F-18 | Safe shutdown timing | Fatal error injected | gate_safe + csi2_disable + buffer_disable within 10 clocks |
| F-19 | Error clear | error_clear command after ERROR | Flags cleared, return to IDLE |
| F-20 | Multiple simultaneous errors | Timeout + Overflow at once | All flags latched, most severe prioritized |

### 2-4. SPI Register (6)
| ID | Scenario | Input | Verification |
|----|----------|-------|-------------|
| F-21 | STATUS real-time reflection | Read during FSM state change | STATUS[10:8]=current FSM state, [0]=idle bit accurate |
| F-22 | Read-only protection | Write to STATUS/VERSION | Write rejected, value unchanged |
| F-23 | CONTROL bit decoding | start_scan, stop_scan, reset, error_clear | Each bit triggers correct action |
| F-24 | Frame counter 32-bit | 65536+ frames | HI/LO registers correctly split |
| F-25 | Unmapped address read | Non-existent register | Returns 0x0000 (REQ-FPGA-044) |
| F-26 | ILA capture | Error occurrence | ILA registers snapshot FSM/buffer/error state |

### 2-5. Line Buffer (4)
| ID | Scenario | Input | Verification |
|----|----------|-------|-------------|
| F-27 | Ping-Pong bank alternation | Continuous line write/read | Bank A->B->A alternation, no data loss |
| F-28 | Overflow detection | Write before read complete | Overflow flag, data protected |
| F-29 | Maximum width test | 3072 pixel line | Normal operation at max capacity |
| F-30 | CDC delay effect | Fast bank switching | 2-stage FF sync delay reflected |

### 2-6. CSI-2 TX (6)
| ID | Scenario | Input | Verification |
|----|----------|-------|-------------|
| F-31 | Full packet sequence | 1 frame transmission | FS -> (LS -> LineData -> LE) x N -> FE order |
| F-32 | ECC accuracy | Various header combinations | MIPI spec 6-bit ECC match |
| F-33 | CRC-16 accuracy | Various payloads | CRC-16/CCITT reference match |
| F-34 | Virtual Channel | VC=0,1,2,3 | DataID[7:6] correct VC encoding |
| F-35 | Backpressure | tready=0 state | Transmission paused, no data loss |
| F-36 | Backpressure release | tready 0->1 transition | Resume from exact interruption point |

---

## 3. MCU/SoC (i.MX8MP) Scenarios (38)

### 3-1. SequenceEngine FSM (8)
| ID | Scenario | Input | Verification |
|----|----------|-------|-------------|
| M-01 | Single scan full cycle | StartScan(Single) | IDLE->CONFIGURE->ARM->SCANNING->STREAMING->COMPLETE->IDLE |
| M-02 | Continuous scan N frames | StartScan(Continuous), 10 frames | COMPLETE->SCANNING auto-transition, correct frame count |
| M-03 | Calibration scan | StartScan(Calibration) | Calibration mode passed to FPGA |
| M-04 | Invalid start | StartScan during SCANNING | -EINVAL return, state unchanged |
| M-05 | Error recovery (1 retry) | ERROR during SCANNING then CLEARED | ERROR->IDLE, retries=1 |
| M-06 | Error recovery (3 exhausted) | 3 consecutive ERRORs | Permanent ERROR after 3rd, no recovery |
| M-07 | Emergency stop | STOP_SCAN from any state | Return to IDLE, all counters reset |
| M-08 | Full 56-transition table | 7 states x 8 events | All combinations verified |

### 3-2. FrameBufferManager (9)
| ID | Scenario | Input | Verification |
|----|----------|-------|-------------|
| M-09 | Normal 1-frame cycle | GetBuffer->Commit->GetReady->Release | FREE->FILLING->READY->SENDING->FREE |
| M-10 | 4-buffer sequential use | 4 consecutive frames | All 4 buffers cycled, all released |
| M-11 | 5th frame (oldest-drop) | 4 frames READY + 5th arrives | Oldest READY dropped, overruns++ |
| M-12 | Producer faster than Consumer | Fast CSI-2 RX, slow UDP TX | Buffer saturation, drops occur, stats accurate |
| M-13 | Consumer faster than Producer | Slow CSI-2 RX, fast UDP TX | GetReadyBuffer waits, empty return |
| M-14 | Concurrency safety | Multi-thread Producer+Consumer | No deadlock, data integrity |
| M-15 | GetReady on empty | All buffers FREE | null return, not error |
| M-16 | Statistics accuracy | 100 frames processed | received=100, sent=100, dropped=0 |
| M-17 | Drop during SENDING | New frame with SENDING buffer | SENDING skipped, only READY dropped |

### 3-3. HealthMonitor (7)
| ID | Scenario | Input | Verification |
|----|----------|-------|-------------|
| M-18 | Watchdog normal | PetWatchdog() at 1s interval | IsAlive()=true, watchdog_resets=0 |
| M-19 | Watchdog timeout | No Pet for 5+ seconds | IsAlive()=false, watchdog_resets++ |
| M-20 | Stat counter update | UpdateStat("frames_sent", 1) x 100 | GetStats().frames_sent == 100 |
| M-21 | 9 counter independence | Individual counter updates | No cross-contamination |
| M-22 | GetStatus response time | GetStatus() 1000 calls | All < 50ms (REQ-FW-112) |
| M-23 | Log level filtering | Level=WARNING | DEBUG/INFO suppressed, WARNING+ output |
| M-24 | System status snapshot | GetStatus() in various states | state, stats, battery_soc, uptime, fpga_temp all included |

### 3-4. CommandProtocol (6)
| ID | Scenario | Input | Verification |
|----|----------|-------|-------------|
| M-25 | Valid HMAC command | Correctly signed START_SCAN | HMAC passes, dispatched to SequenceEngine |
| M-26 | Invalid HMAC command | Wrong key signature | Rejected, auth_failures++ |
| M-27 | Replay attack | Previous timestamp resubmitted | Timestamp expired, rejected |
| M-28 | All command types | START/STOP/GET_STATUS/SET_CONFIG | Each dispatched to correct handler |
| M-29 | Payload tampering | Valid HMAC + modified payload | HMAC mismatch detected, rejected |
| M-30 | Consecutive auth failures | 10 consecutive invalid HMACs | auth_failures=10, blocking policy |

### 3-5. SPI Master (4)
| ID | Scenario | Input | Verification |
|----|----------|-------|-------------|
| M-31 | FPGA register write | WriteReg(MODE, Continuous) | Value delivered to FPGA SpiSlave |
| M-32 | FPGA register read | ReadReg(STATUS) | FSM state accurately reflected |
| M-33 | FPGA config sequence | CONFIGURE phase | MODE->GATE_ON->GATE_OFF->ROWS->COLS order |
| M-34 | SPI error counting | Invalid response simulated | spi_errors++ in HealthMonitor |

### 3-6. MCU Integration (4)
| ID | Scenario | Input | Verification |
|----|----------|-------|-------------|
| M-35 | Full frame processing | HMAC command -> CSI-2 RX -> UDP TX | Command->Sequence->Buffer->UDP full path |
| M-36 | 100-frame continuous | Continuous, 100 frames | All stats accurate, no memory leak |
| M-37 | Error mid-recovery | Error at frame 50 | Auto-recovery, remaining frames normal |
| M-38 | Concurrent command+data | GET_STATUS during frame RX | Command and data pipeline independent |

---

## 4. Network (10GbE/UDP) Scenarios (18)

### 4-1. Packet Loss (6)
| ID | Scenario | Input | Verification |
|----|----------|-------|-------------|
| N-01 | 0% loss (baseline) | lossRate=0.0 | All packets arrive, 100% frame completion |
| N-02 | 1% random loss | lossRate=0.01 | ~1% packets lost, some frames incomplete |
| N-03 | 5% random loss | lossRate=0.05 | Most frames incomplete, many timeouts |
| N-04 | Burst loss | 10 consecutive packets dropped | Specific frame incomplete, others normal |
| N-05 | Last packet loss | last_packet flag packet dropped | Frame completion undetectable, timeout needed |
| N-06 | First packet loss | First packet of frame dropped | Header info missing, partial reassembly |

### 4-2. Packet Reordering (4)
| ID | Scenario | Input | Verification |
|----|----------|-------|-------------|
| N-07 | Minor reorder | Adjacent 2-packet swap | Normal reassembly via packet_seq |
| N-08 | Severe reorder | Full shuffle | Normal restoration via packet_seq |
| N-09 | Inter-frame interleaving | Frame A,B packets mixed | Separated by frameId, both normal |
| N-10 | Reverse arrival | Last packet arrives first | Reassembly complete, data correct |

### 4-3. Delay/Jitter (4)
| ID | Scenario | Input | Verification |
|----|----------|-------|-------------|
| N-11 | Uniform delay | delay=100us all packets | Frame completion time = original + 100us |
| N-12 | Variable jitter | delay=50-200us random | Reordering possible, reassembly normal |
| N-13 | Timeout boundary | delay=4900ms (5000ms timeout) | Completes just before timeout |
| N-14 | Timeout exceeded | delay=6000ms (5000ms timeout) | Timeout triggered, incomplete frame returned |

### 4-4. Data Corruption (4)
| ID | Scenario | Input | Verification |
|----|----------|-------|-------------|
| N-15 | Header CRC corruption | CRC 1-bit flip | TryParse fails, packet ignored |
| N-16 | Payload corruption | Data byte altered | CRC mismatch detected (CSI-2 level) |
| N-17 | Magic number corruption | 0xD7E01234 altered | Packet not recognized, ignored |
| N-18 | Combined faults | 5% loss + 20% reorder + 1% corrupt | Real network approximation, reassembly robustness |

---

## 5. Host (PC) Scenarios (12)

### 5-1. Reassembly (5)
| ID | Scenario | Input | Verification |
|----|----------|-------|-------------|
| H-01 | Normal 1-frame reassembly | All packets in order | FrameData correct, pixel-by-pixel match |
| H-02 | Out-of-order reassembly | Shuffled packets | Normal restoration via packet_seq |
| H-03 | Duplicate packet handling | Same packet sent twice | Second ignored, frame normal |
| H-04 | Multi-frame concurrent | 3 frames interleaved | Independent reassembly per frameId |
| H-05 | Large frame | 2048x2048x16bit | 100+ packets reassembled, memory stable |

### 5-2. Timeout/Recovery (4)
| ID | Scenario | Input | Verification |
|----|----------|-------|-------------|
| H-06 | Timeout detection | Partial packets arrive | Timeout after 5s, missing packet list |
| H-07 | Partial frame return | 80% packets arrived | Partial frame from received packets |
| H-08 | Zero packets | Frame started, no packets | Timeout, empty frame report |
| H-09 | Consecutive timeouts | 5 frames all incomplete | Independent timeout per frame, memory cleanup |

### 5-3. Storage Output (3)
| ID | Scenario | Input | Verification |
|----|----------|-------|-------------|
| H-10 | TIFF save | 2048x2048 frame | TIFF 6.0 compatible, 16-bit grayscale, opens in external viewer |
| H-11 | RAW save | 2048x2048 frame | rows x cols x 2 bytes, little-endian |
| H-12 | Continuous save | 100 frames continuous | Auto-numbered filenames, stable disk I/O |

---

## 6. End-to-End Integration Scenarios (15)

### 6-1. Normal Operation (4)
| ID | Scenario | Input | Verification |
|----|----------|-------|-------------|
| E-01 | Single frame bit-exact | 1 frame full pipeline | Panel->FPGA->MCU->Host, input=output bit-exact |
| E-02 | 100-frame continuous | Continuous, 100 frames | All frames bit-exact, sequential frame numbers |
| E-03 | 1000-frame stress | Continuous, 1000 frames | Memory stable, no performance degradation |
| E-04 | 3-mode switching | Single->Continuous->Calibration | Normal operation after each switch |

### 6-2. Error Injection & Recovery (5)
| ID | Scenario | Input | Verification |
|----|----------|-------|-------------|
| E-05 | FPGA watchdog error | SPI interruption injected | FPGA->ERROR, MCU detects, auto-recovery attempt |
| E-06 | CSI-2 CRC error | Packet corruption injected | MCU CRC error detection, frame dropped |
| E-07 | Buffer overflow | Producer >> Consumer speed | Oldest-drop, overruns stats, data integrity |
| E-08 | Network fault recovery | 5% packet loss for 10s | Host timeouts, lost frame report, recovery after fault |
| E-09 | Multiple simultaneous errors | FPGA CRC + network loss | Independent per-layer error handling, propagation tracking |

### 6-3. Checkpoint Verification (3)
| ID | Scenario | Input | Verification |
|----|----------|-------|-------------|
| E-10 | Per-layer output snapshot | ProcessFrameWithCheckpoints() | Panel/FPGA/MCU/Host outputs capturable |
| E-11 | Per-layer latency | Checkpoint timing measurement | Individual layer processing time |
| E-12 | Data transformation tracking | ushort[] -> CSI-2 -> UDP -> ushort[] | Data integrity at each transformation |

### 6-4. Performance & Stability (3)
| ID | Scenario | Input | Verification |
|----|----------|-------|-------------|
| E-13 | Throughput measurement | 2048x2048, 30fps target | Per-frame processing < 33ms |
| E-14 | Memory profiling | 10000 continuous frames | GC pressure stable, no memory leak |
| E-15 | Reset and restart | Full pipeline Reset then restart | All state initialized, normal restart |

---

## 7. CLI Standalone Scenarios (18)

### 7-1. Single Module (5)
| ID | Scenario | CLI | Verification |
|----|----------|-----|-------------|
| C-01 | Panel standalone | `panel-sim --rows 256 --cols 256 --kvp 80 -o frame.raw` | .raw file, size=256x256x2 |
| C-02 | FPGA standalone | `fpga-sim --input frame.raw --mode single -o packets.csi2` | .csi2 file, FS+Lines+FE sequence |
| C-03 | MCU standalone | `mcu-sim --input packets.csi2 --buffers 4 -o frames.udp` | .udp file, 32-byte header + payload |
| C-04 | Host standalone | `host-sim --input frames.udp --timeout 1000 -o result.tiff` | .tiff file, bit-exact with original |
| C-05 | Full chain | C-01 -> C-02 -> C-03 -> C-04 | Same output as integrated pipeline |

### 7-2. Pipeline Composition (5)
| ID | Scenario | CLI | Verification |
|----|----------|-----|-------------|
| C-06 | Shell pipe | `panel-sim ... \| fpga-sim ... \| mcu-sim ... \| host-sim ...` | stdin/stdout streaming |
| C-07 | Intermediate inspection | Each stage output saved separately | Debug intermediate data |
| C-08 | Shared config | `--config detector_config.yaml` | All modules synchronized |
| C-09 | Fixed seed reproduction | `--seed 42` | Same seed = same result (deterministic) |
| C-10 | Multiple resolutions | 256, 512, 1024, 2048 | Normal operation at all resolutions |

### 7-3. Analysis/Debug (8)
| ID | Scenario | CLI | Verification |
|----|----------|-----|-------------|
| C-11 | Noise analysis | `panel-sim --frames 100 --noise composite` | Mean/variance calculation, SNR graphable |
| C-12 | Calibration set | `panel-sim --mode calibration --frames 100` | Dark/Flat/Bias frame sets |
| C-13 | CSI-2 protocol dump | `fpga-sim --verbose --input frame.raw` | Per-packet type/VC/WC/ECC/CRC detail |
| C-14 | UDP packet analysis | `mcu-sim --verbose --input packets.csi2` | Per-packet frameId/seq/total/CRC detail |
| C-15 | Reassembly debug | `host-sim --verbose --input frames.udp` | Packet order, missing packets, timeout logs |
| C-16 | Error injection test | `fpga-sim --inject-error timeout --at-frame 5` | Timeout error at frame 5 |
| C-17 | Network fault test | `integration-runner --loss-rate 0.05 --reorder-rate 0.1` | Pipeline under network faults |
| C-18 | Performance benchmark | `integration-runner --frames 1000 --benchmark` | Throughput, latency, memory report |

---

## 8. HW Design Verification Scenarios (15)

### 8-1. FPGA RTL Validation (5)
| ID | Scenario | Method | Verification |
|----|----------|--------|-------------|
| G-01 | FSM design review | Compare C# FSM with RTL simulation | State transition identity |
| G-02 | SPI protocol verification | Compare C# register map with RTL | Address/bit mapping match |
| G-03 | CSI-2 packet format | Input C# packets to RTL receiver | Parsing compatibility |
| G-04 | Timing parameters | Compare C# timing with RTL simulation | gate_on/settle/ADC timing match |
| G-05 | Error scenarios | Use C# error injection results as RTL expected values | Protection Logic match |

### 8-2. Firmware Development Support (5)
| ID | Scenario | Method | Verification |
|----|----------|--------|-------------|
| G-06 | API pre-validation | Test C# API call scenarios | Confirm interfaces before real FW development |
| G-07 | State transition verification | Use C# SequenceEngine as reference | FW FSM implementation accuracy |
| G-08 | Buffer policy verification | Analyze drop scenarios with C# FrameBufferManager | Optimal buffer count/policy |
| G-09 | Communication protocol | Use C# HMAC/UDP as reference implementation | SDK-MCU compatibility |
| G-10 | Watchdog tuning | Experiment with C# watchdog timing | Optimal timeout values |

### 8-3. System Integration Pre-verification (5)
| ID | Scenario | Method | Verification |
|----|----------|--------|-------------|
| G-11 | Full data path | Trace Panel->FPGA->MCU->Host | Protocol compatibility before HW assembly |
| G-12 | Error propagation analysis | Track per-layer error propagation | Error handling design pre-validation |
| G-13 | Performance bottleneck prediction | Measure per-layer latency | Performance margin vs HW targets |
| G-14 | Configuration combination testing | Various resolution/mode/timing combos | Pre-verify combos difficult to test on HW |
| G-15 | Long-term stress | 10000+ frame continuous test | SW stability before HW long-term testing |

---

## Summary

| Module | Count | Coverage Areas |
|--------|-------|----------------|
| Panel (X-ray) | 22 | Physics response, noise, calibration, temporal, Gate/ROIC |
| FPGA (Artix-7) | 36 | FSM, control signals, protection, SPI, line buffer, CSI-2 |
| MCU (i.MX8MP) | 38 | SequenceEngine, FrameBuffer, Health, Command, SPI, integration |
| Network (10GbE) | 18 | Loss, reorder, delay, corruption |
| Host (PC) | 12 | Reassembly, timeout, storage |
| End-to-End | 15 | Normal, error, checkpoint, performance |
| CLI | 18 | Standalone, pipeline, analysis/debug |
| HW Verification | 15 | RTL validation, firmware dev, system integration |
| **Total** | **168** | |
