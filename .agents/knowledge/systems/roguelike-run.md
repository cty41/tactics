---
type: Game System
resource: https://github.com/cty41/tactics/tree/main/Assets/Tactics/Scripts/RoguelikeMap
title: Roguelike Run
description: 7 层只前进地图、节点交互、冒险状态和三人小队局内成长主链。
tags: [gameplay, roguelike, map, progression]
timestamp: "2026-07-23T10:32:34+08:00"
status: active
catalog_scope: roguelike-run
repo_paths:
  - .agents/docs/2026-06-24-pure-run-squad-prototype-design.md
  - .agents/docs/roguelike-event-editor-design.md
  - .agents/docs/roguelike-map-editor-manual-test.md
  - Assets/Tactics/Scripts/Common/RoguelikeMapGenerator.cs
  - Assets/Tactics/Scripts/Roguelike/RoguelikeMapRuntimeState.cs
  - Assets/Tactics/Scripts/Roguelike/PureRunSessionStore.cs
  - Assets/Tactics/Scripts/Roguelike/PureRunSummaryRecorder.cs
  - Assets/Tactics/Scripts/Common/Roster/PlayerAdventureState.cs
  - Assets/Tactics/Scripts/Common/Roster/CharacterDefinition.cs
  - Assets/Tactics/Scripts/Common/Roster/PlayerAdventureStateStore.cs
  - Assets/Tactics/Scripts/UI/RoguelikeMapUIController.cs
  - Assets/Tactics/Scripts/UI/InventoryUIController.cs
  - Assets/Tactics/Scripts/UI/LevelUpPanelController.cs
  - Assets/Tactics/Arts/UI/Inventory.uxml
  - Assets/Tactics/Arts/UI/Inventory.uss
  - Assets/Tactics/Scripts/Editor/RoguelikeEventEditor
  - Assets/Tactics/RoguelikeMap/MapConfigs/DefaultRogueLikeMapConfig.asset
  - Assets/Tactics/Tests/Editor/RoguelikeMapEditorTests.cs
verified_revision: c56d71ad4ebd
source_fingerprint: sha256:51180c5dc37239a583a7968135fbec9bbb46a2395d0dc8f0572cb9aeee9cb756
---

# Current State

Pure Run v1 由 `RoguelikeMapGenerator.GetPureRunMap` 生成 7 层只前进地图，单局实际战斗数为 5、6 或 7；第 4、6 层均在战斗、休息、商店和随机事件之间四选一。节点沿 outgoing 揭示，已访问节点不会重新可选。地图布局版本为 2。

Demo 使用单一全局 Run，不经过三存档槽。`PureRunSessionStore` 将版本 5 冒险状态与地图作为配对数据保存；Home 提供 New Run 和 Continue Run。普通战斗胜利结算后回到地图，失败或 Boss 胜利显示 RunEndSummary 并清理本局状态。

Pure Run 的 Mystery、Rest 与 Store 通过持久化节点事务保护中断恢复。事务按 `Entered → Resolved → Committed` 推进，并在冒险状态中记录已应用的奖励键：效果结算前先保存 `Resolved` 快照，重入时恢复同一结果并只补发尚未应用的效果，只有继续/关闭等明确完成动作才提交和消费节点；商店按商品使用独立购买键，因此崩溃或返回地图后不会重复扣款或重复发货。

第 4、6 层的两个 Mystery 节点会由 run seed 确定性分配互不重复的正式事件，并在旧存档缺失分配时补全而不覆盖已有值。正式事件为诅咒宝箱、堕落祭坛和迷途村民；选项使用稳定 ID，检定概率以属性 5 为基准并限制在 5%–95%，检定投点由 run seed、节点 ID 和选项 ID 共同决定，因而重入不会重掷。事件结果页可跨重载恢复，事件致死会在提交后进入战败结算。

`CreatePureRunState` 建立法师、死灵法师和亚马逊固定三人队，等级 1、七项基础属性 5。每次胜利只让一名最低等级存活角色获得 1 级和 1 属性；起始分支主属性达到 7 时，高级技能有一次候选保底。

Pure Run 存档修复将已知旧等级技能 ID 迁移为稳定逻辑 ID、合并重复记录并保留最高等级。拥有投掷系技能的 Amazon 会幂等获得不占槽的 `amazon.pickup_spear` 持久化记录；其实际战斗拾取行为仍属于 Amazon 技能切片。角色 Lv2 起即可同时看到合法新技能和已学技能的已发布下一等级，选择升级后同一 `LearnedSkill` 等级会进入下一场战斗绑定。

LevelUp 面板按实际 `LearnedSkill.Level` 显示当前技能和混合候选，候选明确区分 Lv1 新技能与下一等级升级，并读取对应等级资产描述。Inventory 的技能区是只读视图：主动技能优先、被动技能随后，显示实际等级；点击仅打开详情 popover，不提供装配、卸载或替换操作，`ExtraUtility` 与其他地图隐藏技能不显示。

Inventory、LevelUp、BattleSettlement 与 RunEndSummary 在每次显示时重新绑定当前 UIDocument 的 VisualElement 树，并在隐藏时注销回调、清除旧树引用。Inventory 因而可以在同一缓存实例上反复打开，且会重新读取隐藏期间发生的角色和背包状态变化。

地图层待生效 Buff 快照除名称、持续时间和正负面外，还持久化效果/触发类型、诅咒分类、周期伤害、伤害大类、元素、刷新策略、速度修正和减伤比例；进入战斗时按这些字段还原运行时配置。旧存档缺失伤害大类时按 `Magic` 补全，避免升级后改变既有事件 Buff 的语义。

事件编辑器当前支持 UI Toolkit 图编辑、Inspector、Preview、搜索、连线、删除及 JSON 导入导出；进阶编辑效率和专用测试仍属于缺口。

消耗品按定义、加权池和独立实例三层组织。首批为 `1/1` 的生命药剂、魔法药剂和净化药水；角色各有 1 个携带槽，未携带实例与装备共同显示在统一 Inventory，并通过单击 popover 执行携带、装备、一步替换或卸下。角色死亡在战斗结算或事件应用后自动卸下全部装载。

新局不自带消耗品。普通/精英胜利分别按 25%/30% 概率掉落，Boss 不掉落；每个商店确定性展示 3 件商品、至少 1 件且不重复同一种药水，事件只在配置明确指定时发放。获得反馈显示名称与次数，地图顶部没有消耗品总数或新物品角标。

进行中的 `RunSummary` 与 Pure Run 状态共同持久化，所有奖励、节点和击杀流水都使用稳定 transaction key 去重。`totalGold` 只累计实际提交的正向金币，不因购买扣款回退；获得过的装备和物品记录稳定 ID，即使后来装备、花费或使用仍保留。节点仅在提交完成时计数，Mystery 同时增加事件数，正式敌人死亡只在玩家胜利结算时写入。

战斗失败、Boss 胜利和 Mystery 导致的全灭统一先从进行中统计生成结局快照，再清除活动 session。RunEndSummary 读取该快照并解析装备/消耗品显示名，关闭总结时才消费快照；因此 UI 不依赖已经清理的角色背包或地图运行时状态。

自动化真实路线从 Home 的 New Run 入口开始，经五场自然战斗胜利、一次显式技能升级、商店购买和存档重载抵达 Boss，过程中不使用直接节点完成或伪造战果。自然战斗团灭与 Mystery 事件团灭是独立失败路线；最终仅把 Unity Editor 内的整体视觉、手感和连续操作体验保留给人工验收。

# Relationships

- 战斗节点进入[Battle System](battle.md)并在结算后返回地图。
- 技能成长由当前三职业目录与[SkillGraph](skill-graph.md)承接。
- 地图 seed、成长和节点状态可由[Gameplay Test Framework](gameplay-test-framework.md)验证。
- 战斗内消耗品通过[SkillGraph](skill-graph.md)复用目标合法性和效果执行。
- 未实施的内容扩展与编辑器增强见[Project Known Gaps](../plans/project-known-gaps.md)。

# Verification Guidance

实现判断核对地图生成、运行状态、节点事务、结算代码、配置资产和测试。Mystery/Rest/Store 的自动化验证必须覆盖 Resolved 状态重载、奖励幂等和最终提交；最终玩家流人工验收使用可复现操作与状态结果，不使用截图证明功能。

# Citations

[1] [Pure Run design](https://github.com/cty41/tactics/blob/main/.agents/docs/2026-06-24-pure-run-squad-prototype-design.md)
[2] [Roguelike runtime](https://github.com/cty41/tactics/tree/main/Assets/Tactics/Scripts/RoguelikeMap)
