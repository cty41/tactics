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

**首先**读取项目中的唯一 MCP URL 来源：

```bash
cat .agents/mcp.json
powershell.exe -File Tools/unity-mcp/Sync-ProjectMcpConfig.ps1 --check
```

**不要**假设默认端口！配置文件中的端口可能不同。

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
| 项目唯一真相源 | `.agents/mcp.json` |
| Codex 派生配置 | `.codex/config.toml` |
| OpenCode 派生配置 | `.opencode/opencode.json` |
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

## Checklist

- [ ] 读取了配置文件中的端口设置
- [ ] 验证了端口是否在监听
- [ ] 确认了 Unity Editor 状态
- [ ] 使用了正确的端口进行连接
- [ ] 如果仍然失败，报告了具体错误信息
- [ ] 已核验 `project/info` 的 `projectRoot` 是当前 worktree
