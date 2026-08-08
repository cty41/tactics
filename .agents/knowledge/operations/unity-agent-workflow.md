---
type: Operational Playbook
resource: https://github.com/cty41/tactics/blob/main/AGENTS.md
title: Unity Agent Workflow
description: Agent修改代码、Unity资产、UI、文档和提交时的项目级安全工作流。
tags: [operations, unity, agents, validation]
timestamp: "2026-08-08T12:09:07+08:00"
status: active
catalog_scope: unity-agent-workflow
repo_paths:
  - AGENTS.md
  - .gitignore
  - .mimocode/mimocode.template.json
  - .agents/rules
  - .agents/skills/unity-mcp-core/SKILL.md
  - .agents/skills/unity-mcp-advanced/SKILL.md
  - .agents/skills/mcp-connection-troubleshooting/SKILL.md
  - .agents/skills/unity-auto-compile-guard/SKILL.md
  - .agents/skills/project-doc-organization/SKILL.md
  - Tools/agent-policy
  - Assets/Tactics/Scripts/Editor/MCP
  - Assets/Tactics/Tests/Editor/UnityMcpProjectBootstrapTests.cs
  - Assets/Tactics/Tests/Editor/Tactics.Tests.Editor.asmdef
  - Packages/manifest.json
  - Packages/packages-lock.json
  - Assets/Tactics/Scripts/Editor/Tactics.Editor.asmdef
  - Assets/Tactics/Scripts/Common/Editor/Tactics.Editor.asmref
  - Assets/Tactics/Scripts/Common/RuleTiles/Editor/Tactics.Editor.asmref
  - Assets/Tactics/Scripts/Common/Units/abilities/Editor/Tactics.Editor.asmref
  - Tools/unity-mcp
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:6d295eaf4833a52821e55453d0b080ba6742e66959f55b8c5a267c99094bfd29
---

# Core Rules

- 不使用 `Resources.Load`，运行时资产通过 `GameAssetManager` 管理。
- 不直接调用 `Debug.Log`，使用 `TLog` 或 `TBattleLog`。
- 不直接读写 Unity YAML，资产操作通过 Unity MCP 或项目认可工具完成。
- 修改 C# 后必须显式触发 Unity 编译并检查 Console 错误。
- Agent 默认复用用户当前指定的 worktree；除非活跃计划或用户明确要求，不创建、删除或切换 worktree。Unity 项目导入和启动成本高，不能以“隔离方便”“并行”或“改动较大”为理由自动打开第二份项目；冻结 worktree 只有在用户明确授权修复时才可临时写入。完整约束见 `.agents/rules/agent-worktree.md`。
- 所有桌面前台交互默认禁止；Computer Use、窗口激活、真实鼠标键盘和快捷键只有在当前任务明确要求、Agent 说明焦点影响且用户对本次动作确认后才允许。普通实现、截图、视觉 QA、编译、测试、构建和连接恢复不构成前台控制授权；后台无法提供证据时标记 `manual_visual_qa_pending`。完整定义见[前台交互与焦点保护规则](https://github.com/cty41/tactics/blob/main/.agents/rules/foreground-interaction.md)。
- Unity 编译、测试、构建和截图必须通过 MCP 调用，交互验证优先使用 PlayMode 自动测试与 Input System 虚拟设备。MCP bridge 不可用时只做端点、进程和日志等只读诊断，不能以干扰前台工作的方式绕过连接问题。
- 新增、删除或移动 Unity 文件时保持 `.meta` 配对。
- Unity MCP 的端口从 worktree 本地且忽略的 `.agents/mcp.json` 读取；Initialize/Prepare/Restore/Sync 共用 tracked `FileShare.None` lock anchor。同步在首个写操作前以 Windows PowerShell 5.1 strict UTF-8/JSON 和有限 TOML allowlist 校验 source、模板及受管字段，`--check` 不创建、清理或改写文件；caught write/delete failure 恢复原字节与存在性。OpenCode/MiMoCode 的无关本地字段与 JSON number lexeme 保留，one-shot migration backup 同时保存完整本地 JSON；tracked 模板不含凭据，MiMoCode timeout 固定为 300000 毫秒。首次操作先执行同步检查并核验当前 worktree。`UnityMcpProjectBootstrap` 当前只做 batch/import-worker guard，普通 Editor 路径显式 no-op；项目层不读配置、不写 package preference/endpoint、不注册 callback，也不 start/stop/connect/verify/retry。MCP lifecycle 由 package 或用户显式操作负责。
- 2026-08-07 状态仍为 `0/5`、`blocked_upstream`：项目 no-op 只消除了 manual Disconnect 被覆盖和双 owner 等项目侧副作用，MCPForUnity 10.1.0 的 reconnect continuation/session eviction 以及 10.1.2 未修复的 receive-loop/tool-discovery 路径仍在。禁止以冷启动自动连接、短时工具注册或 guard 定向测试替代 reload 稳定性证据；等待通过源码门的上游 stable 后再从 0 重启 5/5。
- `Manage-UnityTestGate.ps1` 当前仅是本地 draft helper，不是 CI、发布或审计事实源；开发期按单 Editor、单执行者、单 job 串行运行。取得 job ID 后只查询原 job；启动调用未返回 ID或状态不明时停止，禁止无证据重复启动。final v3 的 canonical commitment、attempt-local result、全根 snapshot/fail-closed、migration/replay/concurrency 仍为延期缺口。
- 10.1.2 与 10.1.0 的 HTTP reload handler、WebSocket registered 路径和 tool discovery 源码相同，未满足本项目升级源码门；当前保持 10.1.0，并把 receive-loop 与 focus-nudge 问题登记为上游缺口，禁止 fork 或编辑 `Library/PackageCache`。
- 文档查询先读 OKF index；当前实现仍回到代码、资产和测试。
- Presentation Graph 的 Agent 创作使用专用 list/get/validate/apply/preview MCP 工具。get 一次返回 Graph 与全部可编辑叶资产的规范化 SHA-256 revision、GUID/路径及引用者；写操作先在隐藏副本完成 Graph/叶资产/绑定的 typed ChangeSet 与校验，再以单一 Undo group 和一次 SaveAssets 原子写回。创建 Graph 使用与 expectedRevision 互斥的 createGraph，Recipe 只接受 replaceRecipeBindings 整表操作；禁止任意 SerializedProperty 路径和用户资产删除。preview 明确选择 Full Scenario、Phase、Entry、Leaf 或 Fork Region，并返回固定 seed 的 PNG、实际轨道时间线、诊断和 fallback。
- Editor GPU 崩溃修复必须区分结构测试、Null/离屏渲染与真实图形设备压力验收。Presentation Workbench 的 RenderController 自动测试可证明限频、resize 暂停和资源释放，但 Intel Arc/D3D11 下连续拖动外部窗口与分栏仍须人工完成；通过编译、离屏 PNG 或 Null device 测试不得替代该门禁。

# Related Systems

这些规则适用于[SkillGraph](../systems/skill-graph.md)、[Monster AI](../systems/monster-ai.md)、[Battle System](../systems/battle.md)和[Roguelike Run](../systems/roguelike-run.md)。

# Knowledge Operations

知识查询、ingest、supersede 和 lint 遵循 [OKF v0.1](../references/okf-v0.1.md) 与项目的 `knowledge-maintenance` skill。普通查询默认只读。

修改 `catalog-scopes.yaml` 监控范围内的实现或文档后，继续执行 [OKF Maintenance](okf-maintenance.md) 的影响检测与 scope 同步；这一步发生在提交准备之前，不依赖 pre-commit 或 CI。

设计、活跃计划和完成后清理遵循 [Project Documentation](project-documentation.md)。

# Verification

```powershell
python Tools/agent-policy/validate_foreground_interaction_policy.py
python -m unittest discover Tools/agent-policy -p "test_*.py"
python -m unittest Tools.unity-mcp.test_sync_project_mcp_config
powershell.exe -File Tools/unity-mcp/Sync-ProjectMcpConfig.ps1 --check
```

静态校验负责防止根红线、权威规则、关键 skill 引用和 catalog 映射漂移；它不能在工具层拦截一次违规调用。若需要真正的强制阻断，仍须由产品级工具禁用或逐次确认策略提供。

# Citations

[1] [Tactics AGENTS.md](https://github.com/cty41/tactics/blob/d5f1730d35278e1811cac744a9e1b242eece27e8/AGENTS.md)
