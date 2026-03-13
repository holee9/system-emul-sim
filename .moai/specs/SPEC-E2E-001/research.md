# SPEC-E2E-001 Research: E2E 테스트 인프라 디버그 및 개선

## 문제 요약

GUI.Application E2E 테스트가 비대화형(non-interactive) bash/CI 세션에서 30초 타임아웃 후 실패함.
FlaUI의 UIAutomation이 비대화형 환경에서 WPF 창을 인식할 수 없는 근본적 한계.

---

## AppFixture 상세 분석

### 행(hang) 발생 위치

`AppFixture.InitializeAsync()` (lines 49-97):

1. `Process.Start(startInfo)` → WPF 프로세스 시작 ✅ (성공)
2. `_flaUiApp.GetMainWindow(_automation)` → UIAutomation 트리 검색 시작
3. **30초 루프 (500ms 간격 폴링)** — 여기서 hang 발생
4. 30초 후 `TimeoutException` throw

**근본 원인**: WPF 프로세스는 실행되지만, 비대화형 세션에서 UIAutomation 서버가 프로세스에 바인딩되지 않음. `GetMainWindow()`가 항상 `null` 반환.

### 프로세스 시작 분석

```csharp
var startInfo = new ProcessStartInfo(exePath)
{
    UseShellExecute = false,
};
startInfo.Environment["XRAY_E2E_MODE"] = "true";  // .NET 8 호환 ✅
```

- 실행파일 경로: `bin/Release/net8.0-windows/GUI.Application.exe` → `bin/Debug/net8.0-windows/` 폴백
- 프로세스 시작 자체는 성공 ✅
- 창이 UIAutomation 트리에 등록되지 않음 ❌

### 메뉴 워밍업 로직

`WarmupSingleMenuAsync()` — MainWindow 발견 후 실행:
- WPF MenuItem AutomationPeer가 Dispatcher Background 우선순위로 지연 등록됨
- 첫 실행 시 최대 26초 소요 (90초까지 대기)
- 메뉴가 열린 상태 유지 필수 (접으면 타이머 리셋)

---

## RequiresDesktopFactAttribute 한계 분석

### 현재 동작

```csharp
public sealed class RequiresDesktopFactAttribute : FactAttribute
{
    public RequiresDesktopFactAttribute()
    {
        if (!IsInteractiveDesktop())
            Skip = "Requires interactive desktop session...";
    }
}
```

xUnit이 `[RequiresDesktopFact]`를 체크하는 시점:
1. ❌ **ICollectionFixture<AppFixture> 초기화** (AppFixture.InitializeAsync 실행)
2. ✅ 테스트 메서드 실행 전 Skip 확인

**결론**: 어트리뷰트는 개별 테스트 실행을 막지만, Fixture 초기화 단계의 30초 hang은 막지 못함.

### 누락 발견

`ParameterExtractionE2ETests.cs`의 두 테스트 메서드에 `[RequiresDesktopFact]` 적용됨 ✅
(최신 커밋에서 수정 완료)

---

## 수정 접근법 (우선순위 순)

### Approach 1: AppFixture Fast-Fail (최우선) ⭐

AppFixture.InitializeAsync() 진입 시 데스크톱 환경 조기 감지:

```csharp
public async Task InitializeAsync()
{
    if (!IsInteractiveDesktop())
        throw new SkipException("E2E tests require interactive desktop (UIAutomation unavailable)");
    // ... 기존 코드
}
```

- 30초 hang 즉시 제거
- CI 실행 시간 크게 단축
- 명확한 에러 메시지 제공

### Approach 2: E2ECollection 레벨 Skip ⭐⭐

xUnit의 `IAsyncLifetime`을 활용해 Fixture 초기화 전 환경 감지:

```csharp
public sealed class AppFixture : IAsyncLifetime
{
    private bool _isDesktopAvailable;

    public async Task InitializeAsync()
    {
        _isDesktopAvailable = IsInteractiveDesktop();
        if (!_isDesktopAvailable) return; // 즉시 반환, 프로세스 시작 안 함
        // ... 기존 코드
    }
}
```

E2ETestBase에서 체크:
```csharp
public abstract class E2ETestBase : IDisposable
{
    protected E2ETestBase(AppFixture fixture)
    {
        if (!fixture.IsDesktopAvailable)
            throw new SkipException("Desktop unavailable");
    }
}
```

### Approach 3: 타임아웃 단축

30초 → 5초로 단축 (환경 변수로 제어 가능):
```csharp
var timeoutSeconds = int.TryParse(
    Environment.GetEnvironmentVariable("E2E_INIT_TIMEOUT"), out var t) ? t : 30;
var timeout = TimeSpan.FromSeconds(timeoutSeconds);
```

CI에서 `E2E_INIT_TIMEOUT=3` 설정으로 빠른 실패 가능.

### Approach 4: GitHub Actions 개선

- `windows-latest` 러너에서 실제 데스크톱 세션 활성화
- GitHub Actions에서 WPF UIAutomation 활성화 방법 연구 필요
- 현재 `workflow_dispatch` 전용으로 변경되었으므로 개발자가 수동 실행

---

## 실제 디버그 실행 요구사항

### 대화형 세션에서 테스트 실행

```powershell
# PowerShell ISE 또는 Visual Studio 터미널에서:
cd D:\workspace-github\system-emul-sim
dotnet test tools/GUI.Application/tests/GUI.Application.E2ETests/ --verbosity normal

# CI=true 설정 시 skip 확인:
$env:CI="true"; dotnet test ... # → Skipped: 17
```

### 디버그 수집 항목

E2E 테스트 실행 시 다음 정보 수집 필요:
1. `_flaUiApp.GetMainWindow()` 반환값 로그
2. WPF 프로세스 PID 및 창 핸들
3. UIAutomation 서버 바인딩 상태
4. 스크린샷 (실패 시)

---

## 리스크 및 제약

| 리스크 | 심각도 | 완화 방법 |
|--------|--------|-----------|
| windows-latest 비대화형 세션 | 높음 | workflow_dispatch 수동 실행 유지 |
| 메뉴 워밍업 26초+ 지연 | 중간 | 타임아웃을 90초로 유지 |
| WPF DirectX 렌더링 필요 | 낮음 | Software 렌더링 모드 설정 가능 |
| xUnit Fixture 조건부 초기화 불가 | 설계 제약 | Approach 2 (빠른 반환) 패턴 사용 |

---

## 참조 파일

- `tools/GUI.Application/tests/GUI.Application.E2ETests/Infrastructure/AppFixture.cs`
- `tools/GUI.Application/tests/GUI.Application.E2ETests/Infrastructure/E2ETestBase.cs`
- `tools/GUI.Application/tests/GUI.Application.E2ETests/Infrastructure/RequiresDesktopFactAttribute.cs`
- `tools/GUI.Application/src/GUI.Application/App.xaml.cs`
- `.github/workflows/e2e-tests.yml`
- `.moai/issues/E2E-DEBUG-001.md`

---

Research Date: 2026-03-13
Analyst: MoAI Explore Agent
