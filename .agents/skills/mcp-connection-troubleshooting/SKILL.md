---
name: mcp-connection-troubleshooting
description: "Use when MCP tool calls fail — troubleshoot connection issues by checking config files and server status"
---

# MCP Connection Troubleshooting

MCP 连接故障排除的专用技能。

## Quick Reference

| 步骤 | 操作 | 说明 |
|------|------|------|
| 1 | 读取项目 JSON | 检查唯一端口和 URL 设置 |
| 2 | 验证端口 | 确认端口是否在监听 |
| 3 | 检查服务状态 | 确认 Unity Editor 和 MCP 插件 |
| 4 | 使用正确端口 | 用配置文件中的端口重试 |

## When to use

- MCP 工具调用返回 "Session not found"
- MCP 工具调用返回 "Connection refused"
- MCP 工具调用返回 "Connection timeout"
- MCP 工具调用超时
- 不确定 MCP 服务器的端口配置
- 需要验证 MCP 服务器是否正常运行

## Workflow

### Step 1: 读取项目 JSON

**首先**读取当前 worktree 的本地 MCP URL 来源：

```bash
cat .agents/mcp.json
powershell.exe -File Tools/unity-mcp/Sync-ProjectMcpConfig.ps1 --check
```

若文件不存在，首次运行：

```powershell
powershell.exe -File Tools/unity-mcp/Initialize-ProjectMcpConfig.ps1 -Url http://127.0.0.1:<端口>/mcp
```

若 `.agents/mcp.local.json` 存在，它是迁移时保留且 Restore 后仍会存在的端点备份。`mcp.json` 缺失时优先从该备份恢复，避免重新选择错误端口：

```powershell
powershell.exe -File Tools/unity-mcp/Initialize-ProjectMcpConfig.ps1 -RestoreMigration
```

**不要**假设默认端口！配置文件中的端口可能不同。

### Domain reload 后反复断线

若 MCP 手动启动后可用，但编译、进出 Play Mode 或资源重导入后再次出现 `Session not found`，检查 Console 是否包含：

```text
[UnityMCP] Project bootstrap skipped: Could not read <worktree>/.agents/mcp.json
```

项目会关闭 MCPForUnity 包级共享 auto-start，由 `UnityMcpProjectBootstrap` 独占每个 worktree 的桥启动。缺少 `mcp.json` 时，旧版本 bootstrap 会在重启桥之前返回，因此每次 domain reload 都会断线。当前 bootstrap 可临时回退到 `mcp.local.json` 并输出 Warning，但仍应立即运行 `-RestoreMigration` 恢复正式配置；若两个文件都不存在，则用显式 `-Url` 完整初始化。

### Step 2: 提取端口信息

从配置文件中找到 MCP 服务器的 URL：

```json
{
  "mcpServers": {
    "unityMCP": {
      "url": "http://127.0.0.1:8081/mcp"
    }
  }
}
```

提取端口号（如 8081）。

### Step 3: 验证端口是否在监听

```bash
# Windows (PowerShell)
netstat -ano | findstr "LISTENING" | findstr "<端口号>"

# Windows (CMD)
netstat -ano | findstr ":<端口号>"

# Linux/Mac
lsof -i :<端口号>
```

### Step 4: 检查 Unity 状态

确认：
1. Unity Editor 是否已启动
2. MCP 插件是否已启用
3. MCP 服务器是否在运行

### Step 5: 使用正确端口重试

使用配置文件中的端口进行连接测试。

### Step 6: 校验项目根目录

首次 MCP 调用必须读取 `mcpforunity://project/info`。返回的 `projectRoot` 不是当前 worktree 时，不得继续任何 Unity 写操作；应关闭占用端口的错误 Editor 或启动目标 worktree 的 Editor。

## 常见配置文件位置

| 工具 | 配置文件路径 |
|------|-------------|
| worktree 本地真相源（忽略） | `.agents/mcp.json` |
| Codex 本地派生配置（忽略） | `.codex/config.toml` |
| OpenCode 本地派生配置（忽略） | `.opencode/opencode.json` |
| Git 跟踪模板 | `*.template.*` |
| Claude Code | `.claude/claude_code_config.json` |
| Cursor | `.cursor/mcp.json` |
| VS Code | `.vscode/mcp.json` |

## 常见端口

| 工具/框架 | 常见默认端口 |
|-----------|-------------|
| Unity MCP | 由 `.agents/mcp.json` 指定 |
| Node.js | 3000 |
| Python | 5000, 8000 |
| React | 3000 |
| Vue | 8080 |

**注意**：这些只是常见默认值，实际配置可能不同。**始终以配置文件为准**。

## Anti-patterns

| ❌ 错误 | ✅ 正确 | 原因 |
|---------|---------|------|
| 假设端口 3000 | 读取配置文件确认端口 | 配置可能不同 |
| 重复尝试相同错误端口 | 改变策略，检查配置 | 避免无效重复 |
| 不检查 Unity 状态 | 确认 Unity Editor 已启动 | MCP 需要 Unity 运行 |
| 忽略配置文件 | 首先读取配置 | 配置是唯一真相源 |
| 不验证端口状态 | 使用 netstat 验证端口 | 确认服务是否在运行 |
| 未检查项目根目录就写操作 | 先读取 `project/info` | 防止写入错误 worktree |
| 手动反复启动 MCP server | 恢复或初始化 `.agents/mcp.json` | 手动启动不能跨 domain reload，未修复唯一自动重启路径 |

## Checklist

- [ ] 读取了配置文件中的端口设置
- [ ] 验证了端口是否在监听
- [ ] 确认了 Unity Editor 状态
- [ ] 使用了正确的端口进行连接
- [ ] 如果仍然失败，报告了具体错误信息
- [ ] 已核验 `project/info` 的 `projectRoot` 是当前 worktree
