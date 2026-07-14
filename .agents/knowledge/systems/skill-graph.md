---
type: Game System
resource: https://github.com/cty41/tactics/tree/main/Assets/Tactics/Scripts/Common/Skills/Graph
title: SkillGraph
description: 技能资产、运行时解释器、Ability 桥接、AI识别和自动化验证的当前主链。
tags: [gameplay, skills, skill-graph, unity]
timestamp: "2026-07-14T00:00:00+08:00"
status: active
catalog_scope: skill-graph
repo_paths:
  - .agents/docs/skill-graph-editor-design.md
  - Assets/Tactics/Scripts/Common/Skills/Graph/SkillGraphAsset.cs
  - Assets/Tactics/Scripts/Common/Skills/Graph/SkillGraphRunner.cs
  - Assets/Tactics/Scripts/Common/Units/abilities/SkillGraphAbilityImpl.cs
  - Assets/Tactics/Battle/Abilities/SkillGraphs
  - Assets/Tactics/Tests/PlayMode/SkillGraphRuntimeTests.cs
  - Assets/Tactics/Tests/PlayMode/FirstSliceSkillAssetTests.cs
verified_revision: d5f1730d3527
---

# Current State

`SkillGraphAsset` 是技能执行逻辑的编辑态资产；`SkillGraphRunner.Execute` 解释节点图；`SkillGraphAbilityImpl` 将图能力接入现有 `IAbility` 和 AI 可执行能力接口。项目已经包含多组真实 SkillGraph、AbilityConfig 资产以及运行时、保护机制和首切资产 PlayMode 测试。

# Relationships

- [Battle System](battle.md)提供单位、格子、目标和效果结算环境。
- [Monster AI](monster-ai.md)通过能力元数据和 `IAiExecutableAbility` 选择并执行技能。
- [First Slice Three-Class Skills](../plans/first-slice-three-class-skills.md)以 SkillGraph 作为三职业首批技能的主要落地形式。

# Verification Guidance

涉及某个技能的范围、目标、伤害、Buff 或投射物语义时，必须同时检查：

1. 对应 `SkillGraphAsset`。
2. 对应 AbilityConfig 和节点执行器。
3. 相关 PlayMode 测试或 gameplay spec。

设计文档描述目标架构，但不能代替当前资产和运行时代码。

# Citations

[1] [SkillGraph editor design](https://github.com/cty41/tactics/blob/d5f1730d35278e1811cac744a9e1b242eece27e8/.agents/docs/skill-graph-editor-design.md)
[2] [SkillGraphRunner](https://github.com/cty41/tactics/blob/d5f1730d35278e1811cac744a9e1b242eece27e8/Assets/Tactics/Scripts/Common/Skills/Graph/SkillGraphRunner.cs)
[3] [FirstSliceSkillAssetTests](https://github.com/cty41/tactics/blob/d5f1730d35278e1811cac744a9e1b242eece27e8/Assets/Tactics/Tests/PlayMode/FirstSliceSkillAssetTests.cs)
