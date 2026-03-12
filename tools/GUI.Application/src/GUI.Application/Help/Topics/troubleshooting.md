# 문제 해결 (FAQ)

## 자주 묻는 질문

### Q: 앱이 시작되지 않습니다

**원인**: .NET 8 런타임이 설치되지 않았을 수 있습니다.

**해결**: .NET 8.0 Desktop Runtime을 설치하세요.
- 다운로드: https://dotnet.microsoft.com/download/dotnet/8.0

### Q: 프레임이 표시되지 않습니다

**원인**: 시뮬레이션이 시작되지 않았습니다.

**해결**:
1. **Ctrl+R** 또는 **Start** 버튼 클릭
2. Frame Preview 탭 확인
3. Status Dashboard에서 Frames Received 값 확인

### Q: 성능이 느립니다

**원인**: 고해상도 설정 또는 낮은 사양 PC

**해결**:
1. Rows/Cols를 128x128로 줄이기
2. Frame Rate를 낮추기 (5 fps)
3. NoiseType을 None으로 설정

### Q: 로그 파일 위치는?

**위치**: 실행 파일 경로의 `logs/` 폴더
- 예: `GUI.Application.exe` 실행 시 → `logs/app_YYYYMMDD.log`
