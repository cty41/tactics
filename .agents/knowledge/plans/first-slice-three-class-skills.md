---
type: Development Plan
resource: https://github.com/cty41/tactics/blob/d5f1730d35278e1811cac744a9e1b242eece27e8/.agents/plans/2026-06-24-first-slice-three-class-skills-plan.md
title: First Slice Three-Class Skills
description: 三职业首批 18 技能、升级门槛与候选链路的已完成实施成果。
tags: [plan, skills, first-slice, progression]
timestamp: "2026-07-15T12:00:00+08:00"
status: archived
catalog_scope: first-slice-three-class-skills
repo_paths:
  - .agents/docs/three-class-skill-design.md
  - .agents/docs/2026-06-24-pure-run-squad-prototype-design.md
  - Assets/Tactics/Scripts/Common/Battle/FirstSliceSkillCatalog.cs
  - Assets/Tactics/Battle/Abilities/SkillGraphs
  - Assets/Tactics/Tests/PlayMode/FirstSliceSkillCatalogTests.cs
  - Assets/Tactics/Tests/PlayMode/FirstSliceSkillAssetTests.cs
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:36a0cf2d06c6997b451c0fe52f1d6e2261e02b40255f42d6193c5358dd9f7a95
---

# Archived Outcome

法师、死灵法师和亚马逊的 9 个基础/9 个高级技能已经进入 `FirstSliceSkillCatalog`、SkillGraph/AbilityConfig 资产和对应测试。属性门槛、前置技能、等级上限、升级候选与高级技能一次保底已进入当前实现。

原开发计划已完成并从活跃计划目录删除。当前技能规则以 [三职业首批技能设计](https://github.com/cty41/tactics/blob/main/.agents/docs/three-class-skill-design.md)、技能目录、资产和测试为准；本页仅保留实施成果与历史入口。

# Relationships

- 技能执行依赖[SkillGraph](../systems/skill-graph.md)。
- 升级与 Run 内成长属于[Roguelike Run](../systems/roguelike-run.md)。
- 技能结果由[Battle System](../systems/battle.md)和[Gameplay Test Framework](../systems/gameplay-test-framework.md)验证。

# Citations

[1] [Historical implementation plan](https://github.com/cty41/tactics/blob/d5f1730d35278e1811cac744a9e1b242eece27e8/.agents/plans/2026-06-24-first-slice-three-class-skills-plan.md)
[2] [FirstSliceSkillCatalogTests](https://github.com/cty41/tactics/blob/main/Assets/Tactics/Tests/PlayMode/FirstSliceSkillCatalogTests.cs)
[3] [FirstSliceSkillAssetTests](https://github.com/cty41/tactics/blob/main/Assets/Tactics/Tests/PlayMode/FirstSliceSkillAssetTests.cs)
