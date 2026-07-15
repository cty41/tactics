---
type: Development Plan
resource: https://github.com/cty41/tactics/blob/main/.agents/plans/2026-06-24-first-slice-three-class-skills-plan.md
title: First Slice Three-Class Skills
description: 三职业基础技能、首批高级技能、候选过滤和最小升级UI的首切计划综合页。
tags: [plan, skills, first-slice, progression]
timestamp: "2026-07-14T23:27:16+08:00"
status: active
catalog_scope: first-slice-three-class-skills
repo_paths:
  - .agents/plans/2026-06-24-first-slice-three-class-skills-plan.md
  - .agents/docs/2026-06-24-pure-run-squad-prototype-design.md
  - Assets/Tactics/Scripts/Common/Battle/FirstSliceSkillCatalog.cs
  - Assets/Tactics/Battle/Abilities/SkillGraphs
  - Assets/Tactics/Tests/PlayMode/FirstSliceSkillCatalogTests.cs
  - Assets/Tactics/Tests/PlayMode/FirstSliceSkillAssetTests.cs
verified_revision: d5f1730d3527
source_fingerprint: sha256:36a0cf2d06c6997b451c0fe52f1d6e2261e02b40255f42d6193c5358dd9f7a95
---

# Goal

以法师、死灵法师和亚马逊女战士为第一批职业，打通基础技能分支、首批高级技能、技能前置与属性门槛、升级候选和最小 UI 主链。

# Current Evidence

项目已经存在 `FirstSliceSkillCatalog`、三职业相关 SkillGraph/AbilityConfig 资产及对应 Catalog/Asset PlayMode 测试。Pure Run 初始化会为三职业稳定选择起始分支；当基础主属性达到 7 时，起始分支高级技能拥有一次候选保底，并通过现有 LevelUp UI 真正学习。原计划没有统一完成状态，因此本概念保持 `active`。

# Relationships

- 技能执行和资产结构依赖[SkillGraph](../systems/skill-graph.md)。
- 升级、队伍和 run 内成长属于[Roguelike Run](../systems/roguelike-run.md)。
- 技能最终效果在[Battle System](../systems/battle.md)中验证。

# Citations

[1] [First slice implementation plan](https://github.com/cty41/tactics/blob/d5f1730d35278e1811cac744a9e1b242eece27e8/.agents/plans/2026-06-24-first-slice-three-class-skills-plan.md)
[2] [FirstSliceSkillCatalogTests](https://github.com/cty41/tactics/blob/d5f1730d35278e1811cac744a9e1b242eece27e8/Assets/Tactics/Tests/PlayMode/FirstSliceSkillCatalogTests.cs)
[3] [FirstSliceSkillAssetTests](https://github.com/cty41/tactics/blob/d5f1730d35278e1811cac744a9e1b242eece27e8/Assets/Tactics/Tests/PlayMode/FirstSliceSkillAssetTests.cs)
