---
type: Game System
resource: https://github.com/cty41/tactics/tree/main/Assets/Tactics/Scripts/Common/ai/MonsterAI
title: Monster AI
description: 基于规则门禁、候选评分、决策图和固定执行器的怪物战斗决策系统。
tags: [gameplay, ai, combat, unity]
timestamp: "2026-07-27T12:55:46+08:00"
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
  - Assets/Tactics/AI/Encounters
  - Assets/Tactics/Tests/PlayMode/AiDecisionComponentTests.cs
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:dc40a65a2b1a2a545f3f28b252b65ddf48b2bf3e28e85072499365875ae4ff25
---

# Current State

`AiContextBuilder` 构建战斗快照，`IntentGenerator` 生成“可达站位 × 合法技能目标点”候选，`RuleFilter` 执行硬门禁，`IntentScorer` 评分，`IntentResolver` 稳定选取结果，`IntentExecutor` 执行移动与技能计划。

玩家预览、AI 和执行前重验证共享 `IAbilityTargetingProvider`，AI 候选还自然消费能力统一的 `CanPerform`/可用性门禁；因此 `MaxUsesPerTurn` 达到本回合上限后不会继续生成或执行该技能，无需 AI 维护独立次数。`AiBrainAsset` 组合 `AiDecisionGraph`、`AIProfile` 和可选 Pattern；Pattern 游标按单位保存，失败或 Generic fallback 不推进。Brain 可选配置偏好战斗距离和过近时的重定位优先级；关闭时不改变旧 Brain 候选与评分。

单位朝向属于共享战斗状态而不是 AI 私有状态。AI 成功移动后按路径最后一步转向，成功执行目标技能后朝向目标；失败执行恢复原朝向。AI 不在回合末自动面向最近敌人，也不显示独立箭头。方向型技能后续直接读取这一共享朝向与 `SkillTargetingProtocol`。

Pure Run 的 Charger、Ranged、AOE、Support、EliteCharger 与 ElitePoisonCaster 各自绑定独立 Brain/Profile。三个正式怪物技能当前均为每回合最多成功使用 1 次：Charge Strike 法力 0、基础伤害 8；Area Blast 法力 0、基础伤害 6；Heavy Shot 法力 8、基础伤害 6。Charger 贴近并强化技能效果，Ranged 维持 3–5 格；正式 Ranged 配方最低起始法力为 15，HunterBlue 的 Intelligence 为 5，使首次 Heavy Shot 后的回合末回蓝足以支持下一回合继续支付。AOE 提高覆盖评分，Support 提高减益评分；两个 Elite 通过固定 Pattern 顺序执行高威胁技能并在不合法时回退 Generic AI。旧 `BasicMeleeBrain` 仍服务未迁移内容，FireDemon 继续使用 2–3 格偏好距离。

通用单位资源规则在自身回合结束时按 Intelligence 回复 MP、受 MaxMana 限制；因此敌方与召唤物同样遵循一次回合结束回蓝，AI 的可支付技能候选在下一次决策前读取该更新后的 MP。

SkillGraph AI 元数据会从范围收集节点识别 AOE，并从 Harmful Buff 或死灵诅咒节点识别 Debuff。候选进入评分前拒绝会造成友军伤害的 AOE 中心，也拒绝向已持有同一负面状态/诅咒类别的目标重复施加 Debuff；这两项是硬合法性约束，不依赖权重碰巧压低分数。`ScoreNode.Parameter` 已序列化但仍未由 `IntentScorer` 消费。

亚马逊诱饵沿用普通敌人的候选生成与合法性，不建立专用 Brain。若当前生成结果中存在可达诱饵的移动或攻击候选，`IntentGenerator` 会只保留这些诱饵候选；没有可达诱饵候选时继续使用正常敌方目标，避免不可达诱饵令 AI 停摆。

低生命撤退只在敌人已经进入其即时攻击包络时生成；拉开一个安全间距后必须恢复正常接敌，避免大地图上永久风筝导致玩家输入旅程无法自然结束。AI 行动队列在选择前和每个异步等待后都会过滤已被同步致死销毁的单位。

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
