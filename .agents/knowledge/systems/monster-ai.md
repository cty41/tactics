---
type: Game System
resource: https://github.com/cty41/tactics/tree/main/Assets/Tactics/Scripts/Common/ai/MonsterAI
title: Monster AI
description: 基于规则门禁、候选评分、决策图和固定执行器的怪物战斗决策系统。
tags: [gameplay, ai, combat, unity]
timestamp: "2026-07-15T00:07:01+08:00"
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
source_fingerprint: sha256:c4c7f7adc1fa8276fd7db81be3db016ba55f2f540895be24963a0ae46a508529
---

# Current State

怪物 AI 运行时由 `AiContextBuilder` 构建快照，`IntentGenerator` 生成“可达站位 × 合法技能目标点”候选，`RuleFilter` 执行硬门禁，`IntentScorer` 评分，`IntentResolver` 稳定选择结果，最后由 `IntentExecutor` 执行移动与技能计划。

技能合法性通过共享 `IAbilityTargetingProvider` 供玩家预览、AI 和执行前重验证共同使用；AOE 的目标集合只用于评分，技能图按目标点执行一次。`AiTurnResult` 暴露结构化计划与执行结果。`AiBrainAsset` 组合 `AiDecisionGraph`、`AIProfile` 和可选固定 Pattern；Pattern 游标由 `AiPatternRuntime` 按单位保存，失败或 Generic fallback 不推进。

首版没有运行时 `threatValue`。遭遇压力由四类怪物、显式配方、布局和倍率表达。
旧 Brain 没有显式 `AbilityUse` 节点时，运行时仍会为遭遇定义注入的技能生成共享合法性候选，避免所有原型退化成基础近战。

# Relationships

- AI 候选中的技能执行依赖[SkillGraph](skill-graph.md)及其共享合法性结果。
- 结构化 AI 结果由[Gameplay Test Framework](gameplay-test-framework.md)消费。
- AI 的移动、攻击和回合推进发生在[Battle System](battle.md)中。
- AI 资产创建和修改必须遵循项目的[Unity Agent Workflow](../operations/unity-agent-workflow.md)。
- 敌人节奏、移动评分、模式序列和形态切换可参考[Mewgenics Analysis](../references/mewgenics-analysis.md)，但外部字段不代表项目已实现能力。

# Known Boundary

统一计划同时描述已落地能力和剩余工作。回答“当前是否支持”时不能仅看计划清单，必须核对对应类型、资产和测试。

# Citations

[1] [Monster AI unified plan](https://github.com/cty41/tactics/blob/d5f1730d35278e1811cac744a9e1b242eece27e8/.agents/plans/%E6%80%AA%E7%89%A9%E6%88%98%E6%96%97AI%E7%B3%BB%E7%BB%9F%E7%BB%9F%E4%B8%80%E8%AE%A1%E5%88%92.md)
[2] [Monster AI runtime](https://github.com/cty41/tactics/tree/d5f1730d35278e1811cac744a9e1b242eece27e8/Assets/Tactics/Scripts/Common/ai/MonsterAI)
