---
type: Operational Playbook
resource: https://github.com/cty41/tactics/blob/main/AGENTS.md
title: Unity Agent Workflow
description: Agent修改代码、Unity资产、UI、文档和提交时的项目级安全工作流。
tags: [operations, unity, agents, validation]
timestamp: "2026-07-15T13:51:33+08:00"
status: active
catalog_scope: unity-agent-workflow
repo_paths:
  - AGENTS.md
  - .agents/rules
  - .agents/skills/unity-mcp-core/SKILL.md
  - .agents/skills/unity-auto-compile-guard/SKILL.md
  - .agents/skills/project-doc-organization/SKILL.md
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:6617411637c9911034d298186c5d4f0c70ea6e65ea82f1cfe9692ed512d5d3c4
---

# Core Rules

- 不使用 `Resources.Load`，运行时资产通过 `GameAssetManager` 管理。
- 不直接调用 `Debug.Log`，使用 `TLog` 或 `TBattleLog`。
- 不直接读写 Unity YAML，资产操作通过 Unity MCP 或项目认可工具完成。
- 修改 C# 后必须显式触发 Unity 编译并检查 Console 错误。
- 新增、删除或移动 Unity 文件时保持 `.meta` 配对。
- 文档查询先读 OKF index；当前实现仍回到代码、资产和测试。

# Related Systems

这些规则适用于[SkillGraph](../systems/skill-graph.md)、[Monster AI](../systems/monster-ai.md)、[Battle System](../systems/battle.md)和[Roguelike Run](../systems/roguelike-run.md)。

# Knowledge Operations

知识查询、ingest、supersede 和 lint 遵循 [OKF v0.1](../references/okf-v0.1.md) 与项目的 `knowledge-maintenance` skill。普通查询默认只读。

修改 `catalog-scopes.yaml` 监控范围内的实现或文档后，继续执行 [OKF Maintenance](okf-maintenance.md) 的影响检测与 scope 同步；这一步发生在提交准备之前，不依赖 pre-commit 或 CI。

设计、活跃计划和完成后清理遵循 [Project Documentation](project-documentation.md)。

# Citations

[1] [Tactics AGENTS.md](https://github.com/cty41/tactics/blob/d5f1730d35278e1811cac744a9e1b242eece27e8/AGENTS.md)
