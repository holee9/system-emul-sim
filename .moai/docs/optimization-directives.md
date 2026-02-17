# ABYZ-Lab-ADK Optimization Directives

프로젝트 초기 설정 대비 수정/최적화/추가된 운영 지침 정리본

**프로젝트**: X-ray Detector Panel System (system-emul-sim)
**적용 기간**: 2026-02-16
**목적**: 컨텍스트 사용 최적화 및 워크플로우 개선

---

## 목차

1. [개요](#개요)
2. [카테고리별 최적화](#카테고리별-최적화)
   - [컨텍스트 최적화](#1-컨텍스트-최적화)
   - [질문 빈도 최적화](#2-질문-빈도-최적화)
   - [MCP 통합](#3-mcp-통합)
   - [언어 설정](#4-언어-설정)
3. [적용 방법](#적용-방법)
4. [검증 체크리스트](#검증-체크리스트)
5. [재사용 템플릿](#재사용-템플릿)

---

## 개요

### 최적화 목표

- **컨텍스트 사용량**: 60% 절감 (10,000 → 4,000 tokens for typical sessions)
- **질문 빈도**: 70-80% 감소 (3-5회 → 0-1회 per task)
- **워크플로우**: 중단 없는 연속 실행
- **응답 속도**: 불필요한 대기 시간 제거

### 주요 원칙

1. **조건부 로딩**: 필요한 규칙만 필요할 때 로드
2. **최소 질문**: 시스템 파괴 위험 외에는 자동 진행
3. **자동 결정**: Best practices 기반 기술 결정
4. **세션 일관성**: 한 번 결정한 사항은 세션 전체 적용

---

## 카테고리별 최적화

### 1. 컨텍스트 최적화

#### 1.1 Paths Frontmatter 추가 (조건부 로딩)

**목적**: 규칙 파일을 관련 작업 시에만 로드하여 컨텍스트 사용량 절감

**적용 대상**:
- 언어별 규칙 (16개): 이미 적용됨
- 워크플로우 규칙 (3개): 신규 적용
- 개발 규칙 (3개): 신규 적용

**변경 내역**:

| 파일 | Paths 패턴 | 로딩 조건 |
|------|-----------|----------|
| `spec-workflow.md` | `.abyz-lab/specs/**/*`, `**/spec.md` | SPEC 문서 작업 시 |
| `workflow-modes.md` | `.abyz-lab/specs/**/*`, `**/*test*`, `quality.yaml` | 개발 방법론 관련 작업 |
| `file-reading-optimization.md` | `**/*.{py,ts,js,go,cs,cpp,rs,kt,...}` | 대용량 소스 코드 작업 시 |
| `agent-authoring.md` | `.claude/agents/**/*` | 에이전트 정의 작업 시 |
| `skill-authoring.md` | `.claude/skills/**/*` | 스킬 정의 작업 시 |
| `coding-standards.md` | `.claude/**/*`, `.abyz-lab/**/*`, `CLAUDE.md` | 프로젝트 규칙 수정 시 |

**예상 효과**:
- 일반 대화: ~10,000 → ~4,000 tokens (60% 절감)
- C# 프로젝트 작업: ~15,000 → ~6,000 tokens (60% 절감)
- SPEC 문서 작성: ~20,000 → ~8,000 tokens (60% 절감)

**Git Commit**:
```
34b6e6c refactor(rules): Add paths frontmatter for conditional loading
```

#### 1.2 언어별 규칙 Paths 패턴

언어별 규칙 파일에 이미 적용된 paths 패턴:

```yaml
# C++
---
paths:
  - "**/*.cpp"
  - "**/*.hpp"
  - "**/*.h"
  - "**/*.cc"
  - "**/CMakeLists.txt"
---

# C#
---
paths:
  - "**/*.cs"
  - "**/*.csproj"
  - "**/*.sln"
---

# Python
---
paths:
  - "**/*.py"
  - "**/pyproject.toml"
  - "**/requirements*.txt"
---

# TypeScript
---
paths:
  - "**/*.ts"
  - "**/*.tsx"
  - "**/tsconfig.json"
---

# Go
---
paths:
  - "**/*.go"
  - "**/go.mod"
---

# Rust
---
paths:
  - "**/*.rs"
  - "**/Cargo.toml"
---
```

**전체 언어 목록**:
cpp, csharp, elixir, flutter, go, java, javascript, kotlin, php, python, r, ruby, rust, scala, swift, typescript

---

### 2. 질문 빈도 최적화

#### 2.1 ABYZ-Lab Constitution 강화

**위치**: `.claude/rules/abyz-lab/core/abyz-lab-constitution.md`

**추가된 섹션**: User Interaction Constraints

```markdown
## User Interaction Constraints

Minimize AskUserQuestion usage to avoid interrupting workflow. Ask only when absolutely necessary.

Rules:
- **ONLY ask when**: System destruction risk (rm -rf, DROP DATABASE, force push),
  data loss risk, security compromise, or technically impossible to proceed
- **NEVER ask for**: Progress confirmation, style preferences (already defined),
  completion acknowledgment, optional improvements, minor decisions
- **After user approval**: Execute immediately without additional questions
- **Use best practices**: Make technical decisions automatically based on established patterns
- **One-time decisions**: User's choice applies to entire session scope unless explicitly changed
```

**Git Commit**:
```
e144fbc refactor(rules): Minimize AskUserQuestion usage
```

#### 2.2 CLAUDE.md Section 8 확장

**위치**: `CLAUDE.md` Section 8

**추가된 섹션**: Minimal Question Policy

```markdown
### Minimal Question Policy

**CRITICAL**: Minimize AskUserQuestion usage to avoid workflow interruption.

**ONLY ask when**:
- System destruction risk (rm -rf /, DROP DATABASE, git push --force to main)
- Data loss risk (overwriting uncommitted changes, deleting files without backup)
- Security compromise (exposing credentials, disabling security features)
- Technically impossible to proceed (ambiguous requirements, conflicting constraints)

**NEVER ask for**:
- Progress confirmation ("계속 진행할까요?", "Should I continue?")
- Style preferences (already defined in rules)
- Completion acknowledgment ("완료했습니다. 확인하시겠습니까?")
- Optional improvements ("더 최적화할까요?")
- Minor technical decisions (use best practices automatically)

**After user approval**:
- Execute immediately without additional questions
- Apply user's decision to entire session scope
- Make technical decisions automatically based on established patterns

**Example**:
❌ Bad: User approved plan → Ask "파일 A를 먼저 수정할까요?"
✅ Good: User approved plan → Execute all changes silently
```

#### 2.3 MEMORY.md 즉시 실행 정책 강화

**위치**: `C:\Users\user\.claude\projects\D--workspace-github-system-emul-sim\memory\MEMORY.md`

**강화된 정책**: 즉시 실행 정책 (CRITICAL)

**핵심 변경사항**:

1. **질문 금지 항목** (HARD RULE):
   - ❌ 진행 확인 ("계속 진행할까요?")
   - ❌ 파일 순서 ("A 파일부터 할까요?")
   - ❌ 완료 확인 ("완료했습니다. 확인?")
   - ❌ 선택적 개선 ("더 최적화할까요?")
   - ❌ 소소한 기술 결정

2. **질문 허용 항목** (최소한만):
   - ✅ 시스템 파괴 위험만
   - ✅ 보안 침해 위험만
   - ✅ 기술적 불가능만
   - ✅ 초기 접근법 (최초 1회만)

3. **강제 규칙**:
   - 승인 후 **절대 추가 질문 없음**
   - 기술 결정은 **자동 진행**
   - 한 세션 내 동일 유형 질문은 **1회만**

**예상 효과**:
- 파일 3개 수정: 3-5회 → 0-1회 질문 (80%+ 감소)
- SPEC 실행: 5-7회 → 1-2회 질문 (70%+ 감소)
- 일반 작업: 2-4회 → 0-1회 질문 (75%+ 감소)

---

### 3. MCP 통합

#### 3.1 Codex MCP 추가

**위치**: `.mcp.json`

**추가된 서버**:
```json
{
  "mcpServers": {
    "codex": {
      "command": "npx",
      "args": ["-y", "@anysphere/codex-mcp"]
    }
  }
}
```

**문서화**: `.claude/rules/abyz-lab/core/mcp-integration.md`

**용도**:
- AI 기반 코드 검색
- 시맨틱 코드 분석
- 코드 패턴 발견

**Git Commit**:
```
0197d68 feat(config): Add Codex MCP permissions
```

---

### 4. 언어 설정

#### 4.1 대화 언어 변경

**위치**: `.abyz-lab/config/sections/language.yaml`

**초기 설정**: 한국어 (ko)
**현재 설정**: 영어 (en)

```yaml
language:
  conversation_language: "en"  # ko → en
  conversation_language_name: "English"
  agent_prompt_language: "en"
  git_commit_messages: "en"
  code_comments: "en"
  documentation: "en"
  error_messages: "en"
```

**변경 사유**:
- 국제 협업 대비
- 문서 일관성
- 기술 용어 정확성

---

## 적용 방법

### 신규 프로젝트에 적용

#### Step 1: 기본 구조 복사
```bash
# ABYZ-Lab-ADK 구조 복사
cp -r system-emul-sim/.claude new-project/.claude
cp -r system-emul-sim/.abyz-lab new-project/.abyz-lab
cp system-emul-sim/CLAUDE.md new-project/CLAUDE.md
```

#### Step 2: 언어별 규칙 확인
```bash
# 언어별 규칙 paths frontmatter 확인
grep -r "^paths:" new-project/.claude/rules/abyz-lab/languages/
```

모든 언어 규칙에 paths가 있어야 함.

#### Step 3: 워크플로우/개발 규칙 확인
```bash
# 워크플로우 규칙 paths 확인
head -10 new-project/.claude/rules/abyz-lab/workflow/*.md

# 개발 규칙 paths 확인
head -10 new-project/.claude/rules/abyz-lab/development/*.md
```

#### Step 4: 질문 정책 확인
```bash
# abyz-lab-constitution.md에 User Interaction Constraints 섹션 확인
grep -A 10 "User Interaction Constraints" new-project/.claude/rules/abyz-lab/core/abyz-lab-constitution.md

# CLAUDE.md Section 8에 Minimal Question Policy 확인
grep -A 20 "Minimal Question Policy" new-project/CLAUDE.md
```

#### Step 5: 프로젝트별 설정 조정
```bash
# 프로젝트 정보 수정
vim new-project/.abyz-lab/config/sections/project.yaml

# 개발 방법론 선택
vim new-project/.abyz-lab/config/sections/quality.yaml
# development_mode: ddd, tdd, or hybrid

# 언어 설정
vim new-project/.abyz-lab/config/sections/language.yaml
```

---

### 기존 프로젝트에 적용

#### Step 1: Paths Frontmatter 추가

**워크플로우 규칙** (3개):

```bash
# spec-workflow.md
cat > temp.txt << 'EOF'
---
paths:
  - ".abyz-lab/specs/**/*"
  - "**/spec.md"
---

EOF
cat temp.txt existing-project/.claude/rules/abyz-lab/workflow/spec-workflow.md > temp2.txt
mv temp2.txt existing-project/.claude/rules/abyz-lab/workflow/spec-workflow.md

# workflow-modes.md
cat > temp.txt << 'EOF'
---
paths:
  - ".abyz-lab/specs/**/*"
  - "**/*test*"
  - ".abyz-lab/config/sections/quality.yaml"
---

EOF
cat temp.txt existing-project/.claude/rules/abyz-lab/workflow/workflow-modes.md > temp2.txt
mv temp2.txt existing-project/.claude/rules/abyz-lab/workflow/workflow-modes.md

# file-reading-optimization.md
cat > temp.txt << 'EOF'
---
paths:
  - "**/*.{py,ts,tsx,js,jsx,go,java,cs,cpp,hpp,rs,kt,scala,swift,php,rb,ex}"
  - "**/*.{sv,v,vhd,vhdl}"
---

EOF
cat temp.txt existing-project/.claude/rules/abyz-lab/workflow/file-reading-optimization.md > temp2.txt
mv temp2.txt existing-project/.claude/rules/abyz-lab/workflow/file-reading-optimization.md
```

**개발 규칙** (3개):

```bash
# agent-authoring.md
cat > temp.txt << 'EOF'
---
paths:
  - ".claude/agents/**/*"
---

EOF
cat temp.txt existing-project/.claude/rules/abyz-lab/development/agent-authoring.md > temp2.txt
mv temp2.txt existing-project/.claude/rules/abyz-lab/development/agent-authoring.md

# skill-authoring.md
cat > temp.txt << 'EOF'
---
paths:
  - ".claude/skills/**/*"
---

EOF
cat temp.txt existing-project/.claude/rules/abyz-lab/development/skill-authoring.md > temp2.txt
mv temp2.txt existing-project/.claude/rules/abyz-lab/development/skill-authoring.md

# coding-standards.md
cat > temp.txt << 'EOF'
---
paths:
  - ".claude/**/*"
  - ".abyz-lab/**/*"
  - "CLAUDE.md"
---

EOF
cat temp.txt existing-project/.claude/rules/abyz-lab/development/coding-standards.md > temp2.txt
mv temp2.txt existing-project/.claude/rules/abyz-lab/development/coding-standards.md
```

#### Step 2: 질문 정책 추가

**abyz-lab-constitution.md 업데이트**:

```bash
# User Interaction Constraints 섹션 추가
# (ABYZ-Lab Orchestrator 섹션 다음에 삽입)
```

내용:
```markdown
## User Interaction Constraints

Minimize AskUserQuestion usage to avoid interrupting workflow. Ask only when absolutely necessary.

Rules:
- **ONLY ask when**: System destruction risk (rm -rf, DROP DATABASE, force push), data loss risk, security compromise, or technically impossible to proceed
- **NEVER ask for**: Progress confirmation, style preferences (already defined), completion acknowledgment, optional improvements, minor decisions
- **After user approval**: Execute immediately without additional questions
- **Use best practices**: Make technical decisions automatically based on established patterns
- **One-time decisions**: User's choice applies to entire session scope unless explicitly changed
```

**CLAUDE.md 업데이트**:

```bash
# Section 8에 Minimal Question Policy 추가
# (AskUserQuestion Constraints 다음에 삽입)
```

내용은 위 Section 2.2 참조.

#### Step 3: Git 커밋

```bash
cd existing-project
git add .claude/rules/abyz-lab/workflow/*.md
git add .claude/rules/abyz-lab/development/*.md
git add .claude/rules/abyz-lab/core/abyz-lab-constitution.md
git add CLAUDE.md

git commit -m "refactor(rules): Apply ABYZ-Lab-ADK optimization directives

Apply context optimization and question frequency reduction policies.

Changes:
- Add paths frontmatter to workflow and development rules
- Add User Interaction Constraints to abyz-lab-constitution.md
- Add Minimal Question Policy to CLAUDE.md Section 8

Expected impact:
- 60% context reduction for typical sessions
- 70-80% reduction in AskUserQuestion frequency

Ref: system-emul-sim optimization (2026-02-16)

🗿 ABYZ-Lab <email@mo.ai.kr>"
```

---

## 검증 체크리스트

### 컨텍스트 최적화 검증

- [ ] 모든 언어별 규칙 파일에 paths frontmatter 존재
- [ ] 워크플로우 규칙 (3개)에 paths frontmatter 존재
- [ ] 개발 규칙 (3개)에 paths frontmatter 존재
- [ ] Paths 패턴이 적절한 파일 타입을 포함
- [ ] 일반 대화 시 불필요한 규칙이 로드되지 않음

### 질문 빈도 최적화 검증

- [ ] abyz-lab-constitution.md에 User Interaction Constraints 섹션 존재
- [ ] CLAUDE.md Section 8에 Minimal Question Policy 존재
- [ ] MEMORY.md에 강화된 즉시 실행 정책 존재
- [ ] 사용자 승인 후 추가 질문 없이 실행됨
- [ ] 진행 확인 질문이 나타나지 않음
- [ ] 기술 결정을 자동으로 수행함

### MCP 통합 검증

- [ ] .mcp.json에 필요한 MCP 서버 설정 존재
- [ ] mcp-integration.md에 사용 패턴 문서화
- [ ] MCP 서버가 정상 작동함

### 언어 설정 검증

- [ ] language.yaml의 conversation_language가 의도한 값
- [ ] 대화 응답이 설정된 언어로 나옴
- [ ] Git 커밋 메시지가 영어로 작성됨
- [ ] 코드 주석이 영어로 작성됨

---

## 재사용 템플릿

### Paths Frontmatter 템플릿

#### 언어별 규칙
```yaml
---
paths:
  - "**/*.{extension}"
  - "**/config-file"
---
```

#### 워크플로우 규칙
```yaml
---
paths:
  - ".abyz-lab/specific-dir/**/*"
  - "**/*pattern*"
---
```

#### 도구/프레임워크 규칙
```yaml
---
paths:
  - "**/*.{relevant-extensions}"
  - "**/framework-specific-files"
---
```

### User Interaction Constraints 템플릿

```markdown
## User Interaction Constraints

Minimize AskUserQuestion usage to avoid interrupting workflow. Ask only when absolutely necessary.

Rules:
- **ONLY ask when**: [시스템 파괴 위험 정의]
- **NEVER ask for**: [금지 항목 나열]
- **After user approval**: Execute immediately without additional questions
- **Use best practices**: Make technical decisions automatically
- **One-time decisions**: Apply to entire session scope
```

### Minimal Question Policy 템플릿

```markdown
### Minimal Question Policy

**CRITICAL**: Minimize AskUserQuestion usage to avoid workflow interruption.

**ONLY ask when**:
- [구체적 위험 상황]

**NEVER ask for**:
- [구체적 금지 항목]

**After user approval**:
- [승인 후 행동 지침]

**Example**:
❌ Bad: [나쁜 예]
✅ Good: [좋은 예]
```

---

## 추가 리소스

### 관련 문서

- **컨텍스트 사용 패턴 추적**: `C:\Users\user\.claude\projects\D--workspace-github-system-emul-sim\memory\context-optimization.md`
- **프로젝트 메모리**: `C:\Users\user\.claude\projects\D--workspace-github-system-emul-sim\memory\MEMORY.md`
- **Git 커밋 이력**:
  - `0197d68` - Codex MCP 추가
  - `34b6e6c` - 컨텍스트 최적화
  - `e144fbc` - 질문 빈도 최적화

### 참고 프로젝트

- **소스**: system-emul-sim (X-ray Detector Panel System)
- **적용 날짜**: 2026-02-16
- **저장소**: D:\workspace-github\system-emul-sim

---

## 버전 이력

| 버전 | 날짜 | 변경사항 |
|------|------|----------|
| 1.0.0 | 2026-02-16 | 초기 버전 생성 (컨텍스트 최적화 + 질문 빈도 최적화) |

---

## 라이선스

이 문서는 ABYZ-Lab-ADK 프로젝트의 일부이며, 동일한 라이선스를 따릅니다.

---

*생성일: 2026-02-16*
*프로젝트: X-ray Detector Panel System*
*담당: ABYZ-Lab Development Team*
