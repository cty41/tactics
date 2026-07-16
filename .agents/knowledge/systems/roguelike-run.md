---
type: Game System
resource: https://github.com/cty41/tactics/tree/main/Assets/Tactics/Scripts/RoguelikeMap
title: Roguelike Run
description: 7 层只前进地图、节点交互、冒险状态和三人小队局内成长主链。
tags: [gameplay, roguelike, map, progression]
timestamp: "2026-07-15T10:44:04+08:00"
status: active
catalog_scope: roguelike-run
repo_paths:
  - .agents/docs/2026-06-24-pure-run-squad-prototype-design.md
  - .agents/docs/roguelike-event-editor-design.md
  - .agents/docs/roguelike-map-editor-manual-test.md
  - Assets/Tactics/Scripts/Common/RoguelikeMapGenerator.cs
  - Assets/Tactics/Scripts/Roguelike/RoguelikeMapRuntimeState.cs
  - Assets/Tactics/Scripts/Common/Roster/PlayerAdventureState.cs
  - Assets/Tactics/Scripts/UI/RoguelikeMapUIController.cs
  - Assets/Tactics/Scripts/Editor/RoguelikeEventEditor
  - Assets/Tactics/RoguelikeMap/MapConfigs/DefaultRogueLikeMapConfig.asset
  - Assets/Tactics/Tests/Editor/RoguelikeMapEditorTests.cs
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:d8e53a4c62644be88e719c29d56fbdedba90d06d4885e03ebdf5199ad4829edb
---

# Current State

Pure Run v1 由 `RoguelikeMapGenerator.GetPureRunMap` 生成 7 层只前进结构，单局实际战斗数为 5、6 或 7；第 4、6 层为战斗/休息/商店竞争层。节点沿 outgoing 揭示，已访问节点不会重新可选。运行时持久化 run seed、当前层、胜场和节点进度。

`CreatePureRunState` 建立法师、死灵法师和亚马逊固定三人队，等级 1、七项基础属性 5。每次胜利只让一名最低等级存活角色获得 1 级和 1 属性；起始分支主属性达到 7 时，高级技能有一次候选保底。

事件编辑器当前支持 UI Toolkit 图编辑、Inspector、Preview、搜索、连线、删除及 JSON 导入导出；进阶编辑效率和专用测试仍属于缺口。

# Relationships

- 战斗节点进入[Battle System](battle.md)并在结算后返回地图。
- 技能成长由当前三职业目录与[SkillGraph](skill-graph.md)承接。
- 地图 seed、成长和节点状态可由[Gameplay Test Framework](gameplay-test-framework.md)验证。
- 未实施的内容扩展与编辑器增强见[Project Known Gaps](../plans/project-known-gaps.md)。

# Verification Guidance

实现判断核对地图生成、运行状态、结算代码、配置资产和测试。地图/事件编辑器人工验收使用可复现操作与状态结果，不使用截图证明功能。

# Citations

[1] [Pure Run design](https://github.com/cty41/tactics/blob/main/.agents/docs/2026-06-24-pure-run-squad-prototype-design.md)
[2] [Roguelike runtime](https://github.com/cty41/tactics/tree/main/Assets/Tactics/Scripts/RoguelikeMap)
