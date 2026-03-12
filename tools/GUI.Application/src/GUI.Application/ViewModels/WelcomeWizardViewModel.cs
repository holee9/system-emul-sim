using System.Windows.Input;
using XrayDetector.Gui.Core;

namespace XrayDetector.Gui.ViewModels;

/// <summary>
/// ViewModel for the Welcome Wizard shown on first run (SPEC-HELP-001 Wave 2).
/// 3-step wizard: Welcome → Key Parameters → Ready.
/// </summary>
public class WelcomeWizardViewModel : ObservableObject
{
    private int _currentStep = 1;
    private bool _dontShowAgain;
    private readonly RelayCommand _nextCmd;
    private readonly RelayCommand _previousCmd;
    private readonly RelayCommand _finishCmd;

    private static readonly (string Title, string Content)[] _steps =
    {
        (
            "환영합니다!",
            "X-ray Detector Panel System 에뮬레이터에 오신 것을 환영합니다.\n\n" +
            "이 애플리케이션은 4계층 파이프라인을 시뮬레이션합니다:\n" +
            "  Panel → FPGA(CSI-2) → MCU(UDP) → Host\n\n" +
            "다음 버튼을 클릭하여 시작하세요."
        ),
        (
            "주요 파라미터",
            "시뮬레이션의 핵심 파라미터를 소개합니다:\n\n" +
            "• kVp (40–150 kV): X선관 가속 전압 - 높을수록 투과력 증가\n" +
            "• mAs (0.1–500): X선 방사선량 - 높을수록 SNR 향상\n" +
            "• NoiseType: Gaussian / Poisson / None\n" +
            "• 해상도: 64×64 ~ 4096×4096 픽셀\n\n" +
            "기본값으로 시작하고 나중에 조정해보세요."
        ),
        (
            "시작할 준비가 되었습니다",
            "이제 애플리케이션을 시작할 준비가 완료되었습니다!\n\n" +
            "• Ctrl+R: 파이프라인 시작\n" +
            "• F1: 컨텍스트 도움말\n" +
            "• F11: 전체화면 전환\n\n" +
            "궁금한 점이 있으면 F1 키를 눌러 도움말을 확인하세요."
        )
    };

    /// <summary>Creates a new WelcomeWizardViewModel.</summary>
    public WelcomeWizardViewModel()
    {
        _nextCmd = new RelayCommand(OnNext, () => _currentStep < 3);
        _previousCmd = new RelayCommand(OnPrevious, () => _currentStep > 1);
        _finishCmd = new RelayCommand(OnFinish, () => _currentStep == 3);
        NextCommand = _nextCmd;
        PreviousCommand = _previousCmd;
        FinishCommand = _finishCmd;
    }

    /// <summary>Current wizard step (1–3).</summary>
    public int CurrentStep
    {
        get => _currentStep;
        private set
        {
            if (SetField(ref _currentStep, value))
            {
                OnPropertyChanged(nameof(StepTitle));
                OnPropertyChanged(nameof(StepContent));
                (NextCommand as RelayCommand)?.NotifyCanExecuteChanged();
                (PreviousCommand as RelayCommand)?.NotifyCanExecuteChanged();
                (FinishCommand as RelayCommand)?.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>Title for the current step.</summary>
    public string StepTitle => _steps[_currentStep - 1].Title;

    /// <summary>Content text for the current step.</summary>
    public string StepContent => _steps[_currentStep - 1].Content;

    /// <summary>Whether the user wants to suppress future first-run dialogs.</summary>
    public bool DontShowAgain
    {
        get => _dontShowAgain;
        set => SetField(ref _dontShowAgain, value);
    }

    /// <summary>Optional callback invoked when the wizard finishes.</summary>
    public Action? FinishAction { get; set; }

    /// <summary>Command to advance to the next step.</summary>
    public ICommand NextCommand { get; }

    /// <summary>Command to go back to the previous step.</summary>
    public ICommand PreviousCommand { get; }

    /// <summary>Command to finish the wizard (available only at step 3).</summary>
    public ICommand FinishCommand { get; }

    private void OnNext() => CurrentStep++;

    private void OnPrevious() => CurrentStep--;

    private void OnFinish() => FinishAction?.Invoke();
}
