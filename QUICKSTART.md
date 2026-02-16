# Quick Start Guide

프로젝트를 빠르게 시작하기 위한 핵심 가이드입니다.

## 1. 환경 설정 (10분)

### 필수 도구
```bash
# .NET 8.0 SDK
winget install Microsoft.DotNet.SDK.8

# Git 설정
git config --global user.name "Your Name"
git config --global user.email "your.email@example.com"
```

### 프로젝트 클론
```bash
git clone <repository-url>
cd system-emul-sim
cd tools && dotnet restore && dotnet build
```

## 2. 첫 작업 시작 (5분)

```bash
# 1. 브랜치 생성
git checkout -b feature/my-first-task

# 2. 작업 수행 (TDD/DDD)
# TDD: 테스트 작성 → 구현 → 리팩토링
# DDD: 특성화 테스트 → 점진적 개선

# 3. 테스트
dotnet test

# 4. 커밋
git add .
git commit -m "feat(scope): description"
```

## 3. 개발 방법론

### TDD (신규 코드)
```csharp
// RED
[Fact]
public void Test_FeatureName_Behavior() { }

// GREEN
public void FeatureName() { /* 최소 구현 */ }

// REFACTOR
// 코드 개선
```

### DDD (기존 코드/RTL)
```
ANALYZE   → 기존 동작 이해
PRESERVE  → 특성화 테스트 작성
IMPROVE   → 점진적 개선
```

## 4. 커밋 전 체크리스트

```bash
# 필수 검사
dotnet test                        # ✅ 모든 테스트 통과
dotnet format --verify-no-changes  # ✅ 코드 스타일
git secrets --scan                 # ✅ Secrets 없음

# 커버리지 확인
dotnet test --collect:"XPlat Code Coverage"
```

## 5. Pull Request

```bash
# 1. 푸시
git push origin feature/my-first-task

# 2. Gitea에서 PR 생성
# - 명확한 제목
# - 변경사항 설명
# - 리뷰어 지정

# 3. 코드 리뷰 대응
# 4. 머지 후 브랜치 삭제
```

## 6. 일상 루틴

### 매일 아침
```bash
git pull origin main
```

### 작업 중
```bash
# 자주 커밋
git add . && git commit -m "..."

# 자주 테스트
dotnet test
```

### 작업 종료
```bash
git push
```

## 7. 트러블슈팅

### 테스트 실패
```bash
# 상세 로그 확인
dotnet test --logger "console;verbosity=detailed"
```

### 빌드 실패
```bash
# 클린 빌드
dotnet clean && dotnet build
```

### Merge 충돌
```bash
git fetch origin main
git rebase origin/main
# 충돌 해결 후
git rebase --continue
```

## 8. 자주 쓰는 명령

```bash
# 시뮬레이터 실행
cd tools/PanelSimulator
dotnet run

# Integration Test
cd tools/IntegrationRunner
dotnet run -- --scenario IT-01

# 전체 테스트
dotnet test --no-build
```

## 9. 핵심 제약사항

- **FPGA**: Artix-7 XC7A35T (USB 3.x 불가능)
- **인터페이스**: CSI-2 only (4-lane, ~4-5 Gbps)
- **성능 목표**: 2048x2048@30fps
- **커버리지**: 85%+

## 10. 도움말

- **전체 문서**: `README.md`
- **초고속 참조**: `CHEATSHEET.md`
- **프로젝트 메모리**: `.claude/projects/.../memory/MEMORY.md`
- **상세 레퍼런스**: `docs/references/`

---

*Last Updated: 2026-02-16*
