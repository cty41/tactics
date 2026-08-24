---
type: Game System
resource: https://github.com/cty41/tactics/tree/main/src/Tactics.Core/AI
title: Monster AI
description: Godot Pure Run 中基于合法候选、规则门禁、稳定评分和确定性执行的怪物决策系统。
tags: [gameplay, ai, combat, godot]
timestamp: "2026-08-24T16:30:50+08:00"
status: active
catalog_scope: monster-ai
repo_paths:
  - src/Tactics.Core/AI
  - src/Tactics.Application/AI
  - src/Tactics.Core.Tests/AiEncounterRuntimeTests.cs
  - godot/src/Tactics.Godot.Adapter/Runtime/AiEncounterBatchValidator.cs
  - godot/content
  - .agents/docs/maw-bat-enemy-slice-design.md
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:90b3d7eca258c0aa17ac8b9abd488572de94f13fa66289dce8ebbc82eb3502b9
---

# Current State

Monster AI 以 Core 战斗状态构建候选，复用正式移动、技能目标、资源支付和执行合法性；规则门禁先拒绝非法候选，稳定评分与固定 tie-break 再选择行动。Pattern 游标、技能次数、MP、召唤物和附身控制均保存在正式战斗状态中，不建立第二套规则。

Godot Adapter 负责从 typed Resource/Catalog 装载 AI 配置，并通过正式运行时执行决策。配置作者链使用 Application 的 typed authoring contract 与 Tactics Authoring MCP；不得恢复旧编辑器资产或任意序列化字段 patch。

固定 Seed 与 AI-vs-AI 结果只用于诊断候选、回合推进和清理，不作为平衡或玩家体验通过证据。人工体验结论继续由试玩协议和验收账本记录。

大嘴蝠使用 `PredatoryDiver`：在本回合存在合法咬击时，按真实 Transition 是否致死、当前 HP、移动成本、
稳定实例 ID 选择目标；无咬击候选时接近当前 HP 最低目标，低血量不会切入通用撤退。可击杀判断重放同一
确定性移动与技能 Transition，因此包含护盾、最终伤害和暴击状态，而不是只比较技能面板伤害。

# Relationships

- 技能合法性与执行依赖 [SkillGraph](skill-graph.md)。
- 移动、攻击和回合推进依赖 [Battle System](battle.md)。
- 结构化旅程由 [Gameplay Test Framework](gameplay-test-framework.md)验证。

# Verification Guidance

同时核对 Core 候选/评分、Application 作者合同、Godot Resource、运行时执行和固定 Seed 回归；不得仅根据历史计划或冻结来源推断当前行为。
