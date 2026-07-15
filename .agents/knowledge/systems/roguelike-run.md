---
type: Game System
resource: https://github.com/cty41/tactics/tree/main/Assets/Tactics/Scripts/RoguelikeMap
title: Roguelike Run
description: FTL式地图、节点交互、冒险状态和纯Run三人小队成长的系统综合入口。
tags: [gameplay, roguelike, map, progression]
timestamp: "2026-07-14T23:27:29+08:00"
status: active
catalog_scope: roguelike-run
repo_paths:
  - .agents/docs/2026-06-24-pure-run-squad-prototype-design.md
  - .agents/docs/roguelike-map-gameplay-design.md
  - Assets/Tactics/Scripts/Common/RoguelikeMapGenerator.cs
  - Assets/Tactics/Scripts/Roguelike/RoguelikeMapRuntimeState.cs
  - Assets/Tactics/Scripts/Common/Roster/PlayerAdventureState.cs
  - Assets/Tactics/Scripts/UI/RoguelikeMapUIController.cs
  - Assets/Tactics/RoguelikeMap/MapConfigs/DefaultRogueLikeMapConfig.asset
  - Assets/Tactics/Tests/Editor/RoguelikeMapEditorTests.cs
verified_revision: d5f1730d3527
source_fingerprint: sha256:fa9001e6f81de2611327d6d630cc6221d897541d1d6d9958b5dc344a9945351b
---

# Current State

Pure Run v1 由 `RoguelikeMapGenerator.GetPureRunMap` 生成 7 层只前进地图，实际战斗数为 5、6 或 7；节点只沿 outgoing 揭示，已访问节点不会重新可选。运行时状态持久化 run seed、当前层、胜场和节点进度。

`CreatePureRunState` 建立法师、死灵法师和亚马逊固定三人队，等级 1 且七项基础属性为 5。每次胜利只升级一名最低等级存活角色；主属性达到 7 时，起始分支高级技能拥有一次候选保底。核心成长不带出到局外。

# Relationships

- 战斗节点进入[Battle System](battle.md)，结算后回到地图推进。
- 职业技能成长通过[First Slice Three-Class Skills](../plans/first-slice-three-class-skills.md)逐步落地。
- 地图 seed 与角色成长由[Gameplay Test Framework](gameplay-test-framework.md)提供自动化断言。
- 运行时或资产修改遵循[Unity Agent Workflow](../operations/unity-agent-workflow.md)。

# Known Boundary

纯 Run 设计文档同时包含已定规则、候选方案和 TODO。实现判断必须核对当前地图配置、冒险状态字段、结算代码和测试，而不能把整份设计稿视为全部已完成。

# Citations

[1] [Pure run squad prototype design](https://github.com/cty41/tactics/blob/d5f1730d35278e1811cac744a9e1b242eece27e8/.agents/docs/2026-06-24-pure-run-squad-prototype-design.md)
[2] [Roguelike map gameplay design](https://github.com/cty41/tactics/blob/d5f1730d35278e1811cac744a9e1b242eece27e8/.agents/docs/roguelike-map-gameplay-design.md)
[3] [Roguelike map runtime](https://github.com/cty41/tactics/tree/d5f1730d35278e1811cac744a9e1b242eece27e8/Assets/Tactics/Scripts/RoguelikeMap)
