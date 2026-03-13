## SPEC-UI-001 Progress

- Started: 2026-03-11
- Phase 1 complete: Strategy analysis done (manager-strategy). Key decisions recorded.
- Phase 1.5~2 pending: User chose to defer implementation.

## Key Decisions (Phase 1)

- ProcessInMemory() NOT needed — use existing SimulatorPipeline.ProcessFrame()
- DetectorConfig.PanelConfig needs extension: kVp, mAs, NoiseType, Fidelity, DefectRate
- YamlDotNet: comes as transitive dep via IntegrationRunner.Core ProjectReference
- MainViewModel._detectorClient: change to mutable for mode switching
- DispatcherTimer tests: expose timer callbacks as internal methods
- Tab 6 Config: integrate as Commands in SimulatorControlViewModel (no separate ViewModel)

## Task Order

TASK-001 (XL): PipelineDetectorClient + mode switch
TASK-002 (L):  SimulatorControlViewModel (Panel/FPGA/MCU/Network params)
TASK-003 (M):  PipelineStatusViewModel (2Hz polling)
TASK-004 (L):  ScenarioRunner + ScenarioRunnerViewModel
TASK-005 (M):  Configuration Management (YAML load/save)
TASK-006 (L):  XAML Views + MainWindow integration
