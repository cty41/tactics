---
type: Game System
resource: https://github.com/cty41/tactics/tree/main/Assets/Tactics/Scripts/Common/ai/MonsterAI
title: Monster AI
description: 基于规则门禁、候选评分、决策图和固定执行器的怪物战斗决策系统。
tags: [gameplay, ai, combat, unity]
timestamp: "2026-07-15T12:00:00+08:00"
status: active
catalog_scope: monster-ai
repo_paths:
  - .agents/skills/monster-ai-mcp-workflow/SKILL.md
  - Assets/Tactics/Scripts/Common/ai/MonsterAI
  - Assets/Tactics/Scripts/Editor/MonsterAIEditor
  - Assets/Tactics/AI/BasicMeleeBrain.asset
  - Assets/Tactics/AI/BasicMeleeGraph.asset
  - Assets/Tactics/AI/BasicMeleeProfile.asset
  - Assets/Tactics/Tests/PlayMode/AiDecisionComponentTests.cs
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:0000000000000000000000000000000000000000000000000000000000000000
---

# Current State

`AiContextBuilder` 构建战斗快照，`IntentGenerator` 生成“可达站位 × 合法技能目标点”候选，`RuleFilter` 执行硬门禁，`IntentScorer` 评分，`IntentResolver` 稳定选取结果，`IntentExecutor` 执行移动与技能计划。

玩家预览、AI 和执行前重验证共享 `IAbilityTargetingProvider`。`AiBrainAsset` 组合 `AiDecisionGraph`、`AIProfile` 和可选 Pattern；Pattern 游标按单位保存，失败或 Generic fallback 不推进。

当前项目只有 `BasicMeleeBrain`、`BasicMeleeGraph`、`BasicMeleeProfile` 三个实际 AI 资产，所有首批普通/精英怪物仍共用这一组资产。`ScoreNode.Parameter` 已序列化但尚未由 `IntentScorer` 消费；这些事实是后续差异化工作的边界，不代表当前已有分怪物模式。

# Relationships

- 技能候选与执行依赖[SkillGraph](skill-graph.md)。
- 结构化 AI 结果可由[Gameplay Test Framework](gameplay-test-framework.md)验证。
- 移动、攻击和回合推进发生在[Battle System](battle.md)。
- 敌人设计可参考[Mewgenics Analysis](../references/mewgenics-analysis.md)，外部机制不视为已实现。
- 具体未实施项见[Project Known Gaps](../plans/project-known-gaps.md)。

# Verification Guidance

判断支持面时同时检查 runtime 类型、实际 Brain/Graph/Profile 资产和 PlayMode 测试；不得仅根据历史计划推断行为。

# Citations

[1] [Monster AI runtime](https://github.com/cty41/tactics/tree/main/Assets/Tactics/Scripts/Common/ai/MonsterAI)
[2] [AI assets](https://github.com/cty41/tactics/tree/main/Assets/Tactics/AI)
