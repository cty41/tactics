---
type: Game System
resource: https://github.com/cty41/tactics/tree/main/src/Tactics.Core/AI
title: Monster AI
description: Godot Pure Run 中基于合法候选、规则门禁、稳定评分和确定性执行的怪物决策系统。
tags: [gameplay, ai, combat, godot]
timestamp: "2026-08-20T20:45:05+08:00"
status: active
catalog_scope: monster-ai
repo_paths:
  - src/Tactics.Core/AI
  - src/Tactics.Application/AI
  - src/Tactics.Core.Tests/AiEncounterRuntimeTests.cs
  - godot/src/Tactics.Godot.Adapter/Runtime/AiEncounterBatchValidator.cs
  - godot/content
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:cb1b779eac7f2b24ac7a65047eb96e06318dcd373db89861115a9e2013127ab5
---

# Current State

Monster AI 以 Core 战斗状态构建候选，复用正式移动、技能目标、资源支付和执行合法性；规则门禁先拒绝非法候选，稳定评分与固定 tie-break 再选择行动。Pattern 游标、技能次数、MP、召唤物和附身控制均保存在正式战斗状态中，不建立第二套规则。

Godot Adapter 负责从 typed Resource/Catalog 装载 AI 配置，并通过正式运行时执行决策。配置作者链使用 Application 的 typed authoring contract 与 Tactics Authoring MCP；不得恢复旧编辑器资产或任意序列化字段 patch。

固定 Seed 与 AI-vs-AI 结果只用于诊断候选、回合推进和清理，不作为平衡或玩家体验通过证据。人工体验结论继续由试玩协议和验收账本记录。

# Relationships

- 技能合法性与执行依赖 [SkillGraph](skill-graph.md)。
- 移动、攻击和回合推进依赖 [Battle System](battle.md)。
- 结构化旅程由 [Gameplay Test Framework](gameplay-test-framework.md)验证。

# Verification Guidance

同时核对 Core 候选/评分、Application 作者合同、Godot Resource、运行时执行和固定 Seed 回归；不得仅根据历史计划或冻结来源推断当前行为。
