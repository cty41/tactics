---
type: Game System
resource: https://github.com/cty41/tactics/tree/main/Assets/Tactics/Scripts/RoguelikeMap
title: Roguelike Run
description: 7 层只前进地图、节点交互、冒险状态和三人小队局内成长主链。
tags: [gameplay, roguelike, map, progression]
timestamp: "2026-07-16T10:16:51+08:00"
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
source_fingerprint: sha256:ff754942be80393dd1872043284cd1fcb7f54d3b0617dffb5005c60a03bb7e80
---

# Current State

Pure Run v1 由 `RoguelikeMapGenerator.GetPureRunMap` 生成 7 层只前进地图，单局实际战斗数为 5、6 或 7；第 4、6 层均在战斗、休息、商店和随机事件之间四选一。节点沿 outgoing 揭示，已访问节点不会重新可选。地图布局版本为 2。

Demo 使用单一全局 Run，不经过三存档槽。`PureRunSessionStore` 将版本 4 冒险状态与地图作为配对数据保存；Home 提供 New Run 和 Continue Run。普通战斗胜利结算后回到地图，失败或 Boss 胜利显示 RunEndSummary 并清理本局状态。

`CreatePureRunState` 建立法师、死灵法师和亚马逊固定三人队，等级 1、七项基础属性 5。每次胜利只让一名最低等级存活角色获得 1 级和 1 属性；起始分支主属性达到 7 时，高级技能有一次候选保底。

事件编辑器当前支持 UI Toolkit 图编辑、Inspector、Preview、搜索、连线、删除及 JSON 导入导出；进阶编辑效率和专用测试仍属于缺口。

消耗品按定义、加权池和独立实例三层组织，实例保存剩余/最大耐久。首批包含战地口粮、猫薄荷补剂和绷带卷；新局不自带消耗品，可从普通/精英战斗概率掉落、确定性随机事件池和三个固定商店货位获得。地图与背包 UI 展示真实金币、HP、MP、等级、死亡状态和消耗品耐久。

# Relationships

- 战斗节点进入[Battle System](battle.md)并在结算后返回地图。
- 技能成长由当前三职业目录与[SkillGraph](skill-graph.md)承接。
- 地图 seed、成长和节点状态可由[Gameplay Test Framework](gameplay-test-framework.md)验证。
- 战斗内消耗品通过[SkillGraph](skill-graph.md)复用目标合法性和效果执行。
- 未实施的内容扩展与编辑器增强见[Project Known Gaps](../plans/project-known-gaps.md)。

# Verification Guidance

实现判断核对地图生成、运行状态、结算代码、配置资产和测试。地图/事件编辑器人工验收使用可复现操作与状态结果，不使用截图证明功能。

# Citations

[1] [Pure Run design](https://github.com/cty41/tactics/blob/main/.agents/docs/2026-06-24-pure-run-squad-prototype-design.md)
[2] [Roguelike runtime](https://github.com/cty41/tactics/tree/main/Assets/Tactics/Scripts/RoguelikeMap)
