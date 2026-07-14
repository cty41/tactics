---
type: Game System
resource: https://github.com/cty41/tactics/tree/main/Assets/Tactics/Scripts/Common/ai/MonsterAI
title: Monster AI
description: 基于规则门禁、候选评分、决策图和固定执行器的怪物战斗决策系统。
tags: [gameplay, ai, combat, unity]
timestamp: "2026-07-14T00:00:00+08:00"
status: active
catalog_scope: monster-ai
repo_paths:
  - .agents/plans/怪物战斗AI系统统一计划.md
  - .agents/skills/monster-ai-mcp-workflow/SKILL.md
  - Assets/Tactics/Scripts/Common/ai/MonsterAI
  - Assets/Tactics/Scripts/Editor/MonsterAIEditor
  - Assets/Tactics/AI/BasicMeleeGraph.asset
  - Assets/Tactics/Tests/PlayMode/AiDecisionComponentTests.cs
verified_revision: d5f1730d3527
---

# Current State

怪物 AI 运行时由 `AiContextBuilder` 构建快照，`IntentGenerator` 生成候选，`RuleFilter` 执行硬门禁，`IntentScorer` 评分，`IntentResolver` 选择结果，最后由 `IntentExecutor` 复用移动、攻击和技能执行链。

`AiBrainAsset` 组合 `AiDecisionGraph` 与 `AIProfile`。项目包含决策图编辑器、基础近战图资产和 MCP 资产生成工作流。

# Relationships

- AI 候选中的技能执行依赖[SkillGraph](skill-graph.md)及其能力元数据。
- AI 的移动、攻击和回合推进发生在[Battle System](battle.md)中。
- AI 资产创建和修改必须遵循项目的[Unity Agent Workflow](../operations/unity-agent-workflow.md)。

# Known Boundary

统一计划同时描述已落地能力和剩余工作。回答“当前是否支持”时不能仅看计划清单，必须核对对应类型、资产和测试。

# Citations

[1] [Monster AI unified plan](https://github.com/cty41/tactics/blob/d5f1730d35278e1811cac744a9e1b242eece27e8/.agents/plans/%E6%80%AA%E7%89%A9%E6%88%98%E6%96%97AI%E7%B3%BB%E7%BB%9F%E7%BB%9F%E4%B8%80%E8%AE%A1%E5%88%92.md)
[2] [Monster AI runtime](https://github.com/cty41/tactics/tree/d5f1730d35278e1811cac744a9e1b242eece27e8/Assets/Tactics/Scripts/Common/ai/MonsterAI)
