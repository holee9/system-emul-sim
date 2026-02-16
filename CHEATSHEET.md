# Quick Reference Cheat Sheet

초고속 참조를 위한 핵심 명령어와 체크리스트입니다.

## 🚀 일상 작업

### 작업 시작
```bash
git pull
git checkout -b feature/my-feature
```

### 개발 중
```bash
# TDD (신규): RED → GREEN → REFACTOR
# DDD (기존): ANALYZE → PRESERVE → IMPROVE

# 자주 커밋
git add . && git commit -m "feat: ..."
```

### 커밋 전 체크
```bash
dotnet test                        # 모든 테스트
dotnet format --verify-no-changes  # 코드 스타일
git secrets --scan                 # Secrets 검사
```

## 📋 필수 기준

### TRUST 5
- **T**ested: 85%+ 커버리지
- **R**eadable: 영어 주석, 명확한 이름
- **U**nified: 일관된 스타일
- **S**ecured: OWASP, No secrets
- **T**rackable: 명확한 커밋

### 커버리지 목표
- RTL: Line ≥95%, Branch ≥90%, FSM 100%
- SW: 모듈당 80-90%

## ⚠️ 절대 금지

- ❌ USB 3.x 제안 (FPGA 리소스 부족)
- ❌ 1 GbE로 목표/최대 계층 지원
- ❌ detector_config.yaml 외 중복 설정
- ❌ 테스트 없는 코드
- ❌ Secrets 커밋

## 🔧 자주 쓰는 명령

```bash
# 빌드 & 테스트
dotnet build && dotnet test

# 커버리지
dotnet test --collect:"XPlat Code Coverage"

# RTL 시뮬레이션
vivado -mode batch -source run_tests.tcl

# Integration Test
cd tools/IntegrationRunner
dotnet run -- --scenario IT-01
```

## 📊 핵심 제약

- **FPGA**: Artix-7 XC7A35T (LUT 20,800)
- **목표 사용률**: <60% (12,480 LUTs)
- **CSI-2**: 4-lane, ~1.0-1.25 Gbps/lane
- **성능 목표**: 2048x2048@30fps

## 🎯 현재 마일스톤

**M0 (W1)**: P0 결정 확정
- [ ] 성능 목표
- [ ] Host 링크 (10 GbE 권장)
- [ ] SoC 플랫폼

---

*전체 문서: README.md, QUICKSTART.md, MEMORY.md*
