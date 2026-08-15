---
type: Planning Backlog
resource: https://github.com/cty41/tactics/blob/main/.agents/docs/project-known-gaps.md
title: Project Known Gaps
description: 已从当前实现确认但尚未获批为活跃开发计划的集中缺口目录。
tags: [planning, backlog, gaps]
timestamp: "2026-08-15T15:06:10+08:00"
status: active
catalog_scope: project-known-gaps
repo_paths:
  - .agents/docs/project-known-gaps.md
  - Assets/Tactics/Scripts/Editor/MCP/UnityMcpProjectBootstrap.cs
  - Assets/Tactics/Tests/Editor/UnityMcpProjectBootstrapTests.cs
  - Packages/manifest.json
  - Packages/packages-lock.json
  - Tools/unity-mcp/README.md
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:d01497f536769855d95d8cea60ae6c438d7b38a3cf3e88154034bb2c4a5fcb92
---

# Current State

权威缺口清单按 `verified-gap`、`needs-decision`、`deferred` 和 `idea` 区分确定性。当前覆盖 Monster AI、事件编辑器、Gameplay Test、SkillGraph、Unity MCP 可靠性、Pure Run 内容扩展、战斗反馈和配置硬编码。

该清单不是活跃开发计划。某项只有在满足文档中的激活条件、范围获得确认并建立可验收计划后才进入执行。

Unity MCP 自动恢复当前明确为 `0/5`、`blocked_upstream`：项目 bootstrap 已安全收缩为 batch/import-worker guard 后 no-op，不再覆盖 manual Disconnect，也不再写 endpoint/preference 或持有 start/stop/connect/verify/retry owner；MCPForUnity 10.1.0 包内部 reconnect continuation/session eviction 与 10.1.2 未通过的源码门仍是上游缺口。Test Gate final v3、Context7 旧凭据撤销和连续 5 次 reload 均为 deferred；仅在各自激活条件满足后建立独立计划。

# Relationships

- 文档生命周期由[Project Documentation](../operations/project-documentation.md)管理。
- 缺口分别关联 [Battle System](../systems/battle.md)、[Monster AI](../systems/monster-ai.md)、[Roguelike Run](../systems/roguelike-run.md)、[SkillGraph](../systems/skill-graph.md)和[Gameplay Test Framework](../systems/gameplay-test-framework.md)。

# Verification Guidance

新增条目前必须给出当前代码、资产或测试证据；完成或失效条目应从权威清单移除，并同步相关系统页，而不是保留模糊 TODO。
