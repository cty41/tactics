---
type: Game System
resource: https://github.com/cty41/tactics/tree/main/Assets/Tactics/Scripts/Common/Skills/Graph
title: SkillGraph
description: 技能资产、解释器、Ability 桥接、共享目标规则和 Agent-first 创作验证主链。
tags: [gameplay, skills, skill-graph, unity]
timestamp: "2026-07-22T11:45:14+08:00"
status: active
catalog_scope: skill-graph
repo_paths:
  - .agents/docs/skill-graph-system.md
  - .agents/skills/skill-graph-creation/SKILL.md
  - Assets/Tactics/Scripts/Common/Skills/Graph/SkillGraphAsset.cs
  - Assets/Tactics/Scripts/Common/Skills/Graph/SkillGraphRunner.cs
  - Assets/Tactics/Scripts/Common/Skills/Graph/SkillGraphSpec.cs
  - Assets/Tactics/Scripts/Editor/SkillGraphEditor/SkillGraphSpecCompiler.cs
  - Assets/Tactics/Scripts/Editor/MCP/SkillGraphMcpTools.cs
  - Assets/Tactics/Scripts/Common/Units/abilities/SkillGraphAbilityImpl.cs
  - Assets/Tactics/Battle/Abilities/SkillGraphs
  - Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Fireball_Lv1_Ability.asset
  - Assets/Tactics/Battle/Abilities/SkillGraphAbilityConfigs/Fireball_Lv2_Ability.asset
  - Assets/Tactics/Tests/PlayMode/SkillGraphRuntimeTests.cs
  - Assets/Tactics/Tests/PlayMode/FirstSliceSkillAssetTests.cs
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:5b7f5b699d63099fc3447c5a0525d3ea2ce37de6c867cf14f3723eacdc2a3c52
---

# Current State

`SkillGraphAsset` 保存编辑态节点图，`SkillGraphRunner` 解释执行，`SkillGraphAbilityImpl` 接入既有 `IAbility`、共享 targeting 和计划执行接口。玩家预览、AI 候选及执行前重验证复用射程、阵营、AOE 展开和 LOS 结论；多目标 AOE 只执行一次图并扣除一次资源。

Unity 图编辑器支持创建、连线、属性编辑、搜索和校验。Agent 可通过 `SkillGraphSpec`、`SkillGraphSpecCompiler` 与 `SkillGraphSpecAutoFixer` 建立结构化输入，并使用 MCP 工具生成、校验和应用资产；运行语义继续由 Gameplay Test/PlayMode 测试证明。

节点集合现包含 `ApplyMana` 与 `RemoveHarmfulBuffs`，`SelectAlly` 可显式允许自身成为合法友军目标。`SummonUnit` 可声明召唤物是否接受普通治疗，骷髅与骷髅法师关闭、火焰恶魔保持开启。运行时能力可注入使用策略：策略负责额外合法性、动态显示名和成功完成后的资源提交；图失败时不会扣除资源。Pure Run 消耗品使用该边界实现明确友军目标、每名角色每轮一次，并在图完成后提交对应独立实例。

Pure Run 的首条等级资产链使用独立 AbilityConfig/SkillGraph：火球术 Lv1 保持单体伤害且无 AOE；Lv2 对主目标追加直接伤害，并对命中格与正交相邻敌人造成十字溅射，友军和斜角目标不受伤。资产目录校验当前约束“已发布等级连续且可加载”；完整 1..MaxLevel 发布将在对应职业切片完成。

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
