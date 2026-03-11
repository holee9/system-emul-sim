# SPEC-INTSIM-001: 구현 계획 (Implementation Plan)

## 1. 개요 (Overview)

본 계획은 IntegrationTests 프로젝트의 실제 장비 의존성을 제거하고 순수 에뮬레이션/시뮬레이션 환경으로 완전히 전환하는 것을 목표로 합니다.

## 2. 기술 전략 (Technical Strategy)

### 2.1 아키텍처 접근

**핵심 원칙:**
1. **Pure In-Memory Execution**: 모든 I/O를 가상화
2. **Test Isolation**: 각 테스트의 독립성 보장
3. **Mock/Stub Strategy**: 실제 의존성 추상화
4. **Incremental Refactoring**: 기존 테스트 회귀 방지

### 2.2 설계 패턴

**Dependency Injection 패턴 적용:**
- IFileSystem 인터페이스를 통한 파일 시스템 가상화
- IUdpTransport 인터페이스를 통한 네트워킹 추상화
- ICliInvoker 인터페이스를 통한 CLI 실행 방식 다양화

**Factory 패턴 활용:**
- MockFactory를 통한 테스트 더블 생성
- TestFrameFactory와의 통합 유지

## 3. 단계별 구현 계획 (Phased Implementation)

### Phase 1: 의존성 분석 및 설계 (2-3일)

**목표:** 모든 외부 의존성 식별 및 Mock 인터페이스 설계

**작업:**
1. IntegrationTests 프로젝트의 모든 파일 시스템 I/O 지점 분석
2. 네트워킹 소켓 사용 지점 분석
3. CLI 프로세스 실행 지점 분석
4. Mock 인터페이스 설계 문서 작성

**산출물:**
- `docs/dependency-analysis.md`
- `Helpers/Mock/IFileSystem.cs`
- `Helpers/Mock/IUdpTransport.cs`
- `Helpers/Mock/ICliInvoker.cs`

**성공 기준:**
- 모든 외부 의존성 지점이 문서화됨
- Mock 인터페이스가 설계됨

### Phase 2: 파일 시스템 Mock 구현 (3-4일)

**목표:** 실제 파일 시스템을 사용하지 않고 테스트 실행 가능

**작업:**
1. IFileSystem 인터페이스 정의
2. MemoryFileSystem 구현 (인메모리 가상 파일 시스템)
3. TestFrameFactory에 IFileSystem 통합
4. 기존 코드를 IFileSystem 사용하도록 리팩토링

**파일 변경:**
- **새로운 파일:**
  - `Helpers/Mock/IFileSystem.cs`
  - `Helpers/Mock/MemoryFileSystem.cs`
  - `Helpers/Mock/MemoryFileStream.cs`
  - `Helpers/Mock/MemoryFileInfo.cs`

- **수정 파일:**
  - `Helpers/TestFrameFactory.cs` (IFileSystem 주입)
  - `Integration/IT19_CliRoundTripTests.cs` (MemoryFileSystem 사용)

**테스트:**
- MemoryFileSystem 단위 테스트
- IT19 파일 I/O 가상화 검증

**성공 기준:**
- IT19가 실제 파일을 생성하지 않고 통과
- Process Monitor로 디스크 I/O 0 확인

### Phase 3: 네트워킹 스택 검증 (1-2일)

**목표:** NetworkChannel이 실제 소켓을 사용하지 않음을 확인 및 보장

**작업:**
1. NetworkChannel 소스 코드 검토
2. 실제 소켓 사용 여부 확인
3. 필요시 IUdpTransport 인터페이스 추출
4. InMemoryUdpTransport 구현 (필요시)

**파일 변경:**
- **검토:**
  - `Helpers/NetworkChannel.cs`
  - `Helpers/NetworkChannelConfig.cs`

- **새로운 파일 (필요시):**
  - `Helpers/Mock/IUdpTransport.cs`
  - `Helpers/Mock/InMemoryUdpTransport.cs`

**테스트:**
- 네트워크 포트 사용 모니터링
- NetworkChannel 단위 테스트 강화

**성공 기준:**
- 네트워크 소켓을 사용하지 않음이 확인됨
- 테스트 실행 중 네트워크 포트 미사용

### Phase 4: CLI 실행 방식 개선 (4-5일)

**목표:** CLI 프로세스 실행과 인메모리 직접 호출 방식 모두 지원

**작업:**
1. ICliInvoker 인터페이스 설계
2. DirectCallInvoker 구현 (프로세스 없이 직접 호출)
3. ProcessInvoker 구현 (기존 프로세스 실행 방식)
4. 테스트에서 실행 방식 선택 지원

**파일 변경:**
- **새로운 파일:**
  - `Helpers/Cli/ICliInvoker.cs`
  - `Helpers/Cli/DirectCallInvoker.cs`
  - `Helpers/Cli/ProcessInvoker.cs`
  - `Helpers/Cli/CliInvocationResult.cs`

- **수정 파일:**
  - `Integration/IT19_CliRoundTripTests.cs` (ICliInvoker 사용)
  - CLI 프로젝트의 Program.cs (직접 호출 지원)

**테스트:**
- DirectCallInvoker 단위 테스트
- ProcessInvoker 단위 테스트
- IT19 두 방식 모두로 실행

**성공 기준:**
- IT19가 두 방식 모두로 통과
- 인메모리 방식이 프로세스 방식보다 빠름

### Phase 5: 플레이킹 테스트 수정 (3-4일)

**목표:** IT15_FrameBufferOverflowTests의 스레드 안전성 보장

**작업:**
1. Race condition 원인 분석
2. FrameBufferManager 스레드 안전성 검토
3. 적절한 동기화 메커니즘 추가
4. 병렬 실행 테스트 강화

**파일 변경:**
- **검토/수정:**
  - `Integration/IT15_FrameBufferOverflowTests.cs`
  - `McuSimulator/Core/Buffer/FrameBufferManager.cs` (필요시)

- **새로운 테스트:**
  - `Integration/IT15_ParallelExecutionTests.cs` (별도 병렬 테스트)

**테스트:**
- 병렬 실행 100회 연속 테스트
- 다양한 스레드 수로 테스트 (2, 4, 8 스레드)

**성공 기준:**
- 병렬 실행 100회 연속 통과
- Race condition 도구(예: ThreadSanitizer) 경고 없음

### Phase 6: NetworkChannel 시나리오 확장 (2-3일)

**목표:** 복합 장애 시나리오 지원

**작업:**
1. NetworkChannelConfig에 복합 설정 추가
2. IT20_NetworkComplexScenarios 테스트 추가
3. 경계 값 테스트 (극단적 손실률 등)

**파일 변경:**
- **수정:**
  - `Helpers/NetworkChannelConfig.cs`

- **새로운 파일:**
  - `Integration/IT20_NetworkComplexScenarios.cs`

**테스트 케이스:**
- 손실 10% + 재정렬 5% + 손상 2%
- 손실 50% (극단적)
- 재정렬 20% (극단적)
- 손상 10% (극단적)

**성공 기준:**
- 복합 장애 시나리오 테스트 통과
- 경계 값 테스트 통과

### Phase 7: 문서화 (1-2일)

**목표:** 모든 변경 사항 문서화

**작업:**
1. README.md 업데이트
2. Mock 사용 가이드 추가
3. CI/CD 통합 가이드 업데이트

**파일 변경:**
- `tools/IntegrationTests/README.md`
- `docs/MOCK_USAGE.md` (새 파일)
- `.github/workflows/ci.yml` (필요시)

## 4. 병렬 실행 가능 작업 (Parallelizable Tasks)

다음 작업들은 독립적으로 병렬 실행 가능:

**Group A (Phase 2 시작 후):**
- Phase 3: 네트워킹 스택 검증
- Phase 4: CLI 실행 방식 개선 (초기 설계)

**Group B (Phase 2 완료 후):**
- Phase 5: 플레이킹 테스트 수정
- Phase 6: NetworkChannel 시나리오 확장

## 5. 위험 관리 (Risk Management)

### 5.1 기술적 위험

| 위험 | 확률 | 영향 | 완화 계획 |
|------|------|------|-----------|
| 기존 테스트 회귀 | MEDIUM | HIGH | 점진적 리팩토링, 각 Phase마다 회귀 테스트 |
| Mock 인터페이스 과도한 복잡성 | MEDIUM | MEDIUM | 단순한 인터페이스 설계, YAGNI 원칙 |
| 성능 저하 | LOW | MEDIUM | 인메모리 실행으로 오히려 성능 향상 예상 |
| CLI 직접 호출 호환성 | LOW | LOW | 기존 방식과 병행 지원 |

### 5.2 일정 위험

| 위험 | 완화 계획 |
|------|-----------|
| Phase 5 (플레이킹 수정) 예상보다 오래 소요 | Race condition 분석에 충분한 시간 할당 |
| 의존성 분석에서 예상치 못한 이슈 발견 | Phase 1을 충분히 길게 설정 (2-3일) |

## 6. 마일스톤 및 예상 일정 (Milestones & Timeline)

**총 예상 기간:** 15-20일

### Milestone 1: 기반 구축 (Day 1-5)
- Phase 1: 의존성 분석 및 설계 완료
- Phase 2: 파일 시스템 Mock 구현 시작
- Phase 3: 네트워킹 스택 검증 완료

### Milestone 2: 핵심 기능 (Day 6-12)
- Phase 2: 파일 시스템 Mock 완료
- Phase 4: CLI 실행 방식 개선 완료
- Phase 5: 플레이킹 테스트 수정 완료

### Milestone 3: 확장 및 완료 (Day 13-20)
- Phase 6: NetworkChannel 시나리오 확장
- Phase 7: 문서화 완료
- 모든 테스트 통과 및 코드 리뷰 완료

## 7. 성공 측정 (Success Metrics)

### 정량적 지표
- [ ] 모든 19개 기존 테스트 통과 (0% 회귀)
- [ ] IT15 병렬 실행 100회 연속 통과
- [ ] 파일 시스템 I/O 0바이트 (Process Monitor)
- [ ] 네트워크 포트 미사용 (Netstat)
- [ ] 새로운 테스트 커버리지 90%+

### 정성적 지표
- [ ] 코드 리뷰 통과
- [ ] CI/CD 파이프라인 통과
- [ ] 문서화 완료
- [ ] 팀 승인

## 8. 롤백 계획 (Rollback Plan)

각 Phase는 Git 브랜치에서 진행하여 문제 발생시 롤백 가능:

- Phase별 feature 브랜치: `feature/intsim-phase{N}`
- 문제 발생시 해당 브랜치 폐기
- main 브랜치는 안정 상태 유지

## 9. 다음 단계 (Next Steps)

1. **즉시 실행:** Phase 1 시작 - 의존성 분석
2. **리뷰 요청:** 본 계획서 승인
3. **환경 설정:** 개발 브랜치 생성
4. **첫 번째 커밋:** Mock 인터페이스 설계

---

**문서 버전:** 1.0.0
**마지막 업데이트:** 2026-03-11
**작성자:** manager-spec
