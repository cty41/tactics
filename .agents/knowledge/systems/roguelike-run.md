---
type: Game System
resource: https://github.com/cty41/tactics/tree/main/Assets/Tactics/Scripts/RoguelikeMap
title: Roguelike Run
description: FTL式地图、节点交互、冒险状态和纯Run三人小队成长的系统综合入口。
tags: [gameplay, roguelike, map, progression]
timestamp: "2026-07-14T00:00:00+08:00"
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
---

# Current State

地图由 `RoguelikeMapConfig` 和 `RoguelikeMapGenerator` 生成，运行时状态、地图 UI、节点交互、事件、商店、休息和奖励模块已经形成独立链路。`PlayerAdventureState` 保存 run 所需的队伍和资源状态。

产品设计采用固定三人小队、FTL 式自由星图和 run 内成长；核心成长不带出到局外。

# Relationships

- 战斗节点进入[Battle System](battle.md)，结算后回到地图推进。
- 职业技能成长通过[First Slice Three-Class Skills](../plans/first-slice-three-class-skills.md)逐步落地。
- 运行时或资产修改遵循[Unity Agent Workflow](../operations/unity-agent-workflow.md)。

# Known Boundary

纯 Run 设计文档同时包含已定规则、候选方案和 TODO。实现判断必须核对当前地图配置、冒险状态字段、结算代码和测试，而不能把整份设计稿视为全部已完成。

# Citations

[1] [Pure run squad prototype design](https://github.com/cty41/tactics/blob/d5f1730d35278e1811cac744a9e1b242eece27e8/.agents/docs/2026-06-24-pure-run-squad-prototype-design.md)
[2] [Roguelike map gameplay design](https://github.com/cty41/tactics/blob/d5f1730d35278e1811cac744a9e1b242eece27e8/.agents/docs/roguelike-map-gameplay-design.md)
[3] [Roguelike map runtime](https://github.com/cty41/tactics/tree/d5f1730d35278e1811cac744a9e1b242eece27e8/Assets/Tactics/Scripts/RoguelikeMap)
