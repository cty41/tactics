---
type: Game System
resource: https://github.com/cty41/tactics/tree/main/Assets/Tactics/Scripts/Common/ai/MonsterAI
title: Monster AI
description: 基于规则门禁、候选评分、决策图和固定执行器的怪物战斗决策系统。
tags: [gameplay, ai, combat, unity]
timestamp: "2026-07-23T02:06:18+08:00"
status: active
catalog_scope: monster-ai
repo_paths:
  - .agents/skills/monster-ai-mcp-workflow/SKILL.md
  - Assets/Tactics/Scripts/Common/ai/MonsterAI
  - Assets/Tactics/Scripts/Editor/MonsterAIEditor
  - Assets/Tactics/AI/BasicMeleeBrain.asset
  - Assets/Tactics/AI/BasicMeleeGraph.asset
  - Assets/Tactics/AI/BasicMeleeProfile.asset
  - Assets/Tactics/AI/FireDemonBrain.asset
  - Assets/Tactics/Tests/PlayMode/AiDecisionComponentTests.cs
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:f88cfce413008e3302d9446dd9c47dac9d96e4d9b022cf606d9bebc2c5c1e5e3
---

# Current State

`AiContextBuilder` 构建战斗快照，`IntentGenerator` 生成“可达站位 × 合法技能目标点”候选，`RuleFilter` 执行硬门禁，`IntentScorer` 评分，`IntentResolver` 稳定选取结果，`IntentExecutor` 执行移动与技能计划。

玩家预览、AI 和执行前重验证共享 `IAbilityTargetingProvider`。`AiBrainAsset` 组合 `AiDecisionGraph`、`AIProfile` 和可选 Pattern；Pattern 游标按单位保存，失败或 Generic fallback 不推进。Brain 可选配置偏好战斗距离和过近时的重定位优先级；关闭时不改变旧 Brain 候选与评分。

单位朝向属于共享战斗状态而不是 AI 私有状态。AI 成功移动后按路径最后一步转向，成功执行目标技能后朝向目标；失败执行恢复原朝向。AI 不在回合末自动面向最近敌人，也不显示独立箭头。方向型技能后续直接读取这一共享朝向与 `SkillTargetingProtocol`。

当前普通/精英怪物仍共用 `BasicMeleeBrain`、`BasicMeleeGraph`、`BasicMeleeProfile`。法师召唤物新增 `FireDemonBrain`，复用 BasicMelee 图与 Profile，但设置 2–3 格偏好距离；火魔与敌人相邻且存在合法站位时优先重定位。`ScoreNode.Parameter` 已序列化但尚未由 `IntentScorer` 消费；除火魔外，后续差异化怪物模式仍未实现。

亚马逊诱饵沿用普通敌人的候选生成与合法性，不建立专用 Brain。若当前生成结果中存在可达诱饵的移动或攻击候选，`IntentGenerator` 会只保留这些诱饵候选；没有可达诱饵候选时继续使用正常敌方目标，避免不可达诱饵令 AI 停摆。

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
