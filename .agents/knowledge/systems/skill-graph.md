---
type: Game System
resource: https://github.com/cty41/tactics/tree/main/Assets/Tactics/Scripts/Common/Skills/Graph
title: SkillGraph
description: 技能资产、解释器、Ability 桥接、共享目标规则和 Agent-first 创作验证主链。
tags: [gameplay, skills, skill-graph, unity]
timestamp: "2026-07-22T23:53:50+08:00"
status: active
catalog_scope: skill-graph
repo_paths:
  - .agents/docs/skill-graph-system.md
  - .agents/skills/skill-graph-creation/SKILL.md
  - Assets/Tactics/Scripts/Common/Skills/Graph/SkillGraphAsset.cs
  - Assets/Tactics/Scripts/Common/Skills/Graph/SkillGraphRunner.cs
  - Assets/Tactics/Scripts/Common/Skills/Graph/SkillGraphSpec.cs
  - Assets/Tactics/Scripts/Common/Skills/Graph/SkillTargetingProtocol.cs
  - Assets/Tactics/Scripts/Common/Skills/Graph/OrderedTargetSelectionState.cs
  - Assets/Tactics/Scripts/Editor/SkillGraphEditor/SkillGraphSpecCompiler.cs
  - Assets/Tactics/Scripts/Editor/MCP/SkillGraphMcpTools.cs
  - Assets/Tactics/Scripts/Common/Units/abilities/SkillGraphAbilityImpl.cs
  - Assets/Tactics/Battle/Abilities/SkillGraphs
  - Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Fireball_Lv1_Ability.asset
  - Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Fireball_Lv2_Ability.asset
  - Assets/Tactics/Tests/PlayMode/SkillGraphRuntimeTests.cs
  - Assets/Tactics/Tests/PlayMode/FirstSliceSkillAssetTests.cs
  - Assets/Tactics/Tests/PlayMode/MageSkillLevelTests.cs
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:061429ee5ba7a3b0810df7b8166aebae636559ede5c520a4bb312eec7fb8a97b
---

# Current State

`SkillGraphAsset` 保存编辑态节点图，`SkillGraphRunner` 解释执行，`SkillGraphAbilityImpl` 接入既有 `IAbility`、共享 targeting 和计划执行接口。玩家预览、AI 候选及执行前重验证复用射程、阵营、AOE 展开和 LOS 结论；多目标 AOE 只执行一次图并扣除一次资源。

Unity 图编辑器支持创建、连线、属性编辑、搜索和校验。Agent 可通过 `SkillGraphSpec`、`SkillGraphSpecCompiler` 与 `SkillGraphSpecAutoFixer` 建立结构化输入，并使用 MCP 工具生成、校验和应用资产；运行语义继续由 Gameplay Test/PlayMode 测试证明。

`SkillTargetingProtocol` 在图资产上统一表达主目标、任意格中心、方向扇形、有序多段目标、实体对象格、回收动作和无路径移动；`OrderedTargetSelectionState` 维护分段选择、重复拒绝、取消上一段与完成条件。玩家输入、AI 与 Gameplay Test 可消费同一协议，不各自推导一套阶段规则。

结构化入口将该协议保存在 `SkillGraphSpec.Targeting`；Spec 编译、克隆和导出完整往返全部 targeting 字段，保证 MCP/JSON 重建后语义不丢失。

节点集合现包含 `ApplyMana`、`RemoveHarmfulBuffs` 与法师等级语义节点 `MageSkill`，`SelectAlly` 可显式允许自身成为合法友军目标。伤害节点分别保存伤害大类和元素；`ApplyBuff.RequiresSuccessfulHit` 只在明确的命中附带状态上读取前一伤害节点结果，独立 Buff 不受历史命中结果污染。`SummonUnit` 可声明召唤物是否接受普通治疗，并通过 `SummonRegistry` 按召唤者、类别、上限和创建顺序管理最早替换；骷髅与骷髅法师关闭普通治疗，火魔保持开启。运行时能力可注入使用策略与可用性策略：策略负责额外合法性、稳定禁用原因、动态显示名和成功完成后的资源提交；图失败时不会扣除资源，临时朝向也会恢复。Pure Run 消耗品使用该边界实现明确友军目标、每名角色每轮一次，并在图完成后提交对应独立实例。

法师等级链使用独立 AbilityConfig/SkillGraph：火球术 Lv1 单体、Lv2 十字溅射、Lv3 先引爆主目标旧点燃；寒冰箭 Lv3 增加一次稳定最近目标反弹；霹雳闪电为无 projectile/LOS 的瞬时直击；召唤火魔支持原子批量替换；冰甲 Lv2 对相邻近战攻击者附加 Slow；瞬移 Lv2 取消可见性要求。资产目录校验约束“已发布等级连续且可加载”，法师已完成 1..MaxLevel 发布，其他职业将在对应切片完成。

# Relationships

- [Battle System](battle.md)提供单位、格子、目标和效果环境。
- [Monster AI](monster-ai.md)通过共享合法性和执行接口选择技能。
- [Gameplay Test Framework](gameplay-test-framework.md)验证目标、阶段、状态与投射物结果。
- [Roguelike Run](roguelike-run.md)使用运行时 SkillGraph 模板执行战斗消耗品。
- 三职业首批技能的完成记录保留在[Archived Outcome](../plans/first-slice-three-class-skills.md)。
- 后续静态校验增强见[Project Known Gaps](../plans/project-known-gaps.md)。

# Verification Guidance

验证单个技能时同时检查 `SkillGraphAsset`、AbilityConfig/节点执行器和对应 PlayMode 或 gameplay spec。Unity 资产通过编辑器、MCP 或项目资产工具修改，不直接写 YAML。

# Citations

[1] [SkillGraph runtime](https://github.com/cty41/tactics/tree/main/Assets/Tactics/Scripts/Common/Skills/Graph)
[2] [SkillGraph MCP tools](https://github.com/cty41/tactics/blob/main/Assets/Tactics/Scripts/Editor/MCP/SkillGraphMcpTools.cs)
[3] [FirstSliceSkillAssetTests](https://github.com/cty41/tactics/blob/main/Assets/Tactics/Tests/PlayMode/FirstSliceSkillAssetTests.cs)
