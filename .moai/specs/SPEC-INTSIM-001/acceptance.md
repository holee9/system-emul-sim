# SPEC-INTSIM-001: 수용 기준 (Acceptance Criteria)

## 1. 개요 (Overview)

본 문서는 SPEC-INTSIM-001 "IntegrationTests 에뮬레이션/시뮬레이션 기능 보완"의 수용 기준을 정의합니다. 모든 기능 요구사항이 충족되었는지 검증하는 구체적인 기준을 제공합니다.

## 2. 기능적 수용 기준 (Functional Acceptance Criteria)

### AC-001: 실제 장비 없는 실행

**GIVEN:** IntegrationTests 프로젝트가 CI/CD 환경에서 실행되고
**WHEN:** 실제 하드웨어(X-ray Panel, FPGA, MCU)가 연결되지 않은 상태에서
**THEN:** 모든 19개 테스트(IT01-IT19)가 100% 통과해야 한다

**검증 방법:**
```bash
# HW 없는 환경에서 테스트 실행
cd tools/IntegrationTests
dotnet test --verbosity normal

# 예상 결과: 모든 테스트 통과, 0개 실패
```

**성공 기준:**
- 총 테스트 수: 19개 이상
- 통과: 100%
- 실패: 0개
- 실행 시간: 60초 이내

### AC-002: 파일 시스템 I/O 제거

**GIVEN:** IT19_CliRoundTripTests가 실행되고
**WHEN:** Process Monitor(Windows) 또는 strace(Linux)로 파일 시스템 접근을 모니터링할 때
**THEN:** 임시 파일 생성/삭제를 포함한 디스크 I/O가 0바이트여야 한다

**검증 방법:**
```bash
# Windows: Process Monitor 사용
# 필터: "Process Name is dotnet.exe" AND "Operation is CreateFile"
# 예상 결과: 이벤트 0개

# Linux: strace 사용
strace -e trace=openat,mkdir,unlink dotnet test
# 예상 결과: 파일 시스템 관련 시스템 콜 0개
```

**성공 기준:**
- 디스크 쓰기: 0바이트
- 디스크 읽기: 0바이트 (테스트 데이터 제외)
- 임시 파일 생성: 0개

### AC-003: 네트워크 소켓 미사용

**GIVEN:** 모든 IntegrationTests가 실행되고
**WHEN:** netstat 또는 lsof로 네트워크 포트 사용을 모니터링할 때
**THEN:** UDP/TCP 소켓이 열리지 않아야 한다

**검증 방법:**
```bash
# 테스트 실행 중 네트워크 모니터링
# Linux
lsof -i UDP -c dotnet
# 예상 결과: (출력 없음)

# Windows
netstat -an | findstr UDP
# 예상 결과: dotnet 프로세스에 의한 포트 리스닝 없음
```

**성공 기준:**
- 열린 소켓: 0개
- 바인딩된 포트: 0개
- 네트워크 패킷 송수신: 0개

### AC-004: IT15 플레이킹 해결

**GIVEN:** IT15_FrameBufferOverflowTests가 포함된 테스트 스위트가 있고
**WHEN:** 8개 스레드로 병렬 실행을 100회 반복할 때
**THEN:** 모든 실행에서 100% 통과해야 하며 단 한 번의 플레이킹도 발생하지 않아야 한다

**검증 방법:**
```bash
# 병렬 실행 반복 테스트
for i in {1..100}; do
  dotnet test --filter "IT15" --parallel
  if [ $? -ne 0 ]; then
    echo "실패: 반복 $i"
    exit 1
  fi
done
echo "성공: 100회 연속 통과"
```

**성공 기준:**
- 100회 연속 통과: 100%
- Race condition 도구 경고: 0개
- 평균 실행 시간: 5초 이내

### AC-005: CLI round-trip 인메모리 실행

**GIVEN:** IT19_CliRoundTripTests가 실행되고
**WHEN** (A) 프로세스 실행 방식과 (B) 인메모리 직접 호출 방식으로 각각 실행할 때
**THEN:** 두 방식 모두에서 동일한 결과(비트 정확성)가 나와야 하며 인메모리 방식이 50% 이상 빨라야 한다

**검증 방법:**
```csharp
// 프로세스 실행 방식
var processResult = await RunViaProcessInvoker();
processResult.ExitCode.Should().Be(0);

// 인메모리 방식
var directResult = await RunViaDirectCallInvoker();
directResult.ExitCode.Should().Be(0);

// 결과 비교
AssertResultsIdentical(processResult, directResult);
Assert.IsTrue(directResult.Duration < processResult.Duration * 0.5);
```

**성공 기준:**
- 프로세스 방식 통과: ✓
- 인메모리 방식 통과: ✓
- 결과 일치: 100% (바이트 단위 비교)
- 속도 향상: 50% 이상

### AC-006: NetworkChannel 복합 장애 시나리오

**GIVEN:** NetworkChannel이 손실 10%, 재정렬 5%, 손상 2%로 설정되고
**WHEN:** 1000개의 UDP 패킷이 전송될 때
**THEN:** 각 장애 유형이 지정된 비율 ±5% 범위 내에서 발생하고 최종 재조립된 프레임의 무결성이 유지되어야 한다

**검증 방법:**
```csharp
var config = new NetworkChannelConfig {
    PacketLossRate = 0.10,
    ReorderRate = 0.05,
    CorruptionRate = 0.02,
    Seed = 42
};

var channel = new NetworkChannel(config);
var packets = GenerateTestPackets(1000);
var output = channel.TransmitPackets(packets);

// 장애 발생 비율 검증
var lossRatio = (double)channel.PacketsLost / 1000;
lossRatio.Should().BeApproximately(0.10, 0.05);

var reorderRatio = (double)channel.PacketsReordered / 1000;
reorderRatio.Should().BeApproximately(0.05, 0.05);

var corruptionRatio = (double)channel.PacketsCorrupted / 1000;
corruptionRatio.Should().BeApproximately(0.02, 0.05);

// 최종 프레임 무결성 검증
var reassembled = ReassembleFrame(output);
reassembled.IsValid.Should().BeTrue();
```

**성공 기준:**
- 손실 비율: 10% ± 5% (5-15%)
- 재정렬 비율: 5% ± 5% (0-10%)
- 손상 비율: 2% ± 5% (0-7%)
- 최종 프레임 무결성: 100%

## 3. 비기능적 수용 기준 (Non-Functional Acceptance Criteria)

### NFR-001: 성능

**단일 테스트 성능:**
- WHEN:任意의 단일 테스트를 실행할 때
- THEN: 실행 시간이 5초 이내여야 한다

**테스트 스위트 성능:**
- WHEN: 전체 테스트 스위트를 실행할 때
- THEN: 총 실행 시간이 60초 이내여야 한다

**검증 방법:**
```bash
# 단일 테스트 타이밍
dotnet test --filter "IT001" --diag timing.txt

# 전체 스위트 타이밍
dotnet test --verbosity normal
# 예상: Total time: 00:00:XX.XX (XX < 60)
```

### NFR-002: 테스트 커버리지

**코드 커버리지:**
- WHEN: Code coverage 도구를 실행할 때
- THEN: IntegrationTests 프로젝트의 커버리지가 85% 이상이어야 한다

**시나리오 커버리지:**
- WHEN: 에뮬레이션 시나리오를 분석할 때
- THEN: 새로운 복합 장애 시나리오 커버리지가 90% 이상이어야 한다

**검증 방법:**
```bash
# 커버리지 수집
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# 리포트 생성
reportgenerator -reports:coverage.xml -targetdir:coverage-report

# 예상 결과: Line coverage >= 85%
```

### NFR-003: 크로스 플랫폼 호환성

**운영체제 호환성:**
- WHEN: Windows, Linux, macOS에서 테스트를 실행할 때
- THEN: 모든 플랫폼에서 동일한 결과(100% 통과)가 나와야 한다

**검증 방법:**
```bash
# Windows
PowerShell> dotnet test

# Linux
$ dotnet test

# macOS
$ dotnet test
```

**성공 기준:**
- Windows: 통과 100%
- Linux: 통과 100%
- macOS: 통과 100%

## 4. 품질 게이트 (Quality Gates)

### TRUST 5 검증

**Tested (테스트됨):**
- [ ] 모든 새로운 기능에 단위 테스트 존재
- [ ] xUnit 테스트 커버리지 85%+ 달성
- [ ] 경계 값 테스트 포함

**Readable (가독성):**
- [ ] 코드 명확성: 클래스/메서드 이름이 의도를 명확히 표현
- [ ] XML 문화 주석: 모든 public API에 주석 존재
- [ ] StyleCop 규칙 통과

**Unified (통일성):**
- [ ] 일관된 코드 스타일: dotnet format 적용
- [ ] 네이밍 규칙: C# 관례 준수
- [ ] 파일 구조: 프로젝트 표준 준수

**Secured (보안):**
- [ ] 외부 입력 검증: IFileSystem 경로 검증
- [ ] CWE-252(未检查的返回值) 검증
- [ ] 예외 처리: 모든 외부 호출에 try-catch

**Trackable (추적 가능성):**
- [ ] Git 커밋 메시지: Conventional Commits 형식
- [ ] SPEC 참조: 커밋 메시지에 "SPEC-INTSIM-001" 포함
- [ ] 변경 로그: 각 Phase의 변경 사항 기록

### CI/CD 게이트

**자동화 테스트:**
- [ ] 모든 PR이 CI 파이프라인 통과
- [ ] 실제 HW 없는 환경에서 실행 검증
- [ ] 코드 커버리지 리포트 자동 생성

**코드 리뷰:**
- [ ] 최소 1명의 리뷰어 승인
- [ ] 모든 코멘트 해결
- [ ] Squash merge 또는 Rebase merge 사용

## 5. Given-When-Then 시나리오 상세

### 시나리오 1: 병렬 테스트 격리성

```gherkin
Scenario: 병렬 실행에서 테스트 격리성 보장
  Given IT15_FrameBufferOverflowTests를 포함한 5개의 테스트가 존재하고
  And 각 테스트가 독립적인 SimulatorPipeline 인스턴스를 사용하며
  When 테스트가 8개 스레드로 병렬 실행될 때
  Then 모든 테스트가 독립적으로 통과해야 하고
  And 테스트 간 상태 공유로 인한 부작용이 발생하지 않아야 하며
  And 100회 반복 실행에서 100% 통과율을 유지해야 한다
```

### 시나리오 2: 파일 시스템 없는 CLI 테스트

```gherkin
Scenario: 인메모리 파일 시스템을 사용한 CLI 테스트
  Given IT19_CliRoundTripTests가 실행되고
  And MemoryFileSystem이 IFileSystem 인터페이스로 주입되며
  When 실제 파일 시스템에 쓰기 권한이 없는 환경에서 테스트가 실행될 때
  Then 인메모리 스트림을 사용하여 테스트가 통과해야 하고
  And 디스크 I/O가 0바이트여야 하며
  And 프로세스 실행 방식과 동일한 결과가 나와야 한다
```

### 시나리오 3: 네트워크 인프라 없는 실행

```gherkin
Scenario: 네트워크 인터페이스가 없는 환경에서의 테스트 실행
  Given 네트워크 인터페이스가 비활성화된 환경이고
  And NetworkChannel이 인메모리 패킷 처리를 사용하며
  When 모든 IntegrationTests가 실행될 때
  Then 네트워크 소켓을 사용하지 않고 테스트가 통과해야 하고
  And NetworkChannel의 패킷 손실/재정렬/손상 기능이 정상 작동해야 하며
  And 모든 테스트가 HW 없는 환경에서 실행 가능해야 한다
```

### 시나리오 4: 복합 장애 시뮬레이션

```gherkin
Scenario: NetworkChannel 복합 장애 시나리오 시뮬레이션
  Given NetworkChannel이 다음 설정으로 구성되어 있다:
    | 손실률  | 재정렬률 | 손상률 |
    | 0.10   | 0.05    | 0.02  |
  And 2048x2048 크기의 테스트 프레임이 준비되고
  When 1000개의 UDP 패킷이 NetworkChannel을 통과할 때
  Then 각 장애 유형이 지정된 비율 ±5% 범위 내에서 발생하고
  And 최종 재조립된 프레임의 무결성이 유지되어야 하며
  And 원본 프레임과 비트 정확하게 일치해야 한다
```

## 6. 회귀 방지 기준 (Regression Prevention)

### 기존 테스트 회귀 방지

**GIVEN:** SPEC-INTSIM-001 구현 전
**WHEN:** 기존 19개 테스트를 실행할 때
**THEN:** 기준 결과(Baseline)을 기록

**GIVEN:** SPEC-INTSIM-001 구현 후
**WHEN:** 동일한 19개 테스트를 실행할 때
**THEN:** 기준 결과와 100% 일치해야 한다

**검증 방법:**
```bash
# 기준 결과 기록
dotnet test --logger "trx;LogFileName=baseline.trx"

# 구현 후 결과 비교
dotnet test --logger "trx;LogFileName=after.trx"

# 두 결과 비교
diff baseline.trx after.trx
# 예상 결과: Passed 테스트 수 동일, Failed 테스트 수 0
```

### 성능 회귀 방지

**GIVEN:** SPEC-INTSIM-001 구현 전 실행 시간
**WHEN:** SPEC-INTSIM-001 구현 후 실행 시간을 측정할 때
**THEN:** 성능 저하가 20% 이하여야 한다 (인메모리 실행으로 오히려 향상 예상)

## 7. 정의 완료 기준 (Definition of Done)

각 기능에 대한 DoD 체크리스트:

- [ ] 단위 테스트 작성 완료
- [ ] 통합 테스트 통과
- [ ] 코드 리뷰 완료 및 승인
- [ ] XML 문화 주석 추가
- [ ] StyleCop 경고 없음
- [ ] Git 커밋 (Conventional Commits)
- [ ] CI/CD 파이프라인 통과
- [ ] 성능 기준 충족
- [ ] 보안 검증 통과
- [ ] 문서화 업데이트

## 8. 수용 테스트 절차 (Acceptance Test Procedure)

### 8.1 사전 준비

1. 개발 환경: .NET 8 SDK 설치
2. 리포지토리: 최신 main 브랜치
3. 브랜치: `feature/SPEC-INTSIM-001` 생성

### 8.2 테스트 실행

```bash
# 1. 의존성 복원
dotnet restore

# 2. 빌드
dotnet build --no-restore

# 3. 단위 테스트
dotnet test --no-build --verbosity normal

# 4. 커버리지 수집
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# 5. 성능 측정
time dotnet test
```

### 8.3 수용 결정

**조건부 승인 (Conditional Approval):**
- AC-001~AC-004 충족: 승인
- AC-005 또는 AC-006 미충족: 조건부 승인 (후속 작업 필요)

**거절 (Rejection):**
- 회귀 발생 (기존 테스트 실패)
- 파일 시스템 I/O 발견
- 네트워크 소켓 사용 발견

---

**문서 버전:** 1.0.0
**마지막 업데이트:** 2026-03-11
**작성자:** manager-spec
