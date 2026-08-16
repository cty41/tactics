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
| 5 | 保持后台操作 | 所有 Unity 操作和视觉 QA 均遵循前台交互规则，不抢占 Editor 窗口焦点 |

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

若 `.agents/mcp.local.json` 存在，它是迁移前创建的一次性备份，包含目标端点与当时完整的 OpenCode/MiMoCode 本地 JSON。`mcp.json` 缺失时从该备份恢复，避免重新选择错误端口或丢失个人字段：

```powershell
powershell.exe -File Tools/unity-mcp/Initialize-ProjectMcpConfig.ps1 -RestoreMigration
```

成功 Restore 会在同一事务最后删除该备份；失败则保留备份和原文件字节。Restore 成功后不要再把 `.agents/mcp.local.json` 当作长期 fallback。

**不要**假设默认端口！配置文件中的端口可能不同。

### Domain reload 后反复断线

若 MCP 手动启动后可用，但编译、进出 Play Mode 或资源重导入后再次出现 `Session not found`，不要等待项目 bootstrap 自动恢复。`UnityMcpProjectBootstrap` 现在只做 batch/import-worker 进程 guard，普通 Editor 路径显式 no-op；它不读取配置、不写 `EditorPrefs`/`SessionState`、不注册 callback，也不启动、停止、连接、验证或重试 server/bridge。

这意味着项目代码不再覆盖 manual Disconnect，也不再与 package reload handler 竞争 lifecycle ownership；但 MCPForUnity 10.1.x 自身仍可能按机器级偏好和 package 实现重连，项目不保证 domain reload 后恢复，也不承诺并行 Editor/worktree 隔离。

断线时：

1. 保留已有 test job ID，不重复启动未知 job。
2. 重新读取 `.agents/mcp.json`，运行配置 `--check`，验证端口和 Unity 进程。
3. MCP 仍可调用时读取 `instances`/`project/info`；若 bridge 不可用则停止自动化，请用户手动恢复。
4. 用户恢复后重新核对 `projectRoot`，再查询原 job 或继续任务。

不要通过反复点击 Connect、窗口焦点自动化或启动第二个 server 掩盖连接问题。自动恢复正式验收仍是延期项：当前状态 `0/5`、`blocked_upstream`；只有新的上游 stable 通过源码门后才从 0 重启连续五次 reload。MiMoCode 项目配置的 `timeout: 300000`、`run_tests.init_timeout=120000` 和 `get_test_job.wait_timeout=30` 只控制各自等待预算，不能修复 bridge 或 receive loop。

`Manage-UnityTestGate.ps1` 当前只是未正式发布的本地 draft helper，不是运行 Unity 测试的强制前置，也不是 CI/发布事实源。开发期保持单 Editor、单执行者、单 test job；直接或通过 helper 取得 job ID 后都只查询原 job。`run_tests` 在返回 ID 前超时时停止并确认状态，禁止盲目重启。

截至 2026-08-07，项目 bootstrap 已收缩为 guard 后 no-op；项目层 lifecycle 干预已移除，但 MCPForUnity 10.1.0 的 reconnect continuation/session eviction 和 10.1.2 未修复的 receive-loop/tool-discovery 路径仍在。当前仍是 `0/5`、`blocked_upstream`，不得把本地冷启动或一次定向测试成功写成自动恢复通过。

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

### Step 7: 保持 Editor 后台运行

Unity 编译、测试、构建、截图、视觉 QA 和连接恢复必须遵循[前台交互与焦点保护规则](../../rules/foreground-interaction.md)，优先使用 MCP 工具与只读进程/端口诊断。不得通过窗口自动化激活 Unity、切换焦点、点击真实 Game View 或发送快捷键，以免干扰用户前台工作。

若 MCP bridge 断线且无法通过 MCP 自身恢复：

1. 保留当前测试 job id 和错误证据。
2. 通过配置、端口、进程和日志做只读诊断。
3. 停止自动化并请用户在方便时手动刷新或重启 bridge。
4. 用户确认恢复后，从 `mcpforunity://instances` 和 `mcpforunity://project/info` 重新校验，再继续原 job 或重跑测试。

需要点击技能、切换方向或进入特定 Game View 状态才能补齐视觉证据时，也不能把连接故障当作使用 Computer Use 的理由。没有后台测试或虚拟输入路径时，停止并标记 `manual_visual_qa_pending`。

## 常见配置文件位置

| 工具 | 配置文件路径 |
|------|-------------|
| worktree 本地真相源（忽略） | `.agents/mcp.json` |
| Codex 本地派生配置（忽略） | `.codex/config.toml` |
| OpenCode 本地派生配置（忽略） | `.opencode/opencode.json` |
| MiMoCode 本地派生配置（忽略） | `.mimocode/mimocode.json` |
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
| 激活 Unity 窗口并发送 Refresh/测试快捷键 | 使用 `refresh_unity`、`run_tests`、`get_test_job`、`manage_build` | 避免抢占用户前台焦点；断线时应停下请用户手动恢复 |
| MCP 截图缺少代表状态后点击真实 Game View 补图 | 使用自动测试或虚拟输入；否则记录 `manual_visual_qa_pending` | 视觉完整性不构成前台控制授权 |
| 同时注册多个 reload 重连入口 | 每个新 Domain 只调度一次重连 | 多个 `Bridge.StartAsync` 会形成互相抢占的 WebSocket 重连循环 |

## Checklist

- [ ] 读取了配置文件中的端口设置
- [ ] 验证了端口是否在监听
- [ ] 确认了 Unity Editor 状态
- [ ] 使用了正确的端口进行连接
- [ ] 如果仍然失败，报告了具体错误信息
- [ ] 已核验 `project/info` 的 `projectRoot` 是当前 worktree
- [ ] 未通过窗口自动化抢占 Unity Editor 焦点或触发编译、测试、构建、截图或视觉 QA
- [ ] Domain Reload 后只通过 MCP 轮询确认 Session 自动恢复，未控制 Unity 窗口焦点
- [ ] 后台无法构造视觉状态时已标记 `manual_visual_qa_pending`，没有自动改用 Computer Use
