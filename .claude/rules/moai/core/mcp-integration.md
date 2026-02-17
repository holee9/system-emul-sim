---
paths:
  - "**/.mcp.json"
---

# MCP Integration

Model Context Protocol (MCP) server integration rules.

## Available MCP Servers

Standard MCP servers in ABYZ-Lab-ADK:

- context7: Library documentation lookup
- sequential-thinking: Complex problem analysis
- codex: OpenAI Codex integration for task delegation
- pencil: .pen file design editing. Used by expert-frontend (sub-agent mode) and team-designer (team mode) for .pen file design editing.
- claude-in-chrome: Browser automation

## Tool Loading

MCP tools are deferred and must be loaded before use:

1. Use ToolSearch to find and load the tool
2. Then call the loaded tool directly

Example flow:
- ToolSearch("context7 docs") → loads mcp__context7__* tools
- mcp__context7__resolve-library-id → now available

## Rules

- Always use ToolSearch before calling MCP tools
- Prefer MCP tools over manual alternatives
- Authenticated URLs require specialized MCP tools

## Configuration

MCP servers are defined in `.mcp.json`.

### Configuration Hierarchy

Claude Code uses a two-level configuration system:

1. **Global**: `~/.mcp.json` - User-wide MCP server definitions
2. **Project**: `<project-root>/.mcp.json` - Project-specific overrides

**Important**: When a project-specific `.mcp.json` exists, it completely replaces the global configuration. To use both global and project-specific servers, you must merge the configurations.

### Example Configuration

```json
{
  "$schema": "https://raw.githubusercontent.com/anthropics/claude-code/main/.mcp.schema.json",
  "mcpServers": {
    "context7": {
      "$comment": "Up-to-date documentation and code examples via Context7",
      "command": "cmd.exe",
      "args": ["/c", "npx -y @upstash/context7-mcp@latest"]
    },
    "sequential-thinking": {
      "$comment": "Step-by-step reasoning for complex problems",
      "command": "cmd.exe",
      "args": ["/c", "npx -y @modelcontextprotocol/server-sequential-thinking"]
    },
    "codex": {
      "$comment": "OpenAI Codex - Claude Code integration for task delegation",
      "command": "C:\\Users\\user\\.vscode\\extensions\\openai.chatgpt-0.4.74-win32-x64\\bin\\windows-x86_64\\codex.exe",
      "args": ["mcp-server"]
    },
    "pencil": {
      "$comment": "UI/UX design editing for .pen files",
      "command": "c:\\Users\\user\\.vscode\\extensions\\highagency.pencildev-0.6.24\\out\\mcp-server-windows-x64.exe",
      "args": ["--app", "visual_studio_code"],
      "type": "stdio"
    }
  },
  "staggeredStartup": {
    "enabled": true,
    "delayMs": 500,
    "connectionTimeout": 60000
  }
}
```

## Context7 Usage

For up-to-date library documentation:

1. resolve-library-id: Find library identifier
2. get-library-docs: Retrieve documentation

## Sequential Thinking Usage

For complex analysis requiring step-by-step reasoning:

- Breaking down multi-step problems
- Architecture decisions
- Technology trade-off analysis

Activate with `--ultrathink` flag for enhanced analysis.

## Codex Usage

For OpenAI Codex integration and task delegation:

- Experimental feature for extended task coordination
- Requires OpenAI ChatGPT VSCode extension installed
- Used for specialized task delegation beyond standard agent capabilities

## Troubleshooting

### MCP Server Not Connected

**Symptom**: Expected MCP server is not available (e.g., codex, pencil not showing up)

**Diagnosis**:
1. Check if project-specific `.mcp.json` exists
2. Compare with global `~/.mcp.json` configuration
3. Verify project `.mcp.json` includes all required servers

**Solution**:
- Option A: Merge global server definitions into project `.mcp.json`
- Option B: Remove project `.mcp.json` to use global configuration only
- Option C: Keep separate configs but ensure all needed servers are in project config

**Prevention**: When creating project-specific `.mcp.json`, always include all required MCP servers from the global configuration.

### Connection Timeout

**Symptom**: MCP server startup takes too long or times out

**Solution**:
- Increase `connectionTimeout` in `staggeredStartup` section
- Recommended value: 60000ms (60 seconds) for slower systems
- Default value: 15000ms (15 seconds)

## ABYZ-Lab Integration

- Skill("abyz-lab-workflow-thinking") for Sequential Thinking patterns
- Skill("abyz-lab-foundation-claude") for MCP configuration
