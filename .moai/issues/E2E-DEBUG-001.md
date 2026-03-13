# E2E 테스트 AppFixture.InitializeAsync() 타임아웃 문제

## 라벨
bug, gui, e2e, high-priority

## 문제 설명
GUI.Application.E2ETests가 AppFixture.InitializeAsync()에서 30초 타임아웃으로 실패합니다.

## 증상
```
[xUnit.net 00:00:00.xx]   Starting:    GUI.Application.E2ETests
[...출력 없음...]
[30초 후 타임아웃]
```

- 테스트가 "Starting:" 단계에서 멈춤
- FlaUI.GetMainWindow()가 윈도우를 찾지 못함
- 비대화형 bash 세션(Claude Code)에서만 발생
- GUI.Application 프로세스는 실행 중이지만 MainWindowTitle이 비어 있음

## 원인 분석

### 1. 환경 변수 설정 문제 (.NET 8 호환성)
```csharp
// 기존 코드 (실패)
startInfo.EnvironmentVariables["XRAY_E2E_MODE"] = "true";

// 수정된 코드
startInfo.Environment["XRAY_E2E_MODE"] = "true";
```
`ProcessStartInfo.EnvironmentVariables`는 .NET Framework 방식입니다. .NET 8에서는 `Environment` 속성을 사용해야 합니다.

### 2. FlaUI UIAutomation 제한 사항
- WPF MenuItem AutomationPeers가 Background Dispatcher 우선순위로 지연 등록됨
- 비대화형 세션에서 UIAutomation 트리가 완전히 초기화되지 않음
- GetMainWindow()가 30초 내에 윈도우를 찾지 못함

## 수정된 파일 (2026-03-13)

### 빌드 경고 수정 (28개 → 4개)
- `tools/GUI.Application/tests/GUI.Application.E2ETests/Infrastructure/AppFixture.cs`
  - `EnvironmentVariables` → `Environment` (.NET 8 호환)
- `tools/GUI.Application/src/GUI.Application/Core/IntToVisibilityConverter.cs`
  - XML 주석 `<` → `&lt;`
- `tools/GUI.Application/src/GUI.Application/Services/ParameterExtractorService.cs`
  - nullable 체크 추가
- `tools/ParameterExtractor/src/ParameterExtractor.Core/Services/ConfigExporter.cs`
  - null 병합 연산자 추가
- E2E 테스트 파일들
  - null-forgiving 연산자 추가

남은 4개 경고는 iTextSharp 패키지 호환성(NU1701)으로, 코드 수정 불가능합니다.

## 해결 방안

### 옵션 1: 대화형 데스크톱 세션에서 실행
```powershell
# VSCode 통합 터미널 또는 PowerShell ISE에서 실행
cd D:\workspace-github\system-emul-sim
dotnet test tools/GUI.Application/tests/GUI.Application.E2ETests/GUI.Application.E2ETests.csproj
```

### 옵션 2: CI 환경용 headless WPF 테스트
- WPF를 headless 모드로 실행
- ViewModel 단위 테스트로 분리
- FlaUI 대신 직접 WPF 테스트 접근

### 옵션 3: 테스트 타임아웃 및 재시스 로직 개선
```csharp
// AppFixture.cs
var timeout = TimeSpan.FromSeconds(60); // 30초 → 60초
// 또는 재시스 로직 추가
```

## 작업 항목
- [ ] PowerShell ISE/VSCode 통합 터미널에서 테스트 실행 시도
- [ ] 대화형 세션에서도 실패할 경우 추가 디버깅
- [ ] CI 환경용 UI 테스트 전략 수립
- [ ] 필요시 ViewModel 단위 테스트로 대체

## 참고 자료
- `tools/GUI.Application/tests/GUI.Application.E2ETests/Infrastructure/AppFixture.cs:49-97`
- `tools/GUI.Application/src/GUI.Application/App.xaml.cs:44` (E2E 모드 확인)

---
**생성일**: 2026-03-13
**연관 SPEC**: SPEC-UI-001
**추적 ID**: E2E-DEBUG-001
